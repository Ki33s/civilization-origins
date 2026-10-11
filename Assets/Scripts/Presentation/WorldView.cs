using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Origins
{
    public sealed class TileView : MonoBehaviour { public Tile Tile; }

    public sealed partial class WorldView
    {
        readonly Game game;
        readonly Transform root;
        readonly Dictionary<Hex,Transform> tiles=new Dictionary<Hex,Transform>();
        readonly Dictionary<Hex,GameObject> details=new Dictionary<Hex,GameObject>();
        readonly Dictionary<int,Transform> units=new Dictionary<int,Transform>();
        readonly Dictionary<int,Transform> cities=new Dictionary<int,Transform>();
        readonly Dictionary<int,int> cityVersions=new Dictionary<int,int>();
        readonly List<Citizen> citizens=new List<Citizen>();
        readonly List<Animal> animals=new List<Animal>();
        readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        readonly List<GameObject> orders=new List<GameObject>();
        readonly Mesh hexMesh;
        readonly Material fog;
        readonly System.Random random;
        readonly NaturalDecorCatalog decorCatalog;
        readonly bool displayResources;
        Transform selection;
        sealed class Citizen
        {
            public Transform Body;
            public ArticulatedActor Rig;
            public Vector3 Home, Work, Rest, Target, Ground;
            public int Stage;
            public float Wait, Speed, Offset;
        }
        sealed class Animal { public Transform Body; public Vector3 Home; public float Offset; }

        public WorldView(Game model,Transform parent,bool buildWorld=true,bool showResourceModels=true)
        {
            game=model; root=parent; random=new System.Random(model.World.Seed^0x7891);
            displayResources=showResourceModels;
            decorCatalog=Resources.Load<NaturalDecorCatalog>("NaturalDecorCatalog");
            if(!buildWorld) return;
            terrainCatalog=Resources.Load<WorldTerrainCatalog>("WorldTerrainCatalog");
            if(!terrainCatalog) throw new InvalidOperationException("Prepare the world terrain library before playing");
            editableDecor=new TerrainDecorRenderer(root);
            foreach(var tile in game.World.Ordered) tileRotations.Add(tile.Hex,TileRotation(tile));
            hexMesh=HexMesh(); fog=Mat("fog",new Color(.075f,.12f,.16f));
            foreach(var tile in game.World.Ordered) CreateTile(tile);
            BuildSmoothRivers();
            selection=Ring(root,Vector3.zero,1.04f,new Color(.96f,.77f,.39f),"Selection").transform;
            selection.gameObject.SetActive(false);
            Sync();
        }
        public Vector3 Position(Hex h)
        {
            Vector3 p=FlatPosition(h); return OnSurface(p);
        }
        public Transform UnitTransform(Unit u) { Transform t; return units.TryGetValue(u.Id,out t) ? t : null; }
        public Transform UnitTransform(int id) { Transform t; return units.TryGetValue(id,out t) ? t : null; }
        public void Select(Hex? hex)
        {
            selection.gameObject.SetActive(hex.HasValue);
            if(hex.HasValue) selection.position=Position(hex.Value)+Vector3.up*.055f;
        }
        Material Mat(string key,Color color)
        {
            Material m;
            if(materials.TryGetValue(key,out m)) return m;
            m=new Material(Resources.Load<Material>("WorldSurface")); m.color=color;
            m.SetFloat("_Glossiness",.08f); m.enableInstancing=true; materials.Add(key,m); return m;
        }
        Color TerrainColor(Terrain t)
        {
            switch(t)
            {
                case Terrain.Ocean: return new Color(.13f,.34f,.46f);
                case Terrain.Coast: return new Color(.22f,.48f,.57f);
                case Terrain.Forest: return new Color(.27f,.43f,.29f);
                case Terrain.Hills: return new Color(.5f,.52f,.34f);
                case Terrain.Mountain: return new Color(.54f,.56f,.54f);
                case Terrain.Desert: return new Color(.73f,.63f,.41f);
                default: return new Color(.44f,.59f,.35f);
            }
        }
        void CreateTile(Tile tile)
        {
            var go=new GameObject("Hex "+tile.Hex); go.transform.SetParent(root,false);
            go.transform.position=FlatPosition(tile.Hex);
            go.AddComponent<MeshFilter>().sharedMesh=hexMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=fog;
            go.AddComponent<MeshCollider>().sharedMesh=hexMesh;
            go.AddComponent<TileView>().Tile=tile;
            tiles.Add(tile.Hex,go.transform);
            var props=new GameObject("Landscape"); props.transform.SetParent(go.transform,false);
            details.Add(tile.Hex,props);
            CreateArtwork(tile,props.transform);
            if(!displayResources) return;
            switch(tile.Resource)
            {
                case Resource.Berries:
                    for(int i=0;i<3;i++) BerryBush(props.transform,new Vector3(Range(-.45f,.45f),0,Range(-.4f,.4f)));
                    break;
                case Resource.Grain:
                    for(int i=0;i<7;i++) Wheat(props.transform,new Vector3(Range(-.4f,.4f),0,Range(-.4f,.4f)));
                    break;
                case Resource.Stone: for(int i=0;i<3;i++) Rock(props.transform,new Vector3(Range(-.4f,.4f),.08f,Range(-.4f,.4f)),.25f); break;
                case Resource.Discovery:
                    AncientSite(props.transform);
                    break;
                case Resource.Game:
                case Resource.Furs:
                    var animal=DetailedAnimal(props.transform,LocalSurface(props.transform,Vector3.zero),false);
                    NaturalModelVariation.Sample(random).Apply(animal); break;
                case Resource.Cattle:
                    for(int i=0;i<3;i++) NaturalModelVariation.Sample(random).Apply(DetailedAnimal(props.transform,LocalSurface(props.transform,new Vector3((i-1)*.24f,0,(i%2)*.25f)),true));
                    break;
            }
        }
        public void Sync()
        {
            SyncRoads();
            foreach(var tile in game.World.Ordered)
            {
                var mr=tiles[tile.Hex].GetComponent<MeshRenderer>();
                mr.enabled=!tile.Explored;
                details[tile.Hex].SetActive(tile.Explored);
            }
            foreach(var city in game.Cities)
            {
                int previous=game.ActivePlayer; game.SetPlayer(city.Owner);
                int version=CityVersion(city);
                if(!cities.ContainsKey(city.Id) || cityVersions[city.Id]!=version) BuildCity(city);
                game.SetPlayer(previous);
                cities[city.Id].gameObject.SetActive(city.Owner==game.ActivePlayer || game.IsExplored(game.ActivePlayer,city.Position));
            }
            var live=new HashSet<int>();
            foreach(var unit in game.Units)
            {
                live.Add(unit.Id);
                if(!units.ContainsKey(unit.Id)) { int previous=game.ActivePlayer; game.SetPlayer(unit.Owner); BuildUnit(unit); game.SetPlayer(previous); }
                units[unit.Id].position=Position(unit.Position)+Vector3.up*.05f;
                units[unit.Id].gameObject.SetActive(unit.Owner==game.ActivePlayer || game.IsExplored(game.ActivePlayer,unit.Position));
            }
            var remove=new List<int>();
            foreach(var pair in units) if(!live.Contains(pair.Key)) { Object.Destroy(pair.Value.gameObject); remove.Add(pair.Key); }
            foreach(int id in remove) units.Remove(id);
        }
        int CityVersion(City city)
        {
            int version=city.Population*100+city.Buildings.Count*17+city.Improvements.Count*19+city.Territory.Count*23+game.ToolStage*7919+(game.Technologies.Contains("masonry") ? 127 : 0);
            foreach(var worker in city.Workers) version=unchecked(version*31+worker.Position.GetHashCode()+(worker.BuildingId==null ? 0 : worker.BuildingId.GetHashCode()));
            return version;
        }
        void BuildCity(City city)
        {
            Transform old;
            if(cities.TryGetValue(city.Id,out old))
            {
                citizens.RemoveAll(c=>c.Body==null || c.Body.IsChildOf(old));
                old.gameObject.SetActive(false);
                foreach(var f in old.GetComponentsInChildren<MeshFilter>()) if(f.gameObject.name=="Low poly cone") Object.Destroy(f.sharedMesh);
                Object.Destroy(old.gameObject);
            }
            var go=new GameObject(city.Name); go.transform.SetParent(root,false); go.transform.position=Position(city.Position);
            cities[city.Id]=go.transform; cityVersions[city.Id]=CityVersion(city);
            int houses=Math.Min(4,Math.Max(1,(city.Population+1)/2));
            for(int i=0;i<houses;i++)
            {
                float angle=i*Mathf.PI*2/houses; Vector3 p=new Vector3(Mathf.Cos(angle)*.6f,0,Mathf.Sin(angle)*.6f);
                Hut(go.transform,p,.32f);
            }
            var fire=Primitive(go.transform,PrimitiveType.Sphere,new Vector3(0,.08f,0),new Vector3(.22f,.12f,.22f),Mat("fire",new Color(1,.46f,.14f)));
            var light=fire.AddComponent<Light>(); light.color=new Color(1,.57f,.24f); light.range=2.5f; light.intensity=1.1f; light.shadows=LightShadows.None;
            Color tribeColor=city.Owner==0 ? new Color(.9f,.73f,.35f) : new Color(.36f,.67f,.91f);
            Ring(go.transform,new Vector3(0,.022f,0),.89f,tribeColor,"Village border");
            Primitive(go.transform,PrimitiveType.Cylinder,new Vector3(.2f,.6f,0),new Vector3(.035f,.6f,.035f),Mat("wood",new Color(.34f,.22f,.12f)));
            Primitive(go.transform,PrimitiveType.Cube,new Vector3(.37f,1.02f,0),new Vector3(.34f,.2f,.025f),Mat("banner"+city.Owner,tribeColor));
            foreach(var hex in city.Territory)
            {
                var border=new GameObject("Owned hex"); border.transform.SetParent(go.transform,false);
                border.transform.localPosition=Position(hex)-go.transform.position+Vector3.up*.035f;
                var line=border.AddComponent<LineRenderer>(); line.useWorldSpace=false; line.loop=true; line.positionCount=6; line.startWidth=line.endWidth=.028f;
                line.sharedMaterial=Mat("territory"+city.Owner,tribeColor);
                for(int corner=0;corner<6;corner++)
                { float a=(30+corner*60)*Mathf.Deg2Rad; var p=FlatPosition(hex)+new Vector3(Mathf.Cos(a)*.985f,0,Mathf.Sin(a)*.985f); line.SetPosition(corner,OnSurface(p)+Vector3.up*.018f-border.transform.position); }
            }
            foreach(var site in city.BuildingSites)
            {
                Vector3 p=Position(site.Value)-go.transform.position;
                BuildingModel(go.transform,p,site.Key);
            }
            foreach(var improvement in city.Improvements)
            {
                Vector3 p=Position(improvement.Key)-go.transform.position;
                if(improvement.Value=="farming")
                {
                    Block(go.transform,"Cultivated soil",p+Vector3.up*.025f,new Vector3(1.1f,.035f,.9f),Mat("soil",new Color(.4f,.27f,.15f)));
                    for(int row=0;row<5;row++) for(int stem=0;stem<6;stem++)
                        Wheat(go.transform,p+new Vector3((stem-2.5f)*.16f,.04f,(row-2)*.15f));
                }
                else if(improvement.Value=="stone")
                {
                    for(int stone=0;stone<4;stone++) Rock(go.transform,p+new Vector3((stone%2)*.3f-.15f,.12f,(stone/2)*.3f-.15f),.4f);
                    Primitive(go.transform,PrimitiveType.Cube,p+new Vector3(.52f,.18f,0),new Vector3(.38f,.18f,.2f),Mat("bench",Color.gray));
                }
                else if(improvement.Value!="domestication") Hut(go.transform,p+new Vector3(.4f,0,.3f),.2f);
            }
            for(int i=0;i<city.Population;i++)
            {
                var body=new GameObject("Житель: дом / работа / отдых"); body.transform.SetParent(go.transform,false);
                float angle=i*Mathf.PI*2/city.Population;
                Vector3 home=new Vector3(Mathf.Cos(angle)*.48f,0,Mathf.Sin(angle)*.48f);
                ResidentJob assignment=null; foreach(var job in city.Workers) if(job.ResidentId==i) assignment=job;
                ResidentModel(body.transform,assignment==null ? "Собиратель" : assignment.Profession);
                body.transform.localPosition=home;
                Vector3 work=assignment==null ? home : Position(assignment.Position)-go.transform.position+new Vector3(-.05f,0,-.20f);
                if(assignment!=null)
                {
                    var context=Node(go.transform,"Worker activity · "+assignment.Profession,LocalSurface(go.transform,work));
                    WorkContext(context,assignment.Profession,game.ToolStage);
                }
                if(assignment!=null) Ring(go.transform,work+Vector3.up*.04f,.3f,new Color(.57f,.85f,.44f),"Working resident");
                var citizen=new Citizen{Body=body.transform,Rig=body.GetComponentInChildren<ArticulatedActor>(),Home=home,Work=work,Rest=new Vector3(Mathf.Cos(angle)*.21f,0,Mathf.Sin(angle)*.21f),Ground=home,Stage=0,Wait=i*.5f,Speed=.45f,Offset=Range(0,20)};
                citizen.Target=citizen.Stage==0 ? citizen.Work : citizen.Stage==1 ? citizen.Rest : citizen.Home;
                citizens.Add(citizen);
            }
        }
        void BuildUnit(Unit unit)
        {
            var go=new GameObject(unit.Name); go.transform.SetParent(root,false); units[unit.Id]=go.transform;
            Color color=unit.Kind==UnitKind.Scout ? new Color(.32f,.65f,.68f) : unit.Kind==UnitKind.Hunter || unit.Kind==UnitKind.Slinger ? new Color(.72f,.40f,.27f) : new Color(.76f,.66f,.37f);
            if(game.Multiplayer) color=unit.Owner==0 ? new Color(.85f,.66f,.28f) : new Color(.32f,.57f,.88f);
            string role=unit.Kind==UnitKind.Trader ? "Торговец" : unit.Kind==UnitKind.Slinger ? "Пращник" : unit.Kind==UnitKind.Hunter ? "Охотник" : unit.Kind==UnitKind.Warrior ? "Воин" : unit.Kind==UnitKind.Scout ? "Разведчик" : "Поселенец";
            int members=unit.Kind==UnitKind.Trader ? 1 : 3;
            for(int i=0;i<members;i++)
            {
                var member=Node(go.transform,"Member",members==1 ? Vector3.zero : new Vector3((i-1)*.19f,0,i==1 ? .1f : -.08f));
                if(unit.Kind==UnitKind.Spearman) SpearmanVisual.Create(member,color);
                else DetailedPerson(member,color,1,role,game.ToolStage);
                if(unit.Kind==UnitKind.Settler) { Round(member,"Travel pack",new Vector3(0,.25f,-.065f),new Vector3(.12f,.16f,.07f),Straw); }
                if(unit.Kind==UnitKind.Trader)
                {
                    Round(member,"Merchant goods sack",new Vector3(0,.24f,-.075f),new Vector3(.13f,.17f,.09f),Straw);
                    for(int tie=0;tie<3;tie++) Band(member,new Vector3(0,.18f+tie*.035f,-.075f),.052f,Rope);
                    Beam(member,"Pack carrying strap",new Vector3(-.065f,.17f,-.04f),new Vector3(-.065f,.30f,.034f),.006f,Rope);
                    Jar(member,new Vector3(.10f,.08f,-.055f),.03f,.065f);
                }
            }
            Ring(go.transform,new Vector3(0,.025f,0),.39f,color,"Unit base");
            if(unit.Kind==UnitKind.Trader) return;
            Beam(go.transform,"Carved standard pole",new Vector3(0,0,-.16f),new Vector3(0,.70f,-.16f),.011f,Wood);
            for(int panel=0;panel<7;panel++) Block(go.transform,"Stitched standard cloth",new Vector3(.015f+panel*.027f,.65f,-.16f),new Vector3(.027f,.135f,.008f),Mat("unitflag"+unit.Kind+unit.Owner,color));
            Band(go.transform,new Vector3(0,.56f,-.16f),.014f,Rope); Band(go.transform,new Vector3(0,.73f,-.16f),.014f,Rope);
        }
        void Person(Transform parent,Color color,float size)
        { DetailedPerson(parent,color,size); }
        void BuildingModel(Transform parent,Vector3 p,string id)
        { DetailedBuilding(parent,p,id); }
        void ResidentModel(Transform parent,string profession,int stage=-1)
        {
            Color outfit=profession=="Земледелец" ? new Color(.58f,.69f,.27f) : profession=="Рыбак" ? new Color(.3f,.63f,.72f) : profession=="Каменотёс" ? new Color(.57f,.62f,.68f) : profession=="Гончар" ? new Color(.74f,.43f,.27f) : new Color(.84f,.62f,.34f);
            DetailedPerson(parent,outfit,1.15f,profession,stage<0 ? game.ToolStage : stage);
        }
        public Transform CreateUnitPreview(UnitKind kind)
        {
            var unit=new Unit { Id=-(int)kind-1,Kind=kind }; BuildUnit(unit); return units[unit.Id];
        }
        public Transform CreateBuildingPreview(string id)
        {
            var go=new GameObject(id); go.transform.SetParent(root,false); BuildingModel(go.transform,Vector3.zero,id); return go.transform;
        }
        public Transform CreateResidentPreview(string profession,int stage=-1)
        {
            var go=new GameObject(profession); go.transform.SetParent(root,false);
            var actor=new GameObject("Resident actor").transform; actor.SetParent(go.transform,false); ResidentModel(actor,profession,stage);
            WorkContext(go.transform,profession,stage<0 ? game.ToolStage : stage);
            return go.transform;
        }
        public void AddPreviewPlinth(Transform model)
        {
            var go=new GameObject("Preview hex"); go.transform.SetParent(model,false); go.transform.localPosition=Vector3.down*.03f;
            go.AddComponent<MeshFilter>().sharedMesh=HexMesh(); go.AddComponent<MeshRenderer>().sharedMaterial=Mat("previewground",new Color(.34f,.39f,.30f));
        }
        void Hut(Transform parent,Vector3 p,float radius)
        { DetailedHut(parent,p,radius,game.Technologies.Contains("masonry")); }
        void Tree(Transform parent,Vector3 p,float size)
        {
            Transform tree=NaturalRoot(parent,p,"Tree"); tree.localScale*=size;
            Primitive(tree,PrimitiveType.Cylinder,Vector3.up*.25f,new Vector3(.065f,.25f,.065f),Mat("wood",Color.gray));
            Cone(tree,Vector3.up*.18f,.3f,.8f,Mat("leaves",new Color(.16f,.32f,.2f)));
            Cone(tree,Vector3.up*.48f,.23f,.63f,Mat("leavesTop",new Color(.23f,.4f,.25f)));
        }
        Transform NaturalRoot(Transform parent,Vector3 position,string name)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.localPosition=LocalSurface(parent,position);
            NaturalModelVariation.Sample(random).Apply(go.transform); return go.transform;
        }
        void CatalogModel(Transform parent,Mesh mesh,Vector3 position,float baseScale,string name)
        {
            var model=NaturalRoot(parent,position,name); model.localScale*=baseScale;
            model.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            model.gameObject.AddComponent<MeshRenderer>().sharedMaterial=decorCatalog.surface;
        }
        void Rock(Transform p,Vector3 position,float size)
        {
            position.y=0;
            if(decorCatalog && decorCatalog.stones!=null && decorCatalog.stones.Length>0)
            {
                Mesh mesh=decorCatalog.stones[random.Next(decorCatalog.stones.Length)];
                CatalogModel(p,mesh,position,size/Mathf.Max(mesh.bounds.size.x,mesh.bounds.size.z),"Varied stone");
            }
            else Primitive(NaturalRoot(p,position,"Stone"),PrimitiveType.Sphere,Vector3.up*size*.2f,new Vector3(size,size*.7f,size),Mat("rock",Color.gray));
        }
        GameObject Primitive(Transform parent,PrimitiveType type,Vector3 p,Vector3 scale,Material mat)
        {
            var go=GameObject.CreatePrimitive(type); go.transform.SetParent(parent,false); go.transform.localPosition=p; go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();
            if(Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
            go.GetComponent<Renderer>().sharedMaterial=mat; return go;
        }
        GameObject Cone(Transform parent,Vector3 position,float radius,float height,Material mat)
        {
            const int sides=7;
            var vertices=new List<Vector3>(); var indices=new List<int>();
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                int k=vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));
                vertices.Add(Vector3.up*height);
                vertices.Add(new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius));
                indices.Add(k); indices.Add(k+1); indices.Add(k+2);
            }
            var mesh=new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(indices,0); mesh.RecalculateNormals();
            var go=new GameObject("Low poly cone"); go.transform.SetParent(parent,false); go.transform.localPosition=position;
            go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=mat; return go;
        }
        static Mesh HexMesh()
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            for(int i=0;i<6;i++)
            {
                float a=(30+i*60)*Mathf.Deg2Rad,b=(30+(i+1)*60)*Mathf.Deg2Rad;
                Vector3 p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),q=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                int k=vertices.Count;
                vertices.Add(Vector3.zero); vertices.Add(q); vertices.Add(p);
                triangles.Add(k); triangles.Add(k+1); triangles.Add(k+2);
                k=vertices.Count;
                vertices.Add(p); vertices.Add(q); vertices.Add(q-Vector3.up*.28f); vertices.Add(p-Vector3.up*.28f);
                triangles.Add(k); triangles.Add(k+1); triangles.Add(k+2); triangles.Add(k); triangles.Add(k+2); triangles.Add(k+3);
            }
            var mesh=new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); return mesh;
        }
        GameObject Ring(Transform parent,Vector3 position,float radius,Color color,string name)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.localPosition=position;
            var line=go.AddComponent<LineRenderer>(); line.useWorldSpace=false; line.loop=true; line.positionCount=48; line.startWidth=line.endWidth=.027f;
            line.sharedMaterial=Mat("ring"+color,color);
            for(int i=0;i<48;i++) { float a=i*Mathf.PI*2/48; line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius)); }
            return go;
        }
        GameObject DrawLine(Transform parent,Vector3 a,Vector3 b,float width,Material mat)
        {
            var go=new GameObject("Line"); go.transform.SetParent(parent,false);
            var line=go.AddComponent<LineRenderer>(); line.useWorldSpace=true;
            int segments=terrainCatalog ? Mathf.Max(2,Mathf.CeilToInt(Vector3.Distance(a,b)*12)) : 1;
            line.positionCount=segments+1;
            float lift=terrainCatalog ? Mathf.Max(.018f,a.y-OnSurface(a).y) : 0;
            for(int i=0;i<=segments;i++) { Vector3 p=Vector3.Lerp(a,b,i/(float)segments); line.SetPosition(i,terrainCatalog ? OnSurface(p)+Vector3.up*lift : p); }
            line.startWidth=line.endWidth=width; line.sharedMaterial=mat; return go;
        }
        public void ShowOrders(Unit selected)
        {
            foreach(var go in orders) Object.Destroy(go); orders.Clear();
            if(game.Phase!=Phase.Planning) return;
            if(selected!=null) foreach(var h in game.World.Reachable(selected.Position,game.Movement(selected)).Keys)
                orders.Add(Ring(root,Position(h)+Vector3.up*.045f,.86f,new Color(.38f,.67f,.66f),"Reachable"));
            foreach(var u in game.OwnUnits) for(int i=1;i<u.Order.Count;i++)
                orders.Add(DrawLine(root,Position(u.Order[i-1])+Vector3.up*.12f,Position(u.Order[i])+Vector3.up*.12f,.06f,Mat("order",new Color(1,.78f,.34f))));
        }
        public void Animate(float time,float delta)
        {
            foreach(var citizen in citizens)
            {
                if(citizen.Body==null) continue;
                citizen.Wait-=delta;
                if(citizen.Wait>0)
                {
                    if(citizen.Stage==1) citizen.Body.localRotation=Quaternion.identity;
                    citizen.Rig.Pose(time+citizen.Offset,false,citizen.Stage==1,citizen.Stage!=1); continue;
                }
                Vector3 p=citizen.Ground, target=citizen.Target;
                Vector3 next=Vector3.MoveTowards(p,target,citizen.Speed*delta);
                Vector3 direction=target-p; direction.y=0;
                if(direction.sqrMagnitude>.001f) citizen.Body.localRotation=Quaternion.LookRotation(direction);
                citizen.Ground=next;
                citizen.Rig.Pose(time+citizen.Offset,true,false);
                citizen.Body.localPosition=LocalSurface(citizen.Body.parent,next)+Vector3.up*(Mathf.Abs(Mathf.Sin(time*10+citizen.Offset))*.018f);
                if(Vector3.Distance(next,target)<.02f)
                {
                    citizen.Wait=citizen.Stage==0 ? 2 : citizen.Stage==1 ? 5 : 3;
                    citizen.Stage=(citizen.Stage+1)%3;
                    citizen.Target=citizen.Stage==0 ? citizen.Work : citizen.Stage==1 ? citizen.Rest : citizen.Home;
                }
            }
            foreach(var pair in units)
            {
                Vector3 last; bool walking=unitPrevious.TryGetValue(pair.Key,out last) && (last-pair.Value.position).sqrMagnitude>.000001f;
                foreach(var rig in pair.Value.GetComponentsInChildren<ArticulatedActor>()) rig.Pose(time+pair.Key*.3f,walking,false);
                foreach(var actor in pair.Value.GetComponentsInChildren<SpearmanVisual>()) actor.SetWalking(walking);
                unitPrevious[pair.Key]=pair.Value.position;
            }
            foreach(var a in animals)
            {
                if(!a.Body.gameObject.activeInHierarchy) continue;
                float v=time*.25f+a.Offset;
                a.Body.localPosition=LocalSurface(a.Body.parent,a.Home+new Vector3(Mathf.Sin(v)*.32f,0,Mathf.Cos(v*.8f)*.3f));
                a.Body.localRotation=Quaternion.Euler(0,-v*Mathf.Rad2Deg,0);
            }
        }
        readonly Dictionary<int,Vector3> unitPrevious=new Dictionary<int,Vector3>();
        public void Dispose()
        {
            if(root!=null) root.gameObject.SetActive(false);
            // Explicit ownership prevents generated meshes/materials leaking on a new run.
            var meshes=new HashSet<Mesh>();
            if(root!=null) foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)) if(filter.sharedMesh!=null && filter.sharedMesh!=hexMesh && filter.gameObject.name=="Low poly cone") meshes.Add(filter.sharedMesh);
            foreach(var mesh in meshes) Object.Destroy(mesh);
            Object.Destroy(hexMesh);
            foreach(var mesh in riverMeshes) Object.Destroy(mesh);
            foreach(var material in materials.Values) Object.Destroy(material);
            if(editableDecor!=null) editableDecor.Dispose();
            if(root!=null) Object.Destroy(root.gameObject);
        }
        float Range(float min,float max) { return min+(float)random.NextDouble()*(max-min); }
    }
}
