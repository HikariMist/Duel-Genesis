using System.Reflection;
using DuelGenesis.UI;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Small permanent HUD control for leaving an active duel without bringing
    /// back the old full-screen duel menu.
    /// </summary>
    public sealed class DuelForfeitButton : MonoBehaviour
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private DuelGameController _duel;
        private MethodInfo _forfeit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<DuelForfeitButton>() != null)
                return;

            GameObject host = new GameObject("Duel Forfeit Button");
            host.AddComponent<DuelForfeitButton>();
        }

        private void Awake()
        {
            _forfeit = typeof(DuelGameController).GetMethod("Forfeit", PrivateInstance);
        }

        private void Update()
        {
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();
        }

        private void OnGUI()
        {
            if (_duel == null || !_duel.IsActive || _duel.IsDuelOver)
                return;

            GUI.depth = -650;
            Rect button = new Rect(18f, 20f, 112f, 34f);
            if (GenesisTheme.Button(button, "FORFEIT", GenesisTheme.Danger))
                _forfeit?.Invoke(_duel, null);
        }
    }
}
