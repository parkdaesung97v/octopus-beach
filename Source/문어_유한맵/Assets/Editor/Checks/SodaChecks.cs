using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace OctopusSurvivors.EditorTools
{
    [InitializeOnLoad]
    public static class SodaChecks
    {
        const string Key="Octopus.SodaCheck";
        static SodaChecks()
        {
            // File-triggered capture avoids taking over the user's foreground window.
            double nextPoll=0;
            EditorApplication.update+=()=>
            {
                if(EditorApplication.timeSinceStartup<nextPoll)return;
                nextPoll=EditorApplication.timeSinceStartup+.5;
                const string request="Logs/soda-check.request";
                if(EditorApplication.isPlayingOrWillChangePlaymode ||
                    EditorApplication.isCompiling || EditorApplication.isUpdating)return;
                if(File.Exists(request))
                {
                    File.Delete(request);SessionState.SetBool(Key+".Pending",true);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);return;
                }
                if(!SessionState.GetBool(Key+".Pending",false))return;
                SessionState.SetBool(Key+".Pending",false);Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
                    new GameObject("Soda gameplay checks and five foam captures").AddComponent<SodaCapture>();
                if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key,false))
                {
                    SessionState.SetBool(Key,false);
                    if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetInt(Key+".Exit",2));
                }
            };
        }
        [MenuItem("문어/탄산 거품 실제 플레이 5안 및 검증")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)return;
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/soda-check.status","RUNNING");
            ShaderUtil.allowAsyncCompilation=false;
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        internal static void Finish(bool pass)
        {
            File.WriteAllText("Logs/soda-check.status",pass?"PASS":"FAIL");
            SessionState.SetInt(Key+".Exit",pass?0:2);EditorApplication.isPlaying=false;
        }
    }
    public sealed class SodaCapture:MonoBehaviour
    {
        static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
        IEnumerator routine;float deadline;bool finished;
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Hidden).Invoke(o,args);
        static List<SimActor> Actors(GameSession s)=>(List<SimActor>)typeof(GameSession).GetField("_actors",Hidden).GetValue(s);
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);Debug.Log("SODA PASS: "+message);}
        static void Clear(GameSession s){Call(s,"ClearFieldForNextStage");Actors(s).RemoveAll(a=>a.Dead);}
        static SimActor Target(GameSession s,Vector2 pos)
        {
            var e=(SimActor)Call(s,"Create",ActorKind.Enemy,pos,.7f);
            e.Hp=e.MaxHp=2500;e.StunTime=100;e.Element=ElementId.Grass;return e;
        }
        void Start(){deadline=Time.realtimeSinceStartup+300;routine=Run();}
        void Update()
        {
            if(finished)return;
            try
            {
                if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Soda capture timed out");
                if(!routine.MoveNext()){finished=true;SodaVisual.PreviewStyle=SodaVisual.PreviewVariant=0;Time.captureDeltaTime=0;SodaChecks.Finish(true);}
            }
            catch(Exception e){Debug.LogError("SODA FAILED: "+e);finished=true;Time.captureDeltaTime=0;SodaChecks.Finish(false);}
        }
        IEnumerator Run()
        {
            yield return null;
            ushort port;
            using(var socket=new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,0)))
                port=(ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint).Port;
            typeof(GameBootstrap).GetMethod("StartHost",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(ushort)},null).Invoke(null,new object[]{port});
            while(GameSession.Instance==null || !GameSession.Instance.IsSpawned)yield return null;
            var s=GameSession.Instance;s.SelectStartingElement(ElementId.Water);
            while(s.HostElement.Value==0)yield return null;
            s.ToggleLocalReady();while(!s.CanStart())yield return null;s.RequestStart();
            while(!s.IsPlaying || OctopusPlayer.Local==null)yield return null;
            var p=OctopusPlayer.Local;p.enabled=s.enabled=false;
            p.transform.position=Vector3.zero;p.NetPos.Value=Vector2.zero;p.UpElemRank.Value=0;
            p.Slot0.Value=p.Slot1.Value=p.Slot2.Value=p.Slot3.Value=0;
            Validate(s,p);
            var camera=Camera.main;var follow=camera.GetComponent<PlayerCameraFollow>();if(follow!=null)follow.enabled=false;
            camera.orthographicSize=5.8f;camera.aspect=16f/9;camera.transform.position=new Vector3(0,0,-10);
            var canvasGo=new GameObject("Soda gameplay comparison label");var cv=canvasGo.AddComponent<Canvas>();
            cv.renderMode=RenderMode.ScreenSpaceOverlay;cv.sortingOrder=500;
            var sc=canvasGo.AddComponent<CanvasScaler>();sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;sc.referenceResolution=new Vector2(960,540);
            var labelGo=new GameObject("Variant");labelGo.transform.SetParent(canvasGo.transform,false);
            var label=labelGo.AddComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize=20;label.alignment=TextAnchor.MiddleCenter;label.color=new Color(.1f,.3f,.42f);
            label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(.5f,1);
            label.rectTransform.pivot=new Vector2(.5f,1);label.rectTransform.anchoredPosition=new Vector2(0,-66);label.rectTransform.sizeDelta=new Vector2(680,32);
            Time.captureDeltaTime=1f/30;
            for(int variant=0;variant<5;variant++)
            {
                Clear(s);Call(s,"RenderLocal");yield return null;
                SodaVisual.PreviewStyle=0;SodaVisual.PreviewVariant=variant;
                UnityEngine.Random.InitState(7281);
                for(int y=-2;y<=2;y++)for(int x=-3;x<=3;x++)
                {var enemy=Target(s,new Vector2(x*2.3f,y*1.8f));enemy.Hp=enemy.MaxHp=320;}
                s.CastPlannedCombo(p,ComboId.Soda);
                var group=Actors(s).FindAll(a=>a.Kind==ActorKind.SodaWave);
                // One actual bottle in the close camera; the cast's other bottles stay at their map positions.
                var main=group[0];main.Pos=new Vector2(-3,-1.8f);main.Vel=Vector2.right*SodaGeometry.Speed;main.ContactCooldown=.5f;
                for(int i=1;i<group.Count;i++)group[i].Dead=true;
                string output=Path.GetFullPath("Build/Validation/SodaFrames/foam-"+(variant+1));
                Directory.CreateDirectory(output);
                int retainedSamples=0;
                for(int frame=0;frame<156;frame++)
                {
                    if(!main.Dead)Call(s,"TickPlannedAbility",main,1f/30);
                    foreach(var e in Actors(s))if(e.Kind==ActorKind.Enemy)e.HitFlash=Mathf.Max(0,e.HitFlash-1f/30);
                    // Keep the real octopus and bottle in frame without changing simulation movement.
                    float wide=Mathf.SmoothStep(0,1,Mathf.Clamp01((frame-25)/30f));
                    Vector2 focus=main.Pos*Mathf.Lerp(1,.5f,wide);
                    camera.transform.position=new Vector3(focus.x,focus.y,-10);
                    float overview=Mathf.Max(5.8f,Mathf.Abs(main.Pos.y)*.5f+2.6f,Mathf.Abs(main.Pos.x)*.5f/(16f/9)+2.6f);
                    camera.orthographicSize=Mathf.Lerp(2.8f,overview,wide);
                    string[] titles={"FINE FIZZ","PEARL BUBBLES","DENSE FOAM","TWIN FOAM","AIRY BURST"};
                    label.text=(variant+1)+"  "+titles[variant]+"  |  "+(main.Dead?"EXPIRED - FOAM CLEARED":Mathf.Max(0,main.Life).ToString("F1")+"s");
                    Call(s,"RenderLocal");yield return null;
                    if(frame%2==0)Capture(camera,Path.Combine(output,"frame-"+(frame/2).ToString("D3")+".png"));
                    if(!main.Dead)foreach(var effect in UnityEngine.Object.FindObjectsByType<SodaVisual>())
                    {
                        if(effect.OutletGap>.015f)throw new Exception("Foam emission detached from the opening during a turn");
                        if(effect.TrailSamples<retainedSamples)throw new Exception("Old foam was removed before cast expiry");
                        retainedSamples=effect.TrailSamples;
                    }
                }
                Check(UnityEngine.Object.FindObjectsByType<SodaVisual>().Length==0,"all foam patches and bottles retire at cast end: "+variant);
            }
            Debug.Log("SODA CHECKS PASS: real Unity gameplay capture, five foam styles, continuous outlet anchoring, retained foam, expiry, damage, speed, frame rates");
        }
        static void Validate(GameSession s,OctopusPlayer p)
        {
            Clear(s);s.CastPlannedCombo(p,ComboId.Soda);
            var group=Actors(s).FindAll(a=>a.Kind==ActorKind.SodaWave);
            Check(group.Count==5,"base cast retains five independently timed bottles");
            foreach(var a in group)Check(FiniteBeach.Walkable(a.Pos,a.Size),"random spawn remains walkable");
            float reference=-1;
            foreach(float dt in new[]{1f/30,1f/60,1f/144,.25f})
            {
                Clear(s);var enemy=Target(s,Vector2.zero);var far=Target(s,new Vector2(100,100));
                var bottle=new SimActor{Kind=ActorKind.SodaWave,Pos=new Vector2(-2,0),Vel=Vector2.right*SodaGeometry.Speed,Size=1.2f,
                    Life=4,Duration=4,Damage=25,Owner=p,ContactCooldown=100};
                float t=0;
                while(t<1.5f-.00001f){float step=Mathf.Min(dt,1.5f-t);Call(s,"TickPlannedAbility",bottle,step);t+=step;}
                float hp=enemy.Hp;Call(s,"TickPlannedAbility",bottle,.2f);
                Check(enemy.Hp<hp && Vector2.Distance(bottle.Pos,enemy.Pos)>8,"old trail keeps dealing damage far behind head at dt="+dt);
                Check(far.Hp==far.MaxHp,"outside trail takes no damage at dt="+dt);
                Check(Mathf.Abs(bottle.Vel.magnitude-SodaGeometry.Speed)<.001f,"speed stays bounded at dt="+dt);
                if(reference<0)reference=enemy.MaxHp-enemy.Hp;
                Check(Mathf.Abs((enemy.MaxHp-enemy.Hp)-reference)<1.8f,"DPS stable across frame rates at dt="+dt);
                bottle.Life=.01f;hp=enemy.Hp;Call(s,"TickPlannedAbility",bottle,.5f);
                Check(bottle.Dead && hp-enemy.Hp<=1.01f,"expiry cannot apply damage for oversized final timestep");
            }
            Clear(s);s.CastPlannedCombo(p,ComboId.Soda);group=Actors(s).FindAll(a=>a.Kind==ActorKind.SodaWave);
            bool bounded=true;
            for(int i=0;i<200;i++)foreach(var a in group)if(!a.Dead)
            {
                Call(s,"TickPlannedAbility",a,1f/60);
                bounded &= a.Vel.magnitude<=SodaGeometry.Speed+.01f;
            }
            Check(bounded,"turn/bounce never multiplies speed across 1000 movement steps");
            Clear(s);
            var turning=new SimActor{Kind=ActorKind.SodaWave,Pos=Vector2.zero,Vel=Vector2.right*SodaGeometry.Speed,
                SkillPoint=Vector2.up,Size=1.2f,Life=4,Duration=4,Damage=25,Owner=p,ContactCooldown=100};
            Call(s,"TickPlannedAbility",turning,.1f);
            float turn=Vector2.Angle(Vector2.right,turning.Vel);
            Check(turn>26.8f && turn<27.2f && turning.Pos.y>0,"steering follows a continuous 270-degree/second arc");
            Check(GameConfig.ActorBytes==25,"network actor record remains 25 bytes");
            Clear(s);
        }
        static void Capture(Camera camera,string path)
        {
            var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(960,540,24);
            var tex=new Texture2D(960,540,TextureFormat.RGB24,false);var changed=new List<Canvas>();
            try
            {
                foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if(canvas.isRootCanvas && canvas.renderMode==RenderMode.ScreenSpaceOverlay)
                    {changed.Add(canvas);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
            }
            finally
            {
                foreach(var canvas in changed){canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;}
                RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(tex);
            }
        }
    }
}
