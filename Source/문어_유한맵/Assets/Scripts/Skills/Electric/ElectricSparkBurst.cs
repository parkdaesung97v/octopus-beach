using System.Collections.Generic;
using UnityEngine;

namespace OctopusSurvivors
{
    // Presentation only: started locally from a RadialCurrent snapshot. No network actor, no gameplay state,
    // and a private System.Random so seeded gameplay/validation rolls in UnityEngine.Random are never consumed.
    public sealed class ElectricSparkBurst : MonoBehaviour
    {
        const int MaxSparks = 220, MaxBolts = 12, MaxBursts = 8, SparksPerBurst = 56, EmbersPerBurst = 10, BoltsPerBurst = 6;
        const float Life = 1f, Drag = 5f;
        static readonly Color Gold = new Color(1f, .84f, .2f), Orange = new Color(1f, .58f, .08f),
            Yellow = new Color(1f, .95f, .5f), Amber = new Color(1f, .74f, .12f);

        struct Spark
        {
            public SpriteRenderer sr;
            public float angle, speed, delay, life, width, twinkle;
            public bool ember;
        }

        sealed class Burst
        {
            public Vector2 origin;
            public float radius, age;
            public int seed, boltFrame = -1;
            public Spark[] sparks;
            public SpriteRenderer core, glow;
            public LineRenderer[] outer = new LineRenderer[BoltsPerBurst], inner = new LineRenderer[BoltsPerBurst];
        }

        static ElectricSparkBurst instance;
        static Sprite pixel;
        static Material lineMaterial;
        readonly List<Burst> bursts = new List<Burst>();
        readonly Stack<SpriteRenderer> spritePool = new Stack<SpriteRenderer>();
        readonly Stack<LineRenderer> linePool = new Stack<LineRenderer>();
        int sparksInUse, linesInUse;

        public static int ActiveBursts => instance != null ? instance.bursts.Count : 0;
        public static int ActiveSparks => instance != null ? instance.sparksInUse : 0;
        public static int PeakSparks { get; private set; }
        public static int TotalSpawned { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { instance = null; pixel = null; lineMaterial = null; PeakSparks = 0; TotalSpawned = 0; }

        public static void Spawn(Vector2 origin, float radius)
        {
            if (instance == null)
                instance = new GameObject("Electric spark bursts").AddComponent<ElectricSparkBurst>();
            instance.Begin(origin, Mathf.Max(1f, radius));
        }

        static Sprite Pixel()
        {
            if (pixel != null) return pixel;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Spark pixel" };
            texture.SetPixel(0, 0, Color.white); texture.Apply();
            return pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
        }

        static float Ease(float u) { u = Mathf.Clamp01(u); return 1f - (1f - u) * (1f - u) * (1f - u); }

        SpriteRenderer RentSprite(Sprite sprite, int order)
        {
            SpriteRenderer sr = null;
            while (spritePool.Count > 0 && sr == null) sr = spritePool.Pop();
            if (sr == null)
            {
                var go = new GameObject("Spark part"); go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
            }
            sr.sprite = sprite; sr.sortingOrder = order; sr.enabled = true; sr.transform.rotation = Quaternion.identity;
            return sr;
        }

        void ReturnSprite(SpriteRenderer sr)
        {
            if (sr == null) return;
            sr.enabled = false;
            spritePool.Push(sr);
        }

        LineRenderer RentLine(int order)
        {
            LineRenderer lr = null;
            while (linePool.Count > 0 && lr == null) lr = linePool.Pop();
            if (lr == null)
            {
                if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
                var go = new GameObject("Spark bolt"); go.transform.SetParent(transform, false);
                lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true; lr.sharedMaterial = lineMaterial; lr.numCapVertices = 2;
            }
            lr.sortingOrder = order; lr.enabled = true; lr.positionCount = 0;
            linesInUse++;
            return lr;
        }

        void ReturnLine(LineRenderer lr)
        {
            if (lr == null) return;
            lr.enabled = false;
            linePool.Push(lr);
            linesInUse--;
        }

        void Begin(Vector2 origin, float radius)
        {
            if (bursts.Count >= MaxBursts) { Release(bursts[0]); bursts.RemoveAt(0); }
            var rng = new System.Random(unchecked((int)(origin.x * 977f) ^ (int)(origin.y * 1297f) ^ Time.frameCount * 31));
            var b = new Burst { origin = origin, radius = radius, seed = rng.Next() };
            int budget = Mathf.Max(0, MaxSparks - sparksInUse);
            int sparkCount = Mathf.Min(SparksPerBurst, budget);
            int emberCount = Mathf.Min(EmbersPerBurst, Mathf.Max(0, budget - sparkCount));
            b.sparks = new Spark[sparkCount + emberCount];
            for (int i = 0; i < b.sparks.Length; i++)
            {
                bool ember = i >= sparkCount;
                var s = new Spark { ember = ember, angle = (float)(rng.NextDouble() * Mathf.PI * 2), twinkle = (float)rng.NextDouble() };
                if (ember)
                {
                    s.speed = radius * (.12f + .83f * (float)rng.NextDouble());
                    s.delay = .12f; s.life = .9f + .4f * (float)rng.NextDouble(); s.width = radius * .014f;
                }
                else
                {
                    s.speed = radius * (1.9f + 3.5f * (float)rng.NextDouble());
                    s.life = .5f + .5f * (float)rng.NextDouble();
                    s.width = radius * (s.twinkle > .6f ? .022f : .016f);
                }
                s.sr = RentSprite(Pixel(), 16);
                b.sparks[i] = s;
            }
            sparksInUse += b.sparks.Length;
            PeakSparks = Mathf.Max(PeakSparks, sparksInUse);
            b.core = RentSprite(SpriteFactory.Circle(Color.white), 18);
            b.glow = RentSprite(SpriteFactory.Circle(Color.white), 17);
            int bolts = Mathf.Min(BoltsPerBurst, (MaxBolts - linesInUse) / 2);
            for (int i = 0; i < bolts; i++) { b.outer[i] = RentLine(14); b.inner[i] = RentLine(15); }
            bursts.Add(b);
            TotalSpawned++;
        }

        void Release(Burst b)
        {
            foreach (var s in b.sparks) ReturnSprite(s.sr);
            sparksInUse -= b.sparks.Length;
            ReturnSprite(b.core); ReturnSprite(b.glow);
            for (int i = 0; i < BoltsPerBurst; i++) { ReturnLine(b.outer[i]); ReturnLine(b.inner[i]); }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                var b = bursts[i];
                b.age += dt;
                if (b.age >= Life) { Release(b); bursts.RemoveAt(i); continue; }
                Draw(b);
            }
        }

        void Draw(Burst b)
        {
            float t = b.age, R = b.radius;
            Vector3 o = b.origin;

            if (t < .12f)
            {
                float u = t / .12f;
                float r = R * (.35f * (1 - u) + .05f);
                b.core.enabled = b.glow.enabled = true;
                b.core.transform.position = b.glow.transform.position = o;
                b.core.transform.localScale = Vector3.one * r * 2f;
                b.glow.transform.localScale = Vector3.one * r * 3.2f;
                b.core.color = new Color(1, 1, 1, 1f - u * .3f);
                b.glow.color = new Color(Gold.r, Gold.g, Gold.b, .55f * (1 - u));
            }
            else b.core.enabled = b.glow.enabled = false;

            DrawBolts(b, t, R);

            foreach (var s in b.sparks)
            {
                if (s.sr == null) continue;
                float a = t - s.delay;
                if (a < 0 || a >= s.life) { s.sr.enabled = false; continue; }
                float u = a / s.life;
                if (u > .55f && ((int)(s.twinkle * 10f + t * 45f)) % 2 == 1) { s.sr.enabled = false; continue; }
                s.sr.enabled = true;
                Vector2 dir = new Vector2(Mathf.Cos(s.angle), Mathf.Sin(s.angle));
                float alpha = 1f - Mathf.Pow(u, 2.2f);
                if (s.ember)
                {
                    Vector2 p = b.origin + dir * s.speed + new Vector2(Mathf.Sin(a * 5f + s.twinkle * 9f) * .05f, a * .3f);
                    s.sr.transform.position = p;
                    s.sr.transform.rotation = Quaternion.identity;
                    s.sr.transform.localScale = new Vector3(s.width, s.width, 1);
                    s.sr.color = new Color(s.twinkle > .5f ? Gold.r : Yellow.r, s.twinkle > .5f ? Gold.g : Yellow.g,
                        s.twinkle > .5f ? Gold.b : Yellow.b, alpha);
                    continue;
                }
                float head = s.speed / Drag * (1 - Mathf.Exp(-Drag * a));
                float tail = s.speed / Drag * (1 - Mathf.Exp(-Drag * Mathf.Max(0, a - .05f)));
                float length = Mathf.Max(s.width, head - tail);
                s.sr.transform.position = b.origin + dir * (head - length * .5f);
                s.sr.transform.rotation = Quaternion.Euler(0, 0, s.angle * Mathf.Rad2Deg);
                s.sr.transform.localScale = new Vector3(length, s.width, 1);
                Color c = u < .2f ? Color.white : u < .45f ? Yellow : u < .75f ? Gold : Orange;
                c.a = alpha;
                s.sr.color = c;
            }
        }

        void DrawBolts(Burst b, float t, float R)
        {
            bool active = t < .2f;
            int frame = (int)(t / .045f);
            for (int i = 0; i < BoltsPerBurst; i++)
            {
                var outer = b.outer[i]; var inner = b.inner[i];
                if (outer == null) continue;
                outer.enabled = inner.enabled = active;
                if (!active) continue;
                if (frame != b.boltFrame || outer.positionCount == 0)
                {
                    var rng = new System.Random(b.seed + frame * 131 + i * 17);
                    float angle = i * Mathf.PI * 2f / BoltsPerBurst + ((float)rng.NextDouble() - .5f) * .5f;
                    float length = R * (.35f + .25f * (float)rng.NextDouble()) * Ease(t / .08f);
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), perp = new Vector2(-dir.y, dir.x);
                    const int Points = 9;
                    outer.positionCount = inner.positionCount = Points;
                    for (int p = 0; p < Points; p++)
                    {
                        float offset = p == 0 ? 0f : ((float)rng.NextDouble() - .5f) * R * .09f;
                        Vector3 point = b.origin + dir * (length * p / (Points - 1)) + perp * offset;
                        outer.SetPosition(p, point); inner.SetPosition(p, point);
                    }
                }
                float fade = 1f - t / .22f;
                outer.widthMultiplier = R * .035f;
                inner.widthMultiplier = R * .012f;
                Color o = i % 3 == 0 ? Amber : Orange;
                o.a = .6f * fade;
                outer.startColor = outer.endColor = o;
                inner.startColor = inner.endColor = new Color(1f, 1f, .8f, fade);
            }
            if (frame != b.boltFrame) b.boltFrame = frame;
        }
    }
}
