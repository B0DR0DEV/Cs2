using System.Collections.Generic;
using Godot;
using StrikeZone.Core;

namespace StrikeZone.Game
{
    /// <summary>One body part of a blockout humanoid.</summary>
    public sealed class BodyPart
    {
        public string Name;
        public HitGroup Group;
        public Vector3 Size;
        public Vector3 Stand;
        public Vector3 Crouch;
        public float CrouchScaleY = 1f;
        public Node3D Node;
        public MeshInstance3D Mesh;
    }

    public enum Outfit
    {
        Target,
        Attack,
        Defense
    }

    /// <summary>
    /// Blockout humanoid (1.83 m, front = -Z). Used by practice targets — where every
    /// visible part is also its hitbox — and by the character line-up in the main menu.
    /// </summary>
    public static class HumanoidModel
    {
        public static List<BodyPart> Layout()
        {
            return new List<BodyPart>
            {
                new BodyPart { Name = "Head", Group = HitGroup.Head, Size = new Vector3(0.2f, 0.24f, 0.22f), Stand = new Vector3(0f, 1.67f, 0f), Crouch = new Vector3(0f, 1.21f, -0.06f) },
                new BodyPart { Name = "Chest", Group = HitGroup.Chest, Size = new Vector3(0.42f, 0.34f, 0.24f), Stand = new Vector3(0f, 1.37f, 0f), Crouch = new Vector3(0f, 0.91f, -0.04f) },
                new BodyPart { Name = "Stomach", Group = HitGroup.Stomach, Size = new Vector3(0.36f, 0.26f, 0.22f), Stand = new Vector3(0f, 1.07f, 0f), Crouch = new Vector3(0f, 0.61f, 0f) },
                new BodyPart { Name = "ArmL", Group = HitGroup.LeftArm, Size = new Vector3(0.1f, 0.62f, 0.12f), Stand = new Vector3(-0.27f, 1.24f, 0f), Crouch = new Vector3(-0.27f, 0.78f, -0.05f) },
                new BodyPart { Name = "ArmR", Group = HitGroup.RightArm, Size = new Vector3(0.1f, 0.62f, 0.12f), Stand = new Vector3(0.27f, 1.24f, 0f), Crouch = new Vector3(0.27f, 0.78f, -0.05f) },
                new BodyPart { Name = "LegL", Group = HitGroup.LeftLeg, Size = new Vector3(0.15f, 0.9f, 0.16f), Stand = new Vector3(-0.1f, 0.47f, 0f), Crouch = new Vector3(-0.1f, 0.25f, 0f), CrouchScaleY = 0.52f },
                new BodyPart { Name = "LegR", Group = HitGroup.RightLeg, Size = new Vector3(0.15f, 0.9f, 0.16f), Stand = new Vector3(0.1f, 0.47f, 0f), Crouch = new Vector3(0.1f, 0.25f, 0f), CrouchScaleY = 0.52f }
            };
        }

        /// <summary>Builds the visual parts under <paramref name="body"/>.</summary>
        public static List<BodyPart> Build(Node3D body, Outfit outfit, MaterialLibrary materials, bool crouched = false)
        {
            Color skin = new Color(0.8f, 0.66f, 0.56f);
            Color top, bottom, gear;
            switch (outfit)
            {
                case Outfit.Attack:
                    top = new Color(0.45f, 0.38f, 0.27f);
                    bottom = new Color(0.3f, 0.27f, 0.22f);
                    gear = new Color(0.22f, 0.2f, 0.15f);
                    break;
                case Outfit.Defense:
                    top = new Color(0.22f, 0.3f, 0.42f);
                    bottom = new Color(0.17f, 0.2f, 0.27f);
                    gear = new Color(0.12f, 0.15f, 0.2f);
                    break;
                default:
                    top = new Color(0.34f, 0.41f, 0.5f);
                    bottom = new Color(0.24f, 0.26f, 0.3f);
                    gear = new Color(0.2f, 0.23f, 0.18f);
                    break;
            }

            var parts = Layout();
            foreach (var p in parts)
            {
                if (crouched) p.Size = new Vector3(p.Size.X, p.Size.Y * p.CrouchScaleY, p.Size.Z);
                // Shapes are sized, never scaled: non-uniform scale on collision shapes is unreliable.
                var node = new Node3D { Name = p.Name, Position = crouched ? p.Crouch : p.Stand };
                body.AddChild(node);
                Color c = p.Group == HitGroup.Head ? skin : (p.Group == HitGroup.LeftLeg || p.Group == HitGroup.RightLeg ? bottom : top);
                var mesh = new MeshInstance3D { Mesh = new BoxMesh { Size = p.Size }, MaterialOverride = materials.Lit(c, 0.8f) };
                node.AddChild(mesh);
                p.Node = node;
                p.Mesh = mesh;
            }
            return parts;
        }

        public static MeshInstance3D AddGear(Node3D parent, string name, Vector3 size, Vector3 offset, Color color, MaterialLibrary materials)
        {
            var m = new MeshInstance3D
            {
                Name = name,
                Mesh = new BoxMesh { Size = size },
                MaterialOverride = materials.Lit(color, 0.7f),
                Position = offset
            };
            parent.AddChild(m);
            return m;
        }
    }
}
