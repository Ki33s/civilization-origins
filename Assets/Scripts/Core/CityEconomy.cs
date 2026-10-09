using System;
using System.Collections.Generic;

namespace Origins
{
    // Experimental values, not final rules. The player loads the JSON demo profile.
    [Serializable]
    public sealed class DemoBalance
    {
        public int CenterFood=1, CenterProduction=4, CenterScience=3;
        public int CenterCulture=3, InitialBorderCost=12, BorderCostStep=6, InitiativeWeight=3;
        public int FoodPerResident=2, InitialGrowthCost=10, GrowthCostStep=4;
        public int BaseFoodCapacity=24, GranaryCapacityBonus=5;
        public int BasePopulationCapacity=4, HousingBonus=2;
        public int StartYear=-10000, YearsPerTurn=100;
    }
    public enum CityFocus { Food, Production }
    public sealed class ResidentJob
    {
        public int ResidentId;
        public Hex Position;
        public string BuildingId;
        public string Profession;
    }
    public sealed partial class Game
    {
        public int CalendarYear { get { return Balance.StartYear+(Turn-1)*Balance.YearsPerTurn; } }
        public int GrowthCost(City city) { return Balance.InitialGrowthCost+(city.Population-1)*Balance.GrowthCostStep; }
        public int FoodCapacity(City city) { return Balance.BaseFoodCapacity+(city.Buildings.Contains("granary") ? Balance.GranaryCapacityBonus : 0); }
        public int PopulationCapacity(City city) { return Balance.BasePopulationCapacity+(city.Buildings.Contains("huts") ? Balance.HousingBonus : 0); }
        public bool PlanFocus(City city,CityFocus focus)
        {
            if(Phase!=Phase.Planning || !Cities.Contains(city)) return false;
            city.PlannedFocus=focus; return true;
        }
        public string RequiredOpening(Resource resource)
        {
            switch(resource)
            {
                case Resource.Stone:return "stone"; case Resource.Grain:return "farming";
                case Resource.Game:return "hunting"; case Resource.Fish:return "fishing";
                case Resource.Furs:return "needle";
                case Resource.Cattle:return "domestication";
                default:return null;
            }
        }
        public Yield GrossIncome(City city)
        {
            int food=Balance.CenterFood, production=Balance.CenterProduction, science=Balance.CenterScience,gold=0;
            foreach(var job in city.Workers)
            {
                Yield y=WorkIncome(city,job.Position,job.BuildingId,false);
                food+=y.Food; production+=y.Production; science+=y.Science;
                gold+=y.Gold;
            }
            if(city.Buildings.Contains("hearth")) food++;
            food+=PastureNeighbourBonus(city,city.Position);
            int culture=CityCultureIncome(city);
            foreach(var project in Content.Projects) if(city.Buildings.Contains(project.Id)) { food+=project.BonusFood; production+=project.BonusProduction; science+=project.BonusScience; culture+=project.BonusCulture; }
            return new Yield(food,production,science,culture,gold);
        }
        public Yield WorkIncome(City city,Hex hex,string building,bool prospective=false)
        {
            if(building!=null)
            {
                if(building=="workshop") return new Yield(0,2,0);
                if(building=="potter") return new Yield(0,1,0);
                if(building=="fishhut") return new Yield(2,0,0);
                return new Yield(0,0,0);
            }
            var tile=World.Get(hex); if(tile==null) return new Yield(0,0,0);
            int food=0,production=0,gold=0;
            switch(tile.Terrain)
            {
                case Terrain.Grass:food=2; production=1; break;
                case Terrain.Plains:
                case Terrain.Swamp:food=1; production=1; break;
                case Terrain.Tundra:production=1; break;
                case Terrain.Forest:food=1; production=2; break;
                case Terrain.Hills:production=2; break;
                case Terrain.Desert:production=1; break;
                case Terrain.Coast:food=2; break;
            }
            if(tile.River) food++;
            string opening=RequiredOpening(tile.Resource);
            if(opening!=null && HasOpening(opening) && (prospective || city.Improvements.ContainsKey(hex)))
            {
                switch(tile.Resource)
                {
                    case Resource.Grain:food+=3; break;
                    case Resource.Fish:food+=2; break;
                    case Resource.Game:food+=2; production++; break;
                    case Resource.Stone:production+=2; break;
                    case Resource.Furs:production++; gold++; break;
                    case Resource.Cattle:food+=2; break;
                }
            }
            return new Yield(food+PastureNeighbourBonus(city,hex),production,0,0,gold);
        }
        string BuildingAt(City city,Hex hex)
        {
            foreach(var pair in city.BuildingSites) if(pair.Value.Equals(hex)) return pair.Key;
            return null;
        }
        bool HasWorkplace(string building) { return building=="workshop" || building=="potter" || building=="fishhut"; }
        bool AvailableWork(City city,Hex hex,out string building)
        {
            building=BuildingAt(city,hex);
            if(hex.Equals(city.Position) || !city.Territory.Contains(hex)) return false;
            if(building!=null) return HasWorkplace(building);
            Tile t=World.Get(hex);
            return t!=null && (t.Walkable || t.Terrain==Terrain.Coast && Technologies.Contains("fishing"));
        }
        public void AssignResidents(City city)
        {
            var occupied=new HashSet<Hex>();
            city.Workers.RemoveAll(job=>job.ResidentId>=city.Population);
            city.Workers.RemoveAll(job=>
            {
                string building;
                if(!AvailableWork(city,job.Position,out building) || !occupied.Add(job.Position)) return true;
                job.BuildingId=building; job.Profession=Profession(city,job.Position,building); return false;
            });
            for(int id=0;id<city.Population;id++)
            {
                bool assigned=false; foreach(var job in city.Workers) if(job.ResidentId==id) assigned=true;
                if(assigned) continue;
                var options=new List<Hex>(city.Territory);
                options.Sort((a,b)=>a.R==b.R ? a.Q.CompareTo(b.Q) : a.R.CompareTo(b.R));
                Hex best=city.Position; string bestBuilding=null; int bestScore=int.MinValue;
                bool needFood=GrossIncome(city).Food<city.Population*Balance.FoodPerResident+2;
                foreach(var hex in options)
                {
                    string building;
                    if(occupied.Contains(hex) || !AvailableWork(city,hex,out building)) continue;
                    Yield y=WorkIncome(city,hex,building,true);
                    int score=y.Food*(needFood ? 12 : city.Focus==CityFocus.Food ? 4 : 1)+y.Production*(city.Focus==CityFocus.Production ? 5 : 2)+y.Science;
                    if(score>bestScore) { bestScore=score; best=hex; bestBuilding=building; }
                }
                if(best.Equals(city.Position)) continue;
                string required=RequiredOpening(World.Get(best).Resource);
                if(bestBuilding==null && required!=null && HasOpening(required) && !city.Improvements.ContainsKey(best))
                {
                    city.Improvements.Add(best,required);
                    World.ConnectRoad(city.Position,best);
                    Log(city.Name+": житель проложил тропу и освоил ресурс.");
                }
                city.Workers.Add(new ResidentJob{ResidentId=id,Position=best,BuildingId=bestBuilding,Profession=Profession(city,best,bestBuilding)});
                occupied.Add(best);
            }
        }
        public string Profession(City city,Hex h,string building)
        {
            if(building=="workshop") return "Каменотёс";
            if(building=="potter") return "Гончар";
            if(building=="fishhut") return "Рыбак";
            string improvement;
            if(city.Improvements.TryGetValue(h,out improvement))
            {
                switch(improvement) { case "stone":return "Каменотёс"; case "farming":return "Земледелец"; case "fishing":return "Рыбак"; case "hunting":return "Охотник"; case "needle":return "Обработчик шкур"; case "domestication":return "Пастух"; }
            }
            return World.Get(h).Terrain==Terrain.Forest ? "Лесной собиратель" : "Собиратель";
        }
        public bool FindBuildingSite(City city,Choice choice,out Hex site)
        {
            site=city.Position;
            var ordered=new List<Hex>(city.Territory);
            ordered.Sort((a,b)=>
            {
                int ar=World.Get(a).Resource==Resource.None ? 0 : 1, br=World.Get(b).Resource==Resource.None ? 0 : 1;
                return ar!=br ? ar.CompareTo(br) : a.R==b.R ? a.Q.CompareTo(b.Q) : a.R.CompareTo(b.R);
            });
            foreach(var h in ordered)
            {
                if(h.Equals(city.Position) || BuildingAt(city,h)!=null || city.Improvements.ContainsKey(h)) continue;
                Tile t=World.Get(h);
                if(choice.Id=="fishhut" ? t.Terrain!=Terrain.Coast || t.Resource!=Resource.Fish : !t.Walkable) continue;
                if(!ValidProjectSite(choice,t)) continue;
                site=h; return true;
            }
            return false;
        }
    }
}
