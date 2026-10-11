using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Origins
{
    public sealed class SpearmanShowcaseController : MonoBehaviour
    {
        public SpearmanVisual actor;
        public Camera view;
        public int triangles,renderers;
        readonly string[] labels={"Ожидание","Ходьба","Выпад копьём","Защита щитом","Получение удара","Падение"};
        SpearmanPose selected;
        bool paused,cycling;
        float speed=1,turn=180,nextCycle;
        Font font;
        bool capture;
        IEnumerator Start()
        {
            font=Resources.Load<Font>("OriginsUI");
            Application.targetFrameRate=60; QualitySettings.antiAliasing=4;
            actor.PreviewPose(SpearmanPose.Idle);
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"-spearman-capture");
            if(index<0 || index+1>=args.Length) yield break;
            capture=true; cycling=false;
            string folder=Path.GetFullPath(args[index+1]); Directory.CreateDirectory(folder);
            yield return null; yield return null;
            var texture=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);
            var previousTarget=view.targetTexture; var previousActive=RenderTexture.active;
            bool failed=false;
            try
            {
                foreach(SpearmanPose pose in Enum.GetValues(typeof(SpearmanPose)))
                {
                    float time=pose==SpearmanPose.Thrust ? .48f : pose==SpearmanPose.Death ? .99f : pose==SpearmanPose.Hit ? .28f : .25f;
                    actor.PreviewPose(pose,time); view.targetTexture=texture; view.Render(); RenderTexture.active=texture;
                    var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
                    pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(folder,pose.ToString().ToLowerInvariant()+".png"),pixels.EncodeToPNG()); Destroy(pixels);
                    Debug.Log("SPEARMAN CAPTURE: "+pose+" pelvis="+actor.pelvis.position+" tip="+actor.spearTip.position);
                }
                actor.PreviewPose(SpearmanPose.Thrust,.48f);
                if(Vector3.Dot(actor.spearTip.position-actor.spearGrip.position,actor.transform.forward)<1) throw new InvalidOperationException("Spear did not point forward at contact");
                if(actor.transform.position.sqrMagnitude>.001f) throw new InvalidOperationException("Animation moved the map root");
                int contacts=0,steps=0; actor.ThrustContact+=()=>contacts++; actor.Footstep+=()=>steps++;
                actor.PreviewPose(SpearmanPose.Idle); actor.SetWalking(true);
                for(int frame=0;frame<20;frame++) actor.animator.Update(.06f);
                if(!actor.animator.GetCurrentAnimatorStateInfo(0).IsName("Walk") || steps<2) throw new InvalidOperationException("Walking transition or footstep events failed");
                actor.SetWalking(false); for(int frame=0;frame<6;frame++) actor.animator.Update(.06f);
                if(!actor.animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")) throw new InvalidOperationException("Walking did not return to idle");
                actor.PlayAction(SpearmanPose.Thrust); for(int frame=0;frame<24;frame++) actor.animator.Update(.06f);
                if(contacts!=1 || !actor.animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")) throw new InvalidOperationException("Thrust event or recovery failed");
                actor.PlayAction(SpearmanPose.Death); actor.SetWalking(true); for(int frame=0;frame<24;frame++) actor.animator.Update(.06f);
                if(!actor.animator.GetCurrentAnimatorStateInfo(0).IsName("Death")) throw new InvalidOperationException("Death pose should not resume walking");
                File.WriteAllText(Path.Combine(folder,"capture.json"),JsonUtility.ToJson(new CaptureInfo{triangles=triangles,renderers=renderers,poses=6,footsteps=steps,contacts=contacts,rootMotion=actor.animator.applyRootMotion,contactForward=true,transitions=true},true));
                Debug.Log("SPEARMAN CAPTURE: PASS — six poses, fixed root, transitions, footstep/contact events, terminal death");
            }
            catch(Exception error) { failed=true; Debug.LogException(error); }
            finally { view.targetTexture=previousTarget; RenderTexture.active=previousActive; texture.Release(); Destroy(texture); }
            Application.Quit(failed ? 1 : 0);
        }
        [Serializable] class CaptureInfo { public int triangles,renderers,poses,footsteps,contacts; public bool rootMotion,contactForward,transitions; }
        void Update()
        {
            if(capture) return;
            if(Input.GetKeyDown(KeyCode.Space)) paused=!paused;
            for(int i=0;i<6;i++) if(Input.GetKeyDown(KeyCode.Alpha1+i)) Select((SpearmanPose)i);
            if(cycling && Time.unscaledTime>=nextCycle) { Select((SpearmanPose)(((int)selected+1)%6)); nextCycle=Time.unscaledTime+3; }
            actor.animator.speed=paused ? 0 : speed;
            float zoom=Input.mouseScrollDelta.y;
            if(zoom!=0) view.orthographicSize=Mathf.Clamp(view.orthographicSize-zoom*.15f,1.3f,3.5f);
            actor.transform.rotation=Quaternion.Euler(0,turn,0);
        }
        void Select(SpearmanPose pose) { selected=pose; actor.PreviewPose(pose); nextCycle=Time.unscaledTime+3; }
        void OnGUI()
        {
            if(capture) return;
            GUI.matrix=Matrix4x4.Scale(Vector3.one*(Screen.height/900f)); if(font) GUI.skin.font=font;
            GUI.skin.label.fontSize=19; GUI.skin.button.fontSize=18; GUI.skin.box.fontSize=20;
            GUI.Box(new Rect(22,22,330,596),"КОПЕЙЩИК · НЕОЛИТ");
            GUI.Label(new Rect(42,65,290,55),"Риг + Unity Animator\n6 отдельных анимаций");
            for(int i=0;i<6;i++)
            {
                GUI.backgroundColor=(int)selected==i ? new Color(.88f,.64f,.31f) : Color.white;
                if(GUI.Button(new Rect(42,139+i*49,290,41),(i+1)+". "+labels[i])) Select((SpearmanPose)i);
            }
            GUI.backgroundColor=Color.white;
            if(GUI.Button(new Rect(42,448,140,38),paused ? "Продолжить" : "Пауза")) paused=!paused;
            if(GUI.Button(new Rect(192,448,140,38),"Повтор")) Select(selected);
            cycling=GUI.Toggle(new Rect(42,497,290,30),cycling," Все движения по очереди");
            GUI.Label(new Rect(42,537,290,30),"Темп: "+speed.ToString("0.0")+"×");
            speed=GUI.HorizontalSlider(new Rect(42,578,290,20),speed,.3f,1.5f);
            GUI.Box(new Rect(22,641,330,187),"");
            GUI.Label(new Rect(42,659,290,30),"Поворот модели: "+turn.ToString("0")+"°");
            turn=GUI.HorizontalSlider(new Rect(42,705,290,20),turn,0,360);
            GUI.Label(new Rect(42,740,290,66),triangles.ToString("N0")+" треугольников\nКамера изометрическая\nКолесо — масштаб");
            GUI.Label(new Rect(390,830,850,46),"1–6 — движение · Пробел — пауза · Копьё и щит закреплены на руках");
        }
    }
}
