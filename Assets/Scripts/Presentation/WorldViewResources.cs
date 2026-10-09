using UnityEngine;

namespace Origins
{
    public sealed partial class WorldView
    {
        public Transform CreateResourcePreview(Resource kind)
        {
            var model=Node(root,"Resource · "+kind,Vector3.zero);
            switch(kind)
            {
                case Resource.Berries: BerryBush(model,Vector3.zero,false); break;
                case Resource.Grain: for(int i=0;i<7;i++) Wheat(model,new Vector3((i%3-1)*.12f,0,(i/3-1)*.12f)); break;
                case Resource.Stone: for(int i=0;i<3;i++) Rock(model,new Vector3((i-1)*.20f,0,0),.25f); break;
                case Resource.Discovery: AncientSite(model); break;
                case Resource.Fish: for(int i=0;i<3;i++) DetailedFish(model,new Vector3((i-1)*.22f,.08f,(i%2)*.18f)); break;
                case Resource.Cattle: for(int i=0;i<3;i++) DetailedAnimal(model,new Vector3((i-1)*.28f,0,(i%2)*.20f),true); break;
                default: DetailedAnimal(model,Vector3.zero,false); break;
            }
            return model;
        }
        void DetailedFish(Transform parent,Vector3 p)
        {
            var fish=NaturalRoot(parent,p,"Detailed freshwater fish");
            var silver=Mat("fish silver scales",new Color(.47f,.61f,.57f));
            Round(fish,"Tapered fish body",Vector3.zero,new Vector3(.17f,.065f,.05f),silver);
            Round(fish,"Fish head",new Vector3(.06f,0,0),new Vector3(.064f,.06f,.048f),silver);
            for(int side=0;side<2;side++)
            {
                float s=side==0 ? -1 : 1;
                Round(fish,"Fish eye",new Vector3(.075f,.012f,s*.024f),Vector3.one*.010f,Wood);
                var fin=Detail(fish,"Tail fin",DetailGeometry.Leaf(),new Vector3(-.07f,0,0),new Vector3(.023f,.06f,.015f),silver); fin.transform.localRotation=Quaternion.Euler(0,0,side==0 ? 40 : 140);
                for(int gill=0;gill<3;gill++) Beam(fish,"Gill cover",new Vector3(.025f-gill*.006f,-.019f,s*.02f),new Vector3(.023f-gill*.006f,.019f,s*.02f),.0012f,Wood);
            }
            for(int i=0;i<6;i++) Beam(fish,"Dorsal fin ray",new Vector3(-.035f+i*.009f,.024f,0),new Vector3(-.05f+i*.010f,.048f,0),.0018f,silver);
        }
        void BerryBush(Transform parent,Vector3 p,bool variation=true)
        {
            var bush=variation ? NaturalRoot(parent,p,"Leafy berry bush") : Node(parent,"Leafy berry bush",p);
            LibraryPlant(bush,"shrub",Vector3.zero,.24f);
            var berry=Mat("ripe berries",new Color(.58f,.10f,.13f));
            for(int branch=0;branch<5;branch++)
            {
                float a=branch*Mathf.PI*2/5;
                var tip=new Vector3(Mathf.Cos(a)*.09f,.13f+(branch%2)*.045f,Mathf.Sin(a)*.09f);
                Beam(bush,"Woody berry branch",new Vector3(0,.02f,0),tip,.003f,Wood);
                for(int fruit=0;fruit<4;fruit++) Round(bush,"Individual ripe berry",tip+new Vector3((fruit%2)*.014f,(fruit/2)*.014f,0),Vector3.one*.021f,berry);
            }
        }
        void Wheat(Transform parent,Vector3 p,bool variation=true)
        {
            var stem=variation ? NaturalRoot(parent,p,"Wheat with leaves and grain") : Node(parent,"Wheat with leaves and grain",p);
            var gold=Mat("wheat ears",new Color(.79f,.62f,.24f));
            Beam(stem,"Bent wheat stalk",Vector3.zero,new Vector3(.01f,.16f,0),.003f,Straw);
            Beam(stem,"Wheat stalk tip",new Vector3(.01f,.16f,0),new Vector3(.035f,.28f,0),.0025f,Straw);
            for(int leaf=0;leaf<3;leaf++)
            {
                var blade=Detail(stem,"Curved wheat leaf",DetailGeometry.Leaf(),new Vector3(.006f,.045f+leaf*.04f,0),new Vector3(.032f,.10f,.032f),gold);
                blade.transform.localRotation=Quaternion.Euler(15,leaf*137,55);
            }
            for(int grain=0;grain<12;grain++)
            {
                float side=grain%2==0 ? -1 : 1,y=.20f+(grain/2)*.013f;
                var kernel=Round(stem,"Grain kernel",new Vector3(.02f+side*.011f,y,0),new Vector3(.016f,.025f,.012f),gold);
                kernel.transform.localRotation=Quaternion.Euler(0,0,-side*25);
                Beam(stem,"Fine wheat awn",new Vector3(.02f+side*.017f,y+.009f,0),new Vector3(.02f+side*.030f,y+.042f,0),.00065f,gold);
            }
        }
        void AncientSite(Transform parent)
        {
            var site=NaturalRoot(parent,Vector3.zero,"Weathered ancient site");
            for(int pillar=0;pillar<3;pillar++)
            {
                float height=.22f+pillar*.09f;
                var stone=Detail(site,"Weathered standing stone",DetailGeometry.WeatheredStone(pillar),new Vector3((pillar-1)*.25f,height*.5f,0),new Vector3(.14f,height,.17f),Stone);
                stone.transform.localRotation=Quaternion.Euler(pillar*5,17*pillar,4-pillar*5);
                for(int mark=0;mark<3;mark++) Beam(site,"Ancient stone carving",new Vector3((pillar-1)*.25f-.028f,.07f+mark*.045f,.088f),new Vector3((pillar-1)*.25f+.028f,.085f+mark*.045f,.088f),.003f,Wood);
            }
            for(int rubble=0;rubble<5;rubble++) Rock(site,new Vector3(Range(-.40f,.4f),0,Range(-.3f,.3f)),.08f+Range(0,.07f));
            LibraryPlant(site,"moss",new Vector3(.13f,0,.05f),.04f);
        }
    }
}
