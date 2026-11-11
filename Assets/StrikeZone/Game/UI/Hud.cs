using System.Collections.Generic;
using System.Text;
using StrikeZone.Core;
using UnityEngine;
using UnityEngine.UI;

namespace StrikeZone.Game
{
    /// <summary>
    /// In-match HUD for the training range: dynamic crosshair (gap = real inaccuracy),
    /// hit markers, scope overlay, health/armor, ammo, speed meter, hit log, accuracy stats,
    /// optional spray-pattern overlay. Text is only rebuilt when the value changes, to
    /// avoid per-frame garbage on mobile.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        private const float CanvasHalfHeight = 540f;
        private const int MaxLogLines = 6;
        private const int RecoilDots = 40;

        private PlayerController _player;
        private RectTransform _root;

        private RectTransform[] _crossBars;
        private Image[] _crossImages;
        private RectTransform _crossDot;
        private Image[] _hitMarker;
        private float _hitMarkerTimer;
        private bool _hitMarkerKill;
        private GameObject _scope;
        private Image _damageFlash;
        private float _damageFlashAlpha;

        private Text _health;
        private Text _armor;
        private Text _ammo;
        private Text _weaponName;
        private Text _speed;
        private Text _fps;
        private Text _stats;
        private Text _log;
        private Text _message;
        private float _messageTimer;
        private Text _reloadHint;

        private RectTransform[] _recoilDots;
        private WeaponRuntime _recoilFor;

        private readonly Queue<string> _logLines = new Queue<string>();
        private readonly StringBuilder _sb = new StringBuilder(256);

        private int _shownHealth = -1;
        private int _shownArmor = -1;
        private bool _shownHelmet;
        private int _shownAmmo = -1;
        private int _shownReserve = -1;
        private int _shownSpeed = -1;
        private int _shownShots = -1;
        private int _shownKills = -1;
        private float _fpsTimer;
        private int _fpsFrames;

        public void Initialize(PlayerController player, Canvas canvas)
        {
            _player = player;
            _root = (RectTransform)canvas.transform;

            // Damage flash behind everything.
            var flash = UiFactory.Stretch("DamageFlash", _root);
            _damageFlash = UiFactory.AddImage(flash, new Color(0.8f, 0f, 0f, 0f));

            // Scope overlay: circle mask + black side panels + thin reticle.
            var scopeRoot = UiFactory.Stretch("Scope", _root);
            _scope = scopeRoot.gameObject;
            var mask = UiFactory.Rect("Mask", scopeRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 1080f));
            UiFactory.AddImage(mask, Color.white, ProceduralTextures.ScopeMask());
            var left = UiFactory.Rect("Left", scopeRoot, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(1f, 0.5f), new Vector2(-539f, 0f), Vector2.zero);
            left.offsetMax = new Vector2(-539f, 0f);
            UiFactory.AddImage(left, Color.black);
            var right = UiFactory.Rect("Right", scopeRoot, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            right.offsetMin = new Vector2(539f, 0f);
            UiFactory.AddImage(right, Color.black);
            var h = UiFactory.Rect("H", scopeRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 2f));
            UiFactory.AddImage(h, new Color(0f, 0f, 0f, 0.9f));
            var v = UiFactory.Rect("V", scopeRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 1080f));
            UiFactory.AddImage(v, new Color(0f, 0f, 0f, 0.9f));
            _scope.SetActive(false);

            // Crosshair: four bars + optional dot.
            _crossBars = new RectTransform[4];
            _crossImages = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                bool horizontal = i < 2;
                var bar = UiFactory.Rect("Cross" + i, _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                    horizontal ? new Vector2(14f, 3f) : new Vector2(3f, 14f));
                _crossImages[i] = UiFactory.AddImage(bar, new Color(0.35f, 1f, 0.45f, 0.95f));
                bar.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
                _crossBars[i] = bar;
            }
            _crossDot = UiFactory.Rect("CrossDot", _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 3f));
            UiFactory.AddImage(_crossDot, new Color(0.35f, 1f, 0.45f, 0.95f));

            _hitMarker = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + 90f * i;
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var m = UiFactory.Rect("HitMarker" + i, _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), dir * 16f, new Vector2(12f, 3f));
                m.localRotation = Quaternion.Euler(0f, 0f, angle);
                _hitMarker[i] = UiFactory.AddImage(m, new Color(1f, 1f, 1f, 0f));
            }

            // Recoil pattern overlay dots.
            _recoilDots = new RectTransform[RecoilDots];
            for (int i = 0; i < RecoilDots; i++)
            {
                var d = UiFactory.Rect("RecoilDot" + i, _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5f, 5f));
                UiFactory.AddImage(d, new Color(1f, 0.55f, 0.15f, 0.8f), UiFactory.CircleSprite);
                d.gameObject.SetActive(false);
                _recoilDots[i] = d;
            }

            // Bottom: health / armor (left of centre), ammo (right of centre).
            var bottom = new Vector2(0.5f, 0f);
            _health = UiFactory.Label(_root, "Health", "100", 54, TextAnchor.LowerRight, UiFactory.TextColor, bottom, bottom, new Vector2(1f, 0f), new Vector2(-330f, 24f), new Vector2(200f, 70f));
            UiFactory.Label(_root, "HealthLabel", "ЗДОРОВЬЕ", 20, TextAnchor.LowerLeft, UiFactory.MutedColor, bottom, bottom, new Vector2(0f, 0f), new Vector2(-325f, 34f), new Vector2(140f, 30f));
            _armor = UiFactory.Label(_root, "Armor", "100", 40, TextAnchor.LowerRight, UiFactory.TextColor, bottom, bottom, new Vector2(1f, 0f), new Vector2(-330f, 92f), new Vector2(200f, 50f));
            UiFactory.Label(_root, "ArmorLabel", "БРОНЯ", 20, TextAnchor.LowerLeft, UiFactory.MutedColor, bottom, bottom, new Vector2(0f, 0f), new Vector2(-325f, 100f), new Vector2(200f, 30f));

            _ammo = UiFactory.Label(_root, "Ammo", "30 / 90", 54, TextAnchor.LowerLeft, UiFactory.TextColor, bottom, bottom, new Vector2(0f, 0f), new Vector2(330f, 24f), new Vector2(320f, 70f));
            _weaponName = UiFactory.Label(_root, "WeaponName", "", 24, TextAnchor.LowerLeft, UiFactory.AccentColor, bottom, bottom, new Vector2(0f, 0f), new Vector2(332f, 92f), new Vector2(420f, 34f));
            _reloadHint = UiFactory.Label(_root, "Reload", "", 28, TextAnchor.MiddleCenter, UiFactory.AccentColor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(600f, 40f));

            _speed = UiFactory.Label(_root, "Speed", "", 22, TextAnchor.MiddleCenter, UiFactory.MutedColor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(300f, 30f));

            var topLeft = new Vector2(0f, 1f);
            _fps = UiFactory.Label(_root, "Fps", "", 20, TextAnchor.UpperLeft, UiFactory.MutedColor, topLeft, topLeft, topLeft, new Vector2(24f, -100f), new Vector2(300f, 30f));
            _log = UiFactory.Label(_root, "HitLog", "", 22, TextAnchor.UpperLeft, UiFactory.TextColor, topLeft, topLeft, topLeft, new Vector2(24f, -130f), new Vector2(900f, 220f));

            var top = new Vector2(0.5f, 1f);
            _stats = UiFactory.Label(_root, "Stats", "", 24, TextAnchor.UpperCenter, UiFactory.TextColor, top, top, top, new Vector2(0f, -20f), new Vector2(900f, 34f));
            _message = UiFactory.Label(_root, "Message", "", 34, TextAnchor.MiddleCenter, UiFactory.AccentColor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1400f, 50f));

            if (player.Weapons.Current != null) OnWeaponChanged(player.Weapons.Current);
            ShowMessage("Полигон: стрельба, отдача, прострелы, движение", 4f);
        }

        // ------------------------------------------------------------------ events

        public void OnWeaponChanged(WeaponRuntime w)
        {
            if (_weaponName == null || w == null) return;
            _weaponName.text = w.Def.displayName;
            _shownAmmo = -1;
            _recoilFor = null;
        }

        public void OnShot()
        {
        }

        public void OnReload()
        {
        }

        public void OnHit(ref HitInfo hit, bool killed)
        {
            _hitMarkerTimer = 0.18f;
            _hitMarkerKill = killed;

            var settings = GameContext.Settings;
            if (settings != null && !settings.ShowHitInfo) return;
            _sb.Length = 0;
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
            _damageFlashAlpha = Mathf.Clamp01(0.25f + damage / 100f);
            AddLog("Урон: −" + damage + " (" + source + ")");
        }

        public void ShowMessage(string text, float seconds)
        {
            if (_message == null) return;
            _message.text = text;
            _messageTimer = seconds;
        }

        private void AddLog(string line)
        {
            _logLines.Enqueue(line);
            while (_logLines.Count > MaxLogLines) _logLines.Dequeue();
            _sb.Length = 0;
            foreach (var l in _logLines) _sb.Append(l).Append('\n');
            _log.text = _sb.ToString();
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

        // ------------------------------------------------------------------ per frame

        private void LateUpdate()
        {
            if (_player == null) return;
            float dt = Time.unscaledDeltaTime;
            WeaponRuntime w = _player.Weapons.Current;
            var settings = GameContext.Settings;

            UpdateCrosshair(w);
            UpdateRecoilOverlay(w, settings != null && settings.ShowRecoilPattern);

            // Hit marker fade.
            if (_hitMarkerTimer > 0f) _hitMarkerTimer -= dt;
            float hmAlpha = Mathf.Clamp01(_hitMarkerTimer / 0.18f);
            Color hmColor = _hitMarkerKill ? new Color(1f, 0.25f, 0.2f, hmAlpha) : new Color(1f, 1f, 1f, hmAlpha);
            for (int i = 0; i < 4; i++) _hitMarker[i].color = hmColor;

            // Damage flash fade.
            if (_damageFlashAlpha > 0f)
            {
                _damageFlashAlpha = Mathf.MoveTowards(_damageFlashAlpha, 0f, dt * 1.5f);
                _damageFlash.color = new Color(0.8f, 0f, 0f, _damageFlashAlpha * 0.5f);
            }

            // Health / armor.
            if (_player.Health != _shownHealth)
            {
                _shownHealth = _player.Health;
                _health.text = _shownHealth.ToString();
                _health.color = _shownHealth <= 25 ? new Color(1f, 0.35f, 0.3f) : UiFactory.TextColor;
            }
            if (_player.Armor != _shownArmor || _player.HasHelmet != _shownHelmet)
            {
                _shownArmor = _player.Armor;
                _shownHelmet = _player.HasHelmet;
                _armor.text = _shownArmor + (_shownHelmet ? " +шлем" : "");
            }

            // Ammo.
            if (w != null)
            {
                int ammo = w.Def.IsMelee ? -2 : w.Ammo;
                int reserve = w.InfiniteReserve ? -2 : w.Reserve;
                if (ammo != _shownAmmo || reserve != _shownReserve)
                {
                    _shownAmmo = ammo;
                    _shownReserve = reserve;
                    if (w.Def.IsMelee) _ammo.text = "—";
                    else _ammo.text = w.Ammo + " / " + (w.InfiniteReserve ? "∞" : w.Reserve.ToString());
                    _ammo.color = !w.Def.IsMelee && w.Ammo <= Mathf.Max(1, w.Def.magazineSize / 5) ? new Color(1f, 0.45f, 0.3f) : UiFactory.TextColor;
                }
                _reloadHint.text = w.IsReloading ? "ПЕРЕЗАРЯДКА…" : (!w.Def.IsMelee && w.Ammo == 0 ? "ПУСТО — ПЕРЕЗАРЯДИТЕ" : "");
            }

            // Speed meter (game units/s, the number designers tune against).
            int speed = Mathf.RoundToInt(_player.HorizontalSpeed);
            bool showInfo = settings == null || settings.ShowHitInfo;
            if (speed != _shownSpeed)
            {
                _shownSpeed = speed;
                _speed.text = showInfo ? speed + " ед/с" : "";
            }

            // Stats.
            var stats = GameContext.Stats;
            if (stats != null && (stats.Shots != _shownShots || stats.Kills != _shownKills))
            {
                _shownShots = stats.Shots;
                _shownKills = stats.Kills;
                _stats.text = "Выстрелов " + stats.Shots + "   Точность " + Mathf.RoundToInt(stats.Accuracy * 100f) +
                              "%   В голову " + Mathf.RoundToInt(stats.HeadshotRate * 100f) + "%   Убийств " + stats.Kills;
            }

            // FPS counter (updated twice per second).
            _fpsFrames++;
            _fpsTimer += dt;
            if (_fpsTimer >= 0.5f)
            {
                _fps.text = Mathf.RoundToInt(_fpsFrames / _fpsTimer) + " FPS";
                _fpsFrames = 0;
                _fpsTimer = 0f;
            }

            if (_messageTimer > 0f)
            {
                _messageTimer -= dt;
                if (_messageTimer <= 0f) _message.text = "";
            }
        }

        private float DegreesToCanvas(float degrees)
        {
            float halfFov = _player.Camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            return Mathf.Tan(degrees * Mathf.Deg2Rad) / Mathf.Tan(halfFov) * CanvasHalfHeight;
        }

        private void UpdateCrosshair(WeaponRuntime w)
        {
            bool sniper = w != null && w.Def.Category == WeaponCategory.Sniper;
            bool scoped = w != null && w.IsScoped;
            _scope.SetActive(sniper && scoped);

            // Snipers have no crosshair unscoped (no-scopes are a skill, not a freebie).
            bool show = w != null && !(sniper) && _player.IsAlive;
            for (int i = 0; i < 4; i++) _crossBars[i].gameObject.SetActive(show);
            _crossDot.gameObject.SetActive(show || (w != null && w.Def.IsMelee));
            if (!show) return;

            float spreadPx = DegreesToCanvas(_player.Weapons.CurrentSpread);
            float gap = 4f + Mathf.Min(spreadPx, 220f);
            _crossBars[0].anchoredPosition = new Vector2(-(gap + 7f), 0f);
            _crossBars[1].anchoredPosition = new Vector2(gap + 7f, 0f);
            _crossBars[2].anchoredPosition = new Vector2(0f, gap + 7f);
            _crossBars[3].anchoredPosition = new Vector2(0f, -(gap + 7f));
        }

        private void UpdateRecoilOverlay(WeaponRuntime w, bool enabled)
        {
            bool show = enabled && w != null && w.Def.fullAuto && !w.Def.IsMelee;
            if (!show)
            {
                if (_recoilFor != null || _recoilDots[0].gameObject.activeSelf)
                {
                    for (int i = 0; i < RecoilDots; i++) _recoilDots[i].gameObject.SetActive(false);
                    _recoilFor = null;
                }
                return;
            }

            // Where bullets land relative to the crosshair if you do not compensate:
            // the camera shows `follow` of the recoil, the bullets get all of it.
            float follow = w.Def.cameraRecoilFollow;
            int count = Mathf.Min(RecoilDots, w.Def.magazineSize);
            for (int i = 0; i < RecoilDots; i++)
            {
                bool active = i < count;
                _recoilDots[i].gameObject.SetActive(active);
                if (!active) continue;
                float p, y;
                w.GetPatternPoint(i, out p, out y);
                _recoilDots[i].anchoredPosition = new Vector2(DegreesToCanvas(y * (1f - follow)), DegreesToCanvas(p * (1f - follow)));
            }
            _recoilFor = w;
        }
    }
}
