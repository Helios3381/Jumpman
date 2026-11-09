using System.Collections.Generic;
using UnityEngine;

namespace WeddingRun
{
    /// <summary>What happens when the couple touches a thing.</summary>
    public enum ItemKind { Good, Bad, Obstacle, Shield }

    /// <summary>Every 3D model that can appear on the course.</summary>
    public enum ModelId
    {
        // + score
        Heart, Ring, RedPacket, PineappleBun, GongCandy, BubbleTea, EbiFry, MisoKatsu, WeddingCookie,
        Rose,
        // power-up
        Shield,
        // - score
        AlarmClock, Briefcase, RainCloud,
        // game over
        Cone, Barrier, GiftStack1, GiftStack2, GiftStack3, LiquorJar, BrickWall, Scooter, Suitcase, ChampagneTower,
        // game over unless you slide under
        OverheadSign, OverheadBanner, OverheadFirecracker, OverheadNoren, OverheadGarland,
        // special obstacles
        SumoPig                                    // Nagoya miso pig: charges at you — jump!
    }

    public class ItemDef
    {
        public ModelId id;
        public ItemKind kind;
        public string nameZh, nameEn, nameJa;     // shown in legend / game over
        public string popup, popupEn, popupJa;    // floating text when touched
        public string Name => Loc.T(nameZh, nameEn, nameJa);
        public string Popup => Loc.T(popup, popupEn ?? nameEn, popupJa ?? nameJa);
        public int points;         // +/- score
        public float radius;       // pickups: sphere radius
        public Vector2 size;       // obstacles: width / height
        public bool tall;          // needs a high jump
        public bool overhead;      // hangs above the road: slide under it
        public float charge;       // extra speed: runs towards the player
        public const float OverheadClearance = 1.15f; // bottom edge of overhead obstacles

        public static readonly Dictionary<ModelId, ItemDef> All = new Dictionary<ModelId, ItemDef>();

        static void Add(ItemDef d) { All[d.id] = d; }

        static ItemDef()
        {
            // ---------- good ----------
            Add(new ItemDef { id = ModelId.Heart, nameEn = "Heart", nameJa = "ハート", popupEn = "Love", popupJa = "ハート", kind = ItemKind.Good, nameZh = "愛心", popup = "愛心", points = 10, radius = 0.6f });
            Add(new ItemDef { id = ModelId.Ring, nameEn = "Ring", nameJa = "指輪", popupEn = "Ring!", popupJa = "指輪！", kind = ItemKind.Good, nameZh = "婚戒", popup = "婚戒!", points = 50, radius = 0.65f });
            Add(new ItemDef { id = ModelId.RedPacket, nameEn = "Red Packet", nameJa = "ご祝儀袋", popupEn = "Lucky!", popupJa = "ご祝儀！", kind = ItemKind.Good, nameZh = "紅包", popup = "紅包大吉!", points = 88, radius = 0.65f });
            Add(new ItemDef { id = ModelId.PineappleBun, nameEn = "Pineapple Bun", nameJa = "パイナップルパン", popupEn = "Pineapple Bun", popupJa = "パイナップルパン", kind = ItemKind.Good, nameZh = "菠蘿包", popup = "菠蘿包", points = 20, radius = 0.6f });
            Add(new ItemDef { id = ModelId.GongCandy, nameEn = "Peanut Candy", nameJa = "ピーナッツ菓子", popupEn = "Peanut Candy", popupJa = "ピーナッツ菓子", kind = ItemKind.Good, nameZh = "金門貢糖", popup = "貢糖", points = 20, radius = 0.6f });
            Add(new ItemDef { id = ModelId.BubbleTea, nameEn = "Bubble Tea", nameJa = "タピオカミルクティー", popupEn = "Bubble Tea", popupJa = "タピオカ", kind = ItemKind.Good, nameZh = "珍珠奶茶", popup = "珍奶", points = 20, radius = 0.6f });
            Add(new ItemDef { id = ModelId.EbiFry, nameEn = "Ebi Fry", nameJa = "エビフライ", popupEn = "Ebi Fry", popupJa = "エビフライ", kind = ItemKind.Good, nameZh = "名古屋炸蝦", popup = "エビフライ", points = 20, radius = 0.6f });
            Add(new ItemDef { id = ModelId.MisoKatsu, nameEn = "Miso Katsu", nameJa = "味噌カツ", popupEn = "Miso Katsu", popupJa = "味噌カツ", kind = ItemKind.Good, nameZh = "味噌豬排", popup = "味噌豬排", points = 25, radius = 0.6f });
            Add(new ItemDef { id = ModelId.WeddingCookie, nameEn = "Wedding Pastry", nameJa = "喜餅", popupEn = "Wedding Pastry", popupJa = "喜餅", kind = ItemKind.Good, nameZh = "喜餅", popup = "喜餅", points = 30, radius = 0.6f });
            Add(new ItemDef { id = ModelId.Rose, nameEn = "Red Rose", nameJa = "赤いバラ", popupEn = "Romantic!", popupJa = "ロマンチック！", kind = ItemKind.Good, nameZh = "紅玫瑰", popup = "好浪漫!", points = 40, radius = 0.6f });
            Add(new ItemDef { id = ModelId.Shield, nameEn = "Lucky Shield", nameJa = "「囍」シールド", popupEn = "Shield GET!", popupJa = "シールドGET！", kind = ItemKind.Shield, nameZh = "囍字護盾", popup = "護盾 GET!", points = 0, radius = 0.65f });

            // ---------- bad ----------
            Add(new ItemDef { id = ModelId.AlarmClock, nameEn = "Alarm Clock (late)", nameJa = "目覚まし（遅刻）", popupEn = "Late!", popupJa = "遅刻だ！", kind = ItemKind.Bad, nameZh = "鬧鐘 (遲到)", popup = "遲到啦!", points = -20, radius = 0.5f });
            Add(new ItemDef { id = ModelId.Briefcase, nameEn = "Briefcase (overtime)", nameJa = "カバン（残業）", popupEn = "Overtime...", popupJa = "また残業…", kind = ItemKind.Bad, nameZh = "公事包 (加班)", popup = "又要加班…", points = -30, radius = 0.5f });
            Add(new ItemDef { id = ModelId.RainCloud, nameEn = "Rain Cloud", nameJa = "雨雲", popupEn = "Rain...", popupJa = "雨だ…", kind = ItemKind.Bad, nameZh = "烏雲 (壞天氣)", popup = "下雨了…", points = -15, radius = 0.75f });

            // ---------- obstacles (game over) ----------
            Add(new ItemDef { id = ModelId.Cone, nameEn = "Traffic Cone", nameJa = "カラーコーン", kind = ItemKind.Obstacle, nameZh = "三角錐", size = new Vector2(0.9f, 1.1f) });
            Add(new ItemDef { id = ModelId.Barrier, nameEn = "Barrier", nameJa = "バリケード", kind = ItemKind.Obstacle, nameZh = "路邊欄杆", size = new Vector2(1.6f, 1.2f) });
            Add(new ItemDef { id = ModelId.GiftStack1, nameEn = "Gift Box", nameJa = "プレゼント", kind = ItemKind.Obstacle, nameZh = "禮物盒", size = new Vector2(1.0f, 0.95f) });
            Add(new ItemDef { id = ModelId.GiftStack2, nameEn = "Gift Pile", nameJa = "プレゼントの山", kind = ItemKind.Obstacle, nameZh = "禮物堆", size = new Vector2(1.0f, 1.75f) });
            Add(new ItemDef { id = ModelId.GiftStack3, nameEn = "Gift Tower", nameJa = "プレゼントタワー", kind = ItemKind.Obstacle, nameZh = "禮物山", size = new Vector2(1.0f, 2.55f), tall = true });
            Add(new ItemDef { id = ModelId.LiquorJar, nameEn = "Kaoliang Liquor Jar", nameJa = "高粱酒のかめ", kind = ItemKind.Obstacle, nameZh = "金門高粱酒甕", size = new Vector2(1.1f, 1.45f) });
            Add(new ItemDef { id = ModelId.BrickWall, nameEn = "Brick Wall", nameJa = "レンガの塀", kind = ItemKind.Obstacle, nameZh = "紅磚矮牆", size = new Vector2(2.2f, 0.95f) });
            Add(new ItemDef { id = ModelId.Scooter, nameEn = "Scooter", nameJa = "スクーター", kind = ItemKind.Obstacle, nameZh = "台北機車", size = new Vector2(1.9f, 1.25f) });
            Add(new ItemDef { id = ModelId.Suitcase, nameEn = "Suitcase", nameJa = "スーツケース", kind = ItemKind.Obstacle, nameZh = "行李箱", size = new Vector2(0.9f, 1.35f) });
            Add(new ItemDef { id = ModelId.ChampagneTower, nameEn = "Champagne Tower", nameJa = "シャンパンタワー", kind = ItemKind.Obstacle, nameZh = "香檳塔", size = new Vector2(1.3f, 2.45f), tall = true });

            // overhead (slide under)
            Add(new ItemDef { id = ModelId.OverheadSign, nameEn = "Neon Sign", nameJa = "ネオン看板", kind = ItemKind.Obstacle, nameZh = "霓虹招牌", size = new Vector2(1.9f, 0), overhead = true });
            Add(new ItemDef { id = ModelId.OverheadBanner, nameEn = "Red Banner", nameJa = "赤い横断幕", kind = ItemKind.Obstacle, nameZh = "紅布條", size = new Vector2(2.0f, 0), overhead = true });
            Add(new ItemDef { id = ModelId.OverheadFirecracker, nameEn = "Firecrackers", nameJa = "爆竹", kind = ItemKind.Obstacle, nameZh = "鞭炮串", size = new Vector2(1.8f, 0), overhead = true });
            Add(new ItemDef { id = ModelId.OverheadNoren, nameEn = "Noren Curtain", nameJa = "のれん", kind = ItemKind.Obstacle, nameZh = "暖簾", size = new Vector2(2.0f, 0), overhead = true });
            Add(new ItemDef { id = ModelId.SumoPig, nameEn = "Miso Pig", nameJa = "味噌ブタ", kind = ItemKind.Obstacle, nameZh = "味噌豬", size = new Vector2(1.3f, 1.5f), charge = 7f });
            Add(new ItemDef { id = ModelId.OverheadGarland, nameEn = "Wedding Garland", nameJa = "「囍」ガーランド", kind = ItemKind.Obstacle, nameZh = "囍字彩帶", size = new Vector2(2.1f, 0), overhead = true });
        }

        public static ItemDef Get(ModelId id) => All[id];
    }

    /// <summary>
    /// One chapter of the couple's story: 香港 → 台北 → 金門 → 名古屋 → 名古屋港 → 婚禮會場, then the journey loops (endless).
    /// Visual theme only — difficulty depends on total distance, see <see cref="Stages.DifficultyAt"/>.
    /// </summary>
    public class StageDef
    {
        public int index;          // order in the lap
        public int theme;          // which skyline / street props to build (0 HK, 1 Kinmen, 2 Taipei, 3 Nagoya, 4 Wedding, 5 Port of Nagoya)
        public string shortName;   // progress bar label
        public string nameZh, nameEn, nameJa;
        public string shortEn, shortJa;
        public string Name => Loc.T(nameZh, nameEn, nameJa);
        public string ShortName => Loc.T(shortName, shortEn, shortJa);
        public float length;       // metres
        public bool brideJoins;    // first lap: the bride appears from this stage on
        public ModelId[] foods;    // local bonus items
        public ModelId RandomFood => foods[Random.Range(0, foods.Length)];
        public ModelId[] obstacles;
        public ModelId overhead;   // slide-under obstacle for this city
        public Color skyTop, skyBottom, fog, farGround, midGround, road, roadLine, curb, light, ambient, petalA, petalB;
        public float lightIntensity = 1.1f;
        public float glow = 0.45f;  // how much the sun/moon light tints the horizon
        public float trueSky;       // 1 = sky gradient shows the authored colours (linear colour-space correction)
        public float startDistance; // within a lap (filled in automatically)
    }

    /// <summary>All difficulty knobs for a given distance.</summary>
    public struct Difficulty
    {
        public float speed;
        public float gapExtraMin, gapExtraMax;  // breathing room between patterns (metres)
        public float wObstacle, wGood, wBad;    // pattern weights
        public float tallChance;                // obstacle is a tall one (needs hold / double jump)
        public float doubleChance;              // two obstacles back-to-back
        public float slideChance;               // obstacle is an overhead one (slide under)
        public float mixChance;                 // jump obstacle immediately followed by slide obstacle (or reverse)
        public float rewardArcChance;           // heart arc above an obstacle
        public bool movingBad;                  // rain clouds bob up and down
    }

    public static class Stages
    {
        public static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        public static readonly StageDef[] List =
        {
            new StageDef {
                index = 0, theme = 0, shortName = "香港", nameZh = "香港", nameEn = "HONG KONG", nameJa = "香港", shortEn = "Hong Kong", shortJa = "香港", length = 600,
                foods = new[] { ModelId.PineappleBun }, overhead = ModelId.OverheadSign,
                obstacles = new[] { ModelId.Cone, ModelId.Barrier, ModelId.GiftStack1, ModelId.GiftStack2, ModelId.GiftStack3 },
                skyTop = C("#241C5C"), skyBottom = C("#F08AA6"), fog = C("#8F6FA8"), farGround = C("#2B3E6E"), midGround = C("#5B5373"),
                road = C("#4A4658"), roadLine = C("#F4D35E"), curb = C("#C9C2D6"),
                light = C("#FFE3F0"), ambient = C("#8A7FB0"), lightIntensity = 1.0f,
                petalA = C("#FFD86B"), petalB = C("#FF8FB1"),
            },
            new StageDef {
                index = 1, theme = 2, shortName = "台北", nameZh = "台北", nameEn = "TAIPEI", nameJa = "台北", shortEn = "Taipei", shortJa = "台北", length = 700, brideJoins = true,
                foods = new[] { ModelId.BubbleTea }, overhead = ModelId.OverheadBanner,
                obstacles = new[] { ModelId.Scooter, ModelId.Cone, ModelId.GiftStack1, ModelId.GiftStack2, ModelId.GiftStack3, ModelId.Barrier },
                // night market at night
                skyTop = C("#0E1033"), skyBottom = C("#4A3778"), fog = C("#3A3060"), farGround = C("#272A3E"), midGround = C("#4B4254"),
                road = C("#403C48"), roadLine = C("#F6E7A6"), curb = C("#7A7084"),
                light = C("#FFC98F"), ambient = C("#6A5A88"), lightIntensity = 0.8f,
                petalA = C("#FFB347"), petalB = C("#FF6B6B"),
            },
            new StageDef {
                index = 2, theme = 1, shortName = "金門", nameZh = "金門", nameEn = "KINMEN", nameJa = "金門", shortEn = "Kinmen", shortJa = "金門", length = 800,
                foods = new[] { ModelId.GongCandy }, overhead = ModelId.OverheadFirecracker,
                obstacles = new[] { ModelId.LiquorJar, ModelId.BrickWall, ModelId.Cone, ModelId.GiftStack2, ModelId.GiftStack3 },
                skyTop = C("#3E8EDB"), skyBottom = C("#D8F1FF"), fog = C("#BFE0F2"), farGround = C("#2E9BB0"), midGround = C("#E3CDA2"),
                road = C("#C98E6B"), roadLine = C("#FFF3DE"), curb = C("#9C5B45"),
                light = C("#FFF4E0"), ambient = C("#A9C7E0"), lightIntensity = 1.2f,
                petalA = C("#FFFFFF"), petalB = C("#FFE08A"),
            },
            new StageDef {
                index = 3, theme = 3, shortName = "名古屋", nameZh = "名古屋", nameEn = "NAGOYA", nameJa = "名古屋", shortEn = "Nagoya", shortJa = "名古屋", length = 600,
                foods = new[] { ModelId.EbiFry, ModelId.MisoKatsu }, overhead = ModelId.OverheadNoren,
                obstacles = new[] { ModelId.Suitcase, ModelId.Cone, ModelId.Barrier, ModelId.GiftStack1, ModelId.GiftStack2, ModelId.GiftStack3 },
                skyTop = C("#7EC0EE"), skyBottom = C("#FFE3EC"), fog = C("#F3D7E3"), farGround = C("#7DB88A"), midGround = C("#B9DB9F"),
                road = C("#8D8A96"), roadLine = C("#FFFFFF"), curb = C("#E6E0E8"),
                light = C("#FFF0F5"), ambient = C("#C8B8D8"), lightIntensity = 1.15f,
                petalA = C("#FFC0D3"), petalB = C("#FFE4EE"),
            },
            new StageDef {
                // Garden Pier at night, where he proposed. First lap only: later laps loop the 4 cities before it.
                index = 4, theme = 5, shortName = "名古屋港", nameZh = "名古屋港", nameEn = "Port of Nagoya", nameJa = "名古屋港", shortEn = "Port of Nagoya", shortJa = "名古屋港", length = 450,
                foods = new[] { ModelId.Rose }, overhead = ModelId.OverheadNoren,
                obstacles = new[] { ModelId.Suitcase, ModelId.Cone, ModelId.Barrier, ModelId.GiftStack1, ModelId.GiftStack2, ModelId.GiftStack3 },
                skyTop = C("#0B0E2E"), skyBottom = C("#33295E"), fog = C("#262148"), farGround = C("#1A3360"), midGround = C("#4A4558"),
                road = C("#3A3644"), roadLine = C("#F6E7A6"), curb = C("#6A6476"),
                light = C("#FFD8A8"), ambient = C("#564C7E"), lightIntensity = 0.75f, glow = 0.22f, trueSky = 1f,
                petalA = C("#FFE6A8"), petalB = C("#FFB0C8"),
            },
            new StageDef {
                index = 5, theme = 4, shortName = "婚禮", nameZh = "婚禮會場", nameEn = "WEDDING", nameJa = "結婚式場", shortEn = "Wedding", shortJa = "結婚式", length = 1000,
                foods = new[] { ModelId.WeddingCookie }, overhead = ModelId.OverheadGarland,
                obstacles = new[] { ModelId.GiftStack1, ModelId.GiftStack2, ModelId.ChampagneTower, ModelId.Suitcase, ModelId.GiftStack3 },
                skyTop = C("#2A1E55"), skyBottom = C("#FF9DB0"), fog = C("#C98AB0"), farGround = C("#5E9A6E"), midGround = C("#8CC08A"),
                road = C("#B3243B"), roadLine = C("#F2C66D"), curb = C("#FFF3E6"),
                light = C("#FFE6D0"), ambient = C("#B596C0"), lightIntensity = 1.05f,
                petalA = C("#FF6F91"), petalB = C("#FFD36E"),
            },
        };

        public static readonly float FirstLapLength;
        /// <summary>Later laps loop the 4 cities only (the wedding already happened).</summary>
        public static readonly float LoopLength;
        public const int LoopStages = 4;
        /// <summary>Port of Nagoya: first lap only, and kept off the progress bar until the couple arrives.</summary>
        public const int PortStage = 4;

        static Stages()
        {
            float d = 0;
            foreach (var s in List) { s.startDistance = d; d += s.length; }
            FirstLapLength = d;
            LoopLength = List[LoopStages].startDistance;
        }

        public static StageDef ByTheme(int theme)
        {
            foreach (var s in List) if (s.theme == theme) return s;
            return List[0];
        }

        public static int LapAt(float distance)
        {
            distance = Mathf.Max(0, distance);
            return distance < FirstLapLength ? 0 : 1 + Mathf.FloorToInt((distance - FirstLapLength) / LoopLength);
        }

        /// <summary>Distance inside the current lap.</summary>
        static float LocalDistance(float distance)
        {
            distance = Mathf.Max(0, distance);
            return distance < FirstLapLength ? distance : Mathf.Repeat(distance - FirstLapLength, LoopLength);
        }

        public static int IndexAt(float distance)
        {
            float d = LocalDistance(distance);
            int count = LapAt(distance) == 0 ? List.Length : LoopStages;
            int idx = 0;
            for (int i = 0; i < count; i++) if (d >= List[i].startDistance) idx = i;
            return idx;
        }

        /// <summary>0..1 progress through the current stage.</summary>
        public static float StageProgress(float distance)
        {
            int i = IndexAt(distance);
            return Mathf.Clamp01((LocalDistance(distance) - List[i].startDistance) / List[i].length);
        }

        /// <summary>Visual theme at this distance.</summary>
        public static int ThemeAt(float distance) => List[IndexAt(distance)].theme;

        /// <summary>Absolute distance where stage <paramref name="index"/> of lap <paramref name="lap"/> starts.</summary>
        public static float StartOf(int lap, int index) =>
            (lap == 0 ? 0 : FirstLapLength + (lap - 1) * LoopLength) + List[index].startDistance;

        // ---------------- difficulty: grows with distance, forever ----------------
        public const float StartSpeed = 11f;
        public const float SpeedPerMetre = 0.004f;    // +4 m/s every 1000 m
        public const float MaxSpeed = 30f;
        public const float RampDistance = 4000f;     // distance over which density / mix reach their maximum

        public static float SpeedAt(float distance) => Mathf.Min(MaxSpeed, StartSpeed + Mathf.Max(0, distance) * SpeedPerMetre);

        // ---------------- score: the first lap is the story, so its metres are worth less ----------------
        public const float FirstLapPointsPerMetre = 0.7f;

        public static int DistancePoints(float distance)
        {
            distance = Mathf.Max(0, distance);
            float story = Mathf.Min(distance, FirstLapLength);
            return Mathf.FloorToInt(story * FirstLapPointsPerMetre + (distance - story));
        }

        public static Difficulty DifficultyAt(float distance)
        {
            float t = Mathf.Clamp01(distance / RampDistance);
            return new Difficulty
            {
                speed = SpeedAt(distance),
                gapExtraMin = Mathf.Lerp(3.5f, 0.5f, t),
                gapExtraMax = Mathf.Lerp(9f, 2.5f, t),
                wObstacle = Mathf.Lerp(0.50f, 0.66f, t),
                wGood = Mathf.Lerp(0.40f, 0.20f, t),
                wBad = Mathf.Lerp(0.10f, 0.14f, t),
                tallChance = distance < 150 ? 0 : Mathf.Lerp(0.08f, 0.35f, t),
                doubleChance = distance < 300 ? 0 : Mathf.Lerp(0.08f, 0.30f, t),
                slideChance = distance < 120 ? 0 : Mathf.Lerp(0.22f, 0.32f, t),
                mixChance = distance < 600 ? 0 : Mathf.Lerp(0.10f, 0.35f, t),
                rewardArcChance = Mathf.Lerp(0.45f, 0.25f, t),
                movingBad = distance > 1200,
            };
        }

        /// <summary>0..1 position along the current lap for the HUD progress bar.</summary>
        public static float JourneyProgress(float distance)
        {
            float d = LocalDistance(distance);
            int i = IndexAt(distance);
            float local = Mathf.Clamp01((d - List[i].startDistance) / List[i].length);
            if (LapAt(distance) > 0) return Mathf.Min(1f, (i + local) / LoopStages);        // 4 cities, then "next lap"
            if (i >= PortStage) return Mathf.Min(1f, (i + local) / (List.Length - 1));       // the port has been revealed
            // before the port the bar shows 5 nodes: Nagoya stops short of the venue so the marker never reaches it early
            return (i + local * (i == PortStage - 1 ? 0.8f : 1f)) / PortStage;
        }
    }

    public static class Palette
    {
        public static readonly Color Rose = Stages.C("#E4577E");
        public static readonly Color RoseDark = Stages.C("#B8365C");
        public static readonly Color Wine = Stages.C("#6E2846");
        public static readonly Color Gold = Stages.C("#F0B850");
        public static readonly Color GoldDark = Stages.C("#C98A1E");
        public static readonly Color Cream = Stages.C("#FFF7F0");
        public static readonly Color Blush = Stages.C("#FFE3EA");
        public static readonly Color Ink = Stages.C("#3A2633");
        public static readonly Color Muted = Stages.C("#8C7482");
        public static readonly Color Good = Stages.C("#2FB37E");
        public static readonly Color Bad = Stages.C("#E5484D");
        public static readonly Color Shield = Stages.C("#F0B850");
    }
}
