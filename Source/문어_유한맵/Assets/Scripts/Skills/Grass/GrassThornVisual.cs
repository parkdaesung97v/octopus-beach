using UnityEngine;

namespace OctopusSurvivors
{
    // Presentation only: damage and strike cadence stay independent of this longer animation.
    public sealed class GrassThornVisual : MonoBehaviour
    {
        public const float DefaultLifetime = .72f;
        static Sprite[] frames;
        static readonly float[] FrameEnds = { 80f, 160f, 260f, 380f, 580f, 640f, 690f, 720f };
        SpriteRenderer body;
        float age, duration;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => frames = null;

        public static int FrameIndex(float progress)
        {
            float time = Mathf.Clamp01(progress) * 720f;
            for (int i = 0; i < FrameEnds.Length - 1; i++)
                if (time < FrameEnds[i]) return i;
            return 7;
        }

        public static Sprite Frame(int index)
        {
            if (frames == null || frames[0] == null || frames[0].texture == null)
            {
                var asset = Resources.Load<TextAsset>("Vfx/GrassThornPixel");
                if (asset == null) return null;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    { name = "Grass thorn pixel frames", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                if (!texture.LoadImage(asset.bytes)) { Destroy(texture); return null; }
                const int width = 512, height = 544;
                frames = new Sprite[8];
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = Sprite.Create(texture,
                        new Rect(i % 4 * width, (1 - i / 4) * height, width, height),
                        new Vector2(.5f, 44f / height), width, 0, SpriteMeshType.FullRect);
            }
            return frames[Mathf.Clamp(index, 0, 7)];
        }

        public void Configure(float serverAge, float lifetime)
        {
            duration = lifetime > 0f ? lifetime : DefaultLifetime;
            if (body == null)
            {
                var go = new GameObject("Matte pixel thorn strike");
                go.transform.SetParent(transform, false);
                body = go.AddComponent<SpriteRenderer>();
                body.sortingOrder = 6;
                body.transform.localPosition = Vector3.down * .25f;
                body.transform.localScale = Vector3.one * 2f;
                age = Mathf.Max(0f, serverAge);
            }
            // Legacy transient snapshots have age=duration=0; do not restart on each snapshot.
            else if (serverAge > 0f) age = serverAge;
            RenderAt(age);
        }

        public void RenderAt(float seconds)
        {
            if (body == null) return;
            body.sprite = Frame(FrameIndex(seconds / Mathf.Max(.001f, duration)));
            body.color = Color.white;
            body.enabled = seconds < duration;
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            RenderAt(age);
        }
    }
}
