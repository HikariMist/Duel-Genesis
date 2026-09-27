using System.Collections.Generic;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Keeps stable physical field-slot assignments while the core duel engine continues
    /// to use compact lists. Player cards can be assigned to an exact clicked zone and
    /// CPU cards automatically take the first available slot. Removing a card frees only
    /// its own slot instead of shifting every remaining card across the field.
    /// </summary>
    public static class DuelFieldSlotRegistry
    {
        private static readonly Dictionary<DuelMonsterState, int> PlayerMonsterSlots = new();
        private static readonly Dictionary<DuelMonsterState, int> CpuMonsterSlots = new();
        private static readonly Dictionary<DuelBackrowState, int> PlayerBackrowSlots = new();
        private static readonly Dictionary<DuelBackrowState, int> CpuBackrowSlots = new();

        public static void Reset()
        {
            PlayerMonsterSlots.Clear();
            CpuMonsterSlots.Clear();
            PlayerBackrowSlots.Clear();
            CpuBackrowSlots.Clear();
        }

        public static void AssignMonster(DuelMonsterState state, bool playerSide, int slot)
        {
            if (state == null) return;
            Dictionary<DuelMonsterState, int> map = playerSide ? PlayerMonsterSlots : CpuMonsterSlots;
            map[state] = ClampSlot(slot);
        }

        public static void AssignBackrow(DuelBackrowState state, bool playerSide, int slot)
        {
            if (state == null) return;
            Dictionary<DuelBackrowState, int> map = playerSide ? PlayerBackrowSlots : CpuBackrowSlots;
            map[state] = ClampSlot(slot);
        }

        public static int GetMonsterSlot(DuelMonsterState state, bool playerSide, IReadOnlyList<DuelMonsterState> current)
        {
            if (state == null) return 0;
            Dictionary<DuelMonsterState, int> map = playerSide ? PlayerMonsterSlots : CpuMonsterSlots;
            CleanupMonsterMap(map, current);

            if (map.TryGetValue(state, out int slot))
                return slot;

            slot = FirstOpenMonsterSlot(map);
            map[state] = slot;
            return slot;
        }

        public static int GetBackrowSlot(DuelBackrowState state, bool playerSide, IReadOnlyList<DuelBackrowState> current)
        {
            if (state == null) return 0;
            Dictionary<DuelBackrowState, int> map = playerSide ? PlayerBackrowSlots : CpuBackrowSlots;
            CleanupBackrowMap(map, current);

            if (map.TryGetValue(state, out int slot))
                return slot;

            slot = FirstOpenBackrowSlot(map);
            map[state] = slot;
            return slot;
        }

        public static bool IsMonsterSlotOccupied(IReadOnlyList<DuelMonsterState> current, bool playerSide, int slot)
        {
            Dictionary<DuelMonsterState, int> map = playerSide ? PlayerMonsterSlots : CpuMonsterSlots;
            CleanupMonsterMap(map, current);
            for (int i = 0; current != null && i < current.Count; i++)
            {
                DuelMonsterState state = current[i];
                if (state != null && GetMonsterSlot(state, playerSide, current) == slot)
                    return true;
            }
            return false;
        }

        public static bool IsBackrowSlotOccupied(IReadOnlyList<DuelBackrowState> current, bool playerSide, int slot)
        {
            Dictionary<DuelBackrowState, int> map = playerSide ? PlayerBackrowSlots : CpuBackrowSlots;
            CleanupBackrowMap(map, current);
            for (int i = 0; current != null && i < current.Count; i++)
            {
                DuelBackrowState state = current[i];
                if (state != null && GetBackrowSlot(state, playerSide, current) == slot)
                    return true;
            }
            return false;
        }

        private static int FirstOpenMonsterSlot(Dictionary<DuelMonsterState, int> map)
        {
            for (int slot = 0; slot < 5; slot++)
            {
                bool used = false;
                foreach (KeyValuePair<DuelMonsterState, int> pair in map)
                {
                    if (pair.Value == slot)
                    {
                        used = true;
                        break;
                    }
                }
                if (!used) return slot;
            }
            return 0;
        }

        private static int FirstOpenBackrowSlot(Dictionary<DuelBackrowState, int> map)
        {
            for (int slot = 0; slot < 5; slot++)
            {
                bool used = false;
                foreach (KeyValuePair<DuelBackrowState, int> pair in map)
                {
                    if (pair.Value == slot)
                    {
                        used = true;
                        break;
                    }
                }
                if (!used) return slot;
            }
            return 0;
        }

        private static void CleanupMonsterMap(Dictionary<DuelMonsterState, int> map, IReadOnlyList<DuelMonsterState> current)
        {
            List<DuelMonsterState> stale = null;
            foreach (KeyValuePair<DuelMonsterState, int> pair in map)
            {
                if (ContainsMonster(current, pair.Key)) continue;
                stale ??= new List<DuelMonsterState>();
                stale.Add(pair.Key);
            }

            if (stale == null) return;
            for (int i = 0; i < stale.Count; i++)
                map.Remove(stale[i]);
        }

        private static void CleanupBackrowMap(Dictionary<DuelBackrowState, int> map, IReadOnlyList<DuelBackrowState> current)
        {
            List<DuelBackrowState> stale = null;
            foreach (KeyValuePair<DuelBackrowState, int> pair in map)
            {
                if (ContainsBackrow(current, pair.Key)) continue;
                stale ??= new List<DuelBackrowState>();
                stale.Add(pair.Key);
            }

            if (stale == null) return;
            for (int i = 0; i < stale.Count; i++)
                map.Remove(stale[i]);
        }

        private static bool ContainsMonster(IReadOnlyList<DuelMonsterState> list, DuelMonsterState target)
        {
            for (int i = 0; list != null && i < list.Count; i++)
                if (ReferenceEquals(list[i], target)) return true;
            return false;
        }

        private static bool ContainsBackrow(IReadOnlyList<DuelBackrowState> list, DuelBackrowState target)
        {
            for (int i = 0; list != null && i < list.Count; i++)
                if (ReferenceEquals(list[i], target)) return true;
            return false;
        }

        private static int ClampSlot(int slot)
        {
            if (slot < 0) return 0;
            if (slot > 4) return 4;
            return slot;
        }
    }
}
