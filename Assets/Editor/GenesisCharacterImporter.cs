#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DuelGenesis.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Brings the user's own DMO character (DAZ Genesis 9 figure) into Duel Genesis for the PLAYER only.
    /// Copies the figure and wardrobe items (with every mesh, material and texture they use) from the DMO export
    /// into git-ignored Assets/ThirdParty/DMO, fills Resources/DuelGenesis/Characters/GenesisCharacterAssets and puts
    /// a "Genesis Avatar" on the player that builds the character from the saved look. Never creates NPCs.
    /// </summary>
    public static class GenesisCharacterImporter
    {
        private const string AssetsPath = "Assets/Resources/DuelGenesis/Characters/GenesisCharacterAssets.asset";
        private const string Locomotion = "Assets/UMA/UMA3/Animation/Locomotion.controller";
        private const string DmoPlayerController = "71e52459dc560314a93ff1fceec4cb4e";   // DMO's 1 MB network animator: not needed
        private static readonly Regex GuidRx = new Regex(@"guid: ([0-9a-f]{32})", RegexOptions.Compiled);

        [MenuItem("Duel Genesis/Characters/1. Import Player Character (figure + starter outfits)")]
        public static void ImportStarter() => Import(full: false);

        [MenuItem("Duel Genesis/Characters/2. Import Full Wardrobe (all 256 items, large)")]
        public static void ImportFull() => Import(full: true);

        private static void Import(bool full)
        {
            var log = new StringBuilder();
            string export = Path.Combine(DMOImporter.ExportRoot, "Assets");
            string baseMeta = Path.Combine(export, "GameObject", "Created Player.prefab.meta");
            if (!File.Exists(baseMeta))
            {
                EditorUtility.DisplayDialog("Duel Genesis", "Cannot find the DMO export (Created Player.prefab) at\n" + export, "OK");
                return;
            }
            string baseGuid = GuidRx.Match(File.ReadAllText(baseMeta)).Groups[1].Value;

            GenesisCharacterLibrary library = GenesisCharacterLibrary.Instance;
            var wanted = new List<(GenesisSlot slot, int id)>();
            if (full)
            {
                for (int s = 0; s < GenesisAppearance.SlotCount; s++)
                    for (int i = 0; i < library.ItemsIn((GenesisSlot)s).Length; i++) wanted.Add(((GenesisSlot)s, i));
            }
            else
            {
                foreach (GenesisGender g in new[] { GenesisGender.Female, GenesisGender.Male })
                    foreach (GenesisEquip e in GenesisAppearance.CreateDefault(g).equipment)
                        if (e.id >= 0) wanted.Add((e.slot, e.id));
            }
            wanted = wanted.Distinct().ToList();

            var seeds = new List<string> { baseGuid };
            foreach (var (slot, id) in wanted)
            {
                var item = library.Find(slot, id);
                if (item == null) continue;
                if (!string.IsNullOrEmpty(item.prefabGuid)) seeds.Add(item.prefabGuid);
                if (!string.IsNullOrEmpty(item.iconGuid)) seeds.Add(item.iconGuid);
            }

            try
            {
                EditorUtility.DisplayProgressBar("Duel Genesis", "Copying the character from the DMO export...", 0.2f);
                int copied = DMOImporter.ImportDependencies(seeds, log, new HashSet<string> { DmoPlayerController });
                if (copied < 0) return;
                EditorUtility.DisplayProgressBar("Duel Genesis", "Importing (large meshes can take a few minutes)...", 0.5f);
                AssetDatabase.Refresh();
                DMOImporter.FixMaterials(log);
            }
            finally { EditorUtility.ClearProgressBar(); }

            string basePath = AssetDatabase.GUIDToAssetPath(baseGuid);
            if (string.IsNullOrEmpty(basePath)) { Debug.LogError("Duel: Genesis: the figure did not import.\n" + log); return; }
            DMOImporter.CleanPrefab(basePath, log);

            var assets = AssetDatabase.LoadAssetAtPath<GenesisCharacterAssets>(AssetsPath);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<GenesisCharacterAssets>();
                AssetDatabase.CreateAsset(assets, AssetsPath);
            }
            assets.basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            assets.locomotion = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Locomotion);
            int items = 0;
            foreach (var (slot, id) in wanted)
            {
                var entry = library.Find(slot, id);
                string path = entry != null ? AssetDatabase.GUIDToAssetPath(entry.prefabGuid) : null;
                if (string.IsNullOrEmpty(path)) continue;
                DMOImporter.CleanPrefab(path, log);
                assets.items.RemoveAll(i => i.slot == slot && i.id == id);
                assets.items.Add(new GenesisCharacterAssets.Item { slot = slot, id = id, name = entry.name, prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) });
                items++;
            }
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();

            // Report what the builder will find.
            GameObject fig = assets.basePrefab;
            Animator anim = fig != null ? fig.GetComponentInChildren<Animator>() : null;
            SkinnedMeshRenderer body = fig != null ? fig.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == GenesisCharacterBuilder.BodyMeshName) : null;
            int shapes = body != null && body.sharedMesh != null ? body.sharedMesh.blendShapeCount : 0;
            string[] check = { GenesisMorphMap.FemaleFigure, GenesisMorphMap.MaleFigure, GenesisMorphMap.ScaleTorso, GenesisMorphMap.HideFeet, "head_bs_FaceYoung" };
            string found = body != null && body.sharedMesh != null ? string.Join(", ", check.Select(c => c + (GenesisCharacterBuilder.ShapeIndex(body.sharedMesh, c) >= 0 ? " yes" : " NO"))) : "no body mesh";
            if (body != null && body.sharedMesh != null)
            {
                var names = new StringBuilder();
                for (int i = 0; i < body.sharedMesh.blendShapeCount; i++) names.AppendLine(body.sharedMesh.GetBlendShapeName(i));
                File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Genesis9Blendshapes.txt"), names.ToString());
            }
            log.AppendLine($"Figure: {basePath}; avatar {(anim != null && anim.avatar != null ? (anim.avatar.isHuman ? "humanoid" : "generic") : "missing")}; body blendshapes {shapes} ({found}).");
            log.AppendLine($"Wardrobe: {items} of {wanted.Count} items registered. Locomotion: {(assets.locomotion != null ? "UMA Locomotion" : "MISSING")}.");

            PutOnPlayer(log);
            Debug.Log("Duel: Genesis character import done.\n" + log);
        }

        [MenuItem("Duel Genesis/Characters/3. Put The Character On The Player")]
        public static void PutOnPlayerMenu()
        {
            var log = new StringBuilder();
            PutOnPlayer(log);
            Debug.Log(log.ToString());
        }

        private static void PutOnPlayer(StringBuilder log)
        {
            GameObject player = GameObject.Find("Player_Hikari_Blockout");
            if (player == null) { log.AppendLine("Player_Hikari_Blockout not found in the open scene."); return; }
            Transform avatar = player.transform.Find(DuelGenesis.Player.GenesisAvatarDriver.AvatarChildName);
            if (avatar == null)
            {
                avatar = new GameObject(DuelGenesis.Player.GenesisAvatarDriver.AvatarChildName).transform;
                avatar.SetParent(player.transform, false);
            }
            // Feet on the ground: the bottom of the player's CharacterController.
            var cc = player.GetComponent<CharacterController>();
            avatar.localPosition = cc != null ? new Vector3(0f, cc.center.y - cc.height * 0.5f, 0f) : Vector3.zero;
            avatar.localRotation = Quaternion.identity;
            foreach (Transform old in avatar.Cast<Transform>().ToList()) Object.DestroyImmediate(old.gameObject);   // any old UMA body
            if (avatar.GetComponent<GenesisPlayerCharacter>() == null) avatar.gameObject.AddComponent<GenesisPlayerCharacter>();
            if (player.GetComponent<DuelGenesis.Player.GenesisAvatarDriver>() == null) player.AddComponent<DuelGenesis.Player.GenesisAvatarDriver>();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            log.AppendLine("Player: \"Genesis Avatar\" builds the character at runtime from the saved look (default male until the creator saves one).");
        }
    }
}
#endif
