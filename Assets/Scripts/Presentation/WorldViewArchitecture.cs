using UnityEngine;

namespace Origins
{
    public sealed partial class WorldView
    {
        Material Wood { get { return Mat("art timber",new Color(.34f,.20f,.10f)); } }
        Material Straw { get { return Mat("art straw",new Color(.64f,.47f,.24f)); } }
        Material Clay { get { return Mat("art clay",new Color(.64f,.33f,.18f)); } }
        Material Stone { get { return Mat("art limestone",new Color(.69f,.66f,.53f)); } }
        Material Rope { get { return Mat("art rope",new Color(.70f,.59f,.37f)); } }
        Transform Node(Transform parent,string name,Vector3 position)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.localPosition=position; return go.transform;
        }
        GameObject Detail(Transform parent,string name,Mesh mesh,Vector3 position,Vector3 scale,Material material)
        {
            var node=Node(parent,name,position); node.localScale=scale; node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh; node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material; return node.gameObject;
        }
        GameObject Block(Transform parent,string name,Vector3 p,Vector3 size,Material mat)
        { return Detail(parent,name,DetailGeometry.BevelledBlock(),p,size,mat); }
        GameObject Round(Transform parent,string name,Vector3 p,Vector3 size,Material mat)
        { var go=Primitive(parent,PrimitiveType.Sphere,p,size,mat); go.name=name; return go; }
        void Beam(Transform parent,string name,Vector3 a,Vector3 b,float radius,Material mat)
        {
            var shape=DetailGeometry.Lathe("round structural timber",new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)},32);
            var go=Detail(parent,name,shape,a,new Vector3(radius,(b-a).magnitude,radius),mat); go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        }
        void Jar(Transform parent,Vector3 p,float radius,float height)
        { Detail(parent,"Hollow clay vessel",DetailGeometry.Pot(),p,new Vector3(radius,height,radius),Clay); }
        void Band(Transform parent,Vector3 p,float radius,Material material)
        { Detail(parent,"Rope / hoop binding",DetailGeometry.Ring(),p,Vector3.one*radius,material); }
        void LibraryPlant(Transform parent,string id,Vector3 position,float height,int turn=0)
        {
            var definition=Resources.Load<DecorModelCatalog>("DecorModelCatalog").Get(id);
            var bounds=new Bounds(Vector3.zero,Vector3.zero);
            foreach(var renderer in definition.prefab.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(renderer.bounds);
            var instance=Object.Instantiate(definition.prefab,parent,false); instance.name="Architectural planting · "+id;
            instance.transform.localPosition=position; instance.transform.localScale=Vector3.one*(height/Mathf.Max(.01f,bounds.size.y)); instance.transform.localRotation=Quaternion.Euler(0,turn*73,0);
        }
        void DetailedHut(Transform parent,Vector3 p,float radius,bool stone=false,bool raised=false)
        {
            var house=Node(parent,"Detailed dwelling",LocalSurface(parent,p));
            float floor=raised ? .14f : .025f,height=.40f*radius/.42f;
            var walls=DetailGeometry.Lathe("plastered round walls",new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,.15f),new Vector2(.98f,.9f),new Vector2(.95f,1),new Vector2(0,1)},64);
            Detail(house,"Plastered wall",walls,new Vector3(0,floor,0),new Vector3(radius*.82f,height,radius*.82f),stone ? Stone : Mat("art plaster",new Color(.72f,.60f,.43f)));
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8; var pos=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius*.84f;
                if(raised) Beam(house,"Raised floor pile",pos, pos+Vector3.up*floor,.022f,Wood);
                Beam(house,"Wall timber frame",pos+Vector3.up*floor,pos+Vector3.up*(floor+height),.014f,Wood);
                if(stone) for(int j=0;j<5;j++)
                {
                    float aa=a+(j%2)*Mathf.PI/16; var block=Block(house,"Individual wall stone",new Vector3(Mathf.Cos(aa)*radius*.83f,floor+(j+.5f)*height/5,Mathf.Sin(aa)*radius*.83f),new Vector3(radius*.32f,height/5*.9f,.027f),Stone);
                    block.transform.localRotation=Quaternion.Euler(0,-aa*Mathf.Rad2Deg,0);
                }
            }
            Detail(house,"Layered thatch roof",DetailGeometry.Roof(),new Vector3(0,floor+height-.03f,0),new Vector3(radius,height*.85f,radius),Straw);
            for(int ring=0;ring<4;ring++) Band(house,new Vector3(0,floor+height+ring*height*.18f,0),radius*(.99f-ring*.21f),Rope);
            for(int i=0;i<48;i++)
            {
                float a=i*Mathf.PI/24; var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Beam(house,"Visible roof reed bundle",dir*radius+Vector3.up*(floor+height),dir*radius*.08f+Vector3.up*(floor+height*1.83f),radius*.012f,Straw);
            }
            float doorZ=-radius*.83f;
            Block(house,"Dark doorway recess",new Vector3(0,floor+height*.37f,doorZ-.003f),new Vector3(radius*.42f,height*.74f,.025f),Mat("art doorway",new Color(.12f,.09f,.06f)));
            for(int i=0;i<5;i++) Block(house,"Door plank",new Vector3((i-2)*radius*.066f,floor+height*.35f,doorZ-.027f),new Vector3(radius*.062f,height*.66f,.024f),Wood);
            Beam(house,"Door header",new Vector3(-radius*.23f,floor+height*.75f,doorZ-.04f),new Vector3(radius*.23f,floor+height*.75f,doorZ-.04f),.019f,Wood);
            Round(house,"Door peg",new Vector3(radius*.10f,floor+height*.36f,doorZ-.047f),Vector3.one*.018f,Rope);
            Block(house,"Entry step",new Vector3(0,floor*.4f,doorZ-.12f),new Vector3(radius*.65f,.055f,.18f),Stone);
            for(int i=0;i<3;i++) { Jar(house,new Vector3(radius*.65f+i*.065f,0,-radius*.7f),.04f+.01f*i,.09f+.025f*i); }
        }
        void Table(Transform parent,Vector3 p,float width=.38f)
        {
            Block(parent,"Work table planks",p+Vector3.up*.19f,new Vector3(width,.04f,.24f),Wood);
            for(int i=0;i<4;i++) Beam(parent,"Table leg",p+new Vector3((i%2==0 ? -1 : 1)*width*.4f,0,(i<2 ? -1 : 1)*.09f),p+new Vector3((i%2==0 ? -1 : 1)*width*.4f,.18f,(i<2 ? -1 : 1)*.09f),.018f,Wood);
        }
        void DetailedHearth(Transform parent,Vector3 p,float size=1)
        {
            var hearth=Node(parent,"Detailed stone hearth",p); hearth.localScale=Vector3.one*size;
            for(int i=0;i<16;i++) { float a=i*Mathf.PI/8; Round(hearth,"Hearth stone",new Vector3(Mathf.Cos(a)*.24f,.035f,Mathf.Sin(a)*.24f),new Vector3(.095f,.075f,.08f),Stone); }
            for(int i=0;i<5;i++) { float a=i*Mathf.PI/5; Beam(hearth,"Burning log",new Vector3(Mathf.Cos(a)*-.14f,.06f,Mathf.Sin(a)*-.14f),new Vector3(Mathf.Cos(a)*.14f,.06f,Mathf.Sin(a)*.14f),.025f,Wood); }
            for(int i=0;i<5;i++) { var flame=Round(hearth,"Fire tongue",new Vector3((i-2)*.025f,.12f+Mathf.Abs(i-2)*.018f,0),new Vector3(.035f,.17f-i*.014f,.035f),Mat("art ember",new Color(1,.43f,.07f))); flame.transform.localRotation=Quaternion.Euler(0,0,(i-2)*12); }
            Beam(hearth,"Cooking tripod",new Vector3(-.28f,0,-.10f),new Vector3(0,.52f,0),.016f,Wood);
            Beam(hearth,"Cooking tripod",new Vector3(.28f,0,-.10f),new Vector3(0,.52f,0),.016f,Wood);
            Beam(hearth,"Cooking tripod",new Vector3(0,0,.30f),new Vector3(0,.52f,0),.016f,Wood);
            Jar(hearth,new Vector3(0,.22f,0),.095f,.10f);
            for(int i=0;i<4;i++) Table(hearth,new Vector3((i%2==0 ? -.5f : .5f),0,(i<2 ? -.3f : .3f)),.26f);
        }
        void DetailedBuilding(Transform parent,Vector3 p,string id)
        {
            var site=Node(parent,"Architecture · "+id,LocalSurface(parent,p));
            if(id=="longhouse")
            {
                var prefab=Resources.Load<GameObject>("Buildings/Longhouse");
                if(prefab) Object.Instantiate(prefab,site,false).transform.localScale=Vector3.one*.22f;
                return;
            }
            bool stone=game.Technologies.Contains("masonry");
            if(id=="stonehenge" || id=="pyramids" || id=="gardens") { WonderModel(site,id); return; }
            if(id=="hearth") { DetailedHearth(site,Vector3.zero); return; }
            if(id=="huts") { DetailedHut(site,new Vector3(-.30f,0,.06f),.33f,stone); DetailedHut(site,new Vector3(.32f,0,.18f),.28f,stone); DetailedHearth(site,new Vector3(0,0,-.45f),.4f); return; }
            DetailedHut(site,new Vector3(-.15f,0,.1f),.44f,stone,id=="granary" || id=="fishhut");
            if(id=="granary")
            {
                for(int i=0;i<5;i++)
                {
                    var silo=Node(site,"Woven grain basket",new Vector3(.32f+(i%2)*.2f,0,-.25f+(i/2)*.2f));
                    Detail(silo,"Basket body",DetailGeometry.Pot(),Vector3.zero,new Vector3(.09f,.18f,.09f),Straw);
                    for(int j=0;j<7;j++) Band(silo,Vector3.up*(.02f+j*.024f),.09f*(.75f+Mathf.Sin(j*.4f)*.25f),Rope);
                    for(int j=0;j<6;j++) Beam(silo,"Grain ears",new Vector3((j-3)*.015f,.18f,0),new Vector3((j-3)*.018f,.24f,0),.004f,Straw);
                }
                for(int i=0;i<4;i++) Block(site,"Granary access steps",new Vector3(-.15f,.02f+i*.025f,-.46f+i*.06f),new Vector3(.25f,.035f,.09f),Wood);
            }
            if(id=="workshop")
            {
                Table(site,new Vector3(.40f,0,-.18f));
                for(int i=0;i<8;i++) Round(site,"Worked stone offcut",new Vector3(.2f+(i%4)*.13f,.03f,(i/4)*.1f-.48f),new Vector3(.10f,.07f,.08f),Stone);
                Beam(site,"Hammer handle on bench",new Vector3(.3f,.23f,-.20f),new Vector3(.48f,.23f,-.12f),.008f,Wood);
                Round(site,"Stone hammer head",new Vector3(.48f,.235f,-.12f),new Vector3(.06f,.035f,.07f),Stone);
            }
            if(id=="potter")
            {
                var kiln=Node(site,"Domed clay kiln",new Vector3(.43f,0,.2f));
                Detail(kiln,"Kiln shell",DetailGeometry.Roof(),Vector3.zero,new Vector3(.22f,.32f,.22f),Clay);
                Block(kiln,"Kiln fire opening",new Vector3(0,.10f,-.21f),new Vector3(.13f,.12f,.03f),Mat("art soot",new Color(.1f,.08f,.07f)));
                Table(site,new Vector3(.35f,0,-.36f)); for(int i=0;i<5;i++) Jar(site,new Vector3(.21f+i*.09f,.215f,-.36f),.025f+i*.004f,.06f+i*.01f);
                Detail(site,"Potter wheel",DetailGeometry.Lathe("wheel",new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,.07f),new Vector2(0,.07f)},48),new Vector3(.58f,.11f,-.08f),new Vector3(.13f,1,.13f),Wood); Jar(site,new Vector3(.58f,.18f,-.08f),.05f,.10f);
            }
            if(id=="fishhut")
            {
                for(int i=0;i<12;i++) Block(site,"Dock floor board",new Vector3(.37f, .06f,(i-5.5f)*.07f),new Vector3(.40f,.045f,.06f),Wood);
                for(int i=0;i<4;i++) Beam(site,"Dock pile",new Vector3(.2f+(i%2)*.33f,-.05f,(i<2 ? -.38f : .38f)),new Vector3(.2f+(i%2)*.33f,.25f,(i<2 ? -.38f : .38f)),.025f,Wood);
                Beam(site,"Net rack",new Vector3(.2f,.45f,.38f),new Vector3(.58f,.45f,.38f),.013f,Wood);
                for(int i=0;i<10;i++) { float x=.2f+i*.04f; Beam(site,"Hanging net strand",new Vector3(x,.44f,.38f),new Vector3(x,.19f,.38f),.0017f,Rope); }
                for(int i=0;i<7;i++) Beam(site,"Net crossing strand",new Vector3(.2f,.19f+i*.036f,.38f),new Vector3(.58f,.19f+i*.036f,.38f),.0017f,Rope);
                Jar(site,new Vector3(.38f,.08f,-.24f),.075f,.1f);
            }
        }
        void WonderModel(Transform site,string id)
        {
            if(id=="stonehenge")
            {
                for(int i=0;i<12;i++)
                {
                    float a=i*Mathf.PI/6; var pos=new Vector3(Mathf.Cos(a)*.65f,0,Mathf.Sin(a)*.65f);
                    var monument=Node(site,"Megalith pair "+i,pos); monument.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);
                    for(int leg=0;leg<2;leg++) { var pillar=Round(monument,"Weathered standing stone",new Vector3(0,.40f,(leg==0 ? -.1f : .1f)),new Vector3(.16f,.83f,.15f),Stone); pillar.transform.localRotation=Quaternion.Euler(3,0,leg==0 ? -2 : 2); }
                    Round(monument,"Stone lintel",new Vector3(0,.82f,0),new Vector3(.2f,.14f,.42f),Stone);
                }
                for(int i=0;i<5;i++) { float a=i*Mathf.PI*2/5; Round(site,"Inner ritual stone",new Vector3(Mathf.Cos(a)*.28f,.23f,Mathf.Sin(a)*.28f),new Vector3(.10f,.5f,.11f),Stone); }
                Block(site,"Central altar",new Vector3(0,.10f,0),new Vector3(.27f,.16f,.22f),Stone);
                for(int i=0;i<24;i++) { float a=i*Mathf.PI/12; Round(site,"Outer marker",new Vector3(Mathf.Cos(a)*.87f,.015f,Mathf.Sin(a)*.87f),new Vector3(.05f,.035f,.045f),Stone); }
            }
            else if(id=="pyramids")
            {
                Pyramid(site,new Vector3(-.2f,0,.12f),.65f,1.05f,18); Pyramid(site,new Vector3(.54f,0,.37f),.25f,.40f,8); Pyramid(site,new Vector3(.35f,0,-.53f),.18f,.29f,6);
                for(int i=0;i<16;i++) Block(site,"Ceremonial avenue slab",new Vector3(-.2f,.016f,-.72f+i*.026f),new Vector3(.25f,.025f,.022f),Stone);
            }
            else
            {
                var leaf=Mat("art garden foliage",new Color(.24f,.45f,.18f));
                for(int tier=0;tier<4;tier++)
                {
                    float radius=.82f-tier*.16f,y=tier*.27f;
                    Block(site,"Garden terrace masonry",new Vector3(0,y+.10f,0),new Vector3(radius*2,.2f,radius*2),Stone);
                    for(int side=0;side<4;side++) for(int i=0;i<8;i++)
                    {
                        var pivot=Node(site,"Terrace edge detail",Vector3.up*y); pivot.localRotation=Quaternion.Euler(0,side*90,0);
                        float x=(i-3.5f)*radius*.22f;
                        Beam(pivot,"Arcade column",new Vector3(x,.05f,-radius),new Vector3(x,.24f,-radius),.021f,Stone);
                        LibraryPlant(pivot,"shrub",new Vector3(x,.23f,-radius+.07f),.12f,i+side*3+tier);
                        for(int vine=0;vine<3;vine++) Beam(pivot,"Hanging vine",new Vector3(x+vine*.025f,.26f,-radius-.014f),new Vector3(x+vine*.025f,.08f,-radius-.014f),.004f,leaf);
                    }
                    for(int i=0;i<4;i++) { float a=i*Mathf.PI/2; LibraryPlant(site,"oak_"+(i%3+1),new Vector3(Mathf.Cos(a)*radius*.65f,y+.22f,Mathf.Sin(a)*radius*.65f),.35f,i+tier); }
                }
                for(int i=0;i<18;i++) Block(site,"Garden stair",new Vector3(0,.025f+i*.026f,-.95f+i*.023f),new Vector3(.22f,.04f,.035f),Stone);
                Block(site,"Irrigation canal",new Vector3(.48f,.01f,-.83f),new Vector3(.075f,.018f,.32f),Mat("art garden water",new Color(.22f,.53f,.62f)));
            }
        }
        void Pyramid(Transform root,Vector3 p,float radius,float height,int courses)
        {
            var limestone=Mat("art pyramid sandstone",new Color(.84f,.73f,.49f));
            for(int row=0;row<courses;row++)
            {
                float r=radius*(1-(float)row/courses),y=(row+.5f)*height/courses;
                Block(root,"Pyramid core course",p+new Vector3(0,y,0),new Vector3(r*2,height/courses,r*2),limestone);
                int blocks=Mathf.Max(2,Mathf.CeilToInt(r*16));
                for(int side=0;side<4;side++) for(int i=0;i<blocks;i++)
                {
                    var q=new Vector3(-r+(i+.5f)*2*r/blocks,y,-r-.003f); q=Quaternion.Euler(0,side*90,0)*q;
                    var block=Block(root,"Individual limestone facing block",p+q,new Vector3(2*r/blocks*.96f,height/courses*.9f,.025f),limestone); block.transform.localRotation=Quaternion.Euler(0,side*90,0);
                }
            }
            Block(root,"Pyramid entrance",p+new Vector3(0,.09f,-radius-.018f),new Vector3(.12f,.16f,.03f),Mat("art doorway",new Color(.12f,.09f,.06f)));
        }
    }
}
