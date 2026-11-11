using StrikeZone.Core;
using UnityEngine;

namespace StrikeZone.Game
{
    /// <summary>
    /// First-person hands and weapon, built from boxes per model profile, with procedural
    /// animation: idle sway, movement bob, firing kick, reload, deploy and inspect.
    /// Phase 9 swaps the boxes for real meshes and animation clips; the animation hooks
    /// (OnFire, OnDeploy, reload progress, inspect) stay the same.
    /// </summary>
    public sealed class ViewModel : MonoBehaviour
    {
        private static readonly Vector3 HipPosition = new Vector3(0.16f, -0.17f, 0.3f);
        private static readonly Vector3 CenterPosition = new Vector3(0f, -0.1f, 0.24f);

        private Transform _modelRoot;
        private Transform _muzzle;
        private Transform _ejectPort;
        private Transform _flash;
        private MeshRenderer _flashRenderer;
        private Light _flashLight;
        private string _profile;

        private Vector3 _kickPos;
        private Vector3 _kickRot;
        private float _bobPhase;
        private Vector2 _sway;
        private float _deployT = 1f;
        private float _deployDuration = 1f;
        private float _inspectT = -1f;
        private float _flashTimer;
        private float _swingT = -1f;

        public Transform Muzzle
        {
            get { return _muzzle; }
        }

        public Transform EjectPort
        {
            get { return _ejectPort; }
        }

        public void Initialize(MaterialLibrary materials)
        {
            var flashGo = new GameObject("MuzzleFlash");
            flashGo.transform.SetParent(transform, false);
            flashGo.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Quad();
            _flashRenderer = flashGo.AddComponent<MeshRenderer>();
            _flashRenderer.sharedMaterial = materials.Sprite("muzzle", ProceduralTextures.Glow());
            _flashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _flashRenderer.enabled = false;
            _flash = flashGo.transform;

            var lightGo = new GameObject("FlashLight");
            lightGo.transform.SetParent(transform, false);
            _flashLight = lightGo.AddComponent<Light>();
            _flashLight.type = LightType.Point;
            _flashLight.range = 5f;
            _flashLight.intensity = 2.2f;
            _flashLight.color = new Color(1f, 0.78f, 0.45f);
            _flashLight.shadows = LightShadows.None;
            _flashLight.enabled = false;
        }

        public void SetWeapon(WeaponDef def, MaterialLibrary materials)
        {
            // The flash and its light live under the old muzzle: rescue them before destroying the old model.
            if (_flash != null) _flash.SetParent(transform, false);
            if (_flashLight != null) _flashLight.transform.SetParent(transform, false);
            if (_modelRoot != null) Destroy(_modelRoot.gameObject);
            _profile = def.modelProfile;
            _modelRoot = new GameObject("Model_" + def.id).transform;
            _modelRoot.SetParent(transform, false);
            _modelRoot.localPosition = HipPosition;

            Material metal = materials.Lit(new Color(0.12f, 0.13f, 0.14f), 0.45f, 0.6f);
            Material polymer = materials.Lit(new Color(0.17f, 0.17f, 0.16f), 0.25f);
            Material furniture = materials.Lit(new Color(0.42f, 0.27f, 0.15f), 0.3f);
            Material tan = materials.Lit(new Color(0.55f, 0.48f, 0.36f), 0.25f);
            Material glove = materials.Lit(new Color(0.16f, 0.15f, 0.13f), 0.2f);
            Material sleeve = materials.Lit(new Color(0.3f, 0.34f, 0.27f), 0.15f);
            Material blade = materials.Lit(new Color(0.62f, 0.64f, 0.66f), 0.8f, 0.9f);
            Material glass = materials.Lit(new Color(0.15f, 0.25f, 0.35f), 0.95f, 0.2f);

            float muzzleZ;
            float ejectZ = 0.05f;
            switch (_profile)
            {
                case "knife":
                    Part("Handle", new Vector3(0.026f, 0.032f, 0.11f), new Vector3(0f, 0f, 0f), Vector3.zero, polymer);
                    Part("Guard", new Vector3(0.05f, 0.012f, 0.015f), new Vector3(0f, 0f, 0.06f), Vector3.zero, metal);
                    Part("Blade", new Vector3(0.006f, 0.034f, 0.19f), new Vector3(0f, 0.004f, 0.16f), Vector3.zero, blade);
                    muzzleZ = 0.25f;
                    break;
                case "pistol":
                case "pistol_silenced":
                case "pistol_heavy":
                    bool heavy = _profile == "pistol_heavy";
                    float len = heavy ? 0.25f : 0.19f;
                    Part("Slide", new Vector3(heavy ? 0.042f : 0.034f, heavy ? 0.05f : 0.04f, len), new Vector3(0f, 0.035f, 0.04f), Vector3.zero, metal);
                    Part("Frame", new Vector3(0.03f, 0.025f, len * 0.85f), new Vector3(0f, 0.005f, 0.035f), Vector3.zero, polymer);
                    Part("Grip", new Vector3(0.03f, 0.11f, 0.05f), new Vector3(0f, -0.045f, -0.03f), new Vector3(-14f, 0f, 0f), polymer);
                    muzzleZ = 0.04f + len * 0.5f;
                    if (_profile == "pistol_silenced")
                    {
                        Part("Suppressor", new Vector3(0.03f, 0.03f, 0.13f), new Vector3(0f, 0.035f, muzzleZ + 0.065f), Vector3.zero, metal);
                        muzzleZ += 0.13f;
                    }
                    ejectZ = 0.06f;
                    break;
                case "smg":
                case "smg_large":
                    Part("Receiver", new Vector3(0.05f, 0.07f, 0.32f), new Vector3(0f, 0.02f, 0.05f), Vector3.zero, polymer);
                    Part("Barrel", new Vector3(0.022f, 0.022f, 0.08f), new Vector3(0f, 0.035f, 0.25f), Vector3.zero, metal);
                    Part("Grip", new Vector3(0.03f, 0.1f, 0.045f), new Vector3(0f, -0.05f, -0.03f), new Vector3(-12f, 0f, 0f), polymer);
                    Part("Stock", new Vector3(0.03f, 0.05f, 0.16f), new Vector3(0f, 0.01f, -0.18f), Vector3.zero, polymer);
                    if (_profile == "smg_large")
                        Part("TopMag", new Vector3(0.06f, 0.05f, 0.26f), new Vector3(0f, 0.08f, 0.05f), Vector3.zero, tan);
                    else
                        Part("Mag", new Vector3(0.028f, 0.15f, 0.04f), new Vector3(0f, -0.07f, 0.1f), new Vector3(8f, 0f, 0f), metal);
                    muzzleZ = 0.3f;
                    break;
                case "shotgun":
                    Part("Receiver", new Vector3(0.05f, 0.07f, 0.24f), new Vector3(0f, 0.02f, 0f), Vector3.zero, metal);
                    Part("Barrel", new Vector3(0.035f, 0.035f, 0.5f), new Vector3(0f, 0.045f, 0.35f), Vector3.zero, metal);
                    Part("Pump", new Vector3(0.05f, 0.045f, 0.18f), new Vector3(0f, 0.0f, 0.3f), Vector3.zero, furniture);
                    Part("Stock", new Vector3(0.045f, 0.09f, 0.24f), new Vector3(0f, -0.01f, -0.22f), new Vector3(6f, 0f, 0f), furniture);
                    muzzleZ = 0.6f;
                    break;
                case "sniper":
                case "sniper_light":
                    bool light = _profile == "sniper_light";
                    Part("Body", new Vector3(0.05f, 0.075f, light ? 0.5f : 0.6f), new Vector3(0f, 0.01f, 0.05f), Vector3.zero, light ? polymer : tan);
                    Part("Barrel", new Vector3(0.024f, 0.024f, light ? 0.3f : 0.38f), new Vector3(0f, 0.03f, light ? 0.45f : 0.53f), Vector3.zero, metal);
                    Part("Scope", new Vector3(0.045f, 0.05f, 0.26f), new Vector3(0f, 0.085f, 0.06f), Vector3.zero, metal);
                    Part("Lens", new Vector3(0.04f, 0.04f, 0.005f), new Vector3(0f, 0.085f, 0.193f), Vector3.zero, glass);
                    Part("Bolt", new Vector3(0.05f, 0.012f, 0.012f), new Vector3(0.04f, 0.03f, -0.02f), Vector3.zero, metal);
                    Part("Stock", new Vector3(0.045f, 0.1f, 0.22f), new Vector3(0f, -0.01f, -0.26f), Vector3.zero, light ? polymer : tan);
                    Part("Mag", new Vector3(0.03f, 0.07f, 0.06f), new Vector3(0f, -0.05f, 0.05f), Vector3.zero, metal);
                    muzzleZ = light ? 0.6f : 0.72f;
                    ejectZ = 0.02f;
                    break;
                case "mg":
                    Part("Receiver", new Vector3(0.08f, 0.1f, 0.45f), new Vector3(0f, 0.02f, 0.05f), Vector3.zero, metal);
                    Part("Barrel", new Vector3(0.03f, 0.03f, 0.42f), new Vector3(0f, 0.04f, 0.48f), Vector3.zero, metal);
                    Part("Box", new Vector3(0.09f, 0.11f, 0.12f), new Vector3(0f, -0.08f, 0.08f), Vector3.zero, tan);
                    Part("Handle", new Vector3(0.02f, 0.05f, 0.08f), new Vector3(0f, 0.1f, 0.12f), Vector3.zero, polymer);
                    Part("Stock", new Vector3(0.05f, 0.09f, 0.2f), new Vector3(0f, 0f, -0.26f), Vector3.zero, polymer);
                    muzzleZ = 0.7f;
                    break;
                default: // rifles
                    bool scoped = _profile == "rifle_scoped";
                    bool silenced = _profile == "rifle_silenced";
                    Part("Receiver", new Vector3(0.05f, 0.08f, 0.42f), new Vector3(0f, 0.02f, 0.03f), Vector3.zero, metal);
                    Part("Handguard", new Vector3(0.045f, 0.055f, 0.2f), new Vector3(0f, 0.03f, 0.33f), Vector3.zero, silenced ? polymer : furniture);
                    Part("Barrel", new Vector3(0.022f, 0.022f, 0.18f), new Vector3(0f, 0.04f, 0.5f), Vector3.zero, metal);
                    Part("Mag", new Vector3(0.032f, 0.16f, 0.07f), new Vector3(0f, -0.08f, 0.12f), new Vector3(16f, 0f, 0f), metal);
                    Part("Grip", new Vector3(0.03f, 0.1f, 0.045f), new Vector3(0f, -0.06f, -0.08f), new Vector3(-14f, 0f, 0f), polymer);
                    Part("Stock", new Vector3(0.045f, 0.09f, 0.22f), new Vector3(0f, 0f, -0.28f), Vector3.zero, silenced ? polymer : furniture);
                    muzzleZ = 0.59f;
                    if (scoped)
                    {
                        Part("Scope", new Vector3(0.04f, 0.045f, 0.16f), new Vector3(0f, 0.09f, 0.05f), Vector3.zero, metal);
                        Part("Lens", new Vector3(0.034f, 0.034f, 0.004f), new Vector3(0f, 0.09f, 0.132f), Vector3.zero, glass);
                    }
                    if (silenced)
                    {
                        Part("Suppressor", new Vector3(0.034f, 0.034f, 0.16f), new Vector3(0f, 0.04f, 0.67f), Vector3.zero, metal);
                        muzzleZ = 0.75f;
                    }
                    break;
            }

            // Hands: right hand on the grip, left hand forward.
            bool isKnife = _profile == "knife";
            Part("HandR", new Vector3(0.06f, 0.07f, 0.09f), new Vector3(0.0f, -0.05f, isKnife ? -0.02f : -0.06f), Vector3.zero, glove);
            Part("SleeveR", new Vector3(0.08f, 0.08f, 0.3f), new Vector3(0.05f, -0.09f, isKnife ? -0.2f : -0.24f), new Vector3(10f, -12f, 0f), sleeve);
            if (!isKnife && !_profile.StartsWith("pistol"))
            {
                Part("HandL", new Vector3(0.06f, 0.06f, 0.09f), new Vector3(-0.01f, -0.02f, Mathf.Min(0.32f, muzzleZ * 0.55f)), Vector3.zero, glove);
                Part("SleeveL", new Vector3(0.08f, 0.08f, 0.32f), new Vector3(-0.12f, -0.09f, Mathf.Min(0.32f, muzzleZ * 0.55f) - 0.17f), new Vector3(8f, 28f, 0f), sleeve);
            }
            else if (!isKnife)
            {
                Part("HandL", new Vector3(0.06f, 0.07f, 0.08f), new Vector3(-0.035f, -0.055f, -0.03f), Vector3.zero, glove);
            }

            _muzzle = new GameObject("Muzzle").transform;
            _muzzle.SetParent(_modelRoot, false);
            _muzzle.localPosition = new Vector3(0f, 0.04f, muzzleZ);
            _ejectPort = new GameObject("Eject").transform;
            _ejectPort.SetParent(_modelRoot, false);
            _ejectPort.localPosition = new Vector3(0.03f, 0.05f, ejectZ);

            _flash.SetParent(_muzzle, false);
            _flash.localPosition = new Vector3(0f, 0f, 0.03f);
            _flashLight.transform.SetParent(_muzzle, false);
            _flashLight.transform.localPosition = new Vector3(0f, 0f, 0.1f);

            _inspectT = -1f;
            _swingT = -1f;
        }

        private void Part(string name, Vector3 size, Vector3 position, Vector3 euler, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_modelRoot, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Box(size);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        public void OnDeploy(float duration)
        {
            _deployT = 0f;
            _deployDuration = Mathf.Max(0.1f, duration);
            _inspectT = -1f;
        }

        public void OnFire(float kickScale, bool silenced)
        {
            _kickPos += new Vector3(0f, 0.006f, -0.035f) * kickScale;
            _kickRot += new Vector3(-4.5f * kickScale, Random.Range(-1f, 1f) * kickScale, Random.Range(-1.5f, 1.5f) * kickScale);
            _inspectT = -1f;
            if (!silenced)
            {
                _flashTimer = 0.045f;
                _flash.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                float s = Random.Range(0.11f, 0.16f);
                _flash.localScale = new Vector3(s, s, s);
            }
        }

        public void OnSwing(bool heavy)
        {
            _swingT = 0f;
            _kickRot += new Vector3(heavy ? -12f : 0f, heavy ? 0f : -30f, 0f);
        }

        public void StartInspect()
        {
            if (_deployT < 1f) return;
            _inspectT = 0f;
        }

        /// <summary>Called every frame by the weapon controller.</summary>
        public void Animate(float dt, Vector3 velocity, bool grounded, float lookYawDelta, float lookPitchDelta,
            float reloadProgress, bool scopedHidden, bool aimingDownSights)
        {
            if (_modelRoot == null) return;
            _modelRoot.gameObject.SetActive(!scopedHidden);

            // Kick recovers exponentially (critically damped feel).
            float recover = 1f - Mathf.Exp(-dt * 14f);
            _kickPos = Vector3.Lerp(_kickPos, Vector3.zero, recover);
            _kickRot = Vector3.Lerp(_kickRot, Vector3.zero, recover);

            // Bob while moving on the ground.
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            if (grounded && speed > 0.3f) _bobPhase += dt * Mathf.Lerp(6f, 11f, Mathf.Clamp01(speed / 6.3f));
            float bobAmount = grounded ? Mathf.Clamp01(speed / 6.3f) : 0f;
            Vector3 bob = new Vector3(Mathf.Sin(_bobPhase) * 0.008f, Mathf.Abs(Mathf.Cos(_bobPhase)) * -0.007f, 0f) * bobAmount;

            // Sway lags behind the look direction.
            _sway = Vector2.Lerp(_sway, new Vector2(-lookYawDelta, -lookPitchDelta) * 0.0018f, 1f - Mathf.Exp(-dt * 10f));
            Vector3 sway = new Vector3(Mathf.Clamp(_sway.x, -0.03f, 0.03f), Mathf.Clamp(_sway.y, -0.03f, 0.03f), 0f);

            // Deploy: raise from below.
            Vector3 deployOffset = Vector3.zero;
            Vector3 deployRot = Vector3.zero;
            if (_deployT < 1f)
            {
                _deployT = Mathf.Min(1f, _deployT + dt / (_deployDuration * 0.7f));
                float e = 1f - Mathf.Pow(1f - _deployT, 3f);
                deployOffset = new Vector3(0f, -0.25f, 0f) * (1f - e);
                deployRot = new Vector3(35f, 0f, 0f) * (1f - e);
            }

            // Reload: dip, roll, come back.
            Vector3 reloadOffset = Vector3.zero;
            Vector3 reloadRot = Vector3.zero;
            if (reloadProgress >= 0f)
            {
                float w = Mathf.Sin(Mathf.Clamp01(reloadProgress) * Mathf.PI);
                reloadOffset = new Vector3(-0.03f, -0.09f, 0f) * w;
                reloadRot = new Vector3(18f, -10f, 32f) * w;
            }

            // Inspect: turn the weapon towards the camera and back.
            Vector3 inspectOffset = Vector3.zero;
            Vector3 inspectRot = Vector3.zero;
            if (_inspectT >= 0f)
            {
                _inspectT += dt / 2.6f;
                if (_inspectT >= 1f) _inspectT = -1f;
                else
                {
                    float a = Mathf.Sin(_inspectT * Mathf.PI);
                    float b = Mathf.Sin(_inspectT * Mathf.PI * 2f);
                    inspectOffset = new Vector3(-0.08f, 0.03f, 0.02f) * a;
                    inspectRot = new Vector3(-10f * b, -55f * a, 35f * a);
                }
            }

            // Knife swing arc.
            Vector3 swingRot = Vector3.zero;
            if (_swingT >= 0f)
            {
                _swingT += dt / 0.3f;
                if (_swingT >= 1f) _swingT = -1f;
                else swingRot = new Vector3(20f, 40f, -25f) * Mathf.Sin(_swingT * Mathf.PI);
            }

            Vector3 basePos = aimingDownSights ? CenterPosition : HipPosition;
            _modelRoot.localPosition = basePos + bob + sway + deployOffset + reloadOffset + inspectOffset + _kickPos;
            _modelRoot.localRotation = Quaternion.Euler(_kickRot + deployRot + reloadRot + inspectRot + swingRot);

            if (_flashTimer > 0f)
            {
                _flashTimer -= dt;
                _flashRenderer.enabled = _flashTimer > 0f;
                _flashLight.enabled = _flashTimer > 0f && QualitySettings.GetQualityLevel() > 0;
            }
            else
            {
                _flashRenderer.enabled = false;
                _flashLight.enabled = false;
            }
        }
    }
}
