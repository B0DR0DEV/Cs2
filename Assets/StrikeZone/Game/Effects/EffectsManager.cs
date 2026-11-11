using UnityEngine;

namespace StrikeZone.Game
{
    /// <summary>
    /// Pooled shot effects: tracers, bullet holes, impact puffs and ejected shells.
    /// Fixed-size pools, no runtime allocation after startup — important on mobile where
    /// GC spikes show up as hitches during sprays.
    /// </summary>
    public sealed class EffectsManager : MonoBehaviour
    {
        private const int TracerCount = 24;
        private const int DecalCount = 128;
        private const int PuffCount = 40;
        private const int ShellCount = 24;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private struct Tracer
        {
            public LineRenderer Line;
            public Vector3 Start;
            public Vector3 End;
            public float Distance;
            public float Travelled;
            public bool Active;
        }

        private struct Puff
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public Color Color;
            public float Age;
            public float Life;
            public float Size;
            public Vector3 Velocity;
            public bool Active;
        }

        private struct Shell
        {
            public Rigidbody Body;
            public float Age;
            public bool Clinked;
            public bool Active;
        }

        private Tracer[] _tracers;
        private Transform[] _decals;
        private int _nextDecal;
        private Puff[] _puffs;
        private int _nextPuff;
        private Shell[] _shells;
        private int _nextShell;
        private int _nextTracer;
        private MaterialPropertyBlock _mpb;
        private Camera _camera;
        private AudioManager _audio;

        public bool ShowTracers = true;
        public bool ShowDecals = true;

        public void Initialize(MaterialLibrary materials, Camera cam, AudioManager audio)
        {
            _camera = cam;
            _audio = audio;
            _mpb = new MaterialPropertyBlock();

            Material tracerMat = materials.Sprite("tracer", ProceduralTextures.White());
            Material decalMat = materials.Sprite("bullethole", ProceduralTextures.BulletHole());
            Material puffMat = materials.Sprite("glow", ProceduralTextures.Glow());
            Material shellMat = materials.Lit(new Color(0.78f, 0.6f, 0.25f), 0.7f, 0.9f);

            _tracers = new Tracer[TracerCount];
            for (int i = 0; i < TracerCount; i++)
            {
                var go = new GameObject("Tracer" + i);
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = tracerMat;
                line.positionCount = 2;
                line.startWidth = 0.014f;
                line.endWidth = 0.006f;
                line.startColor = new Color(1f, 0.85f, 0.5f, 0.0f);
                line.endColor = new Color(1f, 0.9f, 0.6f, 0.9f);
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.useWorldSpace = true;
                line.enabled = false;
                _tracers[i].Line = line;
            }

            Mesh quad = MeshFactory.Quad();
            _decals = new Transform[DecalCount];
            for (int i = 0; i < DecalCount; i++)
            {
                var go = new GameObject("Decal" + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = decalMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                _decals[i] = go.transform;
            }

            _puffs = new Puff[PuffCount];
            for (int i = 0; i < PuffCount; i++)
            {
                var go = new GameObject("Puff" + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = puffMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                _puffs[i].Transform = go.transform;
                _puffs[i].Renderer = r;
            }

            Mesh shellMesh = MeshFactory.Box(new Vector3(0.011f, 0.011f, 0.03f));
            _shells = new Shell[ShellCount];
            for (int i = 0; i < ShellCount; i++)
            {
                var go = new GameObject("Shell" + i);
                go.layer = Layers.Debris;
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = shellMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = shellMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(0.011f, 0.011f, 0.03f);
                var body = go.AddComponent<Rigidbody>();
                body.mass = 0.01f;
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                go.SetActive(false);
                _shells[i].Body = body;
            }
        }

        public void SpawnTracer(Vector3 from, Vector3 to)
        {
            if (!ShowTracers || _tracers == null) return;
            float dist = Vector3.Distance(from, to);
            if (dist < 1.5f) return;
            int i = _nextTracer;
            _nextTracer = (_nextTracer + 1) % TracerCount;
            _tracers[i].Start = from;
            _tracers[i].End = to;
            _tracers[i].Distance = dist;
            _tracers[i].Travelled = 0f;
            _tracers[i].Active = true;
            _tracers[i].Line.enabled = true;
            UpdateTracer(ref _tracers[i]);
        }

        public void SpawnDecal(Vector3 point, Vector3 normal, Transform parent)
        {
            if (!ShowDecals || _decals == null) return;
            Transform d = _decals[_nextDecal];
            _nextDecal = (_nextDecal + 1) % DecalCount;
            d.SetParent(parent != null ? parent : transform, true);
            d.position = point + normal * 0.004f;
            d.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            float s = Random.Range(0.045f, 0.065f);
            d.localScale = Vector3.one;
            Vector3 lossy = d.lossyScale;
            d.localScale = new Vector3(s / Mathf.Max(lossy.x, 1e-4f), s / Mathf.Max(lossy.y, 1e-4f), 1f);
            d.gameObject.SetActive(true);
        }

        public void SpawnPuff(Vector3 point, Vector3 normal, Color color, float size, float life)
        {
            if (_puffs == null) return;
            int i = _nextPuff;
            _nextPuff = (_nextPuff + 1) % PuffCount;
            _puffs[i].Active = true;
            _puffs[i].Age = 0f;
            _puffs[i].Life = life;
            _puffs[i].Size = size;
            _puffs[i].Color = color;
            _puffs[i].Velocity = normal * 0.8f + Random.insideUnitSphere * 0.25f;
            _puffs[i].Transform.position = point + normal * 0.02f;
            _puffs[i].Transform.gameObject.SetActive(true);
            UpdatePuff(ref _puffs[i], 0f);
        }

        public void EjectShell(Vector3 position, Vector3 velocity)
        {
            if (_shells == null) return;
            int i = _nextShell;
            _nextShell = (_nextShell + 1) % ShellCount;
            var body = _shells[i].Body;
            body.gameObject.SetActive(true);
            body.position = position;
            body.transform.position = position;
            body.rotation = Random.rotation;
            body.linearVelocity = velocity;
            body.angularVelocity = Random.insideUnitSphere * 25f;
            _shells[i].Age = 0f;
            _shells[i].Clinked = false;
            _shells[i].Active = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_tracers != null)
            {
                for (int i = 0; i < _tracers.Length; i++)
                {
                    if (!_tracers[i].Active) continue;
                    _tracers[i].Travelled += 450f * dt;
                    if (_tracers[i].Travelled >= _tracers[i].Distance)
                    {
                        _tracers[i].Active = false;
                        _tracers[i].Line.enabled = false;
                        continue;
                    }
                    UpdateTracer(ref _tracers[i]);
                }
            }

            if (_puffs != null)
            {
                for (int i = 0; i < _puffs.Length; i++)
                {
                    if (!_puffs[i].Active) continue;
                    _puffs[i].Age += dt;
                    if (_puffs[i].Age >= _puffs[i].Life)
                    {
                        _puffs[i].Active = false;
                        _puffs[i].Transform.gameObject.SetActive(false);
                        continue;
                    }
                    UpdatePuff(ref _puffs[i], dt);
                }
            }

            if (_shells != null)
            {
                for (int i = 0; i < _shells.Length; i++)
                {
                    if (!_shells[i].Active) continue;
                    _shells[i].Age += dt;
                    if (!_shells[i].Clinked && _shells[i].Age > 0.45f)
                    {
                        _shells[i].Clinked = true;
                        if (_audio != null && _audio.Clips != null)
                            _audio.PlayAt(_audio.Clips.GetVariant("shell"), _shells[i].Body.position, 0.5f, Random.Range(0.9f, 1.15f), 1f);
                    }
                    if (_shells[i].Age > 2.5f)
                    {
                        _shells[i].Active = false;
                        _shells[i].Body.gameObject.SetActive(false);
                    }
                }
            }
        }

        private void UpdateTracer(ref Tracer t)
        {
            Vector3 dir = (t.End - t.Start) / t.Distance;
            float head = Mathf.Min(t.Travelled + 4f, t.Distance);
            float tail = t.Travelled;
            t.Line.SetPosition(0, t.Start + dir * tail);
            t.Line.SetPosition(1, t.Start + dir * head);
        }

        private void UpdatePuff(ref Puff p, float dt)
        {
            float k = p.Age / p.Life;
            p.Transform.position += p.Velocity * dt;
            p.Velocity *= 1f - Mathf.Clamp01(dt * 4f);
            float s = p.Size * (0.6f + k * 1.4f);
            p.Transform.localScale = new Vector3(s, s, s);
            if (_camera != null) p.Transform.rotation = Quaternion.LookRotation(_camera.transform.forward);
            Color c = p.Color;
            c.a *= 1f - k;
            _mpb.SetColor(ColorId, c);
            p.Renderer.SetPropertyBlock(_mpb);
        }
    }
}
