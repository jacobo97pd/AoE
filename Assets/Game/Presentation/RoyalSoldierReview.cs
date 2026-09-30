using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Emberfield.Presentation
{
    /// <summary>One posed character; camera interaction only.</summary>
    public sealed class RoyalSoldierReview : MonoBehaviour
    {
        public GameObject Soldier;
        public Camera[] Cameras;
        public RenderPipelineAsset Pipeline;
        public int View = 0;
        public bool Controls = true;
        private RenderPipelineAsset previous;
        private Vector3[] positions;
        private Quaternion[] rotations;
        private float[] fovs, sizes;
        private bool capture;
        public Camera Active => Cameras[View];
        private void Awake()
        {
            previous=QualitySettings.renderPipeline;
            if(Pipeline)QualitySettings.renderPipeline=Pipeline;
            Application.runInBackground=true;
        }
        private void Start()
        {
            Remember();SetView(0);
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-soldierReview");
            if(i>=0&&i+1<args.Length){capture=true;Controls=false;StartCoroutine(Capture(args[i+1]));}
        }
        private void OnDestroy(){if(Application.isPlaying&&QualitySettings.renderPipeline==Pipeline)QualitySettings.renderPipeline=previous;}
        private void Remember()
        {
            if(positions!=null)return;
            positions=new Vector3[Cameras.Length];rotations=new Quaternion[Cameras.Length];fovs=new float[Cameras.Length];sizes=new float[Cameras.Length];
            for(int i=0;i<Cameras.Length;i++){positions[i]=Cameras[i].transform.position;rotations[i]=Cameras[i].transform.rotation;fovs[i]=Cameras[i].fieldOfView;sizes[i]=Cameras[i].orthographicSize;}
        }
        public void SetView(int index)
        {
            Remember();View=Mathf.Clamp(index,0,2);
            for(int i=0;i<Cameras.Length;i++)Cameras[i].enabled=i==View;
            Active.transform.SetPositionAndRotation(positions[View],rotations[View]);Active.fieldOfView=fovs[View];Active.orthographicSize=sizes[View];
        }
        private void Update()
        {
            if(capture)return;
            var keyboard=Keyboard.current;
            if(keyboard!=null){if(keyboard.digit1Key.wasPressedThisFrame)SetView(0);if(keyboard.digit2Key.wasPressedThisFrame)SetView(1);if(keyboard.digit3Key.wasPressedThisFrame)SetView(2);if(keyboard.hKey.wasPressedThisFrame)Controls=!Controls;}
            var mouse=Mouse.current;if(mouse==null)return;
            var p=mouse.position.ReadValue();if(Controls&&(p.y<58||p.y>Screen.height-42))return;
            float wheel=mouse.scroll.ReadValue().y;
            if(Active.orthographic)Active.orthographicSize=Mathf.Clamp(Active.orthographicSize*Mathf.Exp(-wheel*.0008f),1.5f,10);
            else Active.fieldOfView=Mathf.Clamp(Active.fieldOfView*Mathf.Exp(-wheel*.0007f),22,58);
            if(mouse.leftButton.isPressed&&View!=2){float yaw=mouse.delta.ReadValue().x*.15f;Active.transform.RotateAround(new Vector3(0,1.06f,0),Vector3.up,yaw);}
        }
        private void OnGUI()
        {
            if(!Controls||capture)return;
            float s=Mathf.Max(.7f,Screen.height/1080f);GUI.matrix=Matrix4x4.Scale(new Vector3(s,s,1));float w=Screen.width/s,h=Screen.height/s;
            var label=new GUIStyle(GUI.skin.label){fontSize=19,normal={textColor=new Color(.89f,.81f,.64f)}};
            GUI.Box(new Rect(0,0,w,42),GUIContent.none);GUI.Label(new Rect(24,8,w-48,30),"REINO  /  GUARDIA DE INFANTERÍA",label);
            GUI.Box(new Rect(0,h-58,w,58),GUIContent.none);
            string[] names={"1 · Cercana","2 · Media","3 · RTS"};for(int i=0;i<3;i++)if(GUI.Button(new Rect(w*.5f-255+i*170,h-48,160,37),names[i]))SetView(i);
            if(GUI.Button(new Rect(w-148,h-48,130,37),"Ocultar · H"))Controls=false;
        }
        [Serializable] private class Evidence { public string buildGuid,unity,graphicsDevice;public int width,height,units=1;public bool passed;public string scope="One static 3D soldier. Camera captures do not certify artistic quality or frame rate.";public string[] images; }
        private IEnumerator Capture(string folder)
        {
            Directory.CreateDirectory(folder);yield return null;
            string[] names={"close","medium","rts"};
            for(int i=0;i<3;i++){SetView(i);yield return new WaitForEndOfFrame();CaptureCamera(Active,Screen.width,Screen.height,Path.Combine(folder,names[i]+".png"));}
            var data=new Evidence{buildGuid=Application.buildGUID,unity=Application.unityVersion,graphicsDevice=SystemInfo.graphicsDeviceName,width=Screen.width,height=Screen.height,passed=Soldier!=null&&Soldier.GetComponentsInChildren<Renderer>().Length>0,images=names};
            File.WriteAllText(Path.Combine(folder,"review.json"),JsonUtility.ToJson(data,true));Application.Quit(data.passed?0:1);
        }
        public static void CaptureCamera(Camera camera,int width,int height,string path)
        {
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);target.Create();var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;float aspect=camera.aspect;
            try{camera.aspect=width/(float)height;camera.targetTexture=target;var request=new RenderPipeline.StandardRequest{destination=target};for(int i=0;i<3;i++)RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{camera.targetTexture=previousTarget;camera.aspect=aspect;RenderTexture.active=previousActive;target.Release();DestroyImmediate(target);DestroyImmediate(pixels);}
        }
    }
}
