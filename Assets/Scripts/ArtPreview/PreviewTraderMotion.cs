using UnityEngine;

namespace Origins
{
    // A development sample, not an early-era production unlock or a trade-income simulation.
    public sealed class PreviewTraderMotion : MonoBehaviour
    {
        Transform actor;
        ArticulatedActor rig;
        LineRenderer trail;
        Material ownedMaterial;
        float travelled;
        void Start()
        {
            actor=transform.Find("Member"); rig=actor.GetComponentInChildren<ArticulatedActor>();
            var road=new GameObject("Merchant leaves a dry road"); road.transform.SetParent(transform,false);
            road.transform.localRotation=Quaternion.Euler(90,0,0);
            trail=road.AddComponent<LineRenderer>(); trail.useWorldSpace=false; trail.alignment=LineAlignment.TransformZ; trail.positionCount=25; trail.startWidth=trail.endWidth=.11f;
            trail.numCapVertices=4; ownedMaterial=new Material(Shader.Find("Origins/Dry Road")); ownedMaterial.color=new Color(.42f,.30f,.16f); trail.sharedMaterial=ownedMaterial;
        }
        void Update()
        {
            float cycle=Mathf.Repeat(Time.timeSinceLevelLoad,12),progress=cycle<6 ? cycle/6 : 1-(cycle-6)/6;
            travelled=Mathf.Max(travelled,progress);
            actor.localPosition=Point(progress); actor.localRotation=Quaternion.Euler(0,cycle<6 ? 90 : -90,0); rig.Pose(Time.time,true,false);
            for(int i=0;i<25;i++) trail.SetPosition(i,trail.transform.InverseTransformPoint(transform.TransformPoint(Point(travelled*i/24)+Vector3.up*.009f)));
        }
        static Vector3 Point(float t) { return new Vector3(Mathf.Lerp(-.65f,.65f,t),0,Mathf.Sin(t*Mathf.PI)*.16f); }
        void OnDestroy() { if(ownedMaterial) Destroy(ownedMaterial); }
    }
}
