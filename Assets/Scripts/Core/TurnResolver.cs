using System;
using System.Collections.Generic;

namespace Origins
{
    public sealed class UnitMove
    {
        public readonly int UnitId, Tick;
        public readonly Hex From, To;
        public UnitMove(int id,int tick,Hex from,Hex to) { UnitId=id; Tick=tick; From=from; To=to; }
    }
    public sealed class TurnResolution
    {
        public readonly int Turn;
        public readonly IReadOnlyList<UnitMove> Moves;
        public readonly IReadOnlyList<string> Conflicts;
        public TurnResolution(int turn,List<UnitMove> moves,List<string> conflicts)
        { Turn=turn; Moves=moves.AsReadOnly(); Conflicts=conflicts.AsReadOnly(); }
    }
    public sealed partial class Game
    {
        sealed class CommittedUnit
        {
            public Unit Unit;
            public UnitKind Kind;
            public Hex[] Route;
            public bool Found, Wait, Stopped;
            public int Index, Due, Speed;
        }
        List<CommittedUnit> committed;
        TurnResolution resolved;
        public string ValidatePlans()
        {
            var ids=new HashSet<int>();
            foreach(var u in Units)
            {
                if(!ids.Add(u.Id)) return "Повтор идентификатора отряда";
                if(!u.HasOrder) return "Отряд без приказа";
                if(u.WaitOrder && (u.Order.Count>0 || u.FoundOrder)) return "Ожидание несовместимо с маршрутом";
                if(u.Order.Count>0)
                {
                    if(!u.Order[0].Equals(u.Position)) return "Маршрут начинается не у отряда";
                    int cost=0;
                    for(int i=1;i<u.Order.Count;i++)
                    {
                        var tile=World.Get(u.Order[i]);
                        if(tile==null || !tile.Walkable || !IsExplored(u.Owner,tile.Hex) || Hex.Distance(u.Order[i-1],u.Order[i])!=1) return "Маршрут содержит недоступный шаг";
                        cost+=tile.MoveCost;
                    }
                    if(cost>Movement(u)) return "Маршрут превышает очки движения";
                }
                Hex target=u.Order.Count>0 ? u.Order[u.Order.Count-1] : u.Position;
                if(u.FoundOrder && !CanFound(u,target)) return "Место поселения недоступно";
            }
            return null;
        }
        public bool BeginAction()
        {
            int previous=ActivePlayer;
            for(int p=0;p<Tribes.Count;p++) { SetPlayer(p); if(BlockingReason()!=null) { SetPlayer(previous); return false; } }
            SetPlayer(previous); if(ValidatePlans()!=null) return false;
            committed=new List<CommittedUnit>(); resolved=null;
            foreach(var u in Units)
            {
                var c=new CommittedUnit { Unit=u, Kind=u.Kind, Route=u.Order.ToArray(), Found=u.FoundOrder, Wait=u.WaitOrder, Speed=Movement(u) };
                c.Due=c.Route.Length>1 ? World.Get(c.Route[1]).MoveCost : int.MaxValue;
                committed.Add(c);
            }
            committed.Sort((a,b)=>a.Unit.Id.CompareTo(b.Unit.Id)); Phase=Phase.Action; return true;
        }
        public TurnResolution ResolveTurn()
        {
            if(Phase!=Phase.Action) return null;
            if(resolved!=null) return resolved;
            // Initiative has its own per-turn seed, independent of cards, animation and waiting.
            var initiative=new Random(unchecked(World.Seed^Turn*73856093^0x275143));
            var moves=new List<UnitMove>(); var conflicts=new List<string>();
            int ticks=0; foreach(var c in committed) ticks=Math.Max(ticks,c.Speed);
            for(int tick=1;tick<=ticks;tick++)
            {
                var winners=new Dictionary<Hex,CommittedUnit>(); var scores=new Dictionary<Hex,int>();
                foreach(var c in committed)
                {
                    if(c.Stopped || c.Due!=tick || c.Index+1>=c.Route.Length) continue;
                    Hex next=c.Route[c.Index+1]; int score=c.Speed*Balance.InitiativeWeight+initiative.Next(1,7);
                    CommittedUnit other;
                    if(!winners.TryGetValue(next,out other)) { winners.Add(next,c); scores.Add(next,score); }
                    else
                    {
                        bool wins=score>scores[next] || score==scores[next] && initiative.Next(2)==0;
                        var loser=wins ? other : c; loser.Stopped=true;
                        conflicts.Add(loser.Unit.Name+": клетку занял другой отряд, маршрут остановлен.");
                        if(wins) { winners[next]=c; scores[next]=score; }
                    }
                }
                // Departing friendly units can exchange cells or move as a chain. A stationary unit blocks entry.
                bool changed;
                do
                {
                    changed=false; var blocked=new List<Hex>();
                    foreach(var pair in winners)
                    {
                        foreach(var occupant in committed)
                        {
                            if(!occupant.Unit.Position.Equals(pair.Key) || occupant==pair.Value) continue;
                            bool leaves=false; foreach(var move in winners.Values) if(move==occupant) { leaves=true; break; }
                            if(!leaves) { blocked.Add(pair.Key); break; }
                        }
                    }
                    foreach(var cell in blocked)
                    {
                        winners[cell].Stopped=true; conflicts.Add(winners[cell].Unit.Name+": проход занят ожидающим отрядом.");
                        winners.Remove(cell); changed=true;
                    }
                } while(changed);
                // All accepted moves use the same starting occupancy; only then are positions changed.
                foreach(var c in committed)
                {
                    if(c.Stopped || c.Due!=tick || c.Index+1>=c.Route.Length) continue;
                    Hex next=c.Route[c.Index+1]; CommittedUnit winner;
                    if(!winners.TryGetValue(next,out winner) || winner!=c) continue;
                    moves.Add(new UnitMove(c.Unit.Id,tick,c.Unit.Position,next));
                }
                foreach(var move in moves) if(move.Tick==tick)
                {
                    var c=committed.Find(x=>x.Unit.Id==move.UnitId); c.Unit.Position=move.To; c.Index++;
                    if(c.Kind==UnitKind.Trader) World.AddRoad(move.From,move.To);
                    c.Due=c.Index+1<c.Route.Length ? tick+World.Get(c.Route[c.Index+1]).MoveCost : int.MaxValue;
                    Reveal(move.To,2,c.Unit.Owner); var tile=World.Get(move.To);
                    if(tile.Resource==Resource.Discovery && !tile.DiscoveryTaken)
                    { tile.DiscoveryTaken=true; Tribes[c.Unit.Owner].Knowledge+=3; Tribes[c.Unit.Owner].Journal.Insert(0,"Разведчики нашли древнюю стоянку: +3 знания."); }
                }
            }
            foreach(var c in committed)
            {
                c.Unit.Order.Clear(); c.Unit.Order.AddRange(c.Route); c.Unit.FoundOrder=c.Found; c.Unit.WaitOrder=c.Wait;
            }
            foreach(var message in conflicts) Log(message);
            ResolveEconomy(); resolved=new TurnResolution(Turn,moves,conflicts); return resolved;
        }
    }
}
