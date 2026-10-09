namespace Origins
{
    public sealed partial class Game
    {
        public bool WonderBuilt(string id)
        {
            foreach(var city in Cities) if(city.Buildings.Contains(id)) return true;
            return false;
        }
        public bool CanProduce(City city,Choice project)
        {
            if(city.Buildings.Contains(project.Id) || project.Requires!=null && !HasOpening(project.Requires) || city.Population<project.MinPopulation) return false;
            foreach(var required in project.RequiresAll) if(!HasOpening(required)) return false;
            if(project.Wonder && WonderBuilt(project.Id)) return false;
            if(project.Id=="settler" && city.Population<=1) return false;
            Hex site;
            return project.Unit.HasValue || FindBuildingSite(city,project,out site);
        }
        bool ValidProjectSite(Choice project,Tile tile)
        {
            if(project.SiteRule=="desert") return tile.Terrain==Terrain.Desert;
            if(project.SiteRule=="river") return tile.River && (tile.Terrain==Terrain.Grass || tile.Terrain==Terrain.Plains);
            if(project.SiteRule=="near-stone")
            {
                if(tile.Resource==Resource.Stone) return true;
                foreach(var neighbour in World.Neighbours(tile.Hex)) if(neighbour.Resource==Resource.Stone) return true;
                return false;
            }
            return true;
        }
        int PastureNeighbourBonus(City city,Hex hex)
        {
            int bonus=0;
            foreach(var job in city.Workers)
            {
                var tile=World.Get(job.Position);
                if(job.BuildingId==null && tile.Resource==Resource.Cattle && city.Improvements.ContainsKey(tile.Hex) && Hex.Distance(tile.Hex,hex)==1) bonus++;
            }
            return bonus;
        }
        public int ToolStage { get { return Technologies.Contains("metalworking") ? 3 : Technologies.Contains("mining") ? 2 : Technologies.Contains("stone") ? 1 : 0; } }
    }
}
