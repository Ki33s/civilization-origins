using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Origins;

namespace Origins.Editor.ForestArt
{
    public static partial class TerrainShowcaseBuilder
    {
        const string DecorRoot="Assets/Art/Decor/Generated";
        static List<DecorModelDefinition> editableModels;
        public static void BuildEditableDecorLibrary()
        {
            Directory.CreateDirectory(DecorRoot+"/Meshes"); Directory.CreateDirectory(DecorRoot+"/Prefabs"); AssetDatabase.Refresh();
            editableModels=new List<DecorModelDefinition>(); var previousRandom=random; random=new System.Random(73041); buildingDecorPrototypes=true;
            surface=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Forest/Generated/ForestSurface.mat"); surface.enableInstancing=true;
            try
            {
                foreach(string tree in new[]{"Oak","Birch","Pine"}) for(int i=1;i<=3;i++)
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Forest/Generated/Prefabs/"+tree+"_"+i+".prefab");
                    AddEditableModel(tree.ToLowerInvariant()+"_"+i,(tree=="Oak" ? "Дуб" : tree=="Birch" ? "Берёза" : "Сосна")+" "+i,"Деревья",true,prefab);
                }
                for(int i=0;i<3;i++) AddEditableMeshes("snow_"+i,new[]{"Заснеженная ель","Заснеженная сосна","Заснеженная пихта"}[i],"Деревья",true,new[]{Existing("SnowConifer_"+i+"_Wood"),Existing("SnowConifer_"+i+"_Needles"),Existing("SnowConifer_"+i+"_Snow")});
                var grass=new ForestMeshBuilder(); DryTuft(grass,Vector3.zero,TerrainArtKind.Meadow,C("678A45"),.24f); AddBuilder("grass","Пучок зелёной травы","Трава",grass);
                var dry=new ForestMeshBuilder(); DryTuft(dry,Vector3.zero,TerrainArtKind.Plains,C("A69A61"),.30f); AddBuilder("dry_grass","Пучок сухой травы","Трава",dry);
                for(int i=0;i<5;i++) { var flower=new ForestMeshBuilder(); FlowerModel(flower,Vector3.zero,i,false); AddBuilder("flower_"+i,new[]{"Белые цветы","Сиреневые цветы","Жёлтые цветы","Голубые цветы","Розовые цветы"}[i],"Цветы",flower); }
                foreach(string id in new[]{"Rounded","FlatShale","Elongated","Angular","Cluster","SplitBasalt","LayeredSlate","Granite"})
                {
                    string label=new Dictionary<string,string>{{"Rounded","Округлый"},{"FlatShale","Плоский"},{"Elongated","Вытянутый"},{"Angular","Угловатый"},{"Cluster","Группа"},{"SplitBasalt","Базальт"},{"LayeredSlate","Слоистый"},{"Granite","Гранит"}}[id];
                    AddEditableMeshes("stone_"+id,"Камень · "+label,"Камни",false,new[]{Existing("Stone_"+id)});
                    AddEditableMeshes("outcrop_"+id,"Скала · "+label,"Крупные камни",true,new[]{Existing("Outcrop_"+id)});
                }
                for(int i=0;i<3;i++) AddEditableMeshes("cactus_"+i,new[]{"Сагуаро","Бочковидный кактус","Опунция"}[i],"Кактусы",true,new[]{Existing("Cactus_"+new[]{"Saguaro","Barrel","PricklyPear"}[i])});
                foreach(var item in new[]{new[]{"clover","Клевер","CloverCluster"},new[]{"shrub","Лиственный куст","LowLeafyShrub"},new[]{"dry_shrub","Сухой куст","DryShrub"},new[]{"seed_heads","Дикие колоски","WildGrassSeedHeads"},new[]{"twig","Сухая ветка","DryGroundTwig"},new[]{"shell","Ракушка","BeachShell"}})
                    AddEditableMeshes(item[0],item[1],"Растения и детали",false,new[]{Existing(item[2])});
                foreach(var item in new[]{new[]{"fern","Папоротник","FernNineFronds"},new[]{"stump","Пень","StumpWithGrowthRings"},new[]{"fallen_log","Упавший ствол","MossyFallenLog"},new[]{"mushrooms","Грибы","HoneyMushroomCluster"}})
                    AddEditableMeshes(item[0],item[1],"Лесной декор",false,new[]{AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Forest/Generated/Meshes/"+item[2]+".asset")});
                var reeds=new ForestMeshBuilder(); Reeds(reeds,Vector3.zero); AddBuilder("reeds","Пучок камыша","Влажные места",reeds);
                var moss=new ForestMeshBuilder(); Moss(moss,Vector3.zero,TerrainArtKind.Swamp,false); AddBuilder("moss","Кочка мха","Влажные места",moss);
                var lichen=new ForestMeshBuilder(); Moss(lichen,Vector3.zero,TerrainArtKind.Tundra,true); AddBuilder("lichen","Снежная кочка","Снег",lichen);
                var catalog=AssetDatabase.LoadAssetAtPath<DecorModelCatalog>("Assets/Resources/DecorModelCatalog.asset");
                if(!catalog) { catalog=ScriptableObject.CreateInstance<DecorModelCatalog>(); AssetDatabase.CreateAsset(catalog,"Assets/Resources/DecorModelCatalog.asset"); }
                catalog.models=editableModels.ToArray(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
                if(!File.Exists("Assets/Resources/TerrainDecorProfile.json")) File.WriteAllText("Assets/Resources/TerrainDecorProfile.json",JsonUtility.ToJson(DefaultDecorProfile(),true));
                AssetDatabase.Refresh(); Debug.Log("DECOR: "+catalog.models.Length+" editable models exported; original showcase assets preserved");
            }
            finally { random=previousRandom; buildingDecorPrototypes=false; }
        }
        static Mesh Existing(string name)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Terrain/Generated/Meshes/"+name+".asset");
            if(!mesh) throw new Exception("Missing decor mesh "+name); return mesh;
        }
        static Mesh SaveDecorMesh(Mesh mesh,string key)
        {
            string path=DecorRoot+"/Meshes/"+key+".asset"; var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old) { EditorUtility.CopySerialized(mesh,old); UnityEngine.Object.DestroyImmediate(mesh); return old; }
            mesh.name=key; AssetDatabase.CreateAsset(mesh,path); return mesh;
        }
        static void AddBuilder(string id,string label,string category,ForestMeshBuilder builder)
        { AddEditableMeshes(id,label,category,false,new[]{SaveDecorMesh(builder.Finish(id),id)}); }
        static void AddEditableMeshes(string id,string label,string category,bool large,Mesh[] meshes)
        {
            var root=new GameObject(id);
            for(int i=0;i<meshes.Length;i++) { if(!meshes[i]) throw new Exception("Missing model mesh: "+id); var child=new GameObject("Part "+i); child.transform.SetParent(root.transform,false); child.AddComponent<MeshFilter>().sharedMesh=meshes[i]; child.AddComponent<MeshRenderer>().sharedMaterial=surface; }
            AddEditableModel(id,label,category,large,root); UnityEngine.Object.DestroyImmediate(root);
        }
        static void AddEditableModel(string id,string label,string category,bool large,GameObject source)
        {
            var root=UnityEngine.Object.Instantiate(source); root.name=id;
            var filters=root.GetComponentsInChildren<MeshFilter>(true); var definition=new DecorModelDefinition{id=id,label=label,category=category,large=large,medium=new Mesh[filters.Length],far=new Mesh[filters.Length]};
            var near=root.GetComponentsInChildren<Renderer>(true);
            var middle=new List<Renderer>(); var distant=new List<Renderer>();
            for(int i=0;i<filters.Length;i++)
            {
                definition.triangles+=filters[i].sharedMesh.triangles.Length/3;
                definition.medium[i]=SaveDecorMesh(WorldTerrainBuilder.Simplify(filters[i].sharedMesh,.085f,false,4.5f),id+"_Medium_"+i);
                definition.far[i]=SaveDecorMesh(WorldTerrainBuilder.Simplify(filters[i].sharedMesh,.27f,false,4.5f),id+"_Far_"+i);
                filters[i].GetComponent<Renderer>().sharedMaterial.enableInstancing=true;
                middle.Add(ModelLodPart(root.transform,filters[i],definition.medium[i],"Medium "+i,false));
                distant.Add(ModelLodPart(root.transform,filters[i],definition.far[i],"Far "+i,true));
            }
            var lod=root.GetComponent<LODGroup>(); if(!lod) lod=root.AddComponent<LODGroup>();
            lod.SetLODs(new[]{new LOD(.08f,near),new LOD(.03f,middle.ToArray()),new LOD(0,distant.ToArray())}); lod.RecalculateBounds();
            definition.prefab=PrefabUtility.SaveAsPrefabAsset(root,DecorRoot+"/Prefabs/"+id+".prefab"); UnityEngine.Object.DestroyImmediate(root); editableModels.Add(definition);
        }
        static Renderer ModelLodPart(Transform root,MeshFilter source,Mesh mesh,string name,bool far)
        {
            var part=new GameObject(name); part.transform.SetParent(root,false);
            part.transform.localPosition=root.InverseTransformPoint(source.transform.position);
            part.transform.localRotation=Quaternion.Inverse(root.rotation)*source.transform.rotation;
            part.transform.localScale=(root.worldToLocalMatrix*source.transform.localToWorldMatrix).lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=part.AddComponent<MeshRenderer>(); renderer.sharedMaterial=source.GetComponent<Renderer>().sharedMaterial;
            if(far) renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return renderer;
        }
        static TerrainDecorProfile DefaultDecorProfile()
        {
            Func<string,int,DecorQuantity> q=(id,count)=>new DecorQuantity(id,count);
            var profile=new TerrainDecorProfile{biomes=new[]{
                new BiomeDecorSettings{terrain="Ocean",models=new DecorQuantity[0]},
                new BiomeDecorSettings{terrain="Coast",models=new[]{q("dry_grass",4),q("stone_Rounded",2),q("shell",3),q("twig",1)}},
                new BiomeDecorSettings{terrain="Grass",models=new[]{q("grass",8),q("flower_0",2),q("flower_1",2),q("stone_Rounded",1),q("stone_FlatShale",1),q("stump",1)}},
                new BiomeDecorSettings{terrain="Forest",models=new[]{q("oak_1",1),q("birch_1",1),q("pine_1",1),q("grass",5),q("flower_0",2),q("fern",2),q("stone_Rounded",2),q("stump",1),q("fallen_log",1),q("mushrooms",1)}},
                new BiomeDecorSettings{terrain="Hills",models=new[]{q("grass",5),q("flower_2",2),q("outcrop_Angular",3),q("stone_Granite",2),q("shrub",1)}},
                new BiomeDecorSettings{terrain="Mountain",models=new[]{q("dry_grass",3),q("stone_Angular",3),q("outcrop_Granite",2)}},
                new BiomeDecorSettings{terrain="Desert",models=new[]{q("cactus_0",4),q("cactus_1",4),q("cactus_2",4),q("stone_Granite",2),q("dry_shrub",1),q("twig",1)}},
                new BiomeDecorSettings{terrain="Plains",models=new[]{q("dry_grass",8),q("seed_heads",3),q("stone_FlatShale",2),q("twig",1)}},
                new BiomeDecorSettings{terrain="Swamp",models=new[]{q("reeds",6),q("grass",3),q("moss",2),q("stone_Rounded",2),q("twig",1),q("stump",1)}},
                new BiomeDecorSettings{terrain="Tundra",models=new[]{q("snow_0",12),q("snow_1",12),q("snow_2",12),q("lichen",4),q("dry_grass",3),q("stone_Granite",2)}}
            }};
            var art=Resources.Load<TerrainArtCatalog>("TerrainArtCatalog");
            if(art && art.forest.GetComponent<TerrainTileArtwork>().trees)
            {
                var trees=profile.Get(Terrain.Forest).models; foreach(var item in trees) if(item.model=="oak_1" || item.model=="birch_1" || item.model=="pine_1") item.min=item.max=0;
                foreach(Transform tree in art.forest.GetComponent<TerrainTileArtwork>().trees)
                { string id=tree.name.StartsWith("Oak") ? "oak_1" : tree.name.StartsWith("Birch") ? "birch_1" : "pine_1"; foreach(var item in trees) if(item.model==id) { item.min++; item.max++; } }
            }
            return profile;
        }
    }
}
