using System;
using System.Collections.Generic;

namespace Origins
{
    public struct Hex : IEquatable<Hex>
    {
        public readonly int Q, R;
        public Hex(int q, int r) { Q = q; R = r; }
        public bool Equals(Hex other) { return Q == other.Q && R == other.R; }
        public override bool Equals(object obj) { return obj is Hex && Equals((Hex)obj); }
        public override int GetHashCode() { return Q * 397 ^ R; }
        public static int Distance(Hex a, Hex b) { return (Math.Abs(a.Q-b.Q) + Math.Abs(a.R-b.R) + Math.Abs(a.Q+a.R-b.Q-b.R)) / 2; }
        public override string ToString() { return Q + ", " + R; }
        public static readonly Hex[] Directions = { new Hex(1,0), new Hex(1,-1), new Hex(0,-1), new Hex(-1,0), new Hex(-1,1), new Hex(0,1) };
    }

    public enum Terrain { Ocean, Coast, Grass, Forest, Hills, Mountain, Desert, Plains, Swamp, Tundra }
    public enum Resource { None, Berries, Game, Stone, Grain, Fish, Discovery, Furs, Cattle }
    public struct RiverEdge
    {
        public Hex From, To;
        public RiverEdge(Hex from,Hex to) { From=from; To=to; }
    }

    public sealed class Tile
    {
        public Hex Hex;
        public Terrain Terrain;
        public Resource Resource;
        public bool Explored, River, DiscoveryTaken;
        public float Elevation;
        public bool Walkable { get { return Terrain != Terrain.Ocean && Terrain != Terrain.Coast && Terrain != Terrain.Mountain; } }
        public int MoveCost { get { return Terrain == Terrain.Forest || Terrain == Terrain.Hills || Terrain == Terrain.Swamp || Terrain == Terrain.Tundra ? 2 : 1; } }
    }

    public sealed partial class World
    {
        public readonly Dictionary<Hex, Tile> Tiles = new Dictionary<Hex, Tile>();
        public readonly List<Tile> Ordered = new List<Tile>();
        public readonly List<RiverEdge> RiverEdges = new List<RiverEdge>();
        readonly HashSet<string> riverKeys = new HashSet<string>();
        public readonly int Seed, Width, Height;
        public Hex Start;
        public World(int seed, int width = 28, int height = 20)
        {
            Seed = seed; Width = width; Height = height;
            for (int y=0; y<height; y++) for (int x=0; x<width; x++)
            {
                var h = new Hex(x-y/2,y);
                float edge = (float)Math.Sqrt(Math.Pow((x-width*.5)/(width*.56),2)+Math.Pow((y-height*.5)/(height*.59),2));
                float e = Noise(x*.13f,y*.13f,seed) * .68f + Noise(x*.31f,y*.31f,seed+19) * .23f - edge*.43f;
                float moisture = Noise(x*.19f,y*.19f,seed+913);
                var t = new Tile { Hex=h, Elevation=e };
                float latitude=Math.Abs((y-height*.5f)/(height*.5f));
                t.Terrain = e < .12f ? Terrain.Ocean : e < .19f ? Terrain.Coast : e > .56f ? Terrain.Mountain :
                    latitude>.58f ? Terrain.Tundra : e > .46f ? Terrain.Hills : moisture>.59f && e<.36f ? Terrain.Swamp :
                    moisture > .54f ? Terrain.Forest : moisture < .29f ? Terrain.Desert : moisture<.42f ? Terrain.Plains : Terrain.Grass;
                float roll = Hash(x,y,seed+765);
                if (t.Terrain == Terrain.Coast && roll>.73f) t.Resource=Resource.Fish;
                else if (t.Walkable && roll>.65f)
                    t.Resource = t.Terrain == Terrain.Forest || t.Terrain==Terrain.Tundra ? (roll>.82f ? Resource.Furs : Resource.Game) : t.Terrain == Terrain.Hills ? Resource.Stone : roll>.91f ? Resource.Discovery : roll>.8f ? Resource.Grain : Resource.Berries;
                Tiles.Add(h,t); Ordered.Add(t);
            }
            foreach(var t in Ordered) if(t.Terrain==Terrain.Plains && t.Resource==Resource.Berries && Hash(t.Hex.Q,t.Hex.R,seed+411)>.45f) t.Resource=Resource.Cattle;
            Start = new Hex(width/2-height/4,height/2);
            // A fertile connected start is guaranteed, even on extreme seeds.
            foreach (var t in Ordered) if (Hex.Distance(t.Hex,Start)<=3)
            {
                t.Terrain = Hex.Distance(t.Hex,Start)==3 && Hash(t.Hex.Q,t.Hex.R,seed)>.5f ? Terrain.Forest : Terrain.Grass;
                t.Elevation=.3f;
                if(t.Resource==Resource.Fish) t.Resource=Resource.Berries;
            }
            Get(Start).Resource=Resource.None;
            Get(new Hex(Start.Q+1,Start.R)).Resource=Resource.Grain;
            Get(new Hex(Start.Q-1,Start.R)).Resource=Resource.Berries;
            EnsureExpansionRoute();
            MakeHighlands();
            EnsureBiomeVariety();
            bool cattle=false; foreach(var t in Ordered) if(t.Resource==Resource.Cattle) cattle=true;
            if(!cattle) foreach(var t in Ordered) if(t.Terrain==Terrain.Plains && Hex.Distance(t.Hex,Start)>3) { t.Resource=Resource.Cattle; break; }
            GenerateDrainageRivers();
            Reveal(Start,3);
        }

        public Tile Get(Hex h) { Tile t; return Tiles.TryGetValue(h,out t) ? t : null; }
        public IEnumerable<Tile> Neighbours(Hex h)
        {
            foreach (var d in Hex.Directions) { var t=Get(new Hex(h.Q+d.Q,h.R+d.R)); if(t!=null) yield return t; }
        }
        public void Reveal(Hex center, int radius) { foreach(var t in Ordered) if(Hex.Distance(center,t.Hex)<=radius) t.Explored=true; }

        public Dictionary<Hex,int> Reachable(Hex start, int budget)
        {
            var costs = new Dictionary<Hex,int> { {start,0} };
            var open = new List<Hex> { start };
            while(open.Count>0)
            {
                Hex h=PopCheapest(open,costs);
                foreach(var t in Neighbours(h))
                {
                    if(!t.Walkable || !t.Explored) continue;
                    int c=costs[h]+t.MoveCost, old;
                    if(c>budget || costs.TryGetValue(t.Hex,out old) && old<=c) continue;
                    costs[t.Hex]=c; open.Add(t.Hex);
                }
            }
            return costs;
        }
        public List<Hex> Path(Hex start, Hex end, int budget)
        {
            var costs=new Dictionary<Hex,int>{{start,0}};
            var previous=new Dictionary<Hex,Hex>();
            var open=new List<Hex>{start};
            while(open.Count>0)
            {
                Hex h=PopCheapest(open,costs);
                if(h.Equals(end))
                {
                    var result=new List<Hex>{end};
                    while(!h.Equals(start)) { h=previous[h]; result.Add(h); }
                    result.Reverse(); return result;
                }
                foreach(var t in Neighbours(h))
                {
                    if(!t.Explored || !t.Walkable) continue;
                    int c=costs[h]+t.MoveCost,old;
                    if(c>budget || costs.TryGetValue(t.Hex,out old) && old<=c) continue;
                    costs[t.Hex]=c; previous[t.Hex]=h; open.Add(t.Hex);
                }
            }
            return new List<Hex>();
        }
        static Hex PopCheapest(List<Hex> open, Dictionary<Hex,int> costs)
        {
            int best=0; for(int i=1;i<open.Count;i++) if(costs[open[i]]<costs[open[best]]) best=i;
            Hex h=open[best]; open.RemoveAt(best); return h;
        }
        void AddRiverEdge(Hex a,Hex b)
        {
            bool sorted=a.R<b.R || a.R==b.R && a.Q<b.Q;
            string key=sorted ? a+"/"+b : b+"/"+a;
            if(riverKeys.Add(key)) RiverEdges.Add(new RiverEdge(a,b));
        }
        void EnsureExpansionRoute()
        {
            var open=new Queue<Hex>(); open.Enqueue(Start);
            var visited=new HashSet<Hex>{Start};
            while(open.Count>0)
            {
                Hex h=open.Dequeue();
                if(Hex.Distance(Start,h)>=4) return;
                foreach(var t in Neighbours(h)) if(t.Walkable && visited.Add(t.Hex)) open.Enqueue(t.Hex);
            }
            // Rare isolated starts receive a small land bridge for the second settlement.
            for(int i=1;i<=5;i++)
            {
                Tile t=Get(new Hex(Start.Q+i,Start.R));
                t.Terrain=Terrain.Grass; t.Elevation=.3f;
                if(t.Resource==Resource.Fish) t.Resource=Resource.Berries;
            }
        }
        void MakeHighlands()
        {
            // Keep a recognisable mountain range on each demo map, away from the fertile start.
            Tile source=null; float best=float.NegativeInfinity;
            foreach(var t in Ordered)
            {
                if(!t.Walkable || Hex.Distance(t.Hex,Start)<6) continue;
                int land=0; foreach(var n in Neighbours(t.Hex)) if(n.Walkable) land++;
                if(land<4) continue;
                float score=t.Elevation+Hash(t.Hex.Q,t.Hex.R,Seed+222)*.035f;
                if(score>best) { source=t; best=score; }
            }
            if(source==null) return;
            var ridge=new List<Tile>{source};
            for(int step=0;step<2;step++)
            {
                Tile next=null;
                foreach(var n in Neighbours(ridge[ridge.Count-1].Hex))
                    if(n.Walkable && !ridge.Contains(n) && Hex.Distance(n.Hex,Start)>=5 && (next==null || n.Elevation>next.Elevation)) next=n;
                if(next!=null) ridge.Add(next);
            }
            foreach(var peak in ridge)
            {
                peak.Terrain=Terrain.Mountain; peak.Elevation=.64f; peak.Resource=Resource.None;
                foreach(var n in Neighbours(peak.Hex)) if(n.Walkable && !ridge.Contains(n) && Hex.Distance(n.Hex,Start)>3)
                { n.Terrain=Terrain.Hills; n.Elevation=Math.Max(n.Elevation,.49f); if(n.Resource!=Resource.None) n.Resource=Resource.Stone; }
            }
        }
        void EnsureBiomeVariety()
        {
            foreach(var kind in new[]{Terrain.Grass,Terrain.Forest,Terrain.Plains,Terrain.Desert,Terrain.Swamp,Terrain.Tundra})
            {
                bool present=false; foreach(var t in Ordered) if(t.Terrain==kind) { present=true; break; }
                if(present) continue;
                var counts=new Dictionary<Terrain,int>(); foreach(var t in Ordered) { if(!counts.ContainsKey(t.Terrain)) counts.Add(t.Terrain,0); counts[t.Terrain]++; }
                Tile selected=null; float best=float.NegativeInfinity;
                foreach(var t in Ordered)
                {
                    if(!t.Walkable || t.Terrain==Terrain.Hills || Hex.Distance(t.Hex,Start)<5 || counts[t.Terrain]<5) continue;
                    float latitude=Math.Abs((t.Hex.R-Height*.5f)/(Height*.5f));
                    float moisture=Noise((t.Hex.Q+t.Hex.R/2)*.19f,t.Hex.R*.19f,Seed+913);
                    float score=kind==Terrain.Tundra ? latitude : kind==Terrain.Swamp ? moisture-t.Elevation : kind==Terrain.Desert ? 1-moisture-latitude : kind==Terrain.Forest ? moisture : 1-Math.Abs(moisture-.43f);
                    if(score>best) { best=score; selected=t; }
                }
                if(selected==null) continue;
                selected.Terrain=kind; selected.Resource=kind==Terrain.Tundra || kind==Terrain.Forest ? Resource.Furs : Resource.None;
                // A small adjacent patch avoids a lone replacement hex where land allows it.
                foreach(var n in Neighbours(selected.Hex)) if(n.Walkable && n.Terrain!=Terrain.Hills && Hex.Distance(n.Hex,Start)>=5 && counts[n.Terrain]>5)
                { counts[n.Terrain]--; n.Terrain=kind; n.Resource=Resource.None; break; }
            }
        }
        static float Hash(int x,int y,int seed)
        {
            unchecked { uint n=(uint)(x*374761393+y*668265263+seed*1442695041); n=(n^(n>>13))*1274126177; return (n^(n>>16))/(float)uint.MaxValue; }
        }
        static float Noise(float x,float y,int seed)
        {
            int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);
            float fx=x-ix,fy=y-iy; fx=fx*fx*(3-2*fx); fy=fy*fy*(3-2*fy);
            float a=Hash(ix,iy,seed),b=Hash(ix+1,iy,seed),c=Hash(ix,iy+1,seed),d=Hash(ix+1,iy+1,seed);
            return (a+(b-a)*fx)*(1-fy)+(c+(d-c)*fx)*fy;
        }
    }
}
