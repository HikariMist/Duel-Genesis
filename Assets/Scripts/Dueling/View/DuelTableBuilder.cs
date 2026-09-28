using System;
using System.Collections.Generic;
using DuelGenesis.Cards;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DuelGenesis.Dueling
{
    /// <summary>Shared visual resources for the duel table (meshes, materials, procedural textures).</summary>
    public static class DuelVisualResources
    {
        public static readonly Color Cyan = new Color(0.12f, 0.78f, 1f);
        public static readonly Color Violet = new Color(0.66f, 0.30f, 1f);
        public static readonly Color Magenta = new Color(1f, 0.25f, 0.72f);
        public static readonly Color Gold = new Color(1f, 0.78f, 0.30f);
        public static readonly Color Navy = new Color(0.035f, 0.05f, 0.10f);

        private static Mesh _cardMesh;
        private static Material _faceMaterial;
        private static Material _backMaterial;
        private static Material _edgeMaterial;
        private static Sprite _glowSprite;
        private static Sprite _roundedSprite;
        private static Sprite _roundedOutlineSprite;
        private static Texture2D _radialTexture;
        private static Texture2D _logo;

        /// <summary>Drop runtime-made meshes, materials and sprites from a previous Play session (no domain reload).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            _cardMesh = null;
            _faceMaterial = null;
            _backMaterial = null;
            _edgeMaterial = null;
            _glowSprite = null;
            _roundedSprite = null;
            _roundedOutlineSprite = null;
            _radialTexture = null;
            _logo = null;
        }

        public static Texture2D Logo => _logo != null ? _logo : _logo = Resources.Load<Texture2D>("DuelGenesis/DuelGenesisLogo");

        public static Shader LitShader => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        public static Shader UnlitShader => Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");

        public static Material NewLit(Color color, float smoothness = 0.3f, float metallic = 0f, Texture texture = null)
        {
            Material material = new Material(LitShader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            }
            return material;
        }

        public static Material NewEmissive(Color color, float intensity)
        {
            Material material = NewLit(color * 0.4f, 0.6f);
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * intensity);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return material;
        }

        public static Material FaceMaterial => _faceMaterial != null ? _faceMaterial : _faceMaterial = NewLit(Color.white, 0.42f);
        public static Material EdgeMaterial => _edgeMaterial != null ? _edgeMaterial : _edgeMaterial = NewLit(new Color(0.10f, 0.10f, 0.11f), 0.2f);

        public static Material BackMaterial
        {
            get
            {
                if (_backMaterial == null)
                    _backMaterial = NewLit(Color.white, 0.38f, 0f, ProductionCardArtRegistry.LoadCardBack());
                return _backMaterial;
            }
        }

        /// <summary>Rounded-corner card slab: submesh 0 = face (top), 1 = back (bottom), 2 = edge.
        /// Real card: 59 x 86 mm, ~3 mm corner radius, 0.32 mm thick.</summary>
        public static Mesh CardMesh
        {
            get
            {
                if (_cardMesh != null) return _cardMesh;
                _cardMesh = BuildCardMesh(DuelMatLayout.CardWidthMm * 0.001f, DuelMatLayout.CardHeightMm * 0.001f,
                    DuelMatLayout.CardThicknessMm * 0.001f, 0.003f, 6);
                return _cardMesh;
            }
        }

        private static Mesh BuildCardMesh(float width, float height, float thickness, float radius, int cornerSegments)
        {
            // Outline of a rounded rectangle, counter-clockwise when seen from above (+Y).
            var outline = new List<Vector2>();
            Vector2[] centres =
            {
                new(width * 0.5f - radius, height * 0.5f - radius),
                new(-width * 0.5f + radius, height * 0.5f - radius),
                new(-width * 0.5f + radius, -height * 0.5f + radius),
                new(width * 0.5f - radius, -height * 0.5f + radius)
            };
            for (int corner = 0; corner < 4; corner++)
            {
                for (int s = 0; s <= cornerSegments; s++)
                {
                    float angle = (corner * 90f + s * 90f / cornerSegments) * Mathf.Deg2Rad;
                    outline.Add(centres[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var top = new List<int>();
            var bottom = new List<int>();
            var edge = new List<int>();
            float half = thickness * 0.5f;

            // Top face (front): fan from centre.
            int topCentre = vertices.Count;
            vertices.Add(new Vector3(0f, half, 0f)); normals.Add(Vector3.up); uvs.Add(new Vector2(0.5f, 0.5f));
            foreach (Vector2 p in outline)
            {
                vertices.Add(new Vector3(p.x, half, p.y));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(p.x / width + 0.5f, p.y / height + 0.5f));
            }
            for (int i = 0; i < outline.Count; i++)
            {
                int a = topCentre + 1 + i;
                int b = topCentre + 1 + (i + 1) % outline.Count;
                top.Add(topCentre); top.Add(b); top.Add(a);
            }

            // Bottom face (back): U mirrored so the back reads correctly after flipping over the long axis.
            int bottomCentre = vertices.Count;
            vertices.Add(new Vector3(0f, -half, 0f)); normals.Add(Vector3.down); uvs.Add(new Vector2(0.5f, 0.5f));
            foreach (Vector2 p in outline)
            {
                vertices.Add(new Vector3(p.x, -half, p.y));
                normals.Add(Vector3.down);
                uvs.Add(new Vector2(0.5f - p.x / width, p.y / height + 0.5f));
            }
            for (int i = 0; i < outline.Count; i++)
            {
                int a = bottomCentre + 1 + i;
                int b = bottomCentre + 1 + (i + 1) % outline.Count;
                bottom.Add(bottomCentre); bottom.Add(a); bottom.Add(b);
            }

            // Edge strip.
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 p0 = outline[i];
                Vector2 p1 = outline[(i + 1) % outline.Count];
                Vector2 dir = (p1 - p0).normalized;
                Vector3 normal = new Vector3(dir.y, 0f, -dir.x);
                int start = vertices.Count;
                vertices.Add(new Vector3(p0.x, half, p0.y));
                vertices.Add(new Vector3(p1.x, half, p1.y));
                vertices.Add(new Vector3(p1.x, -half, p1.y));
                vertices.Add(new Vector3(p0.x, -half, p0.y));
                for (int k = 0; k < 4; k++) { normals.Add(normal); uvs.Add(Vector2.zero); }
                edge.Add(start); edge.Add(start + 1); edge.Add(start + 2);
                edge.Add(start); edge.Add(start + 2); edge.Add(start + 3);
            }

            Mesh mesh = new Mesh { name = "DG Card Mesh" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(top, 0);
            mesh.SetTriangles(bottom, 1);
            mesh.SetTriangles(edge, 2);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Soft rounded glow used under selectable / targeted cards.</summary>
        public static Sprite GlowSprite
        {
            get
            {
                if (_glowSprite != null) return _glowSprite;
                const int w = 96, h = 128;
                Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "DG Glow" };
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x - w * 0.5f + 0.5f) - w * 0.30f) / (w * 0.20f);
                    float dy = Mathf.Max(0f, Mathf.Abs(y - h * 0.5f + 0.5f) - h * 0.34f) / (h * 0.16f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
                texture.Apply();
                _glowSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1000f);
                return _glowSprite;
            }
        }

        /// <summary>9-sliced rounded rectangle (filled).</summary>
        public static Sprite RoundedSprite => _roundedSprite != null ? _roundedSprite : _roundedSprite = BuildRounded(false);

        /// <summary>9-sliced rounded rectangle outline.</summary>
        public static Sprite RoundedOutlineSprite => _roundedOutlineSprite != null ? _roundedOutlineSprite : _roundedOutlineSprite = BuildRounded(true);

        private static Sprite BuildRounded(bool outlineOnly)
        {
            const int size = 64;
            const float radius = 18f;
            const float stroke = 3f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "DG Rounded" };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x + 0.5f - size * 0.5f);
                float py = Mathf.Abs(y + 0.5f - size * 0.5f);
                float qx = Mathf.Max(px - (size * 0.5f - radius), 0f);
                float qy = Mathf.Max(py - (size * 0.5f - radius), 0f);
                float dist = Mathf.Sqrt(qx * qx + qy * qy) - radius;   // signed distance to the rounded edge
                float alpha = Mathf.Clamp01(0.5f - dist);
                if (outlineOnly) alpha *= Mathf.Clamp01(dist + stroke + 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
        }

        public static Texture2D RadialTexture
        {
            get
            {
                if (_radialTexture != null) return _radialTexture;
                const int size = 128;
                _radialTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "DG Radial" };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    _radialTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
                _radialTexture.Apply();
                return _radialTexture;
            }
        }
    }

    /// <summary>
    /// Builds the life-size duel table: furniture, the printed two-player game mat (rendered once
    /// from the real zone geometry), clickable zone colliders and table lighting.
    /// </summary>
    public static class DuelTableBuilder
    {
        private const int CaptureLayer = 31;
        private const float MatMarginMm = 20f;
        private static RenderTexture _matTexture;
        private static Material _pendingMatMaterial;
        private static float _pendingMatWidthMm;
        private static float _pendingMatDepthMm;
        private static int _matAttempts;

        public static Transform Build(Transform table, List<DuelZoneView> zones)
        {
            GameObject root = new GameObject("DG Duel Table");
            root.transform.SetParent(table, false);

            BuildFurniture(root.transform, withChair: true);
            BuildMat(root.transform);
            BuildArena(root.transform);
            BuildZones(root.transform, zones);
            BuildLights(root.transform);
            return root.transform;
        }

        /// <summary>A furnished table with the printed mat and arena frame but no duel logic
        /// (used for the ambient NPC duels around Genesis City).</summary>
        public static Transform BuildDecorative(Transform parent)
        {
            GameObject root = new GameObject("DG Duel Table (ambient)");
            root.transform.SetParent(parent, false);
            BuildFurniture(root.transform, withChair: false);
            BuildMat(root.transform);
            BuildArena(root.transform);
            return root.transform;
        }

        // ------------------------------------------------------------------ furniture

        private static Material _wood, _woodDark, _metal, _cushion, _bezel, _glowCyan, _glowViolet;

        private static void EnsureFurnitureMaterials()
        {
            if (_wood != null) return;
            _wood = DuelVisualResources.NewLit(new Color(0.30f, 0.18f, 0.10f), 0.42f);        // walnut top
            _woodDark = DuelVisualResources.NewLit(new Color(0.16f, 0.10f, 0.06f), 0.35f);    // legs, apron, chair
            _metal = DuelVisualResources.NewLit(new Color(0.42f, 0.44f, 0.48f), 0.7f, 0.9f);   // edge band, feet
            _cushion = DuelVisualResources.NewLit(new Color(0.07f, 0.08f, 0.12f), 0.15f);     // chair seat
            _bezel = DuelVisualResources.NewLit(new Color(0.05f, 0.06f, 0.08f), 0.6f, 0.6f);   // arena frame
            _glowCyan = DuelVisualResources.NewEmissive(DuelVisualResources.Cyan, 2.4f);
            _glowViolet = DuelVisualResources.NewEmissive(DuelVisualResources.Violet, 2.4f);
        }

        /// <summary>
        /// A real 1200 x 900 mm duel table, 760 mm high: 40 mm walnut top with a metal edge band,
        /// a 90 mm apron, four square legs with metal feet and a low H stretcher, plus the player's chair.
        /// </summary>
        private static void BuildFurniture(Transform root, bool withChair)
        {
            EnsureFurnitureMaterials();
            float w = DuelMatLayout.TableWidth;
            float d = DuelMatLayout.TableDepth;
            float h = DuelMatLayout.TableHeight;
            float t = DuelMatLayout.TableTopThickness;

            Transform table = new GameObject("Table").transform;
            table.SetParent(root, false);

            Box(table, "Table Top", new Vector3(0f, h - t * 0.5f, 0f), new Vector3(w, t, d), _wood);
            float band = 0.012f;
            Box(table, "Edge Near", new Vector3(0f, h - t * 0.5f, -d * 0.5f - band * 0.5f), new Vector3(w + band * 2f, t, band), _metal);
            Box(table, "Edge Far", new Vector3(0f, h - t * 0.5f, d * 0.5f + band * 0.5f), new Vector3(w + band * 2f, t, band), _metal);
            Box(table, "Edge Left", new Vector3(-w * 0.5f - band * 0.5f, h - t * 0.5f, 0f), new Vector3(band, t, d), _metal);
            Box(table, "Edge Right", new Vector3(w * 0.5f + band * 0.5f, h - t * 0.5f, 0f), new Vector3(band, t, d), _metal);

            // Apron (the skirt under the top) on all four sides.
            float inset = 0.06f, apronH = 0.09f, apronT = 0.022f;
            float apronY = h - t - apronH * 0.5f;
            Box(table, "Apron Near", new Vector3(0f, apronY, -d * 0.5f + inset), new Vector3(w - inset * 2f, apronH, apronT), _woodDark);
            Box(table, "Apron Far", new Vector3(0f, apronY, d * 0.5f - inset), new Vector3(w - inset * 2f, apronH, apronT), _woodDark);
            Box(table, "Apron Left", new Vector3(-w * 0.5f + inset, apronY, 0f), new Vector3(apronT, apronH, d - inset * 2f), _woodDark);
            Box(table, "Apron Right", new Vector3(w * 0.5f - inset, apronY, 0f), new Vector3(apronT, apronH, d - inset * 2f), _woodDark);
            // Accent light line under the near and far apron edges (player cyan, opponent violet).
            Box(table, "LED Near", new Vector3(0f, h - t - apronH - 0.003f, -d * 0.5f + inset), new Vector3(w - inset * 2f - 0.04f, 0.004f, 0.008f), _glowCyan);
            Box(table, "LED Far", new Vector3(0f, h - t - apronH - 0.003f, d * 0.5f - inset), new Vector3(w - inset * 2f - 0.04f, 0.004f, 0.008f), _glowViolet);

            // Legs, metal feet and a low H stretcher.
            float leg = 0.06f, legInset = 0.075f, legH = h - t;
            float lx = w * 0.5f - legInset, lz = d * 0.5f - legInset;
            foreach (Vector2 c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            {
                Box(table, "Leg", new Vector3(c.x * lx, legH * 0.5f + 0.015f, c.y * lz), new Vector3(leg, legH - 0.03f, leg), _woodDark);
                Box(table, "Foot", new Vector3(c.x * lx, 0.0075f, c.y * lz), new Vector3(leg + 0.01f, 0.015f, leg + 0.01f), _metal);
            }
            Box(table, "Stretcher Left", new Vector3(-lx, 0.14f, 0f), new Vector3(0.03f, 0.04f, lz * 2f), _woodDark);
            Box(table, "Stretcher Right", new Vector3(lx, 0.14f, 0f), new Vector3(0.03f, 0.04f, lz * 2f), _woodDark);
            Box(table, "Stretcher Centre", new Vector3(0f, 0.14f, 0f), new Vector3(lx * 2f, 0.04f, 0.03f), _woodDark);

            if (withChair) BuildChair(root, new Vector3(0f, 0f, -d * 0.5f - 0.30f), 0f);
        }

        /// <summary>Simple upholstered chair: 460 mm seat height, 440 mm square seat, backrest to 900 mm.</summary>
        private static void BuildChair(Transform root, Vector3 position, float yaw)
        {
            Transform chair = new GameObject("Chair").transform;
            chair.SetParent(root, false);
            chair.localPosition = position;
            chair.localRotation = Quaternion.Euler(0f, yaw, 0f);
            const float seatH = 0.46f, seat = 0.44f, legT = 0.035f;
            Box(chair, "Seat", new Vector3(0f, seatH - 0.02f, 0f), new Vector3(seat, 0.04f, seat), _woodDark);
            Box(chair, "Cushion", new Vector3(0f, seatH + 0.015f, 0.005f), new Vector3(seat - 0.03f, 0.03f, seat - 0.04f), _cushion);
            float o = seat * 0.5f - legT * 0.5f;
            foreach (Vector2 c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                Box(chair, "Chair Leg", new Vector3(c.x * o, (seatH - 0.04f) * 0.5f, c.y * o), new Vector3(legT, seatH - 0.04f, legT), _woodDark);
            // Backrest on the side away from the table (-Z).
            Box(chair, "Back Post L", new Vector3(-o, seatH + 0.22f, -o), new Vector3(legT, 0.44f, legT), _woodDark);
            Box(chair, "Back Post R", new Vector3(o, seatH + 0.22f, -o), new Vector3(legT, 0.44f, legT), _woodDark);
            Box(chair, "Backrest", new Vector3(0f, seatH + 0.30f, -o), new Vector3(seat - 0.02f, 0.22f, 0.025f), _cushion);
        }

        /// <summary>
        /// The duel arena on the table top: a low metal frame around the play mat with a projector
        /// strip inside it (cyan on the player's half, violet on the opponent's) and four hologram
        /// emitters on the corners — the "field" the monsters are projected onto.
        /// </summary>
        private static void BuildArena(Transform root)
        {
            EnsureFurnitureMaterials();
            float matW = (DuelMatLayout.MatWidthMm + MatMarginMm * 2f) * 0.001f;
            float matD = (DuelMatLayout.MatDepthMm * 2f + MatMarginMm * 2f) * 0.001f;
            float y = DuelMatLayout.TableHeight;
            const float frame = 0.018f, frameH = 0.008f, strip = 0.003f;

            Transform arena = new GameObject("Arena").transform;
            arena.SetParent(root, false);

            float hx = matW * 0.5f + frame * 0.5f, hz = matD * 0.5f + frame * 0.5f;
            Box(arena, "Frame Near", new Vector3(0f, y + frameH * 0.5f, -hz), new Vector3(matW + frame * 2f, frameH, frame), _bezel);
            Box(arena, "Frame Far", new Vector3(0f, y + frameH * 0.5f, hz), new Vector3(matW + frame * 2f, frameH, frame), _bezel);
            Box(arena, "Frame Left", new Vector3(-hx, y + frameH * 0.5f, 0f), new Vector3(frame, frameH, matD), _bezel);
            Box(arena, "Frame Right", new Vector3(hx, y + frameH * 0.5f, 0f), new Vector3(frame, frameH, matD), _bezel);

            // Projector strips on the inner lip of the frame, split at the centre line.
            float sx = matW * 0.5f + strip * 0.5f, sz = matD * 0.5f + strip * 0.5f, sy = y + frameH + 0.0005f;
            Box(arena, "Strip Near", new Vector3(0f, sy, -sz), new Vector3(matW, 0.001f, strip), _glowCyan);
            Box(arena, "Strip Far", new Vector3(0f, sy, sz), new Vector3(matW, 0.001f, strip), _glowViolet);
            Box(arena, "Strip Left Near", new Vector3(-sx, sy, -matD * 0.25f), new Vector3(strip, 0.001f, matD * 0.5f), _glowCyan);
            Box(arena, "Strip Right Near", new Vector3(sx, sy, -matD * 0.25f), new Vector3(strip, 0.001f, matD * 0.5f), _glowCyan);
            Box(arena, "Strip Left Far", new Vector3(-sx, sy, matD * 0.25f), new Vector3(strip, 0.001f, matD * 0.5f), _glowViolet);
            Box(arena, "Strip Right Far", new Vector3(sx, sy, matD * 0.25f), new Vector3(strip, 0.001f, matD * 0.5f), _glowViolet);

            // Hologram emitters on the four corners.
            foreach (Vector2 c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            {
                Vector3 p = new Vector3(c.x * hx, y, c.y * hz);
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Emitter";
                DestroyCollider(post);
                post.transform.SetParent(arena, false);
                post.transform.localPosition = p + new Vector3(0f, 0.016f, 0f);
                post.transform.localScale = new Vector3(0.03f, 0.016f, 0.03f);
                post.GetComponent<Renderer>().sharedMaterial = _bezel;

                GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lens.name = "Emitter Lens";
                DestroyCollider(lens);
                lens.transform.SetParent(arena, false);
                lens.transform.localPosition = p + new Vector3(0f, 0.034f, 0f);
                lens.transform.localScale = Vector3.one * 0.018f;
                lens.GetComponent<Renderer>().sharedMaterial = c.y < 0 ? _glowCyan : _glowViolet;
            }
        }

        private static void DestroyCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(c); else UnityEngine.Object.DestroyImmediate(c);
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!name.StartsWith("Table Top")) DestroyCollider(box);
            return box;
        }

        // ------------------------------------------------------------------ printed mat

        private static Material _sharedMatMaterial;

        private static void BuildMat(Transform root)
        {
            float widthMm = DuelMatLayout.MatWidthMm + MatMarginMm * 2f;
            float depthMm = DuelMatLayout.MatDepthMm * 2f + MatMarginMm * 2f;

            GameObject mat = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mat.name = "Game Mat";
            DestroyCollider(mat);
            mat.transform.SetParent(root, false);
            mat.transform.localPosition = new Vector3(0f, DuelMatLayout.SurfaceY - 0.0004f, 0f);
            mat.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            mat.transform.localScale = new Vector3(widthMm * 0.001f, depthMm * 0.001f, 1f);

            // Every table shares one printed-mat material. It is rendered a few frames later, once the
            // render pipeline is running (rendering during scene load silently produces a blank texture).
            if (_sharedMatMaterial == null)
            {
                _sharedMatMaterial = DuelVisualResources.NewLit(DuelVisualResources.Navy, 0.18f);
                _sharedMatMaterial.name = "DG Game Mat";
                _pendingMatMaterial = _sharedMatMaterial;
                _pendingMatWidthMm = widthMm;
                _pendingMatDepthMm = depthMm;
                _matAttempts = 0;
            }
            mat.GetComponent<Renderer>().sharedMaterial = _sharedMatMaterial;

            // Rubber mat body (slight thickness).
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Game Mat Body";
            DestroyCollider(body);
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(0f, DuelMatLayout.TableHeight + DuelMatLayout.MatThickness * 0.5f - 0.0005f, 0f);
            body.transform.localScale = new Vector3(widthMm * 0.001f, DuelMatLayout.MatThickness, depthMm * 0.001f);
            body.GetComponent<Renderer>().sharedMaterial = DuelVisualResources.NewLit(new Color(0.02f, 0.02f, 0.03f), 0.1f);
        }

        /// <summary>Call every frame: renders the printed mat once the pipeline is ready and verifies it.</summary>
        public static void TickMatRender()
        {
            if (_pendingMatMaterial == null) return;
            if (Time.frameCount < 3) return;
            if (GraphicsSettings.currentRenderPipeline != null && RenderPipelineManager.currentPipeline == null) return;

            if (_matAttempts++ > 30)
            {
                Debug.LogWarning("Duel: Genesis could not render the game mat texture; using a plain mat.");
                _pendingMatMaterial = null;
                return;
            }

            if (_matTexture != null) { _matTexture.Release(); UnityEngine.Object.Destroy(_matTexture); _matTexture = null; }
            Texture texture = RenderMatTexture(_pendingMatWidthMm, _pendingMatDepthMm);
            if (texture == null || !LooksRendered(_matTexture)) return;

#if UNITY_EDITOR
            DumpForInspection(_matTexture, "Logs/DG-GameMat.png");
#endif
            // Bake to a mip-mapped texture on the CPU. The table is always seen at a grazing angle, so the GPU
            // samples the small mips — and a render request does not reliably regenerate a render texture's
            // mips, which showed up as a blank/white mat. A baked Texture2D has correct mips on every platform.
            Texture2D baked = BakeWithMips(_matTexture);
            int w = _matTexture.width, h = _matTexture.height;
            _pendingMatMaterial.SetColor("_BaseColor", Color.white);
            _pendingMatMaterial.SetTexture("_BaseMap", baked != null ? baked : texture);
            _pendingMatMaterial = null;
            if (baked != null) { _matTexture.Release(); UnityEngine.Object.Destroy(_matTexture); _matTexture = null; }
            Debug.Log($"Duel: Genesis game mat rendered ({w}x{h}, mip-mapped) after {_matAttempts} attempt(s).");
        }

#if UNITY_EDITOR
        private static void DumpForInspection(RenderTexture rt, string path)
        {
            try
            {
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                Texture2D copy = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
                copy.Apply(false);
                RenderTexture.active = previous;
                System.IO.File.WriteAllBytes(path, copy.EncodeToPNG());
                UnityEngine.Object.Destroy(copy);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Duel: Genesis could not dump the game mat: " + e.Message);
            }
        }
#endif

        private static Texture2D BakeWithMips(RenderTexture rt)
        {
            try
            {
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, true)
                {
                    name = "DG Game Mat (baked)",
                    anisoLevel = 8,
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    mipMapBias = -0.4f
                };
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, true);
                tex.Apply(true, true);
                RenderTexture.active = previous;
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Duel: Genesis could not bake the game mat mips: " + e.Message);
                return null;
            }
        }

        private static bool LooksRendered(RenderTexture rt)
        {
            if (rt == null || !rt.IsCreated()) return false;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D probe = new Texture2D(2, 1, TextureFormat.RGBA32, false);
            probe.ReadPixels(new Rect(4, 4, 1, 1), 0, 0, false);                       // outer margin
            probe.ReadPixels(new Rect(rt.width / 2 - 40, rt.height / 4, 1, 1), 1, 0, false); // inside the player's mat
            probe.Apply(false);
            RenderTexture.active = previous;
            Color a = probe.GetPixel(0, 0), b = probe.GetPixel(1, 0);
            UnityEngine.Object.Destroy(probe);
            bool blankWhite = a.r > 0.95f && a.g > 0.95f && a.b > 0.95f && b.r > 0.95f && b.g > 0.95f;
            bool blankClear = a.a < 0.01f && b.a < 0.01f;
            return !blankWhite && !blankClear;
        }

        private static Texture RenderMatTexture(float widthMm, float depthMm)
        {
            if (_matTexture != null && _matTexture.IsCreated()) return _matTexture;

            const float pxPerMm = 3.2f;
            int w = Mathf.RoundToInt(widthMm * pxPerMm / 16f) * 16;
            int h = Mathf.RoundToInt(depthMm * pxPerMm / 16f) * 16;

            _matTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                name = "DG Game Mat",
                useMipMap = false,
                autoGenerateMips = false,
                anisoLevel = 8,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _matTexture.Create();

            GameObject rig = new GameObject("DG Mat Capture");
            rig.transform.position = new Vector3(0f, -6000f, 0f);
            try
            {
                Camera camera = rig.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = DuelVisualResources.Navy;
                camera.cullingMask = 1 << CaptureLayer;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 10f;
                camera.targetTexture = _matTexture;

                GameObject canvasObject = new GameObject("Mat Canvas", typeof(RectTransform));
                canvasObject.layer = CaptureLayer;
                canvasObject.transform.SetParent(rig.transform, false);
                Canvas canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;

                var painter = new MatPainter((RectTransform)canvasObject.transform, widthMm, depthMm);
                painter.Paint();

                Canvas.ForceUpdateCanvases();
                var request = new RenderPipeline.StandardRequest { destination = _matTexture };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                    RenderPipeline.SubmitRenderRequest(camera, request);
                else
                    camera.Render();
            }
            finally
            {
                UnityEngine.Object.Destroy(rig);
            }
            return _matTexture;
        }

        /// <summary>Draws the printed mat in millimetre coordinates (table-local X/Z, human at -Z).</summary>
        private sealed class MatPainter
        {
            private readonly RectTransform _root;
            private readonly float _widthMm;
            private readonly float _depthMm;
            private readonly Font _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            public MatPainter(RectTransform root, float widthMm, float depthMm)
            {
                _root = root;
                _widthMm = widthMm;
                _depthMm = depthMm;
            }

            public void Paint()
            {
                // Background glow.
                RawImage glow = Raw("Glow", DuelVisualResources.RadialTexture, new Color(0.25f, 0.35f, 0.75f, 0.35f));
                Place(glow.rectTransform, 0f, 0f, _widthMm * 1.2f, _depthMm * 0.9f, 0f);

                for (int player = 0; player < 2; player++)
                {
                    Color accent = player == 0 ? DuelVisualResources.Cyan : DuelVisualResources.Violet;
                    float sign = player == 0 ? -1f : 1f;
                    float rot = player == 0 ? 0f : 180f;

                    // Mat panel.
                    Image panel = Img("Mat " + player, DuelVisualResources.RoundedSprite, new Color(accent.r * 0.10f, accent.g * 0.10f, accent.b * 0.16f + 0.05f, 0.92f));
                    Place(panel.rectTransform, 0f, sign * DuelMatLayout.MatDepthMm * 0.5f, DuelMatLayout.MatWidthMm, DuelMatLayout.MatDepthMm - 4f, rot);
                    Image border = Img("Mat Border " + player, DuelVisualResources.RoundedOutlineSprite, new Color(accent.r, accent.g, accent.b, 0.75f));
                    Place(border.rectTransform, 0f, sign * DuelMatLayout.MatDepthMm * 0.5f, DuelMatLayout.MatWidthMm, DuelMatLayout.MatDepthMm - 4f, rot);

                    // Logo printed on the free band behind the Spell & Trap row.
                    Texture2D logo = DuelVisualResources.Logo;
                    if (logo != null)
                    {
                        RawImage logoImage = Raw("Logo " + player, logo, new Color(1f, 1f, 1f, 0.55f));
                        float logoW = 190f;
                        Place(logoImage.rectTransform, 0f, sign * (DuelMatLayout.MatDepthMm - 44f), logoW, logoW * logo.height / logo.width, rot);
                    }

                    for (int slot = 0; slot < 5; slot++)
                    {
                        Zone(player, DuelMatLayout.MonsterZone(player, slot), "MONSTER", accent, rot);
                        Zone(player, DuelMatLayout.SpellTrapZone(player, slot),
                            slot == 0 || slot == 4 ? "SPELL & TRAP\nPENDULUM" : "SPELL & TRAP", accent * 0.85f, rot);
                    }
                    Zone(player, DuelMatLayout.FieldZone(player), "FIELD", new Color(0.30f, 0.95f, 0.60f), rot);
                    Zone(player, DuelMatLayout.ExtraDeckZone(player), "EXTRA DECK", DuelVisualResources.Violet, rot);
                    Zone(player, DuelMatLayout.GraveyardZone(player), "GRAVEYARD", new Color(0.75f, 0.78f, 0.85f), rot);
                    Zone(player, DuelMatLayout.DeckZone(player), "DECK", DuelVisualResources.Gold, rot);
                    Zone(player, DuelMatLayout.BanishedZone(player), "BANISHED", DuelVisualResources.Magenta, rot);
                }

                Zone(0, DuelMatLayout.ExtraMonsterZone(0), "EXTRA\nMONSTER", DuelVisualResources.Gold, 0f);
                Zone(0, DuelMatLayout.ExtraMonsterZone(1), "EXTRA\nMONSTER", DuelVisualResources.Gold, 0f);

                // Centre line.
                Image line = Img("Centre Line", null, new Color(0.6f, 0.8f, 1f, 0.35f));
                Place(line.rectTransform, 0f, 0f, DuelMatLayout.MatWidthMm - 10f, 0.8f, 0f);
            }

            private void Zone(int player, Vector3 localPosition, string label, Color color, float rotation)
            {
                float xMm = localPosition.x * 1000f;
                float zMm = localPosition.z * 1000f;
                Image fill = Img("Zone Fill " + label, DuelVisualResources.RoundedSprite, new Color(color.r, color.g, color.b, 0.07f));
                Place(fill.rectTransform, xMm, zMm, DuelMatLayout.ZoneWidthMm, DuelMatLayout.ZoneHeightMm, rotation);
                fill.pixelsPerUnitMultiplier = 1.5f;
                Image outline = Img("Zone Outline " + label, DuelVisualResources.RoundedOutlineSprite, new Color(color.r, color.g, color.b, 0.85f));
                Place(outline.rectTransform, xMm, zMm, DuelMatLayout.ZoneWidthMm, DuelMatLayout.ZoneHeightMm, rotation);
                outline.pixelsPerUnitMultiplier = 1.5f;

                Text text = new GameObject("Label " + label, typeof(RectTransform)).AddComponent<Text>();
                text.gameObject.layer = CaptureLayer;
                text.transform.SetParent(_root, false);
                text.font = _font;
                text.text = label;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(color.r, color.g, color.b, 0.55f);
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 8;
                text.resizeTextMaxSize = 30;
                text.lineSpacing = 0.9f;
                Place(text.rectTransform, xMm, zMm, DuelMatLayout.ZoneWidthMm - 12f, 26f, rotation);
            }

            private Image Img(string name, Sprite sprite, Color color)
            {
                GameObject go = new GameObject(name, typeof(RectTransform));
                go.layer = CaptureLayer;
                go.transform.SetParent(_root, false);
                Image image = go.AddComponent<Image>();
                image.sprite = sprite;
                image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                image.color = color;
                image.raycastTarget = false;
                return image;
            }

            private RawImage Raw(string name, Texture texture, Color color)
            {
                GameObject go = new GameObject(name, typeof(RectTransform));
                go.layer = CaptureLayer;
                go.transform.SetParent(_root, false);
                RawImage image = go.AddComponent<RawImage>();
                image.texture = texture;
                image.color = color;
                image.raycastTarget = false;
                return image;
            }

            /// <summary>Centre (x, z) and size in mm; +Z (towards the opponent) is up in the texture.
            /// Uses anchors only, so it does not depend on the canvas having been laid out yet.</summary>
            private void Place(RectTransform rect, float xMm, float zMm, float wMm, float hMm, float rotation)
            {
                Vector2 centre = new Vector2(xMm / _widthMm + 0.5f, zMm / _depthMm + 0.5f);
                Vector2 half = new Vector2(wMm / _widthMm, hMm / _depthMm) * 0.5f;
                rect.anchorMin = centre - half;
                rect.anchorMax = centre + half;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            }
        }

        // ------------------------------------------------------------------ zones & lights

        private static void BuildZones(Transform root, List<DuelZoneView> zones)
        {
            for (int player = 0; player < 2; player++)
            {
                for (int slot = 0; slot < 5; slot++)
                {
                    zones.Add(DuelZoneView.Create(root, player, DuelZone.Monster, slot, DuelMatLayout.MonsterZone(player, slot)));
                    zones.Add(DuelZoneView.Create(root, player, DuelZone.SpellTrap, slot, DuelMatLayout.SpellTrapZone(player, slot)));
                }
                zones.Add(DuelZoneView.Create(root, player, DuelZone.FieldSpell, 0, DuelMatLayout.FieldZone(player)));
                zones.Add(DuelZoneView.Create(root, player, DuelZone.Graveyard, 0, DuelMatLayout.GraveyardZone(player)));
                zones.Add(DuelZoneView.Create(root, player, DuelZone.Deck, 0, DuelMatLayout.DeckZone(player)));
                zones.Add(DuelZoneView.Create(root, player, DuelZone.ExtraDeck, 0, DuelMatLayout.ExtraDeckZone(player)));
                zones.Add(DuelZoneView.Create(root, player, DuelZone.Banished, 0, DuelMatLayout.BanishedZone(player)));
            }
        }

        private static void BuildLights(Transform root)
        {
            GameObject key = new GameObject("Table Key Light");
            key.transform.SetParent(root, false);
            key.transform.localPosition = new Vector3(0.15f, DuelMatLayout.TableHeight + 1.6f, -0.35f);
            key.transform.localRotation = Quaternion.LookRotation(new Vector3(-0.1f, -1f, 0.25f));
            Light spot = key.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.spotAngle = 70f;
            spot.innerSpotAngle = 40f;
            spot.range = 3f;
            spot.intensity = 1.1f;
            spot.color = new Color(1f, 0.96f, 0.90f);
            spot.shadows = LightShadows.Soft;
            spot.shadowStrength = 0.6f;
            spot.shadowBias = 0.01f;
            spot.shadowNormalBias = 0.1f;

            AddPoint(root, "Player Rim Light", new Vector3(0f, DuelMatLayout.TableHeight + 0.45f, -0.75f), DuelVisualResources.Cyan, 0.45f, 1.8f);
            AddPoint(root, "Opponent Rim Light", new Vector3(0f, DuelMatLayout.TableHeight + 0.55f, 0.85f), DuelVisualResources.Violet, 0.55f, 2.0f);
        }

        private static void AddPoint(Transform root, string name, Vector3 position, Color color, float intensity, float range)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
        }
    }
}
