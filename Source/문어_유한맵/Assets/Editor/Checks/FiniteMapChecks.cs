using System;
using UnityEditor;
using UnityEngine;
namespace OctopusSurvivors.EditorTools
{
    public static class FiniteMapChecks
    {
        public static void Verify()
        {
            var beach=RuntimeArt.BeachTexture();
            if (beach == null || Mathf.Abs(FiniteBeach.Height/(GameConfig.CameraSize*2)-2)>0.001f)
                throw new Exception("Field dimensions or background texture");
            if (!FiniteBeach.Walkable(Vector2.zero,GameConfig.PlayerRadius)) throw new Exception("Spawn blocked");
            if (FiniteBeach.Walkable(new Vector2(0,-24),.42f)) throw new Exception("Water walkable");
            var rock = new Vector2((198f/1492-.5f)*FiniteBeach.Width,(.5f-240f/1054)*FiniteBeach.Height);
            if(FiniteBeach.Walkable(rock,.42f)) throw new Exception("Rock walkable");
            for(int i=0;i<72;i++) {
                float a=i*Mathf.PI/36;
                var end=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*100;
                var moved=FiniteBeach.Move(Vector2.zero,end,.42f);
                if(!FiniteBeach.Walkable(moved,.42f)) throw new Exception("Swept movement escaped");
                var spawn=FiniteBeach.Clamp(end,.5f);
                if(!FiniteBeach.Walkable(spawn,.5f)) throw new Exception("Spawn escaped");
            }
            var body=OctopusSheet.Frame(OctopusAnim.Idle,0);
            var pixels=body.texture.GetPixels32(); int lo=body.texture.height,hi=0;
            for(int y=0;y<body.texture.height;y++) for(int x=0;x<body.texture.width;x++) if(pixels[y*body.texture.width+x].a>16){lo=Mathf.Min(lo,y);hi=Mathf.Max(hi,y);}
            float scale=GameConfig.CameraSize*2*GameConfig.PlayerScreenHeight/((hi-lo+1)/body.pixelsPerUnit);
            float height=(hi-lo+1)/body.pixelsPerUnit*scale/(2*GameConfig.CameraSize)*1080;
            if(Mathf.Abs(height-43.2f)>.01f) throw new Exception("Body size");
            Debug.Log("FINITE_MAP_CHECKS_PASSED: source aspect, 2x2 viewport, 43.2px body, shore/rock exclusion, 72 swept paths and out-of-bounds spawns.");
        }
        public static void VerifyAndBuild(){Verify(); BuildTool.BuildWindows();}
    }
}
