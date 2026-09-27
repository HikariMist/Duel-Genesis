using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Classic two-row Yu-Gi-Oh field layout based on the physical game-mat reference.
    /// From each duelist's viewpoint:
    /// top row    = Field Zone, five Monster Zones, Graveyard
    /// bottom row = Extra Deck, five Spell/Trap Zones, Main Deck
    /// Banished cards use a small pocket just outside the Graveyard edge.
    /// </summary>
    public static class DuelTabletopLayout
    {
        public const float BoardSurfaceY = 1.43f;
        public const float HologramBaseY = 1.56f;

        // Seven equal columns across the mat. The five center columns are the playable rows.
        public const float ColumnSpacing = 1.40f;
        public const float ZoneStartX = -2.80f;
        public const float UtilityX = 4.20f;

        // Player rows are on the near half, CPU rows mirror them on the far half.
        public const float PlayerMonsterZ = -0.78f;
        public const float CpuMonsterZ = 0.78f;
        public const float PlayerBackrowZ = -2.22f;
        public const float CpuBackrowZ = 2.22f;

        public static readonly Vector3 BoardScale = new Vector3(10.10f, 0.045f, 6.70f);
        public static readonly Vector3 ZoneScale = new Vector3(1.10f, 0.026f, 1.22f);
        public static readonly Vector3 UtilityZoneScale = new Vector3(1.10f, 0.026f, 1.22f);
        public static readonly Vector3 CardScale = new Vector3(0.74f, 1.04f, 1f);

        public static Vector3 MonsterZonePosition(int index, bool playerSide)
        {
            return new Vector3(
                ZoneStartX + ColumnSpacing * Mathf.Clamp(index, 0, 4),
                BoardSurfaceY + 0.048f,
                playerSide ? PlayerMonsterZ : CpuMonsterZ);
        }

        public static Vector3 BackrowZonePosition(int index, bool playerSide)
        {
            return new Vector3(
                ZoneStartX + ColumnSpacing * Mathf.Clamp(index, 0, 4),
                BoardSurfaceY + 0.048f,
                playerSide ? PlayerBackrowZ : CpuBackrowZ);
        }

        // Player: Field left of monsters. CPU is a 180-degree mirror from its viewpoint.
        public static Vector3 FieldZonePosition(bool playerSide)
        {
            return playerSide
                ? new Vector3(-UtilityX, BoardSurfaceY + 0.048f, PlayerMonsterZ)
                : new Vector3(UtilityX, BoardSurfaceY + 0.048f, CpuMonsterZ);
        }

        // Player: Extra Deck directly below Field Zone.
        public static Vector3 ExtraDeckPosition(bool playerSide)
        {
            return playerSide
                ? new Vector3(-UtilityX, BoardSurfaceY + 0.048f, PlayerBackrowZ)
                : new Vector3(UtilityX, BoardSurfaceY + 0.048f, CpuBackrowZ);
        }

        // Player: Graveyard right of monsters.
        public static Vector3 GraveyardPosition(bool playerSide)
        {
            return playerSide
                ? new Vector3(UtilityX, BoardSurfaceY + 0.048f, PlayerMonsterZ)
                : new Vector3(-UtilityX, BoardSurfaceY + 0.048f, CpuMonsterZ);
        }

        // Player: Main Deck directly below Graveyard.
        public static Vector3 DeckPosition(bool playerSide)
        {
            return playerSide
                ? new Vector3(UtilityX, BoardSurfaceY + 0.048f, PlayerBackrowZ)
                : new Vector3(-UtilityX, BoardSurfaceY + 0.048f, CpuBackrowZ);
        }

        // The reference mat has no printed banished zone, so this is a small external pocket
        // attached beside the Graveyard rather than another full-size field column.
        public static Vector3 BanishedPosition(bool playerSide)
        {
            return playerSide
                ? new Vector3(5.35f, BoardSurfaceY + 0.055f, PlayerMonsterZ)
                : new Vector3(-5.35f, BoardSurfaceY + 0.055f, CpuMonsterZ);
        }

        // Retained for rules compatibility, but the classic reference presentation hides
        // these pads until modern Extra Monster Zone rules are intentionally enabled later.
        public static Vector3 ExtraMonsterZonePosition(int index)
        {
            float x = index <= 0 ? -0.70f : 0.70f;
            return new Vector3(x, BoardSurfaceY + 0.044f, 0f);
        }

        // Keep the arena size the user approved while showing all seven field columns.
        public static Vector3 CameraLocalPosition => new Vector3(0f, 5.75f, -5.05f);
        public static Vector3 CameraTargetLocalPosition => new Vector3(0f, 1.34f, 0.05f);
    }
}
