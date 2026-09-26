using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DuelGenesis.Interaction
{
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("Interaction")]
        public float interactionDistance = 3.5f;
        public float cameraRayDistance = 20f;
        public LayerMask interactionMask = ~0;

        [Header("UI")]
        public CanvasGroup promptCanvas;
        public Text promptText;

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

            // In third-person the camera sits several meters behind the player, so a short
            // ray from the camera can never reach an object that is actually close to the player.
            // We raycast farther, ignore the player's own collider, then enforce the real
            // interaction distance from the player to the object.
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                cameraRayDistance,
                interactionMask,
                QueryTriggerInteraction.Collide
            );

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (IsOwnCollider(hit.collider))
                    continue;

                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable == null)
                    continue;

                Vector3 closestPoint = hit.collider.ClosestPoint(transform.position);
                float playerDistance = Vector3.Distance(transform.position, closestPoint);
                if (playerDistance > interactionDistance)
                    continue;

                _current = interactable;
                break;
            }

            // Friendly fallback: if the center-screen ray misses slightly, find a nearby
            // interactable around the player instead of requiring pixel-perfect aiming.
            if (_current == null)
                _current = FindNearbyInteractable();

            if (_current != null)
                SetPrompt(true, $"[E] {_current.InteractionPrompt}");
            else
                SetPrompt(false, string.Empty);
        }

        private IInteractable FindNearbyInteractable()
        {
            Collider[] nearby = Physics.OverlapSphere(
                transform.position,
                interactionDistance,
                interactionMask,
                QueryTriggerInteraction.Collide
            );

            IInteractable best = null;
            float bestScore = float.MaxValue;
            Camera cam = Camera.main;

            foreach (Collider col in nearby)
            {
                if (IsOwnCollider(col))
                    continue;

                IInteractable interactable = col.GetComponentInParent<IInteractable>();
                if (interactable == null)
                    continue;

                Vector3 closestPoint = col.ClosestPoint(transform.position);
                Vector3 toTarget = closestPoint - transform.position;
                float distance = toTarget.magnitude;

                // Prefer objects that are both close and roughly in front of the camera.
                float anglePenalty = 0f;
                if (cam != null && toTarget.sqrMagnitude > 0.001f)
                {
                    float dot = Vector3.Dot(cam.transform.forward.normalized, toTarget.normalized);
                    anglePenalty = Mathf.Lerp(2f, 0f, Mathf.InverseLerp(-0.25f, 1f, dot));
                }

                float score = distance + anglePenalty;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = interactable;
                }
            }

            return best;
        }

        private bool IsOwnCollider(Collider col)
        {
            if (col == null)
                return false;

            return col.gameObject == gameObject || col.transform.IsChildOf(transform);
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
