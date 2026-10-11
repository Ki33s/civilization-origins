using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Origins.Editor
{
    public static partial class SpearmanModelBuilder
    {
        const string Pelvis="Rig/Pelvis";
        const string Spine=Pelvis+"/Spine";
        const string Chest=Spine+"/Chest";
        const string Neck=Chest+"/Neck";
        const string LeftArm=Chest+"/LeftShoulder";
        const string RightArm=Chest+"/RightShoulder";
        static readonly string[] AnimatedPaths={Pelvis,Spine,Chest,Neck,Neck+"/Head",
            Pelvis+"/LeftHip",Pelvis+"/LeftHip/Knee",Pelvis+"/LeftHip/Knee/Foot",
            Pelvis+"/RightHip",Pelvis+"/RightHip/Knee",Pelvis+"/RightHip/Knee/Foot",
            LeftArm,LeftArm+"/Elbow",LeftArm+"/Elbow/Hand",LeftArm+"/Elbow/Hand/ShieldGrip",
            RightArm,RightArm+"/Elbow",RightArm+"/Elbow/Hand",RightArm+"/Elbow/Hand/SpearGrip"};

        static AnimatorController BuildAnimations()
        {
            var clips=new Dictionary<string,AnimationClip>();
            foreach(SpearmanPose pose in Enum.GetValues(typeof(SpearmanPose))) clips.Add(pose.ToString(),CreateClip(pose));
            string path=ArtPath+"/Animations/Spearman.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(!controller) controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.parameters=new[]{new AnimatorControllerParameter{name="Moving",type=AnimatorControllerParameterType.Bool},new AnimatorControllerParameter{name="Guarding",type=AnimatorControllerParameterType.Bool}};
            var machine=controller.layers[0].stateMachine;
            foreach(var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
            foreach(var state in machine.states) machine.RemoveState(state.state);
            var states=new Dictionary<string,AnimatorState>(); int index=0;
            foreach(var pair in clips)
            {
                var state=machine.AddState(pair.Key,new Vector3(220+index%2*260,80+index/2*130,0)); index++;
                state.motion=pair.Value; state.writeDefaultValues=false; states.Add(pair.Key,state);
            }
            machine.defaultState=states["Idle"];
            Transition(states["Idle"],states["Walk"],"Moving",true);
            Transition(states["Walk"],states["Idle"],"Moving",false);
            Transition(states["Guard"],states["Idle"],"Guarding",false);
            foreach(string name in new[]{"Thrust","Hit"})
            {
                var transition=states[name].AddTransition(states["Idle"]);
                transition.hasExitTime=true; transition.exitTime=.98f; transition.hasFixedDuration=true; transition.duration=.12f;
            }
            EditorUtility.SetDirty(controller); return controller;
        }
        static void Transition(AnimatorState from,AnimatorState to,string parameter,bool value)
        {
            var transition=from.AddTransition(to); transition.hasExitTime=false; transition.hasFixedDuration=true; transition.duration=.12f;
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,0,parameter);
        }
        static AnimationClip CreateClip(SpearmanPose pose)
        {
            float duration=pose==SpearmanPose.Idle ? 1.6f : pose==SpearmanPose.Walk ? .9f : pose==SpearmanPose.Thrust ? 1.1f : pose==SpearmanPose.Guard ? 1.4f : pose==SpearmanPose.Hit ? .55f : 1.3f;
            bool loop=pose==SpearmanPose.Idle || pose==SpearmanPose.Walk || pose==SpearmanPose.Guard;
            var clip=new AnimationClip{name=pose.ToString(),frameRate=30,wrapMode=loop ? WrapMode.Loop : WrapMode.Once};
            var curves=new Dictionary<string,AnimationCurve[]>();
            foreach(string path in AnimatedPaths) curves.Add(path,new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve()});
            var position=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
            int frames=Mathf.CeilToInt(duration*30);
            for(int i=0;i<=frames;i++)
            {
                float normalized=(float)i/frames,time=normalized*duration;
                var rotations=Pose(pose,normalized,out Vector3 pelvis);
                foreach(string path in AnimatedPaths)
                {
                    Vector3 angles=rotations.ContainsKey(path) ? rotations[path] : Vector3.zero;
                    for(int axis=0;axis<3;axis++) curves[path][axis].AddKey(time,angles[axis]);
                }
                for(int axis=0;axis<3;axis++) position[axis].AddKey(time,pelvis[axis]);
            }
            string[] axes={"x","y","z"};
            foreach(var pair in curves) for(int axis=0;axis<3;axis++) SetCurve(clip,pair.Key,"localEulerAnglesRaw."+axes[axis],pair.Value[axis]);
            for(int axis=0;axis<3;axis++) SetCurve(clip,Pelvis,"m_LocalPosition."+axes[axis],position[axis]);
            var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=loop; settings.loopBlend=false;
            AnimationUtility.SetAnimationClipSettings(clip,settings);
            if(pose==SpearmanPose.Thrust) AnimationUtility.SetAnimationEvents(clip,new[]{new AnimationEvent{time=duration*.48f,functionName="OnThrustContact"}});
            if(pose==SpearmanPose.Walk) AnimationUtility.SetAnimationEvents(clip,new[]{new AnimationEvent{time=duration*.5f,functionName="OnFootstep"},new AnimationEvent{time=duration*.99f,functionName="OnFootstep"}});
            string pathName=ArtPath+"/Animations/"+pose+".anim";
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(pathName);
            if(existing) { EditorUtility.CopySerialized(clip,existing); UnityEngine.Object.DestroyImmediate(clip); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(clip,pathName); return clip;
        }
        static void SetCurve(AnimationClip clip,string path,string property,AnimationCurve curve)
        {
            // Linear samples keep feet from overshooting the planted positions between frames.
            for(int i=0;i<curve.length;i++) { AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear); }
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);
        }
        static Dictionary<string,Vector3> Pose(SpearmanPose pose,float t,out Vector3 pelvis)
        {
            var angles=new Dictionary<string,Vector3>(); float breath=Mathf.Sin(t*Mathf.PI*2);
            pelvis=new Vector3(0,.905f,0); float spine=0,chest=0,left=-12,right=-8,leftElbow=-65,rightElbow=-35,spear=0,shield=0;
            float leftZ=0,rightZ=0,leftLift=0,rightLift=0;
            if(pose==SpearmanPose.Idle) { chest=breath*1.3f; left+=breath; right+=breath; }
            if(pose==SpearmanPose.Walk)
            {
                pelvis.y=.877f+Mathf.Cos(t*4*Mathf.PI)*.005f;
                Step(t,out leftZ,out leftLift); Step(Mathf.Repeat(t+.5f,1),out rightZ,out rightLift);
                spine=4; left+=Mathf.Sin(t*2*Mathf.PI)*5; right-=Mathf.Sin(t*2*Mathf.PI)*7; spear=3;
            }
            if(pose==SpearmanPose.Thrust)
            {
                float wind=Pulse(t,0,.22f,.48f),strike=Pulse(t,.24f,.48f,1);
                pelvis.y-=strike*.04f; pelvis.z=strike*.07f;
                spine=strike*14-wind*6; leftZ=strike*.21f; rightZ=-strike*.13f;
                left=-24-strike*20; leftElbow=-60; right=-8+wind*24-strike*78; rightElbow=-35-wind*48+strike*24;
                spear=wind*-23+strike*90; shield=-strike*8;
            }
            if(pose==SpearmanPose.Guard)
            {
                pelvis.y=.875f; leftZ=.15f; rightZ=-.12f;
                spine=8+breath; left=-55; leftElbow=-58; right=-21; rightElbow=-65; spear=34; shield=-10;
            }
            if(pose==SpearmanPose.Hit)
            {
                float hit=Pulse(t,0,.28f,1); pelvis.y-=hit*.025f; pelvis.z=-hit*.045f;
                spine=-hit*17; left=-12-hit*18; right=-8-hit*12; spear=-hit*15;
            }
            if(pose==SpearmanPose.Death)
            {
                float fall=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.08f)/.72f));
                pelvis.y=Mathf.Lerp(.905f,.19f,fall); angles[Pelvis]=new Vector3(0,0,fall*86);
                spine=fall*15; chest=fall*-8; left=-12-fall*25; right=-8-fall*45; leftElbow=-65+fall*42; rightElbow=-35+fall*22;
                foreach(string side in new[]{"LeftHip","RightHip"}) { angles[Pelvis+"/"+side]=new Vector3(-fall*24,0,0); angles[Pelvis+"/"+side+"/Knee"]=new Vector3(fall*48,0,0); angles[Pelvis+"/"+side+"/Knee/Foot"]=new Vector3(-fall*24,0,0); }
                shield=fall*-75; spear=fall*20;
            }
            else { Leg(angles,"LeftHip",pelvis,leftZ,leftLift); Leg(angles,"RightHip",pelvis,rightZ,rightLift); }
            angles[Spine]=new Vector3(spine,0,0); angles[Chest]=new Vector3(chest,0,0);
            angles[Neck]=new Vector3(-(spine+chest)*.65f,0,0);
            angles[LeftArm]=new Vector3(left,0,-6); angles[LeftArm+"/Elbow"]=new Vector3(leftElbow,0,0);
            angles[RightArm]=new Vector3(right,0,6); angles[RightArm+"/Elbow"]=new Vector3(rightElbow,0,0);
            angles[LeftArm+"/Elbow/Hand/ShieldGrip"]=new Vector3(shield-spine-chest-left-leftElbow,0,0);
            angles[RightArm+"/Elbow/Hand/SpearGrip"]=new Vector3(spear-spine-chest-right-rightElbow,0,0);
            return angles;
        }
        static float Pulse(float t,float start,float peak,float end)
        {
            return t<peak ? Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,peak,t)) : Mathf.SmoothStep(1,0,Mathf.InverseLerp(peak,end,t));
        }
        static void Step(float phase,out float z,out float lift)
        {
            phase=Mathf.Repeat(phase,1);
            if(phase<.5f) { z=Mathf.Lerp(.16f,-.16f,phase*2); lift=0; }
            else { float swing=(phase-.5f)*2; z=Mathf.Lerp(-.16f,.16f,swing); lift=Mathf.Sin(swing*Mathf.PI)*.075f; }
        }
        static void Leg(Dictionary<string,Vector3> angles,string side,Vector3 pelvis,float targetZ,float lift)
        {
            const float length=.4f; float down=pelvis.y-.008f-(.10f+lift),z=targetZ-pelvis.z;
            float distance=Mathf.Min(.7999f,Mathf.Sqrt(down*down+z*z));
            float hip=-Mathf.Atan2(z,down)*Mathf.Rad2Deg-Mathf.Acos(distance/(2*length))*Mathf.Rad2Deg;
            float knee=180-Mathf.Acos(Mathf.Clamp((2*length*length-distance*distance)/(2*length*length),-1,1))*Mathf.Rad2Deg;
            angles[Pelvis+"/"+side]=new Vector3(hip,0,0); angles[Pelvis+"/"+side+"/Knee"]=new Vector3(knee,0,0); angles[Pelvis+"/"+side+"/Knee/Foot"]=new Vector3(-hip-knee,0,0);
        }
    }
}
