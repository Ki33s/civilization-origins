using UnityEngine;

namespace Origins
{
    public sealed class DecorModelSamples : MonoBehaviour
    {
        public void Ensure()
        {
            if(transform.childCount>0) return;
            var catalog=Resources.Load<DecorModelCatalog>("DecorModelCatalog");
            Vector3 right=new Vector3(.7071068f,0,-.7071068f),forward=new Vector3(.7071068f,0,.7071068f);
            for(int i=0;i<catalog.models.Length;i++)
            {
                var model=catalog.models[i]; var go=Instantiate(model.prefab,transform,false); go.name=model.id;
                go.transform.localPosition=right*((i%7-3)*6)+forward*((3-i/7)*6);
                var item=go.AddComponent<DevelopmentGalleryItem>(); item.title=model.label; item.description=model.id+" · "+model.triangles+" треуг.";
                var bounds=new Bounds(go.transform.position,Vector3.zero);
                foreach(var renderer in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                item.focusHeight=bounds.center.y-go.transform.position.y;
            }
        }
    }
}
