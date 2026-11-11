using Godot;

namespace StrikeZone.Game
{
    /// <summary>
    /// Pooled shot effects: tracers, bullet holes, impact puffs and ejected shells.
    /// Fixed pools, no allocation during play — GC spikes show up as hitches on mobile.
    /// </summary>
    public partial class EffectsManager : Node3D
    {
        private const int TracerCount = 24;
        private const int DecalCount = 128;
        private const int PuffCount = 40;
        private const int ShellCount = 24;

        private sealed class Tracer
        {
            public MeshInstance3D Mesh;
            public Vector3 Start;
            public Vector3 Dir;
            public float Distance;
            public float Travelled;
            public bool Active;
        }

        private sealed class Puff
        {
            public MeshInstance3D Mesh;
            public StandardMaterial3D Material;
            public Color Color;
            public Vector3 Velocity;
            public float Age;
            public float Life;
            public float Size;
            public bool Active;
        }

        private sealed class Shell
        {
            public RigidBody3D Body;
            public float Age;
            public bool Clinked;
            public bool Active;
        }

        private Tracer[] _tracers;
        private MeshInstance3D[] _decals;
        private Puff[] _puffs;
        private Shell[] _shells;
        private int _nextTracer, _nextDecal, _nextPuff, _nextShell;

        public bool ShowTracers = true;

        public override void _Ready()
        {
            var materials = GameContext.Materials;
            var tracerMat = materials.Unlit("tracer", new Color(1f, 0.85f, 0.5f, 0.85f), null, false, true);
            var tracerMesh = new BoxMesh { Size = new Vector3(0.012f, 0.012f, 1f) };
            _tracers = new Tracer[TracerCount];
            for (int i = 0; i < TracerCount; i++)
            {
                var m = new MeshInstance3D { Mesh = tracerMesh, MaterialOverride = tracerMat, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                AddChild(m);
                _tracers[i] = new Tracer { Mesh = m };
            }

            var decalMat = new StandardMaterial3D
            {
                AlbedoTexture = ProceduralTextures.BulletHole(),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 1f
            };
            var quad = new QuadMesh { Size = Vector2.One };
            _decals = new MeshInstance3D[DecalCount];
            for (int i = 0; i < DecalCount; i++)
            {
                var m = new MeshInstance3D { Mesh = quad, MaterialOverride = decalMat, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                AddChild(m);
                _decals[i] = m;
            }

            var glow = ProceduralTextures.Glow();
            _puffs = new Puff[PuffCount];
            for (int i = 0; i < PuffCount; i++)
            {
                var mat = MaterialLibrary.NewUnlit(Colors.White, glow, true, false);
                var m = new MeshInstance3D { Mesh = quad, MaterialOverride = mat, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                AddChild(m);
                _puffs[i] = new Puff { Mesh = m, Material = mat };
            }

            var shellSize = new Vector3(0.011f, 0.011f, 0.03f);
            var shellMesh = new BoxMesh { Size = shellSize };
            var shellMat = materials.Lit(new Color(0.78f, 0.6f, 0.25f), 0.35f, 0.9f);
            _shells = new Shell[ShellCount];
            for (int i = 0; i < ShellCount; i++)
            {
                var body = new RigidBody3D
                {
                    Mass = 0.01f,
                    CollisionLayer = Layers.Debris,
                    CollisionMask = Layers.World,
                    Freeze = true,
                    Visible = false
                };
                body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = shellSize } });
                body.AddChild(new MeshInstance3D { Mesh = shellMesh, MaterialOverride = shellMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                AddChild(body);
                _shells[i] = new Shell { Body = body };
            }
        }

        public void SpawnTracer(Vector3 from, Vector3 to)
        {
            if (!ShowTracers || _tracers == null) return;
            float dist = from.DistanceTo(to);
            if (dist < 1.5f) return;
            var t = _tracers[_nextTracer];
            _nextTracer = (_nextTracer + 1) % TracerCount;
            t.Start = from;
            t.Dir = (to - from) / dist;
            t.Distance = dist;
            t.Travelled = 0f;
            t.Active = true;
            t.Mesh.Visible = true;
            UpdateTracer(t);
        }

        public void SpawnDecal(Vector3 point, Vector3 normal)
        {
            if (_decals == null) return;
            var d = _decals[_nextDecal];
            _nextDecal = (_nextDecal + 1) % DecalCount;
            Vector3 up = Mathf.Abs(normal.Y) > 0.95f ? Vector3.Right : Vector3.Up;
            // QuadMesh faces +Z: point +Z along the surface normal.
            Basis basis = Basis.LookingAt(-normal, up);
            float roll = (float)GD.RandRange(0.0, Mathf.Tau);
            basis = basis * new Basis(Vector3.Back, roll);
            float s = (float)GD.RandRange(0.045, 0.065);
            d.Transform = new Transform3D(basis.Scaled(new Vector3(s, s, s)), point + normal * 0.004f);
            d.Visible = true;
        }

        public void SpawnPuff(Vector3 point, Vector3 normal, Color color, float size, float life)
        {
            if (_puffs == null) return;
            var p = _puffs[_nextPuff];
            _nextPuff = (_nextPuff + 1) % PuffCount;
            p.Active = true;
            p.Age = 0f;
            p.Life = life;
            p.Size = size;
            p.Color = color;
            p.Velocity = normal * 0.8f + new Vector3((float)GD.RandRange(-0.25, 0.25), (float)GD.RandRange(-0.25, 0.25), (float)GD.RandRange(-0.25, 0.25));
            p.Mesh.Position = point + normal * 0.02f;
            p.Mesh.Visible = true;
            UpdatePuff(p, 0f);
        }

        public void EjectShell(Vector3 position, Vector3 velocity)
        {
            if (_shells == null) return;
            var s = _shells[_nextShell];
            _nextShell = (_nextShell + 1) % ShellCount;
            s.Body.Freeze = false;
            s.Body.Visible = true;
            s.Body.GlobalPosition = position;
            s.Body.Rotation = new Vector3((float)GD.RandRange(0.0, Mathf.Tau), (float)GD.RandRange(0.0, Mathf.Tau), 0f);
            s.Body.LinearVelocity = velocity;
            s.Body.AngularVelocity = new Vector3((float)GD.RandRange(-25.0, 25.0), (float)GD.RandRange(-25.0, 25.0), (float)GD.RandRange(-25.0, 25.0));
            s.Age = 0f;
            s.Clinked = false;
            s.Active = true;
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            foreach (var t in _tracers)
            {
                if (!t.Active) continue;
                t.Travelled += 450f * dt;
                if (t.Travelled >= t.Distance)
                {
                    t.Active = false;
                    t.Mesh.Visible = false;
                    continue;
                }
                UpdateTracer(t);
            }

            foreach (var p in _puffs)
            {
                if (!p.Active) continue;
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Active = false;
                    p.Mesh.Visible = false;
                    continue;
                }
                UpdatePuff(p, dt);
            }

            foreach (var s in _shells)
            {
                if (!s.Active) continue;
                s.Age += dt;
                if (!s.Clinked && s.Age > 0.45f)
                {
                    s.Clinked = true;
                    GameContext.Audio?.PlayAt(GameContext.Sounds.Pick("shell"), s.Body.GlobalPosition, 0.5f, (float)GD.RandRange(0.9, 1.15), 1.2f);
                }
                if (s.Age > 2.5f)
                {
                    s.Active = false;
                    s.Body.Visible = false;
                    s.Body.Freeze = true;
                }
            }
        }

        private static void UpdateTracer(Tracer t)
        {
            float head = Mathf.Min(t.Travelled + 4f, t.Distance);
            float tail = t.Travelled;
            float len = Mathf.Max(head - tail, 0.01f);
            Vector3 mid = t.Start + t.Dir * ((head + tail) * 0.5f);
            Vector3 up = Mathf.Abs(t.Dir.Y) > 0.95f ? Vector3.Right : Vector3.Up;
            Basis look = Basis.LookingAt(t.Dir, up);
            // Scale the local Z column only (stretch along the tracer's own axis).
            Basis basis = new Basis(look.X, look.Y, look.Z * len);
            t.Mesh.Transform = new Transform3D(basis, mid);
        }

        private static void UpdatePuff(Puff p, float dt)
        {
            float k = p.Age / p.Life;
            p.Mesh.Position += p.Velocity * dt;
            p.Velocity *= 1f - Mathf.Clamp(dt * 4f, 0f, 1f);
            float s = p.Size * (0.6f + k * 1.4f);
            p.Mesh.Scale = new Vector3(s, s, s);
            Color c = p.Color;
            c.A *= 1f - k;
            p.Material.AlbedoColor = c;
        }
    }
}
