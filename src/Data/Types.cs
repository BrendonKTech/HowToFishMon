using System.Collections.Generic;
using UnityEngine;

namespace HowToFishMon
{
    public enum MonType
    {
        None, Normal, Fire, Water, Electric, Grass, Ice, Fighting, Poison, Ground,
        Flying, Psychic, Bug, Rock, Ghost, Dragon, Dark, Steel, Fairy
    }

    public static class TypeChart
    {
        private static readonly Dictionary<MonType, (MonType[] strong, MonType[] weak, MonType[] immune)> Chart =
            new Dictionary<MonType, (MonType[], MonType[], MonType[])>
        {
            { MonType.Normal,   (new MonType[0], new[]{ MonType.Rock, MonType.Steel }, new[]{ MonType.Ghost }) },
            { MonType.Fire,     (new[]{ MonType.Grass, MonType.Ice, MonType.Bug, MonType.Steel }, new[]{ MonType.Fire, MonType.Water, MonType.Rock, MonType.Dragon }, new MonType[0]) },
            { MonType.Water,    (new[]{ MonType.Fire, MonType.Ground, MonType.Rock }, new[]{ MonType.Water, MonType.Grass, MonType.Dragon }, new MonType[0]) },
            { MonType.Electric, (new[]{ MonType.Water, MonType.Flying }, new[]{ MonType.Electric, MonType.Grass, MonType.Dragon }, new[]{ MonType.Ground }) },
            { MonType.Grass,    (new[]{ MonType.Water, MonType.Ground, MonType.Rock }, new[]{ MonType.Fire, MonType.Grass, MonType.Poison, MonType.Flying, MonType.Bug, MonType.Dragon, MonType.Steel }, new MonType[0]) },
            { MonType.Ice,      (new[]{ MonType.Grass, MonType.Ground, MonType.Flying, MonType.Dragon }, new[]{ MonType.Fire, MonType.Water, MonType.Ice, MonType.Steel }, new MonType[0]) },
            { MonType.Fighting, (new[]{ MonType.Normal, MonType.Ice, MonType.Rock, MonType.Dark, MonType.Steel }, new[]{ MonType.Poison, MonType.Flying, MonType.Psychic, MonType.Bug, MonType.Fairy }, new[]{ MonType.Ghost }) },
            { MonType.Poison,   (new[]{ MonType.Grass, MonType.Fairy }, new[]{ MonType.Poison, MonType.Ground, MonType.Rock, MonType.Ghost }, new[]{ MonType.Steel }) },
            { MonType.Ground,   (new[]{ MonType.Fire, MonType.Electric, MonType.Poison, MonType.Rock, MonType.Steel }, new[]{ MonType.Grass, MonType.Bug }, new[]{ MonType.Flying }) },
            { MonType.Flying,   (new[]{ MonType.Grass, MonType.Fighting, MonType.Bug }, new[]{ MonType.Electric, MonType.Rock, MonType.Steel }, new MonType[0]) },
            { MonType.Psychic,  (new[]{ MonType.Fighting, MonType.Poison }, new[]{ MonType.Psychic, MonType.Steel }, new[]{ MonType.Dark }) },
            { MonType.Bug,      (new[]{ MonType.Grass, MonType.Psychic, MonType.Dark }, new[]{ MonType.Fire, MonType.Fighting, MonType.Poison, MonType.Flying, MonType.Ghost, MonType.Steel, MonType.Fairy }, new MonType[0]) },
            { MonType.Rock,     (new[]{ MonType.Fire, MonType.Ice, MonType.Flying, MonType.Bug }, new[]{ MonType.Fighting, MonType.Ground, MonType.Steel }, new MonType[0]) },
            { MonType.Ghost,    (new[]{ MonType.Psychic, MonType.Ghost }, new[]{ MonType.Dark }, new[]{ MonType.Normal }) },
            { MonType.Dragon,   (new[]{ MonType.Dragon }, new[]{ MonType.Steel }, new[]{ MonType.Fairy }) },
            { MonType.Dark,     (new[]{ MonType.Psychic, MonType.Ghost }, new[]{ MonType.Fighting, MonType.Dark, MonType.Fairy }, new MonType[0]) },
            { MonType.Steel,    (new[]{ MonType.Ice, MonType.Rock, MonType.Fairy }, new[]{ MonType.Fire, MonType.Water, MonType.Electric, MonType.Steel }, new MonType[0]) },
            { MonType.Fairy,    (new[]{ MonType.Fighting, MonType.Dragon, MonType.Dark }, new[]{ MonType.Fire, MonType.Poison, MonType.Steel }, new MonType[0]) },
        };

        public static float Single(MonType attack, MonType defend)
        {
            if (attack == MonType.None || defend == MonType.None || !Chart.TryGetValue(attack, out var row)) return 1f;
            if (System.Array.IndexOf(row.immune, defend) >= 0) return 0f;
            if (System.Array.IndexOf(row.strong, defend) >= 0) return 2f;
            if (System.Array.IndexOf(row.weak, defend) >= 0) return 0.5f;
            return 1f;
        }

        public static float Effectiveness(MonType attack, MonType defend1, MonType defend2)
        {
            float m = Single(attack, defend1);
            if (defend2 != defend1) m *= Single(attack, defend2);
            return m;
        }

        public static Color ColorOf(MonType t)
        {
            switch (t)
            {
                case MonType.Normal: return new Color(0.66f, 0.66f, 0.47f);
                case MonType.Fire: return new Color(0.94f, 0.50f, 0.19f);
                case MonType.Water: return new Color(0.41f, 0.56f, 0.94f);
                case MonType.Electric: return new Color(0.97f, 0.82f, 0.19f);
                case MonType.Grass: return new Color(0.47f, 0.78f, 0.31f);
                case MonType.Ice: return new Color(0.60f, 0.85f, 0.85f);
                case MonType.Fighting: return new Color(0.75f, 0.19f, 0.16f);
                case MonType.Poison: return new Color(0.63f, 0.25f, 0.63f);
                case MonType.Ground: return new Color(0.88f, 0.75f, 0.41f);
                case MonType.Flying: return new Color(0.66f, 0.56f, 0.94f);
                case MonType.Psychic: return new Color(0.97f, 0.35f, 0.53f);
                case MonType.Bug: return new Color(0.66f, 0.72f, 0.13f);
                case MonType.Rock: return new Color(0.72f, 0.63f, 0.22f);
                case MonType.Ghost: return new Color(0.44f, 0.35f, 0.60f);
                case MonType.Dragon: return new Color(0.44f, 0.22f, 0.97f);
                case MonType.Dark: return new Color(0.44f, 0.35f, 0.28f);
                case MonType.Steel: return new Color(0.72f, 0.72f, 0.82f);
                case MonType.Fairy: return new Color(0.93f, 0.60f, 0.67f);
                default: return Color.gray;
            }
        }
    }
}
