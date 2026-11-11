using System.Collections.Generic;
using UnityEngine;

namespace StrikeZone.Game
{
    public struct TargetSpawn
    {
        public Vector3 Position;
        public float Yaw;
        public TargetMode Mode;
        public Vector3 StrafeA;
        public Vector3 StrafeB;
    }

    public sealed class MapLayout
    {
        public Vector3 PlayerSpawn;
        public float PlayerYaw;
        public readonly List<TargetSpawn> Targets = new List<TargetSpawn>();
        public Transform StaticRoot;
    }

    /// <summary>
    /// Builds the phase-1 training range ("Полигон") out of grey blocks:
    ///  • firing line with targets at 5–70 m, two strafing lanes, a crouching target;
    ///  • wall-bang section: wood, metal sheet, brick and concrete walls with targets behind;
    ///  • movement course: jump / crouch-jump boxes, stairs, ramp, ladders, a 4 m perch
    ///    and a 10 m tower for fall damage;
    ///  • surface strips (metal, wood, dirt) to compare footstep sounds.
    /// All sizes in metres. 1 m = 39.37 game units.
    /// </summary>
    public sealed class BlockoutMapBuilder
    {
        private readonly MaterialLibrary _materials;
        private Transform _root;

        public BlockoutMapBuilder(MaterialLibrary materials)
        {
            _materials = materials;
        }

        public MapLayout Build(Transform parent)
        {
            var layout = new MapLayout();
            _root = new GameObject("Map_TrainingRange").transform;
            _root.SetParent(parent, false);
            layout.StaticRoot = _root;

            // ---------------------------------------------------------- ground & bounds
            Block("Ground", new Vector3(0f, -0.25f, 35f), new Vector3(90f, 0.5f, 120f), "concrete");
            Block("WallNorth", new Vector3(0f, 3f, 95.5f), new Vector3(90f, 6f, 1f), "concrete");
            Block("WallSouth", new Vector3(0f, 3f, -25.5f), new Vector3(90f, 6f, 1f), "concrete");
            Block("WallEast", new Vector3(45.5f, 3f, 35f), new Vector3(1f, 6f, 122f), "concrete");
            Block("WallWest", new Vector3(-45.5f, 3f, 35f), new Vector3(1f, 6f, 122f), "concrete");

            // ---------------------------------------------------------- spawn & firing line
            layout.PlayerSpawn = new Vector3(0f, 0.05f, -8f);
            layout.PlayerYaw = 0f;
            Block("SpawnPad", new Vector3(0f, 0.02f, -12f), new Vector3(10f, 0.04f, 8f), "wood");
            Block("FiringLine", new Vector3(0f, 0.01f, -0.5f), new Vector3(30f, 0.02f, 0.2f), "metal");
            // Low cover for crouch-peeking practice.
            Block("CoverLowL", new Vector3(-8f, 0.5f, -2.5f), new Vector3(2f, 1f, 1f), "wood");
            Block("CoverHighR", new Vector3(8f, 1.3f, -2.5f), new Vector3(1.2f, 2.6f, 1.2f), "concrete");
            Block("CornerPeek", new Vector3(-14f, 1.5f, -4f), new Vector3(0.6f, 3f, 6f), "brick");

            // Distance markers every 10 m.
            for (int d = 10; d <= 70; d += 10)
                Block("Marker" + d, new Vector3(-16f, 0.15f, d), new Vector3(0.6f, 0.3f, 0.2f), "metal");

            // ---------------------------------------------------------- targets (face the player: yaw 180)
            AddTarget(layout, new Vector3(-5f, 0f, 5f), TargetMode.Static);
            AddTarget(layout, new Vector3(-2f, 0f, 10f), TargetMode.Static);
            AddTarget(layout, new Vector3(-9f, 0f, 15f), TargetMode.Crouching);
            AddTarget(layout, new Vector3(1f, 0f, 20f), TargetMode.Static);
            AddTarget(layout, new Vector3(4f, 0f, 30f), TargetMode.Static);
            AddTarget(layout, new Vector3(7f, 0f, 50f), TargetMode.Static);
            // Long-range target on a platform for snipers.
            Block("SniperPlatform", new Vector3(0f, 1f, 70f), new Vector3(4f, 2f, 3f), "concrete");
            AddTarget(layout, new Vector3(0f, 2f, 70f), TargetMode.Static);

            // Strafing lanes.
            Block("LaneA", new Vector3(-8f, 0.01f, 25f), new Vector3(9f, 0.02f, 1.5f), "dirt");
            AddStrafer(layout, new Vector3(-12f, 0f, 25f), new Vector3(-4f, 0f, 25f));
            Block("LaneB", new Vector3(12f, 0.01f, 40f), new Vector3(9f, 0.02f, 1.5f), "dirt");
            AddStrafer(layout, new Vector3(8f, 0f, 40f), new Vector3(16f, 0f, 40f));

            // ---------------------------------------------------------- wall-bang section (east)
            // Walls are 2.6 m tall with a target 1.5 m behind each one. Thickness chosen so
            // rifles go through wood, metal and brick with decreasing damage, and concrete stops them.
            float wz = 12f;
            Block("PenSign", new Vector3(30f, 0.01f, wz - 3f), new Vector3(24f, 0.02f, 1f), "metal");
            Block("WallWood", new Vector3(21f, 1.3f, wz), new Vector3(3f, 2.6f, 0.1f), "wood");
            AddTarget(layout, new Vector3(21f, 0f, wz + 1.5f), TargetMode.Static);
            Block("WallMetal", new Vector3(27f, 1.3f, wz), new Vector3(3f, 2.6f, 0.04f), "metal");
            AddTarget(layout, new Vector3(27f, 0f, wz + 1.5f), TargetMode.Static);
            Block("WallBrick", new Vector3(33f, 1.3f, wz), new Vector3(3f, 2.6f, 0.12f), "brick");
            AddTarget(layout, new Vector3(33f, 0f, wz + 1.5f), TargetMode.Static);
            Block("WallConcrete", new Vector3(39f, 1.3f, wz), new Vector3(3f, 2.6f, 0.3f), "concrete");
            AddTarget(layout, new Vector3(39f, 0f, wz + 1.5f), TargetMode.Static);
            // Double wood layer: two surfaces.
            Block("WallWoodDouble1", new Vector3(30f, 1.3f, wz + 8f), new Vector3(3f, 2.6f, 0.1f), "wood");
            Block("WallWoodDouble2", new Vector3(30f, 1.3f, wz + 8.6f), new Vector3(3f, 2.6f, 0.1f), "wood");
            AddTarget(layout, new Vector3(30f, 0f, wz + 10f), TargetMode.Static);

            // ---------------------------------------------------------- movement course (west)
            float mx = -30f;
            // Jump heights: 0.45 m step (walk over), 1.2 m (normal jump), 1.55 m (crouch-jump only).
            Block("Step045", new Vector3(mx + 6f, 0.225f, 0f), new Vector3(2f, 0.45f, 2f), "wood");
            Block("Jump120", new Vector3(mx + 6f, 0.6f, 4f), new Vector3(2f, 1.2f, 2f), "wood");
            Block("CrouchJump155", new Vector3(mx + 6f, 0.775f, 8f), new Vector3(2f, 1.55f, 2f), "wood");
            // Gap jumps on top of a row of crates.
            for (int i = 0; i < 5; i++)
                Block("Crate" + i, new Vector3(mx - 4f, 0.6f, 2f + i * 3.4f), new Vector3(1.6f, 1.2f, 1.6f), "wood");

            // Stairs up to a 2 m deck.
            for (int i = 0; i < 8; i++)
            {
                float h = 0.25f * (i + 1);
                Block("Stair" + i, new Vector3(mx + 1f, h * 0.5f, 14.5f + i * 0.4f), new Vector3(2f, h, 0.4f), "concrete");
            }
            Block("Deck", new Vector3(mx + 1f, 1f, 20f), new Vector3(6f, 2f, 5f), "concrete");
            Block("DeckGrate", new Vector3(mx + 1f, 2.01f, 20f), new Vector3(5.6f, 0.02f, 4.6f), "metal");

            // Ramp (rotated block).
            Block("Ramp", new Vector3(mx + 6f, 0.9f, 22f), new Vector3(2.5f, 0.2f, 6f), "concrete", Quaternion.Euler(-17f, 0f, 0f));

            // 4 m perch with a ladder.
            Block("Perch", new Vector3(mx - 6f, 2f, 28f), new Vector3(4f, 4f, 4f), "concrete");
            Block("PerchFloor", new Vector3(mx - 6f, 4.01f, 28f), new Vector3(3.8f, 0.02f, 3.8f), "wood");
            Ladder("PerchLadder", new Vector3(mx - 6f, 0f, 25.85f), 4.1f, 0f);

            // 10 m tower — jump off to test fall damage.
            Block("Tower", new Vector3(mx + 6f, 5f, 34f), new Vector3(3f, 10f, 3f), "brick");
            Block("TowerTop", new Vector3(mx + 6f, 10.01f, 34f), new Vector3(2.8f, 0.02f, 2.8f), "metal");
            Ladder("TowerLadder", new Vector3(mx + 6f, 0f, 32.35f), 10.1f, 0f);

            // Surface strips to compare footsteps.
            Block("StripMetal", new Vector3(-10f, 0.01f, -16f), new Vector3(6f, 0.02f, 3f), "metal");
            Block("StripWood", new Vector3(-3f, 0.01f, -19f), new Vector3(6f, 0.02f, 3f), "wood");
            Block("StripDirt", new Vector3(4f, 0.01f, -19f), new Vector3(6f, 0.02f, 3f), "dirt");
            Block("StripSand", new Vector3(11f, 0.01f, -16f), new Vector3(6f, 0.02f, 3f), "sand");

            return layout;
        }

        private void AddTarget(MapLayout layout, Vector3 position, TargetMode mode)
        {
            layout.Targets.Add(new TargetSpawn { Position = position, Yaw = 180f, Mode = mode, StrafeA = position, StrafeB = position });
        }

        private void AddStrafer(MapLayout layout, Vector3 a, Vector3 b)
        {
            layout.Targets.Add(new TargetSpawn { Position = (a + b) * 0.5f, Yaw = 180f, Mode = TargetMode.Strafing, StrafeA = a, StrafeB = b });
        }

        /// <summary>Creates a static solid block with a collider and a surface tag.</summary>
        public GameObject Block(string name, Vector3 center, Vector3 size, string surface)
        {
            return Block(name, center, size, surface, Quaternion.identity);
        }

        public GameObject Block(string name, Vector3 center, Vector3 size, string surface, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.layer = Layers.World;
            go.isStatic = true;
            go.transform.SetParent(_root, false);
            go.transform.position = center;
            go.transform.rotation = rotation;
            go.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Box(size);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _materials.Surface(surface);
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            go.AddComponent<SurfaceTag>().SurfaceId = surface;
            return go;
        }

        /// <summary>
        /// Ladder: thin visual rails/rungs (no colliders) plus a trigger volume on the Ladder layer.
        /// <paramref name="baseCenter"/> is the bottom centre on the wall face.
        /// </summary>
        private void Ladder(string name, Vector3 baseCenter, float height, float yaw)
        {
            var root = new GameObject(name).transform;
            root.SetParent(_root, false);
            root.position = baseCenter;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);

            Material metal = _materials.Surface("metal");
            Mesh rail = MeshFactory.Box(new Vector3(0.06f, height, 0.06f));
            Mesh rung = MeshFactory.Box(new Vector3(0.6f, 0.04f, 0.04f));
            VisualPart(root, "RailL", rail, metal, new Vector3(-0.32f, height * 0.5f, 0f));
            VisualPart(root, "RailR", rail, metal, new Vector3(0.32f, height * 0.5f, 0f));
            for (float y = 0.3f; y < height; y += 0.3f)
                VisualPart(root, "Rung", rung, metal, new Vector3(0f, y, 0f));

            var trigger = new GameObject("LadderVolume");
            trigger.layer = Layers.Ladder;
            trigger.transform.SetParent(root, false);
            trigger.transform.localPosition = new Vector3(0f, height * 0.5f + 0.1f, -0.35f);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.9f, height + 0.2f, 0.8f);
        }

        private static void VisualPart(Transform parent, string name, Mesh mesh, Material material, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.isStatic = true;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
