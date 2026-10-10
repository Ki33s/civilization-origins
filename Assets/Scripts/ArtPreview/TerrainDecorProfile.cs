using System;
using System.IO;
using UnityEngine;

namespace Origins
{
    [Serializable] public sealed class DecorQuantity
    {
        public string model;
        public int min,max;
        public DecorQuantity(string id,int count) { model=id; min=max=count; }
    }
    [Serializable] public sealed class BiomeDecorSettings
    {
        public string terrain;
        public DecorQuantity[] models;
    }
    [Serializable] public sealed class TerrainDecorProfile
    {
        public string version="decor-1";
        public int seed=1090038158;
        public float minScale=.5f,maxScale=1.5f;
        public BiomeDecorSettings[] biomes;
        public BiomeDecorSettings Get(Terrain terrain)
        {
            foreach(var biome in biomes) if(biome.terrain==terrain.ToString()) return biome;
            throw new InvalidOperationException("Missing decor settings: "+terrain);
        }
        public void Validate(DecorModelCatalog catalog)
        {
            if(version!="decor-1" || biomes==null || float.IsNaN(minScale) || float.IsNaN(maxScale) || float.IsInfinity(minScale) || float.IsInfinity(maxScale) || minScale<=0 || maxScale<minScale || maxScale>3) throw new InvalidOperationException("Некорректный профиль декора");
            foreach(Terrain type in Enum.GetValues(typeof(Terrain)))
            {
                var biome=Get(type); var ids=new System.Collections.Generic.HashSet<string>();
                if(biome.models==null) throw new InvalidOperationException("Нет списка моделей: "+biome.terrain);
                foreach(var item in biome.models)
                    if(catalog.Get(item.model)==null || !ids.Add(item.model) || item.min<0 || item.max<item.min || item.max>250) throw new InvalidOperationException("Некорректное количество модели в "+biome.terrain);
            }
        }
        public static string Name(Terrain type)
        {
            switch(type) { case Terrain.Ocean:return "Океан"; case Terrain.Coast:return "Побережье"; case Terrain.Grass:return "Луг"; case Terrain.Forest:return "Лес"; case Terrain.Hills:return "Холмы"; case Terrain.Mountain:return "Горы"; case Terrain.Desert:return "Пустыня"; case Terrain.Plains:return "Равнина"; case Terrain.Swamp:return "Болото"; default:return "Снежная тундра"; }
        }
    }
    public static class DecorProfileStore
    {
        static TerrainDecorProfile active;
        public static TerrainDecorProfile Active { get { if(active==null) Reload(); return active; } set { active=value; } }
        public static string SavePath
        {
            get
            {
                if(Application.isEditor) return Path.Combine(Application.dataPath,"Resources/TerrainDecorProfile.json");
                if(Application.platform==RuntimePlatform.WebGLPlayer) return Path.Combine(Application.persistentDataPath,"TerrainDecorProfile.json");
                string project=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Assets/Resources/TerrainDecorProfile.json"));
                return File.Exists(project) ? project : Path.GetFullPath(Path.Combine(Application.dataPath,"../TerrainDecorProfile.json"));
            }
        }
        public static void Reload()
        {
            string json=File.Exists(SavePath) ? File.ReadAllText(SavePath) : Resources.Load<TextAsset>("TerrainDecorProfile").text;
            active=JsonUtility.FromJson<TerrainDecorProfile>(json); active.Validate(Resources.Load<DecorModelCatalog>("DecorModelCatalog"));
        }
        public static void Save(TerrainDecorProfile profile,string path=null)
        {
            profile.Validate(Resources.Load<DecorModelCatalog>("DecorModelCatalog")); File.WriteAllText(path ?? SavePath,JsonUtility.ToJson(profile,true)); active=profile;
        }
        public static TerrainDecorProfile Copy(TerrainDecorProfile profile) { return JsonUtility.FromJson<TerrainDecorProfile>(JsonUtility.ToJson(profile)); }
    }
}
