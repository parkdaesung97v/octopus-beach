using System.Collections.Generic;
using UnityEngine;

namespace OctopusSurvivors
{
    // Presentation only. Fruit lifetime and landing damage remain server-authoritative.
    public sealed class TreeSkillVisual : MonoBehaviour
    {
        const int Columns = 32, Rows = 32;
        static readonly List<TreeSkillVisual> trees = new List<TreeSkillVisual>();
        struct RainOrigin { public Vector2 root; public float radius, expires; }
        static readonly Dictionary<byte, RainOrigin> origins = new Dictionary<byte, RainOrigin>();
        static Material treeMaterial;
        static Sprite apple, pixel, leaf;
        readonly Vector3[] vertices = new Vector3[(Columns + 1) * (Rows + 1)];
        readonly Vector3[] rest = new Vector3[(Columns + 1) * (Rows + 1)];
        readonly Vector2[] uv = new Vector2[(Columns + 1) * (Rows + 1)];
        readonly Vector3[] shakes = new Vector3[12]; // u, age at release, duration
        readonly SpriteRenderer[] trail = new SpriteRenderer[5];
        Mesh mesh;
        MeshRenderer canopy;
        MaterialPropertyBlock treeTint;
        SpriteRenderer fruit, shadow;
        ActorKind kind;
        Vector3 source, landing;
        float age, duration;
        int shakeIndex, palette = -1;
        float geometryWidth;
        bool initialized, impacted;
        byte sourceKey;

        public static bool Supports(ActorKind kind) => kind == ActorKind.FruitTree || kind == ActorKind.Fruit;
        public bool IsTree => kind == ActorKind.FruitTree;
        public float MaximumLeafOffset { get; private set; }
        public Vector3 FruitPosition => fruit != null ? fruit.transform.position : transform.position;
        public Vector3 SourcePosition => source;
        public Vector3 LandingPosition => landing;
        float Width => TreeAbilityTuning.Data.treeVisualScale * .6f;
        float Height => Width * .74f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { trees.Clear(); origins.Clear(); treeMaterial = null; apple = pixel = leaf = null; }

        public void Configure(ActorKind value, float serverAge, float life, byte originKey = 0)
        {
            kind = value; age = serverAge; duration = life;
            sourceKey = originKey;
            if (IsTree) origins[sourceKey] = new RainOrigin { root = transform.position,
                radius = transform.lossyScale.x, expires = Time.time + Mathf.Max(0, life - serverAge) + 2 };
            if (initialized) return;
            initialized = true;
            if (IsTree)
            {
                BuildTree(); trees.Add(this);
            }
            else BuildFruit();
        }

        void BuildTree()
        {
            treeTint = new MaterialPropertyBlock();
            var texture = Resources.Load<Texture2D>("Vfx/TreeVariants");
            if (texture == null) throw new System.InvalidOperationException("Missing Vfx/TreeVariants");
            if (treeMaterial == null)
            {
                treeMaterial = new Material(Shader.Find("Sprites/Default"));
                treeMaterial.mainTexture = texture;
            }
            var go = new GameObject("Pixel tree - stationary trunk, local leaf shake");
            go.transform.SetParent(transform, false);
            mesh = new Mesh { name = "Tree canopy grid" };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            canopy = go.AddComponent<MeshRenderer>();
            canopy.sharedMaterial = treeMaterial; canopy.sortingOrder = 9;
            var triangles = new int[Columns * Rows * 6];
            var colors = new Color[vertices.Length];
            for (int y = 0; y <= Rows; y++)
            for (int x = 0; x <= Columns; x++)
            {
                int n = y * (Columns + 1) + x;
                colors[n] = Color.white;
                if (x == Columns || y == Rows) continue;
                int i = (y * Columns + x) * 6;
                triangles[i] = n; triangles[i + 1] = n + Columns + 1; triangles[i + 2] = n + 1;
                triangles[i + 3] = n + 1; triangles[i + 4] = n + Columns + 1; triangles[i + 5] = n + Columns + 2;
            }
            UpdateGeometry();
            mesh.colors = colors; mesh.triangles = triangles;
            for (int i = 0; i < shakes.Length; i++) shakes[i] = new Vector3(0, -100, .3f);
        }

        void UpdateGeometry()
        {
            int selected = Mathf.Clamp((int)TreeAbilityTuning.Data.palette, 0, 2);
            for (int y = 0; y <= Rows; y++)
            for (int x = 0; x <= Columns; x++)
            {
                int n = y * (Columns + 1) + x;
                float u = x / (float)Columns, v = y / (float)Rows;
                rest[n] = new Vector3((u - .5f) * Width, (v - .06f) * Height, 0);
                vertices[n] = rest[n];
                // Half-texel inset prevents neighbouring atlas columns bleeding in.
                float inset = .5f / treeMaterial.mainTexture.width;
                uv[n] = new Vector2(Mathf.Lerp(selected / 3f + inset, (selected + 1) / 3f - inset, u), v);
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.RecalculateBounds(); palette = selected;
            geometryWidth = Width;
        }

        Vector3 ReleasePoint(Vector3 target)
        {
            float scale = Mathf.Max(.001f, transform.lossyScale.x);
            float u = (target.x - transform.position.x) / (Width * scale) + .5f;
            shakes[shakeIndex++ % shakes.Length] = new Vector3(u, age, .28f);
            return TreeRainGeometry.ReleasePoint(transform.position, scale, target);
        }

        void BuildFruit()
        {
            EnsurePixelSprites();
            landing = transform.position;
            source = landing + Vector3.up * 3.2f;
            TreeSkillVisual nearest = null;
            float distance = float.PositiveInfinity;
            foreach (var tree in trees)
            {
                if (tree == null || !tree.isActiveAndEnabled || tree.sourceKey != sourceKey) continue;
                float d = (tree.transform.position - landing).sqrMagnitude;
                float radius = tree.transform.lossyScale.x + 1f;
                if (d > radius * radius || d >= distance) continue;
                nearest = tree; distance = d;
            }
            if (nearest != null) source = nearest.ReleasePoint(landing);
            else if (origins.TryGetValue(sourceKey, out var origin) && origin.expires >= Time.time)
                source = TreeRainGeometry.ReleasePoint(origin.root, origin.radius, landing);
            source.x = landing.x;
            fruit = Part("Falling red apple", apple, 14);
            shadow = Part("Landing shadow", pixel, 3);
            for (int i = 0; i < trail.Length; i++) trail[i] = Part("Pixel speed dash", pixel, 13);
            RenderAt(age);
        }

        SpriteRenderer Part(string label, Sprite sprite, int order)
        {
            var go = new GameObject(label); go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sortingOrder = order;
            return sr;
        }

        void LateUpdate() { age += Time.deltaTime; RenderAt(age); }

        public void RenderAt(float time)
        {
            age = time;
            if (!initialized) return;
            if (IsTree)
            {
                if (palette != (int)TreeAbilityTuning.Data.palette || !Mathf.Approximately(geometryWidth, Width)) UpdateGeometry();
                float fade = Mathf.Min(Mathf.Clamp01(age / .16f), Mathf.Clamp01((duration - age) / .22f));
                treeTint.SetColor("_Color", new Color(1, 1, 1, fade)); canopy.SetPropertyBlock(treeTint);
                canopy.transform.localScale = new Vector3(1, Mathf.Lerp(.96f, 1, Mathf.Clamp01(age / .16f)), 1);
                MaximumLeafOffset = 0;
                float scale = Mathf.Max(.01f, transform.lossyScale.x);
                for (int y = 0; y <= Rows; y++)
                for (int x = 0; x <= Columns; x++)
                {
                    int n = y * (Columns + 1) + x;
                    float u = x / (float)Columns, v = y / (float)Rows;
                    float displacement = 0;
                    // No displacement below the foliage, including every trunk vertex.
                    if (v > .43f && (v > .51f || Mathf.Abs(u - .5f) > .1f))
                        foreach (var shake in shakes)
                        {
                            float t = age - shake.y;
                            if (t < 0 || t >= shake.z) continue;
                            float mask = Mathf.Exp(-(u - shake.x) * (u - shake.x) * 160f);
                            displacement += Mathf.Sin(t * 72f) * (1 - t / shake.z) * mask;
                        }
                    float dx = Mathf.Clamp(displacement, -1, 1) * TreeAbilityTuning.Data.leafShake / scale;
                    vertices[n] = rest[n] + new Vector3(dx, Mathf.Abs(dx) * .28f, 0);
                    MaximumLeafOffset = Mathf.Max(MaximumLeafOffset, Mathf.Abs(dx) * scale);
                }
                mesh.vertices = vertices;
                canopy.enabled = fade > 0;
                return;
            }
            float tFall = Mathf.Clamp01(age / Mathf.Max(.01f, duration));
            float progress = tFall * tFall; // Accelerating descent instead of uniform floating.
            float size = TreeRainGeometry.FruitSize;
            // Damage/shadow stay on the marked ground line; the apple's bottom touches it.
            Vector3 contact = landing + Vector3.up * (size * .6f);
            Vector3 point = Vector3.Lerp(source, contact, progress);
            Vector3 direction = Vector3.down;
            float rootScale = Mathf.Max(.01f, transform.lossyScale.x);
            fruit.transform.position = point;
            fruit.transform.localScale = new Vector3(size * Mathf.Lerp(1, .8f, tFall), size * Mathf.Lerp(1, 1.35f, tFall), 1) / rootScale;
            fruit.transform.rotation = Quaternion.identity;
            shadow.transform.position = landing;
            shadow.transform.localScale = new Vector3(size * 1.15f, size * .22f, 1) / rootScale;
            shadow.color = new Color(.12f, .17f, .10f, .13f + .14f * tFall);
            for (int i = 0; i < trail.Length; i++)
            {
                float a = (1 - i / 5f) * Mathf.Clamp01((tFall - .15f) * 2f);
                trail[i].transform.position = point - direction * (size * .75f + (i + 1) * (.09f + tFall * .09f));
                trail[i].enabled = trail[i].transform.position.y <= source.y;
                trail[i].transform.localScale = new Vector3(.035f, .07f + tFall * .07f, 1) / rootScale;
                trail[i].color = new Color(1, .65f, .53f, a * .8f);
            }
        }

        void OnDestroy()
        {
            trees.Remove(this);
            if (mesh != null) Destroy(mesh);
            if (!IsTree && initialized && !impacted && age >= duration * .75f && Application.isPlaying)
            {
                impacted = true;
                var go = new GameObject("Fruit landing pixels"); go.transform.position = landing;
                go.AddComponent<TreeFruitImpact>();
            }
        }

        public static void EnsurePixelSprites()
        {
            if (apple != null) return;
            pixel = MakeSprite(new[] { "W" }, "Tree pixel");
            leaf = MakeSprite(new[] { "..KK..", ".KGGK.", "KGLGK.", ".KGGK.", "..KK.." }, "Tree leaf");
            apple = MakeSprite(new[] {
                ".......KK....", ".....KKGGK...", ".....KGGK....", "...KKKKKKK...", "..KRRRRRRRK..",
                ".KRWWRRRRRRK.", ".KRWRRRRRRRK.", ".KRRRRRRRRRK.", ".KRRRRRRRDRK.",
                "..KRRRRDDDK..", "...KRRDDDK...", "....KKKKK...." }, "Pixel red apple");
        }

        static Sprite MakeSprite(string[] rows, string label)
        {
            var texture = new Texture2D(rows[0].Length, rows.Length, TextureFormat.RGBA32, false) { name = label, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[texture.width * texture.height];
            for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
            {
                char c = rows[y][x];
                pixels[(rows.Length - 1 - y) * texture.width + x] = c == 'K' ? new Color32(35, 34, 32, 255) :
                    c == 'R' ? new Color32(255, 73, 78, 255) : c == 'D' ? new Color32(190, 49, 63, 255) :
                    c == 'G' ? new Color32(99, 159, 67, 255) : c == 'L' ? new Color32(165, 208, 97, 255) :
                    c == 'W' ? new Color32(255, 235, 221, 255) : new Color32(0, 0, 0, 0);
            }
            texture.SetPixels32(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), Mathf.Max(texture.width, texture.height));
        }

        sealed class TreeFruitImpact : MonoBehaviour
        {
            readonly SpriteRenderer[] bits = new SpriteRenderer[5];
            float time;
            void Awake()
            {
                EnsurePixelSprites();
                for (int i = 0; i < bits.Length; i++)
                {
                    var go = new GameObject("Impact fleck"); go.transform.SetParent(transform, false);
                    bits[i] = go.AddComponent<SpriteRenderer>(); bits[i].sprite = i < 2 ? leaf : pixel; bits[i].sortingOrder = 14;
                }
            }
            void Update()
            {
                time += Time.deltaTime;
                if (time > .18f) { Destroy(gameObject); return; }
                for (int i = 0; i < bits.Length; i++)
                {
                    float x = (i - 2) * time * 1.1f;
                    bits[i].transform.localPosition = new Vector3(x, Mathf.Sin(time / .18f * Mathf.PI) * .15f, 0);
                    bits[i].transform.localScale = Vector3.one * (i < 2 ? .09f : .04f);
                    bits[i].color = new Color(.85f, .8f, .5f, 1 - time / .18f);
                }
            }
        }
    }
}
