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
    public static class WaterAlternativesChecks
    {
        const string Key="Octopus.WaterAlternatives";
        static WaterAlternativesChecks()
        {
            // File-triggered capture avoids taking over the user's foreground window.
            double nextPoll=0;
            EditorApplication.update+=()=>
            {
                if(EditorApplication.timeSinceStartup<nextPoll)return;
                nextPoll=EditorApplication.timeSinceStartup+.5;
                const string request="Logs/water-alternatives.request";
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
                    new GameObject("Water alternatives gameplay capture").AddComponent<WaterAlternativesCapture>();
                if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key,false))
                {
                    SessionState.SetBool(Key,false);
                    if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetInt(Key+".Exit",2));
                }
            };
        }
        [MenuItem("문어/기본 물 공격 4안 실제 플레이")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)return;
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/water-alternatives.status","RUNNING");
            ShaderUtil.allowAsyncCompilation=false;
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        internal static void Finish(bool pass)
        {
            File.WriteAllText("Logs/water-alternatives.status",pass?"PASS":"FAIL");
            SessionState.SetInt(Key+".Exit",pass?0:2);EditorApplication.isPlaying=false;
        }
    }
    public sealed class WaterAlternativesCapture:MonoBehaviour
    {
        static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
        IEnumerator routine;float deadline;bool finished;
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Hidden).Invoke(o,args);
        static List<SimActor> Actors(GameSession s)=>(List<SimActor>)typeof(GameSession).GetField("_actors",Hidden).GetValue(s);
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);Debug.Log("WATER PASS: "+message);}
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
                if(!routine.MoveNext()){finished=true;Time.captureDeltaTime=0;WaterAlternativesChecks.Finish(true);}
            }
            catch(Exception e){Debug.LogError("WATER FAILED: "+e);finished=true;Time.captureDeltaTime=0;WaterAlternativesChecks.Finish(false);}
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
            var dots=new List<SpriteRenderer>();
            var texture=new Texture2D(7,7,TextureFormat.RGBA32,false);texture.filterMode=FilterMode.Point;
            for(int y=0;y<7;y++)for(int x=0;x<7;x++)texture.SetPixel(x,y,(x==0||x==6)&&(y<2||y>4)?Color.clear:Color.white);
            texture.Apply();var sprite=Sprite.Create(texture,new Rect(0,0,7,7),new Vector2(.5f,.5f),7);
            for(int i=0;i<900;i++){var go=new GameObject("Pixel water flow");var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=16;dots.Add(sr);}
            camera.orthographicSize=5.8f;camera.transform.position=new Vector3(0,0,-10);
            string[] titles={"01  SWEEPING STREAM","02  ROLLING TIDE","03  SPIRAL CURRENT","04  GEYSER CASCADE"};
            for(int variant=0;variant<4;variant++)
            {
                Clear(s);UnityEngine.Random.InitState(7281);
                for(int y=-2;y<=2;y++)for(int x=-3;x<=3;x++)
                    if(x!=0||y!=0){var enemy=Target(s,new Vector2(x*2.1f,y*1.65f));enemy.Hp=enemy.MaxHp=150;}
                float initial=0;foreach(var e in Actors(s))if(e.Kind==ActorKind.Enemy)initial+=e.Hp;
                string output=Path.GetFullPath("Build/Validation/WaterFrames/option-"+(variant+1));Directory.CreateDirectory(output);
                for(int frame=0;frame<144;frame++)
                {
                    float t=frame/30f;int used=0;
                    Action<Vector2,float,float> dot=(pos,size,foam)=>{
                        if(used>=dots.Count)return;var sr=dots[used++];sr.enabled=true;
                        sr.transform.position=new Vector3(Mathf.Round(pos.x*48)/48,Mathf.Round(pos.y*48)/48,0);
                        sr.transform.localScale=Vector3.one*size;
                        sr.color=foam>.7f?new Color(.83f,1,1,.95f):foam>.35f?new Color(.23f,.83f,1,.85f):new Color(.04f,.48f,.85f,.7f);
                    };
                    Action<Vector2,Vector2,float> hit=(from,to,width)=>s.DamageLine(from,to,width,12f/30,StatusMask.Slow,.5f,0);
                    if(variant==0)
                    {
                        float angle=Mathf.Sin(t*1.65f)*1.1f;
                        Vector2 prev=Vector2.zero;
                        for(int j=0;j<115;j++){
                            float r=.5f+j*.06f;float a=angle+Mathf.Sin(r*1.5f-t*6)*.055f;
                            var center=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;
                            hit(prev,center,.22f);prev=center;
                            for(int k=-1;k<=1;k++)dot(center+new Vector2(-Mathf.Sin(a),Mathf.Cos(a))*(k*.1f+Mathf.Sin(j*.7f-t*12)*.035f),.19f,(j+frame+k*3)%13/12f);
                        }
                    }
                    if(variant==1)
                    {
                        for(int wave=0;wave<2;wave++){
                            float phase=(t*.6f+wave*.5f)%1;float r=.6f+phase*7;
                            Vector2 prev=Vector2.zero;
                            for(int j=0;j<=130;j++){
                                float a=-1.2f+j*2.4f/130;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var pos=dir*r;
                                if(j>0)hit(prev,pos,.23f);prev=pos;
                                for(int k=0;k<3;k++)dot(dir*(r-k*.13f)+dir*Mathf.Sin(j*.4f-t*10)*.035f,.22f,k==0?.95f:k==1?.5f:.15f);
                            }
                        }
                    }
                    if(variant==2)
                    {
                        for(int arm=0;arm<2;arm++){
                            Vector2 prev=Vector2.zero;
                            for(int j=0;j<180;j++){
                                float r=.65f+j*.026f;float a=t*2.6f-j*.027f+arm*Mathf.PI;
                                var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var pos=dir*r;
                                if(j>0)hit(prev,pos,.16f);prev=pos;
                                dot(pos,.22f,(j+frame)%15/14f);dot(pos-dir*.12f,.18f,.25f);
                            }
                        }
                    }
                    if(variant==3)
                    {
                        for(int n=0;n<9;n++){
                            float phase=(t*1.05f-n*.105f)%1;if(phase<0)phase+=1;
                            var root=new Vector2(1.25f+n*.54f,Mathf.Sin(n*.85f)*2.8f);
                            float height=Mathf.Sin(phase*Mathf.PI)*1.7f;
                            for(int j=0;j<34;j++){
                                float u=j/33f;var pos=root+new Vector2(Mathf.Sin(u*4+t*7)*.09f,height*u);
                                for(int k=-1;k<=1;k++)dot(pos+Vector2.right*k*.12f*(1-u*.5f),.19f,(j+frame+k)%11/10f);
                            }
                            for(int j=0;j<28;j++){
                                float a=j*Mathf.PI*2/28;dot(root+new Vector2(Mathf.Cos(a),Mathf.Sin(a)*.4f)*(.25f+phase*.6f),.14f,.9f);
                            }
                            if(height>.5f)hit(root,root+Vector2.up*height,.42f);
                        }
                    }
                    for(int i=used;i<dots.Count;i++)dots[i].enabled=false;
                    foreach(var e in Actors(s))if(e.Kind==ActorKind.Enemy){e.HitFlash=Mathf.Max(0,e.HitFlash-1f/30);if(e.Hp<=0)e.Dead=true;}
                    label.text=titles[variant]+"  |  WATER ATTACK STUDY";
                    Call(s,"RenderLocal");yield return null;
                    if(frame%2==0)Capture(camera,Path.Combine(output,"frame-"+(frame/2).ToString("D3")+".png"));
                }
                float remaining=0;foreach(var e in Actors(s))if(e.Kind==ActorKind.Enemy)remaining+=e.Hp;
                Check(remaining<initial,"alternative "+(variant+1)+" applies real combat damage: "+(initial-remaining));
            }
            Debug.Log("WATER alternatives PASS: four real Unity combat captures");
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
