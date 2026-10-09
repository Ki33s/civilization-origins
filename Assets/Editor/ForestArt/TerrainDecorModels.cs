using System;
using UnityEngine;
using UnityEditor;
using Kind=Origins.TerrainArtKind;

namespace Origins.Editor.ForestArt
{
    public static partial class TerrainShowcaseBuilder
    {
        static Mesh[] stones, outcrops, cacti;
        static Mesh[] coniferWood, coniferLeaves, coniferSnow;
        static int variedModels;
        static float minScale,maxScale,minYaw,maxYaw;
        static bool buildingDecorPrototypes;
        static Origins.NaturalModelVariation Variation()
        {
            var v=Origins.NaturalModelVariation.Sample(random);
            if(v.scale<.5f || v.scale>1.5f || v.yaw<0 || v.yaw>=360) throw new Exception("Decor transform out of range");
            variedModels++; minScale=Mathf.Min(minScale,v.scale); maxScale=Mathf.Max(maxScale,v.scale);
            minYaw=Mathf.Min(minYaw,v.yaw); maxYaw=Mathf.Max(maxYaw,v.yaw); return v;
        }
        static void AppendModel(ForestMeshBuilder target,ForestMeshBuilder model,Vector3 root,Kind kind,bool tilt=false)
        {
            var matrix=buildingDecorPrototypes ? Matrix4x4.identity : Variation().Matrix(root,tilt ? Quaternion.FromToRotation(Vector3.up,Normal(kind,root)) : Quaternion.identity);
            target.AddRange(model,0,model.VertexCount,0,model.IndexCount,matrix);
        }
        static void PlaceStone(ForestMeshBuilder target,Kind kind,Vector3 root,bool large=false)
        {
            var library=large ? outcrops : stones; Mesh stone=library[random.Next(library.Length)];
            if(kind!=Kind.Tundra) { Place(target,stone,kind,root,1,true); return; }
            var model=new ForestMeshBuilder(); model.AddMesh(stone,Matrix4x4.identity); Bounds bounds=stone.bounds;
            model.Blob(new Vector3(bounds.center.x,bounds.max.y-.035f,bounds.center.z),new Vector3(bounds.extents.x*.7f,.045f,bounds.extents.z*.7f),C("E5EBEF"),17,24,12,.07f);
            AppendModel(target,model,root,kind,true);
        }
        static void BuildDecorLibrary()
        {
            stones=new Mesh[8]; outcrops=new Mesh[8];
            for(int i=0;i<8;i++) { stones[i]=StoneVariant(i,false); outcrops[i]=StoneVariant(i,true); }
            cacti=new Mesh[3]; for(int i=0;i<3;i++) cacti[i]=Cactus(i);
            coniferWood=new Mesh[3]; coniferLeaves=new Mesh[3]; coniferSnow=new Mesh[3];
            for(int i=0;i<3;i++) SnowConifer(i,out coniferWood[i],out coniferLeaves[i],out coniferSnow[i]);
            const string path="Assets/Resources/NaturalDecorCatalog.asset";
            var catalog=AssetDatabase.LoadAssetAtPath<Origins.NaturalDecorCatalog>(path);
            if(!catalog) { catalog=ScriptableObject.CreateInstance<Origins.NaturalDecorCatalog>(); AssetDatabase.CreateAsset(catalog,path); }
            catalog.stones=stones; catalog.cacti=cacti; catalog.surface=surface; EditorUtility.SetDirty(catalog);
        }
        static Mesh StoneVariant(int variant,bool large)
        {
            var b=new ForestMeshBuilder(); float size=large ? 1.7f : 1;
            Color tint=C(new[]{"8A908A","7A8589","9B9283","69757B","92978C","686E69","A49D8F","7B8675"}[variant]);
            Vector3[] shapes={new Vector3(.24f,.15f,.20f),new Vector3(.32f,.07f,.22f),new Vector3(.38f,.13f,.12f),new Vector3(.21f,.28f,.18f),new Vector3(.24f,.15f,.19f),new Vector3(.29f,.23f,.19f),new Vector3(.26f,.10f,.30f),new Vector3(.34f,.22f,.26f)};
            Vector3 shape=shapes[variant]*size;
            if(variant==3 || variant==5 || variant==7)
            {
                // Uneven polygonal faces give the fragments a different silhouette from pebbles.
                const int sides=7,rings=8;
                for(int ring=0;ring<rings;ring++) for(int side=0;side<sides;side++)
                {
                    Func<int,int,Vector3> point=(r,s)=>{
                        float y=r/(float)rings,a=s*Mathf.PI*2/sides;
                        float width=y<.65f ? 1-.20f*y : Mathf.Lerp(.87f,.16f,(y-.65f)/.35f);
                        float irregular=1+.20f*Mathf.Sin(s*2.7f+variant)+.045f*Mathf.Sin(r*1.9f+s);
                        return new Vector3(Mathf.Cos(a)*shape.x*width*irregular+shape.x*y*.24f,y*shape.y*1.8f,Mathf.Sin(a)*shape.z*width*irregular);
                    };
                    b.Quad(point(ring,side),point(ring+1,side),point(ring+1,side+1),point(ring,side+1),tint);
                    if(ring==rings-1) b.Triangle(point(rings,side),new Vector3(shape.x*.24f,shape.y*1.8f,0),point(rings,side+1),tint,tint,tint);
                }
            }
            else
            {
                b.Blob(Vector3.up*shape.y*.65f,shape,tint,71+variant*23,32,20,variant==1 ? .045f : .13f);
                if(variant==4)
                {
                    b.Blob(new Vector3(-.22f,.055f,.08f)*size,new Vector3(.13f,.095f,.12f)*size,tint*.92f,92,24,16,.09f);
                    b.Blob(new Vector3(.16f,.04f,-.16f)*size,new Vector3(.11f,.07f,.1f)*size,tint*1.07f,48,24,16,.10f);
                }
                if(variant==6) b.Blob(new Vector3(.03f,.13f,-.02f)*size,new Vector3(.19f,.024f,.22f)*size,tint*.84f,37,28,12,.10f);
            }
            return SaveMesh(b,(large ? "Outcrop_" : "Stone_")+new[]{"Rounded","FlatShale","Elongated","Angular","Cluster","SplitBasalt","LayeredSlate","Granite"}[variant],variant!=3 && variant!=5 && variant!=7);
        }
        static Mesh Cactus(int variant)
        {
            var b=new ForestMeshBuilder(); Color green=C(variant==0 ? "4E784B" : variant==1 ? "688550" : "567A49");
            if(variant==0)
            {
                b.Tube(Vector3.zero,Vector3.up*1.15f,.13f,.10f,green,28,16,.13f);
                b.Blob(Vector3.up*1.15f,new Vector3(.10f,.11f,.10f),green,1,28,16,.015f);
                for(int arm=0;arm<2;arm++)
                {
                    float side=arm==0 ? -1 : 1,baseY=arm==0 ? .47f : .65f,topY=arm==0 ? .89f : 1.03f;
                    Vector3 bend=new Vector3(side*.29f,baseY+.04f,.04f),top=new Vector3(bend.x,topY,bend.z);
                    b.Tube(Vector3.up*baseY,bend,.075f,.075f,green,24,7,.09f); b.Tube(bend,top,.075f,.056f,green,24,9,.10f);
                    b.Blob(top,new Vector3(.057f,.065f,.057f),green,2+arm,24,12,.02f);
                }
            }
            else if(variant==1)
            {
                b.Blob(Vector3.up*.25f,new Vector3(.23f,.28f,.23f),green,9,36,24,.065f);
                for(int rib=0;rib<12;rib++) for(int n=0;n<4;n++)
                {
                    float angle=rib*6.28f/12,y=.09f+n*.095f; Vector3 p=Radial(angle,.21f,y);
                    b.Tube(p,p+Radial(angle,.027f,.008f),.002f,0,C("D4C9A1"),5,1,0);
                }
                b.Blob(Vector3.up*.52f,new Vector3(.05f,.022f,.05f),C("D2A365"),3,16,8,.05f);
            }
            else
            {
                for(int pad=0;pad<7;pad++)
                {
                    Vector3 p=new Vector3(pad==0 ? 0 : (pad%2==0 ? -.16f : .15f)*(1+pad/4f),.14f+(pad/2)*.18f,(pad%3-1)*.04f);
                    b.Blob(p,new Vector3(.12f,.18f,.043f),green,40+pad,28,18,.025f);
                    for(int n=0;n<10;n++)
                    {
                        Vector3 spine=p+new Vector3(Mathf.Sin(n*2.4f)*.085f,Mathf.Cos(n*2.4f)*.12f,.043f);
                        b.Tube(spine,spine+new Vector3(.007f,.005f,.018f),.002f,0,C("C9C29B"),5,1,0);
                    }
                }
            }
            return SaveMesh(b,"Cactus_"+new[]{"Saguaro","Barrel","PricklyPear"}[variant]);
        }
        static void SnowConifer(int variant,out Mesh wood,out Mesh leaves,out Mesh snow)
        {
            var trunk=new ForestMeshBuilder(); var foliage=new ForestMeshBuilder(); var caps=new ForestMeshBuilder();
            float height=variant==0 ? 2.4f : variant==1 ? 2.85f : 2.15f,width=variant==0 ? .73f : variant==1 ? .65f : .82f;
            trunk.Tube(Vector3.zero,new Vector3(.035f,height,0),.075f,.012f,C("685B49"),20,14,.08f);
            for(int tier=0;tier<7;tier++)
            {
                float y=.36f+tier*height*.115f,reach=width*(1-tier/8f);
                for(int branch=0;branch<7;branch++)
                {
                    float angle=branch*6.28f/7+tier*.72f+variant*.4f;
                    Vector3 start=Vector3.up*(y+.12f),tip=Radial(angle,reach,y-.08f);
                    trunk.Tube(start,tip,.020f,.003f,C("685B49"),10,5,0);
                    foliage.Tube(start,tip,reach*.18f,.009f,C(variant==1 ? "355C45" : "2C5444"),16,5,.12f);
                    for(int needle=0;needle<12;needle++)
                    {
                        Vector3 root=Vector3.Lerp(start,tip,needle/13f);
                        foliage.Blade(root,angle+(needle%2==0 ? .65f : -.65f),.08f,.028f,reach*.26f,C("41634D"),4);
                    }
                    Vector3 mid=Vector3.Lerp(start,tip,.52f)+Vector3.up*(reach*.10f);
                    var patch=new ForestMeshBuilder();
                    patch.Blob(Vector3.zero,new Vector3(reach*.13f,.025f,reach*.34f),C("DFE8ED"),tier*7+branch+variant*71,20,12,.09f);
                    caps.AddRange(patch,0,patch.VertexCount,0,patch.IndexCount,Matrix4x4.TRS(mid,Quaternion.Euler(0,90-angle*Mathf.Rad2Deg,0),Vector3.one));
                }
            }
            foliage.Tube(Vector3.up*(height*.76f),Vector3.up*(height+.13f),.14f,.001f,C("315945"),20,7,.10f);
            caps.Tube(Vector3.up*(height+.07f),Vector3.up*(height+.135f),.033f,.001f,C("E8EDF1"),20,6,.02f);
            wood=SaveMesh(trunk,"SnowConifer_"+variant+"_Wood"); leaves=SaveMesh(foliage,"SnowConifer_"+variant+"_Needles"); snow=SaveMesh(caps,"SnowConifer_"+variant+"_Snow");
        }
        static void SnowTree(Transform parent,Vector3 root,int variant)
        {
            var go=new GameObject(new[]{"Snow spruce","Snow pine","Snow fir"}[variant]); go.transform.SetParent(parent,false); go.transform.localPosition=root;
            Variation().Apply(go.transform);
            MeshObject("Trunk and branches",coniferWood[variant],go.transform); MeshObject("Evergreen needles",coniferLeaves[variant],go.transform); MeshObject("Snow on boughs",coniferSnow[variant],go.transform);
        }
    }
}
