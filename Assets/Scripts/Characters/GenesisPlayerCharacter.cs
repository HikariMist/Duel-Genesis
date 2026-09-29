using UnityEngine;

namespace DuelGenesis.Characters
{
    /// <summary>
    /// Sits on the player's "Genesis Avatar" child and builds the player's own character from their saved look
    /// (or the default one) when the game starts. <see cref="Rebuild"/> swaps it live, e.g. from the creator.
    /// </summary>
    public sealed class GenesisPlayerCharacter : MonoBehaviour
    {
        public GameObject Current { get; private set; }

        private void Awake()
        {
            GenesisAppearance look = GenesisAppearanceStore.HasSaved ? GenesisAppearanceStore.Load() : GenesisAppearance.CreateDefault(GenesisGender.Male);
            Rebuild(look);
            if (FindAnyObjectByType<GenesisCharacterCreator>() == null) gameObject.AddComponent<GenesisCharacterCreator>();
        }

        public void Rebuild(GenesisAppearance look)
        {
            if (Current != null) Destroy(Current);
            Current = GenesisCharacterBuilder.Build(transform, look);
            if (Current == null)
            {
                Debug.LogWarning("Duel: Genesis has no imported character yet (Duel Genesis > Characters > 1. Import). The player stays a capsule.");
                return;
            }
            // The capsule stand-in goes once the real character is there.
            MeshRenderer capsule = transform.parent != null ? transform.parent.GetComponent<MeshRenderer>() : null;
            if (capsule != null) capsule.enabled = false;
        }
    }
}
