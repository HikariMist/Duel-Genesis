using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Shared local-space measurements for the physical Duel: Genesis tabletop.
    /// Built around a wide, readable, Master-Duel-style overhead board.
    /// </summary>
    public static class DuelTabletopLayout
    {
        public const float BoardSurfaceY = 1.43f;
        public const float HologramBaseY = 1.54f;

        public const float ZoneStartX = -2.80f;
        public const float ZoneSpacing = 1.40f;

        public const float PlayerMonsterZ = -0.76f;
        public const float CpuMonsterZ = 0.76f;
        public const float PlayerBackrowZ = -2.05f;
        public const float CpuBackrowZ = 2.05f;

        // Large, easy-to-click zones. The board deliberately leaves generous margins
        // around all ten main zones for deck/GY piles and targeting feedback.
        public static readonly Vector3 BoardScale = new Vector3(8.50f, 0.045f, 6.35f);
        public static readonly Vector3 ZoneScale = new Vector3(1.12f, 0.025f, 1.04f);
        public static readonly Vector3 CardScale = new Vector3(0.68f, 0.96f, 1f);

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

        public static Vector3 DeckPosition(bool playerSide)
        {
            return new Vector3(3.75f, BoardSurfaceY + 0.055f, playerSide ? -2.05f : 2.05f);
        }

        public static Vector3 GraveyardPosition(bool playerSide)
        {
            return new Vector3(-3.75f, BoardSurfaceY + 0.055f, playerSide ? -2.05f : 2.05f);
        }

        // Almost top-down, with a small forward offset so depth and hologram height remain visible.
        public static Vector3 CameraLocalPosition => new Vector3(0f, 12.80f, -1.15f);
        public static Vector3 CameraTargetLocalPosition => new Vector3(0f, BoardSurfaceY, 0.12f);
    }
}
