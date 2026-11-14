using System.Collections.Generic;
using System.Linq;

namespace HowToFishMon
{
    public enum MoveCategory { Physical, Special }

    public class Move
    {
        public string Name;
        public MonType Type;
        public MoveCategory Category;
        public int Power;
        public int Accuracy;   // 0-100
        public int Priority;   // higher goes first
        public int LearnLevel;
        public float DrainPercent; // heals attacker by % of damage dealt
        public bool Spread;        // hits every foe nearby, like doubles in pokemon (75% dmg each if it hits more than one)

        public Move(string name, MonType type, MoveCategory cat, int power, int acc, int learn, int priority = 0, float drain = 0f, bool spread = false)
        {
            Name = name; Type = type; Category = cat; Power = power; Accuracy = acc; LearnLevel = learn; Priority = priority; DrainPercent = drain; Spread = spread;
        }
    }

    public static class Moves
    {
        private const MoveCategory P = MoveCategory.Physical;
        private const MoveCategory S = MoveCategory.Special;

        public static readonly List<Move> All = new List<Move>
        {
            new Move("Tackle",        MonType.Normal,   P, 40, 100, 1),
            new Move("Flail",         MonType.Normal,   P, 60, 100, 12),
            new Move("Body Slam",     MonType.Normal,   P, 85, 100, 30),
            new Move("Swift",         MonType.Normal,   S, 60, 100, 20, spread: true),
            new Move("Hyper Voice",   MonType.Normal,   S, 90, 100, 45, spread: true),

            new Move("Water Gun",     MonType.Water,    S, 40, 100, 1),
            new Move("Aqua Jet",      MonType.Water,    P, 40, 100, 8, priority: 1),
            new Move("Bubble Beam",   MonType.Water,    S, 65, 100, 16),
            new Move("Waterfall",     MonType.Water,    P, 80, 100, 28),
            new Move("Surf",          MonType.Water,    S, 90, 100, 38, spread: true),
            new Move("Hydro Pump",    MonType.Water,    S, 110, 80, 52),

            new Move("Ember",         MonType.Fire,     S, 40, 100, 1),
            new Move("Flame Wheel",   MonType.Fire,     P, 60, 100, 18),
            new Move("Lava Plume",    MonType.Fire,     S, 80, 100, 30, spread: true),
            new Move("Flamethrower",  MonType.Fire,     S, 90, 100, 42),

            new Move("Thunder Shock", MonType.Electric, S, 40, 100, 1),
            new Move("Spark",         MonType.Electric, P, 65, 100, 16),
            new Move("Thunderbolt",   MonType.Electric, S, 90, 100, 36),
            new Move("Wild Charge",   MonType.Electric, P, 90, 100, 48),
            new Move("Discharge",     MonType.Electric, S, 80, 100, 30, spread: true),

            new Move("Absorb",        MonType.Grass,    S, 40, 100, 1, drain: 0.5f),
            new Move("Razor Leaf",    MonType.Grass,    P, 55, 95, 15, spread: true),
            new Move("Giga Drain",    MonType.Grass,    S, 75, 100, 32, drain: 0.5f),
            new Move("Energy Ball",   MonType.Grass,    S, 90, 100, 44),

            new Move("Powder Snow",   MonType.Ice,      S, 40, 100, 1, spread: true),
            new Move("Ice Fang",      MonType.Ice,      P, 65, 95, 18),
            new Move("Ice Beam",      MonType.Ice,      S, 90, 100, 40),
            new Move("Icy Wind",      MonType.Ice,      S, 55, 95, 12, spread: true),

            new Move("Rock Smash",    MonType.Fighting, P, 40, 100, 1),
            new Move("Brick Break",   MonType.Fighting, P, 75, 100, 24),
            new Move("Close Combat",  MonType.Fighting, P, 120, 100, 50),

            new Move("Poison Sting",  MonType.Poison,   P, 30, 100, 1),
            new Move("Sludge",        MonType.Poison,   S, 65, 100, 18),
            new Move("Poison Jab",    MonType.Poison,   P, 80, 100, 30),
            new Move("Sludge Bomb",   MonType.Poison,   S, 90, 100, 42),
            new Move("Sludge Wave",   MonType.Poison,   S, 95, 100, 48, spread: true),

            new Move("Mud Slap",      MonType.Ground,   S, 20, 100, 1),
            new Move("Bulldoze",      MonType.Ground,   P, 60, 100, 20, spread: true),
            new Move("Earthquake",    MonType.Ground,   P, 100, 100, 46, spread: true),

            new Move("Peck",          MonType.Flying,   P, 35, 100, 1),
            new Move("Wing Attack",   MonType.Flying,   P, 60, 100, 16),
            new Move("Air Slash",     MonType.Flying,   S, 75, 95, 32),
            new Move("Brave Bird",    MonType.Flying,   P, 120, 100, 50),
            new Move("Air Cutter",    MonType.Flying,   S, 60, 95, 22, spread: true),

            new Move("Confusion",     MonType.Psychic,  S, 50, 100, 1),
            new Move("Psybeam",       MonType.Psychic,  S, 65, 100, 20),
            new Move("Psychic",       MonType.Psychic,  S, 90, 100, 40),

            new Move("Bug Bite",      MonType.Bug,      P, 60, 100, 1),
            new Move("Signal Beam",   MonType.Bug,      S, 75, 100, 24),
            new Move("X-Scissor",     MonType.Bug,      P, 80, 100, 36),
            new Move("Struggle Bug",  MonType.Bug,      S, 50, 100, 12, spread: true),

            new Move("Rock Throw",    MonType.Rock,     P, 50, 90, 1),
            new Move("Rock Slide",    MonType.Rock,     P, 75, 90, 24, spread: true),
            new Move("Stone Edge",    MonType.Rock,     P, 100, 80, 44),

            new Move("Astonish",      MonType.Ghost,    P, 30, 100, 1),
            new Move("Shadow Claw",   MonType.Ghost,    P, 70, 100, 24),
            new Move("Shadow Ball",   MonType.Ghost,    S, 80, 100, 38),

            new Move("Twister",       MonType.Dragon,   S, 40, 100, 1, spread: true),
            new Move("Dragon Breath", MonType.Dragon,   S, 60, 100, 18),
            new Move("Dragon Pulse",  MonType.Dragon,   S, 85, 100, 38),
            new Move("Outrage",       MonType.Dragon,   P, 120, 100, 54),

            new Move("Bite",          MonType.Dark,     P, 60, 100, 1),
            new Move("Crunch",        MonType.Dark,     P, 80, 100, 26),
            new Move("Dark Pulse",    MonType.Dark,     S, 80, 100, 36),
            new Move("Snarl",         MonType.Dark,     S, 55, 95, 14, spread: true),

            new Move("Metal Claw",    MonType.Steel,    P, 50, 95, 1),
            new Move("Iron Head",     MonType.Steel,    P, 80, 100, 28),
            new Move("Flash Cannon",  MonType.Steel,    S, 80, 100, 38),

            new Move("Fairy Wind",    MonType.Fairy,    S, 40, 100, 1),
            new Move("Draining Kiss", MonType.Fairy,    S, 50, 100, 20, drain: 0.75f),
            new Move("Moonblast",     MonType.Fairy,    S, 95, 100, 44),
            new Move("Dazzling Gleam",MonType.Fairy,    S, 80, 100, 32, spread: true),
        };

        public static Move Find(string name) => All.FirstOrDefault(m => m.Name == name);

        // every move a creature of these types can ever learn (own types + Normal), in learn-level order
        public static List<Move> PoolFor(MonType t1, MonType t2) =>
            All.Where(m => m.Type == t1 || m.Type == t2 || m.Type == MonType.Normal)
               .OrderBy(m => m.LearnLevel).ThenBy(m => m.Name).ToList();

        // moves it can use right now at this level
        public static List<Move> LearnableFor(MonType t1, MonType t2, int level) =>
            PoolFor(t1, t2).Where(m => m.LearnLevel <= level).ToList();

        // short tags for a move's special traits
        public static string Traits(Move m)
        {
            var t = new List<string>();
            if (m.Spread) t.Add("hits all nearby foes");
            if (m.Priority > 0) t.Add("fast");
            if (m.DrainPercent > 0f) t.Add($"heals {m.DrainPercent:P0} of damage");
            if (m.Accuracy < 100) t.Add("can miss");
            return string.Join(", ", t);
        }

        // the 4 moves a creature of these types knows at this level (strongest moves, at least one of each own type)
        public static List<Move> MovesetFor(MonType t1, MonType t2, int level, bool physicalLeaning)
        {
            var learnable = All.Where(m => m.LearnLevel <= level &&
                                           (m.Type == t1 || m.Type == t2 || m.Type == MonType.Normal)).ToList();

            float Score(Move m)
            {
                float s = m.Power * (m.Accuracy / 100f);
                if (m.Type == t1 || m.Type == t2) s *= 1.5f; // STAB
                if ((m.Category == MoveCategory.Physical) == physicalLeaning) s *= 1.2f;
                if (m.Priority > 0) s += 15f;
                if (m.Spread) s *= 1.1f;
                return s;
            }

            var result = new List<Move>();
            foreach (var t in new[] { t1, t2 })
            {
                if (t == MonType.None) continue;
                var best = learnable.Where(m => m.Type == t && !result.Contains(m)).OrderByDescending(Score).FirstOrDefault();
                if (best != null) result.Add(best);
            }
            foreach (var m in learnable.OrderByDescending(Score))
            {
                if (result.Count >= 4) break;
                if (!result.Contains(m)) result.Add(m);
            }
            return result;
        }
    }
}
