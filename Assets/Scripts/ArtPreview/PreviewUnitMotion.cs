using UnityEngine;

namespace Origins
{
    public sealed class PreviewUnitMotion : MonoBehaviour
    {
        ArticulatedActor[] members;
        Transform[] stones;
        void Start()
        {
            members=GetComponentsInChildren<ArticulatedActor>(); stones=new Transform[members.Length];
            for(int i=0;i<members.Length;i++) if(members[i].sling)
            {
                var pebble=members[i].sling.Find("Sling stone"); stones[i]=Instantiate(pebble,members[i].transform,false); stones[i].name="Preview thrown stone"; stones[i].gameObject.SetActive(false);
            }
        }
        void Update()
        {
            float t=Mathf.Repeat(Time.time,12);
            for(int i=0;i<members.Length;i++)
            {
                bool throwing=t>=4 && t<8 && members[i].profession=="Пращник";
                members[i].Pose(Time.time,t<4,false,t>=8,throwing);
                if(stones[i])
                {
                    float flight=Mathf.Repeat(t-4-i*.15f,1.6f); bool released=throwing && flight>.9f;
                    stones[i].gameObject.SetActive(released);
                    if(released) { float u=(flight-.9f)/.7f; stones[i].localPosition=Vector3.Lerp(new Vector3(0,.40f,.1f),new Vector3(0,.03f,1.1f),u)+Vector3.up*Mathf.Sin(u*Mathf.PI)*.16f; }
                }
            }
        }
    }
}
