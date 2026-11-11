using System.Collections.Generic;
using Godot;

namespace StrikeZone.Game
{
    public struct TargetSpawn
    {
        public Vector3 Position;
        public TargetMode Mode;
        public Vector3 StrafeA;
        public Vector3 StrafeB;
    }

    public sealed class RangeLayout
    {
        public Vector3 PlayerSpawn;
        public float PlayerYaw;
        public readonly List<TargetSpawn> Targets = new List<TargetSpawn>();
    }

    /// <summary>
    /// Builds the training range out of grey blocks (Godot coordinates: metres, -Z is
    /// "downrange"). Sections: firing line with targets at 5–70 m and two strafing lanes;
    /// wall-bang section (wood, sheet metal, brick, concrete, double wood); movement course
    /// (jump / crouch-jump boxes, crate gaps, stairs, ramp, ladders, a 4 m perch, a 10 m tower
    /// for fall damage); surface strips to compare footsteps.
    /// </summary>
    public sealed class TrainingRangeBuilder
    {
        private readonly MaterialLibrary _materials;
        private Node3D _root;

        public TrainingRangeBuilder(MaterialLibrary materials)
        {
            _materials = materials;
        }

        public RangeLayout Build(Node3D parent)
        {
            var layout = new RangeLayout();
            _root = new Node3D { Name = "TrainingRange" };
            parent.AddChild(_root);

            // Ground & bounds. Downrange is -Z.
            Block("Ground", new Vector3(0f, -0.25f, -35f), new Vector3(90f, 0.5f, 120f), "concrete");
            Block("WallFar", new Vector3(0f, 3f, -95.5f), new Vector3(90f, 6f, 1f), "concrete");
            Block("WallBack", new Vector3(0f, 3f, 25.5f), new Vector3(90f, 6f, 1f), "concrete");
            Block("WallEast", new Vector3(45.5f, 3f, -35f), new Vector3(1f, 6f, 122f), "concrete");
            Block("WallWest", new Vector3(-45.5f, 3f, -35f), new Vector3(1f, 6f, 122f), "concrete");

            // Spawn & firing line.
            layout.PlayerSpawn = new Vector3(0f, 0.05f, 8f);
            layout.PlayerYaw = 0f;
            Block("SpawnPad", new Vector3(0f, 0.02f, 12f), new Vector3(10f, 0.04f, 8f), "wood");
            Block("FiringLine", new Vector3(0f, 0.01f, 0.5f), new Vector3(30f, 0.02f, 0.2f), "metal");
            Block("CoverLow", new Vector3(-8f, 0.5f, 2.5f), new Vector3(2f, 1f, 1f), "wood");
            Block("CoverHigh", new Vector3(8f, 1.3f, 2.5f), new Vector3(1.2f, 2.6f, 1.2f), "concrete");
            Block("CornerPeek", new Vector3(-14f, 1.5f, 4f), new Vector3(0.6f, 3f, 6f), "brick");
            for (int d = 10; d <= 70; d += 10)
                Block("Marker" + d, new Vector3(-16f, 0.15f, -d), new Vector3(0.6f, 0.3f, 0.2f), "metal");

            // Targets.
            Static(layout, new Vector3(-5f, 0f, -5f));
            Static(layout, new Vector3(-2f, 0f, -10f));
            layout.Targets.Add(new TargetSpawn { Position = new Vector3(-9f, 0f, -15f), Mode = TargetMode.Crouching });
            Static(layout, new Vector3(1f, 0f, -20f));
            Static(layout, new Vector3(4f, 0f, -30f));
            Static(layout, new Vector3(7f, 0f, -50f));
            Block("SniperPlatform", new Vector3(0f, 1f, -70f), new Vector3(4f, 2f, 3f), "concrete");
            Static(layout, new Vector3(0f, 2f, -70f));

            Block("LaneA", new Vector3(-8f, 0.01f, -25f), new Vector3(9f, 0.02f, 1.5f), "dirt");
            Strafer(layout, new Vector3(-12f, 0f, -25f), new Vector3(-4f, 0f, -25f));
            Block("LaneB", new Vector3(12f, 0.01f, -40f), new Vector3(9f, 0.02f, 1.5f), "dirt");
            Strafer(layout, new Vector3(8f, 0f, -40f), new Vector3(16f, 0f, -40f));

            // Wall-bang section (east).
            float wz = -12f;
            Block("PenStrip", new Vector3(30f, 0.01f, wz + 3f), new Vector3(24f, 0.02f, 1f), "metal");
            Block("WallWood", new Vector3(21f, 1.3f, wz), new Vector3(3f, 2.6f, 0.1f), "wood");
            Static(layout, new Vector3(21f, 0f, wz - 1.5f));
            Block("WallMetal", new Vector3(27f, 1.3f, wz), new Vector3(3f, 2.6f, 0.04f), "metal");
            Static(layout, new Vector3(27f, 0f, wz - 1.5f));
            Block("WallBrick", new Vector3(33f, 1.3f, wz), new Vector3(3f, 2.6f, 0.12f), "brick");
            Static(layout, new Vector3(33f, 0f, wz - 1.5f));
            Block("WallConcrete", new Vector3(39f, 1.3f, wz), new Vector3(3f, 2.6f, 0.3f), "concrete");
            Static(layout, new Vector3(39f, 0f, wz - 1.5f));
            Block("WallWoodDouble1", new Vector3(30f, 1.3f, wz - 8f), new Vector3(3f, 2.6f, 0.1f), "wood");
            Block("WallWoodDouble2", new Vector3(30f, 1.3f, wz - 8.6f), new Vector3(3f, 2.6f, 0.1f), "wood");
            Static(layout, new Vector3(30f, 0f, wz - 10f));

            // Movement course (west).
            float mx = -30f;
            Block("Jump120", new Vector3(mx + 6f, 0.6f, -4f), new Vector3(2f, 1.2f, 2f), "wood");
            Block("CrouchJump155", new Vector3(mx + 6f, 0.775f, -8f), new Vector3(2f, 1.55f, 2f), "wood");
            for (int i = 0; i < 5; i++)
                Block("Crate" + i, new Vector3(mx - 4f, 0.6f, -2f - i * 3.4f), new Vector3(1.6f, 1.2f, 1.6f), "wood");

            // Stairs: visual steps, but a single sloped collider (Godot bodies do not auto-step).
            for (int i = 0; i < 8; i++)
            {
                float h = 0.25f * (i + 1);
                Visual("Stair" + i, new Vector3(mx + 1f, h * 0.5f, -14.5f - i * 0.4f), new Vector3(2f, h, 0.4f), "concrete");
            }
            float stairAngle = Mathf.Atan2(2f, 3.2f);
            Collider("StairRamp", new Vector3(mx + 1f, 1f - 0.05f, -15.9f), new Vector3(2f, 0.1f, Mathf.Sqrt(2f * 2f + 3.2f * 3.2f)),
                new Vector3(stairAngle, 0f, 0f), "concrete");
            Block("Deck", new Vector3(mx + 1f, 1f, -20f), new Vector3(6f, 2f, 5f), "concrete");
            Block("DeckGrate", new Vector3(mx + 1f, 2.01f, -20f), new Vector3(5.6f, 0.02f, 4.6f), "metal");

            Block("Ramp", new Vector3(mx + 6f, 0.9f, -22f), new Vector3(2.5f, 0.2f, 6f), "concrete", new Vector3(Mathf.DegToRad(17f), 0f, 0f));

            Block("Perch", new Vector3(mx - 6f, 2f, -28f), new Vector3(4f, 4f, 4f), "concrete");
            Block("PerchFloor", new Vector3(mx - 6f, 4.01f, -28f), new Vector3(3.8f, 0.02f, 3.8f), "wood");
            Ladder(new Vector3(mx - 6f, 0f, -25.85f), 4.1f);

            Block("Tower", new Vector3(mx + 6f, 5f, -34f), new Vector3(3f, 10f, 3f), "brick");
            Block("TowerTop", new Vector3(mx + 6f, 10.01f, -34f), new Vector3(2.8f, 0.02f, 2.8f), "metal");
            Ladder(new Vector3(mx + 6f, 0f, -32.35f), 10.1f);

            // Surface strips behind spawn.
            Block("StripMetal", new Vector3(-10f, 0.01f, 16f), new Vector3(6f, 0.02f, 3f), "metal");
            Block("StripWood", new Vector3(-3f, 0.01f, 19f), new Vector3(6f, 0.02f, 3f), "wood");
            Block("StripDirt", new Vector3(4f, 0.01f, 19f), new Vector3(6f, 0.02f, 3f), "dirt");
            Block("StripSand", new Vector3(11f, 0.01f, 16f), new Vector3(6f, 0.02f, 3f), "sand");

            return layout;
        }

        private static void Static(RangeLayout layout, Vector3 position)
        {
            layout.Targets.Add(new TargetSpawn { Position = position, Mode = TargetMode.Static });
        }

        private static void Strafer(RangeLayout layout, Vector3 a, Vector3 b)
        {
            layout.Targets.Add(new TargetSpawn { Position = (a + b) * 0.5f, Mode = TargetMode.Strafing, StrafeA = a, StrafeB = b });
        }

        /// <summary>Solid block: mesh + collider + surface tag (metadata "surface").</summary>
        public StaticBody3D Block(string name, Vector3 center, Vector3 size, string surface, Vector3? rotation = null)
        {
            var body = Collider(name, center, size, rotation ?? Vector3.Zero, surface);
            var mesh = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = size },
                MaterialOverride = _materials.Surface(surface)
            };
            body.AddChild(mesh);
            return body;
        }

        private StaticBody3D Collider(string name, Vector3 center, Vector3 size, Vector3 rotation, string surface)
        {
            var body = new StaticBody3D
            {
                Name = name,
                Position = center,
                Rotation = rotation,
                CollisionLayer = Layers.World,
                CollisionMask = 0
            };
            body.SetMeta("surface", surface);
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
            _root.AddChild(body);
            return body;
        }

        private void Visual(string name, Vector3 center, Vector3 size, string surface)
        {
            _root.AddChild(new MeshInstance3D
            {
                Name = name,
                Position = center,
                Mesh = new BoxMesh { Size = size },
                MaterialOverride = _materials.Surface(surface)
            });
        }

        /// <summary>
        /// Ladder against a wall facing +Z: thin rails and rungs (visual only) plus a trigger
        /// volume on the Ladder layer in front of it.
        /// </summary>
        private void Ladder(Vector3 baseCenter, float height)
        {
            var root = new Node3D { Name = "Ladder", Position = baseCenter };
            _root.AddChild(root);
            var metal = _materials.Surface("metal");
            var rail = new BoxMesh { Size = new Vector3(0.06f, height, 0.06f) };
            var rung = new BoxMesh { Size = new Vector3(0.6f, 0.04f, 0.04f) };
            root.AddChild(new MeshInstance3D { Mesh = rail, MaterialOverride = metal, Position = new Vector3(-0.32f, height * 0.5f, 0f) });
            root.AddChild(new MeshInstance3D { Mesh = rail, MaterialOverride = metal, Position = new Vector3(0.32f, height * 0.5f, 0f) });
            for (float y = 0.3f; y < height; y += 0.3f)
                root.AddChild(new MeshInstance3D { Mesh = rung, MaterialOverride = metal, Position = new Vector3(0f, y, 0f) });

            var volume = new Area3D
            {
                Name = "LadderVolume",
                CollisionLayer = Layers.Ladder,
                CollisionMask = 0,
                Monitoring = false,
                Monitorable = true,
                Position = new Vector3(0f, height * 0.5f + 0.1f, 0.35f)
            };
            volume.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.9f, height + 0.2f, 0.8f) } });
            root.AddChild(volume);
        }
    }
}
