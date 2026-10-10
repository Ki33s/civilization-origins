using System;
using UnityEngine;
namespace Origins.Editor
{
    public static class MultiplayerChecks
    {
        static void Check(bool ok,string what) { if(!ok) throw new Exception("Online check: "+what); }
        public static void Ready(Game g)
        {
            while(g.ResearchReady) { foreach(var c in g.ResearchOffers.ToArray()) if(g.Knowledge>=c.Cost) { g.ChooseResearch(c.Id); break; } }
            while(g.CultureReady) { foreach(var c in g.CultureOffers.ToArray()) if(g.CultureStock>=c.Cost) { g.ChooseCulture(c.Id); break; } }
            foreach(var c in g.OwnCities) if(c.Production==null && c.Offers.Count>0) g.ChooseProduction(c,c.Offers[0].Id);
            foreach(var u in g.OwnUnits) g.PlanWait(u);
        }
        public static void Run()
        {
            ContentLoader.Load();
            var local=new Game(12026,players:2); var server=new Game(12026,players:2);
            Check(local.Cities.Count==2 && local.Units.Count==2,"two independent tribes");
            var first=local.Cities[0]; var second=local.Cities[1];
            Check(Hex.Distance(first.Position,second.Position)>=8,"separate connected starting valleys");
            Check(local.IsExplored(0,first.Position) && !local.IsExplored(0,second.Position),"private exploration");
            Check(!local.PlanWait(local.Units[1]) && !local.ChooseProduction(second,second.Offers[0].Id),"ownership validation");
            for(int i=0;i<18;i++)
            {
                var plans=new PlayerPlan[2];
                for(int p=0;p<2;p++) { local.SetPlayer(p); Ready(local); plans[p]=JsonUtility.FromJson<PlayerPlan>(JsonUtility.ToJson(local.CapturePlan())); }
                Check(server.ApplyPlan(plans[0]) && server.ApplyPlan(plans[1]),"serialized plans accepted");
                Check(local.BeginAction() && server.BeginAction(),"both ready");
                local.FinishAction(); server.FinishAction(); Check(local.Checksum()==server.Checksum(),"client/server replay matches at turn "+local.Turn);
                local.SetPlayer(0); Check(local.OwnCities.Count>=1 && local.OwnUnits.Count>=1,"own roster");
            }
            Debug.Log("ORIGINS ONLINE: multiplayer rules checks passed");
        }
    }
}
