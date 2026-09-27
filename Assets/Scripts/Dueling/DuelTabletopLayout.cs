using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Shared local-space measurements for the physical Duel: Genesis tabletop.
    /// Sized and framed as a clean top-down digital duel board.
    /// </summary>
    public static class DuelTabletopLayout
    {
        public const float BoardSurfaceY = 1.43f;
        public const float HologramBaseY = 1.52f;

        public const float ZoneStartX = -2.16f;
        public const float ZoneSpacing = 1.08f;

        public const float PlayerMonsterZ = -0.56f;
        public const float CpuMonsterZ = 0.56f;
        public const float PlayerBackrowZ = -1.52f;
        public const float CpuBackrowZ = 1.52f;

        public static readonly Vector3 BoardScale = new Vector3(6.40f, 0.045f, 5.00f);
        public static readonly Vector3 ZoneScale = new Vector3(0.86f, 0.018f, 0.82f);
        public static readonly Vector3 CardScale = new Vector3(0.58f, 0.82f, 1f);

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
            return new Vector3(2.86f, BoardSurfaceY + 0.04f, playerSide ? -1.52f : 1.52f);
        }

        public static Vector3 GraveyardPosition(bool playerSide)
        {
            return new Vector3(-2.86f, BoardSurfaceY + 0.04f, playerSide ? -1.52f : 1.52f);
        }

        // Near-overhead framing inspired by digital duel games. A tiny Z offset avoids
        // an exactly collinear look/up vector while still reading as top-down.
        public static Vector3 CameraLocalPosition => new Vector3(0f, 11.35f, -0.32f);
        public static Vector3 CameraTargetLocalPosition => new Vector3(0f, BoardSurfaceY, 0.10f);
    }
}
