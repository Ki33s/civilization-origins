using System;
using System.Collections.Generic;
using UnityEngine;
using Kind=Origins.TerrainArtKind;

namespace Origins.Editor.ForestArt
{
    public static partial class TerrainShowcaseBuilder
    {
        static float WetColdHeight(Kind kind,float x,float z)
        {
            if(kind==Kind.Tundra) return .08f+.10f*Mathf.Sin(x*.7f)*Mathf.Cos(z*.8f)+ForestMeshBuilder.Height(x,z)*.4f;
            float a=Mathf.Exp(-((x+1.35f)*(x+1.35f)/1.9f+(z-.8f)*(z-.8f)/1.5f));
            float b=Mathf.Exp(-((x-1.4f)*(x-1.4f)/1.7f+(z+.7f)*(z+.7f)/2.1f));
            float c=Mathf.Exp(-(x*x/1.4f+(z+2.4f)*(z+2.4f)/.7f));
            return .16f-.40f*Mathf.Max(a,Mathf.Max(b,c))+ForestMeshBuilder.Height(x,z)*.2f;
        }
        static Vector3 DryHabitat(Kind kind)
        {
            for(int i=0;i<200;i++) { var p=Sample(kind); if(p.y>.065f) return p; }
            throw new Exception("No exposed bank for wetland decoration");
        }
        static void Moss(ForestMeshBuilder mesh,Vector3 p,Kind kind,bool lichen)
        {
            var model=new ForestMeshBuilder();
            for(int i=0;i<5;i++)
            {
                Vector3 root=Radial(F(0,6.28f),F(0,.15f));
                Color tint=lichen ? Color.Lerp(C("DCE6ED"),C("EFF3F5"),F(0,1)) : Color.Lerp(C("718341"),C("909951"),F(0,1));
                model.Blob(root+Vector3.up*.017f,new Vector3(F(.06f,.12f),F(.019f,.035f),F(.04f,.09f)),tint,i,12,6,.11f);
            }
            AppendModel(mesh,model,p,kind,true);
        }
        static void Reeds(ForestMeshBuilder mesh,Vector3 p)
        {
            var model=new ForestMeshBuilder();
            for(int i=0;i<7;i++)
            {
                Vector3 root=Radial(F(0,6.28f),F(0,.10f));
                model.Blade(root,F(0,6.28f),F(.35f,.60f),F(.022f,.035f),F(.08f,.17f),C("627A37"),7);
                if(i>=3) continue;
                Vector3 top=root+new Vector3(F(-.06f,.06f),F(.54f,.8f),F(-.06f,.06f));
                model.Tube(root,top,.010f,.006f,C("72824B"),8,5,0);
                model.Blob(top,new Vector3(.025f,.10f,.025f),C("725A3A"),i,12,8,.025f);
            }
            AppendModel(mesh,model,p,Kind.Swamp);
        }
        static Color SwampWaterColor(Vector3 p)
        {
            Color tint=Color.Lerp(C("365F59"),C("537568"),Mathf.PerlinNoise(p.x*.5f+8,p.z*.5f+2));
            // Still ponds have faint ripples, with no marine foam at their edges.
            tint.a=1; return tint;
        }
        static void SwampWaterTriangle(ForestMeshBuilder mesh,Vector3 a,Vector3 b,Vector3 c)
        {
            var input=new[]{a,b,c}; var points=new List<Vector3>(4);
            for(int i=0;i<3;i++)
            {
                Vector3 p=input[i],q=input[(i+1)%3];
                float dp=.025f-Height(Kind.Swamp,p.x,p.z),dq=.025f-Height(Kind.Swamp,q.x,q.z);
                if(dp>=0) points.Add(p);
                if((dp>=0)!=(dq>=0)) points.Add(Vector3.Lerp(p,q,dp/(dp-dq)));
            }
            for(int i=1;i<points.Count-1;i++) mesh.Triangle(points[0],points[i],points[i+1],SwampWaterColor(points[0]),SwampWaterColor(points[i]),SwampWaterColor(points[i+1]));
        }
        static GameObject CreateSwamp(Mesh pebble,Mesh twig,Mesh stump)
        {
            var art=SpecialRoot("Swamp25",Kind.Swamp,"Болото",150,250);
            var plants=new ForestMeshBuilder(); var props=new ForestMeshBuilder();
            int[] counts=Allocate(new[]{95,45,30,12,12,6},art.decorItems); int placed=0;
            for(int i=0;i<counts[0];i++) { Reeds(plants,Sample(Kind.Swamp)); placed++; }
            for(int i=0;i<counts[1];i++) { DryTuft(plants,DryHabitat(Kind.Swamp),Kind.Swamp,C("718C40"),.24f); placed++; }
            for(int i=0;i<counts[2];i++) { Moss(plants,DryHabitat(Kind.Swamp),Kind.Swamp,false); placed++; }
            for(int i=0;i<counts[3];i++) { PlaceStone(props,Kind.Swamp,DryHabitat(Kind.Swamp)); placed++; }
            for(int i=0;i<counts[4];i++) { Place(props,twig,Kind.Swamp,DryHabitat(Kind.Swamp),1,true); placed++; }
            for(int i=0;i<counts[5];i++) { Place(props,stump,Kind.Swamp,DryHabitat(Kind.Swamp),1); placed++; }
            MeshObject("Reed beds, bank grass and moss",SaveMesh(plants,"Swamp_Plants"),art.transform);
            MeshObject("Wet stones, fallen branches and old stumps",SaveMesh(props,"Swamp_Props"),art.transform);
            CreateWater(art,false);
            if(placed!=art.decorItems || Height(Kind.Swamp,-1.35f,.8f)>=0) throw new Exception("Wetland ponds or decor invalid");
            return SavePrefab(art.gameObject);
        }
        static GameObject CreateTundra(Mesh pebble,Mesh shrub)
        {
            var art=SpecialRoot("Tundra25",Kind.Tundra,"Тундра",150,250);
            var plants=new ForestMeshBuilder(); var props=new ForestMeshBuilder();
            var trees=new GameObject("Snow covered conifer forest").transform; trees.SetParent(art.transform,false); art.trees=trees;
            var positions=new List<Vector3>();
            int[] counts=Allocate(new[]{36,32,60,26},art.decorItems); int placed=0;
            for(int i=0;i<counts[0];i++)
            {
                Vector3 p=Sample(Kind.Tundra,.83f);
                for(int attempt=0;attempt<120;attempt++)
                {
                    bool crowded=false; foreach(var other in positions) if((p-other).sqrMagnitude<.47f*.47f) { crowded=true; break; }
                    if(!crowded) break; p=Sample(Kind.Tundra,.83f);
                }
                positions.Add(p); SnowTree(trees,p,i%3); placed++;
            }
            for(int i=0;i<counts[1];i++) { PlaceStone(props,Kind.Tundra,Sample(Kind.Tundra)); placed++; }
            for(int i=0;i<counts[2];i++) { Moss(plants,Sample(Kind.Tundra),Kind.Tundra,true); placed++; }
            for(int i=0;i<counts[3];i++) { DryTuft(plants,Sample(Kind.Tundra),Kind.Tundra,C("AAA990"),.10f); placed++; }
            MeshObject("Snow hummocks and grass through snow",SaveMesh(plants,"Tundra_Plants"),art.transform);
            MeshObject("Varied snow capped stones",SaveMesh(props,"Tundra_Props"),art.transform);
            if(placed!=art.decorItems) throw new Exception("Tundra decor invalid");
            return SavePrefab(art.gameObject);
        }
    }
}
