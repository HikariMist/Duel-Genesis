using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Real-world geometry of a Yu-Gi-Oh! duel, in millimetres, converted to table-local metres.
    ///
    /// Sources: official card size 59 x 86 mm; a standard single-player game mat is about
    /// 600 x 350 mm; current Master Rule zone layout (per duelist, from their own seat):
    ///
    ///                 [Banished]  (outer end of the shared centre row)
    ///   centre row:        [EMZ]        [EMZ]            (shared Extra Monster Zones)
    ///   front row:  [Field] [M1][M2][M3][M4][M5] [GY]
    ///   back row:   [Extra] [S1][S2][S3][S4][S5] [Deck]
    ///
    /// The two mats meet at the table's centre line. Duelist 0 (the human) sits at -Z.
    /// World units are metres (the Genesis City character is 2 m tall), so the table is life size.
    /// </summary>
    public static class DuelMatLayout
    {
        // ---- physical constants (mm)
        public const float CardWidthMm = 59f;
        public const float CardHeightMm = 86f;
        public const float CardThicknessMm = 0.32f;
        public const float ZoneWidthMm = 67f;          // sleeved card + printed border
        public const float ZoneHeightMm = 94f;
        public const float ColumnPitchMm = 80f;        // 7 columns across a 600 mm mat
        public const float RowPitchMm = 106f;          // front row / back row spacing
        public const float MatWidthMm = 600f;
        public const float MatDepthMm = 350f;           // per duelist

        // ---- furniture (m)
        public const float TableWidth = 1.20f;
        public const float TableDepth = 0.90f;
        public const float TableHeight = 0.76f;
        public const float TableTopThickness = 0.035f;
        public const float MatThickness = 0.002f;

        public static float SurfaceY => TableHeight + MatThickness;
        public static Vector3 CardSize => new Vector3(CardWidthMm, CardThicknessMm, CardHeightMm) * 0.001f;

        public const int ColumnField = 0;
        public const int ColumnGraveyard = 6;

        /// <summary>Column centre, measured from the table centre, from a duelist's own point of view (mm).</summary>
        public static float ColumnX(int column) => (column - 3) * ColumnPitchMm;

        public const float CenterRowZ = 0f;
        public const float FrontRowZ = -RowPitchMm;          // Monster Zones, Field Zone, Graveyard
        public const float BackRowZ = -2f * RowPitchMm;      // Spell & Trap Zones, Extra Deck, Deck
        public const float HandRowZ = -MatDepthMm + 20f;

        /// <summary>Converts duelist-relative millimetres into table-local metres.</summary>
        public static Vector3 Local(int player, float xMm, float zMm, float heightMm = 0f)
        {
            float sign = player == 0 ? 1f : -1f;
            return new Vector3(sign * xMm * 0.001f, SurfaceY + heightMm * 0.001f, sign * zMm * 0.001f);
        }

        /// <summary>Card yaw so the top of the card points away from its controller.</summary>
        public static float Yaw(int player) => player == 0 ? 0f : 180f;

        public static Vector3 MonsterZone(int player, int slot) => Local(player, ColumnX(slot + 1), FrontRowZ);
        public static Vector3 SpellTrapZone(int player, int slot) => Local(player, ColumnX(slot + 1), BackRowZ);
        public static Vector3 FieldZone(int player) => Local(player, ColumnX(ColumnField), FrontRowZ);
        public static Vector3 ExtraDeckZone(int player) => Local(player, ColumnX(ColumnField), BackRowZ);
        public static Vector3 GraveyardZone(int player) => Local(player, ColumnX(ColumnGraveyard), FrontRowZ);
        public static Vector3 DeckZone(int player) => Local(player, ColumnX(ColumnGraveyard), BackRowZ);
        public static Vector3 BanishedZone(int player) => Local(player, ColumnX(ColumnGraveyard), CenterRowZ);

        /// <summary>The two shared Extra Monster Zones sit over Main Monster Zone columns 2 and 4.</summary>
        public static Vector3 ExtraMonsterZone(int index) => Local(0, ColumnX(index == 0 ? 2 : 4), CenterRowZ);

        /// <summary>Opponent's hand: cards held just behind their mat edge, backs towards us.</summary>
        public static Vector3 OpponentHandCard(int index, int count)
        {
            float spread = Mathf.Min(34f, 300f / Mathf.Max(1, count));
            float x = (index - (count - 1) * 0.5f) * spread;
            return Local(1, x, -MatDepthMm - 30f, 55f + Mathf.Abs(index - (count - 1) * 0.5f) * -1.5f);
        }
    }
}
