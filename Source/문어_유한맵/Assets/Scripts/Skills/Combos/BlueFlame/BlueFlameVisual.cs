using UnityEngine;

namespace OctopusSurvivors
{
    // Local presentation of an authoritative ChainArc. No damage or new network actors.
    public sealed class BlueFlameVisual : MonoBehaviour
    {
        const int Points = 25;
        const float FlightSeconds = .045f;
        static Material glowMaterial;
        static Material bodyMaterial;
        readonly Mesh[] ribbons = new Mesh[3];
        readonly Vector3[] path = new Vector3[Points];
        readonly Vector3[] vertices = new Vector3[Points * 3];
        readonly Color[] colors = new Color[Points * 3];
        readonly SpriteRenderer[] embers = new SpriteRenderer[6];
        SpriteRenderer impact, impactHalo, flame, flameBody, sourceFlare;
        float age, duration, seed;
        float targetDiameter=.7f;
        bool targetSized;
        bool initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { glowMaterial = null; bodyMaterial = null; }

        public void Configure(float serverAge, float lifetime)
        {
            age = serverAge; duration = lifetime;
            if(!targetSized && WorldView.Instance!=null &&
                WorldView.Instance.TryEnemyBodyDiameter(transform.TransformPoint(Vector3.right),out float diameter))
            {targetDiameter=diameter;targetSized=true;}
            if (initialized) return;
            initialized = true;
            seed = Mathf.Repeat(transform.position.x * .73f + transform.position.y * 1.37f, 19f);
            if (glowMaterial == null)
            {
                var shader = Resources.Load<Shader>("Vfx/BlueFlameGlow");
                glowMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
                glowMaterial.mainTexture = Texture2D.whiteTexture;
                bodyMaterial = new Material(glowMaterial);
                bodyMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            var indices = new int[(Points - 1) * 12];
            for (int i = 0; i < Points - 1; i++)
            for (int side = 0; side < 2; side++)
            {
                int n = i * 3 + side, at = i * 12 + side * 6;
                indices[at] = n; indices[at + 1] = n + 3; indices[at + 2] = n + 1;
                indices[at + 3] = n + 1; indices[at + 4] = n + 3; indices[at + 5] = n + 4;
            }
            for (int i = 0; i < ribbons.Length; i++)
            {
                var go = new GameObject(i == 2 ? "White hot core" : "Cobalt flame ribbon");
                go.transform.SetParent(transform, false);
                var mesh = new Mesh { name = "Blue flame ribbon" }; mesh.MarkDynamic();
                mesh.vertices = vertices; mesh.triangles = indices;
                mesh.uv = new Vector2[vertices.Length];
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = i == 0 ? bodyMaterial : glowMaterial;
                renderer.sortingOrder = 13 + i; ribbons[i] = mesh;
            }
            impact = Sprite("Impact ring", SpriteFactory.Ring(Color.white), 16);
            impactHalo = Sprite("Impact halo", SpriteFactory.Circle(Color.white), 15);
            sourceFlare = Sprite("Departure spark", SpriteFactory.Circle(Color.white), 16);
            flame = Sprite("Original blue flame", null, 17);
            flameBody = Sprite("Blue flame color density", null, 16);
            flameBody.sharedMaterial = bodyMaterial;
            for (int i = 0; i < embers.Length; i++) embers[i] = Sprite("Blue ember", SpriteFactory.Circle(Color.white), 16);
        }

        SpriteRenderer Sprite(string label, Sprite sprite, int order)
        {
            var go = new GameObject(label); go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sharedMaterial = glowMaterial; renderer.sortingOrder = order; return renderer;
        }

        void LateUpdate()
        {
            if (!initialized) return;
            age += Time.deltaTime;
            RenderAt(age);
        }

        // Exposed to the renderer verification so a frame can be inspected deterministically.
        public void RenderAt(float time)
        {
            if (!initialized) return;
            float scale = Mathf.Max(.15f, transform.lossyScale.x);
            float width = Mathf.Max(.1f, UpgradeTuning.Data.blueFlameVisualScale);
            float lifeFade = Mathf.Clamp01((duration - time) / .25f);
            float flash = Mathf.Exp(-Mathf.Max(0, time - FlightSeconds) * 12f);
            float brightness = (time < FlightSeconds ? 1f : .10f + .90f * flash) * lifeFade;
            float head = Mathf.Clamp01(time / FlightSeconds);
            for (int i = 0; i < Points; i++)
            {
                float u = i / (float)(Points - 1) * head;
                float envelope = Mathf.Sin(u * Mathf.PI);
                // Fixed large bends with a small, faster flame ripple. Endpoints never drift.
                float knot = u * 7f, knotIndex = Mathf.Floor(knot);
                float left = Mathf.Repeat(Mathf.Sin(knotIndex * 127.1f + seed * 51.7f) * 43758.55f, 1f) * 2f - 1f;
                float right = Mathf.Repeat(Mathf.Sin((knotIndex + 1f) * 127.1f + seed * 51.7f) * 43758.55f, 1f) * 2f - 1f;
                float bend = Mathf.Lerp(left, right, knot - knotIndex) * .14f * envelope;
                float lick = Mathf.Sin(u * 37f - time * 26f + seed) * .018f * envelope * flash;
                path[i] = new Vector3(Mathf.Lerp(-1f, 1f, u), (bend + lick) / scale, 0f);
            }
            for (int band = 0; band < ribbons.Length; band++)
            {
                float halfWidth = (band == 0 ? .105f : band == 1 ? .065f : .014f) * width / scale;
                Color tint = band == 0 ? new Color(.02f,.12f,.8f) : band == 1 ? new Color(.05f,.75f,1f) : new Color(.8f,1f,1f);
                for (int i = 0; i < Points; i++)
                {
                    Vector3 tangent = path[Mathf.Min(Points - 1, i + 1)] - path[Mathf.Max(0, i - 1)];
                    Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f).normalized;
                    float taper = .45f + .55f * Mathf.Sin(i / (float)(Points - 1) * Mathf.PI);
                    vertices[i * 3] = path[i] - normal * halfWidth * taper;
                    vertices[i * 3 + 1] = path[i];
                    vertices[i * 3 + 2] = path[i] + normal * halfWidth * taper;
                    colors[i * 3] = colors[i * 3 + 2] = new Color(tint.r, tint.g, tint.b, 0f);
                    colors[i * 3 + 1] = new Color(tint.r, tint.g, tint.b, brightness * (band == 0 ? .85f : 1f));
                }
                ribbons[band].vertices = vertices; ribbons[band].colors = colors; ribbons[band].RecalculateBounds();
            }
            float impactAge = Mathf.Max(0, time - FlightSeconds);
            float arrived = time >= FlightSeconds ? 1f : 0f;
            float hit = arrived * Mathf.Exp(-impactAge * 15f) * lifeFade;
            float intensity = Mathf.Clamp(UpgradeTuning.Data.blueFlameImpactIntensity,1f,4f);
            float bodySize=targetDiameter*Mathf.Max(.5f,UpgradeTuning.Data.blueFlameImpactBodyScale);
            float ringSize=targetDiameter*Mathf.Lerp(1.05f,UpgradeTuning.Data.blueFlameImpactRingScale,Mathf.Clamp01(impactAge/.18f));
            float ringFade=arrived*Mathf.Clamp01(1f-impactAge/.28f)*lifeFade;
            Place(impact,Vector3.right,Vector2.one*ringSize/scale,new Color(.08f,.5f,1f,Mathf.Min(1f,ringFade*.65f*intensity)));
            Place(impactHalo,Vector3.right,Vector2.one*bodySize/scale,new Color(.03f,.3f,1f,Mathf.Min(1f,hit*.25f*intensity)));
            float start = Mathf.Exp(-time * 22f) * lifeFade;
            Place(sourceFlare, Vector3.left, Vector2.one * .14f * width / scale, new Color(.25f,.8f,1f,start));
            flame.sprite = HdSpellVisual.Frame("ComboAtlas", 6, 20 + Mathf.Min(3, (int)(impactAge * 16f)));
            float flameAlpha = arrived * Mathf.Clamp01(1f - impactAge / .32f) * lifeFade;
            Place(flame, Vector3.right + Vector3.up * impactAge * .15f * targetDiameter / scale,
                Vector2.one * bodySize / scale, new Color(.08f,.5f,1f,Mathf.Min(1f,flameAlpha * .85f * intensity)));
            if (flame.sprite != null)
                flame.transform.localScale /= Mathf.Max(flame.sprite.bounds.size.x,flame.sprite.bounds.size.y);
            // Saturated alpha layer restores blue contrast on bright terrain without enlarging the effect.
            flameBody.sprite = flame.sprite;
            flameBody.transform.localPosition = flame.transform.localPosition;
            flameBody.transform.localScale = flame.transform.localScale;
            flameBody.color = new Color(.04f,.25f,.9f,flameAlpha * Mathf.Clamp01((intensity-1f)*.5f));
            for (int i = 0; i < embers.Length; i++)
            {
                float phase = seed + i * 2.39996f;
                Vector3 velocity = new Vector3(Mathf.Cos(phase), Mathf.Sin(phase) * .6f + .7f, 0f);
                Vector3 position = Vector3.right + velocity * impactAge * targetDiameter * 1.5f / scale;
                float emberFade = arrived * Mathf.Clamp01(1f - impactAge / (.17f + i * .025f)) * lifeFade;
                Place(embers[i], position, new Vector2(.035f,.09f) * targetDiameter / scale, new Color(.1f,.7f,1f,emberFade));
            }
        }

        static void Place(SpriteRenderer sprite, Vector3 position, Vector2 size, Color tint)
        {
            sprite.transform.localPosition = position;
            sprite.transform.localScale = new Vector3(size.x,size.y,1f);
            sprite.color = tint;
        }

        void OnDestroy()
        {
            foreach (var mesh in ribbons) if (mesh != null) Destroy(mesh);
        }
    }
}
