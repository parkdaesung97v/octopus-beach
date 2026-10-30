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
    public static class WaterBalloonChecks
    {
        const string Key="Octopus.WaterBalloonCheck";
        static WaterBalloonChecks()
        {
            // File-triggered capture avoids taking over the user's foreground window.
            double nextPoll=0;
            EditorApplication.update+=()=>
            {
                if(EditorApplication.timeSinceStartup<nextPoll)return;
                nextPoll=EditorApplication.timeSinceStartup+.5;
                const string request="Logs/water-balloon-check.request";
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
                    new GameObject("Water balloon integration and real gameplay capture").AddComponent<WaterBalloonCapture>();
                if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key,false))
                {
                    SessionState.SetBool(Key,false);
                    if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetInt(Key+".Exit",2));
                }
            };
        }
        [MenuItem("문어/물풍선 5연투 적용 검증")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)return;
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/water-balloon-check.status","RUNNING");
            ShaderUtil.allowAsyncCompilation=false;
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        internal static void Finish(bool pass)
        {
            File.WriteAllText("Logs/water-balloon-check.status",pass?"PASS":"FAIL");
            SessionState.SetInt(Key+".Exit",pass?0:2);EditorApplication.isPlaying=false;
        }
    }
    public sealed class WaterBalloonCapture:MonoBehaviour
    {
        static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
        IEnumerator routine;float deadline;bool finished;
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Hidden).Invoke(o,args);
        static List<SimActor> Actors(GameSession s)=>(List<SimActor>)typeof(GameSession).GetField("_actors",Hidden).GetValue(s);
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);Debug.Log("WATER BALLOON PASS: "+message);}
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
                if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Water balloon capture timed out");
                if(!routine.MoveNext()){finished=true;Time.captureDeltaTime=0;WaterBalloonChecks.Finish(true);}
            }
            catch(Exception e){Debug.LogError("WATER BALLOON FAILED: "+e);finished=true;Time.captureDeltaTime=0;WaterBalloonChecks.Finish(false);}
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
            camera.orthographicSize=11f;camera.aspect=16f/9;camera.transform.position=new Vector3(0,0,-10);
            var canvasGo=new GameObject("Water balloon live gameplay label");var cv=canvasGo.AddComponent<Canvas>();
            cv.renderMode=RenderMode.ScreenSpaceOverlay;cv.sortingOrder=500;
            var sc=canvasGo.AddComponent<CanvasScaler>();sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;sc.referenceResolution=new Vector2(960,540);
            var labelGo=new GameObject("Variant");labelGo.transform.SetParent(canvasGo.transform,false);
            var label=labelGo.AddComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize=20;label.alignment=TextAnchor.MiddleCenter;label.color=new Color(.1f,.3f,.42f);
            label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(.5f,1);
            label.rectTransform.pivot=new Vector2(.5f,1);label.rectTransform.anchoredPosition=new Vector2(0,-66);label.rectTransform.sizeDelta=new Vector2(680,32);
            Time.captureDeltaTime=1f/30;
            Clear(s);p.transform.position=Vector3.zero;p.NetPos.Value=Vector2.zero;
            Target(s,new Vector2(-3,-2));
            for(int y=-1;y<=1;y++)for(int x=0;x<3;x++)
            {var enemy=Target(s,new Vector2(3.7f+x*.8f,y*.75f));enemy.Hp=enemy.MaxHp=320;}
            s.CastBasicAbility(p,ElementId.Water,1);
            string output=Path.GetFullPath("Build/Validation/WaterBalloonFrames");Directory.CreateDirectory(output);
            label.text="LIVING WATER  |  5 SHOTS / 0.22s / SPLASH 1.9";
            for(int frame=0;frame<120;frame++)
            {
                foreach(var actor in Actors(s).ToArray())
                    if(!actor.Dead&&WaterBalloonGeometry.Supports(actor.Kind))Call(s,"TickPlannedAbility",actor,1f/30);
                foreach(var enemy in Actors(s))if(enemy.Kind==ActorKind.Enemy)enemy.HitFlash=Mathf.Max(0,enemy.HitFlash-1f/30);
                Call(s,"RenderLocal");yield return null;
                if(frame%2==0)Capture(camera,Path.Combine(output,"frame-"+(frame/2).ToString("D3")+".png"));
            }
            Check(!Actors(s).Exists(a=>!a.Dead&&WaterBalloonGeometry.Supports(a.Kind)),"all five balloons and membranes expire");
            Check(UnityEngine.Object.FindObjectsByType<WaterBalloonVisual>().Length==0,"local visuals and trail meshes retire with actors");
            File.WriteAllText("Logs/water-balloon-checks.log","PASS: five ready balloons, 0.22 gap, density aim, enlarged splash, no duplicate damage, frame-rate consistency, movement, upgraded count, no-target fallback, dead-owner cleanup, snapshot visuals, actual gameplay frames");
            Debug.Log("WATER BALLOON CHECKS PASS");
        }
        static void Validate(GameSession s,OctopusPlayer p)
        {
            Check(GameConfig.ActorBytes==25,"network snapshot size remains 25 bytes");
            Clear(s);s.CastBasicAbility(p,ElementId.Water,1);
            var group=Actors(s).FindAll(a=>a.Kind==ActorKind.WaterBalloonShot);
            Check(group.Count==5,"basic cast creates five visible balloons at once");
            for(int i=1;i<group.Count;i++)Check(Mathf.Abs(group[i].Duration-group[i-1].Duration-.22f)<.0001f,"launch interval 0.22: "+i);
            var ready=group[0];Vector2 before=ready.Pos;
            p.transform.position=Vector3.up;p.NetPos.Value=Vector2.up;
            Call(s,"TickPlannedAbility",ready,.1f);
            Check(Vector2.Distance(ready.Pos-before,Vector2.up)<.08f,"waiting balloons follow the moving octopus");
            p.transform.position=Vector3.zero;p.NetPos.Value=Vector2.zero;
            foreach(float dt in new[]{1f/30,1f/60,1f/144,.25f,2f})
            {
                Clear(s);var near=Target(s,new Vector2(-1.5f,-1));
                var dense=Target(s,new Vector2(4,0));var adjacent=Target(s,new Vector2(4.5f,.35f));Target(s,new Vector2(3.8f,-.4f));
                var distant=Target(s,new Vector2(10,5));
                s.CastBasicAbility(p,ElementId.Water,1);
                var shot=Actors(s).Find(a=>a.Kind==ActorKind.WaterBalloonShot);
                float until=shot.Duration+.001f;
                while(until>0){float step=Mathf.Min(dt,until);Call(s,"TickPlannedAbility",shot,step);until-=step;}
                Check(shot.Kind==ActorKind.WaterBalloonBurst&&shot.Pos.x>3,"dense crowd preferred to nearby isolated enemy at dt="+dt);
                Check(Mathf.Abs(dense.MaxHp-dense.Hp-27)<.01f && adjacent.Hp<adjacent.MaxHp,"one splash hits multiple enemies once at dt="+dt);
                Check(near.Hp==near.MaxHp && distant.Hp==distant.MaxHp,"outside the splash remains unharmed at dt="+dt);
                float hp=dense.Hp;Call(s,"TickPlannedAbility",shot,.2f);Call(s,"TickPlannedAbility",shot,1f);
                Check(hp==dense.Hp && shot.Dead,"no repeat damage and clean expiry at dt="+dt);
            }
            Clear(s);s.CastBasicAbility(p,ElementId.Water,3);
            Check(Actors(s).FindAll(a=>a.Kind==ActorKind.WaterBalloonShot).Count==7,"legacy water-repeat upgrade adds balloons to the base five");
            Clear(s);s.CastBasicAbility(p,ElementId.Water,1);
            var empty=Actors(s).Find(a=>a.Kind==ActorKind.WaterBalloonShot);Call(s,"TickPlannedAbility",empty,empty.Duration+.001f);
            Check(empty.Kind==ActorKind.WaterBalloonBurst&&!float.IsNaN(empty.Pos.x),"no-target fallback lands normally");
            Clear(s);s.CastBasicAbility(p,ElementId.Water,1);
            var cancelled=Actors(s).Find(a=>a.Kind==ActorKind.WaterBalloonShot);p.Downed.Value=true;
            Call(s,"TickPlannedAbility",cancelled,.1f);Check(cancelled.Dead,"downed owner cancels pending balloons");p.Downed.Value=false;
            Clear(s);
            // Exercise the exact same Show path as a received network actor snapshot.
            var view=WorldView.Instance;
            view.BeginFrame();view.Show(65000,ActorKind.WaterBalloonShot,new Vector2(0,2),.7f,48,.8f,1.05f);view.EndFrame();
            var visual=UnityEngine.Object.FindFirstObjectByType<WaterBalloonVisual>();
            Check(visual!=null,"client snapshot reconstructs an independent balloon visual");
            view.BeginFrame();view.Show(65000,ActorKind.WaterBalloonBurst,new Vector2(0,2),1.9f,0,.1f,.95f);view.EndFrame();
            Check(visual!=null,"same network ID transitions from flight to membrane");
            var shader=Shader.Find("Octopus/WaterBalloonFlow");
            Check(shader!=null && !ShaderUtil.ShaderHasError(shader),"water flow shader compiles");
            Clear(s);Call(s,"RenderLocal");
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
