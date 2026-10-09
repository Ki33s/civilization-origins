using UnityEngine;

namespace Origins
{
    public sealed class ArticulatedActor : MonoBehaviour
    {
        public Transform torso,leftArm,rightArm,leftForearm,rightForearm,leftLeg,rightLeg,sling;
        public string profession;
        public int toolStage;
        public void Pose(float time,bool walking,bool working,bool resting=false,bool throwing=false)
        {
            float wave=Mathf.Sin(time*8),stroke=(1-Mathf.Cos(time*4.5f))*.5f;
            torso.localRotation=Quaternion.Euler(resting ? 15 : working && profession!="Пастух" ? 10 : 0,0,0);
            leftLeg.localRotation=Quaternion.Euler(walking ? wave*28 : 0,0,0); rightLeg.localRotation=Quaternion.Euler(walking ? -wave*28 : 0,0,0);
            leftArm.localRotation=Quaternion.Euler(walking ? -wave*24 : 0,0,8); rightArm.localRotation=Quaternion.Euler(walking ? wave*24 : 0,0,-8);
            leftForearm.localRotation=rightForearm.localRotation=Quaternion.identity;
            if(working)
            {
                if(profession=="Каменотёс" || profession=="Земледелец")
                {
                    rightArm.localRotation=Quaternion.Euler(-145+stroke*(profession=="Земледелец" ? 120 : 90),0,-12);
                    rightForearm.localRotation=Quaternion.Euler(-25*(1-stroke),0,0);
                    leftArm.localRotation=Quaternion.Euler(-35,0,15);
                }
                else if(profession=="Гончар" || profession=="Обработчик шкур")
                {
                    rightArm.localRotation=Quaternion.Euler(-60+wave*8,0,-12); leftArm.localRotation=Quaternion.Euler(-60-wave*8,0,12);
                    leftForearm.localRotation=rightForearm.localRotation=Quaternion.Euler(-30,0,0);
                }
                else if(profession=="Рыбак") { rightArm.localRotation=Quaternion.Euler(-35+Mathf.Sin(time*2)*12,0,-12); }
                else if(profession=="Пастух") { rightArm.localRotation=Quaternion.Euler(-20,0,-12); torso.localRotation=Quaternion.Euler(0,Mathf.Sin(time*.7f)*12,0); }
                else { rightArm.localRotation=Quaternion.Euler(-30-stroke*45,0,-10); torso.localRotation=Quaternion.Euler(15+stroke*20,0,0); }
            }
            if(throwing && sling)
            {
                rightArm.localRotation=Quaternion.Euler(-115,0,-45); sling.localRotation=Quaternion.Euler(0,time*650,0);
                leftArm.localRotation=Quaternion.Euler(-70,0,20);
            }
            else if(sling) sling.localRotation=Quaternion.identity;
        }
    }
}
