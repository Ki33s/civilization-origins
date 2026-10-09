using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Origins.Editor.ForestArt
{
    public static class ForestShowcaseBuilder
    {
        const string Root="Assets/Art/Forest/Generated";
        const string ScenePath="Assets/Scenes/ForestTileShowcase.unity";
        const float Radius=4.5f;
        static Material material;
        static System.Random random;
        static int treeCount, grassCount, flowerCount;
        static readonly List<Mesh> temporary=new List<Mesh>();
        static readonly Color Bark=ForestMeshBuilder.RGB("69513B"),Leaf=ForestMeshBuilder.RGB("456A29"),Grass=ForestMeshBuilder.RGB("60833A");
        sealed class DetailPart
        {
            public ForestMeshBuilder source;
            public int category, firstVertex, vertices, firstIndex, indices;
        }
        static void RecordDetail(ForestMeshBuilder builder,List<DetailPart> parts,int category,Action emit)
        {
            int firstVertex=builder.VertexCount,firstIndex=builder.IndexCount;
            emit();
            if(parts!=null) parts.Add(new DetailPart { source=builder,category=category,firstVertex=firstVertex,vertices=builder.VertexCount-firstVertex,firstIndex=firstIndex,indices=builder.IndexCount-firstIndex });
        }
        // Integer allocation preserves exactly 1/2 and 1/4 of all placed items.
        // Odd counts in individual categories are rounded with the largest-remainder method.
        static int[] DensityCounts(int[] counts,int divisor)
        {
            var result=new int[counts.Length]; var order=new List<int>(); int total=0,allocated=0;
            for(int i=0;i<counts.Length;i++) { result[i]=counts[i]/divisor; total+=counts[i]; allocated+=result[i]; order.Add(i); }
            order.Sort((a,b)=>{ int c=(counts[b]%divisor).CompareTo(counts[a]%divisor); return c!=0 ? c : a.CompareTo(b); });
            for(int i=0;i<total/divisor-allocated;i++) result[order[i]]++;
            return result;
        }
        static bool[] SelectItems(int count,int keep,int category)
        {
            var order=new List<int>(); for(int i=0;i<count;i++) order.Add(i);
            order.Sort((a,b)=>SelectionRank(a,category).CompareTo(SelectionRank(b,category)));
            var result=new bool[count]; for(int i=0;i<keep;i++) result[order[i]]=true; return result;
        }
        static uint SelectionRank(int index,int category)
        {
            unchecked { uint n=(uint)(index+1)*747796405u+(uint)(category+1)*2891336453u; n=(n^(n>>16))*2246822519u; return n^(n>>13); }
        }
        static float F(float a,float b) { return a+(float)random.NextDouble()*(b-a); }
        static Vector3 Radial(float angle,float radius,float y=0) { return new Vector3(Mathf.Cos(angle)*radius,y,Mathf.Sin(angle)*radius); }
        static Vector3 Ground(Vector3 p) { p.y=ForestMeshBuilder.Height(p.x,p.z); return p; }
        static Mesh SaveMesh(ForestMeshBuilder builder,string name,bool smooth=true)
        {
            var mesh=builder.Finish(name,smooth); string path=Root+"/Meshes/"+name+".asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old) { EditorUtility.CopySerialized(mesh,old); UnityEngine.Object.DestroyImmediate(mesh); return old; }
            AssetDatabase.CreateAsset(mesh,path); return mesh;
        }
        static GameObject MeshObject(string name,Mesh mesh,Transform parent,Vector3 position)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.localPosition=position;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.On; renderer.receiveShadows=true; return go;
        }
        [MenuItem("Истоки/Графика/Открыть лесную сцену")]
        public static void OpenScene()
        {
            if(!File.Exists(ScenePath)) Generate();
            else EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("Истоки/Графика/Пересобрать лесные клетки")]
        public static void Generate()
        {
            Directory.CreateDirectory(Root+"/Meshes"); Directory.CreateDirectory(Root+"/Prefabs"); Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            var shader=Shader.Find("Origins/Forest Vertex Lit");
            if(!shader) throw new Exception("Forest vertex shader not imported");
            material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/ForestSurface.mat");
            if(!material) { material=new Material(shader); AssetDatabase.CreateAsset(material,Root+"/ForestSurface.mat"); }
            material.shader=shader; material.enableInstancing=true; material.SetFloat("_Glossiness",.13f);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Art preview — no game simulation").AddComponent<Origins.ArtPreviewScene>();
            random=new System.Random(7816); treeCount=grassCount=flowerCount=0;
            var woods=new Mesh[9]; var crowns=new Mesh[9];
            for(int type=0;type<3;type++) for(int variant=0;variant<3;variant++)
            {
                int index=type*3+variant; CreateTree(type,variant,out woods[index],out crowns[index]);
                var prefab=new GameObject(new[]{"Oak","Birch","Pine"}[type]+"_"+(variant+1));
                MeshObject("Trunk, roots and branches",woods[index],prefab.transform,Vector3.zero);
                MeshObject("Canopy",crowns[index],prefab.transform,Vector3.zero);
                PrefabUtility.SaveAsPrefabAsset(prefab,Root+"/Prefabs/"+prefab.name+".prefab"); UnityEngine.Object.DestroyImmediate(prefab);
            }
            Mesh rock=CreateRock(),stump=CreateStump(),log=CreateLog(),fern=CreateFern(),mushrooms=CreateMushrooms();
            var canopy=new GameObject("Canopies — toggle with C").transform;
            var treeObjects=new List<GameObject>();
            var tiles=new GameObject("Nine forest hexes — original seven and two density variants").transform;
            var context=new List<GameObject>(); var referenceTrees=new List<GameObject>(); var referenceCrowns=new List<GameObject>();
            var referenceDetails=new List<DetailPart>(); Mesh referenceGround=null; Vector3 referenceCenter=Vector3.zero;
            int[,] coords={{0,0},{1,0},{0,1},{-1,1},{-1,0},{0,-1},{1,-1}};
            for(int i=0;i<7;i++)
            {
                Vector3 center=new Vector3(Mathf.Sqrt(3)*Radius*(coords[i,0]+coords[i,1]*.5f),0,Radius*1.5f*coords[i,1]);
                var tile=new GameObject(i==0 ? "00 · Central forest" : "0"+i+" · Forest neighbour").transform; tile.SetParent(tiles,false); tile.position=center;
                CreateTerrain(tile,center,i);
                var parts=i==6 ? referenceDetails : null;
                if(i==6) { referenceGround=tile.GetChild(0).GetComponent<MeshFilter>().sharedMesh; referenceCenter=center; }
                else context.Add(tile.gameObject);
                var positions=new List<Vector3>();
                for(int t=0;t<11;t++)
                {
                    Vector3 p=Sample(center,.82f); int tries=0;
                    while((positions.Exists(v=>(v-p).sqrMagnitude<2.8f) || (i==0 && p.z<-1.2f && p.x>-.6f && p.x<3.0f)) && tries++<100) p=Sample(center,.82f);
                    positions.Add(p); int index=random.Next(9); float scale=F(.78f,1.13f); float yaw=F(0,360);
                    var tree=MeshObject(new[]{"Oak","Birch","Pine"}[index/3]+" "+(t+1),woods[index],tile,p-center);
                    treeObjects.Add(tree);
                    tree.transform.localScale=Vector3.one*scale; tree.transform.rotation=Quaternion.Euler(0,yaw,0);
                    var crown=MeshObject(tree.name+" crown",crowns[index],canopy,p);
                    crown.transform.localScale=Vector3.one*scale; crown.transform.rotation=tree.transform.rotation; treeCount++;
                    if(i==6) { referenceTrees.Add(tree); referenceCrowns.Add(crown); }
                    else context.Add(crown);
                }
                var undergrowth=new ForestMeshBuilder(); var props=new ForestMeshBuilder();
                for(int n=0;n<460;n++)
                {
                    RecordDetail(undergrowth,parts,1,()=>
                    {
                        Vector3 p=Sample(center,.95f); float tall=F(.15f,.42f);
                        for(int b=0;b<8;b++) undergrowth.Blade(p-center+Radial(F(0,Mathf.PI*2),F(0,.11f)),F(0,6.28f),tall*F(.6f,1.2f),F(.025f,.055f),F(.07f,.22f),Color.Lerp(Grass,Leaf,F(0,.65f)),5);
                    });
                    grassCount++;
                }
                for(int n=0;n<65;n++)
                {
                    int type=n%4;
                    RecordDetail(undergrowth,parts,2,()=> { Vector3 p=Sample(center,.95f); Flower(undergrowth,p-center,type); }); flowerCount++;
                }
                for(int n=0;n<18;n++) RecordDetail(props,parts,3,()=>AddAt(props,fern,Sample(center,.9f)-center,F(.7f,1.3f)));
                for(int n=0;n<11;n++) RecordDetail(props,parts,4,()=>AddAt(props,rock,Sample(center,.94f)-center,F(.45f,1.2f)));
                for(int n=0;n<3;n++) RecordDetail(props,parts,5,()=>AddAt(props,stump,Sample(center,.85f)-center,F(.7f,1.1f)));
                for(int n=0;n<2;n++) RecordDetail(props,parts,6,()=>AddAt(props,log,Sample(center,.75f)-center,F(.8f,1.1f)));
                for(int n=0;n<8;n++) RecordDetail(props,parts,7,()=>AddAt(props,mushrooms,Sample(center,.9f)-center,F(.7f,1.25f)));
                for(int n=0;n<230;n++)
                {
                    RecordDetail(undergrowth,parts,8,()=>
                    {
                        Vector3 p=Sample(center,.97f)-center+Vector3.up*.022f; float a=F(0,6.28f); Vector3 d=Radial(a,F(.06f,.14f)),w=Radial(a+1.57f,F(.025f,.05f));
                        Color c=Color.Lerp(ForestMeshBuilder.RGB("AD893D"),ForestMeshBuilder.RGB("67442E"),F(0,1));
                        undergrowth.Quad(p-d,p+w+Vector3.up*.015f,p+d,p-w,c);
                    });
                }
                // Curated foreground patch for the close inspection preset.
                if(i==0)
                {
                    AddAt(props,stump,Ground(new Vector3(.3f,0,-2.2f)),1.05f,20);
                    AddAt(props,log,Ground(new Vector3(2.4f,0,-1.5f)),.85f,70);
                    AddAt(props,rock,Ground(new Vector3(2.4f,0,-3)),.8f,0);
                    AddAt(props,fern,Ground(new Vector3(1.35f,0,-2.15f)),1.25f,40);
                    AddAt(props,mushrooms,Ground(new Vector3(.85f,0,-2.6f)),1.1f,40);
                    for(int n=0;n<18;n++) { Flower(undergrowth,Ground(new Vector3(F(.4f,2.5f),0,F(-3.4f,-2.1f))),n%4); flowerCount++; }
                }
                MeshObject("Grass, wildflowers and leaf litter",SaveMesh(undergrowth,"Understory_"+i,false),tile,Vector3.zero);
                MeshObject("Ferns, mossy rocks, stumps, logs and mushrooms",SaveMesh(props,"ForestProps_"+i),tile,Vector3.zero);
            }
            int[] baseCounts={referenceTrees.Count,460,65,18,11,3,2,8,230};
            var densityCenters=new[]{referenceCenter,referenceCenter+new Vector3(7.424621f,0,-7.424621f),referenceCenter+new Vector3(14.849242f,0,-14.849242f)};
            var densityObjects=new[]{808,404,202}; var densityTrees=new[]{11,0,0};
            var densityReport=new System.Text.StringBuilder("\nDensity comparison (tree, grass tuft, flower, fern, rock, stump, log, mushroom cluster, fallen leaf)\n100%: "+string.Join(", ",baseCounts)+" = 808 items\n");
            for(int variant=1;variant<=2;variant++)
            {
                int divisor=variant==1 ? 2 : 4; int[] counts=DensityCounts(baseCounts,divisor);
                int sum=0; foreach(int count in counts) sum+=count;
                if(sum!=densityObjects[variant]) throw new Exception("Density item count is incorrect");
                var selected=new bool[9][];
                for(int category=0;category<9;category++) selected[category]=SelectItems(baseCounts[category],counts[category],category);
                var tile=new GameObject(variant==1 ? "07 · Forest density 50 percent" : "08 · Forest density 25 percent").transform;
                tile.SetParent(tiles,false); tile.position=densityCenters[variant];
                MeshObject("Same reference terrain",referenceGround,tile,Vector3.zero);
                for(int t=0;t<referenceTrees.Count;t++) if(selected[0][t])
                {
                    var original=referenceTrees[t]; var originalCrown=referenceCrowns[t];
                    var tree=MeshObject(original.name,original.GetComponent<MeshFilter>().sharedMesh,tile,original.transform.localPosition);
                    tree.transform.localScale=original.transform.localScale; tree.transform.rotation=original.transform.rotation; treeObjects.Add(tree);
                    var crown=MeshObject(originalCrown.name+" density "+divisor,originalCrown.GetComponent<MeshFilter>().sharedMesh,canopy,tile.position+original.transform.localPosition);
                    crown.transform.localScale=originalCrown.transform.localScale; crown.transform.rotation=originalCrown.transform.rotation;
                    treeCount++;
                }
                var undergrowth=new ForestMeshBuilder(); var props=new ForestMeshBuilder(); var next=new int[9];
                foreach(var part in referenceDetails)
                {
                    int ordinal=next[part.category]++;
                    if(!selected[part.category][ordinal]) continue;
                    var target=part.category==1 || part.category==2 || part.category==8 ? undergrowth : props;
                    target.AddRange(part.source,part.firstVertex,part.vertices,part.firstIndex,part.indices);
                }
                for(int category=1;category<9;category++) if(next[category]!=baseCounts[category]) throw new Exception("Incomplete reference detail category "+category);
                MeshObject("Grass, flowers and leaf litter",SaveMesh(undergrowth,"Understory_"+(variant+6),false),tile,Vector3.zero);
                MeshObject("Detailed props",SaveMesh(props,"ForestProps_"+(variant+6)),tile,Vector3.zero);
                grassCount+=counts[1]; flowerCount+=counts[2]; densityTrees[variant]=counts[0];
                densityReport.Append((100/divisor)+"%: "+string.Join(", ",counts)+" = "+sum+" items\n");
            }
            foreach(var mesh in temporary) UnityEngine.Object.DestroyImmediate(mesh); temporary.Clear();
            var sun=new GameObject("Late morning sun").AddComponent<Light>(); sun.type=LightType.Directional;
            sun.transform.rotation=Quaternion.Euler(53,-38,0); sun.color=new Color(1,.93f,.78f); sun.intensity=1.3f; sun.shadows=LightShadows.Soft; sun.shadowBias=.045f; sun.shadowNormalBias=.12f;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.65f,.72f,.78f); RenderSettings.ambientEquatorColor=new Color(.48f,.53f,.45f); RenderSettings.ambientGroundColor=new Color(.29f,.33f,.23f);
            RenderSettings.fog=false; RenderSettings.skybox=null;
            var camera=new GameObject("Isometric camera — fixed 35.264 degrees").AddComponent<Camera>(); camera.tag="MainCamera";
            camera.orthographic=true; camera.orthographicSize=10.3f; camera.nearClipPlane=.2f; camera.farClipPlane=150;
            camera.transform.rotation=Quaternion.Euler(35.2643897f,45,0); camera.transform.position=densityCenters[1]+Vector3.up-camera.transform.forward*45;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=ForestMeshBuilder.RGB("182623"); camera.allowHDR=false;
            camera.gameObject.AddComponent<AudioListener>(); var controller=camera.gameObject.AddComponent<Origins.ForestPreviewCamera>(); controller.canopy=canopy; controller.treeGeometry=treeObjects.ToArray();
            controller.comparisonContext=context.ToArray(); controller.densityCenters=densityCenters; controller.densityObjects=densityObjects; controller.densityTrees=densityTrees;
            controller.trees=treeCount; controller.grassTufts=grassCount; controller.flowers=flowerCount;
            long triangleTotal=0;
            foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)) triangleTotal+=filter.sharedMesh.triangles.Length/3;
            controller.triangles=(int)triangleTotal;
            EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
            SceneView.lastActiveSceneView?.AlignViewToObject(camera.transform);
            Directory.CreateDirectory("Docs");
            File.WriteAllText("Docs/forest-art-statistics.txt",$"Forest showcase\n9 hexes (original seven + two density variants)\n{treeCount} trees, 9 mesh variants\n{grassCount} grass tufts\n{flowerCount} flowers\n{triangleTotal:N0} rendered triangles\nSeed 7816\nCamera: orthographic, pitch 35.2643897, yaw 45\n"+densityReport);
            Debug.Log($"FOREST: scene saved, {treeCount} trees, {triangleTotal} triangles");
        }
        static void AddAt(ForestMeshBuilder builder,Mesh mesh,Vector3 p,float scale,float yaw=-1)
        { builder.AddMesh(mesh,Matrix4x4.TRS(p,Quaternion.Euler(0,yaw<0 ? F(0,360) : yaw,0),Vector3.one*scale)); }
        static Vector3 Sample(Vector3 center,float inset)
        {
            int wedge=random.Next(6); float a=(wedge*60+30)*Mathf.Deg2Rad,b=((wedge+1)*60+30)*Mathf.Deg2Rad;
            float u=F(0,1),v=F(0,1); if(u+v>1) {u=1-u;v=1-v;}
            return Ground(center+(Radial(a,Radius)*u+Radial(b,Radius)*v)*inset);
        }
        static void CreateTerrain(Transform tile,Vector3 center,int index)
        {
            var mesh=new ForestMeshBuilder(); const int rings=16,steps=12;
            for(int wedge=0;wedge<6;wedge++)
            {
                Vector3 a=Radial((wedge*60+30)*Mathf.Deg2Rad,Radius-.015f),b=Radial(((wedge+1)*60+30)*Mathf.Deg2Rad,Radius-.015f);
                for(int r=0;r<rings;r++) for(int s=0;s<steps;s++)
                {
                    float t0=r/(float)rings,t1=(r+1)/(float)rings,u=s/(float)steps,v=(s+1)/(float)steps;
                    Vector3 p=Ground(center+Vector3.Lerp(a,b,u)*t0)-center,q=Ground(center+Vector3.Lerp(a,b,v)*t0)-center;
                    Vector3 k=Ground(center+Vector3.Lerp(a,b,v)*t1)-center,l=Ground(center+Vector3.Lerp(a,b,u)*t1)-center;
                    Color c=Color.Lerp(ForestMeshBuilder.RGB("455336"),ForestMeshBuilder.RGB("7A7048"),Mathf.PerlinNoise((p.x+center.x)*.48f+44,(p.z+center.z)*.48f+12));
                    mesh.Triangle(p,l,k,c,c,c); mesh.Triangle(p,k,q,c,c,c);
                }
                for(int s=0;s<32;s++)
                {
                    Vector3 p=Ground(center+Vector3.Lerp(a,b,s/32f))-center,q=Ground(center+Vector3.Lerp(a,b,(s+1)/32f))-center;
                    for(int layer=0;layer<4;layer++)
                    {
                        Vector3 lowP=new Vector3(p.x,-.24f-layer*.22f,p.z),lowQ=new Vector3(q.x,lowP.y,q.z);
                        Vector3 highP=layer==0 ? p : new Vector3(p.x,-.24f-(layer-1)*.22f,p.z),highQ=layer==0 ? q : new Vector3(q.x,highP.y,q.z);
                        Color color=Color.Lerp(ForestMeshBuilder.RGB("685238"),ForestMeshBuilder.RGB("302C24"),layer/3f)*F(.87f,1.1f); color.a=1;
                        mesh.Quad(highP,highQ,lowQ,lowP,color);
                    }
                    // Narrow moss seam lets each of the seven cells remain readable.
                    mesh.Tube(p+Vector3.up*.008f,q+Vector3.up*.008f,.017f,.017f,index==0 ? ForestMeshBuilder.RGB("90A85D") : ForestMeshBuilder.RGB("63713F"),6,1,0);
                }
            }
            MeshObject("Tessellated moss and soil strata",SaveMesh(mesh,"HexGround_"+index),tile,Vector3.zero);
        }
        static void CreateTree(int type,int variant,out Mesh wood,out Mesh canopy)
        {
            var trunk=new ForestMeshBuilder(); var leaves=new ForestMeshBuilder(); float height=type==2 ? 4.6f+variant*.22f : 3.3f+variant*.2f;
            float radius=type==0 ? .25f : type==1 ? .14f : .20f; Color bark=type==1 ? ForestMeshBuilder.RGB("D5D3B7") : Bark;
            Vector3 top=new Vector3(.1f*(variant-1),height,0);
            trunk.Tube(Vector3.zero,top,radius,.035f,bark,24,16,.09f);
            for(int n=0;n<7;n++)
            {
                float angle=n*6.28f/7; trunk.Tube(Radial(angle,.06f,.15f),Radial(angle,radius*3.5f,.025f),radius*.7f,.025f,bark,12,5);
            }
            if(type==1)
            {
                for(int n=0;n<28;n++)
                {
                    float y=F(.3f,height-.35f),angle=F(0,6.28f),r=Mathf.Lerp(radius,.035f,y/height)+.01f;
                    Vector3 p=Radial(angle,r,y); Vector3 side=Radial(angle+1.57f,F(.04f,.1f));
                    trunk.Quad(p-side,p+side,p+side+Vector3.up*.026f,p-side+Vector3.up*.043f,ForestMeshBuilder.RGB("494840"));
                }
            }
            if(type<2)
            {
                for(int n=0;n<12;n++)
                {
                    float angle=n*2.39996f+variant,y=height*F(.45f,.87f); Vector3 start=new Vector3(0,y,0);
                    Vector3 tip=Radial(angle,F(.45f,type==0 ? .95f : .72f),y+F(.3f,.75f));
                    trunk.Tube(start,tip,radius*.48f,.025f,bark,14,7);
                    Vector3 fork=tip+Radial(angle+.7f,.4f,.25f); trunk.Tube(Vector3.Lerp(start,tip,.65f),fork,.06f,.012f,bark,10,4);
                    Color tint=type==0 ? Color.Lerp(Leaf,ForestMeshBuilder.RGB("759044"),F(0,.35f)) : ForestMeshBuilder.RGB("6C913E");
                    Vector3 scale=type==0 ? new Vector3(.63f,.49f,.61f) : new Vector3(.47f,.50f,.47f);
                    // Small shaded interior surrounded by hundreds of individually bent leaves.
                    leaves.Blob(tip,scale*.45f,tint*.65f,variant*93+n,20,12,.08f);
                    for(int leaf=0;leaf<420;leaf++)
                    {
                        float a=F(0,6.28f),v=F(-.8f,.8f); Vector3 normal=new Vector3(Mathf.Sqrt(1-v*v)*Mathf.Cos(a),v,Mathf.Sqrt(1-v*v)*Mathf.Sin(a));
                        Vector3 p=tip+Vector3.Scale(normal,scale)*F(.60f,1.12f);
                        Vector3 axis=Radial(F(0,6.28f),F(.065f,.13f),F(-.05f,.05f)),side=Vector3.Cross(normal,axis).normalized*(type==0 ? .055f : .035f);
                        Color leafTint=tint*F(.86f,1.18f); leafTint.a=1;
                        leaves.Triangle(p-axis,p+side,p+normal*.014f,leafTint*.87f,leafTint,leafTint);
                        leaves.Triangle(p+side,p+axis,p+normal*.014f,leafTint,leafTint*1.05f,leafTint);
                        leaves.Triangle(p+axis,p-side,p+normal*.014f,leafTint*1.05f,leafTint,leafTint);
                        leaves.Triangle(p-side,p-axis,p+normal*.014f,leafTint,leafTint*.87f,leafTint);
                    }
                }
                // The overlapping branch clusters form the crown, without a solid ball on top.
            }
            else
            {
                for(int level=0;level<9;level++)
                {
                    float y=.9f+level*.4f,reach=1.0f*(1-level/10f);
                    for(int n=0;n<7;n++)
                    {
                        float angle=n*6.28f/7+level*.7f; Vector3 p=Radial(angle,reach,y-.08f),basePoint=new Vector3(0,y+.12f,0);
                        trunk.Tube(basePoint,p,.055f*(1-level/11f),.008f,bark,10,4);
                        Vector3 tuft=Vector3.Lerp(basePoint,p,.63f);
                        leaves.Blob(tuft,new Vector3(reach*.37f,.10f,reach*.37f),ForestMeshBuilder.RGB("294D32"),n+level*7,20,12,.08f);
                        for(int needle=0;needle<80;needle++)
                        {
                            float direction=F(0,6.28f); Vector3 root=tuft+Radial(direction,F(0,reach*.48f),F(-.07f,.13f));
                            leaves.Blade(root,direction,F(.09f,.22f),F(.023f,.044f),F(.13f,.27f),Color.Lerp(ForestMeshBuilder.RGB("3D693E"),ForestMeshBuilder.RGB("759359"),F(0,1)),4);
                        }
                    }
                }
                for(int needle=0;needle<80;needle++) leaves.Blade(top-Vector3.up*F(.1f,.5f),F(0,6.28f),F(.13f,.25f),.035f,.12f,Leaf,4);
            }
            string name=new[]{"Oak","Birch","Pine"}[type]+"_"+variant;
            wood=SaveMesh(trunk,name+"_Wood"); canopy=SaveMesh(leaves,name+"_Leaves");
        }
        static Mesh CreateRock()
        {
            var b=new ForestMeshBuilder(); b.Blob(new Vector3(0,.17f,0),new Vector3(.46f,.33f,.37f),ForestMeshBuilder.RGB("89918A"),22,24,16,.18f);
            b.Blob(new Vector3(-.08f,.46f,.02f),new Vector3(.31f,.085f,.26f),ForestMeshBuilder.RGB("5D763B"),31,20,12,.25f);
            b.Blob(new Vector3(-.30f,.28f,.04f),new Vector3(.13f,.10f,.20f),ForestMeshBuilder.RGB("536C32"),7,16,10,.18f);
            return SaveMesh(b,"MossyRock");
        }
        static void Rings(ForestMeshBuilder b,Vector3 center,float radius,int count)
        {
            for(int ring=1;ring<=count;ring++)
            {
                float r=radius*ring/(count+1);
                for(int n=0;n<40;n++) b.Tube(center+Radial(n*6.28f/40,r),center+Radial((n+1)*6.28f/40,r),.0035f,.0035f,ForestMeshBuilder.RGB("856343"),4,1,0);
            }
        }
        static Mesh CreateStump()
        {
            var b=new ForestMeshBuilder(); b.Tube(Vector3.zero,new Vector3(.04f,.45f,0),.28f,.23f,Bark,32,10,.13f);
            b.Tube(new Vector3(.04f,.449f,0),new Vector3(.04f,.456f,0),.218f,.218f,ForestMeshBuilder.RGB("C9A774"),40,1,0);
            Rings(b,new Vector3(.04f,.463f,0),.21f,7);
            for(int n=0;n<6;n++) b.Tube(Radial(n*1.047f,.07f,.15f),Radial(n*1.047f,.57f,.015f),.13f,.025f,Bark,12,5);
            for(int n=0;n<3;n++) b.Tube(new Vector3(.04f,.471f,0)+Radial(n*2.1f,.1f),new Vector3(.04f,.471f,0)+Radial(n*2.1f+.15f,.21f),.008f,.002f,ForestMeshBuilder.RGB("4C392B"),4,1);
            return SaveMesh(b,"StumpWithGrowthRings");
        }
        static Mesh CreateLog()
        {
            var b=new ForestMeshBuilder(); b.Tube(new Vector3(-.8f,.19f,0),new Vector3(.8f,.22f,.1f),.18f,.15f,Bark,24,12,.15f);
            b.Tube(new Vector3(.796f,.22f,.1f),new Vector3(.81f,.22f,.1f),.138f,.138f,ForestMeshBuilder.RGB("C3A374"),32,1,0);
            b.Tube(new Vector3(-.8f,.19f,0),new Vector3(-.814f,.19f,0),.162f,.162f,ForestMeshBuilder.RGB("BFA075"),32,1,0);
            for(int n=0;n<4;n++) b.Tube(new Vector3(-.5f+n*.33f,.25f,0),new Vector3(-.55f+n*.33f,.46f,F(-.18f,.18f)),.05f,.022f,Bark,12,4);
            b.Blob(new Vector3(-.1f,.34f,0),new Vector3(.63f,.045f,.14f),ForestMeshBuilder.RGB("617936"),91,24,10,.25f);
            return SaveMesh(b,"MossyFallenLog");
        }
        static Mesh CreateFern()
        {
            var b=new ForestMeshBuilder(); Color c=ForestMeshBuilder.RGB("4D813A");
            for(int n=0;n<9;n++)
            {
                float angle=n*2.4f; Vector3 d=Radial(angle,1),side=Radial(angle+1.57f,1);
                for(int s=0;s<12;s++)
                {
                    float t=s/12f,u=(s+1)/12f;
                    Vector3 p=d*.58f*t+Vector3.up*(Mathf.Sin(t*2.1f)*.45f),q=d*.58f*u+Vector3.up*(Mathf.Sin(u*2.1f)*.45f);
                    b.Tube(p,q,.008f,.005f,c,6,1,0);
                    if(s==0) continue; float width=.16f*Mathf.Sin(t*Mathf.PI)*F(.8f,1.2f);
                    for(int sign=-1;sign<=1;sign+=2)
                    {
                        Vector3 tip=p+side*sign*width+d*.07f+Vector3.up*.025f;
                        b.Triangle(p,tip+d*.015f,q,c*.7f,c*1.1f,c);
                        b.Triangle(p,tip-d*.025f,tip+d*.015f,c*.7f,c,c*1.1f);
                    }
                }
            }
            return SaveMesh(b,"FernNineFronds");
        }
        static Mesh CreateMushrooms()
        {
            var b=new ForestMeshBuilder();
            for(int n=0;n<4;n++)
            {
                Vector3 p=Radial(n*2.4f,.1f*n); float h=.12f+n*.025f;
                b.Tube(p,p+Vector3.up*h,.019f,.013f,ForestMeshBuilder.RGB("D6CDB0"),12,5);
                b.Blob(p+Vector3.up*h,new Vector3(.085f,.035f,.085f),ForestMeshBuilder.RGB("C18945"),n+5,20,12,.06f);
            }
            return SaveMesh(b,"HoneyMushroomCluster");
        }
        static void Flower(ForestMeshBuilder b,Vector3 p,int type)
        {
            float h=F(.17f,.32f); Vector3 top=p+Vector3.up*h;
            b.Tube(p,top,.007f,.0035f,Grass,6,3,0);
            b.Blade(p+Vector3.up*h*.3f,F(0,6.28f),.12f,.05f,.10f,Grass,4);
            Color c=ForestMeshBuilder.RGB(new[]{"EDE4BF","BAAFD9","EAC853","709BCC"}[type]);
            for(int petal=0;petal<6;petal++)
            {
                float a=petal*6.28f/6; Vector3 d=Radial(a,.05f),w=Radial(a+1.57f,.023f);
                Vector3 tip=top+d*1.65f+Vector3.up*.014f,mid=top+d+Vector3.up*.028f;
                b.Triangle(top,mid-w,mid,c*.84f,c,c); b.Triangle(top,mid,mid+w,c*.84f,c,c);
                b.Triangle(mid-w,tip,mid,c,c*1.1f,c); b.Triangle(mid,tip,mid+w,c,c*1.1f,c);
            }
            b.Blob(top+Vector3.up*.015f,new Vector3(.024f,.013f,.024f),ForestMeshBuilder.RGB("D9AC36"),type,8,6,0);
        }
        [MenuItem("Истоки/Графика/Собрать отдельный просмотр леса")]
        public static void BuildPreview()
        {
            Generate(); Directory.CreateDirectory("Builds/ForestPreview");
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{ScenePath},locationPathName="Builds/ForestPreview/ForestPreview.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Forest build failed: "+report.summary.result);
            Debug.Log("FOREST: Windows preview build succeeded");
        }
    }
}
