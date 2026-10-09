using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Origins.Editor.ForestArt
{
    public static partial class TerrainShowcaseBuilder
    {
        const string Root="Assets/Art/Terrain/Generated";
        const string ScenePath="Assets/Scenes/TerrainTileShowcase.unity";
        const float Radius=4.5f;
        const int MinDecor=150,MaxDecor=250;
        static int generationSeed;
        static System.Random random;
        static Material surface;
        static float F(float a,float b) { return a+(float)random.NextDouble()*(b-a); }
        static Color C(string hex) { return ForestMeshBuilder.RGB(hex); }
        static Vector3 Radial(float a,float r,float y=0) { return new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r); }
        static Mesh SaveMesh(ForestMeshBuilder b,string name,bool smooth=true)
        {
            Mesh mesh=b.Finish(name,smooth); string path=Root+"/Meshes/"+name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing) { EditorUtility.CopySerialized(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); return existing; }
            AssetDatabase.CreateAsset(mesh,path); return mesh;
        }
        static GameObject MeshObject(string name,Mesh mesh,Transform parent)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=surface; return go;
        }
        static int TriangleCount(GameObject go)
        {
            int total=0; foreach(var filter in go.GetComponentsInChildren<MeshFilter>(true)) total+=filter.sharedMesh.triangles.Length/3; return total;
        }
        static GameObject SavePrefab(GameObject go)
        {
            var art=go.GetComponent<Origins.TerrainTileArtwork>();
            if(art.decorItems<art.minDecor || art.decorItems>art.maxDecor) throw new Exception("Artwork decor count outside biome budget");
            art.triangles=TriangleCount(go);
            art.treeTriangles=art.trees ? TriangleCount(art.trees.gameObject) : 0;
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+go.name+".prefab");
            UnityEngine.Object.DestroyImmediate(go); return prefab;
        }
        static Mesh ForestGroundFacingUp(Mesh source)
        {
            // The archived forest used a downward winding for its top surface.
            // Copy it into the approved library with upward normals; leave side strata intact.
            var mesh=UnityEngine.Object.Instantiate(source); var indices=mesh.triangles;
            const int surfaceIndicesPerWedge=16*12*6;
            const int wedgeIndices=surfaceIndicesPerWedge+32*(4*6+6*6+6*3);
            if(indices.Length!=6*wedgeIndices) throw new Exception("Archived forest ground topology changed");
            for(int wedge=0;wedge<6;wedge++) for(int i=wedge*wedgeIndices;i<wedge*wedgeIndices+surfaceIndicesPerWedge;i+=3)
            { int temp=indices[i+1]; indices[i+1]=indices[i+2]; indices[i+2]=temp; }
            mesh.triangles=indices;
            var builder=new ForestMeshBuilder(); builder.AddMesh(mesh,Matrix4x4.identity); UnityEngine.Object.DestroyImmediate(mesh);
            return SaveMesh(builder,"Forest25_Ground");
        }
        static int[] Allocate(int[] weights,int budget)
        {
            int total=0,used=0; foreach(int weight in weights) total+=weight;
            var result=new int[weights.Length]; var order=new List<int>();
            for(int i=0;i<weights.Length;i++) { result[i]=weights[i]*budget/total; used+=result[i]; order.Add(i); }
            order.Sort((a,b)=> { int c=(weights[b]*budget%total).CompareTo(weights[a]*budget%total); return c==0 ? a.CompareTo(b) : c; });
            for(int i=0;i<budget-used;i++) result[order[i]]++;
            return result;
        }
        static bool[] Select(int count,int keep)
        {
            if(keep>count) throw new Exception("Insufficient reference decor");
            var order=new int[count]; for(int i=0;i<count;i++) order[i]=i;
            for(int i=count-1;i>0;i--) { int j=random.Next(i+1),temp=order[i]; order[i]=order[j]; order[j]=temp; }
            var result=new bool[count]; for(int i=0;i<keep;i++) result[order[i]]=true; return result;
        }
        static void CopySelected(ForestMeshBuilder target,ForestMeshBuilder source,ref int offset,int objectVertices,int count,int keep,bool replaceStones=false)
        {
            bool[] selected=Select(count,keep);
            for(int i=0;i<count;i++) if(selected[i])
            {
                int first=offset+i*objectVertices; Vector3 root=source.RangeRoot(first,objectVertices);
                if(replaceStones) PlaceStone(target,Origins.TerrainArtKind.Forest,At(Origins.TerrainArtKind.Forest,root));
                else target.AddRange(source,first,objectVertices,first,objectVertices,Variation().Matrix(root,Quaternion.identity)*Matrix4x4.Translate(-root));
            }
            offset+=count*objectVertices;
        }
        static GameObject ImportApprovedForest()
        {
            if(!File.Exists("Assets/Scenes/ForestTileShowcase.unity")) ForestShowcaseBuilder.Generate();
            else EditorSceneManager.OpenScene("Assets/Scenes/ForestTileShowcase.unity");
            GameObject reference=GameObject.Find("06 · Forest neighbour");
            if(!reference) throw new Exception("Forest reference tile missing");
            random=new System.Random(generationSeed);
            int budget=random.Next(MinDecor,MaxDecor+1);
            // Ecology proportions of the approved 25% forest, with a random total.
            int[] counts=Allocate(new[]{3,115,16,5,3,1,0,2,57},budget);
            var transforms=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            var go=new GameObject("Forest25");
            MeshObject("Approved forest ground",ForestGroundFacingUp(reference.transform.GetChild(0).GetComponent<MeshFilter>().sharedMesh),go.transform);
            var treeRoot=new GameObject("Trees — approved 25 percent density").transform; treeRoot.SetParent(go.transform,false);
            var treeParts=new List<Transform>();
            foreach(Transform child in reference.transform)
                if(child.name.StartsWith("Oak ") || child.name.StartsWith("Birch ") || child.name.StartsWith("Pine ")) treeParts.Add(child);
            bool[] selectedTrees=Select(treeParts.Count,counts[0]);
            for(int t=0;t<treeParts.Count;t++) if(selectedTrees[t])
            {
                var original=treeParts[t]; var tree=UnityEngine.Object.Instantiate(original.gameObject,treeRoot,false).transform; tree.name=original.name;
                Transform crown=null;
                foreach(var candidate in transforms) if(candidate.name==original.name+" crown" && (candidate.position-original.position).sqrMagnitude<.00001f) { crown=candidate; break; }
                if(!crown) throw new Exception("Approved forest canopy missing: "+tree.name);
                var copy=UnityEngine.Object.Instantiate(crown.gameObject,tree,false); copy.name="Crown";
                copy.transform.localPosition=Vector3.zero; copy.transform.localRotation=Quaternion.identity; copy.transform.localScale=Vector3.one;
                Variation().Apply(tree);
            }
            var sourcePlants=new ForestMeshBuilder(); var sourceProps=new ForestMeshBuilder();
            Mesh plantMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Forest/Generated/Meshes/Understory_6.asset");
            Mesh propMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Forest/Generated/Meshes/ForestProps_6.asset");
            if(plantMesh.vertexCount!=plantMesh.triangles.Length || propMesh.vertexCount!=propMesh.triangles.Length) throw new Exception("Forest reference topology changed");
            sourcePlants.AddMesh(plantMesh,Matrix4x4.identity); sourceProps.AddMesh(propMesh,Matrix4x4.identity);
            var plants=new ForestMeshBuilder(); var props=new ForestMeshBuilder(); int offset=0;
            // Each source object remains a complete mesh; no blades or flowers are sliced in half.
            CopySelected(plants,sourcePlants,ref offset,8*5*6,460,counts[1]);
            CopySelected(plants,sourcePlants,ref offset,510,65,counts[2]);
            CopySelected(plants,sourcePlants,ref offset,6,230,counts[8]);
            if(offset!=sourcePlants.VertexCount) throw new Exception("Forest plant layout changed");
            offset=0; string[] names={"FernNineFronds","MossyRock","StumpWithGrowthRings","MossyFallenLog","HoneyMushroomCluster"}; int[] amounts={18,11,3,2,8};
            for(int category=0;category<5;category++)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Forest/Generated/Meshes/"+names[category]+".asset");
                CopySelected(props,sourceProps,ref offset,mesh.vertexCount,amounts[category],counts[category+3],category==1);
            }
            if(offset!=sourceProps.VertexCount) throw new Exception("Forest prop layout changed");
            MeshObject("Random sparse forest undergrowth",SaveMesh(plants,"Forest25_Plants",false),go.transform);
            MeshObject("Random sparse forest props",SaveMesh(props,"Forest25_Props"),go.transform);
            var art=go.AddComponent<Origins.TerrainTileArtwork>(); art.kind=Origins.TerrainArtKind.Forest; art.displayName="Лес"; art.trees=treeRoot; art.decorItems=budget; art.generationSeed=generationSeed;
            return SavePrefab(go);
        }
        static float Height(Origins.TerrainArtKind kind,float x,float z)
        {
            if((int)kind>=(int)Origins.TerrainArtKind.Mountain)
                return SpecialHeight(kind,x,z);
            float small=ForestMeshBuilder.Height(x,z);
            if(kind==Origins.TerrainArtKind.Forest) return small;
            if(kind!=Origins.TerrainArtKind.Hills) return small*(kind==Origins.TerrainArtKind.Plains ? .35f : .6f);
            float r=Mathf.Sqrt(x*x+z*z);
            float edge=Mathf.SmoothStep(0,1,Mathf.Clamp01((4.15f-r)/1.05f));
            float a=2.05f*Mathf.Exp(-((x+1.05f)*(x+1.05f)/3.4f+(z-.45f)*(z-.45f)/5.6f));
            float b=1.25f*Mathf.Exp(-((x-1.35f)*(x-1.35f)/2.3f+(z+1.1f)*(z+1.1f)/3.2f));
            return (a+b)*edge+small*.35f;
        }
        static Vector3 At(Origins.TerrainArtKind kind,Vector3 p) { p.y=Height(kind,p.x,p.z); return p; }
        static Vector3 Sample(Origins.TerrainArtKind kind,float inset=.92f)
        {
            int wedge=random.Next(6); float u=F(0,1),v=F(0,1); if(u+v>1) {u=1-u;v=1-v;}
            Vector3 p=(Radial((wedge*60+30)*Mathf.Deg2Rad,Radius)*u+Radial(((wedge+1)*60+30)*Mathf.Deg2Rad,Radius)*v)*inset;
            return At(kind,p);
        }
        static Vector3 Normal(Origins.TerrainArtKind kind,Vector3 p)
        {
            float dx=(Height(kind,p.x+.04f,p.z)-Height(kind,p.x-.04f,p.z))/.08f;
            float dz=(Height(kind,p.x,p.z+.04f)-Height(kind,p.x,p.z-.04f))/.08f;
            return new Vector3(-dx,1,-dz).normalized;
        }
        static Color GroundColor(Origins.TerrainArtKind kind,Vector3 p)
        {
            if((int)kind>=(int)Origins.TerrainArtKind.Mountain)
                return SpecialGroundColor(kind,p);
            float noise=Mathf.PerlinNoise(p.x*.54f+19,p.z*.54f+25);
            Color a=kind==Origins.TerrainArtKind.Meadow ? C("547E3E") : kind==Origins.TerrainArtKind.Plains ? C("948958") : C("677A43");
            Color b=kind==Origins.TerrainArtKind.Meadow ? C("86A65E") : kind==Origins.TerrainArtKind.Plains ? C("B19F6A") : C("8B945B");
            Color color=Color.Lerp(a,b,noise);
            if(kind==Origins.TerrainArtKind.Hills) color=Color.Lerp(color,C("95907D"),Mathf.Clamp01((1-Normal(kind,p).y)*2.2f));
            return color;
        }
        static Mesh TerrainMesh(Origins.TerrainArtKind kind)
        {
            var b=new ForestMeshBuilder(); int rings=kind==Origins.TerrainArtKind.Ocean ? 1 : kind==Origins.TerrainArtKind.Coast ? 16 : kind==Origins.TerrainArtKind.Mountain ? 40 : 32;
            int steps=kind==Origins.TerrainArtKind.Ocean ? 1 : kind==Origins.TerrainArtKind.Coast ? 16 : 24;
            for(int wedge=0;wedge<6;wedge++)
            {
                Vector3 a=Radial((wedge*60+30)*Mathf.Deg2Rad,Radius),c=Radial(((wedge+1)*60+30)*Mathf.Deg2Rad,Radius);
                for(int r=0;r<rings;r++) for(int s=0;s<steps;s++)
                {
                    Vector3 p=At(kind,Vector3.Lerp(a,c,s/(float)steps)*(r/(float)rings));
                    Vector3 q=At(kind,Vector3.Lerp(a,c,(s+1)/(float)steps)*(r/(float)rings));
                    Vector3 t=At(kind,Vector3.Lerp(a,c,(s+1)/(float)steps)*((r+1)/(float)rings));
                    Vector3 u=At(kind,Vector3.Lerp(a,c,s/(float)steps)*((r+1)/(float)rings));
                    b.Triangle(p,t,u,GroundColor(kind,p),GroundColor(kind,t),GroundColor(kind,u));
                    b.Triangle(p,q,t,GroundColor(kind,p),GroundColor(kind,q),GroundColor(kind,t));
                }
                for(int s=0;s<32;s++)
                {
                    Vector3 p=At(kind,Vector3.Lerp(a,c,s/32f)),q=At(kind,Vector3.Lerp(a,c,(s+1)/32f));
                    for(int layer=0;layer<4;layer++)
                    {
                        bool marine=kind==Origins.TerrainArtKind.Coast || kind==Origins.TerrainArtKind.Ocean;
                        float baseY=marine ? -.6f : -.24f,stepY=marine ? .1f : .22f;
                        Vector3 lowP=new Vector3(p.x,baseY-layer*stepY,p.z),lowQ=new Vector3(q.x,lowP.y,q.z);
                        Vector3 highP=layer==0 ? p : new Vector3(p.x,baseY-(layer-1)*stepY,p.z),highQ=layer==0 ? q : new Vector3(q.x,highP.y,q.z);
                        Color soil=SoilColor(kind,layer)*F(.88f,1.07f); soil.a=1;
                        b.Quad(highP,highQ,lowQ,lowP,soil);
                    }
                    if(kind!=Origins.TerrainArtKind.Ocean) b.Tube(p+Vector3.up*.01f,q+Vector3.up*.01f,.017f,.017f,EdgeColor(kind),6,1,0);
                }
            }
            return SaveMesh(b,kind+"_Ground");
        }
        static void Flower(ForestMeshBuilder b,Vector3 root,int type,bool dry=false)
        {
            var model=new ForestMeshBuilder(); FlowerModel(model,Vector3.zero,type,dry);
            AppendModel(b,model,root,Origins.TerrainArtKind.Meadow);
        }
        static void FlowerModel(ForestMeshBuilder b,Vector3 root,int type,bool dry)
        {
            Color stem=dry ? C("767944") : C("508538"); float height=F(.18f,.34f);
            Vector3 top=root+new Vector3(F(-.04f,.04f),height,F(-.04f,.04f));
            b.Tube(root,top,.007f,.003f,stem,8,4,0);
            b.Blade(root+Vector3.up*height*.3f,F(0,6.28f),.13f,.046f,.10f,stem,5);
            Color petal=C(new[]{"EFE6C8","B5A4D7","D6AF45","8BAACE","D88E87"}[type%5]);
            for(int n=0;n<8;n++)
            {
                float angle=n*6.28f/8; Vector3 d=Radial(angle,.045f),side=Radial(angle+1.57f,.023f);
                Vector3 tip=top+d*1.75f+Vector3.up*.006f,mid=top+d+Vector3.up*.025f;
                b.Triangle(top,mid-side,mid,petal*.87f,petal,petal);
                b.Triangle(top,mid,mid+side,petal*.87f,petal,petal);
                b.Triangle(mid-side,tip,mid,petal,petal*1.04f,petal);
                b.Triangle(mid,tip,mid+side,petal,petal*1.04f,petal);
            }
            b.Blob(top+Vector3.up*.014f,new Vector3(.022f,.012f,.022f),C("D4AD43"),type,12,8,0);
        }
        static Mesh Clover()
        {
            var b=new ForestMeshBuilder(); Color green=C("5D8B3C");
            for(int n=0;n<4;n++)
            {
                Vector3 p=Radial(n*2.4f,.09f*n,.025f+n*.018f);
                b.Tube(new Vector3(p.x,0,p.z),p,.004f,.002f,green,6,2);
                for(int leaf=0;leaf<3;leaf++) b.Blob(p+Radial(leaf*2.094f,.047f),new Vector3(.045f,.009f,.038f),green,n+leaf,16,8,.04f);
            }
            return SaveMesh(b,"CloverCluster");
        }
        static Mesh Shrub(bool dry)
        {
            var b=new ForestMeshBuilder(); Color stem=C(dry ? "7C6950" : "646B3C");
            for(int n=0;n<8;n++)
            {
                Vector3 tip=Radial(n*2.4f,F(.12f,.23f),F(.20f,.38f));
                b.Tube(Vector3.zero,tip,.012f,.003f,stem,10,5);
                for(int leaf=0;leaf<11;leaf++)
                {
                    Vector3 p=Vector3.Lerp(Vector3.zero,tip,F(.35f,1))+Radial(F(0,6.28f),F(.015f,.08f));
                    b.Blob(p,new Vector3(.055f,.018f,.045f),dry ? C("8F8B54") : C("5A7D3E"),n+leaf,12,8,.05f);
                }
            }
            return SaveMesh(b,dry ? "DryShrub" : "LowLeafyShrub");
        }
        static Mesh SeedHeads()
        {
            var b=new ForestMeshBuilder(); Color stem=C("A89B60");
            for(int n=0;n<3;n++)
            {
                Vector3 root=Radial(n*2.1f,.07f),tip=root+new Vector3(.03f,.42f+n*.035f,.015f);
                b.Tube(root,tip,.005f,.002f,stem,8,7,0);
                for(int seed=0;seed<6;seed++)
                {
                    Vector3 p=tip-Vector3.up*seed*.018f+Radial(seed*2.4f,.018f);
                    b.Blob(p,new Vector3(.016f,.026f,.012f),C("B9A979"),n+seed,12,8,.04f);
                }
            }
            return SaveMesh(b,"WildGrassSeedHeads");
        }
        static Mesh Twig()
        {
            var b=new ForestMeshBuilder(); Color wood=C("8B7555");
            b.Tube(new Vector3(-.16f,.016f,0),new Vector3(.18f,.028f,.035f),.017f,.006f,wood,12,5);
            b.Tube(new Vector3(-.02f,.020f,.015f),new Vector3(.09f,.033f,.15f),.009f,.002f,wood,10,4);
            return SaveMesh(b,"DryGroundTwig");
        }
        static void Place(ForestMeshBuilder b,Mesh mesh,Origins.TerrainArtKind kind,Vector3 p,float scale,bool tilt=false)
        {
            Quaternion rotation=tilt ? Quaternion.FromToRotation(Vector3.up,Normal(kind,p)) : Quaternion.identity;
            b.AddMesh(mesh,Variation().Matrix(p,rotation,scale));
        }
        static GameObject CreateOpenLand(Origins.TerrainArtKind kind,Mesh clover,Mesh shrub,Mesh dryShrub,Mesh seedHeads,Mesh twig,Mesh pebble,Mesh outcrop,Mesh stump)
        {
            int tileSeed=unchecked(generationSeed+(int)kind*137);
            random=new System.Random(tileSeed); int budget=random.Next(MinDecor,MaxDecor+1);
            int[] weights=kind==Origins.TerrainArtKind.Meadow ? new[]{115,48,24,4,6,1,4,0} : kind==Origins.TerrainArtKind.Plains ? new[]{115,8,0,8,10,1,24,36} : new[]{115,12,8,18,24,1,24,0};
            int[] counts=Allocate(weights,budget);
            string name=kind==Origins.TerrainArtKind.Meadow ? "Meadow25" : kind==Origins.TerrainArtKind.Plains ? "Plains25" : "Hills25";
            var go=new GameObject(name); var art=go.AddComponent<Origins.TerrainTileArtwork>(); art.kind=kind;
            art.decorItems=budget; art.generationSeed=tileSeed;
            art.displayName=kind==Origins.TerrainArtKind.Meadow ? "Луг" : kind==Origins.TerrainArtKind.Plains ? "Равнина" : "Холмы";
            MeshObject("Sculpted ground and soil strata",TerrainMesh(kind),go.transform);
            var plants=new ForestMeshBuilder(); var props=new ForestMeshBuilder(); int placed=0;
            bool dry=kind==Origins.TerrainArtKind.Plains;
            for(int n=0;n<counts[0];n++)
            {
                Vector3 p=Sample(kind); float height=F(dry ? .19f : .15f,dry ? .41f : .32f);
                Color green=dry ? Color.Lerp(C("8E9754"),C("B4A472"),F(0,1)) : Color.Lerp(C("4F7939"),C("80A252"),F(0,1));
                var tuft=new ForestMeshBuilder();
                for(int blade=0;blade<8;blade++)
                {
                    Vector3 root=Radial(F(0,6.28f),F(0,.1f));
                    tuft.Blade(root,F(0,6.28f),height*F(.65f,1.25f),F(.027f,.05f),F(.07f,.18f),green,5);
                }
                AppendModel(plants,tuft,p,kind,true);
                placed++;
            }
            int flowers=counts[1];
            for(int n=0;n<flowers;n++) { Flower(plants,Sample(kind),n%5,dry); placed++; }
            int clovers=counts[2];
            for(int n=0;n<clovers;n++) { Place(props,clover,kind,Sample(kind),1); placed++; }
            int shrubs=counts[3];
            for(int n=0;n<shrubs;n++) { Place(props,dry ? dryShrub : shrub,kind,Sample(kind),1); placed++; }
            int stones=counts[4];
            for(int n=0;n<stones;n++)
            {
                Vector3 p=n==0 ? At(kind,new Vector3(1.6f,0,-2.1f)) : Sample(kind);
                PlaceStone(props,kind,p,kind==Origins.TerrainArtKind.Hills && n<9); placed++;
            }
            // A single old stump is a rare detail, not a forest canopy.
            for(int n=0;n<counts[5];n++) { Place(props,stump,kind,At(kind,new Vector3(.45f,0,-2.05f)),1); placed++; }
            int twigs=counts[6];
            for(int n=0;n<twigs;n++) { Place(props,twig,kind,Sample(kind),1,true); placed++; }
            if(dry) for(int n=0;n<counts[7];n++) { Place(props,seedHeads,kind,Sample(kind),1); placed++; }
            if(placed!=budget || placed<MinDecor || placed>MaxDecor) throw new Exception("Unexpected density budget: "+kind+" = "+placed);
            MeshObject("Curved grass blades and wildflowers",SaveMesh(plants,name+"_Plants",false),go.transform);
            MeshObject("Small shrubs, pebbles, stump and natural details",SaveMesh(props,name+"_Props"),go.transform);
            if(kind==Origins.TerrainArtKind.Hills && Height(kind,-1,.45f)<1.8f) throw new Exception("Hill relief is missing");
            return SavePrefab(go);
        }
        [MenuItem("Истоки/Графика/Открыть витрину местности 25%")]
        public static void OpenScene()
        {
            if(!File.Exists(ScenePath)) Generate(); else EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("Истоки/Графика/Пересобрать все виды местности")]
        public static void Generate()
        {
            GenerateWithSeed(Guid.NewGuid().GetHashCode()&int.MaxValue);
        }
        public static void GenerateWithSeed(int seed)
        {
            generationSeed=seed;
            Directory.CreateDirectory(Root+"/Meshes"); Directory.CreateDirectory(Root+"/Prefabs"); Directory.CreateDirectory("Assets/Resources"); AssetDatabase.Refresh();
            surface=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Forest/Generated/ForestSurface.mat");
            if(!surface) { ForestShowcaseBuilder.Generate(); surface=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Forest/Generated/ForestSurface.mat"); }
            variedModels=0; minScale=minYaw=float.MaxValue; maxScale=maxYaw=float.MinValue;
            BuildDecorLibrary();
            GameObject forest=ImportApprovedForest();
            if(!surface) surface=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Forest/Generated/ForestSurface.mat");
            random=new System.Random(43016);
            Mesh clover=Clover(),shrub=Shrub(false),dryShrub=Shrub(true),seedHeads=SeedHeads(),twig=Twig(),pebble=stones[0],outcrop=outcrops[0];
            Mesh stump=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Forest/Generated/Meshes/StumpWithGrowthRings.asset");
            if(!stump || !surface) throw new Exception("Forest artwork dependencies missing");
            GameObject meadow=CreateOpenLand(Origins.TerrainArtKind.Meadow,clover,shrub,dryShrub,seedHeads,twig,pebble,outcrop,stump);
            GameObject plains=CreateOpenLand(Origins.TerrainArtKind.Plains,clover,shrub,dryShrub,seedHeads,twig,pebble,outcrop,stump);
            GameObject hills=CreateOpenLand(Origins.TerrainArtKind.Hills,clover,shrub,dryShrub,seedHeads,twig,pebble,outcrop,stump);
            GameObject mountain=CreateMountain(pebble,shrub);
            GameObject desert=CreateDesert(pebble,dryShrub,twig);
            GameObject coast=CreateCoast(pebble,twig);
            GameObject ocean=CreateOcean();
            GameObject swamp=CreateSwamp(pebble,twig,stump);
            GameObject tundra=CreateTundra(pebble,shrub);
            var catalog=AssetDatabase.LoadAssetAtPath<Origins.TerrainArtCatalog>("Assets/Resources/TerrainArtCatalog.asset");
            if(!catalog) { catalog=ScriptableObject.CreateInstance<Origins.TerrainArtCatalog>(); AssetDatabase.CreateAsset(catalog,"Assets/Resources/TerrainArtCatalog.asset"); }
            catalog.defaultDensity=.25f; catalog.minDecorItems=MinDecor; catalog.maxDecorItems=MaxDecor; catalog.generationSeed=generationSeed; catalog.forest=forest; catalog.meadow=meadow; catalog.plains=plains; catalog.hills=hills;
            catalog.mountain=mountain; catalog.desert=desert; catalog.coast=coast; catalog.ocean=ocean;
            catalog.swamp=swamp; catalog.tundra=tundra;
            EditorUtility.SetDirty(catalog);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Artwork preview — no strategy simulation").AddComponent<Origins.ArtPreviewScene>();
            var prefabs=new[]{forest,meadow,plains,hills,mountain,desert,coast,ocean,swamp,tundra}; var tiles=new Origins.TerrainTileArtwork[10]; var detailTargets=new Vector3[10];
            Vector3 right=new Vector3(.7071068f,0,-.7071068f),forward=new Vector3(.7071068f,0,.7071068f);
            var report=new System.Text.StringBuilder("Terrain artwork: land 150-250; desert 30-60 including dunes; coast 40-80; ocean 0 small props\nGeneration seed: "+generationSeed+"\nHex radius 4.5\nWater: opaque shader, two analytic waves, no CPU mesh updates, no reflection/refraction passes\n");
            int total=0;
            for(int i=0;i<prefabs.Length;i++)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[i]);
                go.transform.position=right*((i%4-(i<8 ? 1.5f : .5f))*10.6f)+forward*((1-i/4)*11.8f);
                tiles[i]=go.GetComponent<Origins.TerrainTileArtwork>(); total+=tiles[i].triangles;
                Vector3 p=i==6 ? new Vector3(.5f,0,-1.8f) : new Vector3(.8f,0,-2.05f); p.y=i==0 ? .08f : i==7 ? .04f : Height(tiles[i].kind,p.x,p.z);
                detailTargets[i]=go.transform.position+p;
                report.Append(tiles[i].displayName+": "+tiles[i].decorItems+" items ("+tiles[i].majorFeatures+" landforms), "+tiles[i].triangles+" triangles\n");
            }
            var sun=new GameObject("Late morning sun").AddComponent<Light>(); sun.type=LightType.Directional; sun.transform.rotation=Quaternion.Euler(53,-38,0);
            sun.color=new Color(1,.93f,.78f); sun.intensity=1.3f; sun.shadows=LightShadows.Soft; sun.shadowBias=.045f; sun.shadowNormalBias=.12f;
            RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(.65f,.72f,.78f);
            RenderSettings.ambientEquatorColor=new Color(.48f,.53f,.45f); RenderSettings.ambientGroundColor=new Color(.29f,.33f,.23f); RenderSettings.skybox=null; RenderSettings.fog=false;
            var camera=new GameObject("Fixed isometric camera").AddComponent<Camera>(); camera.tag="MainCamera"; camera.orthographic=true; camera.orthographicSize=11;
            camera.orthographicSize=20;
            camera.transform.rotation=Quaternion.Euler(35.2643897f,45,0); camera.transform.position=Vector3.up-camera.transform.forward*65;
            camera.nearClipPlane=.2f; camera.farClipPlane=160; camera.backgroundColor=C("182623"); camera.clearFlags=CameraClearFlags.SolidColor; camera.allowHDR=false;
            camera.gameObject.AddComponent<AudioListener>(); var viewer=camera.gameObject.AddComponent<Origins.TerrainPreviewCamera>(); viewer.tiles=tiles; viewer.detailTargets=detailTargets;
            EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
            report.Append("Total: "+total+" triangles\nFixed orthographic camera: pitch 35.2643897, yaw 45\n");
            report.Append($"Varied models: {variedModels}; scale {minScale:F4}..{maxScale:F4}; yaw {minYaw:F2}..{maxYaw:F2}\nStone silhouettes: {stones.Length}; sizes: small and large; cactus models: {cacti.Length}; snow conifers: {coniferWood.Length}\n");
            int expectedModels=0; foreach(var tile in tiles) expectedModels+=tile.decorItems-tile.majorFeatures;
            if(variedModels!=expectedModels) throw new Exception($"Variation coverage invalid: {variedModels} / {expectedModels}");
            File.WriteAllText("Docs/terrain-art-statistics.txt",report.ToString());
            Debug.Log("TERRAIN: ten prefabs saved; biome budgets checked; "+total+" triangles");
        }
        [MenuItem("Истоки/Графика/Собрать просмотр местности 25%")]
        public static void BuildPreview()
        {
            var previous=AssetDatabase.LoadAssetAtPath<Origins.TerrainArtCatalog>("Assets/Resources/TerrainArtCatalog.asset");
            if(previous) GenerateWithSeed(previous.generationSeed); else Generate();
            Directory.CreateDirectory("Builds/TerrainPreview");
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{ScenePath},locationPathName="Builds/TerrainPreview/TerrainPreview.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Terrain preview build failed: "+report.summary.result);
            Debug.Log("TERRAIN: Windows preview build succeeded");
        }
        [MenuItem("Истоки/Графика/Собрать витрину и игру")]
        public static void BuildArtworkAndGame()
        {
            BuildPreview();
            Origins.Editor.ProjectSetup.BuildWindows();
        }
    }
}
