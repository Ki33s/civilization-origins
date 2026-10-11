using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace Origins
{
    public enum SpearmanPose { Idle, Walk, Thrust, Guard, Hit, Death }

    // Presentation only. Neither clip time nor animation events execute a game order.
    public sealed class SpearmanVisual : MonoBehaviour
    {
        public Animator animator;
        public Transform pelvis, head, leftFoot, rightFoot, rightHand, spearGrip, spearTip, shieldGrip;
        public Renderer[] teamCloth;
        public event Action ThrustContact;
        public event Action Footstep;
        static readonly int movingId=Animator.StringToHash("Moving");
        static readonly int guardingId=Animator.StringToHash("Guarding");
        int moving=-1;
        bool fallen;
        MaterialPropertyBlock tint;

        public static SpearmanVisual Create(Transform parent,Color team,float scale=.26f)
        {
            var prefab=Resources.Load<SpearmanVisual>("Units/Spearman");
            if(!prefab) throw new InvalidOperationException("Build the spearman prefab with Истоки → Разработка → Собрать копейщика");
            var actor=Instantiate(prefab,parent,false); actor.transform.localScale=Vector3.one*scale;
            actor.SetTeamColor(team); return actor;
        }
        public void SetTeamColor(Color color)
        {
            if(tint==null) tint=new MaterialPropertyBlock();
            tint.SetColor("_Color",color);
            foreach(var part in teamCloth) if(part) part.SetPropertyBlock(tint);
        }
        public void SetWalking(bool value)
        {
            int next=value && !fallen ? 1 : 0;
            if(moving==next) return;
            moving=next; animator.SetBool(movingId,next==1);
            if(next==1) animator.SetBool(guardingId,false);
        }
        public void PlayAction(SpearmanPose pose)
        {
            if(fallen) return;
            SetWalking(pose==SpearmanPose.Walk);
            animator.SetBool(guardingId,pose==SpearmanPose.Guard);
            if(pose==SpearmanPose.Death) fallen=true;
            animator.CrossFadeInFixedTime(pose.ToString(),.12f,0,0);
        }
        // Gallery and diagnostics can reset a visual without resurrecting a gameplay unit.
        public void PreviewPose(SpearmanPose pose,float normalizedTime=-1)
        {
            fallen=false; moving=-1; animator.speed=normalizedTime<0 ? 1 : 0;
            animator.SetBool(movingId,pose==SpearmanPose.Walk);
            animator.SetBool(guardingId,pose==SpearmanPose.Guard);
            moving=pose==SpearmanPose.Walk ? 1 : 0;
            animator.Play(pose.ToString(),0,normalizedTime<0 ? 0 : normalizedTime);
            animator.Update(0);
        }
        [Preserve] public void OnThrustContact() { ThrustContact?.Invoke(); }
        [Preserve] public void OnFootstep() { Footstep?.Invoke(); }
    }
}
