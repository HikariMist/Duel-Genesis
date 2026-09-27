#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DuelGenesis.Cards;
using DuelGenesis.Dueling;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Checks every monster card in the production catalog against the DMO model library:
    /// which model it resolves to, whether that model renders (renderers, textures, URP-safe shaders),
    /// whether it has an idle clip, and how it sits on the table once normalised.
    /// Also renders contact sheets (card face next to its model, posed in its idle clip) so the
    /// card-to-model match can be checked by eye.
    /// Output: Builds/ModelAudit/report.txt, report.csv and sheet_NN.png
    /// </summary>
    public static class MonsterModelAuditTool
    {
        private const string OutputDir = "Builds/ModelAudit";
        private const int Columns = 5;
        private const int Rows = 6;
        private const int FaceW = 200;
        private const int FaceH = 292;
        private const int ModelSize = 292;
        private const int Pad = 10;

        [MenuItem("Duel Genesis/Cards/Audit Monster Models (report + contact sheets)")]
        public static void RunMenu() => Run(renderSheets: true);

        [MenuItem("Duel Genesis/Cards/Audit Monster Models (report only)")]
        public static void RunReportOnly() => Run(renderSheets: false);

        public static void Run(bool renderSheets)
        {
            List<CardData> monsters = LoadMonsters();
            if (monsters.Count == 0)
            {
                EditorUtility.DisplayDialog("Monster Model Audit", "No monster cards found in the catalog.", "OK");
                return;
            }

            Directory.CreateDirectory(OutputDir);
            foreach (string old in Directory.GetFiles(OutputDir, "sheet_*.png")) File.Delete(old);

            var rows = new List<AuditRow>();
            PreviewRenderUtility preview = renderSheets ? CreatePreview() : null;
            Texture2D sheet = null;
            int sheetIndex = 0, cell = 0;
            int cellW = FaceW + ModelSize + Pad * 3;
            int cellH = Mathf.Max(FaceH, ModelSize) + Pad * 2;

            try
            {
                for (int i = 0; i < monsters.Count; i++)
                {
                    CardData card = monsters[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Monster Model Audit", card.cardName, i / (float)monsters.Count))
                        break;

                    AuditRow row = Audit(card, preview, out Texture2D render);
                    rows.Add(row);

                    if (!renderSheets) continue;
                    if (sheet == null)
                    {
                        sheet = new Texture2D(Columns * cellW + Pad, Rows * cellH + Pad, TextureFormat.RGBA32, false);
                        sheet.SetPixels32(Enumerable.Repeat(new Color32(28, 30, 36, 255), sheet.width * sheet.height).ToArray());
                    }

                    int col = cell % Columns;
                    int r = Rows - 1 - cell / Columns;
                    int x0 = Pad + col * cellW;
                    int y0 = Pad + r * cellH;
                    row.Sheet = sheetIndex + 1;
                    row.Cell = cell + 1;

                    Texture2D face = CardFaceCompositor.RenderPreview(card);
                    if (face != null)
                    {
                        Blit(sheet, Scale(face, FaceW, FaceH), x0 + Pad, y0 + Pad);
                        Object.DestroyImmediate(face);
                    }
                    Color status = row.Problems.Count == 0 ? new Color(0.2f, 0.8f, 0.35f) : row.Model == null ? new Color(0.9f, 0.25f, 0.25f) : new Color(0.95f, 0.7f, 0.15f);
                    Fill(sheet, x0 + FaceW + Pad * 2 - 4, y0 + Pad - 4, ModelSize + 8, ModelSize + 8, status);
                    if (render != null)
                    {
                        Blit(sheet, render, x0 + FaceW + Pad * 2, y0 + Pad);
                        Object.DestroyImmediate(render);
                    }
                    else Fill(sheet, x0 + FaceW + Pad * 2, y0 + Pad, ModelSize, ModelSize, new Color(0.12f, 0.12f, 0.14f));

                    cell++;
                    if (cell == Columns * Rows || i == monsters.Count - 1)
                    {
                        sheet.Apply();
                        File.WriteAllBytes(Path.Combine(OutputDir, $"sheet_{sheetIndex + 1:00}.png"), sheet.EncodeToPNG());
                        Object.DestroyImmediate(sheet);
                        sheet = null;
                        cell = 0;
                        sheetIndex++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                preview?.Cleanup();
                if (sheet != null) Object.DestroyImmediate(sheet);
                if (renderSheets) CardFaceCompositor.Shutdown();
            }

            WriteReports(rows, sheetIndex);
        }

        // ------------------------------------------------------------------ audit

        private sealed class AuditRow
        {
            public CardData Card;
            public GameObject Model;
            public string ModelName = "-";
            public string Clip = "-";
            public int Renderers;
            public Vector3 Size;
            public int Sheet, Cell;
            public readonly List<string> Problems = new List<string>();
        }

        private static AuditRow Audit(CardData card, PreviewRenderUtility preview, out Texture2D render)
        {
            render = null;
            var row = new AuditRow { Card = card };
            GameObject prefab = CardModelRegistry.LoadPrefab(card);
            if (prefab == null)
            {
                row.Problems.Add("NO MODEL (card-image hologram fallback)");
                return row;
            }
            row.Model = prefab;
            row.ModelName = prefab.name;

            // Name check: the resolved model must be this card's own character.
            if (CardModelRegistry.NormalizeName(prefab.name) != CardModelRegistry.NormalizeName(card.cardName))
                row.Problems.Add($"name mismatch: card '{card.cardName}' -> model '{prefab.name}'");

            GameObject instance = Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                MonsterHologram.PrepareModel(instance);
                Renderer[] renderers = MonsterHologram.VisibleRenderers(instance.transform);
                row.Renderers = renderers.Length;
                if (renderers.Length == 0) row.Problems.Add("no visible renderers");

                foreach (Renderer r in renderers)
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) { row.Problems.Add($"missing material on {r.name}"); continue; }
                    if (m.shader == null || !m.shader.isSupported || m.shader.name == "Hidden/InternalErrorShader")
                        row.Problems.Add($"unsupported shader on {r.name}");
                    Texture t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                    if (t == null) row.Problems.Add($"untextured material {m.name}");
                }
                if (renderers.OfType<SkinnedMeshRenderer>().Any(s => s.sharedMesh == null))
                    row.Problems.Add("skinned mesh missing");

                AnimationClip[] clips = CardModelRegistry.LoadAnimationClips(card);
                AnimationClip idle = MonsterHologram.PickIdleClip(clips);
                AnimationClip guard = MonsterHologram.PickDefenseClip(clips);
                row.Clip = idle != null ? idle.name + (guard != null ? " / " + guard.name : "") : (clips.Length == 0 ? "none" : "only 0-length poses");
                if (idle != null)
                {
                    Transform animRoot = instance.GetComponentInChildren<Animator>() != null ? instance.GetComponentInChildren<Animator>().transform : instance.transform;
                    idle.SampleAnimation(animRoot.gameObject, idle.length * 0.35f);
                }
                else row.Problems.Add("no animation (static pose)");

                // Normalise exactly as the table does and record the resulting size.
                GameObject holder = new GameObject("Audit Holder") { hideFlags = HideFlags.HideAndDontSave };
                instance.transform.SetParent(holder.transform, false);
                MonsterHologram.Normalize(instance.transform);
                if (renderers.Length > 0)
                {
                    Bounds b = MonsterHologram.CoreBounds(renderers);
                    row.Size = b.size;
                    if (b.size.y < MonsterHologram.TargetHeight * 0.35f)
                        row.Problems.Add($"very flat/short on the card ({b.size.y * 100f:0.0} cm tall)");
                }

                if (preview != null && renderers.Length > 0) render = RenderModel(preview, holder, renderers);
                instance.transform.SetParent(null, false);
                Object.DestroyImmediate(holder);
            }
            catch (System.Exception e)
            {
                row.Problems.Add("exception: " + e.Message);
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
            }
            return row;
        }

        // ------------------------------------------------------------------ rendering

        private static PreviewRenderUtility CreatePreview()
        {
            var p = new PreviewRenderUtility();
            p.camera.fieldOfView = 26f;
            p.camera.nearClipPlane = 0.005f;
            p.camera.farClipPlane = 20f;
            p.camera.clearFlags = CameraClearFlags.SolidColor;
            p.camera.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
            p.lights[0].intensity = 1.25f;
            p.lights[0].transform.rotation = Quaternion.Euler(35f, 150f, 0f);
            p.lights[1].intensity = 0.7f;
            p.lights[1].transform.rotation = Quaternion.Euler(20f, -40f, 0f);
            p.ambientColor = new Color(0.45f, 0.45f, 0.5f);
            return p;
        }

        private static Texture2D RenderModel(PreviewRenderUtility p, GameObject holder, Renderer[] renderers)
        {
            p.AddSingleGO(holder);
            Bounds b = MonsterHologram.CoreBounds(renderers);
            float radius = Mathf.Max(b.extents.magnitude, 0.01f);
            float distance = radius / Mathf.Sin(p.camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            // Three-quarter FRONT view: the side of the model that faces the opponent across the table (+Z).
            Vector3 dir = new Vector3(0.45f, 0.35f, 1f).normalized;
            p.camera.transform.position = b.center + dir * distance;
            p.camera.transform.LookAt(b.center);

            p.BeginStaticPreview(new Rect(0, 0, ModelSize, ModelSize));
            p.Render(true);
            Texture tex = p.EndStaticPreview();
            // EndStaticPreview returns a Texture2D sized to the (possibly DPI-scaled) rect.
            Texture2D result = tex as Texture2D;
            if (result == null) return null;
            Texture2D copy = Scale(result, ModelSize, ModelSize);
            return copy;
        }

        private static Texture2D Scale(Texture2D src, int w, int h)
        {
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            dst.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            dst.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return dst;
        }

        private static void Blit(Texture2D dst, Texture2D src, int x, int y)
        {
            int w = Mathf.Min(src.width, dst.width - x), h = Mathf.Min(src.height, dst.height - y);
            if (w <= 0 || h <= 0) return;
            dst.SetPixels(x, y, w, h, src.GetPixels(0, 0, w, h));
        }

        private static void Fill(Texture2D dst, int x, int y, int w, int h, Color c)
        {
            w = Mathf.Min(w, dst.width - x);
            h = Mathf.Min(h, dst.height - y);
            if (w <= 0 || h <= 0) return;
            dst.SetPixels(x, y, w, h, Enumerable.Repeat(c, w * h).ToArray());
        }

        // ------------------------------------------------------------------ catalog + reports

        private static List<CardData> LoadMonsters()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "duel_genesis_cards.json");
            if (!File.Exists(path)) return new List<CardData>();
            ExternalCardCatalog catalog = JsonUtility.FromJson<ExternalCardCatalog>(File.ReadAllText(path));
            return catalog?.cards?
                .Where(r => r != null && string.Equals(r.kind, "Monster", System.StringComparison.OrdinalIgnoreCase))
                .Select(r => r.ToCardData())
                .OrderBy(c => c.cardName)
                .ToList() ?? new List<CardData>();
        }

        private static void WriteReports(List<AuditRow> rows, int sheets)
        {
            int ok = rows.Count(r => r.Problems.Count == 0);
            int noModel = rows.Count(r => r.Model == null);
            int warnings = rows.Count - ok - noModel;

            var txt = new StringBuilder();
            txt.AppendLine("Duel: Genesis — monster model audit");
            txt.AppendLine($"{rows.Count} monster cards: {ok} OK, {warnings} with warnings, {noModel} without a model (shown as holographic cards).");
            txt.AppendLine($"Contact sheets: {sheets} (Builds/ModelAudit/sheet_NN.png, {Columns}x{Rows} per sheet, reading order; green = OK, amber = warning, red = no model).");
            txt.AppendLine();
            foreach (AuditRow r in rows.Where(r => r.Problems.Count > 0))
                txt.AppendLine($"[{(r.Sheet > 0 ? $"sheet {r.Sheet:00} #{r.Cell:00}" : "")}] {r.Card.cardName} ({r.Card.id}) -> {r.ModelName}: {string.Join("; ", r.Problems.Distinct())}");
            File.WriteAllText(Path.Combine(OutputDir, "report.txt"), txt.ToString());

            var csv = new StringBuilder("sheet,cell,card_id,card_name,model,clip,renderers,height_cm,width_cm,problems\n");
            foreach (AuditRow r in rows)
                csv.AppendLine($"{r.Sheet},{r.Cell},{r.Card.id},\"{r.Card.cardName}\",\"{r.ModelName}\",\"{r.Clip}\",{r.Renderers},{r.Size.y * 100f:0.0},{Mathf.Max(r.Size.x, r.Size.z) * 100f:0.0},\"{string.Join("; ", r.Problems.Distinct())}\"");
            File.WriteAllText(Path.Combine(OutputDir, "report.csv"), csv.ToString());

            string summary = $"{rows.Count} monsters: {ok} OK, {warnings} warnings, {noModel} without a model.";
            Debug.Log("Duel: Genesis monster model audit — " + summary + "\n" + txt);
            EditorUtility.DisplayDialog("Monster Model Audit", summary + "\n\nReport + sheets: " + Path.GetFullPath(OutputDir), "OK");
        }
    }
}
#endif
