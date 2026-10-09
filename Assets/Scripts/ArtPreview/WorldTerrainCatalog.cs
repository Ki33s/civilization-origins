using System;
using UnityEngine;

namespace Origins
{
    [Serializable]
    public sealed class WorldTerrainEntry
    {
        public Terrain terrain;
        public GameObject prefab;
        public Mesh collision;
        public int decorItems, nearTriangles, mediumTriangles, farTriangles;
        public int heightResolution=49;
        public float[] heights;
        public float Sample(float x,float z,float radius)
        {
            float u=Mathf.Clamp((x/radius+1)*.5f*(heightResolution-1),0,heightResolution-1.001f);
            float v=Mathf.Clamp((z/radius+1)*.5f*(heightResolution-1),0,heightResolution-1.001f);
            int ix=(int)u,iz=(int)v; float fx=u-ix,fz=v-iz;
            return Mathf.Lerp(Mathf.Lerp(heights[iz*heightResolution+ix],heights[iz*heightResolution+ix+1],fx),
                Mathf.Lerp(heights[(iz+1)*heightResolution+ix],heights[(iz+1)*heightResolution+ix+1],fx),fz);
        }
    }
    [CreateAssetMenu(menuName="Истоки/Графика полного мира")]
    public sealed class WorldTerrainCatalog : ScriptableObject
    {
        public float sourceRadius=4.5f;
        public WorldTerrainEntry[] entries;
        public WorldTerrainEntry Get(Terrain terrain)
        {
            foreach(var entry in entries) if(entry.terrain==terrain) return entry;
            throw new InvalidOperationException("Missing world artwork: "+terrain);
        }
    }
}
