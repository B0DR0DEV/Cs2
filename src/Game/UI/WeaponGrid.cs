using System;
using System.Collections.Generic;
using Godot;
using StrikeZone.Core;

namespace StrikeZone.Game
{
    /// <summary>
    /// Weapon cards grouped by category with key stats as bars. Used by the in-match
    /// arsenal and by the Inventory tab of the main menu (loadout selection).
    /// </summary>
    public static class WeaponGrid
    {
        public static readonly KeyValuePair<WeaponCategory, string>[] Categories =
        {
            new KeyValuePair<WeaponCategory, string>(WeaponCategory.Pistol, "ПИСТОЛЕТЫ"),
            new KeyValuePair<WeaponCategory, string>(WeaponCategory.SMG, "ПИСТОЛЕТЫ-ПУЛЕМЁТЫ"),
            new KeyValuePair<WeaponCategory, string>(WeaponCategory.Shotgun, "ДРОБОВИКИ"),
            new KeyValuePair<WeaponCategory, string>(WeaponCategory.Rifle, "ВИНТОВКИ"),
            new KeyValuePair<WeaponCategory, string>(WeaponCategory.Sniper, "СНАЙПЕРСКИЕ"),
            new KeyValuePair<WeaponCategory, string>(WeaponCategory.MachineGun, "ПУЛЕМЁТЫ")
        };

        public static ScrollContainer Build(GameDatabase db, Func<WeaponDef, bool> isSelected, Action<WeaponDef> onPick)
        {
            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            var list = Ui.VBox(14);
            list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(list);

            foreach (var cat in Categories)
            {
                list.AddChild(Ui.Label(cat.Value, 20, Ui.Muted, Ui.Wide));
                var flow = new HFlowContainer();
                flow.AddThemeConstantOverride("h_separation", 10);
                flow.AddThemeConstantOverride("v_separation", 10);
                flow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                list.AddChild(flow);
                foreach (var def in db.Weapons)
                {
                    if (def.Category != cat.Key) continue;
                    flow.AddChild(Card(def, isSelected != null && isSelected(def), onPick));
                }
            }
            return scroll;
        }

        private static Control Card(WeaponDef def, bool selected, Action<WeaponDef> onPick)
        {
            var b = new Button { CustomMinimumSize = new Vector2(250f, 150f), FocusMode = Control.FocusModeEnum.None };
            Color bg = selected ? new Color(Ui.Accent.R * 0.35f, Ui.Accent.G * 0.35f, Ui.Accent.B * 0.35f, 0.95f) : Ui.PanelLight;
            var border = selected ? Ui.Accent : Ui.Line;
            b.AddThemeStyleboxOverride("normal", Ui.Box(bg, 4, border, selected ? 2 : 1));
            b.AddThemeStyleboxOverride("hover", Ui.Box(bg.Lightened(0.08f), 4, Ui.Accent, 1));
            b.AddThemeStyleboxOverride("pressed", Ui.Box(bg.Lightened(0.16f), 4, Ui.Accent, 2));
            b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            if (onPick != null)
            {
                WeaponDef captured = def;
                b.Pressed += () =>
                {
                    GameContext.App?.PlayUi("ui_click");
                    onPick(captured);
                };
            }

            var v = Ui.VBox(4);
            Ui.Place(v, 0f, 0f, 1f, 1f, 12f, 10f, -12f, -10f);
            v.MouseFilter = Control.MouseFilterEnum.Ignore;
            b.AddChild(v);

            var top = Ui.HBox(6);
            top.MouseFilter = Control.MouseFilterEnum.Ignore;
            var name = Ui.Label(def.displayName, 21, Ui.Text, Ui.Bold);
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            name.ClipText = true;
            top.AddChild(name);
            string side = def.Side == TeamSide.Attack ? "АТК" : (def.Side == TeamSide.Defense ? "ЗАЩ" : "");
            if (side.Length > 0) top.AddChild(Ui.Label(side, 15, def.Side == TeamSide.Attack ? new Color(0.92f, 0.72f, 0.4f) : new Color(0.5f, 0.7f, 0.95f), Ui.Wide));
            v.AddChild(top);
            v.AddChild(Ui.Label("$" + def.price + "   награда $" + def.killReward, 16, Ui.Muted));

            float dps = def.IsMelee ? 0f : def.damage * def.pellets / def.cycleTime;
            float accuracy = def.IsMelee ? 0f : 1f - Mathf.Clamp((def.HasScope ? def.scopedInaccuracyStand : def.inaccuracyStand) / 1.5f, 0f, 1f);
            Bar(v, "УРОН", def.IsMelee ? def.meleeAltDamage / 120f : def.damage * def.pellets / 160f);
            Bar(v, "ТЕМП", def.IsMelee ? 0.2f : Mathf.Clamp(dps / 600f, 0f, 1f));
            Bar(v, "ТОЧНОСТЬ", accuracy);
            Bar(v, "СКОРОСТЬ", def.maxSpeed / 250f);
            return b;
        }

        private static void Bar(VBoxContainer parent, string title, float value)
        {
            var row = Ui.HBox(8);
            row.MouseFilter = Control.MouseFilterEnum.Ignore;
            var label = Ui.Label(title, 13, Ui.Muted, Ui.Wide);
            label.CustomMinimumSize = new Vector2(90f, 0f);
            row.AddChild(label);
            var track = new Panel { CustomMinimumSize = new Vector2(120f, 6f), MouseFilter = Control.MouseFilterEnum.Ignore };
            track.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            track.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            track.AddThemeStyleboxOverride("panel", Ui.Box(new Color(1f, 1f, 1f, 0.1f), 2, null, 0, 0f, 0f));
            var fill = Ui.Rect(Ui.Accent);
            Ui.Place(fill, 0f, 0f, Mathf.Clamp(value, 0.03f, 1f), 1f);
            track.AddChild(fill);
            row.AddChild(track);
            parent.AddChild(row);
        }
    }
}
