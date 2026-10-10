using System;
using System.Collections.Generic;

namespace Origins
{
    public sealed class Tribe
    {
        public int Knowledge, CultureStock, Gold;
        public readonly HashSet<string> Technologies=new HashSet<string>();
        public readonly HashSet<string> CulturalOpenings=new HashSet<string>();
        public readonly List<Choice> ResearchOffers=new List<Choice>(), CultureOffers=new List<Choice>();
        public readonly List<string> Journal=new List<string>();
        public readonly HashSet<Hex> Explored=new HashSet<Hex>();
        public readonly Random Random;
        public readonly List<PlanDecision> Decisions=new List<PlanDecision>();
        public Tribe(int seed) { Random=new Random(seed); }
    }
    [Serializable] public sealed class PlanDecision { public string kind, choice; public int city, focus; }
    [Serializable] public sealed class PlannedUnit { public int id, q, r; public bool wait, found; }
    [Serializable] public sealed class PlayerPlan
    {
        public int player, turn;
        public PlanDecision[] decisions;
        public PlannedUnit[] units;
    }
    [Serializable] public sealed class OnlineTurn { public int turn; public PlayerPlan[] plans; public string checksum; }
    [Serializable] public sealed class OnlineRoom
    {
        public int protocol=1, seed, seat, turn, players;
        public string code, token, error, version;
        public bool[] ready;
        public OnlineTurn[] history;
        public PlayerPlan pending;
    }
    public sealed partial class Game
    {
        public readonly List<Tribe> Tribes=new List<Tribe>();
        public int ActivePlayer { get; private set; }
        public bool PlansLocked;
        public bool Multiplayer { get { return Tribes.Count>1; } }
        List<PlanDecision> decisions { get { return Tribes[ActivePlayer].Decisions; } }
        public List<City> OwnCities { get { return Cities.FindAll(c=>c.Owner==ActivePlayer); } }
        public List<Unit> OwnUnits { get { return Units.FindAll(u=>u.Owner==ActivePlayer); } }
        public int Knowledge { get { return Tribes[ActivePlayer].Knowledge; } set { Tribes[ActivePlayer].Knowledge=value; } }
        public int CultureStock { get { return Tribes[ActivePlayer].CultureStock; } set { Tribes[ActivePlayer].CultureStock=value; } }
        public int Gold { get { return Tribes[ActivePlayer].Gold; } set { Tribes[ActivePlayer].Gold=value; } }
        public HashSet<string> Technologies { get { return Tribes[ActivePlayer].Technologies; } }
        public HashSet<string> CulturalOpenings { get { return Tribes[ActivePlayer].CulturalOpenings; } }
        public List<Choice> ResearchOffers { get { return Tribes[ActivePlayer].ResearchOffers; } }
        public List<Choice> CultureOffers { get { return Tribes[ActivePlayer].CultureOffers; } }
        public List<string> Journal { get { return Tribes[ActivePlayer].Journal; } }
        Random random { get { return Tribes[ActivePlayer].Random; } }
        public bool CanPlan { get { return Phase==Phase.Planning && !PlansLocked; } }
        public void SetPlayer(int player)
        {
            if(player<0 || player>=Tribes.Count) throw new ArgumentOutOfRangeException("player");
            ActivePlayer=player;
            if(Multiplayer) foreach(var tile in World.Ordered) tile.Explored=Tribes[player].Explored.Contains(tile.Hex);
        }
        void Reveal(Hex h,int radius,int player=-1)
        {
            if(!Multiplayer) { World.Reveal(h,radius); return; }
            if(player<0) player=ActivePlayer;
            foreach(var tile in World.Ordered) if(Hex.Distance(h,tile.Hex)<=radius) Tribes[player].Explored.Add(tile.Hex);
            if(player==ActivePlayer) foreach(var tile in World.Ordered) tile.Explored=Tribes[player].Explored.Contains(tile.Hex);
        }
        public bool IsExplored(int player,Hex h) { return !Multiplayer ? World.Get(h).Explored : Tribes[player].Explored.Contains(h); }
        void Record(string kind,string choice=null,int city=0,int focus=0) { if(Multiplayer) decisions.Add(new PlanDecision{kind=kind,choice=choice,city=city,focus=focus}); }
        public PlayerPlan CapturePlan()
        {
            var orders=new List<PlannedUnit>();
            foreach(var u in OwnUnits) { Hex end=u.Order.Count>0 ? u.Order[u.Order.Count-1] : u.Position; orders.Add(new PlannedUnit{id=u.Id,q=end.Q,r=end.R,wait=u.WaitOrder,found=u.FoundOrder}); }
            return new PlayerPlan { player=ActivePlayer,turn=Turn,decisions=decisions.ToArray(),units=orders.ToArray() };
        }
        public bool ApplyPlan(PlayerPlan plan,bool alreadyApplied=false)
        {
            if(plan==null || plan.turn!=Turn || plan.player<0 || plan.player>=Tribes.Count || plan.decisions==null || plan.units==null) return false;
            int previous=ActivePlayer; bool locked=PlansLocked; PlansLocked=false; SetPlayer(plan.player);
            try
            {
                if(!alreadyApplied) foreach(var d in plan.decisions)
                {
                    var c=Cities.Find(x=>x.Id==d.city && x.Owner==plan.player);
                    bool ok=d.kind=="research" ? ChooseResearch(d.choice) : d.kind=="culture" ? ChooseCulture(d.choice) : d.kind=="production" ? c!=null && ChooseProduction(c,d.choice) : d.kind=="focus" && c!=null && (d.focus==0 || d.focus==1) && PlanFocus(c,(CityFocus)d.focus);
                    if(!ok) return false;
                }
                if(plan.units.Length!=OwnUnits.Count) return false;
                var ids=new HashSet<int>();
                foreach(var order in plan.units)
                {
                    if(order==null || order.wait && order.found) return false;
                    var u=Units.Find(x=>x.Id==order.id && x.Owner==plan.player);
                    if(u==null || !ids.Add(u.Id)) return false;
                    bool ok=order.wait ? PlanWait(u) : order.found && u.Position.Equals(new Hex(order.q,order.r)) ? PlanFound(u) : PlanMove(u,new Hex(order.q,order.r));
                    if(!ok || order.found && !PlanFound(u)) return false;
                }
                return BlockingReason()==null;
            }
            finally { SetPlayer(previous); PlansLocked=locked; }
        }
        Hex SecondStart()
        {
            // Select an existing fertile river valley on the same connected landmass.
            var connected=new HashSet<Hex>{World.Start}; var open=new Queue<Hex>(); open.Enqueue(World.Start);
            while(open.Count>0) foreach(var tile in World.Neighbours(open.Dequeue())) if(tile.Walkable && connected.Add(tile.Hex)) open.Enqueue(tile.Hex);
            Tile best=null; int score=int.MinValue;
            foreach(var tile in World.Ordered)
            {
                if(!tile.Walkable || !connected.Contains(tile.Hex) || Hex.Distance(tile.Hex,World.Start)<8) continue;
                int land=0, food=0; foreach(var n in World.Neighbours(tile.Hex)) { if(n.Walkable) land++; if(n.Terrain==Terrain.Grass || n.Terrain==Terrain.Plains) food++; }
                if(land<4) continue;
                int value=Hex.Distance(tile.Hex,World.Start)*2+food*4+(tile.River ? 18 : 0);
                if(value>score) { best=tile; score=value; }
            }
            if(best==null) throw new InvalidOperationException("No connected second starting valley for this seed");
            return best.Hex;
        }
        void InitializeSecondPlayer(int seed)
        {
            foreach(var tile in World.Ordered) if(tile.Explored) Tribes[0].Explored.Add(tile.Hex);
            Tribes.Add(new Tribe(unchecked(seed^0x12347^7919))); SetPlayer(1);
            Hex start=SecondStart(); AddCity(start);
            Hex scout=start; foreach(var n in World.Neighbours(start)) if(n.Walkable) { scout=n.Hex; break; }
            AddUnit(UnitKind.Scout,scout); RefreshOffers(); Log("Ваше племя начинает путь в своей долине."); SetPlayer(0);
        }
        public string Checksum()
        {
            // Stable across Mono, IL2CPP and .NET: no runtime string hash codes.
            uint hash=2166136261;
            Action<string> add=s=> { foreach(char ch in s) { hash^=ch; hash*=16777619; } hash^=255; hash*=16777619; };
            Action<int> number=n=>add(n.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Action<Hex> hex=h=> { number(h.Q); number(h.R); };
            number(Turn); number(World.Seed);
            foreach(var tile in World.Ordered) { hex(tile.Hex); number((int)tile.Terrain); number((int)tile.Resource); number(tile.River ? 1 : 0); number(tile.DiscoveryTaken ? 1 : 0); }
            foreach(var t in Tribes)
            {
                number(t.Knowledge); number(t.CultureStock); number(t.Gold);
                var tech=new List<string>(t.Technologies); tech.Sort(StringComparer.Ordinal); foreach(var id in tech) add(id);
                tech=new List<string>(t.CulturalOpenings); tech.Sort(StringComparer.Ordinal); foreach(var id in tech) add(id);
                foreach(var c in t.ResearchOffers) add(c.Id); foreach(var c in t.CultureOffers) add(c.Id);
                var seen=new List<Hex>(t.Explored); seen.Sort((a,b)=>a.R==b.R ? a.Q.CompareTo(b.Q) : a.R.CompareTo(b.R)); foreach(var h in seen) hex(h);
            }
            foreach(var c in Cities)
            {
                number(c.Id); number(c.Owner); hex(c.Position); number(c.Population); number(c.Food); number(c.ProductionProgress); number(c.BorderCulture); number(c.BorderExpansions); number((int)c.Focus); add(c.Production==null ? "" : c.Production.Id);
                var buildings=new List<string>(c.Buildings); buildings.Sort(StringComparer.Ordinal); foreach(var id in buildings) { add(id); hex(c.BuildingSites[id]); }
                var territory=new List<Hex>(c.Territory); territory.Sort((a,b)=>a.R==b.R ? a.Q.CompareTo(b.Q) : a.R.CompareTo(b.R)); foreach(var h in territory) hex(h);
                foreach(var choice in c.Offers) add(choice.Id);
                foreach(var job in c.Workers) { number(job.ResidentId); hex(job.Position); add(job.BuildingId??""); }
            }
            foreach(var u in Units) { number(u.Id); number(u.Owner); number((int)u.Kind); hex(u.Position); }
            foreach(var edge in World.RoadEdges) { hex(edge.From); hex(edge.To); }
            return hash.ToString("x8");
        }
        public bool ReplayTurn(OnlineTurn turn)
        {
            if(turn==null || turn.turn!=Turn || turn.plans==null || turn.plans.Length!=Tribes.Count) return false;
            for(int p=0;p<Tribes.Count;p++) if(turn.plans[p]==null || turn.plans[p].player!=p || !ApplyPlan(turn.plans[p])) return false;
            if(!BeginAction()) return false;
            FinishAction(); return string.IsNullOrEmpty(turn.checksum) || Checksum()==turn.checksum;
        }
    }
}
