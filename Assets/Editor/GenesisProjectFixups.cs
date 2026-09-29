#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// One-off repairs for files that landed in the wrong place. Runs once after scripts load and does nothing
    /// when there is nothing to fix. Currently: MillenniumRing.obj unzipped into Assets/Assets/... instead of
    /// Assets/Resources/DuelGenesis/Characters (then rebuilds the wearable ring).
    /// </summary>
    [InitializeOnLoad]
    public static class GenesisProjectFixups
    {
        private const string WrongRing = "Assets/Assets/Resources/DuelGenesis/Characters/MillenniumRing.obj";
        private const string RightRing = "Assets/Resources/DuelGenesis/Characters/MillenniumRing.obj";

        static GenesisProjectFixups() => EditorApplication.delayCall += Run;

        [MenuItem("Duel Genesis/Maintenance/Fix Misplaced Files")]
        public static void Run()
        {
            if (AssetDatabase.LoadMainAssetAtPath(WrongRing) == null) return;
            if (AssetDatabase.LoadMainAssetAtPath(RightRing) != null) AssetDatabase.DeleteAsset(RightRing);
            string error = AssetDatabase.MoveAsset(WrongRing, RightRing);
            if (!string.IsNullOrEmpty(error)) { Debug.LogError("Duel: Genesis could not move MillenniumRing.obj: " + error); return; }
            if (AssetDatabase.IsValidFolder("Assets/Assets")) AssetDatabase.DeleteAsset("Assets/Assets");   // now empty
            AssetDatabase.Refresh();
            Debug.Log("Duel: Genesis moved MillenniumRing.obj into Resources/DuelGenesis/Characters and removed the stray Assets/Assets folder.");
            EditorApplication.ExecuteMenuItem("Duel Genesis/Characters/Rebuild Millennium Ring");
        }
    }
}
#endif
