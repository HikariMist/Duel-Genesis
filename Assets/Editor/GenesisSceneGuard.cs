#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    [InitializeOnLoad]
    public static class GenesisSceneGuard
    {
        private const string PlayableScenePath = "Assets/Scenes/GenesisPrototype.unity";

        static GenesisSceneGuard()
        {
            EditorApplication.delayCall += ConfigurePlayModeScene;
        }

        private static void ConfigurePlayModeScene()
        {
            if (!File.Exists(PlayableScenePath))
                return;

            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayableScenePath);
            if (scene != null && EditorSceneManager.playModeStartScene != scene)
                EditorSceneManager.playModeStartScene = scene;
        }

        [MenuItem("Duel Genesis/Open Playable Scene")]
        public static void OpenPlayableScene()
        {
            if (!File.Exists(PlayableScenePath))
            {
                EditorUtility.DisplayDialog("Duel: Genesis", "GenesisPrototype.unity is missing.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(PlayableScenePath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayableScenePath);
        }
    }
}
#endif
