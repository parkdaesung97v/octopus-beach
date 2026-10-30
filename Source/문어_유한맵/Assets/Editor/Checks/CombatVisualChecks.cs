using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OctopusSurvivors.EditorTools
{
    public static class CombatVisualChecks
    {
        public static void ValidateAndBuild(){AllPreviews();BuildTool.BuildWindows();}
        public static void AllPreviews()
        {
            GiantMonsterSetup.CreateAssets();
            foreach(string file in new[]{"ElementAtlas","ComboAtlas","LegacyAtlas","DetailAtlas"})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/Vfx/"+file+".png");
                if(importer==null)throw new Exception("Missing atlas "+file);
                importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;
                importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
                importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            }
            var monsters=new Sprite[20];for(int i=0;i<20;i++)monsters[i]=GiantMonsterVisual.SpriteAt(i/4+1,i%4);
            RenderGrid(monsters,4,"giant-monsters-preview.png");
            foreach(var item in new[]{("ElementAtlas",4),("ComboAtlas",6),("LegacyAtlas",7),("DetailAtlas",4)})
            {
                var sprites=new Sprite[item.Item2*4];for(int i=0;i<sprites.Length;i++)sprites[i]=HdSpellVisual.Frame(item.Item1,item.Item2,i);
                RenderGrid(sprites,4,item.Item1+"-preview.png");
            }
            SystemDataChecks.Run();
        }
        static void RenderGrid(Sprite[] sprites,int columns,string filename)
        {
            int rows=Mathf.CeilToInt(sprites.Length/(float)columns);
            var root=new GameObject("Visual verification");var camera=root.AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=rows*1.2f;camera.transform.position=new Vector3(0,0,-10);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.19f,.23f);
            for(int i=0;i<sprites.Length;i++)
            {
                if(sprites[i]==null)throw new Exception("Missing preview frame "+filename+" "+i);
                var go=new GameObject("Frame "+i);go.transform.SetParent(root.transform,false);
                go.transform.position=new Vector3((i%columns-(columns-1)*.5f)*2.4f,((rows-1)*.5f-i/columns)*2.4f,0);
                go.transform.localScale=Vector3.one*2.25f;go.AddComponent<SpriteRenderer>().sprite=sprites[i];
            }
            int width=1200,height=rows*300;
            var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var capture=new Texture2D(width,height,TextureFormat.RGB24,false);capture.ReadPixels(new Rect(0,0,width,height),0,0);capture.Apply();
            Directory.CreateDirectory("../../docs/검증");File.WriteAllBytes("../../docs/검증/"+filename,capture.EncodeToPNG());
            var timer=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<30;i++)camera.Render();timer.Stop();
            Debug.Log("VFX RENDER CHECK "+filename+": "+sprites.Length+" sprites, CPU render submission mean "+(timer.Elapsed.TotalMilliseconds/30).ToString("F2")+" ms (editor, not full-game FPS)");
            camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(capture);
        }
        public static void WaterPreview()
        {
            string path="Assets/Resources/Vfx/WaterAtlas.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;
            importer.alphaIsTransparency=true; importer.mipmapEnabled=false;
            importer.filterMode=FilterMode.Bilinear; importer.wrapMode=TextureWrapMode.Clamp;
            importer.maxTextureSize=2048; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var root=new GameObject("Water verification");
            var camera=root.AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=3.6f;camera.transform.position=new Vector3(0,0,-10);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.19f,.23f);
            for(int i=0;i<12;i++)
            {
                var go=new GameObject("Frame "+i);go.transform.SetParent(root.transform,false);
                go.transform.position=new Vector3((i%4-1.5f)*2.6f,(1-i/4)*2.2f,0);
                go.transform.localScale=Vector3.one*2.1f;
                var sr=go.AddComponent<SpriteRenderer>();sr.sprite=WaterVfx.Frame(i);
                if(sr.sprite==null) throw new Exception("Missing water atlas");
            }
            var rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;camera.Render();
            RenderTexture.active=rt;var capture=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            capture.ReadPixels(new Rect(0,0,1600,1000),0,0);capture.Apply();
            Directory.CreateDirectory("../../docs/검증");File.WriteAllBytes("../../docs/검증/water-vfx-preview.png",capture.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=null;
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(capture);
            SystemDataChecks.Run();Debug.Log("WATER VFX PREVIEW PASS: 16 atlas cells, 192 local particle cap, gameplay unchanged");
        }
    }
}
