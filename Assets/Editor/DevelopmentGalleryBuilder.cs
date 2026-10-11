using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Origins.Editor
{
    public static class DevelopmentGalleryBuilder
    {
        const string ScenePath="Assets/Scenes/DevelopmentGallery.unity";
        const string ArtRoot="Assets/Art/Development/Generated";
        static readonly Dictionary<UnityEngine.Object,UnityEngine.Object> saved=new Dictionary<UnityEngine.Object,UnityEngine.Object>();
        static readonly List<UnityEngine.Object> temporary=new List<UnityEngine.Object>();
        static int materialIndex;
        [MenuItem("Истоки/Разработка/Открыть галерею")]
        public static void OpenGallery()
        {
            if(!File.Exists(ScenePath)) Generate(); else EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("Истоки/Разработка/Пересобрать галерею")]
        public static void Generate()
        {
            Directory.CreateDirectory(ArtRoot+"/Meshes"); Directory.CreateDirectory(ArtRoot+"/Materials"); Directory.CreateDirectory(ArtRoot+"/Prefabs"); AssetDatabase.Refresh();
            var catalog=Resources.Load<TerrainArtCatalog>("TerrainArtCatalog");
            if(!catalog) { ForestArt.TerrainShowcaseBuilder.Generate(); catalog=Resources.Load<TerrainArtCatalog>("TerrainArtCatalog"); }
            var content=ContentLoader.Load(); saved.Clear(); temporary.Clear(); materialIndex=0;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Development gallery — isolated from gameplay").AddComponent<ArtPreviewScene>();
            var groups=new GameObject[7]; for(int i=0;i<groups.Length;i++) groups[i]=new GameObject(new[]{"Tiles","Units","Buildings","Residents","Full world","Decor models","Resources"}[i]);
            groups[4].AddComponent<WorldArtPreview>();
            groups[0].AddComponent<EditableTerrainSamples>(); groups[5].AddComponent<DecorModelSamples>();
            var authoring=new GameObject("Temporary model authoring"); var presenter=new WorldView(new Game(12026,null,content),authoring.transform,false);
            int index=0;
            foreach(UnitKind kind in Enum.GetValues(typeof(UnitKind)))
            {
                Transform model=presenter.CreateUnitPreview(kind); presenter.AddPreviewPlinth(model);
                if(kind==UnitKind.Spearman) model.gameObject.AddComponent<SpearmanPreviewMotion>();
                else if(kind==UnitKind.Trader) model.gameObject.AddComponent<PreviewTraderMotion>();
                else model.gameObject.AddComponent<PreviewUnitMotion>();
                var unit=new Unit{Kind=kind};
                AddModel(model,"Unit_"+kind,unit.Name,unit.Movement+" очка движения",groups[1].transform,index++,2,3.5f,1.2f);
                if(kind==UnitKind.Trader) groups[1].transform.GetChild(groups[1].transform.childCount-1).GetComponent<DevelopmentGalleryItem>().details="Образец будущей торговли: человек с мешком оставляет сухую тропу. Реки существуют до движения. В каменном веке производство торговца и торговые доходы ещё закрыты по OpenSpec.";
            }
            index=0;
            foreach(var project in content.Projects) if(!project.Unit.HasValue)
            {
                Transform model=presenter.CreateBuildingPreview(project.Id); presenter.AddPreviewPlinth(model);
                string role=project.Wonder ? "уникальное чудо" : project.Id=="workshop" || project.Id=="potter" || project.Id=="fishhut" ? "рабочее место" : "пассивный эффект";
                AddModel(model,"Building_"+project.Id,project.Name,project.Cost+" труда · "+role,groups[2].transform,index++,3,3.5f,.8f);
                groups[2].transform.GetChild(groups[2].transform.childCount-1).GetComponent<DevelopmentGalleryItem>().details=project.Description;
            }
            index=0;
            foreach(string profession in new[]{"Собиратель","Земледелец","Каменотёс","Рыбак","Гончар","Обработчик шкур","Пастух","Охотник","Каменотёс","Каменотёс","Каменотёс"})
            {
                int tool=index>=8 ? index-7 : 0;
                Transform model=presenter.CreateResidentPreview(profession,tool); presenter.AddPreviewPlinth(model);
                var motion=model.gameObject.AddComponent<PreviewResidentMotion>(); motion.actor=model.Find("Resident actor");
                string title=profession=="Каменотёс" ? profession+" · "+new[]{"камень","молоток","каменная кирка","бронзовая кирка"}[tool] : profession;
                AddModel(model,"Resident_"+index,title,"Ходьба · работа · отдых",groups[3].transform,index++,3,3.5f,.6f);
            }
            index=0;
            foreach(Resource kind in Enum.GetValues(typeof(Resource))) if(kind!=Resource.None)
            {
                Transform model=presenter.CreateResourcePreview(kind); presenter.AddPreviewPlinth(model);
                string title=kind==Resource.Berries ? "Ягоды" : kind==Resource.Grain ? "Колосья" : kind==Resource.Stone ? "Камни" : kind==Resource.Discovery ? "Древняя находка" : kind==Resource.Cattle ? "Стадо" : kind==Resource.Fish ? "Рыба" : kind==Resource.Game ? "Дичь" : "Пушнина";
                AddModel(model,"Resource_"+kind,title,"Подробная игровая модель",groups[6].transform,index++,3,5f,.6f);
            }
            UnityEngine.Object.DestroyImmediate(authoring);
            foreach(var obj in temporary) if(obj && !EditorUtility.IsPersistent(obj)) UnityEngine.Object.DestroyImmediate(obj);
            var light=new GameObject("Gallery sun").AddComponent<Light>(); light.type=LightType.Directional;
            light.transform.rotation=Quaternion.Euler(53,-38,0); light.color=new Color(1,.93f,.78f); light.intensity=1.3f; light.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(.65f,.72f,.78f); RenderSettings.ambientEquatorColor=new Color(.48f,.53f,.45f); RenderSettings.ambientGroundColor=new Color(.29f,.33f,.23f);
            RenderSettings.skybox=null; RenderSettings.fog=false;
            var camera=new GameObject("Fixed isometric gallery camera").AddComponent<Camera>(); camera.tag="MainCamera"; camera.orthographic=true; camera.orthographicSize=20;
            camera.transform.rotation=Quaternion.Euler(35.2643897f,45,0); camera.transform.position=Vector3.up-camera.transform.forward*65;
            camera.nearClipPlane=.1f; camera.farClipPlane=160; camera.backgroundColor=new Color(.094f,.149f,.137f); camera.clearFlags=CameraClearFlags.SolidColor; camera.allowHDR=false;
            camera.gameObject.AddComponent<AudioListener>(); camera.gameObject.AddComponent<DevelopmentGalleryViewer>().groups=groups;
            for(int i=0;i<groups.Length;i++) groups[i].SetActive(i==0);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene,ScenePath);
            Debug.Log("GALLERY: 10 tiles, "+groups[1].transform.childCount+" units, "+groups[2].transform.childCount+" buildings, "+groups[3].transform.childCount+" resident/tool examples saved");
        }
        static void AddModel(Transform model,string key,string title,string description,Transform group,int index,int rows,float scale,float height)
        {
            var item=model.gameObject.AddComponent<DevelopmentGalleryItem>(); item.title=title; item.description=description; item.focusHeight=height;
            int triangles=0; foreach(var filter in model.GetComponentsInChildren<MeshFilter>()) triangles+=filter.sharedMesh.triangles.Length/3;
            item.description+=" · "+triangles+" треуг.";
            int meshIndex=0;
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>()) filter.sharedMesh=(Mesh)Persist(filter.sharedMesh,ArtRoot+"/Meshes/"+key+"_"+(meshIndex++)+".asset");
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                if(renderer is LineRenderer line) line.useWorldSpace=false;
                renderer.sharedMaterial=(Material)Persist(renderer.sharedMaterial,ArtRoot+"/Materials/Material_"+materialIndex+".mat");
            }
            var prefab=PrefabUtility.SaveAsPrefabAsset(model.gameObject,ArtRoot+"/Prefabs/"+key+".prefab");
            UnityEngine.Object.DestroyImmediate(model.gameObject);
            var copy=(GameObject)PrefabUtility.InstantiatePrefab(prefab,group); copy.transform.localScale=Vector3.one*scale;
            Vector3 right=new Vector3(.7071068f,0,-.7071068f),forward=new Vector3(.7071068f,0,.7071068f);
            int columns=group.name=="Units" ? 2 : 3;
            int actualRows=group.name=="Units" ? Mathf.CeilToInt(Enum.GetValues(typeof(UnitKind)).Length/2f) : group.name=="Residents" ? 4 : 3;
            copy.transform.localPosition=right*((index%columns-(columns-1)*.5f)*8.4f)+forward*((actualRows-1)*.5f-index/columns)*8.5f;
        }
        static UnityEngine.Object Persist(UnityEngine.Object obj,string path)
        {
            if(!obj || EditorUtility.IsPersistent(obj)) return obj;
            UnityEngine.Object result; if(saved.TryGetValue(obj,out result)) return result;
            result=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if(result) { EditorUtility.CopySerialized(obj,result); EditorUtility.SetDirty(result); temporary.Add(obj); }
            else { AssetDatabase.CreateAsset(obj,path); result=obj; }
            saved[obj]=result; if(obj is Material) materialIndex++;
            return result;
        }
        [MenuItem("Истоки/Разработка/Собрать игру и галерею")]
        public static void BuildGameplayAndGallery()
        {
            ProjectSetup.BuildWindows(); Generate(); Directory.CreateDirectory("Builds/DevelopmentGallery");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/DevelopmentGallery/DevelopmentGallery.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Gallery build failed");
            Debug.Log("GALLERY: standalone build succeeded");
        }
    }
}
