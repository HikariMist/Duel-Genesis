using System;
using UnityEngine;

namespace DuelGenesis.Progression
{
    public class DuelistProfile : MonoBehaviour
    {
        private const string LevelKey = "DUEL_GENESIS_LEVEL_V1";
        private const string XpKey = "DUEL_GENESIS_XP_V1";
        private const string WinsKey = "DUEL_GENESIS_WINS_V1";
        private const string LossesKey = "DUEL_GENESIS_LOSSES_V1";
        private const string PacksKey = "DUEL_GENESIS_PACKS_V1";

        [SerializeField, Min(1)] private int level = 1;
        [SerializeField, Min(0)] private int xp;
        [SerializeField, Min(0)] private int wins;
        [SerializeField, Min(0)] private int losses;
        [SerializeField, Min(0)] private int packsOpened;

        public event Action ProgressChanged;

        public int Level => level;
        public int XP => xp;
        public int Wins => wins;
        public int Losses => losses;
        public int PacksOpened => packsOpened;
        public int XPToNextLevel => RequiredXPForNextLevel(level);
        public float LevelProgress01 => XPToNextLevel <= 0 ? 1f : Mathf.Clamp01(xp / (float)XPToNextLevel);
        public string Title => GetTitleForLevel(level);

        private void Awake()
        {
            Load();
        }

        public void AddXP(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (amount == 0) return;

            xp += amount;
            while (level < 100 && xp >= RequiredXPForNextLevel(level))
            {
                xp -= RequiredXPForNextLevel(level);
                level++;
                Debug.Log($"Duelist Level Up! Level {level} — {Title}");
            }

            if (level >= 100)
                xp = 0;

            SaveAndNotify();
        }

        public void RecordPackOpened()
        {
            packsOpened++;
            AddXP(25);
        }

        public void RecordDuelResult(bool won)
        {
            if (won)
            {
                wins++;
                AddXP(60);
            }
            else
            {
                losses++;
                AddXP(30);
            }
        }

        public static int RequiredXPForNextLevel(int currentLevel)
        {
            currentLevel = Mathf.Clamp(currentLevel, 1, 100);
            if (currentLevel >= 100) return 0;
            return 100 + (currentLevel - 1) * 35;
        }

        public static string GetTitleForLevel(int currentLevel)
        {
            if (currentLevel >= 100) return "Genesis Legend";
            if (currentLevel >= 75) return "Arena Elite";
            if (currentLevel >= 60) return "Master Duelist";
            if (currentLevel >= 50) return "Genesis Veteran";
            if (currentLevel >= 40) return "Holo Vanguard";
            if (currentLevel >= 30) return "Neon Tactician";
            if (currentLevel >= 20) return "Seasoned Duelist";
            if (currentLevel >= 10) return "Genesis City Duelist";
            if (currentLevel >= 5) return "Card Hunter";
            return "Rookie Duelist";
        }

        private void Load()
        {
            level = Mathf.Max(1, PlayerPrefs.GetInt(LevelKey, 1));
            xp = Mathf.Max(0, PlayerPrefs.GetInt(XpKey, 0));
            wins = Mathf.Max(0, PlayerPrefs.GetInt(WinsKey, 0));
            losses = Mathf.Max(0, PlayerPrefs.GetInt(LossesKey, 0));
            packsOpened = Mathf.Max(0, PlayerPrefs.GetInt(PacksKey, 0));
        }

        private void SaveAndNotify()
        {
            PlayerPrefs.SetInt(LevelKey, level);
            PlayerPrefs.SetInt(XpKey, xp);
            PlayerPrefs.SetInt(WinsKey, wins);
            PlayerPrefs.SetInt(LossesKey, losses);
            PlayerPrefs.SetInt(PacksKey, packsOpened);
            PlayerPrefs.Save();
            ProgressChanged?.Invoke();
        }

#if UNITY_EDITOR
        [ContextMenu("DEV Reset Duelist Profile")]
        private void DevReset()
        {
            level = 1;
            xp = 0;
            wins = 0;
            losses = 0;
            packsOpened = 0;
            SaveAndNotify();
        }
#endif
    }
}
