using UnityEngine;

namespace Origins
{
    // Read-only art preview: no turn loop, production, stock changes or player input.
    public sealed class WorldArtPreview : MonoBehaviour
    {
        public int seed=12026;
        WorldView world;
        Game model;
        public bool IsBuilt { get { return world!=null; } }
        float elapsed;
        int frames;
        int fps;
        public int TileCount { get { Ensure(); return world.ArtworkTileCount; } }
        public string Statistics
        {
            get
            {
                Ensure(); var text=new System.Text.StringBuilder("Seed "+seed+"; artwork tiles "+TileCount+"\n");
                foreach(Terrain type in System.Enum.GetValues(typeof(Terrain))) { int count=0; foreach(var tile in model.World.Ordered) if(tile.Terrain==type) count++; text.AppendLine(type+": "+count); }
                return text.ToString();
            }
        }
        public Vector3 Center { get { Ensure(); return world.WorldBounds.center; } }
        public string Summary { get { Ensure(); return "Мир "+seed+" · "+model.World.Ordered.Count+" клеток · вся карта без тумана · "+fps+" FPS"; } }
        public void Ensure()
        {
            if(world!=null) return;
            model=new Game(seed,JsonUtility.FromJson<DemoBalance>(Resources.Load<TextAsset>("DemoBalance").text),ContentLoader.Load());
            model.Cities.Clear(); model.Units.Clear(); model.World.Reveal(model.World.Start,10000);
            var root=new GameObject("Full generated world"); root.transform.SetParent(transform,false); world=new WorldView(model,root.transform,true,false);
            Debug.Log("WORLD PREVIEW: "+world.ArtworkTileCount+" approved artwork tiles, no gameplay simulation");
        }
        public void Regenerate() { if(world!=null) world.Dispose(); world=null; seed++; Ensure(); }
        public void RefreshDecor() { if(world!=null) world.Dispose(); world=null; Ensure(); }
        void Update()
        {
            elapsed+=Time.unscaledDeltaTime; frames++;
            if(elapsed>=.5f) { fps=Mathf.RoundToInt(frames/elapsed); frames=0; elapsed=0; }
        }
        void OnDestroy() { if(world!=null) world.Dispose(); world=null; }
    }
}
