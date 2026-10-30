using System.Collections.Generic;
using UnityEngine;

namespace OctopusSurvivors
{
    // Only presentation. The server's sampled trail decides damage, including during recall.
    [DefaultExecutionOrder(150)]
    public sealed class FireOrbVisual : MonoBehaviour
    {
        struct Sample { public Vector3 Pos; public float Time; public float Angle; }
        static Sprite[] frames;
        static Material spriteMaterial;
        readonly List<Sample> history = new List<Sample>(40);
        readonly SpriteRenderer[] tail = new SpriteRenderer[18];
        readonly SpriteRenderer[] embers = new SpriteRenderer[8];
        readonly LineRenderer[] heat = new LineRenderer[2];
        SpriteRenderer body, warmth;
        float age, duration, seed;
        Vector3 previous;
        bool initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { frames = null; spriteMaterial = null; }

        public void Configure(float serverAge, float lifetime)
        {
            age = serverAge; duration = lifetime;
            if (initialized) return;
            initialized = true; previous = transform.position;
            seed = Mathf.Repeat(transform.position.x * 1.7f + transform.position.y * 2.1f, 4f);
            LoadFrames();
            body = Layer("Pixel fire head", 18);
            warmth = Layer("Subtle warmth", 11); warmth.sprite = SpriteFactory.Circle(Color.white);
            for (int i = 0; i < tail.Length; i++) tail[i] = Layer("Burning tail " + i, 16);
            for (int i = 0; i < embers.Length; i++) { embers[i] = Layer("Fire ember " + i, 19); embers[i].sprite = SpriteFactory.Circle(Color.white); }
            for (int i = 0; i < heat.Length; i++)
            {
                var go = new GameObject("Heat wisp " + i); go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>(); heat[i] = line;
                line.useWorldSpace = true; line.positionCount = 10;
                line.sharedMaterial = spriteMaterial; line.sortingOrder = 12; line.widthMultiplier = .018f;
                line.startColor = new Color(1f, .58f, .24f, .08f); line.endColor = new Color(1f, .68f, .4f, 0);
            }
        }

        static void LoadFrames()
        {
            if (frames != null && frames[0] != null) return;
            var asset = Resources.Load<TextAsset>("Vfx/FireOrbPixel");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(asset.bytes); texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = texture.GetPixels32(); int cell = texture.width / 4;
            frames = new Sprite[4];
            for (int frame = 0; frame < 4; frame++)
            {
                int minX = cell, minY = texture.height, maxX = 0, maxY = 0;
                for (int y = 0; y < texture.height; y++) for (int x = 0; x < cell; x++)
                    if (pixels[y * texture.width + frame * cell + x].a > 40)
                    { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
                var rect = new Rect(frame * cell + minX, minY, maxX - minX + 1, maxY - minY + 1);
                frames[frame] = Sprite.Create(texture, rect, new Vector2(.73f, .5f), rect.width, 0, SpriteMeshType.FullRect);
            }
            spriteMaterial = new Material(Shader.Find("Sprites/Default"));
        }
        SpriteRenderer Layer(string name, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var sprite = go.AddComponent<SpriteRenderer>(); sprite.sharedMaterial = spriteMaterial; sprite.sortingOrder = order; return sprite;
        }
        void LateUpdate()
        {
            if (!initialized || frames == null) return;
            age += Time.deltaTime;
            Vector3 pos = transform.position;
            Vector3 delta = pos - previous; previous = pos;
            float angle = transform.eulerAngles.z;
            if (delta.sqrMagnitude > .00001f) angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            if (history.Count == 0 || Time.time - history[history.Count - 1].Time >= 1f / 60f)
                history.Add(new Sample { Pos = pos, Time = Time.time, Angle = angle });
            float trailTime = UpgradeTuning.Data.fireOrbTrailTime;
            while (history.Count > 1 && Time.time - history[1].Time > trailTime) history.RemoveAt(0);
            float scale = Mathf.Max(.15f, transform.lossyScale.x);
            float fade = Mathf.Clamp01((duration - age) / .08f);
            int frame = Mathf.FloorToInt(age * 13f + seed) % 4;
            body.sprite = frames[frame]; body.transform.rotation = Quaternion.Euler(0, 0, angle);
            body.transform.localScale = Vector3.one * 3.2f; body.color = new Color(1, 1, 1, fade);
            warmth.transform.localScale = Vector3.one * 2.15f;
            warmth.color = new Color(1, .22f, .025f, .055f * fade);
            for (int i = 0; i < tail.Length; i++)
            {
                float q = (i + 1f) / tail.Length;
                float targetTime = Time.time - q * trailTime;
                Sample sample = history[0];
                for (int k = history.Count - 1; k >= 0; k--) if (history[k].Time <= targetTime) { sample = history[k]; break; }
                bool visible = history.Count > 1 && sample.Time >= Time.time - trailTime && Time.time - history[0].Time >= q * trailTime * .7f;
                tail[i].enabled = visible;
                if (!visible) continue;
                tail[i].sprite = frames[(frame + i) % 4];
                tail[i].transform.position = sample.Pos;
                tail[i].transform.rotation = Quaternion.Euler(0, 0, sample.Angle);
                tail[i].transform.localScale = Vector3.one * Mathf.Lerp(2.4f, .28f, q);
                tail[i].color = new Color(1f, .85f, .65f, Mathf.Lerp(.35f, .02f, q) * fade);
            }
            for (int i = 0; i < embers.Length; i++)
            {
                float q = Mathf.Repeat(age * 1.8f + i * .137f, 1f);
                Vector3 at = history[Mathf.Clamp(Mathf.RoundToInt((1 - q) * (history.Count - 1)), 0, history.Count - 1)].Pos;
                at += new Vector3(Mathf.Sin(i * 2.4f + seed) * q * .28f, q * .35f, 0);
                embers[i].transform.position = at; embers[i].transform.localScale = Vector3.one * (.08f * (1 - q));
                embers[i].color = new Color(1, .55f, .12f, (1 - q) * .45f * fade);
            }
            for (int i = 0; i < heat.Length; i++) for (int k = 0; k < 10; k++)
            {
                float q = k / 9f;
                Vector3 at = history[Mathf.Clamp(history.Count / 2, 0, history.Count - 1)].Pos;
                at += new Vector3(Mathf.Sin(age * 8f + q * 6f + i * 2f) * .05f + (i - .5f) * .15f, q * .5f, 0);
                heat[i].SetPosition(k, at);
            }
        }
    }
}
