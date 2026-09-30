#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    /// <summary>Logs how long pressing Play takes (Edit mode -> first Play frame), to keep an eye on start-up time.</summary>
    [InitializeOnLoad]
    public static class GenesisPlayModeTimer
    {
        private const string Key = "dg.playTimer.start";

        static GenesisPlayModeTimer()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        private static void Changed(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
                SessionState.SetFloat(Key, (float)EditorApplication.timeSinceStartup);
            else if (change == PlayModeStateChange.EnteredPlayMode)
            {
                float start = SessionState.GetFloat(Key, -1f);
                if (start >= 0f)
                {
                    float seconds = (float)EditorApplication.timeSinceStartup - start;
                    EditorApplication.delayCall += () => Debug.Log($"Duel: Genesis entered Play mode in {seconds:0.0} s (first frame after {(float)EditorApplication.timeSinceStartup - start:0.0} s).");
                }
            }
        }
    }
}
#endif
