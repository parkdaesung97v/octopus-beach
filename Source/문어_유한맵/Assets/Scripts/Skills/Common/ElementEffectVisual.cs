using System.Collections.Generic;
using UnityEngine;

namespace OctopusSurvivors
{
    // Cosmetic only: server snapshots still determine all positions, range and lifetime.
    // Child geometry is local and adds no network actors or gameplay randomness.
    public sealed class ElementEffectVisual : MonoBehaviour
    {
        static Material _material;
        readonly List<LineRenderer> _lines = new List<LineRenderer>();
        readonly List<SpriteRenderer> _parts = new List<SpriteRenderer>();
        ActorKind _kind;
        float _age, _radius, _duration;
        byte _flags;
        Vector2 _lastPosition;
        Vector2 _direction = Vector2.right;
        int _usedLines, _usedParts;
        static readonly Color Cyan = new Color(0.15f, 0.8f, 1f);
        static readonly Color Hot = new Color(1f, 0.36f, 0.06f);
        static readonly Color Leaf = new Color(0.3f, 0.8f, 0.24f);

        public static bool IsDirectional(ActorKind k) => k == ActorKind.MowerBlade || k == ActorKind.Jellyfish ||
            k == ActorKind.FlameAura || k == ActorKind.SodaWave || k == ActorKind.ChainArc || k == ActorKind.CurrentFan || k == ActorKind.MagnetPair || k == ActorKind.MagnetBlast;
        public static bool Supports(ActorKind k) => (k >= ActorKind.SteamField && k <= ActorKind.MagnetBlast) || (k >= ActorKind.SodaWave && k <= ActorKind.RadialCurrent);

        public void Configure(ActorKind kind, float radius, byte flags, float age, float duration)
        {
            if (_kind != kind) { _kind = kind; _age = 0f; }
            _radius = radius;
            if (Supports(kind)) _age = age;
            _duration = duration;
            _flags = flags;
            if (IsDirectional(kind)) _direction = AbilityRunner.Rotate(Vector2.right, flags / 255f * 360f);
        }

        void LateUpdate()
        {
            _age += Time.deltaTime;
            _usedLines = _usedParts = 0;
            Vector2 delta = (Vector2)transform.position - _lastPosition;
            if (!IsDirectional(_kind) && delta.sqrMagnitude > 0.00001f) _direction = delta.normalized;
            _lastPosition = transform.position;
            float t = _age;
            switch (_kind)
            {
                case ActorKind.Enemy:
                case ActorKind.Boss:
                    if ((_flags & (byte)EnemyFlag.Spicy) != 0)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            float phase = Mathf.Repeat(t * 1.6f + i / 6f, 1f);
                            Blob(new Vector2(Mathf.Sin(i * 3f + t) * 0.4f, phase), 0.14f * (1f - phase),
                                new Color(1f, 0.25f + phase * 0.3f, 0.08f, 1f - phase));
                            Blob(new Vector2(Mathf.Cos(i * 2f + t) * 0.4f, phase + 0.3f), 0.2f,
                                new Color(0.5f, 0.3f, 0.3f, (1f - phase) * 0.5f));
                        }
                    }
                    break;
                case ActorKind.SodaWave:
                    // PET bottle silhouette, neck, cap, label and carbonation.
                    Curve(new[]{new Vector2(-.22f,-.55f),new Vector2(.22f,-.55f),new Vector2(.28f,-.38f),
                        new Vector2(.28f,.22f),new Vector2(.13f,.4f),new Vector2(.13f,.58f),
                        new Vector2(-.13f,.58f),new Vector2(-.13f,.4f),new Vector2(-.28f,.22f),
                        new Vector2(-.28f,-.38f),new Vector2(-.22f,-.55f)}, .045f, new Color(.7f,.95f,1f,.9f));
                    Curve(new[]{new Vector2(-.14f,.59f),new Vector2(.14f,.59f)}, .13f, Cyan);
                    Curve(new[]{new Vector2(-.24f,-.05f),new Vector2(.24f,-.05f)}, .22f, Cyan);
                    for(int i=0;i<4;i++)
                        Blob(new Vector2(Mathf.Sin(i*2.4f)*.15f, -.4f+Mathf.Repeat(t*.6f+i*.21f,.75f)),.035f,Color.white);
                    transform.rotation=Quaternion.Euler(0f,0f,Mathf.Atan2(_direction.y,_direction.x)*Mathf.Rad2Deg-90f);
                    break;
                case ActorKind.LemonShot:
                    Blob(Vector2.zero, .9f, new Color(.95f,.8f,.1f));
                    Ring(Vector2.zero, .65f, new Color(1f,1f,.6f), .1f);
                    for(int i=0;i<6;i++) Curve(new[]{Vector2.zero,Polar(i*Mathf.PI/3f,.62f)},.06f,Color.white);
                    break;
                case ActorKind.FruitTree:
                    Ring(Vector2.zero, 1f, new Color(.3f,.8f,.3f,.2f), .015f);
                    Curve(new[]{new Vector2(0f,-.2f),new Vector2(0f,.45f)},.18f,new Color(.45f,.23f,.1f));
                    Blob(new Vector2(0f,.45f),.4f,Leaf);
                    for(int i=0;i<4;i++) Blob(new Vector2((i-1.5f)*.14f,.4f+Mathf.Sin(i)*.12f),.07f,new Color(1f,.4f,.15f));
                    break;
                case ActorKind.Fruit:
                    Ring(Vector2.zero,1f,new Color(1f,.5f,.2f,.3f),.04f);
                    Blob(new Vector2(0f,Mathf.Max(0f,1f-t/.6f)*3f),.7f,new Color(1f,.4f,.15f));
                    break;
                case ActorKind.ChainArc:
                    Arc(Vector2.left,Vector2.right,new Color(.2f,.5f,1f),t);
                    break;
                case ActorKind.FlameAura:
                    for(int i=0;i<32;i++)
                    {
                        float travel = Mathf.Repeat(t * 2.5f + i / 32f, 1f);
                        float angle = Mathf.Sin(i * 7.13f) * Mathf.PI / 6f;
                        Vector2 flame = Polar(angle, travel * .94f);
                        Blob(flame, .035f + travel * .075f, new Color(1f,.25f,.02f,1f-travel*.7f));
                        Blob(flame, .02f + travel * .035f, new Color(1f,.85f,.2f,1f-travel));
                    }
                    break;
                case ActorKind.RadialCurrent:
                    float front = Mathf.Clamp01(t / Mathf.Max(.01f, _duration));
                    for (int i = 0; i < 16; i++)
                    {
                        float angle = i * Mathf.PI / 8f;
                        Curve(new[]{Polar(angle, Mathf.Max(0f, front-.22f)), Polar(angle+.04f, front-.1f), Polar(angle, front)}, .025f, Cyan);
                    }
                    break;
                case ActorKind.Mist:
                case ActorKind.SteamField:
                    Ring(Vector2.zero, 1f, new Color(1f, 1f, 1f, 0.8f), 0.02f);
                    for (int i = 0; i < 14; i++)
                    {
                        Vector2 p = Polar(i * 2.4f + t * 0.1f, 0.65f * Mathf.Sqrt((i + 1f) / 14f));
                        Blob(p, 0.35f + Mathf.Sin(t + i) * 0.035f, new Color(1f, 1f, 1f, 0.11f));
                    }
                    break;
                case ActorKind.ChiliShot:
                    Curve(new[] { new Vector2(-0.7f,-0.3f), new Vector2(-0.25f,-0.2f), new Vector2(0.15f,0.05f), new Vector2(0.35f,0.4f) }, 0.34f, Color.red);
                    Curve(new[] {new Vector2(0.35f,0.4f),new Vector2(0.3f,0.65f),new Vector2(0.55f,0.7f)},0.12f,Leaf);
                    break;
                case ActorKind.Volcano:
                    Curve(new[] {new Vector2(-1f,-0.65f),new Vector2(-0.4f,0.55f),new Vector2(0.4f,0.55f),new Vector2(1f,-0.65f),new Vector2(-1f,-0.65f)},0.35f,new Color(0.3f,0.19f,0.16f));
                    Blob(Vector2.zero,0.7f,new Color(0.36f,0.21f,0.15f));
                    Ring(new Vector2(0f,0.5f),0.35f,Hot,0.12f);
                    SmokeAndSparks(t,Hot);
                    break;
                case ActorKind.LavaWarning:
                    Ring(Vector2.zero,1f,new Color(1f,0.32f,0.1f,0.8f),0.04f);
                    Curve(new[] {new Vector2(-0.3f,0f),new Vector2(0.3f,0f)},0.03f,Hot);
                    Curve(new[] {new Vector2(0f,-0.3f),new Vector2(0f,0.3f)},0.03f,Hot);
                    float fall = Mathf.Clamp01(t / Mathf.Max(0.01f, _duration));
                    Vector2 rock = Vector2.up * (1f-fall)*3f;
                    Blob(rock,0.22f,new Color(0.24f,0.12f,0.1f));
                    Curve(new[] {rock+new Vector2(-0.12f,0.12f),rock,rock+new Vector2(0.13f,-0.1f)},0.045f,Hot);
                    break;
                case ActorKind.LavaPool:
                    Blob(Vector2.zero,1f,new Color(0.8f,0.12f,0.02f,0.5f));
                    Ring(Vector2.zero,1f,Hot,0.025f);
                    SmokeAndSparks(t,Hot);
                    break;
                case ActorKind.SeaweedPlant:
                    Blob(Vector2.zero,1f,new Color(0.1f,0.55f,0.85f,0.2f));
                    Ring(Vector2.zero,0.9f+0.06f*Mathf.Sin(t*3f),Cyan*new Color(1f,1f,1f,0.5f),0.025f);
                    for(int i=0;i<5;i++)
                    {
                        float x=(i-2)*0.22f;
                        Curve(new[] {new Vector2(x,-0.4f),new Vector2(x+Mathf.Sin(t*3f+i)*0.2f,0.1f),new Vector2(x,0.6f),new Vector2(x+Mathf.Sin(t*3f+i)*0.2f,1.1f)},0.14f,Leaf);
                    }
                    Droplets(t);
                    break;
                case ActorKind.HomingFish:
                    Blob(Vector2.zero,0.65f,Cyan);
                    Curve(new[] {new Vector2(-0.4f,0f),new Vector2(-0.95f,0.45f),new Vector2(-0.95f,-0.45f),new Vector2(-0.4f,0f)},0.15f,Cyan);
                    Blob(new Vector2(0.3f,0.2f),0.09f,Color.white);
                    transform.rotation=Quaternion.Euler(0f,0f,Mathf.Atan2(_direction.y,_direction.x)*Mathf.Rad2Deg);
                    Droplets(t);
                    break;
                case ActorKind.MossShot:
                    Blob(Vector2.zero,0.8f,new Color(0.34f,0.4f,0.4f));
                    for(int i=0;i<5;i++) Blob(Polar(i*2.4f,0.5f),0.22f,Leaf);
                    for(int i=1;i<=4;i++)
                        Ring(-_direction*i*0.8f,0.15f,new Color(0.2f,0.85f,1f,0.5f/i),0.06f);
                    Droplets(t);
                    break;
                case ActorKind.SeedEmitter: break;
                case ActorKind.Seed:
                    Blob(Vector2.zero,0.7f,new Color(0.5f,0.28f,0.08f));
                    if(t>0.35f) for(int i=0;i<4;i++) Curve(new[] {Vector2.zero,Polar(i*1.5f,1f),Polar(i*1.5f+0.3f,1.8f)},0.08f,Leaf);
                    break;
                case ActorKind.GrownTree:
                    float grow=Mathf.Clamp01(t*7f);
                    Curve(new[] {new Vector2(0f,-0.2f),new Vector2(0f,0.8f*grow)},0.24f,new Color(0.4f,0.2f,0.06f));
                    for(int i=0;i<5;i++) Blob(new Vector2(0f,0.55f*grow)+Polar(i*1.25f,0.35f*grow),0.48f*grow,Leaf);
                    for(int i=0;i<6;i++) Curve(new[] {Vector2.zero,Polar(i*1.05f,0.4f),Polar(i*1.05f+0.2f,0.9f)},0.025f,new Color(0.25f,0.18f,0.1f));
                    if(t<0.5f) Burst(t*2f,Leaf);
                    break;
                case ActorKind.MicrowaveCharge:
                    Ring(Vector2.zero,1f,new Color(1f,0.7f,0.3f,0.65f),0.025f);
                    for(int i=0;i<10;i++)
                    {
                        float angle=t*(3f+t*2f)+i*Mathf.PI/5f;
                        Vector2 p=Polar(angle,0.25f+Mathf.Min(t,1.5f)*0.3f);
                        Blob(p,0.045f+t*0.035f,i%2==0?Cyan:Hot);
                        Arc(Polar(angle-0.5f,0.2f),p,i%2==0?Cyan:Hot,t+i);
                    }
                    break;
                case ActorKind.MowerBlade:
                    Ring(Vector2.zero,1f,Cyan,0.035f);
                    for(int i=0;i<16;i++)
                    {
                        float a=t*15f+i*Mathf.PI/8f;
                        Curve(new[] {Polar(a,0.65f),Polar(a+0.1f,1f),Polar(a+0.25f,0.72f)},0.06f,Color.Lerp(Cyan,Color.white,0.5f));
                    }
                    for(int i=0;i<5;i++) Arc(Polar(i*1.2f,0.5f),Polar(i*1.2f+0.3f,1f),Cyan,t+i);
                    Droplets(t,Leaf);
                    break;
                case ActorKind.Jellyfish:
                    // Normalized radius = gameplay reach; body sits in the forward half.
                    Blob(new Vector2(0.35f,0f),0.24f,new Color(0.3f,0.7f,1f,0.35f));
                    Ring(new Vector2(0.35f,0f),0.24f,Cyan,0.02f);
                    for(int i=0;i<7;i++)
                    {
                        float angle=(i-3)*0.22f+Mathf.Sin(t*10f)*0.12f;
                        Arc(new Vector2(0.38f,0f),Polar(angle,0.95f),Cyan,t+i);
                    }
                    FanEdge();
                    break;
                case ActorKind.CurrentFan:
                    FanEdge();
                    for(int i=0;i<13;i++)
                    {
                        float a=-1.08f+i*2.16f/12f;
                        float reach=0.75f+0.24f*Mathf.Sin(t*28f+i)*Mathf.Sin(t*28f+i);
                        Arc(Vector2.zero,Polar(a,reach),Cyan,t*2f+i);
                        Arc(Polar(a,reach*0.6f),Polar(a+0.07f,reach*0.8f),Color.white,t+i);
                    }
                    break;
                case ActorKind.MagnetPair:
                    Ring(Vector2.zero,1f,new Color(0.3f,0.7f,1f,0.35f),0.02f);
                    float collapse=Mathf.Clamp01((t-(_duration-0.25f))/0.25f);
                    for(int pole=-1;pole<=1;pole+=2)
                    {
                        Vector2 p=new Vector2(0f,pole*0.55f*(1f-collapse));
                        Color color=pole<0?Color.red:Cyan;
                        Curve(new[] {p+new Vector2(-0.25f,-0.1f),p+new Vector2(-0.25f,0.1f),p+new Vector2(0.25f,0.1f),p+new Vector2(0.25f,-0.1f)},0.12f,color);
                    }
                    for(int i=0;i<5;i++) Arc(new Vector2((i-2)*0.08f,-0.5f),new Vector2((i-2)*0.08f,0.5f),Cyan,t+i);
                    break;
                case ActorKind.LavaImpact:
                    Burst(Mathf.Clamp01(t/0.45f),Hot); break;
                case ActorKind.MicrowaveBlast:
                case ActorKind.MagnetBlast:
                    Burst(Mathf.Clamp01(t/0.4f),Cyan);
                    Blob(Vector2.zero,Mathf.Min(1f,0.4f+t*3f),new Color(1f,1f,1f,Mathf.Max(0f,0.8f-t*4f)));
                    for(int i=0;i<8;i++) Arc(Vector2.zero,Polar(i*Mathf.PI/4f,0.95f),i%2==0?Hot:Cyan,t+i);
                    if(_kind==ActorKind.MagnetBlast) for(int pole=-1;pole<=1;pole+=2)
                        Blob(new Vector2(0f,pole*0.55f*Mathf.Max(0f,1f-t*8f)),0.15f,pole<0?Color.red:Cyan);
                    break;
            }
            for(int i=_usedLines;i<_lines.Count;i++) _lines[i].enabled=false;
            for(int i=_usedParts;i<_parts.Count;i++) _parts[i].enabled=false;
        }

        void FanEdge()
        {
            var points=new Vector2[35]; points[0]=Vector2.zero; points[34]=Vector2.zero;
            for(int i=1;i<34;i++) points[i]=Polar((-65f+(i-1)*130f/32f)*Mathf.Deg2Rad,1f);
            Curve(points,0.013f,new Color(0.3f,0.85f,1f,0.3f));
        }
        void Droplets(float t) { Droplets(t,Cyan); }
        void Droplets(float t,Color color)
        {
            for(int i=0;i<5;i++)
            {
                float phase=Mathf.Repeat(t*2f+i*0.2f,1f);
                color.a=1f-phase;
                Blob(Polar(i*2.4f,0.3f+phase),0.08f,color);
            }
        }
        void SmokeAndSparks(float t,Color color)
        {
            for(int i=0;i<6;i++)
            {
                float phase=Mathf.Repeat(t*0.8f+i/6f,1f);
                Blob(new Vector2(Mathf.Sin(i+t)*phase*0.5f,0.5f+phase),0.16f+phase*0.15f,new Color(0.4f,0.4f,0.4f,(1f-phase)*0.45f));
                color.a=1f-phase;
                Blob(Polar(i*2.3f,phase*0.7f),0.07f,color);
            }
        }
        void Burst(float u,Color color)
        {
            color.a=1f-u;
            Ring(Vector2.zero,Mathf.Max(0.05f,u),color,0.06f);
            for(int i=0;i<12;i++)
            {
                Blob(Polar(i*2.4f,u),0.055f,color);
                Blob(Polar(i*2.4f+0.3f,u*0.8f),0.09f,new Color(0.35f,0.3f,0.25f,1f-u));
            }
        }
        void Arc(Vector2 from,Vector2 to,Color color,float phase)
        {
            var points=new Vector2[9];
            Vector2 side=new Vector2(-(to-from).y,(to-from).x).normalized;
            for(int i=0;i<9;i++)
            {
                float u=i/8f;
                points[i]=Vector2.Lerp(from,to,u)+side*(i==0||i==8?0f:Mathf.Sin(i*5.7f+phase*31f)*0.025f);
                // Keep all electric branches inside the exact radius and sector.
                if(_kind==ActorKind.CurrentFan || _kind==ActorKind.Jellyfish)
                {
                    float angle=Mathf.Clamp(Mathf.Atan2(points[i].y,points[i].x),-65f*Mathf.Deg2Rad,65f*Mathf.Deg2Rad);
                    points[i]=Polar(angle,Mathf.Min(0.99f,points[i].magnitude));
                }
            }
            var glow = color; glow.a *= 0.2f;
            Curve(points,0.045f,glow);
            Curve(points,0.014f,color);
            if (_kind == ActorKind.CurrentFan || _kind == ActorKind.Jellyfish)
            {
                var edge = color; edge.a *= 0.12f;
                _lines[_usedLines - 1].endColor = edge;
            }
        }
        void Ring(Vector2 center,float radius,Color color,float width)
        {
            var points=new Vector2[49];
            for(int i=0;i<49;i++) points[i]=center+Polar(i*Mathf.PI*2f/48f,radius);
            Curve(points,width,color);
        }
        static Vector2 Polar(float angle,float radius) => new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
        void Curve(Vector2[] points,float width,Color color)
        {
            if(_material==null) _material=new Material(Shader.Find("Sprites/Default"));
            if(_usedLines==_lines.Count)
            {
                var go=new GameObject("EffectLine");go.transform.SetParent(transform,false);
                var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=_material;
                line.sortingOrder=9;line.numCapVertices=2;_lines.Add(line);
            }
            var lr=_lines[_usedLines++];lr.enabled=true;lr.positionCount=points.Length;
            // Positions are converted to world units so line width follows the attack radius too.
            lr.widthMultiplier=width*Mathf.Max(0.2f,_radius);
            lr.startColor=color;lr.endColor=color;
            for(int i=0;i<points.Length;i++) lr.SetPosition(i,points[i]);
        }
        void Blob(Vector2 pos,float radius,Color color)
        {
            if(_usedParts==_parts.Count)
            {
                var go=new GameObject("EffectParticle");go.transform.SetParent(transform,false);
                var sr=go.AddComponent<SpriteRenderer>();sr.sprite=SpriteFactory.Circle(Color.white);sr.sortingOrder=8;_parts.Add(sr);
            }
            var p=_parts[_usedParts++];p.enabled=true;p.color=color;p.transform.localPosition=pos;
            p.transform.localScale=Vector3.one*radius*2f;
        }
    }
}
