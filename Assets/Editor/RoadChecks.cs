using System.Collections.Generic;

namespace Origins.Editor
{
    public static partial class ModelChecks
    {
        static void CheckMerchantRoads()
        {
            var game=CollisionGame(512); var world=game.World;
            var merchant=new Unit{Id=900,Kind=UnitKind.Trader,Position=world.Start}; game.Units.Add(merchant);
            Hex middle=new Hex(world.Start.Q+1,world.Start.R),target=new Hex(world.Start.Q+2,world.Start.R);
            var rivers=new List<RiverEdge>(world.RiverEdges); var flags=new Dictionary<Hex,bool>(); foreach(var tile in world.Ordered) flags[tile.Hex]=tile.River;
            Assert(game.PlanMove(merchant,target),"merchant can plan a dry land route");
            Assert(world.RoadEdges.Count==0,"planning does not build roads");
            Ready(game); Assert(game.BeginAction(),"merchant turn commits"); game.ResolveTurn();
            Assert(world.HasRoad(world.Start,middle) && world.HasRoad(middle,target),"accepted merchant steps leave persistent roads");
            int count=world.RoadEdges.Count; game.ResolveTurn(); Assert(world.RoadEdges.Count==count,"road creation is idempotent");
            Assert(!world.AddRoad(middle,world.Start),"reverse travel reuses the existing road");
            Assert(world.RiverEdges.Count==rivers.Count,"merchant movement never creates river edges");
            for(int i=0;i<rivers.Count;i++) Assert(world.RiverEdges[i].From.Equals(rivers[i].From) && world.RiverEdges[i].To.Equals(rivers[i].To),"river layout remains unchanged after travel");
            foreach(var tile in world.Ordered) Assert(tile.River==flags[tile.Hex],"travel does not change freshwater bonuses");
            game.FinishAction(); game.PlanMove(merchant,world.Start); Ready(game); game.BeginAction(); game.FinishAction();
            Assert(world.RoadEdges.Count==count,"return trip keeps the road instead of duplicating it");
            var scoutGame=CollisionGame(513); var scout=new Unit{Id=901,Kind=UnitKind.Scout,Position=scoutGame.World.Start}; scoutGame.Units.Add(scout);
            scoutGame.PlanMove(scout,new Hex(scout.Position.Q+1,scout.Position.R)); Ready(scoutGame); scoutGame.BeginAction(); scoutGame.FinishAction();
            Assert(scoutGame.World.RoadEdges.Count==0,"scouts do not lay trade roads");
            var blocked=CollisionGame(514); Hex blockedTarget=new Hex(blocked.World.Start.Q+1,blocked.World.Start.R);
            var blockedTrader=new Unit{Id=902,Kind=UnitKind.Trader,Position=blocked.World.Start};
            var guard=new Unit{Id=903,Kind=UnitKind.Warrior,Position=blockedTarget}; blocked.Units.Add(blockedTrader); blocked.Units.Add(guard);
            blocked.PlanMove(blockedTrader,blockedTarget); blocked.PlanWait(guard); Ready(blocked); blocked.BeginAction(); blocked.FinishAction();
            Assert(blocked.World.RoadEdges.Count==0 && blockedTrader.Position.Equals(blocked.World.Start),"blocked movement does not build a road to an unvisited tile");
            Hex water=new Hex(middle.Q,middle.R+1); world.Get(water).Terrain=Terrain.Ocean;
            Assert(!world.AddRoad(middle,water),"dry roads cannot cross ocean cells");
            Assert(!world.AddRoad(world.Start,target),"roads store actual adjacent steps rather than teleport lines");
        }
    }
}
