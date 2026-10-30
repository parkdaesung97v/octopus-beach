using UnityEngine;
using UnityEngine.Rendering;

namespace OctopusSurvivors
{
    // Snapshot-driven presentation only. Damage, landing and aim remain server-owned.
    public sealed class WaterBalloonVisual : MonoBehaviour
    {
        static Mesh quad;
        static Material sharedMaterial;
        static Texture2D sphere, burst;
        MeshRenderer body, trail;
        Mesh trailMesh;
        MaterialPropertyBlock properties;
        readonly Vector3[] history = new Vector3[18];
        readonly float[] historyTimes = new float[18];
        readonly Vector3[] vertices = new Vector3[36];
        readonly Vector2[] uvs = new Vector2[36];
        int samples;
        ActorKind kind;
        float snapshotAge, snapshotTime, duration, heading, phase;
        bool wasFlying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { quad = null; sharedMaterial = null; sphere = burst = null; }

        static Texture2D LoadPixels(string path)
        {
            var bytes = Resources.Load<TextAsset>(path);
            if (bytes == null) return Texture2D.whiteTexture;
            var original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            original.LoadImage(bytes.bytes);
            var pixels = original.GetPixels32();
            int x0 = original.width, y0 = original.height, x1 = 0, y1 = 0;
            for (int y = 0; y < original.height; y++) for (int x = 0; x < original.width; x++)
                if (pixels[y * original.width + x].a > 40)
                { x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y); }
            if (x1 < x0 || y1 < y0) return original;
            var texture = new Texture2D(x1-x0+1, y1-y0+1, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = path };
            texture.SetPixels(original.GetPixels(x0, y0, texture.width, texture.height));
            texture.Apply(false, true); Destroy(original); return texture;
        }

        static void EnsureAssets()
        {
            if (quad == null)
            {
                quad = new Mesh { name = "Water balloon surface quad" };
                quad.vertices = new[] { new Vector3(-.95f,-.95f), new Vector3(.95f,-.95f), new Vector3(.95f,.95f), new Vector3(-.95f,.95f) };
                quad.uv = new[] { new Vector2(-1.9f,-1.9f), new Vector2(1.9f,-1.9f), new Vector2(1.9f,1.9f), new Vector2(-1.9f,1.9f) };
                quad.triangles = new[] { 0,1,2,0,2,3 }; quad.RecalculateBounds();
            }
            if (sharedMaterial != null) return;
            sphere = LoadPixels("Vfx/WaterBalloonPixel");
            burst = LoadPixels("Vfx/WaterBalloonBurst");
            var shader = Shader.Find("Octopus/WaterBalloonFlow");
            if (shader == null) shader = Resources.Load<Shader>("Vfx/WaterBalloonFlow");
            if (shader == null) return;
            sharedMaterial = new Material(shader) { name = "Shared living water surface" };
            sharedMaterial.SetTexture("_MainTex", sphere); sharedMaterial.SetTexture("_BurstTex", burst);
        }

        MeshRenderer Child(string name, Mesh mesh, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = sharedMaterial;
            renderer.sortingOrder = order; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return renderer;
        }

        public void Configure(ActorKind type, float age, float life, byte angle, ushort id)
        {
            EnsureAssets();
            if (body == null)
            {
                properties = new MaterialPropertyBlock(); body = Child("Living water body", quad, 16);
                trailMesh = new Mesh { name = "Water balloon short fluid trail" }; trailMesh.MarkDynamic();
                var triangles = new int[17*6];
                for (int j=0;j<17;j++) { int v=j*2, i=j*6; triangles[i]=v; triangles[i+1]=v+1; triangles[i+2]=v+2; triangles[i+3]=v+1; triangles[i+4]=v+3; triangles[i+5]=v+2; }
                trailMesh.vertices = vertices; trailMesh.uv = uvs; trailMesh.triangles = triangles;
                trail = Child("Short flowing water neck", trailMesh, 15); trail.enabled = false;
            }
            if (type != kind) { samples = 0; wasFlying = false; }
            kind = type; snapshotAge = age; snapshotTime = Time.time; duration = life;
            heading = angle / 255f * 360f; phase = Mathf.Repeat(id * 2.399963f, Mathf.PI * 2);
        }

        void LateUpdate()
        {
            if (body == null) return;
            float age = snapshotAge + Mathf.Min(.2f, Mathf.Max(0, Time.time - snapshotTime));
            bool impact = kind == ActorKind.WaterBalloonBurst;
            bool flying = !impact && age >= duration - WaterBalloonGeometry.FlightTime;
            body.transform.localScale = impact ? Vector3.one * 2f : Vector3.one;
            body.transform.localRotation = Quaternion.Euler(0,0,flying ? heading : 0);
            properties.Clear(); properties.SetFloat("_Clock", age); properties.SetFloat("_Phase", phase);
            properties.SetFloat("_Mode", impact ? 1 : 0); properties.SetFloat("_Flight", flying ? 1 : 0);
            body.SetPropertyBlock(properties);
            if (flying && wasFlying) DrawTrail(age); else { samples = 0; trail.enabled = false; }
            wasFlying = flying;
        }

        void DrawTrail(float age)
        {
            for (int j=Mathf.Min(samples,17);j>0;j--) { history[j]=history[j-1]; historyTimes[j]=historyTimes[j-1]; }
            history[0]=transform.position; historyTimes[0]=Time.time; samples=Mathf.Min(18,samples+1);
            while(samples>1 && Time.time-historyTimes[samples-1]>.065f) samples--;
            if(samples<2) { trail.enabled=false; return; }
            for(int j=0;j<18;j++)
            {
                int sample=Mathf.Min(j,samples-1); float u=sample/(float)(samples-1);
                Vector3 tangent = history[Mathf.Max(0,sample-1)] - history[Mathf.Min(samples-1,sample+1)];
                Vector3 normal = new Vector3(-tangent.y,tangent.x).normalized;
                Vector3 point = history[sample]+normal*((Mathf.Sin(age*13-u*8)*.07f+Mathf.Sin(age*9+u*14)*.025f)*u);
                float width = (.055f*Mathf.Pow(1-u,1.1f)+.003f)*transform.lossyScale.x/WaterBalloonGeometry.Diameter;
                vertices[j*2]=transform.InverseTransformPoint(point+normal*width);
                vertices[j*2+1]=transform.InverseTransformPoint(point-normal*width);
                uvs[j*2]=new Vector2(u,-1); uvs[j*2+1]=new Vector2(u,1);
            }
            trailMesh.vertices=vertices; trailMesh.uv=uvs; trailMesh.RecalculateBounds();
            properties.Clear(); properties.SetFloat("_Mode",2); properties.SetFloat("_Clock",age);
            trail.SetPropertyBlock(properties); trail.enabled=true;
        }
        void OnDestroy() { if(trailMesh!=null) Destroy(trailMesh); }
    }
}
