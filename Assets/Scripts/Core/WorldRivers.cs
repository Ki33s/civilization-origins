using System;
using System.Collections.Generic;

namespace Origins
{
    public sealed partial class World
    {
        void GenerateDrainageRivers()
        {
            // A drainage forest rooted in ocean cells guarantees an outlet and cannot contain cycles.
            var costs=new Dictionary<Hex,float>();
            var downstream=new Dictionary<Hex,Hex>();
            var open=new List<Hex>(); var settled=new HashSet<Hex>();
            foreach(var tile in Ordered) if(tile.Terrain==Terrain.Ocean) { costs[tile.Hex]=0; open.Add(tile.Hex); }
            while(open.Count>0)
            {
                int best=0; for(int i=1;i<open.Count;i++) if(costs[open[i]]<costs[open[best]]) best=i;
                Hex h=open[best]; open.RemoveAt(best); if(!settled.Add(h)) continue;
                foreach(var n in Neighbours(h))
                {
                    if(settled.Contains(n.Hex)) continue;
                    float uphill=Math.Max(0,Get(h).Elevation-n.Elevation);
                    float next=costs[h]+1+uphill*40+(n.Terrain==Terrain.Mountain ? 30 : 0);
                    float old; if(costs.TryGetValue(n.Hex,out old) && old<=next) continue;
                    costs[n.Hex]=next; downstream[n.Hex]=h; open.Add(n.Hex);
                }
            }
            Action<Hex> trace=source=>
            {
                Hex h=source;
                while(downstream.ContainsKey(h))
                {
                    Get(h).River=true;
                    Hex next=downstream[h]; AddRiverEdge(h,next); h=next;
                }
                Get(h).River=true;
            };
            Hex startingSource=Start; int upstreamLength=0;
            foreach(var tile in Ordered)
            {
                if(!tile.Walkable || !downstream.ContainsKey(tile.Hex)) continue;
                Hex cursor=tile.Hex; int steps=0;
                while(downstream.ContainsKey(cursor) && !cursor.Equals(Start)) { cursor=downstream[cursor]; steps++; }
                if(cursor.Equals(Start) && steps>upstreamLength) { startingSource=tile.Hex; upstreamLength=steps; }
            }
            trace(startingSource);
            var sources=new List<Tile>(Ordered);
            sources.Sort((a,b)=> { int c=b.Elevation.CompareTo(a.Elevation); return c!=0 ? c : a.Hex.R!=b.Hex.R ? a.Hex.R.CompareTo(b.Hex.R) : a.Hex.Q.CompareTo(b.Hex.Q); });
            int count=1;
            foreach(var source in sources)
            {
                if(count>=5) break;
                if(source.Elevation<.38f || source.River) continue;
                bool nearby=false; foreach(var tile in Ordered) if(tile.River && Hex.Distance(tile.Hex,source.Hex)<4) { nearby=true; break; }
                if(nearby || !downstream.ContainsKey(source.Hex)) continue;
                trace(source.Hex); count++;
            }
        }
    }
}
