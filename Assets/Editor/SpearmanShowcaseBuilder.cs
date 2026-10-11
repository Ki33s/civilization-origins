using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Origins.Editor
{
    public static partial class SpearmanModelBuilder
    {
        static Mesh StageMesh()
        {
            string path=ArtPath+"/Meshes/Stage.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path); if(mesh) return mesh;
            var profile=new[]{new Vector2(0,-.085f),new Vector2(1.13f,-.085f),new Vector2(1.17f,-.03f),new Vector2(1.14f,0),new Vector2(0,0)};
            mesh=UnityEngine.Object.Instantiate(DetailGeometry.Lathe("Spearman stage",profile,6)); mesh.name="Hex display base";
            AssetDatabase.CreateAsset(mesh,path); return mesh;
        }
        static void InstallGallery()
        {
            const string path="Assets/Scenes/DevelopmentGallery.unity";
            if(!File.Exists(path)) return;
            var scene=EditorSceneManager.OpenScene(path);
            var viewer=UnityEngine.Object.FindAnyObjectByType<DevelopmentGalleryViewer>();
            if(!viewer || viewer.groups.Length<2) throw new InvalidOperationException("Gallery unit group missing");
            var group=viewer.groups[1].transform;
            var old=group.Find("Unit_Spearman"); if(old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var sample=new GameObject("Unit_Spearman"); sample.transform.SetParent(group,false); sample.transform.localScale=Vector3.one*1.5f;
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),sample.transform);
            visual.transform.localRotation=Quaternion.Euler(0,180,0);
            Part(sample.transform,"Display base",StageMesh(),Vector3.zero,Vector3.one*1.2f,StageMaterial());
            sample.AddComponent<SpearmanPreviewMotion>();
            var item=sample.AddComponent<DevelopmentGalleryItem>(); item.title="Копейщик · неолит"; item.focusHeight=1.45f;
            item.description="Деревянный щит · каменное копьё · 6 клипов Animator";
            item.details="Ожидание, ходьба, выпад, защита, удар и падение. Это образец модели второй эпохи; производство и бой подключаются отдельно.";
            Vector3 right=new Vector3(.7071068f,0,-.7071068f),forward=new Vector3(.7071068f,0,.7071068f);
            int rows=Mathf.CeilToInt(group.childCount/2f);
            for(int i=0;i<group.childCount;i++) group.GetChild(i).localPosition=right*((i%2-.5f)*8.4f)+forward*((rows-1)*.5f-i/2)*8.5f;
            EditorSceneManager.SaveScene(scene,path);
        }
        static Material StageMaterial()
        {
            var existing=AssetDatabase.LoadAssetAtPath<Material>(ArtPath+"/Materials/Stage.mat");
            return existing ? existing : Material("Stage",new Color(.20f,.27f,.19f));
        }
        static void CreateShowcase()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Spearman art preview — no gameplay").AddComponent<ArtPreviewScene>();
            var copy=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            copy.transform.rotation=Quaternion.Euler(0,180,0);
            Part(new GameObject("Stage").transform,"Hex base",StageMesh(),Vector3.zero,Vector3.one,StageMaterial());
            var light=new GameObject("Warm key light").AddComponent<Light>(); light.type=LightType.Directional;
            light.transform.rotation=Quaternion.Euler(50,-30,0); light.intensity=1.25f; light.color=new Color(1,.92f,.80f); light.shadows=LightShadows.Soft;
            light.shadowBias=.12f; light.shadowNormalBias=.03f;
            RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(.69f,.74f,.75f);
            RenderSettings.ambientEquatorColor=new Color(.47f,.51f,.45f); RenderSettings.ambientGroundColor=new Color(.28f,.31f,.23f);
            RenderSettings.skybox=null; RenderSettings.fog=false;
            var camera=new GameObject("Fixed isometric camera").AddComponent<Camera>(); camera.tag="MainCamera";
            camera.orthographic=true; camera.orthographicSize=2.05f; camera.transform.rotation=Quaternion.Euler(35.2643897f,45,0);
            camera.transform.position=new Vector3(0,1.05f,0)-camera.transform.forward*12;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.075f,.13f,.12f); camera.nearClipPlane=.1f; camera.farClipPlane=40; camera.allowHDR=false;
            camera.gameObject.AddComponent<AudioListener>();
            var controller=camera.gameObject.AddComponent<SpearmanShowcaseController>(); controller.actor=copy.GetComponent<SpearmanVisual>(); controller.view=camera;
            controller.triangles=copy.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3); controller.renderers=copy.GetComponentsInChildren<MeshRenderer>().Length;
            EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
        }
        [MenuItem("Истоки/Разработка/Проверить копейщика")]
        public static void Validate()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(!prefab) throw new InvalidOperationException("Spearman prefab missing");
            var copy=UnityEngine.Object.Instantiate(prefab); var actor=copy.GetComponent<SpearmanVisual>();
            try
            {
                if(actor.animator.applyRootMotion || !actor.spearTip.IsChildOf(actor.rightHand)) throw new InvalidOperationException("Invalid rig or root motion");
                foreach(SpearmanPose pose in Enum.GetValues(typeof(SpearmanPose)))
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtPath+"/Animations/"+pose+".anim");
                    if(!clip || clip.length<.5f) throw new InvalidOperationException("Missing clip "+pose);
                    for(int i=0;i<=30;i++)
                    {
                        clip.SampleAnimation(copy,clip.length*i/30f);
                        foreach(var joint in copy.GetComponentsInChildren<Transform>()) if(!float.IsFinite(joint.position.sqrMagnitude)) throw new InvalidOperationException("Nonfinite joint "+pose);
                        if(copy.transform.position.sqrMagnitude>.00001f) throw new InvalidOperationException("Animated map root "+pose);
                        if(pose==SpearmanPose.Idle || pose==SpearmanPose.Walk)
                        {
                            if(actor.leftFoot.position.y<.098f || actor.rightFoot.position.y<.098f) throw new InvalidOperationException("Foot below floor "+pose);
                            if(actor.leftFoot.up.y<.998f || actor.rightFoot.up.y<.998f) throw new InvalidOperationException("Foot not level "+pose);
                        }
                    }
                }
                AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtPath+"/Animations/Thrust.anim").SampleAnimation(copy,1.1f*.48f);
                if(actor.spearTip.position.z-actor.spearGrip.position.z<1) throw new InvalidOperationException("Thrust contact is not forward");
                Debug.Log("SPEARMAN CHECKS: PASS — 186 pose samples, grounded level feet, fixed map root, attached weapon and forward thrust");
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
        [MenuItem("Истоки/Разработка/Открыть копейщика")]
        public static void OpenShowcase() { if(!File.Exists(ScenePath)) Generate(); else EditorSceneManager.OpenScene(ScenePath); }
        [MenuItem("Истоки/Разработка/Собрать просмотр копейщика")]
        public static void BuildPreview()
        {
            Generate(); ProjectSetup.Validate(); Directory.CreateDirectory("Builds/SpearmanPreview");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/SpearmanPreview/SpearmanPreview.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Spearman preview build failed");
            Debug.Log("SPEARMAN: playable Windows preview built");
            BuildGallery();
        }
        public static void BuildGallery()
        {
            const string gallery="Assets/Scenes/DevelopmentGallery.unity";
            if(!File.Exists(gallery)) return;
            Directory.CreateDirectory("Builds/DevelopmentGallery");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{gallery},locationPathName="Builds/DevelopmentGallery/DevelopmentGallery.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Gallery build failed");
            Debug.Log("SPEARMAN: development gallery updated and built");
        }
    }
}
