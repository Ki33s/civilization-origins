using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Origins
{
    public sealed partial class OriginsController
    {
        string smokeOutput;
        bool smokeFailed;
        void StartSmokeIfRequested()
        {
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++) if(args[i]=="-origins-ui-checks")
            {
                NewRun(12026); StartCoroutine(CaptureDevelopmentUI(args[i+1])); return;
            }
            if(Array.IndexOf(args,"-origins-play")>=0) { NewRun(12026); StartCoroutine(CapturePlayablePreview()); return; }
            for(int i=0;i<args.Length-1;i++) if(args[i]=="-origins-smoke")
            {
                smokeOutput=args[i+1]; Directory.CreateDirectory(smokeOutput);
                Application.logMessageReceived+=SmokeLog;
                StartCoroutine(SmokeRoutine()); return;
            }
        }
        IEnumerator CapturePlayablePreview()
        {
            yield return new WaitForSeconds(2);
            // Application.dataPath is Builds/Windows/Origins_Data in the player.
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../TestResults/CurrentPreview"));
            Directory.CreateDirectory(output);
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"game-ui.png"));
        }
        IEnumerator CaptureDevelopmentUI(string folder)
        {
            Directory.CreateDirectory(folder);
            yield return new WaitForSeconds(2);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"game-start-ui.png"));
            for(int turn=0;turn<3;turn++) AdvanceShowcaseTurn();
            view.Sync(); choiceWindow=1;
            yield return new WaitForSeconds(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"science-ui.png"));
            yield return new WaitForSeconds(1);
            Game.ChooseResearch(Game.ResearchOffers[0].Id); choiceWindow=3;
            yield return new WaitForSeconds(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"culture-ui.png"));
            yield return new WaitForSeconds(1);
            Game.ChooseCulture(Game.CultureOffers[0].Id); choiceWindow=0; view.Sync();
            yield return new WaitForSeconds(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"game-developed-ui.png"));
            yield return new WaitForSeconds(2); Debug.Log("ORIGINS: gameplay UI captures completed"); Application.Quit();
        }
        void SmokeLog(string message,string trace,LogType type)
        {
            if(type!=LogType.Exception && type!=LogType.Error && type!=LogType.Assert) return;
            smokeFailed=true; File.AppendAllText(Path.Combine(smokeOutput,"errors.txt"),message+"\n"+trace+"\n");
        }
        void SmokeAssert(bool condition,string name)
        {
            File.AppendAllText(Path.Combine(smokeOutput,"checks.txt"),(condition ? "PASS " : "FAIL ")+name+"\n");
            if(!condition) smokeFailed=true;
        }
        IEnumerator SmokeRoutine()
        {
            // This path is activated only by an explicit validation command-line argument.
            yield return new WaitForSeconds(1);
            CaptureWorld("world-start.png");
            yield return new WaitForSeconds(.5f);
            NewRun(12026); // NewRun cancels coroutines, so launch the remaining checks separately.
            StartCoroutine(SmokeGame());
        }
        IEnumerator SmokeGame()
        {
            yield return new WaitForSeconds(1);
            CaptureWorld("world-planning.png");
            SmokeAssert(Game.World.Ordered.Count==560,"560 rendered world tiles");
            SmokeAssert(view.ArtworkTileCount==560,"every world tile uses the approved artwork library");
            bool picking=true;
            foreach(var tile in Game.World.Ordered) if(tile.Explored)
            {
                RaycastHit hit;
                if(!Physics.Raycast(view.Position(tile.Hex)+Vector3.up*10,Vector3.down,out hit,20) || hit.collider.GetComponent<TileView>()?.Tile!=tile) picking=false;
            }
            SmokeAssert(picking,"rendered terrain colliders select the correct logical hex");
            SmokeAssert(cameraView!=null && cameraView.isActiveAndEnabled,"active camera");
            SmokeAssert(Game.World.Get(Game.World.Start).River,"starting freshwater");
            SmokeAssert(Game.Cities[0].Population==1 && Game.Cities[0].Workers.Count==1,"one resident with a real assignment");
            if(Game.ResearchReady) Game.ChooseResearch(Game.ResearchOffers[0].Id);
            Game.ChooseProduction(Game.Cities[0],Game.Cities[0].Offers[0].Id);
            Unit scout=Game.Units[0]; Hex destination=new Hex(scout.Position.Q+2,scout.Position.R);
            SelectUnit(scout);
            SmokeAssert(Game.PlanMove(scout,destination),"plan a two-cell scout route"); view.ShowOrders(scout);
            yield return new WaitForSeconds(.5f);
            CaptureWorld("world-orders.png");
            Execute();
            float deadline=Time.realtimeSinceStartup+12;
            while(Game.Phase==Phase.Action && Time.realtimeSinceStartup<deadline) yield return null;
            SmokeAssert(Game.Phase==Phase.Planning && Game.Turn==2,"animated action returns to planning");
            SmokeAssert(scout.Position.Equals(destination),"model reaches destination");
            SmokeAssert(Vector3.Distance(view.UnitTransform(scout).position,view.Position(destination))<.1f,"visual matches model position");
            yield return new WaitForSeconds(.5f);
            CaptureWorld("world-turn-two.png");
            // Zoom to the village to verify residents and procedural buildings visually.
            SelectCity(Game.Cities[0]); zoom=4.5f; UpdateCamera();
            yield return new WaitForSeconds(1);
            CaptureWorld("village.png");
            for(int turn=0;turn<17;turn++) AdvanceShowcaseTurn();
            view.Sync(); SelectCity(Game.Cities[0]); zoom=4.5f; UpdateCamera();
            yield return new WaitForSeconds(3);
            SmokeAssert(Game.Cities[0].Population>1,"food surplus creates new residents");
            SmokeAssert(Game.Cities[0].Improvements.Count>0,"unlocked resource has a real improvement");
            SmokeAssert(Game.Technologies.Count>0 && Game.CulturalOpenings.Count>0,"science and culture openings both applied");
            SmokeAssert(Game.Cities[0].Territory.Count>7,"culture visibly expands the territory");
            CaptureWorld("developed-city.png");
            yield return new WaitForSeconds(1);
            File.WriteAllText(Path.Combine(smokeOutput,"result.txt"),smokeFailed ? "FAIL" : "PASS");
            Application.logMessageReceived-=SmokeLog;
            Application.Quit(smokeFailed ? 1 : 0);
        }
        void AdvanceShowcaseTurn()
        {
            while(Game.ResearchReady)
            {
                Choice selected=Game.ResearchOffers.Find(c=>c.Cost<=Game.Knowledge);
                foreach(string preferred in new[]{"farming","stone","fire","pottery","hunting","fishing"})
                {
                    bool found=false; foreach(var offer in Game.ResearchOffers) if(offer.Id==preferred && offer.Cost<=Game.Knowledge) { selected=offer; found=true; }
                    if(found) break;
                }
                Game.ChooseResearch(selected.Id);
            }
            while(Game.CultureReady)
            {
                Choice selected=Game.CultureOffers.Find(c=>c.Cost<=Game.CultureStock); foreach(var choice in Game.CultureOffers) if(choice.Id=="oral" && choice.Cost<=Game.CultureStock) selected=choice;
                Game.ChooseCulture(selected.Id);
            }
            foreach(var city in Game.Cities) if(city.Production==null && city.Offers.Count>0)
            {
                Choice selected=city.Offers[0];
                foreach(string preferred in new[]{"hearth","huts","workshop","granary","potter","warrior","scout"})
                {
                    bool found=false; foreach(var offer in city.Offers) if(offer.Id==preferred) { selected=offer; found=true; }
                    if(found) break;
                }
                Game.ChooseProduction(city,selected.Id);
            }
            Game.PlanFocus(Game.Cities[0],CityFocus.Production);
            foreach(var unit in Game.Units) if(!unit.HasOrder) Game.PlanWait(unit);
            if(Game.BeginAction()) Game.FinishAction();
        }
        void CaptureWorld(string filename)
        {
            // Explicit camera rendering also works when the OS window is hidden.
            // These frames show the world; IMGUI panels belong to the screen pass.
            RenderTexture previousTarget=cameraView.targetTexture, previousActive=RenderTexture.active;
            var target=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32);
            cameraView.targetTexture=target; cameraView.Render(); RenderTexture.active=target;
            var picture=new Texture2D(1600,900,TextureFormat.RGB24,false);
            picture.ReadPixels(new Rect(0,0,1600,900),0,0); picture.Apply();
            File.WriteAllBytes(Path.Combine(smokeOutput,filename),picture.EncodeToPNG());
            var colors=new System.Collections.Generic.HashSet<Color32>();
            for(int y=0;y<900;y+=30) for(int x=0;x<1600;x+=30) colors.Add(picture.GetPixel(x,y));
            SmokeAssert(colors.Count>8,"nonblank rendered frame: "+filename);
            cameraView.targetTexture=previousTarget; RenderTexture.active=previousActive;
            RenderTexture.ReleaseTemporary(target); Destroy(picture);
        }
    }
}
