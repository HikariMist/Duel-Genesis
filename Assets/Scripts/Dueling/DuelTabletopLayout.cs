using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Shared local-space measurements for the physical Duel: Genesis tabletop.
    /// </summary>
    public static class DuelTabletopLayout
    {
        public const float BoardSurfaceY = 1.43f;
        public const float HologramBaseY = 1.50f;
        public const float ZoneStartX = -1.68f;
        public const float ZoneSpacing = 0.84f;
        public const float PlayerMonsterZ = -0.34f;
        public const float CpuMonsterZ = 0.34f;
        public const float PlayerBackrowZ = -1.08f;
        public const float CpuBackrowZ = 1.08f;

        public static readonly Vector3 BoardScale = new Vector3(4.70f, 0.035f, 2.78f);
        public static readonly Vector3 ZoneScale = new Vector3(0.66f, 0.018f, 0.76f);
        public static readonly Vector3 CardScale = new Vector3(0.47f, 0.67f, 1f);

        public static Vector3 MonsterZonePosition(int index, bool playerSide)
        {
            return new Vector3(
                ZoneStartX + ZoneSpacing * Mathf.Clamp(index, 0, 4),
                BoardSurfaceY + 0.035f,
                playerSide ? PlayerMonsterZ : CpuMonsterZ);
        }

        public static Vector3 BackrowZonePosition(int index, bool playerSide)
        {
            return new Vector3(
                ZoneStartX + ZoneSpacing * Mathf.Clamp(index, 0, 4),
                BoardSurfaceY + 0.035f,
                playerSide ? PlayerBackrowZ : CpuBackrowZ);
        }

        public static Vector3 DeckPosition(bool playerSide)
        {
            return new Vector3(2.18f, BoardSurfaceY + 0.04f, playerSide ? -1.08f : 1.08f);
        }

        public static Vector3 GraveyardPosition(bool playerSide)
        {
            return new Vector3(-2.18f, BoardSurfaceY + 0.04f, playerSide ? -1.08f : 1.08f);
        }

        // Higher and slightly more overhead so the player's physical hand and the
        // opponent's complete side of the field are both visible at once.
        public static Vector3 CameraLocalPosition => new Vector3(0f, 7.80f, -4.40f);
        public static Vector3 CameraTargetLocalPosition => new Vector3(0f, BoardSurfaceY, 0.12f);
    }
}
