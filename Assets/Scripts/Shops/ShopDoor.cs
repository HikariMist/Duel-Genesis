using DuelGenesis.Interaction;
using DuelGenesis.Player;
using UnityEngine;

namespace DuelGenesis.Shops
{
    /// <summary>
    /// A shop doorway: press E to step through to <see cref="destination"/> (inside or back out), like the
    /// "enterable" triggers DMO used for its Kame Game Shop.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ShopDoor : MonoBehaviour, IInteractable
    {
        public Transform destination;
        public string prompt = "Enter the shop";

        public string InteractionPrompt => prompt;

        public void Interact(GameObject interactor)
        {
            if (destination == null || interactor == null) return;
            var controller = interactor.GetComponent<ThirdPersonPlayerController>();
            if (controller != null) controller.Teleport(destination.position, destination.rotation);
            else interactor.transform.SetPositionAndRotation(destination.position, destination.rotation);
            Object.FindFirstObjectByType<ThirdPersonCamera>()?.SnapBehind(destination.forward);
        }
    }
}
