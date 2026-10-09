using UnityEngine;

namespace Origins
{
    public sealed partial class WorldView
    {
        ArticulatedActor DetailedPerson(Transform parent,Color outfit,float size,string profession="",int stage=0)
        {
            var body=Node(parent,"Articulated human",Vector3.zero); body.localScale=Vector3.one*size;
            var rig=body.gameObject.AddComponent<ArticulatedActor>(); rig.profession=profession; rig.toolStage=stage;
            var skin=Mat("art skin",new Color(.72f,.49f,.32f)); var cloth=Mat("art cloth "+outfit,outfit); var leather=Mat("art leather",new Color(.28f,.17f,.1f));
            rig.torso=Node(body,"Torso pivot",new Vector3(0,.155f,0));
            Detail(rig.torso,"Tailored tunic",DetailGeometry.Torso(),Vector3.zero,new Vector3(1,1,.72f),cloth);
            Band(rig.torso,new Vector3(0,.075f,0),.054f,leather);
            Block(rig.torso,"Belt buckle",new Vector3(0,.075f,.045f),new Vector3(.019f,.021f,.006f),Rope);
            Round(rig.torso,"Leather pouch",new Vector3(-.06f,.065f,0),new Vector3(.035f,.045f,.027f),leather);
            Beam(rig.torso,"Neck",new Vector3(0,.17f,0),new Vector3(0,.22f,0),.015f,skin);
            var head=Node(rig.torso,"Head",new Vector3(0,.238f,0));
            Round(head,"Sculpted head",Vector3.zero,new Vector3(.077f,.095f,.075f),skin);
            Round(head,"Hair cap",new Vector3(0,.024f,-.004f),new Vector3(.078f,.056f,.076f),Mat("art hair",new Color(.16f,.10f,.07f)));
            for(int lockIndex=0;lockIndex<8;lockIndex++)
            {
                float a=lockIndex*Mathf.PI/4;
                Round(head,"Individual hair lock",new Vector3(Mathf.Cos(a)*.029f,.022f,Mathf.Sin(a)*.026f-.006f),new Vector3(.018f,.035f,.014f),Mat("art hair",new Color(.16f,.10f,.07f)));
            }
            Round(head,"Nose",new Vector3(0,-.002f,.038f),new Vector3(.014f,.018f,.018f),skin);
            for(int side=0;side<2;side++)
            {
                float s=side==0 ? -1 : 1;
                Round(head,"Ear",new Vector3(s*.037f,0,0),new Vector3(.016f,.026f,.013f),skin);
                Round(head,"Eye",new Vector3(s*.018f,.006f,.032f),new Vector3(.010f,.007f,.008f),Mat("art eyes",new Color(.09f,.07f,.05f)));
                Beam(head,"Eyebrow",new Vector3(s*.012f,.014f,.031f),new Vector3(s*.024f,.014f,.028f),.002f,leather);
                var leg=Node(body,side==0 ? "Left hip" : "Right hip",new Vector3(s*.029f,.16f,0));
                Beam(leg,"Upper leg",Vector3.zero,new Vector3(0,-.066f,0),.019f,cloth);
                Round(leg,"Knee",new Vector3(0,-.075f,0),new Vector3(.034f,.034f,.032f),skin);
                Beam(leg,"Lower leg",new Vector3(0,-.083f,0),new Vector3(0,-.13f,.001f),.014f,skin);
                Round(leg,"Stitched leather shoe",new Vector3(0,-.145f,.014f),new Vector3(.04f,.026f,.075f),leather);
                for(int lace=0;lace<3;lace++) Beam(leg,"Shoe lace",new Vector3(-.011f,-.131f,.01f+lace*.009f),new Vector3(.011f,-.131f,.01f+lace*.009f),.0012f,Rope);
                var arm=Node(rig.torso,side==0 ? "Left shoulder" : "Right shoulder",new Vector3(s*.065f,.15f,0));
                Round(arm,"Shoulder cloth",new Vector3(0,-.013f,0),new Vector3(.045f,.05f,.043f),cloth);
                Beam(arm,"Upper arm",Vector3.zero,new Vector3(0,-.063f,0),.014f,skin);
                var lower=Node(arm,"Elbow joint",new Vector3(0,-.066f,0));
                Round(lower,"Elbow",Vector3.zero,Vector3.one*.028f,skin); Beam(lower,"Forearm",Vector3.zero,new Vector3(0,-.06f,0),.012f,skin);
                Round(lower,"Hand and fingers",new Vector3(0,-.071f,.006f),new Vector3(.027f,.031f,.02f),skin);
                for(int finger=0;finger<4;finger++) Beam(lower,"Curled finger",new Vector3((finger-1.5f)*.005f,-.072f,.012f),new Vector3((finger-1.5f)*.005f,-.086f,.008f),.002f,skin);
                if(side==0) { rig.leftLeg=leg; rig.leftArm=arm; rig.leftForearm=lower; } else { rig.rightLeg=leg; rig.rightArm=arm; rig.rightForearm=lower; }
            }
            var grip=Node(rig.rightForearm,"Tool grip",new Vector3(0,-.065f,.006f));
            for(int stitch=0;stitch<8;stitch++) Beam(rig.torso,"Tunic hem stitching",new Vector3(Mathf.Cos(stitch*Mathf.PI/4)*.063f,.008f,Mathf.Sin(stitch*Mathf.PI/4)*.044f),new Vector3(Mathf.Cos(stitch*Mathf.PI/4)*.063f,.016f,Mathf.Sin(stitch*Mathf.PI/4)*.044f),.001f,Rope);
            if(profession=="Каменотёс") { MiningTool(grip,stage); grip.localRotation=Quaternion.Euler(180,0,0); }
            else if(profession=="Земледелец") { Beam(grip,"Hoe shaft",new Vector3(0,-.09f,0),new Vector3(0,.17f,0),.006f,Wood); Block(grip,"Stone hoe blade",new Vector3(0,.17f,.02f),new Vector3(.045f,.022f,.05f),Stone); grip.localRotation=Quaternion.Euler(180,0,0); }
            else if(profession=="Рыбак")
            {
                Beam(grip,"Fishing rod",Vector3.zero,new Vector3(0,.34f,.12f),.004f,Wood);
                Beam(grip,"Fishing line",new Vector3(0,.34f,.12f),new Vector3(0,-.16f,.28f),.0009f,Rope);
                Round(grip,"Fishing float",new Vector3(0,-.10f,.26f),new Vector3(.008f,.021f,.008f),Straw);
            }
            else if(profession=="Пастух")
            {
                Beam(grip,"Shepherd staff",new Vector3(0,-.16f,0),new Vector3(0,.28f,0),.005f,Wood);
                for(int i=0;i<8;i++) { float a=i*Mathf.PI/10,b=(i+1)*Mathf.PI/10; Beam(grip,"Staff curved top",new Vector3(Mathf.Cos(a)*.025f-.025f,.28f+Mathf.Sin(a)*.025f,0),new Vector3(Mathf.Cos(b)*.025f-.025f,.28f+Mathf.Sin(b)*.025f,0),.004f,Wood); }
            }
            else if(profession=="Пращник")
            {
                var sling=Node(grip,"Sling pivot",Vector3.zero); rig.sling=sling;
                Beam(sling,"Sling cord",Vector3.zero,new Vector3(.12f,.04f,0),.0018f,Rope); Beam(sling,"Sling return cord",new Vector3(0,.008f,0),new Vector3(.12f,.04f,0),.0018f,Rope);
                Round(sling,"Sling leather pouch",new Vector3(.12f,.04f,0),new Vector3(.037f,.015f,.022f),leather);
                Round(sling,"Sling stone",new Vector3(.12f,.05f,0),Vector3.one*.019f,Stone);
                Round(rig.torso,"Ammunition bag",new Vector3(.05f,.06f,-.01f),new Vector3(.045f,.055f,.034f),leather);
            }
            else if(profession=="Воин" || profession=="Охотник" || profession=="Разведчик")
            {
                Beam(grip,"Spear / walking staff",new Vector3(0,-.12f,0),new Vector3(0,.25f,0),.005f,Wood);
                if(profession!="Разведчик") Round(grip,"Knapped spear point",new Vector3(0,.28f,0),new Vector3(.016f,.07f,.009f),Stone);
                if(profession=="Воин") { var shield=Node(rig.leftForearm,"Hide shield",new Vector3(0,-.04f,.03f)); Round(shield,"Shield face",Vector3.zero,new Vector3(.13f,.15f,.022f),leather); for(int i=0;i<4;i++) Beam(shield,"Shield reinforcing ribs",new Vector3((i-1.5f)*.025f,-.05f,0),new Vector3((i-1.5f)*.025f,.05f,0),.003f,Wood); }
            }
            else if(profession!="Гончар" && profession!="Обработчик шкур")
            {
                Detail(rig.leftForearm,"Woven gathering basket",DetailGeometry.Pot(),new Vector3(0,-.16f,.02f),new Vector3(.039f,.056f,.039f),Straw);
            }
            rig.Pose(0,false,false); return rig;
        }
        void MiningTool(Transform grip,int stage)
        {
            if(stage==0) { Round(grip,"Hand-held knapped stone",new Vector3(0,.02f,.014f),new Vector3(.036f,.043f,.029f),Stone); return; }
            Beam(grip,"Bound wooden tool handle",new Vector3(0,-.03f,0),new Vector3(0,.14f,0),.005f,Wood);
            var headMaterial=stage==3 ? Mat("art bronze",new Color(.62f,.39f,.18f)) : Stone;
            if(stage==1) Round(grip,"Stone hammer head",new Vector3(0,.14f,0),new Vector3(.06f,.034f,.038f),headMaterial);
            else { Beam(grip,stage==3 ? "Bronze pick head" : "Stone pick head",new Vector3(-.052f,.125f,0),new Vector3(.052f,.16f,0),.008f,headMaterial); }
            for(int i=0;i<4;i++) Band(grip,new Vector3(0,.122f+i*.007f,0),.008f,Rope);
        }
        void WorkContext(Transform parent,string profession,int stage)
        {
            if(profession=="Каменотёс") Rock(parent,new Vector3(.04f,0,.23f),.32f);
            else if(profession=="Пастух")
            {
                for(int i=0;i<3;i++) DetailedAnimal(parent,new Vector3((i-1)*.26f,0,.3f+(i%2)*.14f),true);
                for(int i=0;i<4;i++) Beam(parent,"Pasture fence post",new Vector3((i-1.5f)*.24f,0,.66f),new Vector3((i-1.5f)*.24f,.22f,.66f),.012f,Wood);
                Beam(parent,"Pasture rail",new Vector3(-.36f,.16f,.66f),new Vector3(.36f,.16f,.66f),.008f,Wood);
            }
            else if(profession=="Гончар") { Detail(parent,"Working potter wheel",DetailGeometry.Ring(),new Vector3(0,.16f,.25f),Vector3.one*.12f,Wood); Jar(parent,new Vector3(0,.17f,.25f),.045f,.085f); }
            else if(profession=="Обработчик шкур") { Table(parent,new Vector3(0,0,.25f)); Round(parent,"Hide on workbench",new Vector3(0,.22f,.25f),new Vector3(.30f,.014f,.20f),Mat("art leather",new Color(.28f,.17f,.10f))); }
            else if(profession=="Земледелец") for(int i=0;i<5;i++) { Block(parent,"Soil furrow",new Vector3((i-2)*.08f,.015f,.23f),new Vector3(.045f,.022f,.26f),Mat("art soil",new Color(.32f,.21f,.12f))); Wheat(parent,new Vector3((i-2)*.08f,.025f,.23f)); }
            else if(profession=="Рыбак") Block(parent,"Water at fishing spot",new Vector3(0,.008f,.35f),new Vector3(.55f,.018f,.28f),Mat("art garden water",new Color(.22f,.53f,.62f)));
            else if(profession=="Охотник") DetailedAnimal(parent,new Vector3(0,0,.40f),false);
            else BerryBush(parent,new Vector3(0,0,.23f),false);
        }
        Transform DetailedAnimal(Transform parent,Vector3 p,bool sheep)
        {
            var animal=Node(parent,sheep ? "Detailed sheep" : "Detailed deer",p);
            var hide=sheep ? Mat("art wool",new Color(.81f,.78f,.67f)) : Mat("art deer hide",new Color(.54f,.34f,.18f));
            Round(animal,"Animal body",new Vector3(0,.17f,0),new Vector3(.25f,.17f,.13f),hide);
            Round(animal,"Chest muscle",new Vector3(.075f,.16f,0),new Vector3(.10f,.17f,.13f),hide);
            Round(animal,"Hindquarters",new Vector3(-.08f,.16f,0),new Vector3(.09f,.15f,.14f),hide);
            Round(animal,"Pale underside",new Vector3(0,.117f,0),new Vector3(.17f,.045f,.10f),Mat("animal underside",new Color(.72f,.62f,.44f)));
            var head=Node(animal,"Grazing head pivot",new Vector3(.10f,.20f,0));
            Round(head,"Neck",new Vector3(.01f,.01f,0),new Vector3(.09f,.14f,.075f),hide);
            Round(head,"Head",new Vector3(.055f,.055f,0),new Vector3(.075f,.067f,.066f),hide);
            Round(head,"Tapered muzzle",new Vector3(.096f,.041f,0),new Vector3(.059f,.045f,.045f),hide);
            Round(head,"Dark nose",new Vector3(.121f,.041f,0),new Vector3(.014f,.021f,.034f),Wood);
            Beam(head,"Mouth",new Vector3(.082f,.024f,-.021f),new Vector3(.116f,.024f,-.012f),.0012f,Wood);
            for(int i=0;i<4;i++)
            {
                var leg=Node(animal,"Animal leg "+i,new Vector3(i%2==0 ? -.075f : .075f,.15f,i<2 ? -.04f : .04f));
                Round(leg,"Upper leg muscle",Vector3.down*.022f,new Vector3(.033f,.06f,.027f),hide);
                Beam(leg,"Animal shin",Vector3.down*.038f,Vector3.down*.10f,.008f,hide);
                Round(leg,"Hock joint",Vector3.down*.093f,Vector3.one*.022f,hide);
                Beam(leg,"Lower pastern",Vector3.down*.10f,Vector3.down*.125f,.006f,hide);
                for(int hoof=0;hoof<2;hoof++) Round(leg,"Split cloven hoof",new Vector3(.006f,-.13f,(hoof-.5f)*.011f),new Vector3(.023f,.018f,.010f),Wood);
            }
            for(int i=0;i<2;i++)
            {
                float s=i==0 ? -1 : 1;
                Round(head,"Animal ear",new Vector3(.035f,.085f,s*.047f),new Vector3(.028f,.013f,.048f),hide);
                Round(head,"Ear inner skin",new Vector3(.035f,.089f,s*.049f),new Vector3(.022f,.005f,.032f),Mat("animal ear inner",new Color(.58f,.37f,.31f)));
                Round(head,"Animal eye",new Vector3(.073f,.063f,s*.029f),Vector3.one*.009f,Wood);
                Round(head,"Eye glint",new Vector3(.075f,.065f,s*.033f),Vector3.one*.0025f,Rope);
                if(!sheep) for(int tine=0;tine<3;tine++)
                {
                    Vector3 start=new Vector3(.022f-tine*.009f,.09f+tine*.027f,s*(.02f+tine*.012f));
                    Beam(head,"Branched antler beam",start,start+new Vector3(-.012f,.04f,s*.015f),.004f,Stone);
                    Beam(head,"Antler tine",start,start+new Vector3(.025f,.038f,s*.006f),.003f,Stone);
                }
            }
            if(sheep) for(int ring=0;ring<6;ring++) for(int curl=0;curl<12;curl++)
            {
                float a=curl*Mathf.PI/6; Round(animal,"Individual wool curl",new Vector3((ring-2.5f)*.038f,.175f+Mathf.Sin(a)*.064f,Mathf.Cos(a)*.058f),new Vector3(.045f,.040f,.032f),hide);
            }
            else for(int spot=0;spot<12;spot++) Round(animal,"Fur highlight",new Vector3(-.07f+(spot%4)*.033f,.19f+(spot/4)*.019f,spot%2==0 ? -.060f : .060f),new Vector3(.012f,.007f,.004f),Mat("deer fur highlight",new Color(.68f,.47f,.27f)));
            var tail=Node(animal,"Tail pivot",new Vector3(-.125f,.19f,0));
            Round(tail,"Furred tail",new Vector3(-.019f,0,0),new Vector3(.048f,.024f,.025f),hide);
            animal.gameObject.AddComponent<AnimalMotion>(); return animal;
        }
    }
}
