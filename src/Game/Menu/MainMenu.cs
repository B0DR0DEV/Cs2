using System;
using System.Collections.Generic;
using Godot;
using StrikeZone.Core;

namespace StrikeZone.Game
{
    /// <summary>
    /// Main menu. Layout follows the familiar tactical-shooter structure: a top bar with
    /// system buttons and logo on the left, section tabs in the middle and the profile,
    /// currencies and friends on the right; your character in a 3D scene behind; a lobby
    /// strip with party slots at the bottom; the Play panel with mode tabs, a map grid with
    /// checkboxes and a big primary action in the bottom-right. Visual design is original.
    /// </summary>
    public partial class MainMenu : Node
    {
        private enum Page
        {
            Home,
            Play,
            Inventory,
            Shop,
            Pass,
            Stats,
            Settings
        }

        private static readonly string[] TabTitles = { "ИГРАТЬ", "ИНВЕНТАРЬ", "МАГАЗИН", "ПРОПУСК", "СТАТИСТИКА" };
        private static readonly Page[] TabPages = { Page.Play, Page.Inventory, Page.Shop, Page.Pass, Page.Stats };

        private MenuBackground _background;
        private Control _root;
        private readonly Dictionary<Page, Control> _pages = new Dictionary<Page, Control>();
        private readonly List<Button> _tabButtons = new List<Button>();
        private Page _page = Page.Home;
        private Control _friendsPanel;
        private Label _toast;
        private float _toastTimer;
        private Label _nickLabel;
        private Label _avatarLabel;
        private Control _partyBar;

        // Play page state.
        private int _mode = 5;
        private readonly Dictionary<int, HashSet<string>> _selectedMaps = new Dictionary<int, HashSet<string>>();
        private readonly List<Button> _modeButtons = new List<Button>();
        private Label _modeDescription;
        private Label _modeExtra;
        private HFlowContainer _mapGrid;
        private Button _goButton;
        private Label _goNote;
        private VBoxContainer _trainingOptions;
        private bool _optInfiniteAmmo = true;

        public override void _Ready()
        {
            for (int i = 0; i < MapCatalog.Modes.Length; i++)
                _selectedMaps[i] = new HashSet<string>(MapCatalog.Modes[i].Maps);

            _background = new MenuBackground();
            AddChild(_background);

            var layer = new CanvasLayer { Layer = 5 };
            AddChild(layer);
            _root = Ui.Fill(new Control { MouseFilter = Control.MouseFilterEnum.Ignore });
            layer.AddChild(_root);

            // Vignette on the left so text stays readable over the 3D scene.
            var vignette = Ui.Place(new TextureRect
            {
                Texture = ProceduralTextures.Gradient("menu_vignette", new Color(0f, 0f, 0f, 0.0f), new Color(0f, 0f, 0f, 0.75f)),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore
            }, 0f, 0.55f, 1f, 1f);
            _root.AddChild(vignette);

            BuildPages();
            BuildTopBar();
            BuildPartyBar();
            BuildFriendsPanel();

            _toast = Ui.Place(Ui.Label("", 24, Ui.Text, Ui.Bold, HorizontalAlignment.Center), 0.5f, 1f, 0.5f, 1f, -700f, -210f, 700f, -170f);
            _root.AddChild(_toast);

            ShowPage(Page.Home);
            GameContext.App?.PlayUi("ui_accept");
        }

        public override void _Process(double delta)
        {
            if (_toastTimer > 0f)
            {
                _toastTimer -= (float)delta;
                _toast.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_toastTimer / 0.4f, 0f, 1f));
            }
        }

        private void Toast(string text)
        {
            _toast.Text = text;
            _toastTimer = 3f;
            _toast.Modulate = Colors.White;
        }

        // ================================================================== top bar

        private void BuildTopBar()
        {
            var bar = Ui.Place(Ui.Rect(Ui.Bar), 0f, 0f, 1f, 0f, 0f, 0f, 0f, 84f);
            bar.MouseFilter = Control.MouseFilterEnum.Stop;
            _root.AddChild(bar);
            var line = Ui.Place(Ui.Rect(new Color(1f, 1f, 1f, 0.06f)), 0f, 1f, 1f, 1f, 0f, -2f, 0f, 0f);
            bar.AddChild(line);

            // Left: power, settings, logo.
            var left = Ui.Place(Ui.HBox(6), 0f, 0f, 0f, 1f, 16f, 10f, 520f, -10f);
            bar.AddChild(left);
            left.AddChild(IconButton(IconKind.Power, () => GetTree().Quit()));
            left.AddChild(IconButton(IconKind.Gear, () => ShowPage(Page.Settings)));
            left.AddChild(Ui.Spacer(14f));
            var logo = Ui.VBox(0);
            logo.Alignment = BoxContainer.AlignmentMode.Center;
            var logoButton = new Button { Flat = true, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(250f, 64f) };
            logoButton.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            logoButton.Pressed += () => ShowPage(Page.Home);
            var logoText = Ui.Place(Ui.Label("STRIKE ZONE", 34, Ui.Text, Ui.Wide), 0f, 0f, 1f, 1f, 0f, 0f, 0f, -10f);
            var logoBar = Ui.Place(Ui.Rect(Ui.Accent), 0f, 1f, 0f, 1f, 2f, -12f, 92f, -8f);
            logoButton.AddChild(logoText);
            logoButton.AddChild(logoBar);
            left.AddChild(logoButton);

            // Centre: section tabs.
            var tabs = Ui.Place(Ui.HBox(4), 0.5f, 0f, 0.5f, 1f, -420f, 0f, 420f, 0f);
            tabs.Alignment = BoxContainer.AlignmentMode.Center;
            bar.AddChild(tabs);
            for (int i = 0; i < TabTitles.Length; i++)
            {
                Page page = TabPages[i];
                bool play = page == Page.Play;
                var b = Ui.Button(TabTitles[i], play ? 24 : 19, new Color(0f, 0f, 0f, 0f), play ? Ui.Accent : Ui.Text, () => ShowPage(page), Ui.Wide, 0);
                b.CustomMinimumSize = new Vector2(play ? 210f : 150f, 84f);
                if (play)
                {
                    var icon = MenuIcon.Create(IconKind.Play, 22f, Ui.Accent);
                    Ui.Place(icon, 0f, 0.5f, 0f, 0.5f, 22f, -11f, 44f, 11f);
                    b.AddChild(icon);
                }
                tabs.AddChild(b);
                _tabButtons.Add(b);
            }

            // Right: currencies, profile, friends.
            var right = Ui.Place(Ui.HBox(10), 1f, 0f, 1f, 1f, -560f, 10f, -16f, -10f);
            right.Alignment = BoxContainer.AlignmentMode.End;
            bar.AddChild(right);
            right.AddChild(Currency(IconKind.Gem, new Color(0.45f, 0.75f, 1f), "0", "Кредиты", true));
            right.AddChild(Currency(IconKind.Coin, new Color(0.95f, 0.78f, 0.3f), "0", "Монеты", false));

            var profile = new Button { Flat = true, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(220f, 64f) };
            profile.AddThemeStyleboxOverride("normal", Ui.Box(new Color(1f, 1f, 1f, 0.05f), 4));
            profile.AddThemeStyleboxOverride("hover", Ui.Box(new Color(1f, 1f, 1f, 0.1f), 4));
            profile.AddThemeStyleboxOverride("pressed", Ui.Box(new Color(1f, 1f, 1f, 0.14f), 4));
            profile.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            profile.Pressed += OpenNicknameEditor;
            var avatar = Ui.Place(Ui.Rect(new Color(0.85f, 0.45f, 0.2f)), 0f, 0f, 0f, 1f, 6f, 6f, 58f, -6f);
            _avatarLabel = Ui.Fill(Ui.Label(Initials(), 22, Colors.White, Ui.Bold, HorizontalAlignment.Center));
            avatar.AddChild(_avatarLabel);
            profile.AddChild(avatar);
            _nickLabel = Ui.Place(Ui.Label(GameContext.Settings.Nickname, 22, Ui.Text, Ui.Bold), 0f, 0f, 1f, 0.5f, 68f, 4f, -6f, 4f);
            profile.AddChild(_nickLabel);
            profile.AddChild(Ui.Place(Ui.Label("УР. 1 · БЕЗ РАНГА", 15, Ui.Muted, Ui.Wide), 0f, 0.5f, 1f, 1f, 68f, -2f, -6f, -6f));
            right.AddChild(profile);
            right.AddChild(IconButton(IconKind.People, () => _friendsPanel.Visible = !_friendsPanel.Visible));
        }

        private Control Currency(IconKind icon, Color color, string amount, string tooltip, bool canTopUp)
        {
            var box = Ui.HBox(6);
            box.TooltipText = tooltip;
            box.AddChild(MenuIcon.Create(icon, 24f, color));
            box.AddChild(Ui.Label(amount, 24, Ui.Text, Ui.Bold));
            if (canTopUp)
            {
                var plus = IconButton(IconKind.Plus, () => Toast("Покупка Кредитов появится вместе с сервером платежей (фаза 7)"), 40f);
                box.AddChild(plus);
            }
            return box;
        }

        private static Button IconButton(IconKind kind, Action action, float size = 56f)
        {
            var b = Ui.Button("", 1, new Color(1f, 1f, 1f, 0.04f), Ui.Text, action, null, 4);
            b.CustomMinimumSize = new Vector2(size, size);
            b.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            var icon = MenuIcon.Create(kind, size * 0.55f, Ui.Text);
            Ui.Place(icon, 0.5f, 0.5f, 0.5f, 0.5f, -size * 0.275f, -size * 0.275f, size * 0.275f, size * 0.275f);
            b.AddChild(icon);
            return b;
        }

        private static string Initials()
        {
            string n = GameContext.Settings.Nickname ?? "?";
            n = n.Trim();
            return n.Length == 0 ? "?" : n.Substring(0, Math.Min(2, n.Length)).ToUpperInvariant();
        }

        // ================================================================== party & friends

        private void BuildPartyBar()
        {
            _partyBar = Ui.Place(Ui.HBox(10), 0f, 1f, 0f, 1f, 32f, -120f, 700f, -28f);
            _root.AddChild(_partyBar);
            RefreshParty();
        }

        private void RefreshParty()
        {
            foreach (Node child in _partyBar.GetChildren()) child.QueueFree();
            int size = MapCatalog.Modes[_mode].PartySize;
            if (_page != Page.Play) size = 5;
            for (int i = 0; i < 5; i++)
            {
                bool usable = i < Math.Max(size, 1);
                if (i == 0)
                {
                    var me = Ui.PanelBox(new Color(0.85f, 0.45f, 0.2f), 4, 0f);
                    me.CustomMinimumSize = new Vector2(92f, 92f);
                    me.AddChild(Ui.Label(Initials(), 30, Colors.White, Ui.Bold, HorizontalAlignment.Center));
                    _partyBar.AddChild(me);
                    continue;
                }
                var slot = IconButton(IconKind.Plus, () => Toast("Пригласить друга можно будет с запуском онлайна (фаза 6): по нику, ID или коду лобби"), 92f);
                slot.Modulate = usable ? Colors.White : new Color(1f, 1f, 1f, 0.25f);
                slot.Disabled = !usable;
                _partyBar.AddChild(slot);
            }
        }

        private void BuildFriendsPanel()
        {
            _friendsPanel = Ui.Place(Ui.PanelBox(Ui.Panel, 0, 20f), 1f, 0f, 1f, 1f, -400f, 84f, 0f, 0f);
            _friendsPanel.Visible = false;
            _root.AddChild(_friendsPanel);
            var v = Ui.VBox(14);
            _friendsPanel.AddChild(v);
            v.AddChild(Ui.Label("ДРУЗЬЯ", 26, Ui.Text, Ui.Wide));
            v.AddChild(Ui.Label("В сети: 0", 18, Ui.Muted));
            uint hash = 2166136261u;
            foreach (char ch in GameContext.Settings.Nickname) hash = (hash ^ (uint)ch) * 16777619u;
            var code = Ui.Label("Код лобби: SZ-" + (hash & 0xFFFFu).ToString("X4"), 20, Ui.Accent, Ui.Bold);
            v.AddChild(code);
            var info = Ui.Label("Список друзей, статусы «в сети / в матче / в лобби», приглашения и голосовой чат пати работают через сервер — они появятся в фазе 6 вместе с матчмейкингом.", 18, Ui.Muted);
            info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            info.CustomMinimumSize = new Vector2(340f, 0f);
            v.AddChild(info);
            var add = Ui.Button("ДОБАВИТЬ ДРУГА", 20, Ui.PanelLight, Ui.Muted, () => Toast("Добавление друзей — фаза 6"), Ui.Wide);
            add.CustomMinimumSize = new Vector2(0f, 56f);
            v.AddChild(add);
        }

        private void OpenNicknameEditor()
        {
            var overlay = Ui.Fill(new Control { MouseFilter = Control.MouseFilterEnum.Stop });
            overlay.AddChild(Ui.Fill(Ui.Rect(new Color(0f, 0f, 0f, 0.6f))));
            var panel = Ui.Place(Ui.PanelBox(Ui.Panel, 6, 28f), 0.5f, 0.5f, 0.5f, 0.5f, -360f, -150f, 360f, 150f);
            overlay.AddChild(panel);
            var v = Ui.VBox(16);
            panel.AddChild(v);
            v.AddChild(Ui.Label("НИКНЕЙМ", 26, Ui.Text, Ui.Wide));
            var edit = new LineEdit { Text = GameContext.Settings.Nickname, MaxLength = 16, CustomMinimumSize = new Vector2(0f, 60f) };
            edit.AddThemeFontSizeOverride("font_size", 26);
            v.AddChild(edit);
            var row = Ui.HBox(12);
            row.Alignment = BoxContainer.AlignmentMode.End;
            v.AddChild(row);
            row.AddChild(Ui.Button("ОТМЕНА", 22, Ui.PanelLight, Ui.Text, () => overlay.QueueFree(), Ui.Wide));
            row.AddChild(Ui.Button("СОХРАНИТЬ", 22, Ui.Go, Colors.White, () =>
            {
                string nick = edit.Text.Trim();
                if (nick.Length > 0)
                {
                    GameContext.Settings.Nickname = nick;
                    GameContext.Settings.Save();
                    _nickLabel.Text = nick;
                    _avatarLabel.Text = Initials();
                    RefreshParty();
                }
                overlay.QueueFree();
            }, Ui.Wide));
            _root.AddChild(overlay);
            edit.GrabFocus();
        }

        // ================================================================== pages

        private void BuildPages()
        {
            _pages[Page.Home] = BuildHome();
            _pages[Page.Play] = BuildPlay();
            _pages[Page.Inventory] = BuildInventory();
            _pages[Page.Shop] = BuildInfoPage("МАГАЗИН", new[]
            {
                "Прямая покупка любого скина за Кредиты — всегда без случайности.",
                "Кейсы: шансы каждой редкости и полный список предметов показываются до покупки.",
                "Ротация витрины (ежедневная и еженедельная), наборы, наклейки, брелоки.",
                "Итоговая цена и остаток валюты — перед каждой покупкой, без скрытых списаний."
            }, "Откроется вместе с сервером покупок и инвентаря (фаза 7).");
            _pages[Page.Pass] = BuildPassPage();
            _pages[Page.Stats] = BuildStatsPage();
            _pages[Page.Settings] = BuildSettingsPage();
            foreach (var p in _pages.Values) _root.AddChild(p);
        }

        private void ShowPage(Page page)
        {
            _page = page;
            foreach (var kv in _pages) kv.Value.Visible = kv.Key == page;
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                bool active = TabPages[i] == page;
                var color = active ? Ui.Selected : new Color(0f, 0f, 0f, 0f);
                Ui.SetButtonColors(_tabButtons[i], color, i == 0 ? Ui.Accent : (active ? Colors.White : Ui.Text), 0);
            }
            _partyBar.Visible = page == Page.Home || page == Page.Play;
            _background.Visible = true;
            if (page == Page.Stats) RefreshStats();
            if (page == Page.Play) RefreshPlay();
            RefreshParty();
        }

        private static Control ContentArea()
        {
            return Ui.Place(new Control { MouseFilter = Control.MouseFilterEnum.Ignore }, 0f, 0f, 1f, 1f, 0f, 84f, 0f, 0f);
        }

        // ------------------------------------------------------------------ home

        private Control BuildHome()
        {
            var page = ContentArea();
            var news = Ui.Place(Ui.VBox(12), 0f, 1f, 0f, 1f, 32f, -470f, 620f, -140f);
            news.Alignment = BoxContainer.AlignmentMode.End;
            page.AddChild(news);
            news.AddChild(NewsCard("ПОЛИГОН ОТКРЫТ", "Отработайте контроль отдачи, контр-стрейф и прострелы. 23 оружия, движущиеся мишени, урон по зонам.", true));
            news.AddChild(NewsCard("ДОРОЖНАЯ КАРТА", "Раунды и экономика → гранаты → боты → онлайн-матчи → магазин и инвентарь → новые карты.", false));

            var play = Ui.Button("ИГРАТЬ", 34, Ui.Go, Colors.White, () => ShowPage(Page.Play), Ui.Wide, 4);
            Ui.Place(play, 1f, 1f, 1f, 1f, -420f, -130f, -32f, -40f);
            page.AddChild(play);
            return page;
        }

        private Control NewsCard(string title, string text, bool highlight)
        {
            var card = Ui.PanelBox(highlight ? new Color(0.12f, 0.1f, 0.07f, 0.92f) : Ui.Panel, 4, 18f);
            var v = Ui.VBox(6);
            card.AddChild(v);
            v.AddChild(Ui.Label(highlight ? "НОВОЕ" : "НОВОСТИ", 15, Ui.Accent, Ui.Wide));
            v.AddChild(Ui.Label(title, 26, Ui.Text, Ui.Wide));
            var body = Ui.Label(text, 18, Ui.Muted);
            body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            body.CustomMinimumSize = new Vector2(540f, 0f);
            v.AddChild(body);
            return card;
        }

        // ------------------------------------------------------------------ play

        private Control BuildPlay()
        {
            var page = ContentArea();
            var panel = Ui.Fill(Ui.Rect(new Color(0.04f, 0.05f, 0.065f, 0.86f)));
            panel.MouseFilter = Control.MouseFilterEnum.Stop;
            page.AddChild(panel);

            // Mode tabs.
            var modes = Ui.Place(Ui.HBox(4), 0f, 0f, 1f, 0f, 32f, 16f, -32f, 86f);
            page.AddChild(modes);
            for (int i = 0; i < MapCatalog.Modes.Length; i++)
            {
                int index = i;
                var m = MapCatalog.Modes[i];
                var b = Ui.Button(m.Title, 21, Ui.PanelLight, Ui.Text, () => SelectMode(index), Ui.Wide, 2);
                b.CustomMinimumSize = new Vector2(0f, 70f);
                b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                if (!m.Online)
                {
                    var tag = Ui.Place(Ui.Label("ОФЛАЙН", 12, Ui.Accent, Ui.Wide, HorizontalAlignment.Center), 0f, 1f, 1f, 1f, 0f, -20f, 0f, -4f);
                    b.AddChild(tag);
                }
                modes.AddChild(b);
                _modeButtons.Add(b);
            }

            _modeDescription = Ui.Place(Ui.Label("", 24, Ui.Text, Ui.Bold), 0f, 0f, 1f, 0f, 34f, 100f, -32f, 136f);
            page.AddChild(_modeDescription);
            _modeExtra = Ui.Place(Ui.Label("", 18, Ui.Muted), 0f, 0f, 1f, 0f, 34f, 136f, -32f, 164f);
            page.AddChild(_modeExtra);

            var selectAll = Ui.Button("ВЫБРАТЬ ВСЕ", 17, Ui.PanelLight, Ui.Text, () => SetAllMaps(true), Ui.Wide);
            Ui.Place(selectAll, 1f, 0f, 1f, 0f, -420f, 104f, -236f, 150f);
            page.AddChild(selectAll);
            var clearAll = Ui.Button("СНЯТЬ ВСЕ", 17, Ui.PanelLight, Ui.Text, () => SetAllMaps(false), Ui.Wide);
            Ui.Place(clearAll, 1f, 0f, 1f, 0f, -226f, 104f, -32f, 150f);
            page.AddChild(clearAll);

            var scroll = Ui.Place(new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, 0f, 0f, 1f, 1f, 32f, 176f, -32f, -150f);
            page.AddChild(scroll);
            var content = Ui.VBox(18);
            content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(content);
            _mapGrid = new HFlowContainer();
            _mapGrid.AddThemeConstantOverride("h_separation", 16);
            _mapGrid.AddThemeConstantOverride("v_separation", 16);
            _mapGrid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            content.AddChild(_mapGrid);
            _trainingOptions = Ui.VBox(10);
            content.AddChild(_trainingOptions);

            // Bottom-right: region + primary action.
            var region = Ui.Place(Ui.HBox(8), 1f, 1f, 1f, 1f, -980f, -112f, -480f, -60f);
            region.Alignment = BoxContainer.AlignmentMode.End;
            region.AddChild(MenuIcon.Create(IconKind.Globe, 22f, Ui.Muted));
            region.AddChild(Ui.Label("РЕГИОН: АВТО · ПИНГ —", 18, Ui.Muted, Ui.Wide));
            page.AddChild(region);
            _goNote = Ui.Place(Ui.Label("", 16, Ui.Muted, Ui.Regular, HorizontalAlignment.Right), 1f, 1f, 1f, 1f, -980f, -60f, -480f, -30f);
            page.AddChild(_goNote);
            _goButton = Ui.Button("", 32, Ui.Go, Colors.White, Go, Ui.Wide, 4);
            Ui.Place(_goButton, 1f, 1f, 1f, 1f, -440f, -130f, -32f, -36f);
            page.AddChild(_goButton);
            return page;
        }

        private void SelectMode(int index)
        {
            _mode = index;
            RefreshPlay();
            RefreshParty();
        }

        private void SetAllMaps(bool on)
        {
            var set = _selectedMaps[_mode];
            set.Clear();
            if (on) foreach (var id in MapCatalog.Modes[_mode].Maps) set.Add(id);
            RefreshPlay();
        }

        private void RefreshPlay()
        {
            var mode = MapCatalog.Modes[_mode];
            for (int i = 0; i < _modeButtons.Count; i++)
            {
                bool active = i == _mode;
                Ui.SetButtonColors(_modeButtons[i], active ? Ui.Selected.Lightened(0.08f) : Ui.PanelLight, active ? Colors.White : Ui.Muted, 2);
                _modeButtons[i].AddThemeStyleboxOverride("normal", ActiveTab(active));
            }
            _modeDescription.Text = mode.Description;
            _modeExtra.Text = mode.Ranked
                ? "Ранг: не откалиброван — 10 побед для калибровки. Ранг общий, статистика ведётся по каждой карте."
                : (mode.Online ? "Без влияния на рейтинг." : "Не требует сети и аккаунта.");

            foreach (Node child in _mapGrid.GetChildren()) child.QueueFree();
            foreach (var id in mode.Maps) _mapGrid.AddChild(MapCard(MapCatalog.Get(id)));

            foreach (Node child in _trainingOptions.GetChildren()) child.QueueFree();
            if (!mode.Online)
            {
                var s = GameContext.Settings;
                _trainingOptions.AddChild(Ui.Label("ПАРАМЕТРЫ", 18, Ui.Muted, Ui.Wide));
                var row = Ui.HBox(10);
                _trainingOptions.AddChild(row);
                if (_mode == 5) row.AddChild(OptionToggle("Бесконечный боезапас", () => _optInfiniteAmmo, v => _optInfiniteAmmo = v));
                row.AddChild(OptionToggle("Мишени двигаются", () => s.TargetsMove, v => s.TargetsMove = v));
                row.AddChild(OptionToggle("Броня у мишеней", () => s.TargetArmor, v => s.TargetArmor = v));
                row.AddChild(OptionToggle("Шлем у мишеней", () => s.TargetHelmet, v => s.TargetHelmet = v));
            }

            bool anyMap = _selectedMaps[_mode].Count > 0;
            if (mode.Online)
            {
                _goButton.Text = "НАЙТИ ИГРУ";
                _goButton.Disabled = true;
                _goNote.Text = "Онлайн-матчи откроются с запуском серверов и матчмейкинга (фаза 6)";
            }
            else
            {
                _goButton.Text = "ЗАПУСТИТЬ";
                _goButton.Disabled = !anyMap;
                _goNote.Text = anyMap ? "Карта: Полигон · офлайн" : "Отметьте карту";
            }
        }

        private static StyleBoxFlat ActiveTab(bool active)
        {
            var sb = Ui.Box(active ? new Color(0.2f, 0.25f, 0.31f, 0.98f) : Ui.PanelLight, 2);
            if (active)
            {
                sb.BorderColor = Ui.Accent;
                sb.BorderWidthBottom = 4;
            }
            return sb;
        }

        private Control MapCard(MapInfo map)
        {
            bool selected = _selectedMaps[_mode].Contains(map.Id);
            var b = new Button { CustomMinimumSize = new Vector2(330f, 210f), FocusMode = Control.FocusModeEnum.None };
            var border = selected ? Ui.Accent : new Color(1f, 1f, 1f, 0.08f);
            b.AddThemeStyleboxOverride("normal", Ui.Box(Ui.PanelLight, 4, border, selected ? 3 : 1, 0f, 0f));
            b.AddThemeStyleboxOverride("hover", Ui.Box(Ui.PanelLight.Lightened(0.08f), 4, Ui.Accent, 2, 0f, 0f));
            b.AddThemeStyleboxOverride("pressed", Ui.Box(Ui.PanelLight.Lightened(0.15f), 4, Ui.Accent, 3, 0f, 0f));
            b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            b.Pressed += () =>
            {
                GameContext.App?.PlayUi("ui_click");
                var set = _selectedMaps[_mode];
                if (!set.Remove(map.Id)) set.Add(map.Id);
                RefreshPlay();
            };

            var art = Ui.Place(new MapArt { Map = map, MouseFilter = Control.MouseFilterEnum.Ignore }, 0f, 0f, 1f, 1f, 3f, 3f, -3f, -58f);
            b.AddChild(art);
            b.AddChild(Ui.Place(Ui.Label(map.Name, 24, Ui.Text, Ui.Wide), 0f, 1f, 1f, 1f, 14f, -56f, -14f, -26f));
            b.AddChild(Ui.Place(Ui.Label(map.Available ? map.Subtitle : map.Subtitle + " · в разработке", 15, Ui.Muted), 0f, 1f, 1f, 1f, 14f, -28f, -14f, -6f));

            var check = Ui.Place(Ui.PanelBox(selected ? Ui.Accent : new Color(0f, 0f, 0f, 0.55f), 3, 0f), 1f, 0f, 1f, 0f, -44f, 10f, -10f, 44f);
            check.MouseFilter = Control.MouseFilterEnum.Ignore;
            if (selected) check.AddChild(MenuIcon.Create(IconKind.Check, 30f, Colors.White));
            b.AddChild(check);
            if (!map.Available)
            {
                var lockIcon = MenuIcon.Create(IconKind.Lock, 26f, new Color(1f, 1f, 1f, 0.8f));
                Ui.Place(lockIcon, 0f, 0f, 0f, 0f, 12f, 12f, 38f, 38f);
                b.AddChild(lockIcon);
            }
            return b;
        }

        private Button OptionToggle(string title, Func<bool> get, Action<bool> set)
        {
            var b = Ui.Button("", 18, Ui.PanelLight, Ui.Text, null, Ui.Bold);
            b.CustomMinimumSize = new Vector2(280f, 56f);
            Action refresh = () =>
            {
                bool on = get();
                b.Text = title + (on ? " · ВКЛ" : " · ВЫКЛ");
                Ui.SetButtonColors(b, on ? new Color(Ui.Accent.R, Ui.Accent.G, Ui.Accent.B, 0.45f) : Ui.PanelLight, on ? Colors.White : Ui.Muted);
            };
            b.Pressed += () =>
            {
                GameContext.App?.PlayUi("ui_click");
                set(!get());
                GameContext.Settings.Save();
                refresh();
            };
            refresh();
            return b;
        }

        private void Go()
        {
            var mode = MapCatalog.Modes[_mode];
            if (mode.Online || _selectedMaps[_mode].Count == 0) return;
            GameContext.App?.PlayUi("ui_accept");
            var options = new TrainingOptions
            {
                Mode = _mode == 5 ? TrainingMode.Free : TrainingMode.Targets,
                ModeTitle = _mode == 5 ? "СВОБОДНАЯ ТРЕНИРОВКА" : "ТРЕНИРОВКА С БОТАМИ-МИШЕНЯМИ",
                InfiniteAmmo = _mode == 5 && _optInfiniteAmmo
            };
            GameContext.App.StartTraining(options);
        }

        // ------------------------------------------------------------------ inventory

        private Control BuildInventory()
        {
            var page = ContentArea();
            page.AddChild(Ui.Fill(Ui.Rect(new Color(0.04f, 0.05f, 0.065f, 0.86f))));
            page.AddChild(Ui.Place(Ui.Label("ИНВЕНТАРЬ · СНАРЯЖЕНИЕ ДЛЯ ТРЕНИРОВКИ", 28, Ui.Text, Ui.Wide), 0f, 0f, 1f, 0f, 32f, 20f, -32f, 64f));
            var hint = Ui.Place(Ui.Label("Нажмите на оружие, чтобы взять его в тренировку. Скины, наклейки, брелоки и экипировка для атаки и защиты — фаза 7 и 9.", 18, Ui.Muted), 0f, 0f, 1f, 0f, 32f, 64f, -32f, 96f);
            page.AddChild(hint);
            var holder = Ui.Place(new Control(), 0f, 0f, 1f, 1f, 32f, 110f, -32f, -24f);
            page.AddChild(holder);
            RebuildInventoryGrid(holder);
            return page;
        }

        private void RebuildInventoryGrid(Control holder)
        {
            foreach (Node child in holder.GetChildren()) child.QueueFree();
            var s = GameContext.Settings;
            var grid = WeaponGrid.Build(GameContext.Database,
                def => def.id == s.LoadoutPrimary || def.id == s.LoadoutSecondary,
                def =>
                {
                    if (def.IsMelee) return;
                    if (def.slot == 2) s.LoadoutSecondary = def.id;
                    else s.LoadoutPrimary = def.id;
                    s.Save();
                    Toast("В снаряжении: " + def.displayName);
                    RebuildInventoryGrid(holder);
                });
            holder.AddChild(Ui.Fill(grid));
        }

        // ------------------------------------------------------------------ info pages

        private Control BuildInfoPage(string title, string[] bullets, string footer)
        {
            var page = ContentArea();
            page.AddChild(Ui.Fill(Ui.Rect(new Color(0.04f, 0.05f, 0.065f, 0.86f))));
            var v = Ui.Place(Ui.VBox(16), 0f, 0f, 0.6f, 1f, 64f, 40f, 0f, -40f);
            page.AddChild(v);
            v.AddChild(Ui.Label(title, 44, Ui.Text, Ui.Wide));
            v.AddChild(Ui.Label(footer, 22, Ui.Accent, Ui.Bold));
            foreach (var line in bullets)
            {
                var card = Ui.PanelBox(Ui.PanelLight, 4, 18f);
                var l = Ui.Label(line, 22, Ui.Text);
                l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                l.CustomMinimumSize = new Vector2(800f, 0f);
                card.AddChild(l);
                v.AddChild(card);
            }
            return page;
        }

        private Control BuildPassPage()
        {
            var page = BuildInfoPage("БОЕВОЙ ПРОПУСК", new[]
            {
                "Сезонный пропуск с бесплатной и премиальной линейкой наград.",
                "Опыт за матчи, победы и задания; немного Кредитов за уровни.",
            }, "Сезон 1 стартует вместе с онлайн-режимами.");
            var track = Ui.Place(Ui.HBox(10), 0f, 1f, 1f, 1f, 64f, -230f, -64f, -80f);
            page.AddChild(track);
            for (int i = 1; i <= 10; i++)
            {
                var tier = Ui.PanelBox(i == 1 ? new Color(0.22f, 0.18f, 0.1f, 0.95f) : Ui.PanelLight, 4, 10f);
                tier.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                var tv = Ui.VBox(6);
                tier.AddChild(tv);
                tv.AddChild(Ui.Label("УР. " + i, 18, i == 1 ? Ui.Accent : Ui.Muted, Ui.Wide, HorizontalAlignment.Center));
                var icon = MenuIcon.Create(i % 3 == 0 ? IconKind.Crate : (i % 2 == 0 ? IconKind.Coin : IconKind.Gem), 42f, new Color(1f, 1f, 1f, i == 1 ? 0.9f : 0.35f));
                icon.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
                tv.AddChild(icon);
                tv.AddChild(MenuIcon.Create(IconKind.Lock, 20f, new Color(1f, 1f, 1f, 0.4f)));
                track.AddChild(tier);
            }
            return page;
        }

        private GridContainer _statsGrid;

        private Control BuildStatsPage()
        {
            var page = ContentArea();
            page.AddChild(Ui.Fill(Ui.Rect(new Color(0.04f, 0.05f, 0.065f, 0.86f))));
            page.AddChild(Ui.Place(Ui.Label("СТАТИСТИКА · ПОЛИГОН", 34, Ui.Text, Ui.Wide), 0f, 0f, 1f, 0f, 64f, 30f, -64f, 80f));
            page.AddChild(Ui.Place(Ui.Label("K/D, ADR, процент побед, лучшие карты и история матчей появятся с онлайн-матчами.", 18, Ui.Muted), 0f, 0f, 1f, 0f, 64f, 84f, -64f, 112f));
            _statsGrid = Ui.Place(new GridContainer { Columns = 4 }, 0f, 0f, 1f, 0f, 64f, 140f, -64f, 520f);
            _statsGrid.AddThemeConstantOverride("h_separation", 16);
            _statsGrid.AddThemeConstantOverride("v_separation", 16);
            page.AddChild(_statsGrid);
            return page;
        }

        private void RefreshStats()
        {
            foreach (Node child in _statsGrid.GetChildren()) child.QueueFree();
            var t = GameContext.Lifetime;
            int minutes = (int)(t.Seconds / 60.0);
            Tile("ВРЕМЯ НА ПОЛИГОНЕ", minutes + " мин");
            Tile("ВЫСТРЕЛОВ", t.Shots.ToString());
            Tile("ПОПАДАНИЙ", t.Hits.ToString());
            Tile("ТОЧНОСТЬ", Mathf.RoundToInt(t.Accuracy * 100f) + "%");
            Tile("В ГОЛОВУ", Mathf.RoundToInt(t.HeadshotRate * 100f) + "%");
            Tile("УБИЙСТВ", t.Kills.ToString());
            Tile("УРОН", t.Damage.ToString());
            Tile("УРОН ЗА ВЫСТРЕЛ", t.Shots > 0 ? (t.Damage / (float)t.Shots).ToString("0.0") : "—");
        }

        private void Tile(string title, string value)
        {
            var card = Ui.PanelBox(Ui.PanelLight, 4, 20f);
            card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            card.CustomMinimumSize = new Vector2(0f, 150f);
            var v = Ui.VBox(8);
            card.AddChild(v);
            v.AddChild(Ui.Label(title, 17, Ui.Muted, Ui.Wide));
            v.AddChild(Ui.Label(value, 46, Ui.Text, Ui.Bold));
            _statsGrid.AddChild(card);
        }

        private Control BuildSettingsPage()
        {
            var page = ContentArea();
            page.AddChild(Ui.Fill(Ui.Rect(new Color(0.04f, 0.05f, 0.065f, 0.92f))));
            page.AddChild(Ui.Place(Ui.Label("НАСТРОЙКИ", 34, Ui.Text, Ui.Wide), 0f, 0f, 1f, 0f, 64f, 24f, -64f, 74f));
            var panel = Ui.Place(new SettingsPanel(), 0f, 0f, 1f, 1f, 64f, 90f, -64f, -32f);
            page.AddChild(panel);
            return page;
        }
    }
}
