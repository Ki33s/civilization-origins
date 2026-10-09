using System.Collections;
using System.IO;
using UnityEngine;

namespace Origins
{
    public sealed partial class DevelopmentGalleryViewer : MonoBehaviour
    {
        public GameObject[] groups;
        public string[] titles={"Клетки","Юниты","Постройки","Жители","Мир","Модели","Ресурсы"};
        Camera view;
        DevelopmentGalleryItem[] items;
        Vector3 focus;
        float zoom;
        int tab,selected=-1;
        bool showUI=true;
        GUIStyle label,small;
        TerrainDecorPanel decorPanel;
        void Start()
        {
            view=GetComponent<Camera>(); Application.targetFrameRate=60; QualitySettings.antiAliasing=4; QualitySettings.lodBias=1;
            decorPanel=new TerrainDecorPanel{Applied=RefreshDecor,InspectModel=InspectDecorModel,InspectBiome=InspectBiome};
            SwitchTab(0);
            var args=System.Environment.GetCommandLineArgs();
            decorPanel.open=System.Array.IndexOf(args,"-gallery-edit")>=0;
            for(int i=0;i<args.Length-1;i++)
            {
                if(args[i]=="-gallery-tab" && int.TryParse(args[i+1],out int index)) SwitchTab(Mathf.Clamp(index,0,groups.Length-1));
                if(args[i]=="-gallery-focus" && int.TryParse(args[i+1],out int sample) && sample>=0 && sample<items.Length) FocusItem(sample);
                if(args[i]=="-decor-smoke") StartCoroutine(CheckDecor(args[i+1]));
                if(args[i]=="-gallery-capture") StartCoroutine(Capture(args[i+1]));
                if(args[i]=="-gallery-ui-capture") StartCoroutine(CaptureUI(args[i+1]));
            }
        }
        void SwitchTab(int index)
        {
            tab=index; selected=-1;
            for(int i=0;i<groups.Length;i++) groups[i].SetActive(i==tab);
            if(tab==0) groups[0].GetComponent<EditableTerrainSamples>().Ensure();
            if(tab==5) groups[5].GetComponent<DecorModelSamples>().Ensure();
            items=groups[tab].GetComponentsInChildren<DevelopmentGalleryItem>(true);
            ShowAll();
        }
        void ShowAll()
        {
            selected=-1; foreach(var item in items) item.gameObject.SetActive(true);
            var world=groups[tab].GetComponent<WorldArtPreview>();
            focus=world ? world.Center : new Vector3(0,1,0); zoom=world ? 21 : tab==0 || tab==5 ? 20 : tab==3 ? 15 : 12; Apply();
        }
        void FocusItem(int index)
        {
            selected=index; foreach(var item in items) item.gameObject.SetActive(item==items[index]);
            focus=items[index].transform.position+Vector3.up*items[index].focusHeight;
            zoom=tab==0 ? 5.8f : 3.8f; Apply();
            if(tab==0)
            {
                var inventory=items[index].GetComponentInChildren<DecorTileInventory>();
                if(inventory && System.Enum.TryParse(inventory.biome,out Terrain type)) decorPanel.Choose(type);
            }
            if(tab==5)
            {
                var b=new Bounds(items[index].transform.position,Vector3.zero); foreach(var renderer in items[index].GetComponentsInChildren<Renderer>()) b.Encapsulate(renderer.bounds);
                focus=b.center; zoom=Mathf.Clamp(b.extents.magnitude*1.3f,.3f,7); Apply();
            }
        }
        void InspectDecorModel(string id)
        {
            SwitchTab(5); for(int i=0;i<items.Length;i++) if(items[i].name==id) { FocusItem(i); return; }
        }
        void InspectBiome(Terrain type)
        {
            SwitchTab(0);
            for(int i=0;i<items.Length;i++) if(items[i].GetComponentInChildren<DecorTileInventory>().biome==type.ToString()) { FocusItem(i); return; }
        }
        void RefreshDecor()
        {
            Vector3 oldFocus=focus; float oldZoom=zoom; int oldSelected=selected;
            groups[0].GetComponent<EditableTerrainSamples>().Rebuild();
            var world=groups[4].GetComponent<WorldArtPreview>(); if(world.IsBuilt) world.RefreshDecor();
            items=groups[tab].GetComponentsInChildren<DevelopmentGalleryItem>(true);
            if(tab==0 && oldSelected>=0) FocusItem(oldSelected); else { focus=oldFocus; zoom=oldZoom; Apply(); }
        }
        void Apply()
        {
            view.orthographic=true; view.orthographicSize=zoom;
            transform.rotation=Quaternion.Euler(35.2643897f,45,0);
            Vector3 editorOffset=decorPanel!=null && decorPanel.open ? transform.right*(zoom*view.aspect*416/Screen.width) : Vector3.zero;
            transform.position=focus+editorOffset-transform.forward*65;
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.E) && GUIUtility.keyboardControl==0) decorPanel.open=!decorPanel.open;
            if(Input.GetKeyDown(KeyCode.Escape) && decorPanel.open) { decorPanel.open=false; GUIUtility.keyboardControl=0; }
            if(GUIUtility.keyboardControl!=0 || decorPanel.OverMouse()) { Apply(); return; }
            for(int i=0;i<groups.Length;i++) if(Input.GetKeyDown(KeyCode.Alpha1+i)) SwitchTab(i);
            if(Input.GetKeyDown(KeyCode.Alpha0)) ShowAll();
            if(Input.GetKeyDown(KeyCode.F)) { if(selected>=0) ShowAll(); else if(items.Length>0) FocusItem(0); }
            if(Input.GetKeyDown(KeyCode.N) && items.Length>0) FocusItem((selected+1)%items.Length);
            if(Input.GetKeyDown(KeyCode.N) && tab==4) { groups[tab].GetComponent<WorldArtPreview>().Regenerate(); ShowAll(); }
            if(Input.GetKeyDown(KeyCode.H)) showUI=!showUI;
            zoom=Mathf.Clamp(zoom-Input.mouseScrollDelta.y*zoom*.1f,1.1f,33);
            Vector3 right=transform.right,forward=Vector3.Cross(right,Vector3.up);
            focus+=(right*Input.GetAxisRaw("Horizontal")+forward*Input.GetAxisRaw("Vertical"))*Time.deltaTime*zoom*.6f;
            if(Input.GetMouseButton(1)||Input.GetMouseButton(2)) focus-=(right*Input.GetAxis("Mouse X")+forward*Input.GetAxis("Mouse Y"))*zoom*.035f;
            Apply();
        }
        void OnGUI()
        {
            if(!showUI || view==null) return;
            if(label==null)
            {
                label=new GUIStyle(GUI.skin.label){fontSize=19,alignment=TextAnchor.MiddleCenter,wordWrap=true}; label.normal.textColor=new Color(.90f,.91f,.83f);
                small=new GUIStyle(label){fontSize=14}; small.normal.textColor=new Color(.70f,.77f,.73f);
            }
            Panel(new Rect(12,12,Screen.width-24,72));
            GUI.Label(new Rect(25,18,205,26),"ГАЛЕРЕЯ",label);
            float tabWidth=Mathf.Min(145,(Screen.width-430f)/groups.Length);
            for(int i=0;i<groups.Length;i++) if(GUI.Button(new Rect(240+i*tabWidth,22,tabWidth-5,32),(i+1)+" · "+titles[i])) SwitchTab(i);
            if(GUI.Button(new Rect(Screen.width-178,22,150,32),"Параметры [E]")) decorPanel.open=!decorPanel.open;
            GUI.Label(new Rect(26,52,Screen.width-52,24),tab==4 ? "N — новый мир · 0 — вся карта · Колесо — масштаб · WASD / ПКМ — камера · H — интерфейс" : "0 — все образцы вкладки · N — следующий · F — крупный план · Колесо / WASD — камера · H — интерфейс",small);
            foreach(var item in items) if(item.gameObject.activeSelf)
            {
                Vector3 p=view.WorldToScreenPoint(item.transform.position+new Vector3(-2.4f,-1,-2.4f));
                float width=tab==5 && selected<0 ? 130 : 260;
                var rect=new Rect(p.x-width*.5f,Screen.height-p.y,width,54);
                if(p.z<=0 || rect.y<90 || rect.y>Screen.height-100) continue;
                Panel(rect);
                if(GUI.Button(new Rect(rect.x,rect.y,rect.width,27),item.title,label)) FocusItem(System.Array.IndexOf(items,item));
                GUI.Label(new Rect(rect.x,rect.y+27,rect.width,25),item.description,new GUIStyle(small){fontSize=12,wordWrap=false});
            }
            if(selected>=0 && !string.IsNullOrEmpty(items[selected].details))
            {
                var info=new Rect(24,100,Screen.width-(decorPanel.open ? 464 : 48),72); Panel(info);
                GUI.Label(new Rect(info.x+14,info.y+8,info.width-28,info.height-16),items[selected].details,label);
            }
            Panel(new Rect(12,Screen.height-42,Screen.width-24,30));
            GUI.Label(new Rect(25,Screen.height-40,Screen.width-50,27),tab==4 ? groups[tab].GetComponent<WorldArtPreview>().Summary : tab==5 && selected>=0 ? items[selected].title+" · "+items[selected].description : titles[tab]+" · "+items.Length+" образцов · Изометрия · Текущие игровые модели",small);
            decorPanel.Draw();
        }
        static void Panel(Rect rect) { GUI.color=new Color(.08f,.13f,.12f,.94f); GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=Color.white; }
        IEnumerator CaptureUI(string file)
        {
            yield return new WaitForSeconds(3); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(file); Debug.Log("GALLERY: UI captured");
        }
        IEnumerator Capture(string folder)
        {
            Directory.CreateDirectory(folder); showUI=false; yield return null;
            if(Object.FindFirstObjectByType<OriginsController>()!=null) throw new System.InvalidOperationException("Gameplay leaked into the gallery");
            string[] names={"tiles","units","buildings","residents","scout-detail","resident-detail","world","world-detail","models","forest-detail","tundra-detail","desert-detail","workshop-detail","potter-detail","fishhut-detail","stonehenge-detail","pyramids-detail","gardens-detail","slinger-detail","shepherd-detail","miner-stone","miner-hammer","miner-pick","miner-bronze","resources","berries-detail","grain-detail","ancient-site-detail","herd-detail"};
            int[] focuses={-1,-1,-1,-1,0,0,-1,-1,-1,0,9,5,1,4,5,6,7,8,4,6,2,8,9,10,-1,0,3,5,7};
            for(int i=0;i<names.Length;i++)
            {
                SwitchTab(i>=24 ? 6 : i>=19 ? 3 : i==18 ? 1 : i>=12 ? 2 : i<4 ? i : i==4 ? 1 : i==5 ? 3 : i<8 ? 4 : i==8 ? 5 : 0);
                if(focuses[i]>=0) FocusItem(focuses[i]);
                if(i==1 || i==2 || i==3)
                {
                    var statistics=new System.Text.StringBuilder();
                    foreach(var item in items)
                    {
                        int triangles=0; foreach(var mesh in item.GetComponentsInChildren<MeshFilter>(true)) triangles+=mesh.sharedMesh.triangles.Length/3;
                        statistics.AppendLine(item.title+": "+triangles+" triangles, "+item.GetComponentsInChildren<Renderer>(true).Length+" mesh parts");
                    }
                    File.WriteAllText(Path.Combine(folder,names[i]+"-statistics.txt"),statistics.ToString());
                }
                if(i>=19 && i<24)
                {
                    var motion=items[selected].GetComponent<PreviewResidentMotion>(); motion.enabled=false; motion.actor.localPosition=Vector3.zero; motion.actor.localRotation=Quaternion.identity;
                    motion.actor.GetComponentInChildren<ArticulatedActor>().Pose(1.1f,false,true);
                }
                if(i==18) { items[selected].GetComponent<PreviewUnitMotion>().enabled=false; foreach(var actor in items[selected].GetComponentsInChildren<ArticulatedActor>()) actor.Pose(.75f,false,false,false,true); }
                if(i==6)
                {
                    var world=groups[tab].GetComponent<WorldArtPreview>();
                    if(world.TileCount!=560) throw new System.InvalidOperationException("Incomplete artwork world");
                    File.WriteAllText(Path.Combine(folder,"world-stats.txt"),world.Statistics);
                }
                if(i==7) { focus=new Vector3(24,0,15); zoom=4.5f; Apply(); }
                yield return null; yield return new WaitForEndOfFrame();
                var rt=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);
                view.targetTexture=rt; view.Render(); var previous=RenderTexture.active; RenderTexture.active=rt;
                var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder,names[i]+".png"),pixels.EncodeToPNG());
                RenderTexture.active=previous; view.targetTexture=null; RenderTexture.ReleaseTemporary(rt); Destroy(pixels);
            }
            Debug.Log("GALLERY: all seven categories rendered, gameplay bootstrap isolated"); Application.Quit();
        }
    }
}
