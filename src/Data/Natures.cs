namespace HowToFishMon
{
    public static class Stat
    {
        public const int HP = 0, Atk = 1, Def = 2, SpA = 3, SpD = 4, Spe = 5;
        public static readonly string[] Short = { "HP", "Atk", "Def", "SpA", "SpD", "Spe" };
        public static readonly string[] Long = { "HP", "Attack", "Defense", "Sp. Atk", "Sp. Def", "Speed" };
    }

    public static class Natures
    {
        // name, boosted stat, lowered stat (-1 = neutral)
        public static readonly (string name, int up, int down)[] All =
        {
            ("Hardy", -1, -1),   ("Lonely", 1, 2),  ("Brave", 1, 5),   ("Adamant", 1, 3), ("Naughty", 1, 4),
            ("Bold", 2, 1),      ("Docile", -1, -1),("Relaxed", 2, 5), ("Impish", 2, 3),  ("Lax", 2, 4),
            ("Timid", 5, 1),     ("Hasty", 5, 2),   ("Serious", -1, -1),("Jolly", 5, 3),  ("Naive", 5, 4),
            ("Modest", 3, 1),    ("Mild", 3, 2),    ("Quiet", 3, 5),   ("Bashful", -1, -1),("Rash", 3, 4),
            ("Calm", 4, 1),      ("Gentle", 4, 2),  ("Sassy", 4, 5),   ("Careful", 4, 3), ("Quirky", -1, -1),
        };

        public static float Modifier(int nature, int stat)
        {
            if (nature < 0 || nature >= All.Length) return 1f;
            var n = All[nature];
            if (n.up == stat) return 1.1f;
            if (n.down == stat) return 0.9f;
            return 1f;
        }
    }
}
