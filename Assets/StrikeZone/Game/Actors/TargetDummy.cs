using System.Collections.Generic;
using StrikeZone.Core;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace StrikeZone.Game
{
    public enum TargetMode
    {
        Static,
        Crouching,
        Strafing
    }

    /// <summary>
    /// Humanoid practice target. Every visible body part IS its hitbox (same box, same
    /// size), so what you see is exactly what you hit. Strafing targets move with the
    /// same Source-style movement code as the player — including deceleration when they
    /// change direction — so tracking them trains the right reflexes.
    /// </summary>
    public sealed class TargetDummy : Damageable
    {
        private struct Part
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public Collider Collider;
            public HitGroup Group;
            public Vector3 StandPosition;
            public Vector3 CrouchPosition;
            public float CrouchScaleY;
        }

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly List<Part> _parts = new List<Part>();
        private TargetMode _mode;
        private Transform _body;
        private Transform _helmet;
        private Transform _vest;
        private Transform _healthBar;
        private MeshRenderer _healthBarRenderer;
        private CapsuleCollider _blocker;
        private MaterialPropertyBlock _mpb;

        private Vector3 _spawnPosition;
        private Vector3 _strafeA;
        private Vector3 _strafeB;
        private float _strafeYaw;
        private MovementState _move;
        private int _strafeDir = 1;
        private int _phase; // 0 moving, 1 stopping, 2 pausing
        private float _phaseTimer;
        private float _flashTimer;
        private int _flashPart = -1;
        private float _deathTimer;
        private float _toppleAngle;

        public TargetMode Mode
        {
            get { return _mode; }
        }

        public static TargetDummy Create(Transform parent, Vector3 position, float yawDeg, TargetMode mode, Vector3 strafeA, Vector3 strafeB)
        {
            var go = new GameObject("Target_" + mode);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            var dummy = go.AddComponent<TargetDummy>();
            dummy.Build(mode, strafeA, strafeB);
            return dummy;
        }

        private void Build(TargetMode mode, Vector3 strafeA, Vector3 strafeB)
        {
            _mode = mode;
            _spawnPosition = transform.position;
            _strafeA = strafeA;
            _strafeB = strafeB;
            Vector3 axis = strafeB - strafeA;
            _strafeYaw = Mathf.Atan2(axis.x, axis.z) * Mathf.Rad2Deg;
            _move = MovementState.Create();
            _mpb = new MaterialPropertyBlock();

            var materials = GameContext.Materials;
            Material skin = materials.Lit(new Color(0.82f, 0.68f, 0.58f), 0.3f);
            Material cloth = materials.Lit(new Color(0.34f, 0.41f, 0.5f), 0.2f);
            Material pants = materials.Lit(new Color(0.24f, 0.26f, 0.3f), 0.2f);
            Material gear = materials.Lit(new Color(0.2f, 0.23f, 0.18f), 0.35f);

            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);

            AddPart("Head", HitGroup.Head, new Vector3(0.2f, 0.24f, 0.22f), new Vector3(0f, 1.67f, 0f), new Vector3(0f, 1.21f, 0.06f), 1f, skin);
            AddPart("Chest", HitGroup.Chest, new Vector3(0.42f, 0.34f, 0.24f), new Vector3(0f, 1.37f, 0f), new Vector3(0f, 0.91f, 0.04f), 1f, cloth);
            AddPart("Stomach", HitGroup.Stomach, new Vector3(0.36f, 0.26f, 0.22f), new Vector3(0f, 1.07f, 0f), new Vector3(0f, 0.61f, 0f), 1f, cloth);
            AddPart("ArmL", HitGroup.LeftArm, new Vector3(0.1f, 0.62f, 0.12f), new Vector3(-0.27f, 1.24f, 0f), new Vector3(-0.27f, 0.78f, 0.05f), 1f, cloth);
            AddPart("ArmR", HitGroup.RightArm, new Vector3(0.1f, 0.62f, 0.12f), new Vector3(0.27f, 1.24f, 0f), new Vector3(0.27f, 0.78f, 0.05f), 1f, cloth);
            AddPart("LegL", HitGroup.LeftLeg, new Vector3(0.15f, 0.9f, 0.16f), new Vector3(-0.1f, 0.47f, 0f), new Vector3(-0.1f, 0.25f, 0f), 0.52f, pants);
            AddPart("LegR", HitGroup.RightLeg, new Vector3(0.15f, 0.9f, 0.16f), new Vector3(0.1f, 0.47f, 0f), new Vector3(0.1f, 0.25f, 0f), 0.52f, pants);

            // Gear visuals (no colliders): they only show the armor state.
            _helmet = Visual("Helmet", new Vector3(0.235f, 0.1f, 0.255f), gear, _parts[0].Transform, new Vector3(0f, 0.09f, 0f));
            _vest = Visual("Vest", new Vector3(0.45f, 0.58f, 0.27f), gear, _parts[1].Transform, new Vector3(0f, -0.13f, 0f));

            _blocker = gameObject.AddComponent<CapsuleCollider>();
            _blocker.height = mode == TargetMode.Crouching ? 1.37f : 1.83f;
            _blocker.radius = 0.3f;
            _blocker.center = new Vector3(0f, _blocker.height * 0.5f, 0f);
            gameObject.layer = Layers.BodyBlocker;

            // Health bar above the head.
            var bar = new GameObject("HealthBar");
            bar.transform.SetParent(transform, false);
            bar.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Quad();
            _healthBarRenderer = bar.AddComponent<MeshRenderer>();
            _healthBarRenderer.sharedMaterial = materials.Sprite("healthbar", ProceduralTextures.White());
            _healthBarRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _healthBar = bar.transform;

            ApplyPose(mode == TargetMode.Crouching ? 1f : 0f);
            Respawn();
        }

        private void AddPart(string name, HitGroup group, Vector3 size, Vector3 stand, Vector3 crouch, float crouchScaleY, Material material)
        {
            var go = new GameObject(name);
            go.layer = Layers.Hitbox;
            go.transform.SetParent(_body, false);
            go.transform.localPosition = stand;
            go.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Box(size);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = true;
            var hb = go.AddComponent<HitboxPart>();
            hb.Owner = this;
            hb.Group = group;
            _parts.Add(new Part
            {
                Transform = go.transform,
                Renderer = renderer,
                Collider = box,
                Group = group,
                StandPosition = stand,
                CrouchPosition = crouch,
                CrouchScaleY = crouchScaleY
            });
        }

        private static Transform Visual(string name, Vector3 size, Material material, Transform parent, Vector3 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            go.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Box(size);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            return go.transform;
        }

        private void ApplyPose(float crouch)
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                Part p = _parts[i];
                p.Transform.localPosition = Vector3.Lerp(p.StandPosition, p.CrouchPosition, crouch);
                p.Transform.localScale = new Vector3(1f, Mathf.Lerp(1f, p.CrouchScaleY, crouch), 1f);
            }
        }

        /// <summary>Re-reads armor settings and restores full health.</summary>
        public void Respawn()
        {
            var settings = GameContext.Settings;
            Health = MaxHealth;
            Armor = settings == null || settings.TargetArmor ? 100 : 0;
            HasHelmet = Armor > 0 && (settings == null || settings.TargetHelmet);
            _helmet.gameObject.SetActive(HasHelmet);
            _vest.gameObject.SetActive(Armor > 0);
            _toppleAngle = 0f;
            _body.localRotation = Quaternion.identity;
            _deathTimer = 0f;
            _move = MovementState.Create();
            _phase = 0;
            foreach (var p in _parts) p.Collider.enabled = true;
            _blocker.enabled = true;
            _healthBar.gameObject.SetActive(true);
            if (_mode != TargetMode.Strafing) transform.position = _spawnPosition;
            UpdateHealthBar();
        }

        protected override void OnDamaged(ref HitInfo hit)
        {
            PlayerMovement.ApplyTag(ref _move, hit.Weapon != null ? hit.Weapon.taggingStrength : 0.5f);
            _vest.gameObject.SetActive(Armor > 0);
            _helmet.gameObject.SetActive(HasHelmet);
            for (int i = 0; i < _parts.Count; i++)
            {
                if (_parts[i].Group == hit.Group)
                {
                    SetFlash(i);
                    break;
                }
            }
            UpdateHealthBar();
        }

        protected override void OnKilled(ref HitInfo hit)
        {
            foreach (var p in _parts) p.Collider.enabled = false;
            _blocker.enabled = false;
            _healthBar.gameObject.SetActive(false);
            _deathTimer = 1.8f;
            if (GameContext.Stats != null) GameContext.Stats.Kills++;
        }

        /// <summary>True when the attacker is behind the target (used for knife backstabs).</summary>
        public bool IsBehind(Vector3 attackerForward)
        {
            Vector3 f = transform.forward;
            f.y = 0f;
            attackerForward.y = 0f;
            return Vector3.Dot(f.normalized, attackerForward.normalized) > 0.5f;
        }

        private void SetFlash(int index)
        {
            ClearFlash();
            _flashPart = index;
            _flashTimer = 0.09f;
            _mpb.Clear();
            Color c = new Color(1f, 0.25f, 0.2f);
            _mpb.SetColor(ColorId, c);
            _mpb.SetColor(BaseColorId, c);
            _parts[index].Renderer.SetPropertyBlock(_mpb);
        }

        private void ClearFlash()
        {
            if (_flashPart >= 0 && _flashPart < _parts.Count) _parts[_flashPart].Renderer.SetPropertyBlock(null);
            _flashPart = -1;
        }

        private void UpdateHealthBar()
        {
            float k = Mathf.Clamp01(Health / (float)MaxHealth);
            _healthBar.localScale = new Vector3(0.5f * Mathf.Max(k, 0.02f), 0.05f, 1f);
            _mpb.Clear();
            Color c = Color.Lerp(new Color(0.95f, 0.2f, 0.15f), new Color(0.3f, 0.95f, 0.4f), k);
            _mpb.SetColor(ColorId, c);
            _healthBarRenderer.SetPropertyBlock(_mpb);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_flashTimer > 0f)
            {
                _flashTimer -= dt;
                if (_flashTimer <= 0f) ClearFlash();
            }

            if (!IsAlive)
            {
                _toppleAngle = Mathf.MoveTowards(_toppleAngle, 88f, 300f * dt);
                _body.localRotation = Quaternion.Euler(-_toppleAngle, 0f, 0f);
                _deathTimer -= dt;
                if (_deathTimer <= 0f) Respawn();
                return;
            }

            if (_mode == TargetMode.Strafing) UpdateStrafe(dt);
        }

        private void LateUpdate()
        {
            var player = GameContext.Player;
            if (player == null || !_healthBar.gameObject.activeSelf) return;
            float headY = _mode == TargetMode.Crouching ? 1.5f : 1.95f;
            _healthBar.position = transform.position + Vector3.up * headY;
            _healthBar.rotation = Quaternion.LookRotation(player.CameraTransform.forward);
        }

        private void UpdateStrafe(float dt)
        {
            var settings = GameContext.Settings;
            if (settings != null && !settings.TargetsMove)
            {
                _move.Velocity = NVector3.Zero;
                return;
            }

            var cfg = GameContext.Database.Movement;
            var input = new MovementInput { YawDeg = _strafeYaw, WeaponMaxSpeed = 225f };
            Vector3 axis = _strafeB - _strafeA;
            float length = axis.magnitude;
            Vector3 dir = axis / Mathf.Max(length, 0.01f);
            float along = Vector3.Dot(transform.position - _strafeA, dir);

            switch (_phase)
            {
                case 0:
                    input.Forward = _strafeDir;
                    bool pastEnd = (_strafeDir > 0 && along >= length) || (_strafeDir < 0 && along <= 0f);
                    // Occasional mid-lane stop imitates a player jiggle-peeking.
                    bool randomStop = Random.value < 0.35f * dt;
                    if (pastEnd || randomStop)
                    {
                        _phase = 1;
                        if (pastEnd) _strafeDir = -_strafeDir;
                    }
                    break;
                case 1:
                    // Counter-strafe to stop quickly, exactly like a player would.
                    input.Forward = _move.HorizontalSpeed > 30f ? -Mathf.Sign(Vector3.Dot(Units.Direction(_move.Velocity), dir)) : 0f;
                    if (_move.HorizontalSpeed < 15f)
                    {
                        _phase = 2;
                        _phaseTimer = Random.Range(0.15f, 0.9f);
                    }
                    break;
                default:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f)
                    {
                        _phase = 0;
                        if (Random.value < 0.3f) _strafeDir = -_strafeDir;
                    }
                    break;
            }

            _move.OnGround = true;
            PlayerMovement.Simulate(ref _move, ref input, cfg, dt);
            Vector3 v = Units.ToUnity(_move.Velocity);
            v.y = 0f;
            Vector3 next = transform.position + v * dt;
            // Clamp to the lane.
            float nextAlong = Mathf.Clamp(Vector3.Dot(next - _strafeA, dir), -0.3f, length + 0.3f);
            transform.position = _strafeA + dir * nextAlong;
        }
    }
}
