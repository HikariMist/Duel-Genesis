using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Shared local-space measurements for the physical Duel: Genesis tabletop.
    /// The center arena stays compact while utility zones live in side wings that
    /// follow the standard Yu-Gi-Oh field layout from each duelist's perspective.
    /// </summary>
    public static class DuelTabletopLayout
    {
        public const float BoardSurfaceY = 1.43f;
        public const float HologramBaseY = 1.54f;

        // Main five-column play area.
        public const float ZoneStartX = -2.95f;
        public const float ZoneSpacing = 1.475f;

        public const float PlayerMonsterZ = -0.76f;
        public const float CpuMonsterZ = 0.76f;
        public const float PlayerBackrowZ = -2.12f;
        public const float CpuBackrowZ = 2.12f;

        // The center arena is only slightly wider than the version the user approved.
        // Side-wing housings extend beyond this footprint for Deck / Extra Deck / etc.
        public static readonly Vector3 BoardScale = new Vector3(9.70f, 0.045f, 6.90f);
        public static readonly Vector3 ZoneScale = new Vector3(1.22f, 0.028f, 1.08f);
        public static readonly Vector3 CardScale = new Vector3(0.74f, 1.04f, 1f);
        public static readonly Vector3 UtilityZoneScale = new Vector3(1.14f, 0.030f, 1.34f);

        public static Vector3 MonsterZonePosition(int index, bool playerSide)
        {
            return new Vector3(
                ZoneStartX + ZoneSpacing * Mathf.Clamp(index, 0, 4),
                BoardSurfaceY + 0.042f,
                playerSide ? PlayerMonsterZ : CpuMonsterZ);
        }

        public static Vector3 BackrowZonePosition(int index, bool playerSide)
        {
            return new Vector3(
                ZoneStartX + ZoneSpacing * Mathf.Clamp(index, 0, 4),
                BoardSurfaceY + 0.042f,
                playerSide ? PlayerBackrowZ : CpuBackrowZ);
        }

        // From the PLAYER'S viewpoint:
        // left side = Field Zone above Extra Deck.
        public static Vector3 FieldZonePosition(bool playerSide)
        {
            return playerSide
                ? new Vector3(-5.35f, BoardSurfaceY + 0.055f, -0.98f)
                : new Vector3(5.35f, BoardSurfaceY + 0.055f, 0.98f);
        }

        public static Vector3 ExtraDeckPosition(bool playerSide)
        {
            return playerSide
                ? new Vector3(-5.35f, BoardSurfaceY + 0.055f, -2.38f)
                : new Vector3(5.35f, BoardSurfaceY + 0.055f, 2.38f);
        }

        // From the PLAYER'S viewpoint:
        // right side = Graveyard above Main Deck.
        public static Vector3 GraveyardPosition(bool playerSide)
        {
            return playerSide
                ? new Vector3(5.35f, BoardSurfaceY + 0.055f, -0.98f)
                : new Vector3(-5.35f, BoardSurfaceY + 0.055f, 0.98f);
        }

        public static Vector3 DeckPosition(bool playerSide)
        {
            return playerSide
                ? new Vector3(5.35f, BoardSurfaceY + 0.055f, -2.38f)
                : new Vector3(-5.35f, BoardSurfaceY + 0.055f, 2.38f);
        }

        // Banished cards are kept next to the Graveyard. This small outer tray is
        // intentionally offset farther from the central play surface.
        public static Vector3 BanishedPosition(bool playerSide)
        {
            return playerSide
                ? new Vector3(6.52f, BoardSurfaceY + 0.055f, -0.98f)
                : new Vector3(-6.52f, BoardSurfaceY + 0.055f, 0.98f);
        }

        // Shared Extra Monster Zones between the two Main Monster rows.
        public static Vector3 ExtraMonsterZonePosition(int index)
        {
            float x = index <= 0 ? -0.78f : 0.78f;
            return new Vector3(x, BoardSurfaceY + 0.044f, 0f);
        }

        // Slightly wider framing to include the new side pods without making the arena
        // feel smaller on screen.
        public static Vector3 CameraLocalPosition => new Vector3(0f, 5.85f, -5.00f);
        public static Vector3 CameraTargetLocalPosition => new Vector3(0f, 1.35f, 0.10f);
    }
}
