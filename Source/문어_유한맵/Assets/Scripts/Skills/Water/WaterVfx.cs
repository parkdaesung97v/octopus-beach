using System.Collections.Generic;
using UnityEngine;

namespace OctopusSurvivors
{
    // All particles are local, bounded, and recycled. No gameplay or network timers.
    public sealed class WaterVfx : MonoBehaviour
    {
        static Sprite[] frames;
        static readonly Queue<SpriteRenderer> pool = new Queue<SpriteRenderer>();
        static readonly List<Particle> particles = new List<Particle>();
        static int updateFrame = -1;
        struct Particle { public SpriteRenderer renderer; public Vector3 velocity; public float life, total, size; }
        SpriteRenderer body;
        Vector3 previous;
        float clock, emit;
        bool impact, initialized;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            frames = null;
            pool.Clear();
            particles.Clear();
            updateFrame = -1;
        }

        public static bool Supports(ActorKind kind) => kind == ActorKind.Balloon || kind == ActorKind.Droplet || kind == ActorKind.WaterImpact;
        public static Sprite Frame(int index)
        {
            if (frames == null || frames[Mathf.Clamp(index, 0, 15)] == null ||
                frames[Mathf.Clamp(index, 0, 15)].texture == null)
            {
                var texture = Resources.Load<Texture2D>("Vfx/WaterAtlas");
                if (texture == null) return null;
                frames = new Sprite[16];
                int w = texture.width / 4, h = texture.height / 4;
                for (int i = 0; i < 16; i++)
                    frames[i] = Sprite.Create(texture, new Rect(i % 4 * w, (3-i/4)*h, w, h), new Vector2(.5f,.5f), w);
            }
            return frames[Mathf.Clamp(index,0,15)];
        }
        public void Configure(ActorKind kind)
        {
            impact = kind == ActorKind.WaterImpact;
            if (body == null)
            {
                var child = new GameObject("Liquid frames"); child.transform.SetParent(transform,false);
                body = child.AddComponent<SpriteRenderer>(); body.sortingOrder = 14;
            }
            if (!initialized)
            {
                initialized = true; previous = transform.position;
                if (impact) for (int i=0;i<9;i++)
                {
                    float angle = i * 2.39996f;
                    Emit(transform.position, new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0) * (1.4f+i*.19f), 12, .09f+i%3*.035f, .27f+i%4*.045f);
                }
            }
        }
        void LateUpdate()
        {
            clock += Time.deltaTime;
            if (body == null) return;
            Vector3 delta = transform.position-previous;
            if (!impact && delta.sqrMagnitude>.00001f)
                body.transform.rotation = Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            body.sprite = Frame(impact ? 4+Mathf.Min(7,(int)(clock*40)) : (int)(clock*16)%4);
            body.transform.localScale = Vector3.one*(impact?1.4f:2.9f);
            body.color = new Color(1,1,1,impact?Mathf.Clamp01(1-clock/.24f):1);
            if (!impact && (emit-=Time.deltaTime)<=0)
            {
                emit=.045f;
                Emit(transform.position, -delta.normalized*.4f,13,.24f,.18f);
                Emit(transform.position,new Vector3(Mathf.Sin(clock*61),Mathf.Cos(clock*47),0)*.35f,12,.07f,.24f);
            }
            previous=transform.position;
            TickParticles();
        }
        static void Emit(Vector3 position, Vector3 velocity, int frame, float size, float life)
        {
            if(particles.Count>=192) return;
            SpriteRenderer sr=null;
            while(pool.Count>0 && sr==null) sr=pool.Dequeue();
            if(sr==null) sr=new GameObject("Pooled liquid particle").AddComponent<SpriteRenderer>();
            sr.gameObject.SetActive(true); sr.sprite=Frame(frame); sr.sortingOrder=13;
            sr.transform.position=position; sr.transform.localScale=Vector3.one*size;
            sr.color=Color.white;
            var driver=sr.GetComponent<WaterParticleDriver>() ?? sr.gameObject.AddComponent<WaterParticleDriver>();
            particles.Add(new Particle{renderer=sr,velocity=velocity,life=life,total=life,size=size});
        }
        public static void TickParticles()
        {
            if(updateFrame==Time.frameCount) return; updateFrame=Time.frameCount;
            for(int i=particles.Count-1;i>=0;i--)
            {
                var p=particles[i];
                if(p.renderer==null) { particles.RemoveAt(i); continue; }
                p.life-=Time.deltaTime;
                if(p.life<=0) {p.renderer.gameObject.SetActive(false);pool.Enqueue(p.renderer);particles.RemoveAt(i);continue;}
                p.renderer.transform.position+=p.velocity*Time.deltaTime;
                p.renderer.color=new Color(1,1,1,p.life/p.total*.7f);
                particles[i]=p;
            }
        }
    }
    public sealed class WaterParticleDriver : MonoBehaviour { void LateUpdate() => WaterVfx.TickParticles(); }
}
