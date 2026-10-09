using UnityEngine;

namespace Origins
{
    public sealed class AnimalMotion : MonoBehaviour
    {
        Transform[] legs; Transform head,tail; Vector3 origin;
        void Start() { origin=transform.localPosition; head=transform.Find("Grazing head pivot"); tail=transform.Find("Tail pivot"); var list=new System.Collections.Generic.List<Transform>(); foreach(Transform child in transform) if(child.name.StartsWith("Animal leg")) list.Add(child); legs=list.ToArray(); }
        void Update()
        {
            float t=Time.time+origin.x*3,step=Mathf.Sin(t*2);
            transform.localPosition=origin+new Vector3(Mathf.Sin(t*.35f)*.025f,0,Mathf.Cos(t*.35f)*.025f);
            for(int i=0;i<legs.Length;i++) legs[i].localRotation=Quaternion.Euler(0,0,step*(i%2==0 ? 9 : -9));
            if(head) head.localRotation=Quaternion.Euler(Mathf.Sin(t*.8f)*8,0,-Mathf.Max(0,Mathf.Sin(t*.6f))*48);
            if(tail) tail.localRotation=Quaternion.Euler(Mathf.Sin(t*3)*20,Mathf.Sin(t*2)*25,0);
        }
    }
}
