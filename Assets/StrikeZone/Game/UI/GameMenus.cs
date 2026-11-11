using System;
using System.Collections.Generic;
using StrikeZone.Core;
using UnityEngine;
using UnityEngine.UI;

namespace StrikeZone.Game
{
    /// <summary>
    /// Training-range menus: the arsenal (pick any weapon — the real buy menu with money
    /// arrives with rounds and economy in phase 2) and settings (sensitivity, FPS cap,
    /// graphics preset, target and training options).
    /// </summary>
    public sealed class GameMenus : MonoBehaviour
    {
        private GameDatabase _db;
        private PlayerController _player;
        private InputRouter _input;
        private Action _applySettings;
        private Action _resetStats;

        private GameObject _arsenal;
        private GameObject _settingsPanel;
        private readonly List<Action> _refreshers = new List<Action>();

        public bool IsOpen
        {
            get { return (_arsenal != null && _arsenal.activeSelf) || (_settingsPanel != null && _settingsPanel.activeSelf); }
        }

        public void Initialize(GameDatabase db, PlayerController player, InputRouter input, Canvas canvas, Action applySettings, Action resetStats)
        {
            _db = db;
            _player = player;
            _input = input;
            _applySettings = applySettings;
            _resetStats = resetStats;

            var root = canvas.transform;
            var tl = new Vector2(0f, 1f);
            UiFactory.MenuButton(root, "ArsenalButton", "АРСЕНАЛ", tl, tl, tl, new Vector2(20f, -20f), new Vector2(190f, 64f), ToggleArsenal, 26);
            UiFactory.MenuButton(root, "SettingsButton", "НАСТРОЙКИ", tl, tl, tl, new Vector2(220f, -20f), new Vector2(200f, 64f), ToggleSettings, 26);
            UiFactory.MenuButton(root, "ResetButton", "СБРОС", tl, tl, tl, new Vector2(430f, -20f), new Vector2(130f, 64f), () =>
            {
                if (_resetStats != null) _resetStats();
            }, 26);

            BuildArsenal(root);
            BuildSettings(root);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.B)) ToggleArsenal();
            if (Input.GetKeyDown(KeyCode.F10)) ToggleSettings();
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) CloseAll();
        }

        private void ToggleArsenal()
        {
            bool open = !_arsenal.activeSelf;
            CloseAll();
            _arsenal.SetActive(open);
            SyncInput();
        }

        private void ToggleSettings()
        {
            bool open = !_settingsPanel.activeSelf;
            CloseAll();
            if (open) foreach (var r in _refreshers) r();
            _settingsPanel.SetActive(open);
            SyncInput();
        }

        private void CloseAll()
        {
            _arsenal.SetActive(false);
            _settingsPanel.SetActive(false);
            SyncInput();
        }

        private void SyncInput()
        {
            _input.MenuOpen = IsOpen;
            if (IsOpen) _input.SetCursorLocked(false);
        }

        // ------------------------------------------------------------------ arsenal

        private void BuildArsenal(Transform canvas)
        {
            var panel = UiFactory.Rect("Arsenal", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1640f, 900f));
            UiFactory.AddImage(panel, UiFactory.PanelColor, null, true);
            _arsenal = panel.gameObject;

            UiFactory.Label(panel, "Title", "АРСЕНАЛ — выберите оружие", 36, TextAnchor.UpperLeft, UiFactory.AccentColor,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -24f), new Vector2(900f, 50f));
            UiFactory.MenuButton(panel, "Close", "ЗАКРЫТЬ", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(200f, 60f), CloseAll, 26);

            var categories = new[]
            {
                new KeyValuePair<WeaponCategory, string>(WeaponCategory.Pistol, "Пистолеты"),
                new KeyValuePair<WeaponCategory, string>(WeaponCategory.SMG, "Пистолеты-пулемёты"),
                new KeyValuePair<WeaponCategory, string>(WeaponCategory.Shotgun, "Дробовики"),
                new KeyValuePair<WeaponCategory, string>(WeaponCategory.Rifle, "Винтовки"),
                new KeyValuePair<WeaponCategory, string>(WeaponCategory.Sniper, "Снайперские"),
                new KeyValuePair<WeaponCategory, string>(WeaponCategory.MachineGun, "Пулемёты")
            };

            float y = -90f;
            foreach (var cat in categories)
            {
                UiFactory.Label(panel, "Cat_" + cat.Key, cat.Value, 26, TextAnchor.MiddleLeft, UiFactory.MutedColor,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, y - 10f), new Vector2(300f, 60f));
                float x = 330f;
                foreach (var def in _db.Weapons)
                {
                    if (def.Category != cat.Key) continue;
                    WeaponDef captured = def;
                    string side = def.Side == TeamSide.Attack ? " (А)" : (def.Side == TeamSide.Defense ? " (З)" : "");
                    string label = def.displayName + side + "\n$" + def.price;
                    UiFactory.MenuButton(panel, "W_" + def.id, label, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(x, y), new Vector2(180f, 112f), () => Equip(captured), 19);
                    x += 186f;
                }
                y -= 124f;
            }

            UiFactory.Label(panel, "Hint", "(А) — оружие атаки, (З) — оружие защиты. Цены — для экономики из фазы 2; на полигоне всё бесплатно.", 20,
                TextAnchor.LowerLeft, UiFactory.MutedColor, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 20f), new Vector2(1500f, 30f));
            _arsenal.SetActive(false);
        }

        private void Equip(WeaponDef def)
        {
            _player.Weapons.Give(def, true);
            CloseAll();
        }

        // ------------------------------------------------------------------ settings

        private void BuildSettings(Transform canvas)
        {
            var panel = UiFactory.Rect("Settings", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1640f, 900f));
            UiFactory.AddImage(panel, UiFactory.PanelColor, null, true);
            _settingsPanel = panel.gameObject;

            UiFactory.Label(panel, "Title", "НАСТРОЙКИ", 36, TextAnchor.UpperLeft, UiFactory.AccentColor,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -24f), new Vector2(600f, 50f));
            UiFactory.MenuButton(panel, "Close", "ГОТОВО", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(200f, 60f), () =>
            {
                GameContext.Settings.Save();
                CloseAll();
            }, 26);

            var s = GameContext.Settings;
            float colA = 30f;
            float colB = 840f;
            float y = -110f;
            const float step = 82f;

            Stepper(panel, colA, y, "Чувствительность (касание)", () => s.TouchSensitivity.ToString("0.0"), d => s.TouchSensitivity = Mathf.Clamp(s.TouchSensitivity + d * 0.1f, 0.2f, 4f)); y -= step;
            Stepper(panel, colA, y, "Множитель в прицеле", () => s.ScopedSensitivityMultiplier.ToString("0.00"), d => s.ScopedSensitivityMultiplier = Mathf.Clamp(s.ScopedSensitivityMultiplier + d * 0.05f, 0.2f, 1.5f)); y -= step;
            Stepper(panel, colA, y, "Чувствительность мыши", () => s.MouseSensitivity.ToString("0.0"), d => s.MouseSensitivity = Mathf.Clamp(s.MouseSensitivity + d * 0.1f, 0.2f, 8f)); y -= step;
            Toggle(panel, colA, y, "Инверсия по вертикали", () => s.InvertY, v => s.InvertY = v); y -= step;
            Toggle(panel, colA, y, "Ускорение обзора", () => s.LookAcceleration, v => s.LookAcceleration = v); y -= step;
            Toggle(panel, colA, y, "Присед: переключение", () => s.CrouchToggle, v => s.CrouchToggle = v); y -= step;
            Stepper(panel, colA, y, "Ограничение FPS", () => s.TargetFps.ToString(), d => s.TargetFps = CycleFps(s.TargetFps, d)); y -= step;
            Stepper(panel, colA, y, "Графика", () => QualityPresets.Names[Mathf.Clamp(s.GraphicsPreset, 0, 3)], d => s.GraphicsPreset = Mathf.Clamp(s.GraphicsPreset + d, 0, 3)); y -= step;
            Stepper(panel, colA, y, "Громкость", () => Mathf.RoundToInt(s.MasterVolume * 100f) + "%", d => s.MasterVolume = Mathf.Clamp01(s.MasterVolume + d * 0.1f)); y -= step;

            y = -110f;
            Toggle(panel, colB, y, "Броня у мишеней", () => s.TargetArmor, v => s.TargetArmor = v); y -= step;
            Toggle(panel, colB, y, "Шлем у мишеней", () => s.TargetHelmet, v => s.TargetHelmet = v); y -= step;
            Toggle(panel, colB, y, "Мишени двигаются", () => s.TargetsMove, v => s.TargetsMove = v); y -= step;
            Toggle(panel, colB, y, "Бесконечный боезапас", () => s.InfiniteAmmo, v => s.InfiniteAmmo = v); y -= step;
            Toggle(panel, colB, y, "Паттерн отдачи на экране", () => s.ShowRecoilPattern, v => s.ShowRecoilPattern = v); y -= step;
            Toggle(panel, colB, y, "Трассеры", () => s.ShowTracers, v => s.ShowTracers = v); y -= step;
            Toggle(panel, colB, y, "Информация о попаданиях", () => s.ShowHitInfo, v => s.ShowHitInfo = v); y -= step;
            Toggle(panel, colB, y, "Вибрация", () => s.Vibration, v => s.Vibration = v); y -= step;
            Toggle(panel, colB, y, "Сенсорное управление", () => _input.TouchVisible, v => _input.TouchVisible = v); y -= step;

            _settingsPanel.SetActive(false);
        }

        private static int CycleFps(int current, int dir)
        {
            int[] options = { 30, 60, 90, 120 };
            int idx = Array.IndexOf(options, current);
            if (idx < 0) idx = 1;
            idx = Mathf.Clamp(idx + dir, 0, options.Length - 1);
            return options[idx];
        }

        private void Stepper(Transform panel, float x, float y, string title, Func<string> value, Action<int> change)
        {
            var tl = new Vector2(0f, 1f);
            UiFactory.Label(panel, "L_" + title, title, 26, TextAnchor.MiddleLeft, UiFactory.TextColor, tl, tl, tl, new Vector2(x, y), new Vector2(430f, 64f));
            var valueText = UiFactory.Label(panel, "V_" + title, value(), 26, TextAnchor.MiddleCenter, UiFactory.AccentColor, tl, tl, tl, new Vector2(x + 520f, y), new Vector2(140f, 64f));
            Action refresh = () => valueText.text = value();
            UiFactory.MenuButton(panel, "Minus_" + title, "−", tl, tl, tl, new Vector2(x + 440f, y), new Vector2(76f, 64f), () =>
            {
                change(-1);
                refresh();
                Apply();
            }, 34);
            UiFactory.MenuButton(panel, "Plus_" + title, "+", tl, tl, tl, new Vector2(x + 664f, y), new Vector2(76f, 64f), () =>
            {
                change(1);
                refresh();
                Apply();
            }, 34);
            _refreshers.Add(refresh);
        }

        private void Toggle(Transform panel, float x, float y, string title, Func<bool> get, Action<bool> set)
        {
            var tl = new Vector2(0f, 1f);
            UiFactory.Label(panel, "L_" + title, title, 26, TextAnchor.MiddleLeft, UiFactory.TextColor, tl, tl, tl, new Vector2(x, y), new Vector2(520f, 64f));
            Button button = null;
            Action refresh = () =>
            {
                bool on = get();
                UiFactory.SetButtonLabel(button, on ? "ВКЛ" : "ВЫКЛ");
                UiFactory.SetButtonTint(button, on ? new Color(0.98f, 0.66f, 0.18f, 0.55f) : new Color(1f, 1f, 1f, 0.12f));
            };
            button = UiFactory.MenuButton(panel, "T_" + title, "", tl, tl, tl, new Vector2(x + 560f, y), new Vector2(180f, 64f), () =>
            {
                set(!get());
                refresh();
                Apply();
            }, 26);
            refresh();
            _refreshers.Add(refresh);
        }

        private void Apply()
        {
            if (_applySettings != null) _applySettings();
        }
    }
}
