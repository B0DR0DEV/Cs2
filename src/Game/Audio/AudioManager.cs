using Godot;

namespace StrikeZone.Game
{
    /// <summary>
    /// Pooled one-shot audio for a match. Positional sounds get a cheap occlusion pass:
    /// if world geometry blocks the line from the listener, the voice is muffled with the
    /// player's built-in low-pass and attenuated — "behind the wall" is audible by ear.
    /// </summary>
    public partial class AudioManager : Node3D
    {
        private const int Voices3D = 24;
        private const int Voices2D = 12;

        private AudioStreamPlayer3D[] _voices3D;
        private ulong[] _started3D;
        private AudioStreamPlayer[] _voices2D;
        private ulong[] _started2D;
        private Node3D _listener;

        public override void _Ready()
        {
            _voices3D = new AudioStreamPlayer3D[Voices3D];
            _started3D = new ulong[Voices3D];
            for (int i = 0; i < Voices3D; i++)
            {
                var p = new AudioStreamPlayer3D
                {
                    AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
                    UnitSize = 3f,
                    MaxDistance = 120f,
                    DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.Disabled,
                    AttenuationFilterCutoffHz = 20500f,
                    AttenuationFilterDb = -6f
                };
                AddChild(p);
                _voices3D[i] = p;
            }
            _voices2D = new AudioStreamPlayer[Voices2D];
            _started2D = new ulong[Voices2D];
            for (int i = 0; i < Voices2D; i++)
            {
                var p = new AudioStreamPlayer();
                AddChild(p);
                _voices2D[i] = p;
            }
        }

        public void SetListener(Node3D listener)
        {
            _listener = listener;
        }

        public void Play2D(AudioStream stream, float volume = 1f, float pitch = 1f)
        {
            if (stream == null || _voices2D == null) return;
            int best = 0;
            ulong oldest = ulong.MaxValue;
            for (int i = 0; i < _voices2D.Length; i++)
            {
                if (!_voices2D[i].Playing) { best = i; oldest = 0; break; }
                if (_started2D[i] < oldest) { oldest = _started2D[i]; best = i; }
            }
            var v = _voices2D[best];
            v.Stream = stream;
            v.VolumeDb = Mathf.LinearToDb(Mathf.Max(volume, 0.0001f));
            v.PitchScale = pitch;
            v.Play();
            _started2D[best] = Time.GetTicksMsec();
        }

        /// <param name="unitSize">How far the sound carries (larger = louder at distance).</param>
        public void PlayAt(AudioStream stream, Vector3 position, float volume = 1f, float pitch = 1f, float unitSize = 3f)
        {
            if (stream == null || _voices3D == null) return;
            int best = 0;
            ulong oldest = ulong.MaxValue;
            for (int i = 0; i < _voices3D.Length; i++)
            {
                if (!_voices3D[i].Playing) { best = i; oldest = 0; break; }
                if (_started3D[i] < oldest) { oldest = _started3D[i]; best = i; }
            }

            bool occluded = false;
            if (_listener != null && IsInsideTree())
            {
                Vector3 from = _listener.GlobalPosition;
                if (from.DistanceTo(position) > 0.6f)
                {
                    var query = PhysicsRayQueryParameters3D.Create(from, position, Layers.World);
                    var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
                    if (hit.Count > 0 && hit["position"].AsVector3().DistanceTo(position) > 0.3f) occluded = true;
                }
            }

            var v = _voices3D[best];
            v.Stream = stream;
            v.GlobalPosition = position;
            v.UnitSize = unitSize;
            v.AttenuationFilterCutoffHz = occluded ? 1200f : 20500f;
            v.VolumeDb = Mathf.LinearToDb(Mathf.Max(occluded ? volume * 0.55f : volume, 0.0001f));
            v.PitchScale = pitch;
            v.Play();
            _started3D[best] = Time.GetTicksMsec();
        }
    }
}
