using System.Collections.Generic;

namespace Origins
{
    public struct RoadEdge
    {
        public Hex From,To;
        public RoadEdge(Hex from,Hex to) { From=from; To=to; }
    }
    public sealed partial class World
    {
        public readonly List<RoadEdge> RoadEdges=new List<RoadEdge>();
        readonly HashSet<string> roadKeys=new HashSet<string>();
        static string RoadKey(Hex a,Hex b) { return a.R<b.R || a.R==b.R && a.Q<b.Q ? a+"/"+b : b+"/"+a; }
        public bool HasRoad(Hex a,Hex b) { return roadKeys.Contains(RoadKey(a,b)); }
        public bool AddRoad(Hex a,Hex b)
        {
            if(Hex.Distance(a,b)!=1 || Get(a)==null || Get(b)==null || !Get(a).Walkable || !Get(b).Walkable) return false;
            if(!roadKeys.Add(RoadKey(a,b))) return false;
            RoadEdges.Add(new RoadEdge(a,b)); return true;
        }
        public void ConnectRoad(Hex a,Hex b)
        {
            var path=Path(a,b,Width*Height*2);
            for(int i=1;i<path.Count;i++) AddRoad(path[i-1],path[i]);
        }
    }
}
