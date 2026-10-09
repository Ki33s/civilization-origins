using System;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Origins
{
    public sealed partial class WorldView
    {
        readonly WorldTerrainCatalog terrainCatalog;
        readonly TerrainDecorRenderer editableDecor;
        readonly Dictionary<Hex,Quaternion> tileRotations=new Dictionary<Hex,Quaternion>();
        public int ArtworkTileCount { get; private set; }
        public Bounds WorldBounds
        {
            get
            {
                var b=new Bounds(FlatPosition(game.World.Start),Vector3.zero);
                foreach(var tile in game.World.Ordered) b.Encapsulate(FlatPosition(tile.Hex));
                b.Expand(new Vector3(2,3,2)); return b;
            }
        }
        static Vector3 FlatPosition(Hex h) { return new Vector3(Mathf.Sqrt(3)*(h.Q+h.R*.5f),0,h.R*1.5f); }
        Quaternion TileRotation(Tile tile)
        {
            int turn=unchecked((int)((uint)(game.World.Seed^tile.Hex.Q*73856093^tile.Hex.R*19349663)%6));
            if(tile.Terrain==Terrain.Coast)
            {
                Vector3 towardsWater=Vector3.zero;
                foreach(var n in game.World.Neighbours(tile.Hex)) if(n.Terrain==Terrain.Ocean) towardsWater+=FlatPosition(n.Hex)-FlatPosition(tile.Hex);
                if(towardsWater.sqrMagnitude>.01f)
                {
                    float best=-2;
                    for(int angle=0;angle<6;angle++) { float score=Vector3.Dot(Quaternion.Euler(0,angle*60,0)*Vector3.right,towardsWater.normalized); if(score>best) { best=score; turn=angle; } }
                }
            }
            return Quaternion.Euler(0,turn*60,0);
        }
        void CreateArtwork(Tile tile,Transform parent)
        {
            var entry=terrainCatalog.Get(tile.Terrain);
            var go=Object.Instantiate(entry.prefab,parent,false); go.name="Approved artwork · "+tile.Terrain;
            go.transform.localScale=Vector3.one/terrainCatalog.sourceRadius; go.transform.localRotation=tileRotations[tile.Hex];
            var collider=go.AddComponent<MeshCollider>(); collider.sharedMesh=entry.collision; go.AddComponent<TileView>().Tile=tile;
            int variant=unchecked(game.World.Seed^tile.Hex.Q*911^tile.Hex.R*3571);
            editableDecor.Create(tile.Terrain,go.transform,variant);
            ArtworkTileCount++;
        }
        public Vector3 OnSurface(Vector3 point)
        {
            if(!terrainCatalog) return point;
            float q=point.x/Mathf.Sqrt(3)-point.z/3,r=point.z/1.5f,s=-q-r;
            int iq=Mathf.RoundToInt(q),ir=Mathf.RoundToInt(r),isum=Mathf.RoundToInt(s);
            float dq=Mathf.Abs(iq-q),dr=Mathf.Abs(ir-r),ds=Mathf.Abs(isum-s);
            if(dq>dr && dq>ds) iq=-ir-isum; else if(dr>ds) ir=-iq-isum;
            var h=new Hex(iq,ir); var tile=game.World.Get(h); if(tile==null) return point;
            var local=Quaternion.Inverse(tileRotations[h])*(point-FlatPosition(h))*terrainCatalog.sourceRadius;
            float height=terrainCatalog.Get(tile.Terrain).Sample(local.x,local.z,terrainCatalog.sourceRadius)/terrainCatalog.sourceRadius;
            // Coast workers stand on shore, wetland actors remain above the shallow water surface.
            if(tile.Terrain==Terrain.Swamp) height=Mathf.Max(height,.025f/terrainCatalog.sourceRadius);
            point.y=height; return point;
        }
        Vector3 LocalSurface(Transform parent,Vector3 local)
        {
            if(!terrainCatalog) return local;
            return parent.InverseTransformPoint(OnSurface(parent.TransformPoint(local)));
        }
    }
}
