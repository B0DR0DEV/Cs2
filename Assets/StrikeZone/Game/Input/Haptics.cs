using UnityEngine;

namespace StrikeZone.Game
{
    /// <summary>
    /// Short haptic pulses. Handheld.Vibrate() is ~0.5 s on Android — far too long for a
    /// button tap — so on device we call VibrationEffect.createOneShot directly.
    /// Any failure (old OS, missing service) silently disables haptics.
    /// </summary>
    public static class Haptics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static bool _initialized;
#endif

        public static void Tap()
        {
            Pulse(12, 60);
        }

        public static void Hit()
        {
            Pulse(25, 140);
        }

        public static void Pulse(long milliseconds, int amplitude)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (!_initialized)
                {
                    _initialized = true;
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    {
                        var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                        if (activity != null) _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    }
                }
                if (_vibrator == null) return;
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                {
                    var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, Mathf.Clamp(amplitude, 1, 255));
                    _vibrator.Call("vibrate", effect);
                }
            }
            catch (System.Exception)
            {
                _vibrator = null;
            }
#endif
        }

        /// <summary>
        /// Never actually vibrates; referencing Handheld.Vibrate makes the Unity build add
        /// the VIBRATE permission to the Android manifest.
        /// </summary>
        public static void EnsurePermissionIsRequested()
        {
            if (Time.frameCount < 0) Handheld.Vibrate();
        }
    }
}
