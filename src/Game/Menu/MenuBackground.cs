using System.Collections.Generic;
using Godot;

namespace StrikeZone.Game
{
    /// <summary>
    /// 3D backdrop of the main menu: the player's character standing in a moody warehouse
    /// corner with their weapon, rim-lit, with slow idle motion. Party members will stand
    /// beside it once parties go online (phase 6).
    /// </summary>
    public partial class MenuBackground : Node3D
    {
        private Node3D _character;
        private Node3D _torso;
        private Node3D _gun;
        private Camera3D _camera;
        private float _t;
        private Outfit _outfit = Outfit.Attack;

        public override void _Ready()
        {
            var mats = GameContext.Materials;

            var sky = new ProceduralSkyMaterial
            {
                SkyTopColor = new Color(0.04f, 0.05f, 0.07f),
                SkyHorizonColor = new Color(0.12f, 0.14f, 0.18f),
                GroundBottomColor = new Color(0.02f, 0.02f, 0.03f),
                GroundHorizonColor = new Color(0.1f, 0.11f, 0.13f)
            };
            var env = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Sky,
                Sky = new Sky { SkyMaterial = sky },
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.32f, 0.36f, 0.44f),
                AmbientLightEnergy = 0.55f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
                FogEnabled = true,
                FogLightColor = new Color(0.1f, 0.12f, 0.16f),
                FogDensity = 0.06f,
                GlowEnabled = true,
                GlowIntensity = 0.6f
            };
            AddChild(new WorldEnvironment { Environment = env });

            // Floor and backdrop.
            AddBox(new Vector3(0f, -0.05f, -2f), new Vector3(20f, 0.1f, 14f), mats.Surface("concrete"));
            AddBox(new Vector3(0f, 3f, -5.5f), new Vector3(20f, 6f, 0.4f), mats.Surface("brick"));
            AddBox(new Vector3(-3.4f, 0.6f, -3.6f), new Vector3(1.2f, 1.2f, 1.2f), mats.Surface("wood"));
            AddBox(new Vector3(-3.1f, 1.6f, -3.8f), new Vector3(0.8f, 0.8f, 0.8f), mats.Surface("wood"), 14f);
            AddBox(new Vector3(3.8f, 1.3f, -4.2f), new Vector3(2.6f, 2.6f, 1.6f), mats.Surface("metal"));
            AddBox(new Vector3(-1.6f, 0.35f, -4.6f), new Vector3(1.6f, 0.7f, 0.9f), mats.Surface("metal"));

            // Lighting: warm key from the front-left, cool rim from behind, a lamp glow on the wall.
            AddChild(new SpotLight3D
            {
                Position = new Vector3(-2.2f, 3.2f, 2.6f),
                RotationDegrees = new Vector3(-38f, -35f, 0f),
                LightEnergy = 5f,
                LightColor = new Color(1f, 0.86f, 0.68f),
                SpotRange = 9f,
                SpotAngle = 32f,
                ShadowEnabled = true
            });
            AddChild(new OmniLight3D { Position = new Vector3(1.6f, 2.4f, -1.6f), LightEnergy = 2.4f, LightColor = new Color(0.45f, 0.6f, 1f), OmniRange = 5f });
            AddChild(new OmniLight3D { Position = new Vector3(-1.8f, 1.6f, -1.2f), LightEnergy = 1.2f, LightColor = new Color(0.4f, 0.55f, 0.95f), OmniRange = 4f });
            AddChild(new OmniLight3D { Position = new Vector3(2.6f, 4.2f, -5f), LightEnergy = 3f, LightColor = new Color(1f, 0.65f, 0.3f), OmniRange = 6f });

            BuildCharacter();

            _camera = new Camera3D { Fov = 38f, Current = true, Position = new Vector3(-0.35f, 1.3f, 3.6f) };
            AddChild(_camera);
            _camera.LookAt(new Vector3(-0.35f, 1.05f, 0f), Vector3.Up);
        }

        private void AddBox(Vector3 pos, Vector3 size, Material mat, float yawDeg = 0f)
        {
            AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = size },
                MaterialOverride = mat,
                Position = pos,
                RotationDegrees = new Vector3(0f, yawDeg, 0f)
            });
        }

        public void SetOutfit(Outfit outfit)
        {
            if (outfit == _outfit) return;
            _outfit = outfit;
            BuildCharacter();
        }

        private void BuildCharacter()
        {
            _character?.QueueFree();
            var mats = GameContext.Materials;
            _character = new Node3D { Name = "Character", Position = new Vector3(0.75f, 0f, -0.4f), RotationDegrees = new Vector3(0f, 200f, 0f) };
            AddChild(_character);
            _torso = new Node3D { Name = "Torso" };
            _character.AddChild(_torso);
            List<BodyPart> parts = HumanoidModel.Build(_torso, _outfit, mats);
            Color gearColor = _outfit == Outfit.Attack ? new Color(0.25f, 0.22f, 0.16f) : new Color(0.13f, 0.17f, 0.23f);
            HumanoidModel.AddGear(parts[0].Node, "Helmet", new Vector3(0.235f, 0.1f, 0.255f), new Vector3(0f, 0.09f, 0f), gearColor, mats);
            HumanoidModel.AddGear(parts[1].Node, "Vest", new Vector3(0.45f, 0.58f, 0.27f), new Vector3(0f, -0.13f, 0f), gearColor, mats);
            HumanoidModel.AddGear(parts[0].Node, "Visor", new Vector3(0.18f, 0.05f, 0.02f), new Vector3(0f, 0.02f, -0.12f), new Color(0.05f, 0.06f, 0.07f), mats);

            // Arms forward holding a rifle at the low-ready.
            parts[3].Node.RotationDegrees = new Vector3(55f, 0f, -10f);
            parts[3].Node.Position += new Vector3(0.08f, 0.05f, -0.14f);
            parts[4].Node.RotationDegrees = new Vector3(45f, 0f, 12f);
            parts[4].Node.Position += new Vector3(-0.06f, 0.02f, -0.12f);

            _gun = new Node3D { Name = "Rifle", Position = new Vector3(0.02f, 1.05f, -0.34f), RotationDegrees = new Vector3(-18f, 8f, 0f) };
            _torso.AddChild(_gun);
            var metal = mats.Lit(new Color(0.1f, 0.11f, 0.12f), 0.5f, 0.6f);
            var wood = mats.Lit(new Color(0.42f, 0.27f, 0.15f), 0.7f);
            GunPart(new Vector3(0.05f, 0.08f, 0.42f), new Vector3(0f, 0f, 0f), metal);
            GunPart(new Vector3(0.045f, 0.055f, 0.2f), new Vector3(0f, 0.01f, -0.3f), wood);
            GunPart(new Vector3(0.022f, 0.022f, 0.2f), new Vector3(0f, 0.02f, -0.5f), metal);
            GunPart(new Vector3(0.032f, 0.16f, 0.07f), new Vector3(0f, -0.1f, -0.1f), metal);
            GunPart(new Vector3(0.045f, 0.09f, 0.22f), new Vector3(0f, -0.02f, 0.3f), wood);
        }

        private void GunPart(Vector3 size, Vector3 pos, Material mat)
        {
            _gun.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = mat, Position = pos });
        }

        public override void _Process(double delta)
        {
            _t += (float)delta;
            if (_torso != null)
            {
                // Breathing and weight shift.
                _torso.Position = new Vector3(Mathf.Sin(_t * 0.6f) * 0.012f, Mathf.Sin(_t * 1.7f) * 0.006f, 0f);
                _torso.RotationDegrees = new Vector3(0f, Mathf.Sin(_t * 0.45f) * 2.5f, Mathf.Sin(_t * 0.6f) * 0.8f);
            }
            if (_camera != null)
            {
                _camera.Position = new Vector3(-0.35f + Mathf.Sin(_t * 0.2f) * 0.05f, 1.3f + Mathf.Sin(_t * 0.27f) * 0.02f, 3.6f);
            }
        }
    }
}
