using System;
using System.Collections.Generic;

namespace Origins
{
    public enum Phase { Planning, Action, Finished }
    public enum UnitKind { Scout, Hunter, Settler, Warrior, Slinger, Trader }
    public sealed class Unit
    {
        public int Id, Owner;
        public UnitKind Kind;
        public Hex Position;
        public readonly List<Hex> Order=new List<Hex>();
        public bool FoundOrder;
        public bool WaitOrder;
        public bool HasOrder { get { return WaitOrder || FoundOrder || Order.Count>1; } }
        public int Movement { get { return Kind==UnitKind.Scout ? 4 : 3; } }
        public string Name { get { return Kind==UnitKind.Scout ? "Разведчики" : Kind==UnitKind.Hunter ? "Охотники" : Kind==UnitKind.Warrior ? "Воины" : Kind==UnitKind.Slinger ? "Пращники" : Kind==UnitKind.Trader ? "Торговец" : "Поселенцы"; } }
    }
    public sealed class Choice
    {
        public string Id, Name, Description, Requires;
        public int Cost;
        public UnitKind? Unit;
        public string[] RequiresAll=new string[0];
        public string SiteRule;
        public bool Wonder;
        public int MinPopulation,BonusFood,BonusProduction,BonusScience,BonusCulture;
        public Choice(string id,string name,string description,int cost,string requires=null,UnitKind? unit=null)
        { Id=id; Name=name; Description=description; Cost=cost; Requires=requires; Unit=unit; }
    }
    public sealed class City
    {
        public int Id, Owner, Population=1, Food, ProductionProgress, BorderCulture, BorderExpansions;
        public string Name;
        public Hex Position;
        public Choice Production;
        public readonly HashSet<string> Buildings=new HashSet<string>();
        public readonly List<Choice> Offers=new List<Choice>();
        public readonly HashSet<Hex> Territory=new HashSet<Hex>();
        public readonly Dictionary<string,Hex> BuildingSites=new Dictionary<string,Hex>();
        public readonly Dictionary<Hex,string> Improvements=new Dictionary<Hex,string>();
        public readonly List<ResidentJob> Workers=new List<ResidentJob>();
        public CityFocus Focus=CityFocus.Food;
        public CityFocus? PlannedFocus;
    }
    public struct Yield
    {
        public int Food, Production, Science, Culture, Gold;
        public Yield(int food,int production,int science,int culture=0,int gold=0) { Food=food; Production=production; Science=science; Culture=culture; Gold=gold; }
    }

    // The rules have no Unity dependency: visuals never determine economic outcomes.
    public sealed partial class Game
    {
        public const int TurnLimit=48;
        public readonly World World;
        public readonly List<City> Cities=new List<City>();
        public readonly List<Unit> Units=new List<Unit>();
        public Phase Phase=Phase.Planning;
        public int Turn=1;
        public readonly GameContent Content;
        public bool Won;
        public readonly DemoBalance Balance;
        int nextId;

        public static Choice[] Techs { get { return GameContent.Current.Technologies; } }
        public static Choice[] Projects { get { return GameContent.Current.Projects; } }

        public Game(int seed,DemoBalance balance=null,GameContent content=null,int players=1)
        {
            Content=content??GameContent.Current??throw new InvalidOperationException("Load a game content catalog before creating a match");
            Balance=balance??new DemoBalance();
            Tribes.Add(new Tribe(seed^0x12347)); World=new World(seed);
            AddCity(World.Start);
            AddUnit(UnitKind.Scout,new Hex(World.Start.Q+1,World.Start.R));
            RefreshOffers();
            Log("У реки начинается история вашего племени.");
            if(players==2) InitializeSecondPlayer(seed);
        }
        void AddCity(Hex h)
        {
            var city=new City{Id=nextId++,Owner=ActivePlayer,Position=h,Name=OwnCities.Count==0 ? ActivePlayer==0 ? "Первый очаг" : "Очаг друга" : "Поселение "+(OwnCities.Count+1)};
            city.Territory.Add(h);
            foreach(var tile in World.Neighbours(h))
            {
                bool claimed=false; foreach(var existing in Cities) if(existing.Territory.Contains(tile.Hex)) claimed=true;
                if(!claimed) city.Territory.Add(tile.Hex);
            }
            Cities.Add(city); AssignResidents(city);
            Reveal(h,3);
        }
        void AddUnit(UnitKind kind,Hex h) { Units.Add(new Unit{Id=nextId++,Owner=ActivePlayer,Kind=kind,Position=h}); Reveal(h,2); }
        public void Log(string message) { Journal.Insert(0,message); if(Journal.Count>16) Journal.RemoveAt(16); }
        public int TotalPopulation { get { int total=0; foreach(var c in OwnCities) total+=c.Population; return total; } }
        public int ExploredCount { get { int count=0; foreach(var t in World.Ordered) if(t.Explored) count++; return count; } }
        public int Movement(Unit unit) { return unit.Movement; }
        public Yield Income(City city)
        {
            Yield gross=GrossIncome(city);
            return new Yield(gross.Food-city.Population*Balance.FoodPerResident,gross.Production,gross.Science,gross.Culture,gross.Gold);
        }
        public bool ChooseResearch(string id)
        {
            if(!CanPlan) return false;
            foreach(var c in ResearchOffers) if(c.Id==id && Knowledge>=c.Cost)
            { Knowledge-=c.Cost; Technologies.Add(c.Id); ResearchOffers.Clear(); RefreshOffers(); Record("research",id); Log("Открытие: "+c.Name); return true; }
            return false;
        }
        public bool ResearchReady { get { return CanAfford(ResearchOffers,Knowledge); } }
        public bool CultureReady { get { return CanAfford(CultureOffers,CultureStock); } }
        static bool CanAfford(List<Choice> offers,int stock) { foreach(var c in offers) if(stock>=c.Cost) return true; return false; }
        public bool ChooseCulture(string id)
        {
            if(!CanPlan || ResearchReady) return false;
            foreach(var c in CultureOffers) if(c.Id==id && CultureStock>=c.Cost)
            { CultureStock-=c.Cost; CulturalOpenings.Add(c.Id); CultureOffers.Clear(); RefreshOffers(); Record("culture",id); Log("Культурное открытие: "+c.Name); return true; }
            return false;
        }
        public bool ChooseProduction(City city,string id)
        {
            if(!CanPlan || !Cities.Contains(city) || city.Owner!=ActivePlayer || city.Production!=null) return false;
            foreach(var c in city.Offers) if(c.Id==id && CanProduce(city,c)) { city.Production=c; city.ProductionProgress=0; city.Offers.Clear(); Record("production",id,city.Id); return true; }
            return false;
        }
        public bool PlanMove(Unit unit,Hex target)
        {
            if(!CanPlan || !Units.Contains(unit) || unit.Owner!=ActivePlayer) return false;
            var route=World.Path(unit.Position,target,Movement(unit));
            if(route.Count==0) return false;
            if(route.Count==1) return PlanWait(unit);
            unit.Order.Clear(); unit.Order.AddRange(route); unit.FoundOrder=false; unit.WaitOrder=false; return true;
        }
        public bool PlanWait(Unit unit)
        {
            if(!CanPlan || !Units.Contains(unit) || unit.Owner!=ActivePlayer) return false;
            unit.Order.Clear(); unit.WaitOrder=true; unit.FoundOrder=false; return true;
        }
        public bool CanFound(Unit unit,Hex h)
        {
            if(unit.Kind!=UnitKind.Settler || World.Get(h)==null || !World.Get(h).Walkable) return false;
            foreach(var c in Cities) if(Hex.Distance(c.Position,h)<4) return false;
            return true;
        }
        public bool PlanFound(Unit unit)
        {
            if(!CanPlan || !Units.Contains(unit) || unit.Owner!=ActivePlayer) return false;
            Hex h=unit.Order.Count>0 ? unit.Order[unit.Order.Count-1] : unit.Position;
            if(!CanFound(unit,h)) return false;
            unit.FoundOrder=true; unit.WaitOrder=false; return true;
        }
        public string BlockingReason()
        {
            if(Phase!=Phase.Planning) return "Дождитесь завершения действий";
            if(ResearchReady) return "Выберите исследование";
            if(CultureReady) return "Выберите культурное открытие";
            foreach(var c in OwnCities) if(c.Production==null && c.Offers.Count>0) return "Выберите производство: "+c.Name;
            foreach(var u in OwnUnits) if(!u.HasOrder) return "Отдайте приказ: "+u.Name;
            return null;
        }
        public void FinishAction()
        {
            if(Phase!=Phase.Action) return;
            ResolveTurn();
            foreach(var unit in Units) { unit.Order.Clear(); unit.FoundOrder=false; unit.WaitOrder=false; }
            Turn++; Phase=Phase.Planning; foreach(var t in Tribes) t.Decisions.Clear(); PlansLocked=false;
            int previous=ActivePlayer; for(int p=0;p<Tribes.Count;p++) { SetPlayer(p); RefreshOffers(); } SetPlayer(previous);
        }
        void ResolveEconomy()
        {
            var consumed=new List<Unit>();
            int previous=ActivePlayer;
            foreach(var unit in Units) if(unit.FoundOrder && (unit.Order.Count==0 || unit.Position.Equals(unit.Order[unit.Order.Count-1])) && CanFound(unit,unit.Position))
            { SetPlayer(unit.Owner); AddCity(unit.Position); consumed.Add(unit); Log("Основано поселение: "+Cities[Cities.Count-1].Name); }
            foreach(var unit in consumed) Units.Remove(unit);
            foreach(var city in Cities)
            {
                SetPlayer(city.Owner);
                if(city.PlannedFocus.HasValue) { city.Focus=city.PlannedFocus.Value; city.PlannedFocus=null; city.Workers.Clear(); }
                AssignResidents(city);
                Yield y=Income(city); Knowledge+=y.Science; CultureStock+=y.Culture; Gold+=y.Gold;
                city.BorderCulture+=y.Culture; ExpandCulture(city);
                city.Food=Math.Max(0,Math.Min(FoodCapacity(city),city.Food+y.Food));
                int threshold=GrowthCost(city);
                if(city.Food>=threshold && city.Population<PopulationCapacity(city) && y.Food>0)
                { city.Food-=threshold; city.Population++; AssignResidents(city); Log(city.Name+": население выросло до "+city.Population); }
                if(city.Production==null) continue;
                city.ProductionProgress+=y.Production;
                if(city.ProductionProgress<city.Production.Cost) continue;
                Choice done=city.Production;
                if(done.Unit.HasValue)
                {
                    if(done.Unit.Value==UnitKind.Settler && city.Population<=1) continue;
                    AddUnit(done.Unit.Value,city.Position);
                    if(done.Unit.Value==UnitKind.Settler) city.Population--;
                }
                else
                {
                    Hex site;
                    if(done.Wonder && WonderBuilt(done.Id)) { city.Production=null; city.ProductionProgress=0; Log(done.Name+": чудо уже построено в другом городе."); continue; }
                    if(!FindBuildingSite(city,done,out site)) continue;
                    city.Buildings.Add(done.Id); city.BuildingSites.Add(done.Id,site);
                }
                AssignResidents(city);
                city.Production=null; city.ProductionProgress=0;
                Log(city.Name+": готовы "+done.Name.ToLowerInvariant());
            }
            SetPlayer(previous);
        }
        void RefreshOffers()
        {
            if(ResearchOffers.Count==0)
            {
                var pool=new List<Choice>();
                foreach(var t in Content.Technologies) if(!Technologies.Contains(t.Id) && (t.Requires==null || HasOpening(t.Requires))) pool.Add(t);
                Offer(pool,ResearchOffers);
            }
            if(CultureOffers.Count==0)
            {
                var pool=new List<Choice>();
                foreach(var t in Content.Cultures) if(!CulturalOpenings.Contains(t.Id) && (t.Requires==null || HasOpening(t.Requires))) pool.Add(t);
                Offer(pool,CultureOffers);
            }
            foreach(var city in OwnCities)
            {
                city.Offers.RemoveAll(p=>!CanProduce(city,p));
                if(city.Production!=null || city.Offers.Count>0) continue;
                var pool=new List<Choice>();
                foreach(var p in Content.Projects)
                {
                    Hex site;
                    if(CanProduce(city,p)) pool.Add(p);
                }
                Offer(pool,city.Offers);
            }
        }
        void Offer(List<Choice> pool,List<Choice> into)
        {
            for(int i=pool.Count-1;i>0;i--) { int j=random.Next(i+1); var swap=pool[i]; pool[i]=pool[j]; pool[j]=swap; }
            for(int i=0;i<Math.Min(3,pool.Count);i++) into.Add(pool[i]);
        }
    }
}
