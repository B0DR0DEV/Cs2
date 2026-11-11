using Godot;
using StrikeZone.Core;

namespace StrikeZone.Game
{
    /// <summary>
    /// First-person hands and weapon built from boxes per model profile, with procedural
    /// animation: idle sway, movement bob, firing kick, reload, deploy, inspect, knife swing.
    /// Phase 9 swaps the boxes for real meshes; the animation hooks stay the same.
    /// Model space: the gun points along -Z (camera forward).
    /// </summary>
    public partial class ViewModel : Node3D
    {
        private static readonly Vector3 HipPosition = new Vector3(0.16f, -0.17f, -0.3f);
        private static readonly Vector3 AdsPosition = new Vector3(0f, -0.1f, -0.24f);

        private Node3D _model;
        private Node3D _muzzle;
        private Node3D _eject;
        private MeshInstance3D _flash;
        private StandardMaterial3D _flashMat;
        private OmniLight3D _flashLight;
        private string _profile = "";

        private Vector3 _kickPos;
        private Vector3 _kickRot;
        private float _bobPhase;
        private Vector2 _sway;
        private float _deployT = 1f;
        private float _deployDuration = 1f;
        private float _inspectT = -1f;
        private float _swingT = -1f;
        private float _flashTimer;

        public Node3D Muzzle
        {
            get { return _muzzle; }
        }

        public Node3D EjectPort
        {
            get { return _eject; }
        }

        public override void _Ready()
        {
            _flashMat = MaterialLibrary.NewUnlit(new Color(1f, 0.8f, 0.45f, 1f), ProceduralTextures.Glow(), false, true);
            _flash = new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = Vector2.One },
                MaterialOverride = _flashMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false
            };
            AddChild(_flash);
            _flashLight = new OmniLight3D
            {
                OmniRange = 5f,
                LightEnergy = 2.2f,
                LightColor = new Color(1f, 0.78f, 0.45f),
                ShadowEnabled = false,
                Visible = false
            };
            AddChild(_flashLight);
        }

        public void SetWeapon(WeaponDef def)
        {
            // The flash lives under the old muzzle: rescue it before freeing the old model.
            if (_flash.GetParent() != this) _flash.Reparent(this, false);
            if (_flashLight.GetParent() != this) _flashLight.Reparent(this, false);
            if (_model != null) _model.QueueFree();

            _profile = def.modelProfile ?? "rifle";
            _model = new Node3D { Name = "Model_" + def.id, Position = HipPosition };
            AddChild(_model);

            var mats = GameContext.Materials;
            var metal = mats.Lit(new Color(0.12f, 0.13f, 0.14f), 0.55f, 0.6f);
            var polymer = mats.Lit(new Color(0.17f, 0.17f, 0.16f), 0.75f);
            var furniture = mats.Lit(new Color(0.42f, 0.27f, 0.15f), 0.7f);
            var tan = mats.Lit(new Color(0.55f, 0.48f, 0.36f), 0.75f);
            var glove = mats.Lit(new Color(0.16f, 0.15f, 0.13f), 0.8f);
            var sleeve = mats.Lit(new Color(0.3f, 0.34f, 0.27f), 0.85f);
            var blade = mats.Lit(new Color(0.62f, 0.64f, 0.66f), 0.2f, 0.9f);
            var glass = mats.Lit(new Color(0.15f, 0.25f, 0.35f), 0.05f, 0.2f);

            // Positions below are written with +z = forward and converted in Part().
            float muzzleZ;
            float ejectZ = 0.05f;
            switch (_profile)
            {
                case "knife":
                    Part(new Vector3(0.026f, 0.032f, 0.11f), new Vector3(0f, 0f, 0f), Vector3.Zero, polymer);
                    Part(new Vector3(0.05f, 0.012f, 0.015f), new Vector3(0f, 0f, 0.06f), Vector3.Zero, metal);
                    Part(new Vector3(0.006f, 0.034f, 0.19f), new Vector3(0f, 0.004f, 0.16f), Vector3.Zero, blade);
                    muzzleZ = 0.25f;
                    break;
                case "pistol":
                case "pistol_silenced":
                case "pistol_heavy":
                {
                    bool heavy = _profile == "pistol_heavy";
                    float len = heavy ? 0.25f : 0.19f;
                    Part(new Vector3(heavy ? 0.042f : 0.034f, heavy ? 0.05f : 0.04f, len), new Vector3(0f, 0.035f, 0.04f), Vector3.Zero, metal);
                    Part(new Vector3(0.03f, 0.025f, len * 0.85f), new Vector3(0f, 0.005f, 0.035f), Vector3.Zero, polymer);
                    Part(new Vector3(0.03f, 0.11f, 0.05f), new Vector3(0f, -0.045f, -0.03f), new Vector3(-14f, 0f, 0f), polymer);
                    muzzleZ = 0.04f + len * 0.5f;
                    if (_profile == "pistol_silenced")
                    {
                        Part(new Vector3(0.03f, 0.03f, 0.13f), new Vector3(0f, 0.035f, muzzleZ + 0.065f), Vector3.Zero, metal);
                        muzzleZ += 0.13f;
                    }
                    ejectZ = 0.06f;
                    break;
                }
                case "smg":
                case "smg_large":
                    Part(new Vector3(0.05f, 0.07f, 0.32f), new Vector3(0f, 0.02f, 0.05f), Vector3.Zero, polymer);
                    Part(new Vector3(0.022f, 0.022f, 0.08f), new Vector3(0f, 0.035f, 0.25f), Vector3.Zero, metal);
                    Part(new Vector3(0.03f, 0.1f, 0.045f), new Vector3(0f, -0.05f, -0.03f), new Vector3(-12f, 0f, 0f), polymer);
                    Part(new Vector3(0.03f, 0.05f, 0.16f), new Vector3(0f, 0.01f, -0.18f), Vector3.Zero, polymer);
                    if (_profile == "smg_large")
                        Part(new Vector3(0.06f, 0.05f, 0.26f), new Vector3(0f, 0.08f, 0.05f), Vector3.Zero, tan);
                    else
                        Part(new Vector3(0.028f, 0.15f, 0.04f), new Vector3(0f, -0.07f, 0.1f), new Vector3(8f, 0f, 0f), metal);
                    muzzleZ = 0.3f;
                    break;
                case "shotgun":
                    Part(new Vector3(0.05f, 0.07f, 0.24f), new Vector3(0f, 0.02f, 0f), Vector3.Zero, metal);
                    Part(new Vector3(0.035f, 0.035f, 0.5f), new Vector3(0f, 0.045f, 0.35f), Vector3.Zero, metal);
                    Part(new Vector3(0.05f, 0.045f, 0.18f), new Vector3(0f, 0f, 0.3f), Vector3.Zero, furniture);
                    Part(new Vector3(0.045f, 0.09f, 0.24f), new Vector3(0f, -0.01f, -0.22f), new Vector3(6f, 0f, 0f), furniture);
                    muzzleZ = 0.6f;
                    break;
                case "sniper":
                case "sniper_light":
                {
                    bool light = _profile == "sniper_light";
                    var body = light ? polymer : tan;
                    Part(new Vector3(0.05f, 0.075f, light ? 0.5f : 0.6f), new Vector3(0f, 0.01f, 0.05f), Vector3.Zero, body);
                    Part(new Vector3(0.024f, 0.024f, light ? 0.3f : 0.38f), new Vector3(0f, 0.03f, light ? 0.45f : 0.53f), Vector3.Zero, metal);
                    Part(new Vector3(0.045f, 0.05f, 0.26f), new Vector3(0f, 0.085f, 0.06f), Vector3.Zero, metal);
                    Part(new Vector3(0.04f, 0.04f, 0.005f), new Vector3(0f, 0.085f, 0.193f), Vector3.Zero, glass);
                    Part(new Vector3(0.05f, 0.012f, 0.012f), new Vector3(0.04f, 0.03f, -0.02f), Vector3.Zero, metal);
                    Part(new Vector3(0.045f, 0.1f, 0.22f), new Vector3(0f, -0.01f, -0.26f), Vector3.Zero, body);
                    Part(new Vector3(0.03f, 0.07f, 0.06f), new Vector3(0f, -0.05f, 0.05f), Vector3.Zero, metal);
                    muzzleZ = light ? 0.6f : 0.72f;
                    ejectZ = 0.02f;
                    break;
                }
                case "mg":
                    Part(new Vector3(0.08f, 0.1f, 0.45f), new Vector3(0f, 0.02f, 0.05f), Vector3.Zero, metal);
                    Part(new Vector3(0.03f, 0.03f, 0.42f), new Vector3(0f, 0.04f, 0.48f), Vector3.Zero, metal);
                    Part(new Vector3(0.09f, 0.11f, 0.12f), new Vector3(0f, -0.08f, 0.08f), Vector3.Zero, tan);
                    Part(new Vector3(0.02f, 0.05f, 0.08f), new Vector3(0f, 0.1f, 0.12f), Vector3.Zero, polymer);
                    Part(new Vector3(0.05f, 0.09f, 0.2f), new Vector3(0f, 0f, -0.26f), Vector3.Zero, polymer);
                    muzzleZ = 0.7f;
                    break;
                default:
                {
                    bool scoped = _profile == "rifle_scoped";
                    bool silenced = _profile == "rifle_silenced";
                    Part(new Vector3(0.05f, 0.08f, 0.42f), new Vector3(0f, 0.02f, 0.03f), Vector3.Zero, metal);
                    Part(new Vector3(0.045f, 0.055f, 0.2f), new Vector3(0f, 0.03f, 0.33f), Vector3.Zero, silenced ? polymer : furniture);
                    Part(new Vector3(0.022f, 0.022f, 0.18f), new Vector3(0f, 0.04f, 0.5f), Vector3.Zero, metal);
                    Part(new Vector3(0.032f, 0.16f, 0.07f), new Vector3(0f, -0.08f, 0.12f), new Vector3(16f, 0f, 0f), metal);
                    Part(new Vector3(0.03f, 0.1f, 0.045f), new Vector3(0f, -0.06f, -0.08f), new Vector3(-14f, 0f, 0f), polymer);
                    Part(new Vector3(0.045f, 0.09f, 0.22f), new Vector3(0f, 0f, -0.28f), Vector3.Zero, silenced ? polymer : furniture);
                    muzzleZ = 0.59f;
                    if (scoped)
                    {
                        Part(new Vector3(0.04f, 0.045f, 0.16f), new Vector3(0f, 0.09f, 0.05f), Vector3.Zero, metal);
                        Part(new Vector3(0.034f, 0.034f, 0.004f), new Vector3(0f, 0.09f, 0.132f), Vector3.Zero, glass);
                    }
                    if (silenced)
                    {
                        Part(new Vector3(0.034f, 0.034f, 0.16f), new Vector3(0f, 0.04f, 0.67f), Vector3.Zero, metal);
                        muzzleZ = 0.75f;
                    }
                    break;
                }
            }

            bool isKnife = _profile == "knife";
            bool isPistol = _profile.StartsWith("pistol");
            Part(new Vector3(0.06f, 0.07f, 0.09f), new Vector3(0f, -0.05f, isKnife ? -0.02f : -0.06f), Vector3.Zero, glove);
            Part(new Vector3(0.08f, 0.08f, 0.3f), new Vector3(0.05f, -0.09f, isKnife ? -0.2f : -0.24f), new Vector3(10f, -12f, 0f), sleeve);
            if (!isKnife && !isPistol)
            {
                float hz = Mathf.Min(0.32f, muzzleZ * 0.55f);
                Part(new Vector3(0.06f, 0.06f, 0.09f), new Vector3(-0.01f, -0.02f, hz), Vector3.Zero, glove);
                Part(new Vector3(0.08f, 0.08f, 0.32f), new Vector3(-0.12f, -0.09f, hz - 0.17f), new Vector3(8f, 28f, 0f), sleeve);
            }
            else if (isPistol)
            {
                Part(new Vector3(0.06f, 0.07f, 0.08f), new Vector3(-0.035f, -0.055f, -0.03f), Vector3.Zero, glove);
            }

            _muzzle = new Node3D { Name = "Muzzle", Position = new Vector3(0f, 0.04f, -muzzleZ) };
            _model.AddChild(_muzzle);
            _eject = new Node3D { Name = "Eject", Position = new Vector3(0.03f, 0.05f, -ejectZ) };
            _model.AddChild(_eject);
            _flash.Reparent(_muzzle, false);
            _flash.Position = new Vector3(0f, 0f, -0.03f);
            _flashLight.Reparent(_muzzle, false);
            _flashLight.Position = new Vector3(0f, 0f, -0.1f);

            _inspectT = -1f;
            _swingT = -1f;
        }

        /// <summary>Adds a box. Position/rotation are given with +z forward and mirrored into Godot's -z forward.</summary>
        private void Part(Vector3 size, Vector3 pos, Vector3 eulerDeg, Material material)
        {
            var m = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = size },
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(pos.X, pos.Y, -pos.Z),
                RotationDegrees = new Vector3(-eulerDeg.X, -eulerDeg.Y, eulerDeg.Z)
            };
            _model.AddChild(m);
        }

        public void OnDeploy(float duration)
        {
            _deployT = 0f;
            _deployDuration = Mathf.Max(0.1f, duration);
            _inspectT = -1f;
        }

        public void OnFire(float kickScale, bool silenced)
        {
            _kickPos += new Vector3(0f, 0.006f, 0.035f) * kickScale;
            _kickRot += new Vector3(4.5f * kickScale, (float)GD.RandRange(-1.0, 1.0) * kickScale, (float)GD.RandRange(-1.5, 1.5) * kickScale);
            _inspectT = -1f;
            if (!silenced)
            {
                _flashTimer = 0.045f;
                float s = (float)GD.RandRange(0.11, 0.16);
                _flash.Scale = new Vector3(s, s, s);
                _flash.Rotation = new Vector3(0f, 0f, (float)GD.RandRange(0.0, Mathf.Tau));
            }
        }

        public void OnSwing(bool heavy)
        {
            _swingT = 0f;
            _kickRot += new Vector3(heavy ? 12f : 0f, heavy ? 0f : 30f, 0f);
        }

        public void StartInspect()
        {
            if (_deployT < 1f) return;
            _inspectT = 0f;
        }

        public void Animate(float dt, Vector3 velocity, bool grounded, float lookYawDelta, float lookPitchDelta,
            float reloadProgress, bool hidden, bool aimDownSights)
        {
            if (_model == null) return;
            _model.Visible = !hidden;

            float recover = 1f - Mathf.Exp(-dt * 14f);
            _kickPos = _kickPos.Lerp(Vector3.Zero, recover);
            _kickRot = _kickRot.Lerp(Vector3.Zero, recover);

            float speed = new Vector2(velocity.X, velocity.Z).Length();
            if (grounded && speed > 0.3f) _bobPhase += dt * Mathf.Lerp(6f, 11f, Mathf.Clamp(speed / 6.3f, 0f, 1f));
            float bobAmount = grounded ? Mathf.Clamp(speed / 6.3f, 0f, 1f) : 0f;
            Vector3 bob = new Vector3(Mathf.Sin(_bobPhase) * 0.008f, Mathf.Abs(Mathf.Cos(_bobPhase)) * -0.007f, 0f) * bobAmount;

            _sway = _sway.Lerp(new Vector2(-lookYawDelta, -lookPitchDelta) * 0.0018f, 1f - Mathf.Exp(-dt * 10f));
            Vector3 sway = new Vector3(Mathf.Clamp(_sway.X, -0.03f, 0.03f), Mathf.Clamp(_sway.Y, -0.03f, 0.03f), 0f);

            Vector3 deployOffset = Vector3.Zero, deployRot = Vector3.Zero;
            if (_deployT < 1f)
            {
                _deployT = Mathf.Min(1f, _deployT + dt / (_deployDuration * 0.7f));
                float e = 1f - Mathf.Pow(1f - _deployT, 3f);
                deployOffset = new Vector3(0f, -0.25f, 0f) * (1f - e);
                deployRot = new Vector3(-35f, 0f, 0f) * (1f - e);
            }

            Vector3 reloadOffset = Vector3.Zero, reloadRot = Vector3.Zero;
            if (reloadProgress >= 0f)
            {
                float w = Mathf.Sin(Mathf.Clamp(reloadProgress, 0f, 1f) * Mathf.Pi);
                reloadOffset = new Vector3(-0.03f, -0.09f, 0f) * w;
                reloadRot = new Vector3(-18f, 10f, 32f) * w;
            }

            Vector3 inspectOffset = Vector3.Zero, inspectRot = Vector3.Zero;
            if (_inspectT >= 0f)
            {
                _inspectT += dt / 2.6f;
                if (_inspectT >= 1f) _inspectT = -1f;
                else
                {
                    float a = Mathf.Sin(_inspectT * Mathf.Pi);
                    float b = Mathf.Sin(_inspectT * Mathf.Pi * 2f);
                    inspectOffset = new Vector3(-0.08f, 0.03f, -0.02f) * a;
                    inspectRot = new Vector3(10f * b, 55f * a, 35f * a);
                }
            }

            Vector3 swingRot = Vector3.Zero;
            if (_swingT >= 0f)
            {
                _swingT += dt / 0.3f;
                if (_swingT >= 1f) _swingT = -1f;
                else swingRot = new Vector3(-20f, -40f, -25f) * Mathf.Sin(_swingT * Mathf.Pi);
            }

            Vector3 basePos = aimDownSights ? AdsPosition : HipPosition;
            _model.Position = basePos + bob + sway + deployOffset + reloadOffset + inspectOffset + _kickPos;
            _model.RotationDegrees = _kickRot + deployRot + reloadRot + inspectRot + swingRot;

            if (_flashTimer > 0f)
            {
                _flashTimer -= dt;
                bool on = _flashTimer > 0f;
                _flash.Visible = on;
                _flashLight.Visible = on && GameContext.Settings.GraphicsPreset > 0;
            }
            else
            {
                _flash.Visible = false;
                _flashLight.Visible = false;
            }
        }
    }
}
