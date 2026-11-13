using System;
using System.IO;
using UnityEngine;

namespace WarehouseDuel.Profile
{
    /// <summary>
    /// Lets the player choose a profile photo from the device.
    /// Android: the system picker (no storage permission needed) via Plugins/Android/WDAvatarPicker.java,
    /// which also fixes EXIF rotation, centre-crops and scales the photo. Editor: a file dialog.
    /// The result is a square texture of <see cref="Size"/> pixels.
    /// </summary>
    public sealed class AvatarPicker : MonoBehaviour
    {
        public const int Size = 256;
        private const string ReceiverName = "WD_AvatarPickerReceiver";

        private static AvatarPicker receiver;
        private static Action<Texture2D, string> pending;

        /// <summary>Opens the picker. <paramref name="done"/> gets the texture, or null and an error message (empty when cancelled).</summary>
        public static void Pick(Action<Texture2D, string> done)
        {
            pending = done;
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Choose a profile picture", "", "png,jpg,jpeg");
            if (string.IsNullOrEmpty(path)) Finish(null, string.Empty);
            else LoadFromFile(path);
#elif UNITY_ANDROID
            EnsureReceiver();
            try
            {
                string output = Path.Combine(Application.temporaryCachePath, "avatar_pick.png");
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var picker = new AndroidJavaClass("com.warehouseduel.profile.WDAvatarPicker");
                picker.CallStatic("pick", activity, ReceiverName, nameof(OnPicked), output, Size);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AvatarPicker] {e}");
                Finish(null, "Could not open the photo picker on this device.");
            }
#else
            Finish(null, "Choosing a photo is not supported on this platform.");
#endif
        }

        private static void EnsureReceiver()
        {
            if (receiver != null) return;
            var go = new GameObject(ReceiverName);
            DontDestroyOnLoad(go);
            receiver = go.AddComponent<AvatarPicker>();
        }

        /// <summary>Called from Java with the output path, "" when cancelled, or "error:..." on failure.</summary>
        public void OnPicked(string result)
        {
            if (string.IsNullOrEmpty(result)) Finish(null, string.Empty);
            else if (result.StartsWith("error", StringComparison.Ordinal)) Finish(null, "That picture could not be opened. Try a JPG or PNG photo.");
            else LoadFromFile(result);
        }

        private static void LoadFromFile(string path)
        {
            try
            {
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!source.LoadImage(File.ReadAllBytes(path)))
                {
                    Finish(null, "That picture could not be opened. Try a JPG or PNG photo.");
                    return;
                }
                Texture2D square = CropSquare(source, Size);
                Destroy(source);
                Finish(square, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AvatarPicker] {e.Message}");
                Finish(null, "That picture could not be opened. Try a JPG or PNG photo.");
            }
        }

        /// <summary>Centre-crops to a square and scales to <paramref name="size"/> on the GPU.</summary>
        public static Texture2D CropSquare(Texture2D source, int size)
        {
            float w = source.width, h = source.height;
            Vector2 scale = w > h ? new Vector2(h / w, 1f) : new Vector2(1f, w / h);
            Vector2 offset = new Vector2((1f - scale.x) / 2f, (1f - scale.y) / 2f);

            RenderTexture rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            source.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, rt, scale, offset);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var result = new Texture2D(size, size, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        private static void Finish(Texture2D texture, string error)
        {
            Action<Texture2D, string> callback = pending;
            pending = null;
            callback?.Invoke(texture, error);
        }
    }
}
