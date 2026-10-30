using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace OctopusSurvivors
{
    // Round, independent foam patches never fold a strip or stretch its UVs at turns.
    [DefaultExecutionOrder(160)]
    public sealed class SodaVisual : MonoBehaviour
    {
        public static int PreviewStyle, PreviewVariant;
        struct Deposit { public Vector2 Position, Direction; public float Birth, Seed; }
        static Sprite glass, pixelGlass;
        static Material foamMaterial, glassMaterial;
        readonly List<Deposit> deposits=new List<Deposit>(600);
        readonly List<Vector3> vertices=new List<Vector3>(3000);
        readonly List<Vector2> uv=new List<Vector2>(3000), flow=new List<Vector2>(3000);
        readonly List<Color> colors=new List<Color>(3000);
        readonly List<int> triangles=new List<int>(4500);
        readonly SpriteRenderer[] bubbles=new SpriteRenderer[36], glints=new SpriteRenderer[36];
        SpriteRenderer bottle; Mesh foam; MeshRenderer foamRenderer; MaterialPropertyBlock properties;
        float size, age, targetAngle, angle, sampleRemainder; Vector2 previousMouth; bool ready, sampled;
        public int TrailSamples=>deposits.Count;
        public Vector2 MouthPosition { get; private set; }
        public float OutletGap=>Vector2.Distance(MouthPosition,bottle.transform.TransformPoint(new Vector3(-.48f,0)));
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache(){glass=pixelGlass=null;foamMaterial=glassMaterial=null;PreviewStyle=PreviewVariant=0;}
        public void Configure(float radius,float serverAge,float duration,byte direction)
        {
            size=radius;age=serverAge;targetAngle=direction/255f*360f;
            if(ready)return;ready=true;angle=targetAngle;
            if(glass==null)
            {
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                texture.LoadImage(Resources.Load<TextAsset>("Vfx/SodaGlass").bytes);
                texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;
                var pixels=texture.GetPixels32();int x0=texture.width,y0=texture.height,x1=0,y1=0;
                for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)
                    if(pixels[y*texture.width+x].a>64){x0=Mathf.Min(x0,x);y0=Mathf.Min(y0,y);x1=Mathf.Max(x1,x);y1=Mathf.Max(y1,y);}
                var rect=new Rect(x0,y0,x1-x0+1,y1-y0+1);
                glass=Sprite.Create(texture,rect,new Vector2(.5f,.5f),rect.width,0,SpriteMeshType.FullRect);
                var pixel=new Texture2D(80,21,TextureFormat.RGBA32,false);pixel.filterMode=FilterMode.Point;
                for(int y=0;y<21;y++)for(int x=0;x<80;x++)pixel.SetPixel(x,y,texture.GetPixelBilinear((rect.x+(x+.5f)*rect.width/80)/texture.width,(rect.y+(y+.5f)*rect.height/21)/texture.height));
                pixel.Apply();pixelGlass=Sprite.Create(pixel,new Rect(0,0,80,21),new Vector2(.5f,.5f),80,0,SpriteMeshType.FullRect);
                glassMaterial=new Material(Shader.Find("Sprites/Default"));foamMaterial=new Material(Shader.Find("Octopus/SodaFlow"));
            }
            var head=new GameObject("Aqua glass bottle - open neck at local left");head.transform.SetParent(transform,false);
            bottle=head.AddComponent<SpriteRenderer>();bottle.sprite=glass;bottle.sharedMaterial=glassMaterial;bottle.sortingOrder=18;
            var go=new GameObject("Persistent carbonated foam - round patches, no ribbon seams");go.transform.SetParent(transform,false);
            foam=new Mesh{name="Carbonation foam patches"};foam.MarkDynamic();go.AddComponent<MeshFilter>().sharedMesh=foam;
            foamRenderer=go.AddComponent<MeshRenderer>();foamRenderer.sharedMaterial=foamMaterial;foamRenderer.sortingOrder=2;
            foamRenderer.shadowCastingMode=ShadowCastingMode.Off;foamRenderer.receiveShadows=false;properties=new MaterialPropertyBlock();
            for(int i=0;i<bubbles.Length;i++)
            {
                var ring=new GameObject("Rising carbonation "+i);ring.transform.SetParent(transform,false);
                var sr=ring.AddComponent<SpriteRenderer>();sr.sprite=SpriteFactory.Ring(Color.white);sr.sharedMaterial=glassMaterial;sr.sortingOrder=4;bubbles[i]=sr;
                var shine=new GameObject("Bubble highlight and pop "+i);shine.transform.SetParent(transform,false);
                var hi=shine.AddComponent<SpriteRenderer>();hi.sprite=SpriteFactory.Circle(Color.white);hi.sharedMaterial=glassMaterial;hi.sortingOrder=5;glints[i]=hi;
            }
        }
        void LateUpdate()
        {
            if(!ready)return;age+=Time.deltaTime;angle=Mathf.MoveTowardsAngle(angle,targetAngle,420f*Time.deltaTime);
            Vector2 direction=AbilityRunner.Rotate(Vector2.right,angle);
            bottle.transform.rotation=Quaternion.Euler(0,0,angle);
            bottle.transform.localScale=Vector3.one*(SodaGeometry.BottleLength(size)/Mathf.Max(.001f,transform.lossyScale.x));
            bottle.sprite=PreviewStyle==1?pixelGlass:glass;
            // One transform anchors BOTH the visible bottle opening and every new foam birth.
            MouthPosition=bottle.transform.TransformPoint(new Vector3(-.48f,0));
            float spacing=Mathf.Max(.09f,size*.14f);
            if(!sampled){previousMouth=MouthPosition;sampled=true;deposits.Add(new Deposit{Position=MouthPosition,Direction=direction,Birth=age,Seed=0});}
            float distance=Vector2.Distance(previousMouth,MouthPosition);
            if(distance>0)
            {
                float at=spacing-sampleRemainder;
                while(at<=distance)
                {
                    float q=at/distance;
                    deposits.Add(new Deposit{Position=Vector2.Lerp(previousMouth,MouthPosition,q),Direction=direction,Birth=age-Time.deltaTime*(1-q),Seed=Mathf.Repeat(deposits.Count*.6180339f,1)});
                    at+=spacing;
                }
                sampleRemainder=Mathf.Repeat(sampleRemainder+distance,spacing);
            }
            previousMouth=MouthPosition;vertices.Clear();uv.Clear();flow.Clear();colors.Clear();triangles.Clear();
            float width=SodaGeometry.TrailRadius(size);int variant=Mathf.Clamp(PreviewVariant,0,4);
            float widthMul=variant==2?1.10f:variant==4?1.02f:.90f;
            for(int i=0;i<deposits.Count;i++)
            {
                var sample=deposits[i];float elapsed=Mathf.Max(0,age-sample.Birth), expansion=1-Mathf.Exp(-elapsed*10);
                // Eject, advect backwards, expand into foam, then settle for the cast's ENTIRE lifetime.
                Vector2 at=sample.Position-sample.Direction*(size*.36f*expansion),side=new Vector2(-sample.Direction.y,sample.Direction.x);
                float radius=Mathf.Lerp(size*.028f,width*widthMul,Mathf.SmoothStep(0,1,elapsed/.28f));
                at+=side*(Mathf.Sin(sample.Seed*47)*radius*.13f);float opacity=elapsed<.16f?.66f:.25f;
                if(variant==3)
                {
                    Quad(at+side*radius*.50f,sample.Direction,radius*.65f,opacity,.1f,sample.Seed);
                    Quad(at-side*radius*.50f,sample.Direction,radius*.65f,opacity,.1f,sample.Seed+.3f);
                }
                else Quad(at,sample.Direction,radius,opacity,0,sample.Seed);
            }
            // Curved source-to-bank bridge. A straight jet splits away from the old path on turns.
            var connection=deposits[0];
            for(int i=deposits.Count-1;i>=0;i--)
                if(age-deposits[i].Birth>=.24f){connection=deposits[i];break;}
            float elapsedConnection=Mathf.Max(0,age-connection.Birth);
            Vector2 end=connection.Position-connection.Direction*(size*.36f*(1-Mathf.Exp(-elapsedConnection*10)));
            if(elapsedConnection<.10f)end=MouthPosition-direction*Mathf.Min(size*.7f,age*SodaGeometry.Speed);
            Vector2 control1=MouthPosition-direction*size*.48f;
            Vector2 control2=end+connection.Direction*size*.32f;
            for(int i=0;i<36;i++)
            {
                float q=i/35f,u=1-q,pulse=.92f+.08f*Mathf.Sin(age*31-i*.5f);
                Vector2 at=u*u*u*MouthPosition+3*u*u*q*control1+3*u*q*q*control2+q*q*q*end;
                Vector2 tangent=(3*u*u*(control1-MouthPosition)+6*u*q*(control2-control1)+3*q*q*(end-control2)).normalized;
                if(tangent.sqrMagnitude<.01f)tangent=-direction;
                float radius=Mathf.Lerp(size*.026f,width*.84f,q)*pulse;
                Quad(at,-tangent,radius,Mathf.Lerp(.78f,.14f,q),1,q);
            }
            foam.Clear();foam.SetVertices(vertices);foam.SetUVs(0,uv);foam.SetUVs(1,flow);foam.SetColors(colors);foam.SetTriangles(triangles,0);foam.RecalculateBounds();
            properties.SetFloat("_Clock",age);properties.SetFloat("_Variant",variant);properties.SetFloat("_Pixel",PreviewStyle);foamRenderer.SetPropertyBlock(properties);
            AnimateBubbles(width,variant);
        }
        void Quad(Vector2 center,Vector2 direction,float radius,float opacity,float fresh,float seed)
        {
            int start=vertices.Count;Vector2 side=new Vector2(-direction.y,direction.x);
            for(int j=0;j<4;j++)
            {
                float x=(j==0||j==3)?-1:1,y=j<2?-1:1;
                vertices.Add(transform.InverseTransformPoint(center+direction*(x*radius)+side*(y*radius)));
                uv.Add(new Vector2(x,y));flow.Add(direction);colors.Add(new Color(fresh,seed,0,opacity));
            }
            triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);triangles.Add(start);triangles.Add(start+2);triangles.Add(start+3);
        }
        void AnimateBubbles(float width,int variant)
        {
            for(int i=0;i<bubbles.Length;i++)
            {
                float q=Mathf.Repeat(age*(variant==4?1.5f:1.05f)+i*.173f,1);
                int index=Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(i*.371f,1)*deposits.Count),0,deposits.Count-1);
                var sample=deposits[index];Vector2 side=new Vector2(-sample.Direction.y,sample.Direction.x);
                Vector2 p=sample.Position-sample.Direction*size*.36f+side*(Mathf.Sin(i*19.1f)*width*.6f)+Vector2.up*(q*.24f);
                float diameter=(variant==1?.17f:.09f)+q*(variant==1?.17f:.11f);
                bubbles[i].transform.position=p;bubbles[i].transform.localScale=Vector3.one*diameter;
                bubbles[i].color=new Color(.20f,.79f,.93f,Mathf.Sin(q*Mathf.PI)*.65f);
                glints[i].transform.position=p+new Vector2(-diameter*.22f,diameter*.24f);
                glints[i].transform.localScale=Vector3.one*(q<.84f?diameter*.18f:diameter*.44f);
                glints[i].color=new Color(.92f,1,1,q<.84f?.88f:(1-q)*5);
            }
        }
        void OnDestroy(){if(foam!=null)Destroy(foam);}
    }
}
