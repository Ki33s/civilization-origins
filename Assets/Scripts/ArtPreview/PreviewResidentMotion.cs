using UnityEngine;

namespace Origins
{
    // Gallery-only motion; it never changes economy, population or gameplay orders.
    public sealed class PreviewResidentMotion : MonoBehaviour
    {
        public Transform actor;
        Vector3 origin;
        ArticulatedActor rig;
        void Start() { origin=actor.localPosition; rig=actor.GetComponentInChildren<ArticulatedActor>(); }
        void Update()
        {
            float t=Mathf.Repeat(Time.time,12);
            if(t<4)
            {
                float u=t/4; actor.localPosition=origin+new Vector3(Mathf.Lerp(-.36f,0,u),Mathf.Abs(Mathf.Sin(t*9))*.01f,0); rig.Pose(Time.time,true,false);
                actor.localRotation=Quaternion.Euler(0,90,0);
            }
            else if(t<8)
            {
                actor.localPosition=origin; actor.localRotation=Quaternion.identity; rig.Pose(Time.time,false,true);
            }
            else
            {
                actor.localPosition=origin+new Vector3(Mathf.Lerp(0,-.36f,(t-8)/4),0,0); rig.Pose(Time.time,t<10,false,t>=10);
                actor.localRotation=Quaternion.Euler(0,270,0);
            }
        }
    }
}
