using System;
using System.Collections.Generic;
using Godot;

namespace StrikeZone.Game
{
    /// <summary>
    /// Settings page used both in the main menu and in the match pause menu.
    /// Changes apply immediately and are saved when the panel is hidden.
    /// </summary>
    public partial class SettingsPanel : Control
    {
        public Action Changed;
        public Func<bool> GetTouchVisible;
        public Action<bool> SetTouchVisible;

        private readonly List<Action> _refreshers = new List<Action>();

        public override void _Ready()
        {
            var s = GameContext.Settings;
            var scroll = Ui.Fill(new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled });
            AddChild(scroll);
            var columns = Ui.HBox(48);
            columns.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(columns);

            var left = Ui.VBox(10);
            left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var right = Ui.VBox(10);
            right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            columns.AddChild(left);
            columns.AddChild(right);

            Header(left, "УПРАВЛЕНИЕ");
            Stepper(left, "Чувствительность (касание)", () => s.TouchSensitivity.ToString("0.0"), d => s.TouchSensitivity = Mathf.Clamp(s.TouchSensitivity + d * 0.1f, 0.2f, 4f));
            Stepper(left, "Множитель в прицеле", () => s.ScopedSensitivityMultiplier.ToString("0.00"), d => s.ScopedSensitivityMultiplier = Mathf.Clamp(s.ScopedSensitivityMultiplier + d * 0.05f, 0.2f, 1.5f));
            Stepper(left, "Чувствительность мыши", () => s.MouseSensitivity.ToString("0.00"), d => s.MouseSensitivity = Mathf.Clamp(s.MouseSensitivity + d * 0.01f, 0.02f, 1f));
            Toggle(left, "Инверсия по вертикали", () => s.InvertY, v => s.InvertY = v);
            Toggle(left, "Ускорение обзора", () => s.LookAcceleration, v => s.LookAcceleration = v);
            Toggle(left, "Присед: переключение", () => s.CrouchToggle, v => s.CrouchToggle = v);
            Toggle(left, "Вибрация", () => s.Vibration, v => s.Vibration = v);
            if (GetTouchVisible != null) Toggle(left, "Сенсорное управление", GetTouchVisible, v => SetTouchVisible?.Invoke(v));

            Header(right, "ГРАФИКА И ЗВУК");
            Stepper(right, "Качество графики", () => QualityPresets.Names[Mathf.Clamp(s.GraphicsPreset, 0, 3)], d => s.GraphicsPreset = Mathf.Clamp(s.GraphicsPreset + d, 0, 3));
            Stepper(right, "Ограничение FPS", () => s.TargetFps.ToString(), d => s.TargetFps = CycleFps(s.TargetFps, d));
            Stepper(right, "Громкость", () => Mathf.RoundToInt(s.MasterVolume * 100f) + "%", d => s.MasterVolume = Mathf.Clamp(s.MasterVolume + d * 0.1f, 0f, 1f));
            Header(right, "ТРЕНИРОВКА");
            Toggle(right, "Броня у мишеней", () => s.TargetArmor, v => s.TargetArmor = v);
            Toggle(right, "Шлем у мишеней", () => s.TargetHelmet, v => s.TargetHelmet = v);
            Toggle(right, "Мишени двигаются", () => s.TargetsMove, v => s.TargetsMove = v);
            Toggle(right, "Бесконечный боезапас", () => s.InfiniteAmmo, v => s.InfiniteAmmo = v);
            Toggle(right, "Паттерн отдачи на экране", () => s.ShowRecoilPattern, v => s.ShowRecoilPattern = v);
            Toggle(right, "Трассеры", () => s.ShowTracers, v => s.ShowTracers = v);
            Toggle(right, "Информация о попаданиях", () => s.ShowHitInfo, v => s.ShowHitInfo = v);

            VisibilityChanged += () =>
            {
                if (Visible) Refresh();
                else GameContext.Settings.Save();
            };
        }

        public void Refresh()
        {
            foreach (var r in _refreshers) r();
        }

        private static void Header(VBoxContainer parent, string text)
        {
            parent.AddChild(Ui.Spacer(0f, 8f));
            parent.AddChild(Ui.Label(text, 22, Ui.Accent, Ui.Wide));
            var line = Ui.Rect(Ui.Line);
            line.CustomMinimumSize = new Vector2(0f, 2f);
            parent.AddChild(line);
        }

        private static int CycleFps(int current, int dir)
        {
            int[] options = { 30, 60, 90, 120 };
            int idx = Array.IndexOf(options, current);
            if (idx < 0) idx = 1;
            return options[Mathf.Clamp(idx + dir, 0, options.Length - 1)];
        }

        private HBoxContainer Row(VBoxContainer parent, string title)
        {
            var row = Ui.HBox(12);
            row.CustomMinimumSize = new Vector2(0f, 60f);
            var bg = Ui.PanelBox(new Color(1f, 1f, 1f, 0.03f), 3, 10f);
            bg.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            bg.AddChild(row);
            parent.AddChild(bg);
            var label = Ui.Label(title, 24, Ui.Text);
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(label);
            return row;
        }

        private void Stepper(VBoxContainer parent, string title, Func<string> value, Action<int> change)
        {
            var row = Row(parent, title);
            var valueLabel = Ui.Label(value(), 24, Ui.Accent, Ui.Bold, HorizontalAlignment.Center);
            valueLabel.CustomMinimumSize = new Vector2(150f, 0f);
            var minus = Ui.Button("−", 30, Ui.PanelLight, Ui.Text, null);
            var plus = Ui.Button("+", 30, Ui.PanelLight, Ui.Text, null);
            minus.CustomMinimumSize = plus.CustomMinimumSize = new Vector2(64f, 52f);
            Action refresh = () => valueLabel.Text = value();
            minus.Pressed += () => { change(-1); refresh(); Notify(); };
            plus.Pressed += () => { change(1); refresh(); Notify(); };
            row.AddChild(minus);
            row.AddChild(valueLabel);
            row.AddChild(plus);
            _refreshers.Add(refresh);
        }

        private void Toggle(VBoxContainer parent, string title, Func<bool> get, Action<bool> set)
        {
            var row = Row(parent, title);
            var button = Ui.Button("", 22, Ui.PanelLight, Ui.Text, null, Ui.Wide);
            button.CustomMinimumSize = new Vector2(150f, 52f);
            Action refresh = () =>
            {
                bool on = get();
                button.Text = on ? "ВКЛ" : "ВЫКЛ";
                Ui.SetButtonColors(button, on ? new Color(Ui.Accent.R, Ui.Accent.G, Ui.Accent.B, 0.55f) : Ui.PanelLight, on ? Colors.White : Ui.Muted);
            };
            button.Pressed += () => { set(!get()); refresh(); Notify(); };
            refresh();
            row.AddChild(button);
            _refreshers.Add(refresh);
        }

        private void Notify()
        {
            GameContext.App?.PlayUi("ui_click");
            GameContext.App?.ApplyGlobalSettings();
            Changed?.Invoke();
        }
    }
}
