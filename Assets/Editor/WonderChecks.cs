using Origins;

namespace Origins.Editor
{
    public static partial class ModelChecks
    {
        static Choice Project(Game game,string id) { foreach(var project in game.Content.Projects) if(project.Id==id) return project; throw new System.Exception(id); }
        static void CheckWondersAndTools()
        {
            var game=new Game(12026); var city=game.Cities[0]; var wonder=Project(game,"stonehenge");
            var h=new Hex(city.Position.Q,city.Position.R+1); var tile=game.World.Get(h); tile.Terrain=Terrain.Grass; tile.Resource=Resource.Stone; city.Population=3;
            Assert(!game.CanProduce(city,wonder),"wonder requires both technology and culture");
            game.Technologies.Add("stone"); Assert(!game.CanProduce(city,wonder),"technology alone cannot unlock a wonder");
            game.CulturalOpenings.Add("monuments"); Assert(game.CanProduce(city,wonder),"stone and monument traditions unlock stone circle near stone");
            city.Offers.Clear(); city.Offers.Add(wonder); Assert(game.ChooseProduction(city,wonder.Id),"choose available wonder card"); city.ProductionProgress=wonder.Cost;
            Ready(game); Assert(game.BeginAction(),"wonder turn ready"); game.FinishAction();
            Assert(city.Buildings.Contains(wonder.Id) && city.BuildingSites.ContainsKey(wonder.Id),"completed wonder occupies a real map tile");
            var withBonus=game.GrossIncome(city); city.Buildings.Remove(wonder.Id); var without=game.GrossIncome(city); city.Buildings.Add(wonder.Id);
            Assert(withBonus.Culture-without.Culture==4 && withBonus.Science-without.Science==1,"wonder bonus is real city income");
            var rival=new City{Id=999,Population=4,Position=new Hex(city.Position.Q+4,city.Position.R)};
            foreach(var t in game.World.Neighbours(rival.Position)) rival.Territory.Add(t.Hex);
            game.Cities.Add(rival); Assert(!game.CanProduce(rival,wonder),"completed wonder is unique across all cities");
            game.Technologies.Add("masonry"); var pyramid=Project(game,"pyramids");
            foreach(var t in game.World.Neighbours(rival.Position)) { t.Terrain=Terrain.Grass; t.Resource=Resource.None; }
            Assert(!game.CanProduce(rival,pyramid),"pyramid needs desert land"); game.World.Get(new Hex(rival.Position.Q+1,rival.Position.R)).Terrain=Terrain.Desert;
            Assert(game.CanProduce(rival,pyramid),"pyramid allowed after science culture population and desert requirements");
            var garden=Project(game,"gardens"); game.Technologies.Add("irrigation"); game.CulturalOpenings.Add("council");
            foreach(var t in game.World.Neighbours(rival.Position)) t.River=false;
            Assert(!game.CanProduce(rival,garden),"gardens need a river"); var river=game.World.Get(new Hex(rival.Position.Q,rival.Position.R+1)); river.Terrain=Terrain.Grass; river.River=true;
            Assert(game.CanProduce(rival,garden),"river gardens use different prerequisites");
            var tools=new Game(14); Assert(tools.ToolStage==0,"initial hand-held stone"); tools.Technologies.Add("stone"); Assert(tools.ToolStage==1,"stone processing upgrades to hammer"); tools.Technologies.Add("mining"); Assert(tools.ToolStage==2,"mining upgrades to stone pick"); tools.Technologies.Add("metalworking"); Assert(tools.ToolStage==3,"metalworking upgrades to bronze pick");
            var pasture=new Game(22); var pastureCity=pasture.Cities[0]; var resource=pasture.World.Get(new Hex(pastureCity.Position.Q+1,pastureCity.Position.R)); resource.Resource=Resource.Cattle;
            Assert(pasture.RequiredOpening(Resource.Cattle)=="domestication","herding requires domestication"); var before=pasture.WorkIncome(pastureCity,resource.Hex,null); pasture.Technologies.Add("domestication"); pastureCity.Improvements[resource.Hex]="domestication";
            var after=pasture.WorkIncome(pastureCity,resource.Hex,null); Assert(after.Food-before.Food==2,"pasture worker gains two food");
            Assert(pasture.Profession(pastureCity,resource.Hex,null)=="Пастух","pasture assignment gives matching visual profession");
            Assert(Project(game,"slinger").Unit==UnitKind.Slinger,"first ranged project is a slinger");
        }
    }
}
