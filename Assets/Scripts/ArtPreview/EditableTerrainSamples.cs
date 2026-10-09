using UnityEngine;

namespace Origins
{
    public sealed class EditableTerrainSamples : MonoBehaviour
    {
        TerrainDecorRenderer decor;
        public void Ensure() { if(decor==null) Rebuild(); }
        public void Rebuild()
        {
            if(decor!=null) decor.Dispose();
            foreach(Transform child in transform) { child.gameObject.SetActive(false); } // Destroy after the current render frame.
            var children=new System.Collections.Generic.List<GameObject>(); foreach(Transform child in transform) children.Add(child.gameObject);
            foreach(var child in children) { child.transform.SetParent(null); Destroy(child); }
            decor=new TerrainDecorRenderer(transform); var catalog=Resources.Load<WorldTerrainCatalog>("WorldTerrainCatalog");
            Terrain[] kinds={Terrain.Forest,Terrain.Grass,Terrain.Plains,Terrain.Hills,Terrain.Mountain,Terrain.Desert,Terrain.Coast,Terrain.Ocean,Terrain.Swamp,Terrain.Tundra};
            Vector3 right=new Vector3(.7071068f,0,-.7071068f),forward=new Vector3(.7071068f,0,.7071068f);
            for(int i=0;i<kinds.Length;i++)
            {
                var entry=catalog.Get(kinds[i]); var go=Instantiate(entry.prefab,transform,false);
                go.transform.localPosition=right*((i%4-(i<8 ? 1.5f : .5f))*10.6f)+forward*((1-i/4)*11.8f);
                var inventory=decor.Create(kinds[i],go.transform,0); var item=go.AddComponent<DevelopmentGalleryItem>();
                item.title=TerrainDecorProfile.Name(kinds[i]); item.description=inventory.large+" крупных · "+inventory.small+" мелких"; item.focusHeight=i==4 ? 2 : i==0 || i==9 ? 1.5f : .5f;
            }
        }
        void OnDestroy() { if(decor!=null) decor.Dispose(); }
    }
}
