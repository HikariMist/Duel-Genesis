using DuelGenesis.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelGenesis.Interaction
{
    public class PlayerInteractor : MonoBehaviour
    {
        public float interactionDistance = 3f;
        public LayerMask interactionMask = ~0;
        public CanvasGroup promptCanvas;
        public TMPro.TMP_Text promptText;

        private IInteractable _current;

        private void Update()
        {
            FindInteractable();

            Keyboard keyboard = Keyboard.current;
            if (_current != null && keyboard != null && keyboard.eKey.wasPressedThisFrame)
                _current.Interact(gameObject);
        }

        private void FindInteractable()
        {
            _current = null;
            Camera cam = Camera.main;
            if (cam == null)
            {
                SetPrompt(false, string.Empty);
                return;
            }

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionMask, QueryTriggerInteraction.Collide))
            {
                _current = hit.collider.GetComponentInParent<IInteractable>();
            }

            if (_current != null)
                SetPrompt(true, $"[E] {_current.InteractionPrompt}");
            else
                SetPrompt(false, string.Empty);
        }

        private void SetPrompt(bool visible, string text)
        {
            if (promptCanvas != null)
            {
                promptCanvas.alpha = visible ? 1f : 0f;
                promptCanvas.interactable = false;
                promptCanvas.blocksRaycasts = false;
            }

            if (promptText != null)
                promptText.text = text;
        }
    }
}
