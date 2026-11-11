using System.Collections.Generic;
using System.Text;
using Godot;
using StrikeZone.Core;

namespace StrikeZone.Game
{
    /// <summary>
    /// Match HUD: dynamic crosshair (gap = real inaccuracy in degrees), hit marker, sniper
    /// scope, damage flash, health/armor, ammo, speed meter, hit log, accuracy stats and an
    /// optional spray-pattern overlay. Text is rebuilt only when a value changes.
    /// </summary>
    public partial class Hud : Control
    {
        private const int MaxLog = 6;

        private PlayerController _player;
        private Label _health, _armor, _ammo, _weapon, _speed, _fps, _stats, _log, _message, _reload;
        private float _messageTimer;
        private float _hitMarker;
        private bool _hitKill;
        private float _damageFlash;
        private readonly Queue<string> _logLines = new Queue<string>();
        private readonly StringBuilder _sb = new StringBuilder(256);
        private int _shownHealth = -1, _shownArmor = -1, _shownAmmo = -9, _shownReserve = -9, _shownSpeed = -1, _shownShots = -1, _shownKills = -1;
        private bool _shownHelmet;
        private float _fpsTimer;

        public void Setup(PlayerController player)
        {
            _player = player;
        }

        public override void _Ready()
        {
            Ui.Fill(this);
            MouseFilter = MouseFilterEnum.Ignore;

            _health = Add(Ui.Label("100", 56, Ui.Text, Ui.Bold, HorizontalAlignment.Right), 0.5f, 1f, -560f, -100f, -340f, -24f);
            Add(Ui.Label("ЗДОРОВЬЕ", 18, Ui.Muted, Ui.Wide), 0.5f, 1f, -330f, -70f, -150f, -40f);
            _armor = Add(Ui.Label("100", 34, Ui.Text, Ui.Bold, HorizontalAlignment.Right), 0.5f, 1f, -560f, -150f, -340f, -100f);
            Add(Ui.Label("БРОНЯ", 18, Ui.Muted, Ui.Wide), 0.5f, 1f, -330f, -135f, -150f, -105f);

            _ammo = Add(Ui.Label("30 / 90", 56, Ui.Text, Ui.Bold), 0.5f, 1f, 340f, -100f, 700f, -24f);
            _weapon = Add(Ui.Label("", 22, Ui.Accent, Ui.Wide), 0.5f, 1f, 342f, -140f, 800f, -104f);
            _reload = Add(Ui.Label("", 26, Ui.Accent, Ui.Wide, HorizontalAlignment.Center), 0.5f, 0.5f, -300f, 100f, 300f, 140f);
            _speed = Add(Ui.Label("", 20, Ui.Muted, Ui.Regular, HorizontalAlignment.Center), 0.5f, 0.5f, -150f, 60f, 150f, 90f);

            _fps = Add(Ui.Label("", 18, Ui.Muted), 0f, 0f, 24f, 96f, 300f, 120f);
            _log = Add(Ui.Label("", 20, Ui.Text), 0f, 0f, 24f, 124f, 900f, 330f);
            _log.VerticalAlignment = VerticalAlignment.Top;
            _stats = Add(Ui.Label("", 22, Ui.Text, Ui.Bold, HorizontalAlignment.Center), 0.5f, 0f, -500f, 16f, 500f, 48f);
            _message = Add(Ui.Label("", 32, Ui.Accent, Ui.Wide, HorizontalAlignment.Center), 0.5f, 0.5f, -800f, -260f, 800f, -210f);
        }

        private Label Add(Label l, float ax, float ay, float left, float top, float right, float bottom)
        {
            Ui.Place(l, ax, ay, ax, ay, left, top, right, bottom);
            AddChild(l);
            return l;
        }

        // ------------------------------------------------------------------ events

        public void OnWeaponChanged(WeaponRuntime w)
        {
            if (_weapon == null || w == null) return;
            _weapon.Text = w.Def.displayName.ToUpperInvariant();
            _shownAmmo = -9;
        }

        public void OnShot()
        {
        }

        public void OnHit(ref HitInfo hit, bool killed)
        {
            _hitMarker = 0.18f;
            _hitKill = killed;
            if (!GameContext.Settings.ShowHitInfo) return;
            _sb.Clear();
            _sb.Append(hit.Weapon != null ? hit.Weapon.displayName : "?").Append(" → ").Append(GroupName(hit.Group))
               .Append("  ").Append(hit.Damage.HealthDamage);
            if (hit.Damage.ArmorDamage > 0) _sb.Append(" (броня −").Append(hit.Damage.ArmorDamage).Append(')');
            _sb.Append("  ").Append(hit.DistanceMeters.ToString("0.0")).Append(" м");
            if (hit.Penetrated) _sb.Append("  [прострел]");
            if (killed) _sb.Append(hit.Damage.Headshot ? "  — ХЕДШОТ!" : "  — УБИТ");
            AddLog(_sb.ToString());
        }

        public void OnPlayerDamaged(int damage, string source)
        {
            _damageFlash = Mathf.Clamp(0.25f + damage / 100f, 0f, 1f);
            AddLog("Урон: −" + damage + " (" + source + ")");
        }

        public void ShowMessage(string text, float seconds)
        {
            if (_message == null) return;
            _message.Text = text;
            _messageTimer = seconds;
        }

        private void AddLog(string line)
        {
            _logLines.Enqueue(line);
            while (_logLines.Count > MaxLog) _logLines.Dequeue();
            _sb.Clear();
            foreach (var l in _logLines) _sb.Append(l).Append('\n');
            _log.Text = _sb.ToString();
        }

        private static string GroupName(HitGroup g)
        {
            switch (g)
            {
                case HitGroup.Head: return "голова";
                case HitGroup.Chest: return "грудь";
                case HitGroup.Stomach: return "живот";
                case HitGroup.LeftArm:
                case HitGroup.RightArm: return "рука";
                case HitGroup.LeftLeg:
                case HitGroup.RightLeg: return "нога";
                default: return "тело";
            }
        }

        // ------------------------------------------------------------------ frame

        public override void _Process(double delta)
        {
            if (_player == null) return;
            float dt = (float)delta;
            var w = _player.Weapons?.Current;

            if (_hitMarker > 0f) _hitMarker -= dt;
            if (_damageFlash > 0f) _damageFlash = Mathf.MoveToward(_damageFlash, 0f, dt * 1.5f);

            if (_player.Health != _shownHealth)
            {
                _shownHealth = _player.Health;
                _health.Text = _shownHealth.ToString();
                Ui.SetLabelColor(_health, _shownHealth <= 25 ? Ui.Danger : Ui.Text);
            }
            if (_player.Armor != _shownArmor || _player.HasHelmet != _shownHelmet)
            {
                _shownArmor = _player.Armor;
                _shownHelmet = _player.HasHelmet;
                _armor.Text = _shownArmor + (_shownHelmet ? " +шлем" : "");
            }

            if (w != null)
            {
                int ammo = w.Def.IsMelee ? -2 : w.Ammo;
                int reserve = w.InfiniteReserve ? -2 : w.Reserve;
                if (ammo != _shownAmmo || reserve != _shownReserve)
                {
                    _shownAmmo = ammo;
                    _shownReserve = reserve;
                    _ammo.Text = w.Def.IsMelee ? "—" : w.Ammo + " / " + (w.InfiniteReserve ? "∞" : w.Reserve.ToString());
                    bool low = !w.Def.IsMelee && w.Ammo <= Mathf.Max(1, w.Def.magazineSize / 5);
                    Ui.SetLabelColor(_ammo, low ? Ui.Danger : Ui.Text);
                }
                _reload.Text = w.IsReloading ? "ПЕРЕЗАРЯДКА" : (!w.Def.IsMelee && w.Ammo == 0 ? "ПУСТО — ПЕРЕЗАРЯДИТЕ" : "");
            }

            int speed = Mathf.RoundToInt(_player.HorizontalSpeed);
            if (speed != _shownSpeed)
            {
                _shownSpeed = speed;
                _speed.Text = GameContext.Settings.ShowHitInfo ? speed + " ед/с" : "";
            }

            var stats = GameContext.Session;
            if (stats != null && (stats.Shots != _shownShots || stats.Kills != _shownKills))
            {
                _shownShots = stats.Shots;
                _shownKills = stats.Kills;
                _stats.Text = "ВЫСТРЕЛОВ " + stats.Shots + "    ТОЧНОСТЬ " + Mathf.RoundToInt(stats.Accuracy * 100f) +
                              "%    В ГОЛОВУ " + Mathf.RoundToInt(stats.HeadshotRate * 100f) + "%    УБИЙСТВ " + stats.Kills;
            }

            _fpsTimer += dt;
            if (_fpsTimer >= 0.5f)
            {
                _fpsTimer = 0f;
                _fps.Text = Engine.GetFramesPerSecond() + " FPS";
            }

            if (_messageTimer > 0f)
            {
                _messageTimer -= dt;
                if (_messageTimer <= 0f) _message.Text = "";
            }
            QueueRedraw();
        }

        private float DegToPixels(float degrees)
        {
            var cam = _player.Camera;
            float fov = cam != null ? cam.Fov : PlayerController.BaseFov;
            return Mathf.Tan(Mathf.DegToRad(degrees)) / Mathf.Tan(Mathf.DegToRad(fov * 0.5f)) * (Size.Y * 0.5f);
        }

        public override void _Draw()
        {
            if (_player == null) return;
            Vector2 c = Size * 0.5f;
            var w = _player.Weapons?.Current;

            if (_damageFlash > 0f) DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.8f, 0f, 0f, _damageFlash * 0.45f));

            bool sniper = w != null && w.Def.Category == WeaponCategory.Sniper;
            if (sniper && w.IsScoped)
            {
                // Scope: black outside a circle, thin reticle.
                float r = Size.Y * 0.48f;
                DrawRect(new Rect2(0f, 0f, c.X - r, Size.Y), Colors.Black);
                DrawRect(new Rect2(c.X + r, 0f, Size.X - c.X - r, Size.Y), Colors.Black);
                DrawArc(c, r + Size.Y * 0.15f, 0f, Mathf.Tau, 96, Colors.Black, Size.Y * 0.3f, true);
                DrawLine(new Vector2(c.X - r, c.Y), new Vector2(c.X + r, c.Y), new Color(0f, 0f, 0f, 0.9f), 2f);
                DrawLine(new Vector2(c.X, c.Y - r), new Vector2(c.X, c.Y + r), new Color(0f, 0f, 0f, 0.9f), 2f);
            }

            if (w != null && _player.IsAlive)
            {
                Color cross = new Color(0.35f, 1f, 0.45f, 0.95f);
                Color outline = new Color(0f, 0f, 0f, 0.7f);
                if (!sniper && !w.Def.IsMelee)
                {
                    // Snipers have no crosshair unscoped: no-scopes are a skill, not a freebie.
                    float gap = 4f + Mathf.Min(DegToPixels(_player.Weapons.CurrentSpread), 220f);
                    const float len = 12f;
                    DrawBar(c + new Vector2(-gap - len, -1.5f), new Vector2(len, 3f), cross, outline);
                    DrawBar(c + new Vector2(gap, -1.5f), new Vector2(len, 3f), cross, outline);
                    DrawBar(c + new Vector2(-1.5f, -gap - len), new Vector2(3f, len), cross, outline);
                    DrawBar(c + new Vector2(-1.5f, gap), new Vector2(3f, len), cross, outline);
                }
                if (!sniper || w.Def.IsMelee) DrawBar(c - new Vector2(1.5f, 1.5f), new Vector2(3f, 3f), cross, outline);

                if (GameContext.Settings.ShowRecoilPattern && w.Def.fullAuto)
                {
                    // Where bullets land relative to the crosshair without compensation.
                    float follow = w.Def.cameraRecoilFollow;
                    int count = Mathf.Min(40, w.Def.magazineSize);
                    for (int i = 0; i < count; i++)
                    {
                        float p, y;
                        w.GetPatternPoint(i, out p, out y);
                        Vector2 pos = c + new Vector2(DegToPixels(y * (1f - follow)), -DegToPixels(p * (1f - follow)));
                        DrawCircle(pos, 2.5f, new Color(1f, 0.55f, 0.15f, 0.8f));
                    }
                }
            }

            if (_hitMarker > 0f)
            {
                float a = Mathf.Clamp(_hitMarker / 0.18f, 0f, 1f);
                Color hm = _hitKill ? new Color(1f, 0.25f, 0.2f, a) : new Color(1f, 1f, 1f, a);
                for (int i = 0; i < 4; i++)
                {
                    float ang = Mathf.Pi * 0.25f + Mathf.Pi * 0.5f * i;
                    Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    DrawLine(c + d * 10f, c + d * 22f, hm, 3f, true);
                }
            }
        }

        private void DrawBar(Vector2 pos, Vector2 size, Color fill, Color outline)
        {
            DrawRect(new Rect2(pos - Vector2.One, size + Vector2.One * 2f), outline);
            DrawRect(new Rect2(pos, size), fill);
        }
    }
}
