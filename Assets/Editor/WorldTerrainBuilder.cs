using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace Origins.Editor
{
    public static class WorldTerrainBuilder
    {
        const string Root="Assets/Art/World/Generated";
        struct Key : IEquatable<Key>
        {
            public int x,y,z;
            public bool Equals(Key b) { return x==b.x && y==b.y && z==b.z; }
            public override bool Equals(object o) { return o is Key k && Equals(k); }
            public override int GetHashCode() { return unchecked(x*73856093^y*19349663^z*83492791); }
        }
        sealed class Cluster
        {
            public Vector3 position,normal; public Color color; public Vector2 uv; public int count;
        }
        [MenuItem("Истоки/Графика/Подготовить клетки для полного мира")]
        public static void Generate()
        {
            Directory.CreateDirectory(Root+"/Meshes"); Directory.CreateDirectory(Root+"/Prefabs"); AssetDatabase.Refresh();
            var source=Resources.Load<TerrainArtCatalog>("TerrainArtCatalog");
            if(!source) throw new Exception("Generate the approved terrain library first");
            ForestArt.TerrainShowcaseBuilder.BuildEditableDecorLibrary();
            var originals=new[]{source.ocean,source.coast,source.meadow,source.forest,source.hills,source.mountain,source.desert,source.plains,source.swamp,source.tundra};
            var terrains=new[]{Terrain.Ocean,Terrain.Coast,Terrain.Grass,Terrain.Forest,Terrain.Hills,Terrain.Mountain,Terrain.Desert,Terrain.Plains,Terrain.Swamp,Terrain.Tundra};
            var catalog=AssetDatabase.LoadAssetAtPath<WorldTerrainCatalog>("Assets/Resources/WorldTerrainCatalog.asset");
            if(!catalog) { catalog=ScriptableObject.CreateInstance<WorldTerrainCatalog>(); AssetDatabase.CreateAsset(catalog,"Assets/Resources/WorldTerrainCatalog.asset"); }
            catalog.sourceRadius=source.hexRadius; catalog.entries=new WorldTerrainEntry[terrains.Length];
            var stats=new System.Text.StringBuilder("Ground/water only; native / medium / far triangles. Individual scattered model prefabs are counted separately by TerrainDecorProfile.json.\n");
            for(int i=0;i<terrains.Length;i++)
            {
                var entry=new WorldTerrainEntry{terrain=terrains[i]}; catalog.entries[i]=entry;
                var model=new GameObject("World_"+terrains[i]);
                var near=UnityEngine.Object.Instantiate(originals[i],model.transform,false); near.name="Original detailed artwork";
                var art=near.GetComponent<TerrainTileArtwork>();
                var remove=new List<GameObject>();
                foreach(Transform child in near.transform)
                    if(child.GetSiblingIndex()!=0 && !child.name.StartsWith("Water edge") && (child.GetComponent<MeshRenderer>()==null || child.GetComponent<MeshRenderer>().sharedMaterial.shader.name!="Origins/Simple Waves")) remove.Add(child.gameObject);
                foreach(var child in remove) UnityEngine.Object.DestroyImmediate(child);
                art.trees=null; art.decorItems=art.majorFeatures; art.minDecor=0; art.maxDecor=art.majorFeatures;
                entry.decorItems=art.majorFeatures; entry.nearTriangles=0;
                foreach(var filter in near.GetComponentsInChildren<MeshFilter>()) entry.nearTriangles+=filter.sharedMesh.triangles.Length/3;
                art.triangles=entry.nearTriangles;
                var ground=near.transform.GetChild(0).GetComponent<MeshFilter>();
                if(!ground) throw new Exception("Terrain surface must be the first artwork child: "+terrains[i]);
                entry.heights=BakeHeights(ground.sharedMesh,ground.transform.localToWorldMatrix,source.hexRadius,entry.heightResolution);
                entry.collision=SaveMesh(Simplify(ground.sharedMesh,.16f,true,source.hexRadius),terrains[i]+"_Collider");
                Renderer[] nearRenderers=near.GetComponentsInChildren<Renderer>();
                foreach(var renderer in nearRenderers) if(renderer.sharedMaterial.shader.name=="Origins/Simple Waves") renderer.shadowCastingMode=ShadowCastingMode.Off;
                var medium=MakeLOD(near,model.transform,terrains[i]+"_Medium",.085f,source.hexRadius,out entry.mediumTriangles);
                var far=MakeLOD(near,model.transform,terrains[i]+"_Far",.27f,source.hexRadius,out entry.farTriangles);
                foreach(var renderer in far) renderer.shadowCastingMode=ShadowCastingMode.Off;
                var lod=model.AddComponent<LODGroup>(); lod.SetLODs(new[]{new LOD(.14f,nearRenderers),new LOD(.065f,medium),new LOD(0,far)});
                lod.localReferencePoint=new Vector3(0,source.hexRadius*.25f,0); lod.size=source.hexRadius*2; lod.fadeMode=LODFadeMode.None;
                entry.prefab=PrefabUtility.SaveAsPrefabAsset(model,Root+"/Prefabs/"+model.name+".prefab");
                UnityEngine.Object.DestroyImmediate(model);
                stats.AppendLine(terrains[i]+": "+entry.decorItems+" fixed landforms; triangles "+entry.nearTriangles+" / "+entry.mediumTriangles+" / "+entry.farTriangles);
            }
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); File.WriteAllText("Docs/world-art-statistics.txt",stats.ToString());
            Debug.Log("WORLD ART: 10 approved biomes prepared with shared meshes and three LODs");
        }
        static Renderer[] MakeLOD(GameObject original,Transform parent,string key,float cell,float radius,out int triangles)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>(); var temporary=new List<Mesh>();
            foreach(var filter in original.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>(); if(!renderer || renderer.sharedMaterials.Length!=1) throw new Exception("Unexpected artwork material layout");
                var material=renderer.sharedMaterial; material.enableInstancing=true; EditorUtility.SetDirty(material);
                var reduced=Simplify(filter.sharedMesh,cell,filter.transform==original.transform.GetChild(0),radius); temporary.Add(reduced);
                if(!groups.ContainsKey(material)) groups.Add(material,new List<CombineInstance>());
                groups[material].Add(new CombineInstance{mesh=reduced,transform=original.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix});
            }
            var root=new GameObject(key); root.transform.SetParent(parent,false); var result=new List<Renderer>(); triangles=0; int index=0;
            foreach(var group in groups)
            {
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32}; mesh.CombineMeshes(group.Value.ToArray(),true,true); triangles+=mesh.triangles.Length/3;
                var go=new GameObject("Shared artwork mesh "+index); go.transform.SetParent(root.transform,false);
                go.AddComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,key+"_"+index++);
                var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=group.Key;
                if(group.Key.shader.name=="Origins/Simple Waves") renderer.shadowCastingMode=ShadowCastingMode.Off;
                result.Add(renderer);
            }
            foreach(var mesh in temporary) UnityEngine.Object.DestroyImmediate(mesh);
            return result.ToArray();
        }
        // Cluster vertices, then remap complete triangles. No random triangle removal or holes.
        public static Mesh Simplify(Mesh source,float cell,bool ground,float radius)
        {
            var vertices=source.vertices; var normals=source.normals; var colors=source.colors; var uv=source.uv;
            var lookup=new Dictionary<Key,int>(); var clusters=new List<Cluster>(); var map=new int[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 p=vertices[i]; bool boundary=ground && (Mathf.Abs(p.x)>radius*.8660254f-.01f || Mathf.Abs(p.x)*.5f+Mathf.Abs(p.z)*.8660254f>radius*.8660254f-.01f);
                float step=boundary ? .00001f : cell;
                var key=new Key{x=Mathf.RoundToInt(p.x/step),y=Mathf.RoundToInt(p.y/step),z=Mathf.RoundToInt(p.z/step)};
                // Keep exact boundary vertices in a separate key range, preserving hex seams.
                if(boundary) key.y+=100000000;
                int id; if(!lookup.TryGetValue(key,out id)) { id=clusters.Count; lookup.Add(key,id); clusters.Add(new Cluster()); }
                var c=clusters[id]; c.position+=p; c.normal+=normals.Length>i ? normals[i] : Vector3.up; c.color+=colors.Length>i ? colors[i] : Color.white;
                if(uv.Length>i) c.uv+=uv[i]; c.count++; map[i]=id;
            }
            var outputVertices=new Vector3[clusters.Count]; var outputNormals=new Vector3[clusters.Count]; var outputColors=new Color[clusters.Count]; var outputUV=new Vector2[clusters.Count];
            for(int i=0;i<clusters.Count;i++) { var c=clusters[i]; outputVertices[i]=c.position/c.count; outputNormals[i]=c.normal.normalized; outputColors[i]=c.color/c.count; outputUV[i]=c.uv/c.count; }
            var indices=source.triangles; var triangles=new List<int>(); var seen=new HashSet<Key>();
            for(int i=0;i<indices.Length;i+=3)
            {
                int a=map[indices[i]],b=map[indices[i+1]],c=map[indices[i+2]]; if(a==b || b==c || a==c) continue;
                // Cyclic duplicates are identical faces, while opposite faces retain their winding.
                if(b<a && b<c) { int swap=a; a=b; b=c; c=swap; } else if(c<a && c<b) { int swap=a; a=c; c=b; b=swap; }
                if(!seen.Add(new Key{x=a,y=b,z=c})) continue; triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32}; mesh.vertices=outputVertices; mesh.normals=outputNormals; mesh.colors=outputColors; mesh.uv=outputUV; mesh.SetTriangles(triangles,0); mesh.RecalculateBounds(); return mesh;
        }
        static float[] BakeHeights(Mesh mesh,Matrix4x4 transform,float radius,int resolution)
        {
            var values=new float[resolution*resolution]; for(int i=0;i<values.Length;i++) values[i]=float.NegativeInfinity;
            var points=mesh.vertices; for(int i=0;i<points.Length;i++) points[i]=transform.MultiplyPoint3x4(points[i]); var indices=mesh.triangles;
            float step=radius*2/(resolution-1);
            for(int i=0;i<indices.Length;i+=3)
            {
                Vector3 a=points[indices[i]],b=points[indices[i+1]],c=points[indices[i+2]];
                float denominator=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z); if(Mathf.Abs(denominator)<.000001f) continue;
                int minX=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x,Mathf.Min(b.x,c.x))+radius)/step),0,resolution-1),maxX=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x,Mathf.Max(b.x,c.x))+radius)/step),0,resolution-1);
                int minZ=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z,Mathf.Min(b.z,c.z))+radius)/step),0,resolution-1),maxZ=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.z,Mathf.Max(b.z,c.z))+radius)/step),0,resolution-1);
                for(int z=minZ;z<=maxZ;z++) for(int x=minX;x<=maxX;x++)
                {
                    float px=x*step-radius,pz=z*step-radius;
                    float u=((b.z-c.z)*(px-c.x)+(c.x-b.x)*(pz-c.z))/denominator,v=((c.z-a.z)*(px-c.x)+(a.x-c.x)*(pz-c.z))/denominator,w=1-u-v;
                    if(u>=-.0001f && v>=-.0001f && w>=-.0001f) values[z*resolution+x]=Mathf.Max(values[z*resolution+x],u*a.y+v*b.y+w*c.y);
                }
            }
            for(int i=0;i<values.Length;i++) if(float.IsNegativeInfinity(values[i])) values[i]=0;
            return values;
        }
        static Mesh SaveMesh(Mesh mesh,string key)
        {
            string path=Root+"/Meshes/"+key+".asset"; var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing) { EditorUtility.CopySerialized(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); return existing; }
            mesh.name=key; AssetDatabase.CreateAsset(mesh,path); return mesh;
        }
        [MenuItem("Истоки/Графика/Собрать полный мир и галерею")]
        public static void BuildWorldAndGallery() { Generate(); DevelopmentGalleryBuilder.BuildGameplayAndGallery(); }
    }
}
