using System.Collections.Generic;
using UnityEngine;

namespace OctopusSurvivors
{
    public sealed class HdSpellVisual : MonoBehaviour
    {
        static readonly Dictionary<string,Sprite[]> sheets=new Dictionary<string,Sprite[]>();
        static readonly Dictionary<int,Sprite> yellowFrames=new Dictionary<int,Sprite>();
        SpriteRenderer image,echo;
        LineRenderer footprint;
        ActorKind kind;
        float age,duration,offset;
        Vector3 previous;
        bool initialized;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { sheets.Clear(); yellowFrames.Clear(); }

        // Basic electric reads as yellow: the blue/violet atlas frame is recoloured once on the GPU->CPU copy and cached.
        static Sprite YellowFrame(int index)
        {
            if(yellowFrames.TryGetValue(index,out var cached) && cached!=null)return cached;
            var source=Frame("ElementAtlas",4,index);
            if(source==null)return null;
            var texture=source.texture;Rect rect=source.rect;
            var rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            Texture2D copy;
            try
            {
                Graphics.Blit(texture,rt);RenderTexture.active=rt;
                copy=new Texture2D((int)rect.width,(int)rect.height,TextureFormat.RGBA32,false){name="Yellow electric",filterMode=texture.filterMode};
                copy.ReadPixels(rect,0,0);
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
            var pixels=copy.GetPixels32();
            for(int i=0;i<pixels.Length;i++)
            {
                var c=pixels[i];
                float v=Mathf.Max(c.r,Mathf.Max(c.g,c.b))/255f,m=Mathf.Min(c.r,Mathf.Min(c.g,c.b))/255f;
                pixels[i]=new Color32((byte)Mathf.RoundToInt(v*255f),(byte)Mathf.RoundToInt((v*.82f+m*.18f)*255f),(byte)Mathf.RoundToInt(m*.6f*255f),c.a);
            }
            copy.SetPixels32(pixels);copy.Apply();
            var sprite=Sprite.Create(copy,new Rect(0,0,copy.width,copy.height),new Vector2(.5f,.5f),Mathf.Max(texture.width/4f,texture.height/4f));
            return yellowFrames[index]=sprite;
        }

        public static Sprite Frame(string name,int rows,int index)
        {
            if(!sheets.TryGetValue(name,out var frames) || frames == null || frames.Length != rows * 4 ||
                frames[Mathf.Clamp(index, 0, frames.Length - 1)] == null ||
                frames[Mathf.Clamp(index, 0, frames.Length - 1)].texture == null)
            {
                var texture=Resources.Load<Texture2D>("Vfx/"+name);if(texture==null)return null;
                frames=new Sprite[rows*4];int w=texture.width/4,h=texture.height/rows;
                // Authored row gutters are measured from the reviewed atlas, not assumed uniform.
                float[] gutters=name=="ComboAtlas"?new[]{0f,.19f,.345f,.51f,.675f,.83f,1f}:
                    name=="LegacyAtlas"?new[]{0f,.178f,.316f,.443f,.596f,.735f,.863f,1f}:
                    name=="DetailAtlas"?new[]{0f,.31f,.515f,.759f,1f}:
                    new[]{0f,.25f,.505f,.769f,1f};
                for(int i=0;i<frames.Length;i++)
                {
                    int r=i/4;float bottom=(1-gutters[r+1])*texture.height, height=(gutters[r+1]-gutters[r])*texture.height;
                    float[] columns=name=="ComboAtlas"?new[]{0f,.2734375f,.5234375f,.7734375f,1f}:new[]{0f,.25f,.5f,.75f,1f};
                    int c=i%4;
                    frames[i]=Sprite.Create(texture,new Rect(columns[c]*texture.width,bottom,(columns[c+1]-columns[c])*texture.width,height),new Vector2(.5f,.5f),Mathf.Max(w,h));
                }
                sheets[name]=frames;
            }
            return frames[Mathf.Clamp(index,0,frames.Length-1)];
        }
        public static bool Supports(ActorKind k)=>Mapping(k,out _,out _,out _);
        static bool Mapping(ActorKind k,out string name,out int rows,out int row)
        {
            name="ElementAtlas";rows=4;row=0;
            switch(k)
            {
                case ActorKind.FlameAura:case ActorKind.Tornado:case ActorKind.Firework:case ActorKind.Explosion:return true;
                case ActorKind.RadialCurrent:case ActorKind.ElectricWave:case ActorKind.LightningBolt:row=1;return true;
                case ActorKind.Vine:case ActorKind.ThornPatch:case ActorKind.ThornShot:row=2;return true;
            }
            name="DetailAtlas";rows=4;
            switch(k)
            {
                case ActorKind.Fruit:row=0;return true;
                case ActorKind.HomingFish:case ActorKind.Fish:row=1;return true;
                case ActorKind.Seed:row=2;return true;
                case ActorKind.VolcanoRock:row=3;return true;
            }
            name="ComboAtlas";rows=6;
            switch(k)
            {
                case ActorKind.Mist:case ActorKind.SteamField:case ActorKind.Fog:row=0;return true;
                case ActorKind.ChiliField:case ActorKind.ChiliShot:case ActorKind.Pepper:row=1;return true;
                case ActorKind.FruitTree:case ActorKind.GrownTree:case ActorKind.Tree:row=2;return true;
                case ActorKind.SodaWave:row=3;return true;
                case ActorKind.LemonWheel:case ActorKind.LemonShot:row=4;return true;
                case ActorKind.ChainArc:row=5;return true;
            }
            name="LegacyAtlas";rows=7;
            switch(k)
            {
                case ActorKind.Volcano:case ActorKind.LavaImpact:case ActorKind.LavaPool:row=0;return true;
                case ActorKind.SeaweedPlant:row=1;return true;
                case ActorKind.MossShot:case ActorKind.BounceRock:row=2;return true;
                case ActorKind.MicrowaveCharge:case ActorKind.MicrowaveBlast:row=3;return true;
                case ActorKind.MowerBlade:row=4;return true;
                case ActorKind.Jellyfish:case ActorKind.CurrentFan:row=5;return true;
                case ActorKind.MagnetPair:case ActorKind.MagnetBlast:row=6;return true;
            }
            return false;
        }
        public void Configure(ActorKind value,float serverAge,float life)
        {
            kind=value;duration=life;
            if (kind == ActorKind.Vine)
            {
                var thorns = GetComponent<GrassThornVisual>() ?? gameObject.AddComponent<GrassThornVisual>();
                thorns.Configure(serverAge, life);
                return;
            }
            if (kind == ActorKind.FlameAura)
            {
                var orb = GetComponent<FireOrbVisual>() ?? gameObject.AddComponent<FireOrbVisual>();
                orb.Configure(serverAge, life);
                return;
            }
            if (kind == ActorKind.ChainArc)
            {
                var blueFlame = GetComponent<BlueFlameVisual>() ?? gameObject.AddComponent<BlueFlameVisual>();
                blueFlame.Configure(serverAge, life);
                return;
            }
            if(image==null)
            {
                image=Layer("Textured skill",12);echo=Layer("Textured afterglow",11);
                offset=Mathf.Repeat(transform.position.x*1.37f+transform.position.y*.73f,1);
                if (kind == ActorKind.FruitTree || kind == ActorKind.FlameAura)
                {
                    var go = new GameObject("Attack footprint"); go.transform.SetParent(transform, false);
                    footprint = go.AddComponent<LineRenderer>();
                    footprint.useWorldSpace = false;
                    footprint.sharedMaterial = image.sharedMaterial;
                    footprint.sortingOrder = 3;
                    footprint.widthMultiplier = .025f;
                    bool tree = kind == ActorKind.FruitTree;
                    footprint.startColor = footprint.endColor = tree ? new Color(.5f, 1f, .3f, .25f) : new Color(1f, .5f, .15f, .3f);
                    footprint.loop = true;
                    footprint.positionCount = tree ? 65 : 26;
                    for (int i = 0; i < footprint.positionCount; i++)
                    {
                        if (!tree && i == 0) { footprint.SetPosition(i, Vector3.zero); continue; }
                        float angle = (tree ? i * 360f / 65 : -30f + (i - 1) * 60f / 24) * Mathf.Deg2Rad;
                        footprint.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0));
                    }
                }
            }
            if(serverAge>0)age=serverAge;
            if(!initialized)
            {
                initialized=true;previous=transform.position;
                if(kind==ActorKind.MicrowaveBlast || kind==ActorKind.MagnetBlast || kind==ActorKind.LavaImpact)
                    PlayerCameraFollow.StrongImpact(transform.position);
            }
        }
        SpriteRenderer Layer(string name,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var sr=go.AddComponent<SpriteRenderer>();sr.sortingOrder=order;return sr;
        }

        void LateUpdate()
        {
            if(image==null || !Mapping(kind,out string name,out int rows,out int row))return;
            age+=Time.deltaTime;
            int frame=(int)((age+offset)*14)%4;
            bool tree = kind == ActorKind.FruitTree;
            if (tree) frame = 0; // Stable trunk/canopy; only the falling fruit animates.
            bool shortEffect=duration>0 && duration<.65f;
            if(shortEffect || kind==ActorKind.Fruit || kind==ActorKind.Seed)frame=Mathf.Min(3,(int)(age/Mathf.Max(.01f,duration)*4));
            image.sprite=Frame(name,rows,row*4+frame);
            echo.sprite=Frame(name,rows,row*4+(frame+3)%4);
            if(kind==ActorKind.RadialCurrent)
            {
                image.sprite=YellowFrame(row*4+frame)??image.sprite;
                echo.sprite=YellowFrame(row*4+(frame+3)%4)??echo.sprite;
            }
            Vector3 delta=transform.position-previous;previous=transform.position;
            bool directional=ElementEffectVisual.IsDirectional(kind);
            if(!directional && !tree && delta.sqrMagnitude>.00001f)
                image.transform.rotation=echo.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            float scale=2f,alpha=.85f;
            Vector3 center=Vector3.zero;
            if(kind==ActorKind.Fruit)
            {
                center=Vector3.up*Mathf.Max(0,1-age/Mathf.Max(.01f,duration))*3;
                scale=TreeAbilityTuning.Data.treeVisualScale * TreeAbilityTuning.Data.fruitVisualScaleRatio;
            }
            if(tree)image.transform.localRotation=echo.transform.localRotation=Quaternion.identity;
            if(kind==ActorKind.FlameAura){scale=1.05f;center=Vector3.right*.48f;}
            if(kind==ActorKind.ChainArc){scale=2;alpha=1;}
            if(kind==ActorKind.RadialCurrent){scale=2*Mathf.Clamp01(age/Mathf.Max(.01f,duration));alpha=.85f;}
            if(kind==ActorKind.Mist || kind==ActorKind.SteamField || kind==ActorKind.Fog){scale=2;alpha=.3f;image.sortingOrder=2;echo.sortingOrder=1;}
            if(kind==ActorKind.FruitTree || kind==ActorKind.GrownTree)scale=TreeAbilityTuning.Data.treeVisualScale;
            if(kind==ActorKind.ChiliShot || kind==ActorKind.LemonShot || kind==ActorKind.MossShot)scale=2.7f;
            if(kind==ActorKind.Jellyfish || kind==ActorKind.CurrentFan){scale=1.2f;center=Vector3.right*.4f;}
            if(kind==ActorKind.Tornado || kind==ActorKind.LemonWheel)
                image.transform.localRotation=Quaternion.Euler(0,0,age*220);
            if(kind==ActorKind.ThornPatch || kind==ActorKind.ChiliField){image.sortingOrder=3;alpha=.65f;}
            float fade=duration>0?Mathf.Clamp01((duration-age)/Mathf.Min(.2f,duration*.35f)):1;
            image.transform.localPosition=center;image.transform.localScale=Vector3.one*scale;
            image.color=new Color(1,1,1,alpha*fade);
            echo.transform.localPosition=center-Vector3.right*.06f;
            echo.transform.localScale=Vector3.one*scale*1.05f;
            echo.color=new Color(1,1,1,alpha*.15f*fade);
            echo.enabled = !tree;
        }
    }
    public sealed class ElementHitVisual : MonoBehaviour
    {
        SpriteRenderer flash;
        float remaining;
        bool wasHit;
        public void Configure(byte flags,ElementId element)
        {
            bool hit=(flags&(byte)EnemyFlag.Hit)!=0;
            if(hit && !wasHit)
            {
                if(flash==null){var go=new GameObject("Impact fragments");go.transform.SetParent(transform,false);flash=go.AddComponent<SpriteRenderer>();flash.sortingOrder=16;}
                int frame=element==ElementId.Fire?12:element==ElementId.Grass?14:element==ElementId.Water?15:13;
                flash.sprite=HdSpellVisual.Frame("ElementAtlas",4,frame);remaining=.16f;
            }
            wasHit=hit;
        }
        void LateUpdate()
        {
            if(flash==null)return;remaining-=Time.deltaTime;
            flash.enabled=remaining>0;flash.color=new Color(1,1,1,Mathf.Clamp01(remaining/.16f));
            flash.transform.localScale=Vector3.one*(.7f+(1-Mathf.Clamp01(remaining/.16f))*.3f);
        }
    }
}
