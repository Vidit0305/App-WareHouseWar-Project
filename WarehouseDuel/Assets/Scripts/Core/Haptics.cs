using System;
using UnityEngine;

namespace WarehouseWar.Core
{
    /// <summary>
    /// Short vibration pulses for button presses and in-match feedback (Android). Uses
    /// VibrationEffect.createOneShot so a tap is a crisp ~20 ms tick, not the long buzz of Handheld.Vibrate.
    /// Respects the Vibration setting; silently does nothing on devices without a vibrator.
    /// </summary>
    public static class Haptics
    {
        public enum Strength { Light, Medium, Heavy }

        private const float MinInterval = 0.04f;
        private static float lastPulse = -1f;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator;
        private static AndroidJavaClass effects;
        private static bool initialised, available, amplitudeControl;
#endif

        public static void Light() => Pulse(Strength.Light);
        public static void Medium() => Pulse(Strength.Medium);
        public static void Heavy() => Pulse(Strength.Heavy);

        public static void Pulse(Strength strength)
        {
            if (!GameSettings.Vibration) return;
            if (Time.unscaledTime - lastPulse < MinInterval) return;
            lastPulse = Time.unscaledTime;

            long milliseconds = strength == Strength.Light ? 18 : strength == Strength.Medium ? 40 : 90;
            int amplitude = strength == Strength.Light ? 90 : strength == Strength.Medium ? 170 : 255;
#if UNITY_ANDROID && !UNITY_EDITOR
            Vibrate(milliseconds, amplitude);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void Vibrate(long milliseconds, int amplitude)
        {
            try
            {
                if (!initialised)
                {
                    initialised = true;
                    using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    available = vibrator != null && vibrator.Call<bool>("hasVibrator");
                    if (available)
                    {
                        effects = new AndroidJavaClass("android.os.VibrationEffect");
                        amplitudeControl = vibrator.Call<bool>("hasAmplitudeControl");
                    }
                }
                if (!available) return;
                using AndroidJavaObject effect = effects.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitudeControl ? amplitude : -1);
                vibrator.Call("vibrate", effect);
            }
            catch (Exception e)
            {
                available = false;
                Debug.LogWarning($"[Haptics] Vibration unavailable: {e.Message}");
                // Referencing Handheld.Vibrate also makes Unity add the VIBRATE permission to the manifest.
                if (milliseconds >= 80) Handheld.Vibrate();
            }
        }
#endif
    }
}
