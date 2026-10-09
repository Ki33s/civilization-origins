using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Origins
{
    public sealed class TerrainPreviewCamera : MonoBehaviour
    {
        public TerrainTileArtwork[] tiles;
        public Vector3[] detailTargets;
        Camera view;
        Vector3 focus;
        float size=15.5f;
        int selected=-1;
        bool closeUp, treesVisible=true, showUI=true;
        bool wavesPaused;
        Vector3[] originalPositions,detailOffsets;
        readonly List<Material> waterMaterials=new List<Material>();
        GUIStyle title, body, caption;
        void Start()
        {
            view=GetComponent<Camera>(); Application.targetFrameRate=60;
            QualitySettings.antiAliasing=4; QualitySettings.shadows=ShadowQuality.All;
            QualitySettings.shadowResolution=ShadowResolution.VeryHigh; QualitySettings.shadowDistance=100;
            var catalog=Resources.Load<TerrainArtCatalog>("TerrainArtCatalog");
            if(!catalog || catalog.defaultDensity!=.25f || catalog.minDecorItems!=150 || catalog.maxDecorItems!=250 || !catalog.forest || !catalog.meadow || !catalog.plains || !catalog.hills || !catalog.mountain || !catalog.desert || !catalog.coast || !catalog.ocean || !catalog.swamp || !catalog.tundra)
                Debug.LogError("TERRAIN: approved artwork catalog is incomplete");
            originalPositions=new Vector3[tiles.Length]; detailOffsets=new Vector3[tiles.Length];
            for(int i=0;i<tiles.Length;i++)
            {
                originalPositions[i]=tiles[i].transform.position; detailOffsets[i]=detailTargets[i]-originalPositions[i];
                foreach(var renderer in tiles[i].GetComponentsInChildren<MeshRenderer>(true))
                    if(renderer.sharedMaterial && renderer.sharedMaterial.shader.name=="Origins/Simple Waves") waterMaterials.Add(renderer.material);
            }
            Preset(11);
            string[] args=System.Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)
            {
                if(args[i]=="-terrain-capture") StartCoroutine(Capture(args[i+1]));
                if(args[i]=="-terrain-ui-capture") StartCoroutine(CaptureUI(args[i+1]));
            }
        }
        void Apply()
        {
            view.orthographic=true; view.orthographicSize=size;
            transform.rotation=Quaternion.Euler(35.2643897f,45,0); transform.position=focus-transform.forward*65;
        }
        void Trees(bool visible)
        {
            treesVisible=visible;
            foreach(var tile in tiles) if(tile.trees) tile.trees.gameObject.SetActive(visible);
        }
        void Preset(int key)
        {
            selected=key==11 ? -2 : key-1; closeUp=false; Trees(true);
            Vector3 right=new Vector3(.7071068f,0,-.7071068f),forward=new Vector3(.7071068f,0,.7071068f);
            for(int i=0;i<tiles.Length;i++)
            {
                tiles[i].transform.position=selected==-2 && i>=4 ? right*((i%2-.5f)*10.6f)+forward*((1-(i-4)/2)*11.8f) : originalPositions[i];
                tiles[i].gameObject.SetActive(selected==-2 ? i>=4 : selected<0 || selected==i);
            }
            focus=selected<0 ? new Vector3(0,1.4f,0) : tiles[selected].transform.position+Vector3.up*(selected==4 ? 1.7f : selected==0 ? 1.25f : selected==3 ? .9f : selected==5 ? .6f : .15f);
            size=selected==-2 ? 15.5f : selected<0 ? 20 : selected==4 ? 6.5f : selected==0 || selected==9 ? 5.8f : selected==3 ? 5.7f : 5.1f;
            Apply();
        }
        void Detail()
        {
            if(closeUp) { Preset(selected+1); return; }
            if(selected<0) Preset(selected==-2 ? 6 : 2);
            closeUp=true; focus=tiles[selected].transform.position+detailOffsets[selected]+Vector3.up*.12f; size=1.75f;
            if(selected==0 || selected==9) Trees(false); Apply();
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.Alpha0)) Preset(0);
            if(Input.GetKeyDown(KeyCode.Alpha1)) Preset(1);
            if(Input.GetKeyDown(KeyCode.Alpha2)) Preset(2);
            if(Input.GetKeyDown(KeyCode.Alpha3)) Preset(3);
            if(Input.GetKeyDown(KeyCode.Alpha4)) Preset(4);
            if(Input.GetKeyDown(KeyCode.Alpha5)) Preset(5);
            if(Input.GetKeyDown(KeyCode.Alpha6)) Preset(6);
            if(Input.GetKeyDown(KeyCode.Alpha7)) Preset(7);
            if(Input.GetKeyDown(KeyCode.Alpha8)) Preset(8);
            if(Input.GetKeyDown(KeyCode.Alpha9)) Preset(9);
            if(Input.GetKeyDown(KeyCode.T)) Preset(10);
            if(Input.GetKeyDown(KeyCode.N)) Preset(11);
            if(Input.GetKeyDown(KeyCode.F)) Detail();
            if(Input.GetKeyDown(KeyCode.C)) Trees(!treesVisible);
            if(Input.GetKeyDown(KeyCode.H)) showUI=!showUI;
            if(Input.GetKeyDown(KeyCode.P)) ToggleWaves();
            size=Mathf.Clamp(size-Input.mouseScrollDelta.y*size*.09f,1.1f,23);
            Vector3 right=transform.right,forward=Vector3.Cross(right,Vector3.up);
            focus+=(right*Input.GetAxisRaw("Horizontal")+forward*Input.GetAxisRaw("Vertical"))*Time.deltaTime*size*.65f;
            if(Input.GetMouseButton(1)||Input.GetMouseButton(2)) focus-=(right*Input.GetAxis("Mouse X")+forward*Input.GetAxis("Mouse Y"))*size*.035f;
            Apply();
        }
        void OnGUI()
        {
            if(!showUI || tiles==null || view==null) return;
            if(title==null)
            {
                title=new GUIStyle(GUI.skin.label){fontSize=23,fontStyle=FontStyle.Bold}; title.normal.textColor=new Color(.92f,.92f,.79f);
                body=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true}; body.normal.textColor=new Color(.75f,.80f,.73f);
                caption=new GUIStyle(body){alignment=TextAnchor.MiddleCenter,fontSize=16};
            }
            Panel(new Rect(18,18,372,362));
            GUI.Label(new Rect(34,28,340,34),"МИР • КЛЕТКИ",title);
            GUI.Label(new Rect(34,68,340,45),"Десять видов местности · живая вода\nИзометрическая камера · угол фиксирован",body);
            if(GUI.Button(new Rect(34,120,164,27),"0 · Все десять")) Preset(0);
            if(GUI.Button(new Rect(207,120,165,27),"N · Новые клетки")) Preset(11);
            for(int i=0;i<tiles.Length;i++)
            {
                string label=tiles[i].kind==TerrainArtKind.Coast ? "Берег" : tiles[i].displayName;
                if(GUI.Button(new Rect(34+(i%4)*86,155+(i/4)*35,80,27),(i==9 ? "T" : (i+1).ToString())+" · "+label)) Preset(i+1);
            }
            if(GUI.Button(new Rect(34,260,164,27),closeUp ? "F · Вся клетка" : "F · Рассмотреть детали")) Detail();
            if(GUI.Button(new Rect(207,260,165,27),treesVisible ? "C · Скрыть деревья" : "C · Показать деревья")) Trees(!treesVisible);
            if(GUI.Button(new Rect(34,295,338,27),wavesPaused ? "P · Запустить волны" : "P · Остановить волны")) ToggleWaves();
            GUI.Label(new Rect(34,330,340,40),"Колесо — масштаб · ПКМ / WASD — сдвиг\nH — скрыть интерфейс",body);
            if(!closeUp)
            {
                foreach(var tile in tiles) if(tile.gameObject.activeSelf)
                {
                    Vector3 p=view.WorldToScreenPoint(tile.transform.position+new Vector3(-2.8f,-.9f,-2.8f));
                    var rect=new Rect(p.x-101,Screen.height-p.y,202,51);
                    string info=tile.kind==TerrainArtKind.Desert ? tile.majorFeatures+" дюны · "+(tile.decorItems-tile.majorFeatures)+" деталей" : tile.kind==TerrainArtKind.Ocean ? "Движущиеся волны" : tile.kind==TerrainArtKind.Coast ? tile.decorItems+" деталей · прибой" : tile.decorItems+" элементов";
                    if(p.z>0 && rect.y<Screen.height-65 && rect.y>0) { Panel(rect); GUI.Label(rect,tile.displayName+"\n"+info,caption); }
                }
            }
            int triangles=0,decor=0;
            foreach(var tile in tiles) if(tile.gameObject.activeSelf) { triangles+=tile.triangles-(treesVisible ? 0 : tile.treeTriangles); decor+=tile.decorItems; }
            Panel(new Rect(18,Screen.height-48,680,30));
            GUI.Label(new Rect(31,Screen.height-43,660,25),$"{(selected==-2 ? "Новые клетки" : selected<0 ? "Все клетки" : tiles[selected].displayName)}  /  {decor} элементов декора  /  {triangles:N0} треугольников",body);
        }
        void WaterTime(float time) { foreach(var material in waterMaterials) material.SetFloat("_PreviewTime",time); }
        void ToggleWaves() { wavesPaused=!wavesPaused; WaterTime(wavesPaused ? Time.time : -1); }
        void OnDestroy() { foreach(var material in waterMaterials) Destroy(material); }
        static void Panel(Rect rect)
        { GUI.color=new Color(.08f,.13f,.12f,.94f); GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=Color.white; }
        IEnumerator CaptureUI(string file)
        {
            yield return new WaitForSeconds(3); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(file);
            Debug.Log("TERRAIN: UI captured, orthographic camera active");
        }
        IEnumerator Capture(string folder)
        {
            Directory.CreateDirectory(folder); showUI=false; yield return null;
            string[] names={"terrain-overview","forest-25","meadow-25","plains-25","hills-25","mountain","desert","coast","ocean","swamp","tundra","new-biomes","desert-detail","coast-detail","swamp-detail","tundra-detail","ocean-waves-0","ocean-waves-4"};
            int[] presets={0,1,2,3,4,5,6,7,8,9,10,11,6,7,9,10,8,8};
            for(int i=0;i<names.Length;i++)
            {
                Preset(presets[i]);
                WaterTime(i==17 ? 4 : 0); if(i>=12 && i<=15) Detail();
                yield return null; yield return new WaitForEndOfFrame();
                RenderTexture rt=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);
                view.targetTexture=rt; view.Render(); var previous=RenderTexture.active; RenderTexture.active=rt;
                var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder,names[i]+".png"),pixels.EncodeToPNG());
                RenderTexture.active=previous; view.targetTexture=null; RenderTexture.ReleaseTemporary(rt); Destroy(pixels);
            }
            Debug.Log("TERRAIN: eighteen views captured; biome budgets and water phases rendered"); Application.Quit();
        }
    }
}
