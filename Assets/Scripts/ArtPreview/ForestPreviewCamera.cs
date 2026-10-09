using System.Collections;
using System.IO;
using UnityEngine;

namespace Origins
{
    // Fixed true isometry: no perspective or orbit mode.
    public sealed class ForestPreviewCamera : MonoBehaviour
    {
        public Transform canopy;
        public GameObject[] treeGeometry;
        public GameObject[] comparisonContext;
        public Vector3[] densityCenters;
        public int[] densityObjects, densityTrees;
        public int trees, grassTufts, flowers, triangles;
        Camera view;
        Vector3 focus=new Vector3(0,1,0);
        float size=13.5f;
        bool showCanopy=true, showUI=true, comparing;
        GUIStyle title, body;
        void Start()
        {
            view=GetComponent<Camera>(); Application.targetFrameRate=60;
            QualitySettings.antiAliasing=4;
            QualitySettings.shadows=ShadowQuality.All;
            QualitySettings.shadowResolution=ShadowResolution.VeryHigh;
            QualitySettings.shadowDistance=100;
            Preset(3);
            string[] args=System.Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)
            {
                if(args[i]=="-forest-capture") StartCoroutine(Capture(args[i+1]));
                if(args[i]=="-forest-ui-capture") StartCoroutine(CaptureUI(args[i+1]));
            }
        }
        void Apply()
        {
            view.orthographic=true; view.orthographicSize=size;
            transform.rotation=Quaternion.Euler(35.2643897f,45,0);
            transform.position=focus-transform.forward*45;
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.Alpha1)) Preset(0);
            if(Input.GetKeyDown(KeyCode.Alpha2)) Preset(1);
            if(Input.GetKeyDown(KeyCode.Alpha3)) Preset(2);
            if(Input.GetKeyDown(KeyCode.Alpha4)) Preset(3);
            if(Input.GetKeyDown(KeyCode.C)) ToggleCanopy();
            if(Input.GetKeyDown(KeyCode.H)) showUI=!showUI;
            size=Mathf.Clamp(size-Input.mouseScrollDelta.y*size*.09f,1.1f,26);
            Vector3 right=transform.right, forward=Vector3.Cross(right,Vector3.up);
            float x=Input.GetAxisRaw("Horizontal"),y=Input.GetAxisRaw("Vertical");
            focus+=(right*x+forward*y)*Time.deltaTime*size*.65f;
            if(Input.GetMouseButton(1)||Input.GetMouseButton(2))
                focus-=(right*Input.GetAxis("Mouse X")+forward*Input.GetAxis("Mouse Y"))*size*.035f;
            Apply();
        }
        void Preset(int index)
        {
            comparing=index==3 && densityCenters!=null && densityCenters.Length==3;
            focus=comparing ? densityCenters[1]+Vector3.up : index==0 ? new Vector3(4,1,-5) : index==1 ? new Vector3(0,1,0) : new Vector3(1.6f,.15f,-2.4f);
            size=comparing ? 10.3f : index==0 ? 18f : index==1 ? 5.1f : 1.6f;
            SetTrees(index!=2);
            if(comparisonContext!=null) foreach(var item in comparisonContext) if(item) item.SetActive(!comparing);
            Apply();
        }
        void SetTrees(bool visible)
        {
            showCanopy=visible; if(canopy) canopy.gameObject.SetActive(visible);
            if(treeGeometry!=null) foreach(var tree in treeGeometry) if(tree) tree.SetActive(visible);
        }
        void ToggleCanopy() { SetTrees(!showCanopy); }
        void OnGUI()
        {
            if(!showUI) return;
            if(title==null)
            {
                title=new GUIStyle(GUI.skin.label){fontSize=23,fontStyle=FontStyle.Bold}; title.normal.textColor=new Color(.92f,.92f,.79f);
                body=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true}; body.normal.textColor=new Color(.75f,.80f,.73f);
            }
            GUI.color=new Color(.08f,.13f,.12f,.94f); GUI.DrawTexture(new Rect(18,18,372,257),Texture2D.whiteTexture); GUI.color=Color.white;
            GUI.Label(new Rect(34,28,340,34),"ЛЕС • ПЕРВАЯ ЭПОХА",title);
            GUI.Label(new Rect(34,68,340,45),"9 гексов · сравнение плотности 100 / 50 / 25%\nИзометрическая камера · угол фиксирован",body);
            if(GUI.Button(new Rect(34,119,102,28),"1 · Весь лес")) Preset(0);
            if(GUI.Button(new Rect(142,119,102,28),"2 · Центр")) Preset(1);
            if(GUI.Button(new Rect(250,119,122,28),"3 · Подлесок")) Preset(2);
            if(GUI.Button(new Rect(34,155,338,27),"4 · Сравнить исходный, 1/2 и 1/4")) Preset(3);
            if(GUI.Button(new Rect(34,190,338,27),showCanopy ? "C · Скрыть деревья для осмотра земли" : "C · Показать деревья")) ToggleCanopy();
            GUI.Label(new Rect(34,225,340,40),"Колесо — масштаб · ПКМ — перемещение\nWASD — перемещение · H — скрыть интерфейс",body);
            if(comparing)
            {
                var label=new GUIStyle(body){alignment=TextAnchor.MiddleCenter,fontSize=16};
                for(int i=0;i<3;i++)
                {
                    Vector3 anchor=view.WorldToScreenPoint(densityCenters[i]+new Vector3(-2.8f,-.9f,-2.8f));
                    if(anchor.z<=0) continue;
                    var rect=new Rect(anchor.x-101,Screen.height-anchor.y,202,54);
                    GUI.color=new Color(.08f,.13f,.12f,.94f); GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=Color.white;
                    GUI.Label(rect,$"{new[]{100,50,25}[i]}% · {densityObjects[i]} элементов\n{densityTrees[i]} деревьев",label);
                }
            }
            GUI.color=new Color(.08f,.13f,.12f,.88f); GUI.DrawTexture(new Rect(18,Screen.height-48,685,30),Texture2D.whiteTexture); GUI.color=Color.white;
            GUI.Label(new Rect(31,Screen.height-43,670,25),$"Всего: {trees} деревьев  /  {grassTufts} пучков травы  /  {flowers} цветов  /  {triangles:N0} треугольников",body);
        }
        IEnumerator CaptureUI(string file)
        {
            yield return new WaitForSeconds(3);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(file);
            Debug.Log("FOREST: UI frame captured, fixed orthographic camera active");
        }
        IEnumerator Capture(string folder)
        {
            Directory.CreateDirectory(folder); showUI=false;
            yield return null; yield return new WaitForEndOfFrame();
            for(int i=0;i<6;i++)
            {
                Preset(i>=4 ? 3 : Mathf.Min(i,2)); if(i==2) SetTrees(true); if(i==5) SetTrees(false); yield return null; yield return new WaitForEndOfFrame();
                RenderTexture rt=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);
                view.targetTexture=rt; view.Render();
                var previous=RenderTexture.active; RenderTexture.active=rt;
                var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,1600,1000),0,0); texture.Apply();
                File.WriteAllBytes(Path.Combine(folder,new[]{"forest-overview.png","forest-center.png","forest-undergrowth.png","forest-details.png","forest-density-comparison.png","forest-density-ground.png"}[i]),texture.EncodeToPNG());
                RenderTexture.active=previous; view.targetTexture=null; RenderTexture.ReleaseTemporary(rt); Destroy(texture);
            }
            Debug.Log("FOREST: screenshots captured; "+triangles+" triangles; "+trees+" trees");
            Application.Quit();
        }
    }
}
