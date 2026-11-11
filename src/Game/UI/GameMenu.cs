using System;
using Godot;
using StrikeZone.Core;

namespace StrikeZone.Game
{
    /// <summary>
    /// In-match overlay: a МЕНЮ button (top-left, also Esc) opening a side-nav panel with
    /// Resume, Arsenal (pick any weapon — the money-based buy menu arrives with rounds and
    /// economy in phase 2), Settings, Reset statistics and Exit to the main menu.
    /// </summary>
    public partial class GameMenu : Control
    {
        public Action<WeaponDef> OnWeaponPicked;
        public Action OnResetStats;
        public Action OnExit;
        public Action OnSettingsChanged;
        public InputRouter Router;

        private Control _overlay;
        private Control _content;
        private Label _title;
        private SettingsPanel _settings;
        private Control _arsenal;

        public bool IsOpen
        {
            get { return _overlay != null && _overlay.Visible; }
        }

        public override void _Ready()
        {
            Ui.Fill(this);
            MouseFilter = MouseFilterEnum.Ignore;

            var menuButton = Ui.Button("МЕНЮ", 22, Ui.Bar, Ui.Text, Open, Ui.Wide);
            Ui.Place(menuButton, 0f, 0f, 0f, 0f, 20f, 20f, 180f, 76f);
            AddChild(menuButton);

            _overlay = Ui.Fill(new Control { MouseFilter = MouseFilterEnum.Stop });
            _overlay.Visible = false;
            AddChild(_overlay);
            _overlay.AddChild(Ui.Fill(Ui.Rect(new Color(0f, 0f, 0f, 0.55f))));

            var nav = Ui.Place(Ui.Rect(Ui.Bar), 0f, 0f, 0f, 1f, 0f, 0f, 380f, 0f);
            _overlay.AddChild(nav);
            var navBox = Ui.Place(Ui.VBox(8), 0f, 0f, 1f, 1f, 28f, 40f, -28f, -40f);
            nav.AddChild(navBox);
            navBox.AddChild(Ui.Label("STRIKE ZONE", 34, Ui.Text, Ui.Wide));
            navBox.AddChild(Ui.Label("ПОЛИГОН", 18, Ui.Accent, Ui.Wide));
            navBox.AddChild(Ui.Spacer(0f, 30f));
            NavButton(navBox, "ПРОДОЛЖИТЬ", Close);
            NavButton(navBox, "АРСЕНАЛ", () => ShowPage(false));
            NavButton(navBox, "НАСТРОЙКИ", () => ShowPage(true));
            NavButton(navBox, "СБРОС СТАТИСТИКИ", () =>
            {
                OnResetStats?.Invoke();
                Close();
            });
            navBox.AddChild(Ui.Spacer(0f, 0f, true));
            var exit = Ui.Button("ВЫЙТИ В ГЛАВНОЕ МЕНЮ", 22, new Color(0.35f, 0.12f, 0.1f, 0.9f), Ui.Text, () => OnExit?.Invoke(), Ui.Wide);
            exit.CustomMinimumSize = new Vector2(0f, 64f);
            navBox.AddChild(exit);

            _content = Ui.Place(new Control(), 0f, 0f, 1f, 1f, 430f, 40f, -40f, -40f);
            _overlay.AddChild(_content);
            _title = Ui.Place(Ui.Label("", 36, Ui.Text, Ui.Wide), 0f, 0f, 1f, 0f, 0f, 0f, 0f, 50f);
            _content.AddChild(_title);

            _settings = new SettingsPanel
            {
                Changed = () => OnSettingsChanged?.Invoke(),
                GetTouchVisible = () => Router != null && Router.TouchVisible,
                SetTouchVisible = v => { if (Router != null) Router.TouchVisible = v; }
            };
            Ui.Place(_settings, 0f, 0f, 1f, 1f, 0f, 70f, 0f, 0f);
            _settings.Visible = false;
            _content.AddChild(_settings);

            RebuildArsenal();
        }

        private void RebuildArsenal()
        {
            _arsenal?.QueueFree();
            var current = GameContext.Player?.Weapons;
            _arsenal = WeaponGrid.Build(GameContext.Database,
                def => current != null && (current.GetSlot(def.slot)?.Def == def),
                def =>
                {
                    OnWeaponPicked?.Invoke(def);
                    Close();
                });
            Ui.Place(_arsenal, 0f, 0f, 1f, 1f, 0f, 70f, 0f, 0f);
            _arsenal.Visible = false;
            _content.AddChild(_arsenal);
        }

        private static void NavButton(VBoxContainer parent, string text, Action action)
        {
            var b = Ui.Button(text, 24, new Color(1f, 1f, 1f, 0.04f), Ui.Text, action, Ui.Wide);
            b.Alignment = HorizontalAlignment.Left;
            b.CustomMinimumSize = new Vector2(0f, 64f);
            parent.AddChild(b);
        }

        private void ShowPage(bool settings)
        {
            if (!settings) RebuildArsenal();
            _title.Text = settings ? "НАСТРОЙКИ" : "АРСЕНАЛ";
            _settings.Visible = settings;
            _arsenal.Visible = !settings;
        }

        public void Open()
        {
            _overlay.Visible = true;
            ShowPage(false);
            Router?.SetMenuOpen(true);
        }

        public void Close()
        {
            _settings.Visible = false;
            _overlay.Visible = false;
            Router?.SetMenuOpen(false);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Escape)
                {
                    if (IsOpen) Close();
                    else Open();
                    GetViewport().SetInputAsHandled();
                }
                else if (key.Keycode == Key.B && !IsOpen)
                {
                    Open();
                    GetViewport().SetInputAsHandled();
                }
            }
        }
    }
}
