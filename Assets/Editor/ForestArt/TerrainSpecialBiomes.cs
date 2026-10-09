using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kind=Origins.TerrainArtKind;

namespace Origins.Editor.ForestArt
{
    public static partial class TerrainShowcaseBuilder
    {
        static float CoastBoundary(float z) { return .35f+.22f*Mathf.Sin(z*.85f)+.08f*Mathf.Sin(z*2.1f); }
        static float CoastDistance(Vector3 p) { return p.x-CoastBoundary(p.z); }
        static float SpecialHeight(Kind kind,float x,float z)
        {
            if(kind==Kind.Swamp || kind==Kind.Tundra) return WetColdHeight(kind,x,z);
            if(kind==Kind.Ocean) return -.36f;
            if(kind==Kind.Coast) return Mathf.Clamp((CoastBoundary(z)-x)*.13f,-.48f,.60f);
            float r=Mathf.Sqrt(x*x+z*z),edge=Mathf.SmoothStep(0,1,Mathf.Clamp01((4.3f-r)/1.15f));
            if(kind==Kind.Mountain)
            {
                Vector2[] centers={new Vector2(-1.1f,.65f),new Vector2(1.25f,.50f),new Vector2(.3f,-1.8f)};
                float[] heights={4.65f,3.60f,2.65f}; float height=0;
                for(int i=0;i<3;i++)
                {
                    float dx=x-centers[i].x,dz=z-centers[i].y,angle=Mathf.Atan2(dz,dx);
                    float distance=Mathf.Sqrt(dx*dx+dz*dz)*(1+.13f*Mathf.Sin(angle*5+i)+.055f*Mathf.Sin(angle*9-i));
                    height=Mathf.Max(height,heights[i]*Mathf.Clamp01(1-distance/(i==0 ? 3.1f : 2.4f)));
                }
                return height*edge+ForestMeshBuilder.Height(x,z)*.12f;
            }
            Vector2[] dunes={new Vector2(-1.7f,1.25f),new Vector2(.4f,-.6f),new Vector2(2.0f,1.1f),new Vector2(-1.1f,-2.5f)};
            float[] angles={.25f,.55f,-.6f,.95f},peaks={1.55f,1.15f,1.20f,.70f}; float dune=0;
            for(int i=0;i<4;i++)
            {
                float dx=x-dunes[i].x,dz=z-dunes[i].y;
                float u=dx*Mathf.Cos(angles[i])+dz*Mathf.Sin(angles[i]),v=-dx*Mathf.Sin(angles[i])+dz*Mathf.Cos(angles[i]);
                // A broad windward face and a steeper slip face form a visible dune crest.
                float cross=Mathf.Exp(-Mathf.Abs(v)/(v<0 ? 1.30f : .46f));
                dune=Mathf.Max(dune,peaks[i]*cross*Mathf.Exp(-u*u/7.5f));
            }
            return dune*edge+.018f*Mathf.Sin((x*.5f+z)*20)*edge;
        }
        static Color SpecialGroundColor(Kind kind,Vector3 p)
        {
            float noise=Mathf.PerlinNoise(p.x*.7f+17,p.z*.7f+21);
            if(kind==Kind.Swamp) return Color.Lerp(C("526044"),C("7F8653"),noise);
            if(kind==Kind.Tundra) return Color.Lerp(C("D6E2EB"),C("EDF2F4"),noise);
            if(kind==Kind.Ocean) return C("34586C");
            if(kind==Kind.Coast) return Color.Lerp(C("B9A275"),C("D6C49A"),noise);
            if(kind==Kind.Desert)
            {
                float ripples=.025f*Mathf.Sin((p.x*.5f+p.z)*25);
                return Color.Lerp(C("B99760"),C("D7B77A"),Mathf.Clamp01(noise+ripples));
            }
            Color rock=Color.Lerp(C("68706E"),C("96998D"),noise);
            float strata=Mathf.Sin(p.y*8+p.x*.7f+p.z*.3f)*.035f; rock*=1+strata; rock.a=1;
            float snowLine=3.15f+.16f*Mathf.Sin(p.x*3+p.z*2)+.12f*(1-Normal(kind,p).y);
            return Color.Lerp(rock,C("E4E5DC"),Mathf.SmoothStep(0,1,Mathf.Clamp01((p.y-snowLine)*3)));
        }
        static Color SoilColor(Kind kind,int layer)
        {
            Color a=kind==Kind.Desert ? C("B28B55") : kind==Kind.Coast ? C("998566") : kind==Kind.Ocean ? C("4A6068") : kind==Kind.Mountain ? C("686963") : C(kind==Kind.Plains ? "806747" : "685238");
            return Color.Lerp(a,C("302C24"),layer/3f);
        }
        static Color EdgeColor(Kind kind)
        {
            if(kind==Kind.Desert || kind==Kind.Coast) return C("D5BA88");
            if(kind==Kind.Mountain) return C("8B9182");
            if(kind==Kind.Tundra) return C("CFDBE4");
            if(kind==Kind.Ocean) return C("356781");
            return kind==Kind.Plains ? C("B4A471") : C("819553");
        }
        static Origins.TerrainTileArtwork SpecialRoot(string name,Kind kind,string display,int min,int max,int features=0)
        {
            random=new System.Random(unchecked(generationSeed+(int)kind*137));
            var go=new GameObject(name); var art=go.AddComponent<Origins.TerrainTileArtwork>();
            art.kind=kind; art.displayName=display; art.minDecor=min; art.maxDecor=max; art.decorItems=random.Next(min,max+1); art.majorFeatures=features;
            art.generationSeed=unchecked(generationSeed+(int)kind*137);
            MeshObject("Sculpted biome surface and strata",TerrainMesh(kind),go.transform);
            return art;
        }
        static Vector3 LowHabitat(Kind kind,float height)
        {
            Vector3 p=Sample(kind); int tries=0; while(p.y>height && tries++<60) p=Sample(kind); return p;
        }
        static void DryTuft(ForestMeshBuilder b,Vector3 p,Kind kind,Color tint,float height)
        {
            var model=new ForestMeshBuilder();
            for(int blade=0;blade<7;blade++)
            {
                Vector3 root=Radial(F(0,6.28f),F(0,.065f));
                model.Blade(root,F(0,6.28f),height*F(.65f,1.25f),F(.018f,.035f),F(.05f,.12f),tint,5);
            }
            AppendModel(b,model,p,kind,true);
        }
        static GameObject CreateMountain(Mesh pebble,Mesh shrub)
        {
            var art=SpecialRoot("Mountain25",Kind.Mountain,"Горы",150,250,3); var props=new ForestMeshBuilder(); var plants=new ForestMeshBuilder();
            int[] counts=Allocate(new[]{145,35,17},art.decorItems-3); int placed=3;
            for(int i=0;i<counts[0];i++) { PlaceStone(props,Kind.Mountain,Sample(Kind.Mountain)); placed++; }
            for(int i=0;i<counts[1];i++) { DryTuft(plants,LowHabitat(Kind.Mountain,1.6f),Kind.Mountain,C("76874D"),.14f); placed++; }
            for(int i=0;i<counts[2];i++) { Place(props,shrub,Kind.Mountain,LowHabitat(Kind.Mountain,1.2f),1); placed++; }
            MeshObject("Loose scree and hardy low shrubs",SaveMesh(props,"Mountain_Props"),art.transform);
            MeshObject("Sparse alpine grass",SaveMesh(plants,"Mountain_Plants",false),art.transform);
            if(placed!=art.decorItems || Height(Kind.Mountain,-1.1f,.65f)<4.2f) throw new Exception("Mountain shape or count invalid");
            return SavePrefab(art.gameObject);
        }
        static GameObject CreateDesert(Mesh pebble,Mesh dryShrub,Mesh twig)
        {
            var art=SpecialRoot("DesertSparse",Kind.Desert,"Пустыня",30,60,4); var props=new ForestMeshBuilder(); var plants=new ForestMeshBuilder();
            int[] counts=Allocate(new[]{14,18,6,8},art.decorItems-4); int placed=4;
            for(int i=0;i<counts[0];i++) { PlaceStone(props,Kind.Desert,Sample(Kind.Desert)); placed++; }
            for(int i=0;i<counts[1];i++) { Place(plants,cacti[i%cacti.Length],Kind.Desert,Sample(Kind.Desert),1); placed++; }
            for(int i=0;i<counts[2];i++) { Place(props,dryShrub,Kind.Desert,Sample(Kind.Desert),1); placed++; }
            for(int i=0;i<counts[3];i++) { Place(props,twig,Kind.Desert,Sample(Kind.Desert),1,true); placed++; }
            MeshObject("Rare stones, dry shrubs and twigs",SaveMesh(props,"Desert_Props"),art.transform);
            MeshObject("Saguaro, barrel and prickly pear cacti",SaveMesh(plants,"Desert_Plants"),art.transform);
            if(placed!=art.decorItems || Height(Kind.Desert,-1.7f,1.25f)<1.1f) throw new Exception("Desert dunes or count invalid");
            return SavePrefab(art.gameObject);
        }
        static Mesh Shell()
        {
            var b=new ForestMeshBuilder(); b.Blob(new Vector3(0,.025f,0),new Vector3(.075f,.028f,.058f),C("D9CDB1"),9,20,12,.05f);
            for(int i=0;i<7;i++)
            {
                float angle=-1.2f+i*.4f;
                b.Tube(new Vector3(0,.04f,-.02f),Radial(angle,.059f,.040f),.0028f,.001f,C("B4A58A"),5,3,0);
            }
            return SaveMesh(b,"BeachShell");
        }
        static Vector3 BeachSample()
        {
            Vector3 p=Sample(Kind.Coast); int tries=0;
            while(CoastDistance(p)>-.25f && tries++<80) p=Sample(Kind.Coast); return p;
        }
        static GameObject CreateCoast(Mesh pebble,Mesh twig)
        {
            var art=SpecialRoot("CoastSparse",Kind.Coast,"Побережье",40,80); var props=new ForestMeshBuilder(); var plants=new ForestMeshBuilder(); Mesh shell=Shell();
            int[] counts=Allocate(new[]{14,20,15,7},art.decorItems); int placed=0;
            for(int i=0;i<counts[0];i++) { PlaceStone(props,Kind.Coast,BeachSample()); placed++; }
            for(int i=0;i<counts[1];i++) { Place(props,shell,Kind.Coast,i==0 ? At(Kind.Coast,new Vector3(-.6f,0,-1.8f)) : BeachSample(),1,true); placed++; }
            for(int i=0;i<counts[2];i++) { DryTuft(plants,BeachSample(),Kind.Coast,C("9C9F61"),.24f); placed++; }
            for(int i=0;i<counts[3];i++) { Place(props,twig,Kind.Coast,BeachSample(),1,true); placed++; }
            MeshObject("Beach pebbles, shells and washed up wood",SaveMesh(props,"Coast_Props"),art.transform);
            MeshObject("Dune grass above the shoreline",SaveMesh(plants,"Coast_Plants",false),art.transform);
            CreateWater(art,true); if(placed!=art.decorItems) throw new Exception("Coast decor count invalid");
            return SavePrefab(art.gameObject);
        }
        static GameObject CreateOcean()
        {
            var art=SpecialRoot("OceanWater",Kind.Ocean,"Океан",0,0); CreateWater(art,false);
            return SavePrefab(art.gameObject);
        }
        static List<Vector3> ClipSea(Vector3 a,Vector3 b,Vector3 c)
        {
            var input=new[]{a,b,c}; var output=new List<Vector3>(4);
            for(int i=0;i<3;i++)
            {
                Vector3 p=input[i],q=input[(i+1)%3]; float dp=CoastDistance(p),dq=CoastDistance(q);
                if(dp>=0) output.Add(p);
                if((dp>=0)!=(dq>=0)) output.Add(Vector3.Lerp(p,q,dp/(dp-dq)));
            }
            return output;
        }
        static Color WaterColor(Vector3 p,bool coast)
        {
            float distance=coast ? Mathf.Max(0,CoastDistance(p)) : 3;
            Color color=coast ? Color.Lerp(C("60AAA9"),C("286A8C"),Mathf.Clamp01(distance/3.8f)) : Color.Lerp(C("245B82"),C("347B98"),Mathf.PerlinNoise(p.x*.25f+19,p.z*.25f+23));
            color.a=coast ? Mathf.Clamp01(distance/.85f) : 1; return color;
        }
        static void WaterTriangle(ForestMeshBuilder mesh,Vector3 a,Vector3 b,Vector3 c,bool coast)
        {
            if(!coast) { mesh.Triangle(a,b,c,WaterColor(a,false),WaterColor(b,false),WaterColor(c,false)); return; }
            var points=ClipSea(a,b,c);
            for(int i=1;i<points.Count-1;i++) mesh.Triangle(points[0],points[i],points[i+1],WaterColor(points[0],true),WaterColor(points[i],true),WaterColor(points[i+1],true));
        }
        static void CreateWater(Origins.TerrainTileArtwork art,bool coast)
        {
            var shader=Shader.Find("Origins/Simple Waves"); if(!shader) throw new Exception("Water shader missing");
            bool swamp=art.kind==Kind.Swamp;
            string name=swamp ? "SwampWater" : coast ? "CoastalWater" : "OceanWater",path=Root+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material) { material=new Material(shader); AssetDatabase.CreateAsset(material,path); }
            material.shader=shader; material.SetFloat("_Amplitude",swamp ? .006f : coast ? .025f : .035f); material.SetFloat("_PreviewTime",-1);
            material.SetFloat("_WaveScale",2.7f); material.SetFloat("_WaveSpeed",.55f);
            material.SetFloat("_RippleStrength",swamp ? .2f : 1);
            material.enableInstancing=true; EditorUtility.SetDirty(material);
            var water=new ForestMeshBuilder(); var sides=new ForestMeshBuilder(); const int rings=12,steps=12;
            for(int wedge=0;wedge<6;wedge++)
            {
                Vector3 a=Radial((wedge*60+30)*Mathf.Deg2Rad,Radius),b=Radial(((wedge+1)*60+30)*Mathf.Deg2Rad,Radius);
                for(int r=0;r<rings;r++) for(int s=0;s<steps;s++)
                {
                    Vector3 p=Vector3.Lerp(a,b,s/(float)steps)*(r/(float)rings)+Vector3.up*.035f;
                    Vector3 q=Vector3.Lerp(a,b,(s+1)/(float)steps)*(r/(float)rings)+Vector3.up*.035f;
                    Vector3 t=Vector3.Lerp(a,b,(s+1)/(float)steps)*((r+1)/(float)rings)+Vector3.up*.035f;
                    Vector3 u=Vector3.Lerp(a,b,s/(float)steps)*((r+1)/(float)rings)+Vector3.up*.035f;
                    if(swamp) { SwampWaterTriangle(water,p,t,u); SwampWaterTriangle(water,p,q,t); }
                    else { WaterTriangle(water,p,t,u,coast); WaterTriangle(water,p,q,t,coast); }
                }
                for(int s=0;s<32;s++)
                {
                    if(swamp) continue;
                    Vector3 p=Vector3.Lerp(a,b,s/32f),q=Vector3.Lerp(a,b,(s+1)/32f);
                    if(coast)
                    {
                        float dp=CoastDistance(p),dq=CoastDistance(q); if(dp<0 && dq<0) continue;
                        if(dp<0) p=Vector3.Lerp(p,q,dp/(dp-dq)); else if(dq<0) q=Vector3.Lerp(p,q,dp/(dp-dq));
                    }
                    sides.Quad(p+Vector3.up*.035f,q+Vector3.up*.035f,At(art.kind,q),At(art.kind,p),coast ? C("418391") : C("2B6484"));
                }
            }
            var go=MeshObject("Water surface — two gentle shader waves",SaveMesh(water,name+"_Surface"),art.transform);
            var renderer=go.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material; renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            MeshObject("Water edge cross-section",SaveMesh(sides,name+"_Edges"),art.transform);
            art.animatedWater=true;
            if(water.IndexCount/3>2000) throw new Exception("Water triangle budget exceeded");
        }
    }
}
