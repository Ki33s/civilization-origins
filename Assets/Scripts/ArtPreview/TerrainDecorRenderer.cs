using System;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Origins
{
    // Each tile has its own deterministic scatter. Each object is an independent prefab instance.
    public sealed class TerrainDecorRenderer
    {
        readonly DecorModelCatalog models;
        readonly TerrainDecorProfile profile;
        readonly WorldTerrainCatalog terrain;
        public TerrainDecorRenderer(Transform parent)
        {
            models=Resources.Load<DecorModelCatalog>("DecorModelCatalog"); profile=DecorProfileStore.Copy(DecorProfileStore.Active);
            profile.Validate(models); terrain=Resources.Load<WorldTerrainCatalog>("WorldTerrainCatalog");
        }
        public DecorTileInventory Create(Terrain type,Transform parent,int variant)
        {
            var entry=terrain.Get(type); var settings=profile.Get(type);
            var random=new System.Random(unchecked(profile.seed+(int)type*73856093+variant*19349663));
            var root=new GameObject("Editable decor · "+type); root.transform.SetParent(parent,false);
            var near=new GameObject("Individual models").transform; near.SetParent(root.transform,false);
            var inventory=root.AddComponent<DecorTileInventory>(); inventory.biome=type.ToString(); inventory.counts=new DecorQuantity[settings.models.Length];
            var trees=new List<Vector3>();
            for(int i=0;i<settings.models.Length;i++)
            {
                var quantity=settings.models[i]; var model=models.Get(quantity.model); int count=random.Next(quantity.min,quantity.max+1);
                inventory.counts[i]=new DecorQuantity(model.id,count); inventory.total+=count; if(model.large) inventory.large+=count; else inventory.small+=count;
                for(int n=0;n<count;n++)
                {
                    Vector3 p=Sample(random,entry,model.large,trees);
                    float yaw=(float)random.NextDouble()*360,scale=Mathf.Lerp(profile.minScale,profile.maxScale,(float)random.NextDouble());
                    var item=Object.Instantiate(model.prefab,near,false); item.name=model.id; item.transform.localPosition=p; item.transform.localRotation=Quaternion.Euler(0,yaw,0); item.transform.localScale=Vector3.one*scale;
                }
            }
            return inventory;
        }
        Vector3 Sample(System.Random random,WorldTerrainEntry entry,bool large,List<Vector3> occupied)
        {
            Vector3 p=Vector3.zero;
            for(int attempt=0;attempt<140;attempt++)
            {
                int wedge=random.Next(6); float u=(float)random.NextDouble(),v=(float)random.NextDouble(); if(u+v>1) { u=1-u; v=1-v; }
                float a=(30+wedge*60)*Mathf.Deg2Rad,b=a+Mathf.PI/3,r=terrain.sourceRadius*(large ? .78f : .9f);
                p=new Vector3((Mathf.Cos(a)*u+Mathf.Cos(b)*v)*r,0,(Mathf.Sin(a)*u+Mathf.Sin(b)*v)*r); p.y=entry.Sample(p.x,p.z,terrain.sourceRadius);
                if(entry.terrain==Terrain.Coast && p.y<.005f || entry.terrain==Terrain.Swamp && p.y<.045f) continue;
                bool crowded=false; if(large) foreach(var other in occupied) if((p-other).sqrMagnitude<.45f*.45f) { crowded=true; break; }
                if(!crowded) break;
            }
            if(large) occupied.Add(p); return p;
        }
        // All meshes belong to shared prefab assets; the owning scene destroys the instances.
        public void Dispose() { }
    }
}
