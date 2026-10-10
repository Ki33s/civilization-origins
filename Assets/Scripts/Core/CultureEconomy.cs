using System;
using System.Collections.Generic;

namespace Origins
{
    public sealed partial class Game
    {
        public bool HasOpening(string id) { return Technologies.Contains(id) || CulturalOpenings.Contains(id); }
        public int BorderCost(City city) { return Balance.InitialBorderCost+city.BorderExpansions*Balance.BorderCostStep; }
        public int CityCultureIncome(City city)
        {
            int culture=Balance.CenterCulture;
            if(CulturalOpenings.Contains("oral")) culture++;
            if(CulturalOpenings.Contains("caveart")) foreach(var tile in World.Neighbours(city.Position)) if(tile.Terrain==Terrain.Mountain) culture++;
            return culture;
        }
        void ExpandCulture(City city)
        {
            if(city.BorderCulture<BorderCost(city)) return;
            var candidates=new HashSet<Hex>();
            foreach(var owned in city.Territory) foreach(var tile in World.Neighbours(owned))
            {
                if(!tile.Walkable || city.Territory.Contains(tile.Hex)) continue;
                bool claimed=false; foreach(var other in Cities) if(other.Territory.Contains(tile.Hex)) { claimed=true; break; }
                if(!claimed) candidates.Add(tile.Hex);
            }
            var sorted=new List<Hex>(candidates); sorted.Sort((a,b)=>a.R==b.R ? a.Q.CompareTo(b.Q) : a.R.CompareTo(b.R));
            if(sorted.Count==0) return;
            Hex best=sorted[0]; int score=int.MinValue;
            foreach(var h in sorted)
            {
                var y=WorkIncome(city,h,null,true); int value=y.Food*3+y.Production+y.Gold*2;
                if(value>score) { best=h; score=value; }
            }
            city.BorderCulture-=BorderCost(city); city.BorderExpansions++; city.Territory.Add(best);
            Reveal(best,1); Log(city.Name+": культура расширила территорию на одну клетку.");
        }
    }
}
