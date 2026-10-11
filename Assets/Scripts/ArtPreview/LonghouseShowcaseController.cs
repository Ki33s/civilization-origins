using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Origins
{
    public sealed class LonghouseShowcaseController : MonoBehaviour
    {
        public Transform model;
        public GameObject roof;
        public Camera view;
        public int triangles,draws;
        float angle;
        bool capture;
        Font font;
        IEnumerator Start()
        {
            Application.targetFrameRate=60; QualitySettings.antiAliasing=4;
            font=Resources.Load<Font>("OriginsUI");
            var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-longhouse-capture");
            if(index<0 || index+1>=args.Length) yield break;
            capture=true; string folder=Path.GetFullPath(args[index+1]); Directory.CreateDirectory(folder);
            yield return null; yield return null;
            var texture=new RenderTexture(1600,1100,24,RenderTextureFormat.ARGB32){antiAliasing=4};
            var previousTarget=view.targetTexture; var previousActive=RenderTexture.active; bool failed=false;
            try
            {
                foreach(string name in new[]{"front","rear","interior","detail"})
                {
                    model.localRotation=Quaternion.Euler(0,name=="rear" ? 180 : 0,0);
                    roof.SetActive(name!="interior"); view.orthographicSize=name=="detail" ? 3.25f : 4.25f;
                    view.targetTexture=texture; view.Render(); RenderTexture.active=texture;
                    var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
                    pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(folder,name+".png"),pixels.EncodeToPNG()); Destroy(pixels);
                }
                File.WriteAllText(Path.Combine(folder,"stats.json"),JsonUtility.ToJson(new Statistics{triangles=triangles,renderers=draws,views=4},true));
                Debug.Log("LONGHOUSE CAPTURE: PASS — front, rear, interior and detail rendered");
            }
            catch(Exception error) { failed=true; Debug.LogException(error); }
            finally { view.targetTexture=previousTarget; RenderTexture.active=previousActive; texture.Release(); Destroy(texture); }
            Application.Quit(failed ? 1 : 0);
        }
        [Serializable] class Statistics { public int triangles,renderers,views; }
        void Update()
        {
            if(capture) return;
            if(Input.GetKeyDown(KeyCode.R)) roof.SetActive(!roof.activeSelf);
            if(Input.GetKeyDown(KeyCode.Home)) { angle=0; view.orthographicSize=4.25f; roof.SetActive(true); }
            view.orthographicSize=Mathf.Clamp(view.orthographicSize-Input.mouseScrollDelta.y*.15f,2.4f,6);
            model.localRotation=Quaternion.Euler(0,angle,0);
        }
        void OnGUI()
        {
            if(capture) return;
            GUI.matrix=Matrix4x4.Scale(Vector3.one*Screen.height/900f); if(font) GUI.skin.font=font;
            GUI.skin.label.fontSize=18; GUI.skin.button.fontSize=18; GUI.skin.box.fontSize=20;
            GUI.Box(new Rect(20,20,330,386),"ДЛИННЫЙ ДОМ · НЕОЛИТ");
            GUI.Label(new Rect(40,64,290,96),"Первая постройка второй эпохи\nДерево · солома · глина\nПлетёные стены и общий очаг");
            if(GUI.Button(new Rect(40,166,290,42),roof.activeSelf ? "Снять крышу [R]" : "Вернуть крышу [R]")) roof.SetActive(!roof.activeSelf);
            GUI.Label(new Rect(40,227,290,30),"Поворот модели: "+angle.ToString("0")+"°");
            angle=GUI.HorizontalSlider(new Rect(40,270,290,20),angle,0,360);
            if(GUI.Button(new Rect(40,310,290,38),"Исходный вид [Home]")) { angle=0; view.orthographicSize=4.25f; roof.SetActive(true); }
            GUI.Label(new Rect(40,361,290,28),triangles.ToString("N0")+" треугольников");
            GUI.Label(new Rect(380,841,900,30),"Колесо — масштаб · Камера изометрическая · Образец для оценки графики");
        }
    }
}
