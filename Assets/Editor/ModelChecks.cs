using System;
using System.Collections.Generic;

namespace Origins.Editor
{
    public static partial class ModelChecks
    {
        static void Assert(bool test,string name) { if(!test) throw new Exception("Model check failed: "+name); }
        public static void Run()
        {
            ContentLoader.Load();
            for(int seed=0;seed<80;seed++)
            {
                var a=new World(seed); var b=new World(seed);
                Assert(a.Ordered.Count==560,"map dimensions");
                var biomeKinds=new HashSet<Terrain>(); foreach(var tile in a.Ordered) biomeKinds.Add(tile.Terrain);
                Assert(biomeKinds.Count==Enum.GetValues(typeof(Terrain)).Length,"all ten approved biomes occur in the demo world");
                Assert(a.Get(a.Start).Walkable,"walkable spawn");
                Assert(a.Reachable(a.Start,3).Count>=19,"connected starting area");
                Assert(a.RiverEdges.Count==b.RiverEdges.Count,"deterministic river edges");
                CheckRiverOutlets(a);
                for(int i=0;i<a.RiverEdges.Count;i++) Assert(a.RiverEdges[i].From.Equals(b.RiverEdges[i].From) && a.RiverEdges[i].To.Equals(b.RiverEdges[i].To),"deterministic river channels");
                a.Reveal(a.Start,100);
                bool expansion=false;
                foreach(var reachable in a.Reachable(a.Start,30).Keys) if(Hex.Distance(reachable,a.Start)>=4) expansion=true;
                Assert(expansion,"reachable second settlement site");
                for(int i=0;i<a.Ordered.Count;i++)
                {
                    Assert(a.Ordered[i].Terrain==b.Ordered[i].Terrain && a.Ordered[i].Resource==b.Ordered[i].Resource && a.Ordered[i].River==b.Ordered[i].River,"deterministic generation");
                }
            }
            var g=new Game(12026);
            Assert(g.ResearchOffers.Count==3 && g.Cities[0].Offers.Count==3,"three initial choices");
            string first=g.ResearchOffers[0].Id;
            Assert(!g.ChooseResearch(first),"science must accumulate before an opening");
            g.Knowledge=9;
            Assert(!g.BeginAction(),"cannot skip ready research");
            Assert(!g.ChooseResearch("unknown"),"cannot pick outside offer");
            Assert(g.ChooseResearch(first),"choose offered research");
            Assert(!g.ChooseResearch(first) && g.Knowledge==0 && g.Technologies.Contains(first),"opening spends stock once");
            Assert(!g.BeginAction(),"cannot skip production");
            var city=g.Cities[0]; g.ChooseProduction(city,city.Offers[0].Id);
            Unit scout=g.Units[0]; Hex start=scout.Position;
            Hex target=new Hex(start.Q+1,start.R);
            Assert(g.PlanMove(scout,target) && scout.Position.Equals(start),"planning does not move units");
            Assert(!g.PlanMove(scout,new Hex(999,999)),"reject invalid move");
            Assert(g.BeginAction(),"ready action starts");
            Assert(!g.ChooseResearch(first) && !g.PlanMove(scout,start),"action locks planning");
            scout.Order.Clear(); scout.Order.Add(new Hex(999,999));
            var report=g.ResolveTurn(); Assert(scout.Position.Equals(target),"immutable committed route moves despite draft mutation");
            int stockAfter=g.Knowledge; Assert(object.ReferenceEquals(report,g.ResolveTurn()) && g.Knowledge==stockAfter,"resolver is idempotent");
            g.FinishAction(); Assert(g.Turn==2 && scout.Order.Count==0,"finish clears orders and advances");
            g.FinishAction(); Assert(g.Turn==2,"cannot resolve a turn twice");
            var w=new World(25); Hex h=w.Start; Hex adjacent=new Hex(h.Q+1,h.R);
            w.Get(adjacent).Terrain=Terrain.Forest;
            Assert(w.Path(h,adjacent,1).Count==0 && w.Path(h,adjacent,2).Count==2,"terrain movement cost");
            w.Get(adjacent).Explored=false; Assert(w.Path(h,adjacent,5).Count==0,"fog blocks direct orders");
            w.Get(adjacent).Explored=true; w.Get(adjacent).Terrain=Terrain.Mountain;
            Assert(w.Path(h,adjacent,5).Count==0,"mountains block movement");
            CheckFounding();
            CheckEconomy();
            CheckAdditionalBiomes();
            CheckCultureAndResolver();
            CheckMerchantRoads();
            CheckWondersAndTools();
            for(int seed=0;seed<25;seed++) Simulate(seed);
        }
        static void CheckFounding()
        {
            var g=new Game(4); var unit=new Unit{Id=999,Kind=UnitKind.Settler,Position=g.World.Start}; g.Units.Add(unit);
            Assert(!g.CanFound(unit,unit.Position),"cities need spacing");
            Hex site=new Hex(g.World.Start.Q+4,g.World.Start.R); var tile=g.World.Get(site); tile.Terrain=Terrain.Grass; tile.Explored=true;
            unit.Position=site; Assert(g.PlanFound(unit),"legal founding order");
            Assert(g.Cities.Count==1,"founding waits for action");
            Ready(g); g.BeginAction(); g.FinishAction();
            Assert(g.Cities.Count==2 && !g.Units.Contains(unit),"founding creates city and consumes settlers");
            Assert(g.Cities[1].Offers.Count==3,"new city receives production choices");
        }
        static void CheckRiverOutlets(World world)
        {
            var next=new Dictionary<Hex,Hex>();
            foreach(var edge in world.RiverEdges)
            {
                Assert(Hex.Distance(edge.From,edge.To)==1,"river connects adjacent hexes");
                Assert(!next.ContainsKey(edge.From),"river has a single downstream direction");
                next.Add(edge.From,edge.To);
            }
            Assert(next.ContainsKey(world.Start),"starting freshwater connects to drainage network");
            foreach(var edge in world.RiverEdges)
            {
                Hex h=edge.From; var visited=new HashSet<Hex>();
                while(next.ContainsKey(h)) { Assert(visited.Add(h),"river contains no cycle"); h=next[h]; }
                Assert(world.Get(h).Terrain==Terrain.Ocean,"every river reaches ocean rather than ending on land or coast");
            }
        }
        static void CheckAdditionalBiomes()
        {
            var game=new Game(12026); var city=game.Cities[0]; var tile=game.World.Get(city.Position); tile.River=false; tile.Resource=Resource.None;
            foreach(var kind in new[]{Terrain.Plains,Terrain.Swamp,Terrain.Tundra})
            {
                tile.Terrain=kind; var income=game.WorkIncome(city,tile.Hex,null);
                Assert(tile.Walkable && income.Production==1 && income.Food==(kind==Terrain.Tundra ? 0 : 1),"additional biome income requires a worker");
                Assert(tile.MoveCost==(kind==Terrain.Plains ? 1 : 2),"additional biome movement cost");
            }
        }
        static void Ready(Game g)
        {
            while(g.ResearchReady) Assert(g.ChooseResearch(g.ResearchOffers.Find(c=>c.Cost<=g.Knowledge).Id),"choose an affordable research card");
            while(g.CultureReady) Assert(g.ChooseCulture(g.CultureOffers.Find(c=>c.Cost<=g.CultureStock).Id),"choose an affordable cultural card");
            foreach(var city in g.Cities) if(city.Production==null && city.Offers.Count>0) g.ChooseProduction(city,city.Offers[0].Id);
            foreach(var unit in g.Units) if(!unit.HasOrder) g.PlanWait(unit);
        }
        static Game CollisionGame(int seed)
        {
            var g=new Game(seed); g.Units.Clear(); g.World.Reveal(g.World.Start,100);
            foreach(var t in g.World.Ordered) if(Hex.Distance(t.Hex,g.World.Start)<4) { t.Terrain=Terrain.Grass; t.Resource=Resource.None; }
            return g;
        }
        static void CheckCultureAndResolver()
        {
            var g=new Game(42); var city=g.Cities[0];
            g.Knowledge=9; g.CultureStock=9;
            Assert(!g.ChooseCulture("oral"),"science decision precedes culture");
            g.ChooseResearch(g.ResearchOffers[0].Id);
            Assert(g.ChooseCulture("oral") && g.CityCultureIncome(city)==g.Balance.CenterCulture+1,"oral traditions have a real city income");
            Assert(g.CultureStock==0 && g.Knowledge==0,"separate science and culture stocks");
            var scout=g.Units[0];
            foreach(var c in g.Cities) if(c.Production==null) g.ChooseProduction(c,c.Offers[0].Id);
            Assert(!g.BeginAction(),"every unit requires explicit movement or wait");
            Assert(g.PlanWait(scout) && scout.HasOrder,"wait is a recorded order");
            int oldArea=city.Territory.Count;
            for(int turn=0;turn<4;turn++) { Ready(g); Assert(g.BeginAction(),"culture test ready"); g.FinishAction(); }
            Assert(city.Territory.Count>oldArea && city.BorderExpansions>0,"culture expands connected city territory");
            var furs=new Game(41); var fc=furs.Cities[0]; Hex fh=fc.Position;
            foreach(var h in fc.Territory) if(!h.Equals(fc.Position)) { fh=h; break; }
            furs.World.Get(fh).Resource=Resource.Furs; furs.World.Get(fh).Terrain=Terrain.Forest;
            Assert(furs.WorkIncome(fc,fh,null,true).Gold==0,"furs locked without needle");
            furs.CultureStock=9; furs.ChooseCulture("needle");
            Assert(furs.WorkIncome(fc,fh,null,true).Gold==1,"needle enables genuine furs income");
            int fastWins=0,slowWins=0;
            for(int seed=0;seed<100;seed++)
            {
                var a=CollisionGame(seed); var b=CollisionGame(seed); Hex target=a.World.Start;
                foreach(var match in new[]{a,b})
                {
                    var fast=new Unit{Id=100,Kind=UnitKind.Scout,Position=new Hex(target.Q-1,target.R)};
                    var slow=new Unit{Id=101,Kind=UnitKind.Warrior,Position=new Hex(target.Q+1,target.R)};
                    match.Units.Add(fast); match.Units.Add(slow);
                    match.PlanMove(fast,target); match.PlanMove(slow,target); Ready(match);
                    Assert(match.BeginAction(),"contested turn ready"); match.ResolveTurn();
                }
                Assert(a.Units[0].Position.Equals(b.Units[0].Position) && a.Units[1].Position.Equals(b.Units[1].Position),"initiative reproducible across runs");
                Assert(a.Units[0].Position.Equals(target)!=a.Units[1].Position.Equals(target),"one winner per contested destination");
                if(a.Units[0].Position.Equals(target)) fastWins++; else slowWins++;
            }
            Assert(fastWins>slowWins && slowWins>0,"faster usually wins, slower has a chance");
            var block=CollisionGame(6); Hex center=block.World.Start;
            var mover=new Unit{Id=100,Kind=UnitKind.Scout,Position=new Hex(center.Q-1,center.R)};
            var waiting=new Unit{Id=101,Kind=UnitKind.Scout,Position=center}; block.Units.Add(mover); block.Units.Add(waiting);
            block.PlanMove(mover,center); block.PlanWait(waiting); Ready(block); block.BeginAction(); var blocked=block.ResolveTurn();
            Assert(!mover.Position.Equals(center) && blocked.Moves.Count==0,"stationary occupant blocks movement");
            block.FinishAction(); block.PlanMove(mover,center); block.PlanMove(waiting,mover.Position); Ready(block); block.BeginAction();
            Assert(block.ResolveTurn().Moves.Count==2,"friendly simultaneous exchange is allowed");
            var cost=CollisionGame(8); var runner=new Unit{Id=100,Kind=UnitKind.Scout,Position=cost.World.Start}; cost.Units.Add(runner);
            Hex woods=new Hex(runner.Position.Q+1,runner.Position.R); cost.World.Get(woods).Terrain=Terrain.Forest;
            cost.PlanMove(runner,woods); Ready(cost); cost.BeginAction();
            Assert(cost.ResolveTurn().Moves[0].Tick==2,"terrain cost controls movement microsteps");
            var invalid=CollisionGame(7); var u=new Unit{Id=100,Kind=UnitKind.Scout,Position=invalid.World.Start}; invalid.Units.Add(u);
            u.Order.Add(u.Position); u.Order.Add(new Hex(u.Position.Q+2,u.Position.R)); Ready(invalid);
            Assert(!invalid.BeginAction(),"mutated nonadjacent route rejected before commitment");
        }
        static void CheckEconomy()
        {
            var g=new Game(12026);
            var c=g.Cities[0];
            Assert(c.Population==1 && c.Workers.Count==1 && c.Territory.Count==7,"one resident and seven starting cells");
            foreach(var h in c.Territory) { var t=g.World.Get(h); t.Terrain=Terrain.Grass; t.Resource=Resource.None; t.River=false; }
            c.Workers.Clear();
            Assert(g.GrossIncome(c).Food==g.Balance.CenterFood && g.GrossIncome(c).Production==g.Balance.CenterProduction,"unworked tiles provide no yield");
            g.AssignResidents(c);
            Hex worked=c.Workers[0].Position;
            g.World.Get(worked).Resource=Resource.Grain;
            int before=g.GrossIncome(c).Food;
            Assert(before==g.Balance.CenterFood+2,"locked grain only provides terrain yield");
            g.Technologies.Add("farming"); g.AssignResidents(c);
            Assert(!c.Improvements.ContainsKey(worked) && g.GrossIncome(c).Food==before,"opening alone does not improve an occupied resource");
            Hex free=worked;
            foreach(var h in c.Territory) if(!h.Equals(c.Position) && !h.Equals(worked)) { free=h; break; }
            g.World.Get(free).Resource=Resource.Grain; c.Population=2; g.AssignResidents(c);
            Assert(c.Workers.Count==2 && c.Improvements.ContainsKey(free),"free new resident improves an unlocked resource immediately");
            Assert(g.GrossIncome(c).Food==g.Balance.CenterFood+2+5,"only worked improved grain adds its bonus");
            var positions=new HashSet<Hex>(); var ids=new HashSet<int>();
            foreach(var job in c.Workers) Assert(positions.Add(job.Position) && ids.Add(job.ResidentId),"unique workplaces and resident assignments");
            Yield preview=g.Income(c); int stock=c.Food;
            for(int i=0;i<10;i++) g.Income(c);
            Assert(c.Food==stock,"income preview never mutates stocks");
            Assert(g.PlanFocus(c,CityFocus.Production) && c.Focus==CityFocus.Food && g.Income(c).Food==preview.Food,"focus is a deferred order");
            int capacity=g.FoodCapacity(c),food=g.GrossIncome(c).Food;
            c.Buildings.Add("granary");
            Assert(g.FoodCapacity(c)==capacity+5 && g.GrossIncome(c).Food==food,"granary increases storage, not food income");
            var workshopGame=new Game(1); var workshopCity=workshopGame.Cities[0];
            workshopGame.Balance.CenterFood=10; workshopCity.Workers.Clear();
            foreach(var h in workshopCity.Territory) { var t=workshopGame.World.Get(h); t.Terrain=Terrain.Grass; t.Resource=Resource.None; t.River=false; }
            Choice workshop=null; foreach(var p in Game.Projects) if(p.Id=="workshop") workshop=p;
            Hex site; Assert(workshopGame.FindBuildingSite(workshopCity,workshop,out site) && !site.Equals(workshopCity.Position),"buildings occupy a separate territory cell");
            workshopCity.Buildings.Add("workshop"); workshopCity.BuildingSites.Add("workshop",site);
            Assert(workshopGame.Income(workshopCity).Production==workshopGame.Balance.CenterProduction,"empty workshop has no worker income");
            workshopCity.Focus=CityFocus.Production; workshopGame.AssignResidents(workshopCity);
            Assert(workshopCity.Workers[0].BuildingId=="workshop" && workshopGame.Income(workshopCity).Production==workshopGame.Balance.CenterProduction+2,"staffed workshop produces its own yield");
        }
        static void Simulate(int seed)
        {
            var g=new Game(seed); var known=new HashSet<string>();
            for(int turn=0;turn<55;turn++)
            {
                Ready(g); Assert(g.BeginAction(),"simulated turn unblocked"); g.FinishAction();
                Assert(g.Cities[0].Population>0 && g.Phase==Phase.Planning,"continuous demo progress");
                foreach(var tech in g.Technologies) known.Add(tech);
                foreach(var offered in g.ResearchOffers)
                {
                    Assert(!known.Contains(offered.Id),"completed research not offered");
                    Assert(offered.Requires==null || known.Contains(offered.Requires),"research prerequisite");
                }
                foreach(var city in g.Cities)
                {
                    Assert(city.Food>=0,"food remains nonnegative");
                    foreach(var offer in city.Offers) Assert(!city.Buildings.Contains(offer.Id),"built buildings not offered again");
                }
            }
            Assert(g.Turn==56 && !g.Won,"no artificial early victory or turn limit");
        }
    }
}
