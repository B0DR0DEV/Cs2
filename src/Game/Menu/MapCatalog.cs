using Godot;

namespace StrikeZone.Game
{
    public sealed class MapInfo
    {
        public string Id;
        public string Name;
        public string Subtitle;
        public Color Top;
        public Color Bottom;
        public bool Available;
    }

    public sealed class ModeInfo
    {
        public string Title;
        public string Description;
        public bool Online;
        public bool Ranked;
        public int PartySize;
        public string[] Maps;
    }

    /// <summary>Modes and maps shown in the Play tab. Map availability follows the phase plan.</summary>
    public static class MapCatalog
    {
        public static readonly MapInfo[] Maps =
        {
            new MapInfo { Id = "range", Name = "ПОЛИГОН", Subtitle = "Тренировочная площадка", Top = new Color(0.36f, 0.5f, 0.68f), Bottom = new Color(0.55f, 0.55f, 0.52f), Available = true },
            new MapInfo { Id = "citadel", Name = "ЦИТАДЕЛЬ", Subtitle = "Старый город · 2 точки", Top = new Color(0.78f, 0.6f, 0.38f), Bottom = new Color(0.35f, 0.26f, 0.18f) },
            new MapInfo { Id = "harbor", Name = "ПОРТ", Subtitle = "Доки и склады · 2 точки", Top = new Color(0.32f, 0.46f, 0.56f), Bottom = new Color(0.14f, 0.18f, 0.22f) },
            new MapInfo { Id = "canyon", Name = "КАНЬОН", Subtitle = "Горная база · 2 точки", Top = new Color(0.84f, 0.5f, 0.3f), Bottom = new Color(0.32f, 0.2f, 0.16f) },
            new MapInfo { Id = "depot", Name = "СКЛАД", Subtitle = "Для 2 × 2 · 1 точка", Top = new Color(0.5f, 0.52f, 0.46f), Bottom = new Color(0.2f, 0.2f, 0.18f) }
        };

        public static readonly ModeInfo[] Modes =
        {
            new ModeInfo { Title = "СОРЕВНОВАТЕЛЬНЫЙ", Description = "5 × 5 · до 13 побед · рейтинг Glicko-2", Online = true, Ranked = true, PartySize = 5, Maps = new[] { "citadel", "harbor", "canyon" } },
            new ModeInfo { Title = "ОБЫЧНЫЙ", Description = "5 × 5 · без рейтинга · вход в идущий матч", Online = true, PartySize = 5, Maps = new[] { "citadel", "harbor", "canyon" } },
            new ModeInfo { Title = "DEATHMATCH", Description = "Мгновенное возрождение · оружие бесплатно · 10 минут", Online = true, PartySize = 5, Maps = new[] { "citadel", "harbor", "canyon" } },
            new ModeInfo { Title = "НАПАРНИКИ", Description = "2 × 2 · маленькая карта · одна точка закладки", Online = true, Ranked = true, PartySize = 2, Maps = new[] { "depot" } },
            new ModeInfo { Title = "С БОТАМИ", Description = "Офлайн · мишени-боты (тактический ИИ появится в фазе 5)", Online = false, PartySize = 1, Maps = new[] { "range" } },
            new ModeInfo { Title = "СВОБОДНАЯ", Description = "Офлайн · бесконечный боезапас · отработка отдачи и движения", Online = false, PartySize = 1, Maps = new[] { "range" } }
        };

        public static MapInfo Get(string id)
        {
            foreach (var m in Maps)
                if (m.Id == id) return m;
            return Maps[0];
        }
    }
}
