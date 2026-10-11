using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Origins.Editor
{
    public static class LonghouseModelBuilder
    {
        public const string PrefabPath="Assets/Resources/Buildings/Longhouse.prefab";
        public const string ScenePath="Assets/Scenes/LonghouseShowcase.unity";
        const string Art="Assets/Art/Buildings/Longhouse";
        static readonly Dictionary<string,Material> palette=new Dictionary<string,Material>();
        static System.Random random;
        static Transform house,roof,interior,yard;
        static Material wood,lightWood,rope,mud,dark,straw,clay,stone;

        [MenuItem("Истоки/Разработка/Собрать длинный дом")]
        public static void Generate()
        {
            foreach(string path in new[]{Art+"/Meshes",Art+"/Materials","Assets/Resources/Buildings"}) Directory.CreateDirectory(path);
            AssetDatabase.Refresh(); palette.Clear(); random=new System.Random(12026);
            wood=Paint("Timber",new Color(.32f,.19f,.10f)); lightWood=Paint("Cut wood",new Color(.53f,.35f,.19f));
            rope=Paint("Rope",new Color(.64f,.51f,.29f)); mud=Paint("Clay plaster",new Color(.55f,.40f,.26f));
            dark=Paint("Recess",new Color(.13f,.095f,.065f)); straw=Paint("Straw",new Color(.63f,.46f,.22f));
            clay=Paint("Pottery",new Color(.52f,.26f,.14f)); stone=Paint("Stone",new Color(.43f,.44f,.38f));
            var reed=new Material[5]; for(int i=0;i<reed.Length;i++) reed[i]=Paint("Thatch "+i,new Color(.57f+i*.024f,.39f+i*.023f,.17f+i*.015f));
            house=new GameObject("Longhouse").transform; roof=Node(house,"Removable roof"); interior=Node(house,"Interior"); yard=Node(house,"Courtyard");
            var walls=Node(house,"Walls and timber frame");
            Box(walls,"Earthen raised floor",new Vector3(0,.08f,0),new Vector3(5.6f,.16f,2.95f),mud);
            // Front openings are actual gaps between panels, rather than painted black doors.
            float[] posts={-2.7f,-1.8f,-.7f,.7f,1.8f,2.7f};
            for(int side=-1;side<=1;side+=2)
            {
                float z=side*1.36f;
                for(int p=0;p<posts.Length;p++)
                {
                    Beam(walls,"Load-bearing post",new Vector3(posts[p],.12f,z),new Vector3(posts[p],1.99f,z),.078f,wood,20);
                    for(int loop=0;loop<4;loop++) Band(walls,new Vector3(posts[p],1.72f+loop*.023f,z),.083f,rope);
                    Band(walls,new Vector3(posts[p],.29f,z),.083f,rope);
                }
                Beam(walls,"Continuous sill",new Vector3(-2.8f,.22f,z),new Vector3(2.8f,.22f,z),.069f,wood,20);
                Beam(walls,"Eaves beam",new Vector3(-2.86f,1.85f,z),new Vector3(2.86f,1.85f,z),.085f,wood,24);
                for(int panel=0;panel<posts.Length-1;panel++)
                {
                    float left=posts[panel]+.08f,right=posts[panel+1]-.08f,center=(left+right)*.5f;
                    if(side<0 && (panel==1 || panel==3))
                    {
                        Box(walls,"Door header plaster",new Vector3(center,1.77f,z),new Vector3(right-left,.18f,.11f),mud);
                        Door(walls,center,z);
                    }
                    else Wattle(walls,new Vector3(center,1.02f,z),right-left,1.5f,side<0);
                }
            }
            for(int side=-1;side<=1;side+=2)
            {
                var end=Node(walls,side<0 ? "West gable" : "East gable"); end.localPosition=new Vector3(side*2.7f,0,0); end.localRotation=Quaternion.Euler(0,90,0);
                Wattle(end,new Vector3(0,1.03f,0),2.55f,1.5f,side<0);
                Part(end,"Gable plaster",Gable(),new Vector3(0,1.78f,0),Vector3.one,mud);
                Beam(end,"Gable tie",new Vector3(-1.35f,1.81f,-.02f),new Vector3(1.35f,1.81f,-.02f),.075f,wood);
                Beam(end,"Gable king post",new Vector3(0,1.83f,-.055f),new Vector3(0,3.12f,-.055f),.073f,wood);
                Beam(end,"Gable diagonal",new Vector3(-1.31f,1.85f,-.055f),new Vector3(0,3.13f,-.055f),.062f,wood);
                Beam(end,"Gable diagonal",new Vector3(1.31f,1.85f,-.055f),new Vector3(0,3.13f,-.055f),.062f,wood);
            }
            for(int rib=0;rib<6;rib++)
            {
                float x=-2.7f+rib*1.08f;
                Beam(roof,"Visible rafter",new Vector3(x,1.57f,-1.62f),new Vector3(x,2.94f,0),.046f,wood);
                Beam(roof,"Visible rafter",new Vector3(x,1.57f,1.62f),new Vector3(x,2.94f,0),.046f,wood);
                Beam(interior,"Interior roof tie",new Vector3(x,1.86f,-1.34f),new Vector3(x,1.86f,1.34f),.052f,wood);
            }
            for(int side=-1;side<=1;side+=2)
            {
                Part(roof,"Thick thatch slope",RoofSlope(side),Vector3.zero,Vector3.one,straw);
                for(int row=0;row<7;row++) for(int tuft=0;tuft<80;tuft++)
                {
                    float x=-3.02f+(tuft+.5f)*6.04f/80+Noise(.018f);
                    float outer=1.72f-row*.235f,inner=Mathf.Max(0,outer-.31f);
                    var a=new Vector3(x,RoofHeight(outer)+.03f+Noise(.012f),side*outer);
                    var b=new Vector3(x+Noise(.025f),RoofHeight(inner)+.045f,side*inner);
                    Beam(roof,"Individual straw bundle",a,b,.020f+Noise(.004f),reed[random.Next(5)],8);
                    // Irregular tips break up the silhouette at the bottom of each thatch layer.
                    if(tuft%3==0) Beam(roof,"Loose reed tip",a,new Vector3(a.x+Noise(.025f),a.y-.10f,a.z+side*.055f),.007f,reed[random.Next(5)],6);
                }
                for(int band=0;band<3;band++)
                {
                    float z=side*(.36f+band*.43f);
                    Beam(roof,"Thatch retaining batten",new Vector3(-3.12f,RoofHeight(Mathf.Abs(z))+.086f,z),new Vector3(3.12f,RoofHeight(Mathf.Abs(z))+.086f,z),.026f,lightWood);
                }
            }
            Beam(roof,"Ridge timber",new Vector3(-3.26f,3.22f,0),new Vector3(3.26f,3.22f,0),.085f,wood,24);
            for(int tie=0;tie<18;tie++)
            {
                var binding=Band(roof,new Vector3(-3.12f+tie*.367f,3.22f,0),.09f,rope); binding.localRotation=Quaternion.Euler(0,0,90);
            }
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*2.94f;
                Beam(roof,"Crossed ridge end",new Vector3(x,2.89f,-.35f),new Vector3(x,3.41f,.25f),.049f,wood);
                Beam(roof,"Crossed ridge end",new Vector3(x,2.89f,.35f),new Vector3(x,3.41f,-.25f),.049f,wood);
            }
            Furnish(); Courtyard();
            Bake(walls,"Frame"); Bake(roof,"Roof"); Bake(interior,"Interior"); Bake(yard,"Courtyard");
            PrefabUtility.SaveAsPrefabAsset(house.gameObject,PrefabPath); Object.DestroyImmediate(house.gameObject);
            AssetDatabase.SaveAssets(); CreateShowcase(); InstallGallery(); AssetDatabase.SaveAssets();
            Debug.Log("LONGHOUSE: detailed model, removable roof, interior and gallery sample saved");
        }
        static float Noise(float amount) { return ((float)random.NextDouble()*2-1)*amount; }
        static Material Paint(string name,Color color)
        {
            string path=Art+"/Materials/"+name+".mat"; var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat) { mat=new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat,path); }
            mat.color=color; mat.SetFloat("_Glossiness",.07f); mat.enableInstancing=true; EditorUtility.SetDirty(mat); palette.Add(name,mat); return mat;
        }
        static Transform Node(Transform parent,string name) { var item=new GameObject(name).transform; item.SetParent(parent,false); return item; }
        static Transform Part(Transform parent,string name,Mesh mesh,Vector3 p,Vector3 size,Material mat)
        {
            var item=Node(parent,name); item.localPosition=p; item.localScale=size;
            item.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh; item.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat; return item;
        }
        static void Box(Transform parent,string name,Vector3 p,Vector3 size,Material mat) { Part(parent,name,DetailGeometry.BevelledBlock(),p,size,mat); }
        static void Beam(Transform parent,string name,Vector3 a,Vector3 b,float radius,Material mat,int sides=12)
        {
            bool fine=radius<=.028f;
            var profile=fine ? new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)} : new[]{new Vector2(0,0),new Vector2(.9f,0),new Vector2(1,.04f),new Vector2(.94f,.52f),new Vector2(1,.96f),new Vector2(.9f,1),new Vector2(0,1)};
            var mesh=DetailGeometry.Lathe("Longhouse beam "+sides+(fine ? " fine" : " timber"),profile,sides);
            var item=Part(parent,name,mesh,a,new Vector3(radius,(b-a).magnitude,radius),mat); item.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        }
        static Transform Band(Transform parent,Vector3 p,float radius,Material mat) { return Part(parent,"Rope binding",DetailGeometry.Ring(),p,Vector3.one*radius,mat); }
        static void Wattle(Transform parent,Vector3 p,float width,float height,bool face)
        {
            Box(parent,"Earthen infill panel",p,new Vector3(width,height,.12f),mud);
            // Leave woven rods exposed at the outer surface, with sufficient depth separation.
            int stakes=Mathf.CeilToInt(width/.16f),rows=Mathf.CeilToInt(height/.085f);
            float normal=face ? -1 : 1;
            for(int i=0;i<=stakes;i++)
            {
                float x=p.x-width*.5f+i*width/stakes;
                Beam(parent,"Wattle upright",new Vector3(x,p.y-height*.5f,p.z+normal*.094f),new Vector3(x,p.y+height*.5f,p.z+normal*.094f),.013f,lightWood,8);
            }
            for(int row=0;row<rows;row++) for(int segment=0;segment<stakes*2;segment++)
            {
                float x=p.x-width*.5f+segment*width/(stakes*2),y=p.y-height*.5f+(row+.5f)*height/rows;
                float z1=p.z+normal*(.11f+Mathf.Cos(segment*Mathf.PI+(row%2)*Mathf.PI)*.022f);
                float z2=p.z+normal*(.11f+Mathf.Cos((segment+1)*Mathf.PI+(row%2)*Mathf.PI)*.022f);
                Beam(parent,"Woven willow rod",new Vector3(x,y,z1),new Vector3(x+width/(stakes*2),y+Noise(.006f),z2),.011f,row%3==0 ? rope : lightWood,6);
            }
        }
        static void Door(Transform parent,float x,float z)
        {
            for(int side=-1;side<=1;side+=2) Beam(parent,"Door jamb",new Vector3(x+side*.41f,.18f,z-.035f),new Vector3(x+side*.41f,1.66f,z-.035f),.05f,wood);
            Beam(parent,"Door lintel",new Vector3(x-.46f,1.64f,z-.035f),new Vector3(x+.46f,1.64f,z-.035f),.06f,wood);
            var leaf=Node(parent,"Open plank door"); leaf.localPosition=new Vector3(x-.38f,.19f,z-.045f); leaf.localRotation=Quaternion.Euler(0,58,0);
            for(int plank=0;plank<5;plank++) Box(leaf,"Door plank",new Vector3((plank+.5f)*.14f,.69f,0),new Vector3(.133f,1.38f,.045f),plank%2==0 ? wood : lightWood);
            Box(leaf,"Door cross brace",new Vector3(.35f,.33f,-.04f),new Vector3(.72f,.052f,.035f),wood);
            Box(leaf,"Door cross brace",new Vector3(.35f,1.05f,-.04f),new Vector3(.72f,.052f,.035f),wood);
            for(int step=0;step<3;step++) Box(yard,"Entry stone step",new Vector3(x,.04f+step*.035f,z-.62f+step*.16f),new Vector3(.94f,.075f,.19f),stone);
            for(int plank=0;plank<5;plank++) Box(interior,"Entry floor board",new Vector3(x-.32f+plank*.16f,.19f,z+.20f),new Vector3(.15f,.045f,.45f),lightWood);
        }
        static float RoofHeight(float z) { return 3.14f-.70f*z-.10f*z*z; }
        static Mesh RoofSlope(int side)
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            for(int layer=0;layer<2;layer++) for(int cross=0;cross<=12;cross++) for(int along=0;along<=24;along++)
            {
                float z=cross*1.74f/12,x=-3.08f+along*6.16f/24;
                vertices.Add(new Vector3(x,RoofHeight(z)-layer*.13f,side*z));
            }
            int count=13*25;
            Action<int,int,int> tri=(a,b,c)=>{ var normal=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]); bool reverse=normal.y*(a<count ? 1 : -1)<0; triangles.Add(a); triangles.Add(reverse ? c : b); triangles.Add(reverse ? b : c); };
            for(int layer=0;layer<2;layer++) for(int row=0;row<12;row++) for(int along=0;along<24;along++)
            {
                int a=layer*count+row*25+along,b=a+25; tri(a,b,a+1); tri(a+1,b,b+1);
            }
            // Thick closed eave edge; reed tips cover the remaining short ends.
            for(int along=0;along<24;along++)
            {
                int a=12*25+along,b=a+count; triangles.AddRange(side<0 ? new[]{a,a+1,b,a+1,b+1,b} : new[]{a,b,a+1,a+1,b,b+1});
            }
            return Mesh("Curved thick thatch slope",vertices,triangles);
        }
        static Mesh Gable()
        {
            var vertices=new List<Vector3>{new Vector3(-1.35f,0,-.07f),new Vector3(1.35f,0,-.07f),new Vector3(0,1.33f,-.07f),new Vector3(-1.35f,0,.07f),new Vector3(1.35f,0,.07f),new Vector3(0,1.33f,.07f)};
            return Mesh("Gable infill",vertices,new List<int>{0,2,1,3,4,5,0,1,3,1,4,3,1,2,4,2,5,4,2,0,5,0,3,5});
        }
        static Mesh Mesh(string name,List<Vector3> vertices,List<int> triangles)
        {
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32}; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static void Furnish()
        {
            for(int side=-1;side<=1;side+=2) for(int bay=0;bay<3;bay++)
            {
                float x=(bay-1)*1.72f,z=side*.94f;
                for(int board=0;board<4;board++) Box(interior,"Sleeping platform plank",new Vector3(x,.42f,z+(board-1.5f)*.15f),new Vector3(1.51f,.05f,.14f),lightWood);
                for(int leg=0;leg<4;leg++) Beam(interior,"Platform leg",new Vector3(x+(leg%2==0 ? -.6f : .6f),.17f,z+(leg<2 ? -.22f : .22f)),new Vector3(x+(leg%2==0 ? -.6f : .6f),.41f,z+(leg<2 ? -.22f : .22f)),.034f,wood);
                Box(interior,"Reed sleeping mat",new Vector3(x,.46f,z),new Vector3(1.35f,.035f,.50f),straw);
            }
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6; var rock=Part(interior,"Hearth stone",DetailGeometry.WeatheredStone(i%3),new Vector3(Mathf.Cos(a)*.34f,.22f,Mathf.Sin(a)*.34f),new Vector3(.18f,.12f,.14f),stone);
                rock.localRotation=Quaternion.Euler(0,i*31,0);
            }
            for(int i=0;i<5;i++) Beam(interior,"Hearth log",new Vector3(-.24f,.23f,(i-2)*.065f),new Vector3(.24f,.25f,(i-2)*-.065f),.04f,wood);
            Jar(interior,new Vector3(.12f,.24f,.09f),.13f,.20f);
            Box(interior,"Shared table",new Vector3(1.4f,.70f,0),new Vector3(1.15f,.08f,.56f),lightWood);
            for(int i=0;i<4;i++) Beam(interior,"Table leg",new Vector3(1.4f+(i%2==0 ? -.44f : .44f),.18f,i<2 ? -.2f : .2f),new Vector3(1.4f+(i%2==0 ? -.44f : .44f),.66f,i<2 ? -.2f : .2f),.042f,wood);
            Jar(interior,new Vector3(1.45f,.74f,0),.12f,.18f);
        }
        static void Jar(Transform parent,Vector3 p,float radius,float height) { Part(parent,"Hollow clay vessel",DetailGeometry.Pot(),p,new Vector3(radius,height,radius),clay); }
        static void Courtyard()
        {
            for(int board=0;board<3;board++) Box(yard,"Bench seat plank",new Vector3(0,.40f,-2.03f+(board-1)*.1f),new Vector3(1.15f,.06f,.09f),lightWood);
            for(int leg=0;leg<4;leg++) Beam(yard,"Bench leg",new Vector3(leg%2==0 ? -.44f : .44f,.03f,leg<2 ? -2.12f : -1.95f),new Vector3(leg%2==0 ? -.44f : .44f,.36f,leg<2 ? -2.12f : -1.95f),.04f,wood);
            for(int i=0;i<3;i++) Jar(yard,new Vector3(-2.7f+i*.28f,.02f,-1.8f),.12f+i*.025f,.24f+i*.055f);
            var pile=Node(yard,"Stacked firewood"); pile.localPosition=new Vector3(2.02f,0,-2.0f);
            for(int row=0;row<3;row++) for(int log=0;log<3-row;log++)
            {
                float x=(log-(2-row)*.5f)*.18f;
                Beam(pile,"Split firewood",new Vector3(x,.12f+row*.15f,-.28f),new Vector3(x,.12f+row*.15f,.32f),.09f,wood,16);
                Beam(pile,"Visible log end grain",new Vector3(x,.12f+row*.15f,-.285f),new Vector3(x,.12f+row*.15f,-.30f),.075f,lightWood,16);
            }
            Jar(yard,new Vector3(2.88f,.02f,-1.55f),.2f,.40f);
            for(int i=0;i<4;i++)
            {
                float x=-3.15f+Noise(.2f),z=-.6f+i*.44f;
                var rock=Part(yard,"Loose foundation stone",DetailGeometry.WeatheredStone(i%3),new Vector3(x,.06f,z),new Vector3(.20f,.14f,.16f),stone); rock.localRotation=Quaternion.Euler(0,i*77,0);
            }
        }
        static void Bake(Transform section,string key)
        {
            var originals=section.GetComponentsInChildren<MeshFilter>();
            foreach(var mat in palette.Values)
            {
                var pieces=originals.Where(f=>f.GetComponent<Renderer>().sharedMaterial==mat).Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=section.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray();
                if(pieces.Length==0) continue;
                var mesh=new Mesh{name=key+" "+mat.name,indexFormat=IndexFormat.UInt32}; mesh.CombineMeshes(pieces,true,true); mesh.RecalculateBounds();
                var path=Art+"/Meshes/"+key+"_"+mat.name+".asset"; var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing) { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); mesh=existing; EditorUtility.SetDirty(existing); } else AssetDatabase.CreateAsset(mesh,path);
                Part(section,mat.name,mesh,Vector3.zero,Vector3.one,mat);
            }
            foreach(var filter in originals) Object.DestroyImmediate(filter.gameObject);
            // Remove now-empty authoring nodes, retaining the four independently viewable sections.
            foreach(var child in section.GetComponentsInChildren<Transform>().Reverse()) if(child!=section && child.childCount==0 && !child.GetComponent<MeshFilter>()) Object.DestroyImmediate(child.gameObject);
        }
        static Mesh Stage()
        {
            var path=Art+"/Meshes/DisplayHex.asset"; var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path); if(existing) return existing;
            var mesh=Object.Instantiate(DetailGeometry.Lathe("Longhouse hex",new[]{new Vector2(0,-.22f),new Vector2(4.45f,-.22f),new Vector2(4.53f,-.06f),new Vector2(4.5f,0),new Vector2(0,0)},6)); mesh.name="Longhouse display hex"; AssetDatabase.CreateAsset(mesh,path); return mesh;
        }
        static Material Ground()
        {
            var path=Art+"/Materials/Ground.mat"; var existing=AssetDatabase.LoadAssetAtPath<Material>(path); return existing ? existing : Paint("Ground",new Color(.25f,.31f,.20f));
        }
        static void CreateShowcase()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Longhouse preview — independent from gameplay").AddComponent<ArtPreviewScene>();
            var copy=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            Part(new GameObject("Display stage").transform,"Hex",Stage(),Vector3.zero,Vector3.one,Ground());
            var light=new GameObject("Warm sun").AddComponent<Light>(); light.type=LightType.Directional; light.transform.rotation=Quaternion.Euler(51,-38,0);
            light.color=new Color(1,.94f,.82f); light.intensity=1.25f; light.shadows=LightShadows.Soft; light.shadowBias=.15f; light.shadowNormalBias=.02f;
            RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(.67f,.72f,.75f); RenderSettings.ambientEquatorColor=new Color(.47f,.50f,.43f); RenderSettings.ambientGroundColor=new Color(.24f,.28f,.20f); RenderSettings.skybox=null; RenderSettings.fog=false;
            var camera=new GameObject("Fixed isometric camera").AddComponent<Camera>(); camera.tag="MainCamera"; camera.orthographic=true; camera.orthographicSize=4.25f;
            camera.transform.rotation=Quaternion.Euler(35.2643897f,45,0); camera.transform.position=new Vector3(0,1.05f,0)-camera.transform.forward*18;
            camera.backgroundColor=new Color(.075f,.13f,.12f); camera.clearFlags=CameraClearFlags.SolidColor; camera.nearClipPlane=.1f; camera.farClipPlane=50; camera.allowHDR=false; camera.gameObject.AddComponent<AudioListener>();
            var viewer=camera.gameObject.AddComponent<LonghouseShowcaseController>(); viewer.model=copy.transform; viewer.roof=copy.transform.Find("Removable roof").gameObject; viewer.view=camera;
            viewer.triangles=copy.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3); viewer.draws=copy.GetComponentsInChildren<MeshRenderer>().Length;
            EditorSceneManager.SaveScene(scene,ScenePath);
        }
        public static void InstallGallery()
        {
            const string path="Assets/Scenes/DevelopmentGallery.unity"; if(!File.Exists(path)) return;
            var scene=EditorSceneManager.OpenScene(path); var viewer=Object.FindAnyObjectByType<DevelopmentGalleryViewer>(); var group=viewer.groups[2].transform;
            var old=group.Find("Building_longhouse"); if(old) Object.DestroyImmediate(old.gameObject);
            var sample=new GameObject("Building_longhouse").transform; sample.SetParent(group,false); sample.localScale=Vector3.one*.8f;
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),sample);
            Part(sample,"Display base",Stage(),Vector3.zero,Vector3.one,Ground());
            var item=sample.gameObject.AddComponent<DevelopmentGalleryItem>(); item.title="Длинный дом · неолит"; item.focusHeight=1.1f;
            item.description="Каркас · плетёные стены · соломенная крыша · общий очаг"; item.details="Первый образец новой архитектуры E02 для оценки. Производство и бонусы ещё не подключены.";
            Vector3 right=new Vector3(.7071068f,0,-.7071068f),forward=new Vector3(.7071068f,0,.7071068f); int rows=Mathf.CeilToInt(group.childCount/3f);
            for(int i=0;i<group.childCount;i++) group.GetChild(i).localPosition=right*((i%3-1)*8.4f)+forward*((rows-1)*.5f-i/3)*8.5f;
            EditorSceneManager.SaveScene(scene,path);
        }
        [MenuItem("Истоки/Разработка/Открыть длинный дом")]
        public static void Open() { if(!File.Exists(ScenePath)) Generate(); EditorSceneManager.OpenScene(ScenePath); }
        [MenuItem("Истоки/Разработка/Собрать просмотр длинного дома")]
        public static void BuildPreview()
        {
            Generate(); Directory.CreateDirectory("Builds/LonghousePreview");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/LonghousePreview/LonghousePreview.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Longhouse preview build failed");
            SpearmanModelBuilder.BuildGallery(); Debug.Log("LONGHOUSE: model preview and development gallery built");
        }
    }
}
