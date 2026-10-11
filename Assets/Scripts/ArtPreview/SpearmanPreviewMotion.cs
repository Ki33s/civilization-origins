using UnityEngine;

namespace Origins
{
    // Only gallery samples have this component. Gameplay selects poses explicitly.
    public sealed class SpearmanPreviewMotion : MonoBehaviour
    {
        SpearmanVisual[] actors;
        int previous=-1;
        void OnEnable() { previous=-1; }
        void Update()
        {
            if(actors==null) actors=GetComponentsInChildren<SpearmanVisual>();
            int pose=Mathf.FloorToInt(Time.time/3)%6;
            if(pose==previous) return;
            previous=pose;
            foreach(var actor in actors) actor.PreviewPose((SpearmanPose)pose);
        }
    }
}
