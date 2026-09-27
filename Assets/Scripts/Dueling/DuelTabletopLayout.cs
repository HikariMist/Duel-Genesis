using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Shared local-space measurements for the physical Duel: Genesis tabletop.
    /// Built around a wide, readable, Master-Duel-style board mounted inside a real table.
    /// </summary>
    public static class DuelTabletopLayout
    {
        public const float BoardSurfaceY = 1.43f;
        public const float HologramBaseY = 1.54f;

        public const float ZoneStartX = -3.10f;
        public const float ZoneSpacing = 1.55f;

        public const float PlayerMonsterZ = -0.82f;
        public const float CpuMonsterZ = 0.82f;
        public const float PlayerBackrowZ = -2.28f;
        public const float CpuBackrowZ = 2.28f;

        public static readonly Vector3 BoardScale = new Vector3(9.40f, 0.045f, 6.90f);
        public static readonly Vector3 ZoneScale = new Vector3(1.25f, 0.028f, 1.12f);
        public static readonly Vector3 CardScale = new Vector3(0.74f, 1.04f, 1f);

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
            return new Vector3(4.18f, BoardSurfaceY + 0.055f, playerSide ? -2.28f : 2.28f);
        }

        public static Vector3 GraveyardPosition(bool playerSide)
        {
            return new Vector3(-4.18f, BoardSurfaceY + 0.055f, playerSide ? -2.28f : 2.28f);
        }

        // Angled overhead view that deliberately keeps the physical table body and near legs visible.
        // It is still high enough to read and click the duel zones comfortably.
        public static Vector3 CameraLocalPosition => new Vector3(0f, 8.35f, -7.25f);
        public static Vector3 CameraTargetLocalPosition => new Vector3(0f, 1.12f, 0.30f);
    }
}
