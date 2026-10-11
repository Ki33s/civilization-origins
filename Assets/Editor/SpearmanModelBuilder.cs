using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Origins.Editor
{
    public static partial class SpearmanModelBuilder
    {
        public const string PrefabPath="Assets/Resources/Units/Spearman.prefab";
        public const string ScenePath="Assets/Scenes/SpearmanShowcase.unity";
        const string ArtPath="Assets/Art/Units/Spearman";
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        static readonly HashSet<Transform> bones=new HashSet<Transform>();
        static Transform root;

        [MenuItem("Истоки/Разработка/Собрать копейщика")]
        public static void Generate()
        {
            foreach(string folder in new[]{ArtPath+"/Meshes",ArtPath+"/Materials",ArtPath+"/Animations","Assets/Resources/Units"}) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh(); materials.Clear(); bones.Clear();
            var model=new GameObject("Spearman"); root=model.transform;
            var visual=model.AddComponent<SpearmanVisual>();
            visual.animator=model.AddComponent<Animator>(); visual.animator.applyRootMotion=false;
            visual.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var skin=Material("Skin",new Color(.71f,.48f,.31f));
            var cloth=Material("Cloth",new Color(.75f,.53f,.25f));
            var linen=Material("Linen",new Color(.76f,.67f,.47f));
            var leather=Material("Leather",new Color(.25f,.14f,.08f));
            var hair=Material("Hair",new Color(.12f,.065f,.035f));
            var timber=Material("Timber",new Color(.37f,.22f,.105f));
            var rope=Material("Cord",new Color(.67f,.54f,.33f));
            var stone=Material("Flint",new Color(.43f,.44f,.41f));
            var white=Material("Eyes",new Color(.79f,.77f,.68f));
            var dark=Material("Pupils",new Color(.055f,.040f,.024f));
            var rig=Bone(root,"Rig",Vector3.zero);
            visual.pelvis=Bone(rig,"Pelvis",new Vector3(0,.905f,0));
            var spine=Bone(visual.pelvis,"Spine",new Vector3(0,.065f,0));
            var chest=Bone(spine,"Chest",new Vector3(0,.24f,0));
            var neck=Bone(chest,"Neck",new Vector3(0,.195f,0));
            visual.head=Bone(neck,"Head",new Vector3(0,.16f,0));
            Part(visual.pelvis,"Tunic skirt",Profile("Skirt",new[]{new Vector2(0,0),new Vector2(.235f,0),new Vector2(.23f,.025f),new Vector2(.19f,.15f),new Vector2(.17f,.23f),new Vector2(0,.23f)}),new Vector3(0,-.16f,0),new Vector3(1,1,.75f),cloth);
            Part(spine,"Waist cloth",Profile("Waist",new[]{new Vector2(0,0),new Vector2(.172f,0),new Vector2(.175f,.14f),new Vector2(.18f,.28f),new Vector2(0,.28f)}),new Vector3(0,-.02f,0),new Vector3(1,1,.77f),cloth);
            Part(chest,"Fitted chest",Profile("Chest",new[]{new Vector2(0,0),new Vector2(.177f,0),new Vector2(.188f,.08f),new Vector2(.182f,.135f),new Vector2(.145f,.195f),new Vector2(.07f,.22f),new Vector2(0,.22f)}),Vector3.zero,new Vector3(1,1,.72f),cloth);
            Ring(chest,"Bound neckline",new Vector3(0,.215f,0),.073f,linen);
            var shoulderStrap=Part(chest,"Diagonal woven shoulder strap",DetailGeometry.BevelledBlock(),new Vector3(0,.08f,.143f),new Vector3(.028f,.32f,.012f),leather);
            shoulderStrap.localRotation=Quaternion.Euler(0,0,-38);
            Block(chest,"Carved strap toggle",new Vector3(.056f,.03f,.153f),new Vector3(.043f,.029f,.016f),timber);
            for(int stitch=0;stitch<10;stitch++)
            {
                float y=stitch*.018f;
                Beam(chest,"Tunic front stitching",new Vector3(-.015f,y,.145f),new Vector3(.008f,y+.003f,.145f),.0018f,linen);
            }
            Part(spine,"Broad stitched belt",Profile("Belt",new[]{new Vector2(.166f,0),new Vector2(.18f,0),new Vector2(.18f,.045f),new Vector2(.166f,.045f),new Vector2(.166f,0)}),new Vector3(0,.02f,0),new Vector3(1,1,.80f),leather);
            Block(spine,"Wooden toggle",new Vector3(.015f,.043f,.146f),new Vector3(.045f,.033f,.018f),timber);
            Sphere(spine,"Belt pouch",new Vector3(-.18f,.012f,.025f),new Vector3(.105f,.14f,.095f),leather);
            Block(spine,"Pouch flap",new Vector3(-.18f,.063f,.06f),new Vector3(.088f,.04f,.02f),linen);
            for(int i=0;i<28;i++)
            {
                float a=i*Mathf.PI*2/28;
                Beam(visual.pelvis,"Hem stitch",new Vector3(Mathf.Cos(a)*.23f,-.14f,Mathf.Sin(a)*.172f),new Vector3(Mathf.Cos(a)*.23f,-.119f,Mathf.Sin(a)*.172f),.0018f,rope);
            }
            Sphere(neck,"Neck skin",new Vector3(0,.007f,0),new Vector3(.12f,.15f,.11f),skin);
            Part(visual.head,"Sculpted face",Profile("Head",new[]{new Vector2(0,-.155f),new Vector2(.065f,-.145f),new Vector2(.105f,-.11f),new Vector2(.13f,-.055f),new Vector2(.136f,.045f),new Vector2(.126f,.12f),new Vector2(.086f,.165f),new Vector2(0,.18f)},40),Vector3.zero,new Vector3(1,1,.89f),skin);
            Sphere(visual.head,"Nose bridge",new Vector3(0,.025f,.115f),new Vector3(.037f,.073f,.04f),skin);
            Sphere(visual.head,"Nose tip",new Vector3(0,-.006f,.138f),new Vector3(.048f,.029f,.041f),skin);
            Beam(visual.head,"Mouth",new Vector3(-.032f,-.062f,.118f),new Vector3(.032f,-.061f,.118f),.0035f,leather);
            Sphere(visual.head,"Beard chin",new Vector3(0,-.11f,.095f),new Vector3(.10f,.09f,.085f),hair);
            Sphere(visual.head,"Hair cap",new Vector3(0,.085f,-.010f),new Vector3(.28f,.24f,.27f),hair);
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI*2/16;
                var lockPart=Sphere(visual.head,"Swept hair lock",new Vector3(Mathf.Cos(a)*.09f,.105f,Mathf.Sin(a)*.075f-.028f),new Vector3(.059f,.133f,.038f),hair);
                lockPart.localRotation=Quaternion.Euler(-15,0,Mathf.Cos(a)*-25);
            }
            Sphere(visual.head,"Tied hair knot",new Vector3(0,.19f,-.06f),new Vector3(.098f,.075f,.10f),hair);
            Ring(visual.head,"Hair tie",new Vector3(0,.158f,-.055f),.041f,rope);
            for(int sign=-1;sign<=1;sign+=2)
            {
                Sphere(visual.head,"Ear",new Vector3(sign*.134f,-.007f,0),new Vector3(.044f,.073f,.042f),skin);
                Sphere(visual.head,"Eye white",new Vector3(sign*.049f,.033f,.111f),new Vector3(.044f,.024f,.013f),white);
                Sphere(visual.head,"Iris",new Vector3(sign*.049f,.033f,.120f),new Vector3(.019f,.023f,.008f),dark);
                Beam(visual.head,"Eyebrow",new Vector3(sign*.027f,.061f,.117f),new Vector3(sign*.074f,.062f,.109f),.008f,hair);
                Sphere(visual.head,"Beard side",new Vector3(sign*.085f,-.075f,.064f),new Vector3(.056f,.105f,.07f),hair);
                var hip=Bone(visual.pelvis,sign<0 ? "LeftHip" : "RightHip",new Vector3(sign*.10f,-.008f,0));
                Part(hip,"Upper leg",Limb(),new Vector3(0,-.40f,0),new Vector3(.066f,.40f,.066f),linen);
                var knee=Bone(hip,"Knee",new Vector3(0,-.40f,0));
                Sphere(knee,"Knee skin",Vector3.zero,new Vector3(.112f,.11f,.105f),skin);
                Part(knee,"Shin",Limb(),new Vector3(0,-.40f,0),new Vector3(.047f,.40f,.047f),skin);
                Ring(knee,"Calf binding",new Vector3(0,-.25f,0),.051f,leather);
                Ring(knee,"Ankle binding",new Vector3(0,-.35f,0),.048f,leather);
                var foot=Bone(knee,"Foot",new Vector3(0,-.40f,0));
                Sphere(foot,"Foot skin",new Vector3(0,-.025f,.047f),new Vector3(.115f,.075f,.205f),skin);
                Block(foot,"Sandal sole",new Vector3(0,-.065f,.055f),new Vector3(.14f,.045f,.25f),leather);
                for(int lace=0;lace<3;lace++) Beam(foot,"Sandal strap",new Vector3(-.062f,-.012f,lace*.054f),new Vector3(.062f,-.011f,.035f+lace*.054f),.006f,leather);
                var shoulder=Bone(chest,sign<0 ? "LeftShoulder" : "RightShoulder",new Vector3(sign*.20f,.14f,0));
                Sphere(shoulder,"Sleeve shoulder",new Vector3(0,-.028f,0),new Vector3(.128f,.13f,.125f),cloth);
                Part(shoulder,"Upper arm",Limb(),new Vector3(0,-.25f,0),new Vector3(.047f,.25f,.047f),skin);
                Part(shoulder,"Sleeve",Limb(),new Vector3(0,-.12f,0),new Vector3(.060f,.13f,.060f),cloth);
                Ring(shoulder,"Sleeve hem",new Vector3(0,-.114f,0),.061f,linen);
                var elbow=Bone(shoulder,"Elbow",new Vector3(0,-.25f,0));
                Sphere(elbow,"Elbow skin",Vector3.zero,new Vector3(.09f,.085f,.09f),skin);
                Part(elbow,"Forearm",Limb(),new Vector3(0,-.24f,0),new Vector3(.040f,.24f,.040f),skin);
                Ring(elbow,"Wrist wrap",new Vector3(0,-.21f,0),.041f,leather);
                var hand=Bone(elbow,"Hand",new Vector3(0,-.255f,0));
                Sphere(hand,"Palm",new Vector3(0,-.014f,0),new Vector3(.072f,.092f,.045f),skin);
                for(int finger=0;finger<4;finger++)
                {
                    float x=(finger-1.5f)*.018f;
                    Beam(hand,"Finger proximal",new Vector3(x,-.045f,0),new Vector3(x,-.057f,.023f),.008f,skin);
                    Sphere(hand,"Finger knuckle",new Vector3(x,-.055f,.024f),Vector3.one*.019f,skin);
                    Beam(hand,"Finger curled tip",new Vector3(x,-.055f,.024f),new Vector3(x,-.032f,.036f),.007f,skin);
                }
                Sphere(hand,"Gripping thumb",new Vector3(sign*.034f,-.004f,.02f),new Vector3(.020f,.043f,.028f),skin);
                if(sign<0) { visual.leftFoot=foot; visual.shieldGrip=Bone(hand,"ShieldGrip",new Vector3(-.026f,-.012f,.066f)); }
                else { visual.rightFoot=foot; visual.rightHand=hand; visual.spearGrip=Bone(hand,"SpearGrip",new Vector3(0,-.032f,.028f)); }
            }
            Beam(visual.spearGrip,"Long wooden shaft",new Vector3(0,-.65f,0),new Vector3(0,1.13f,0),.016f,timber);
            Part(visual.spearGrip,"Chipped stone point",FlintPoint(),new Vector3(0,1.095f,0),Vector3.one,stone);
            for(int tie=0;tie<8;tie++) Ring(visual.spearGrip,"Point lashing",new Vector3(0,1.035f+tie*.013f,0),.023f,rope);
            for(int tie=0;tie<3;tie++) Ring(visual.spearGrip,"Grip wrapping",new Vector3(0,-.04f+tie*.017f,0),.021f,leather);
            visual.spearTip=Bone(visual.spearGrip,"Tip",new Vector3(0,1.355f,0));
            float radius=.24f;
            for(int plank=-3;plank<=3;plank++)
            {
                float x=plank*.063f,h=2*Mathf.Sqrt(radius*radius-x*x);
                Block(visual.shieldGrip,"Individual shield plank",new Vector3(x,0,0),new Vector3(.060f,h,.031f),timber);
                for(int grain=0;grain<2;grain++) Beam(visual.shieldGrip,"Carved plank groove",new Vector3(x+(grain-.5f)*.017f,-h*.43f,.017f),new Vector3(x+(grain-.5f)*.017f,h*.43f,.017f),.0014f,leather);
            }
            var rim=Ring(visual.shieldGrip,"Rawhide shield rim",Vector3.zero,radius,leather); rim.localRotation=Quaternion.Euler(90,0,0);
            Sphere(visual.shieldGrip,"Bound center",new Vector3(0,0,.029f),new Vector3(.080f,.080f,.035f),leather);
            for(int tie=0;tie<12;tie++)
            {
                float a=tie*Mathf.PI/6;
                Beam(visual.shieldGrip,"Rim fastening",new Vector3(Mathf.Cos(a)*.218f,Mathf.Sin(a)*.218f,-.017f),new Vector3(Mathf.Cos(a)*.251f,Mathf.Sin(a)*.251f,.017f),.004f,rope);
            }
            for(int strap=0;strap<2;strap++)
            {
                var band=Ring(visual.shieldGrip,"Rear leather arm strap",new Vector3(0,(strap-.5f)*.11f,-.054f),.058f,leather);
                band.localRotation=Quaternion.Euler(0,0,90); band.localScale=new Vector3(.75f,1,.65f)*.058f;
            }
            visual.teamCloth=BakeGeometry(cloth);
            // Use material shading on the fine grooves; the shield still casts a ground shadow.
            foreach(var surface in visual.shieldGrip.GetComponentsInChildren<MeshRenderer>()) surface.receiveShadows=false;
            visual.animator.runtimeAnimatorController=BuildAnimations();
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtPath+"/Animations/Idle.anim"); idle.SampleAnimation(model,0);
            PrefabUtility.SaveAsPrefabAsset(model,PrefabPath); Object.DestroyImmediate(model);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            InstallGallery(); CreateShowcase(); Validate();
            Debug.Log("SPEARMAN: model, six animation clips, prefab and isometric showcase saved");
        }

        static Material Material(string name,Color color)
        {
            var path=ArtPath+"/Materials/"+name+".mat";
            var value=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!value) { value=new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(value,path); }
            value.color=color; value.SetFloat("_Glossiness",.12f); value.enableInstancing=true;
            EditorUtility.SetDirty(value); materials.Add(name,value); return value;
        }
        static Transform Bone(Transform parent,string name,Vector3 position)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.localPosition=position; bones.Add(go.transform); return go.transform;
        }
        static Transform Part(Transform parent,string name,Mesh mesh,Vector3 p,Vector3 size,Material material)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.localPosition=p; go.transform.localScale=size;
            go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=material; return go.transform;
        }
        static Transform Sphere(Transform parent,string name,Vector3 p,Vector3 size,Material material)
        {
            var profile=new Vector2[13]; for(int i=0;i<profile.Length;i++) { float a=i*Mathf.PI/12; profile[i]=new Vector2(Mathf.Sin(a)*.5f,-Mathf.Cos(a)*.5f); }
            return Part(parent,name,Profile("Sphere",profile,24),p,size,material);
        }
        static Mesh Profile(string name,Vector2[] points,int sides=32) { return DetailGeometry.Lathe("Spearman "+name,points,sides); }
        static Mesh Limb() { return Profile("Limb",new[]{new Vector2(0,0),new Vector2(.72f,0),new Vector2(.84f,.12f),new Vector2(1,.40f),new Vector2(.90f,.85f),new Vector2(.75f,1),new Vector2(0,1)},24); }
        static void Block(Transform parent,string name,Vector3 p,Vector3 size,Material material) { Part(parent,name,DetailGeometry.BevelledBlock(),p,size,material); }
        static void Beam(Transform parent,string name,Vector3 a,Vector3 b,float radius,Material material)
        {
            var mesh=Profile("Shaft",new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)},16);
            var part=Part(parent,name,mesh,a,new Vector3(radius,(b-a).magnitude,radius),material); part.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        }
        static Transform Ring(Transform parent,string name,Vector3 p,float radius,Material material) { return Part(parent,name,DetailGeometry.Ring(),p,Vector3.one*radius,material); }
        static Mesh FlintPoint()
        {
            var outline=new[]{new Vector2(-.010f,0),new Vector2(-.025f,.024f),new Vector2(-.037f,.054f),new Vector2(-.043f,.097f),new Vector2(-.033f,.143f),new Vector2(-.020f,.196f),new Vector2(0,.26f),new Vector2(.023f,.192f),new Vector2(.034f,.14f),new Vector2(.041f,.09f),new Vector2(.033f,.052f),new Vector2(.021f,.025f),new Vector2(.010f,0)};
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            for(int side=-1;side<=1;side+=2) for(int i=0;i<outline.Length;i++)
            {
                var a=new Vector3(outline[i].x,outline[i].y,0); var b=new Vector3(outline[(i+1)%outline.Length].x,outline[(i+1)%outline.Length].y,0);
                var c=new Vector3(0,.115f,side*.024f); var center=(a+b+c)/3+Vector3.forward*(side*(i%2==0 ? .003f : -.002f));
                foreach(var pair in new[]{new[]{a,b},new[]{b,c},new[]{c,a}})
                {
                    int start=vertices.Count; vertices.Add(center); vertices.Add(pair[side>0 ? 1 : 0]); vertices.Add(pair[side>0 ? 0 : 1]);
                    triangles.Add(start); triangles.Add(start+1); triangles.Add(start+2);
                }
            }
            var mesh=new Mesh{name="Hand-knapped flint"}; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Renderer[] BakeGeometry(Material cloth)
        {
            var originals=root.GetComponentsInChildren<MeshFilter>(); var tinted=new List<Renderer>();
            foreach(var bone in bones.OrderBy(b=>AnimationUtility.CalculateTransformPath(b,root))) foreach(var material in materials.Values.OrderBy(m=>m.name))
            {
                var combine=new List<CombineInstance>();
                foreach(var filter in originals)
                {
                    Transform owner=filter.transform.parent; while(owner && !bones.Contains(owner)) owner=owner.parent;
                    if(owner==bone && filter.GetComponent<Renderer>().sharedMaterial==material) combine.Add(new CombineInstance{mesh=filter.sharedMesh,transform=bone.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                }
                if(combine.Count==0) continue;
                var mesh=new Mesh{name=bone.name+" "+material.name,indexFormat=IndexFormat.UInt32}; mesh.CombineMeshes(combine.ToArray(),true,true); mesh.RecalculateBounds();
                string path=ArtPath+"/Meshes/"+AnimationUtility.CalculateTransformPath(bone,root).Replace('/','_')+"_"+material.name+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing) { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); mesh=existing; EditorUtility.SetDirty(existing); }
                else AssetDatabase.CreateAsset(mesh,path);
                var part=Part(bone,material.name+" surface",mesh,Vector3.zero,Vector3.one,material).GetComponent<Renderer>();
                if(material==cloth) tinted.Add(part);
            }
            foreach(var filter in originals) Object.DestroyImmediate(filter.gameObject);
            return tinted.ToArray();
        }
    }
}
