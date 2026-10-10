using Origins;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

var json=new JsonSerializerOptions { IncludeFields=true,PropertyNamingPolicy=null };
GameContent.Current=new GameContent(JsonSerializer.Deserialize<ContentRecords>(File.ReadAllText("Assets/Resources/EarlyEraContent.json"),json));
var balance=JsonSerializer.Deserialize<DemoBalance>(File.ReadAllText("Assets/Resources/DemoBalance.json"),json);
using var http=new HttpClient { BaseAddress=new Uri(args.Length>0 ? args[0] : "http://localhost:5080"),Timeout=TimeSpan.FromSeconds(90) };
int checks=0;
void Check(bool condition,string name) { if(!condition) throw new Exception("FAIL: "+name); checks++; Console.WriteLine("PASS: "+name); }
async Task<(HttpStatusCode Status,OnlineRoom Room)> Request(string method,string path,object body=null,string token=null)
{
    using var request=new HttpRequestMessage(new HttpMethod(method),path);
    if(token!=null) request.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
    if(body!=null) request.Content=JsonContent.Create(body,options:json);
    using var result=await http.SendAsync(request); return (result.StatusCode,await result.Content.ReadFromJsonAsync<OnlineRoom>(json));
}
void Ready(Game game)
{
    while(game.ResearchReady) foreach(var c in game.ResearchOffers.ToArray()) if(game.Knowledge>=c.Cost) { game.ChooseResearch(c.Id); break; }
    while(game.CultureReady) foreach(var c in game.CultureOffers.ToArray()) if(game.CultureStock>=c.Cost) { game.ChooseCulture(c.Id); break; }
    foreach(var c in game.OwnCities) if(c.Production==null && c.Offers.Count>0) game.ChooseProduction(c,c.Offers[0].Id);
    foreach(var u in game.OwnUnits) game.PlanWait(u);
}
// One disposable QA guest: exercises the browser host against a real second seat.
if(args.Length==3 && args[1]=="--guest-once")
{
    string qaCode=args[2];
    var qaJoin=await Request("POST","/api/rooms/"+qaCode+"/join");
    if(qaJoin.Status!=HttpStatusCode.OK) throw new Exception("QA room guest seat unavailable");
    var qaGame=new Game(qaJoin.Room.seed,balance,players:2);
    foreach(var t in qaJoin.Room.history) if(!qaGame.ReplayTurn(t)) throw new Exception("QA replay failed");
    qaGame.SetPlayer(1); Ready(qaGame);
    var qaReady=await Request("POST","/api/rooms/"+qaCode+"/ready",qaGame.CapturePlan(),qaJoin.Room.token);
    if(qaReady.Status!=HttpStatusCode.OK) throw new Exception("QA guest plan rejected");
    Console.WriteLine("QA guest joined and submitted turn "+qaGame.Turn);
    return;
}
var created=await Request("POST","/api/rooms"); Check(created.Status==HttpStatusCode.OK && created.Room.seat==0,"create room with host token");
string code=created.Room.code,token0=created.Room.token;
var guest=await Request("POST","/api/rooms/"+code+"/join"); Check(guest.Status==HttpStatusCode.OK && guest.Room.seat==1,"friend receives independent seat"); string token1=guest.Room.token;
Check((await Request("POST","/api/rooms/"+code+"/join")).Status==HttpStatusCode.Conflict,"third player cannot steal a seat");
Check((await Request("GET","/api/rooms/"+code)).Status==HttpStatusCode.Unauthorized,"room state requires seat token");
var game=new Game(created.Room.seed,balance,players:2); Check(game.Cities.Count==2 && !game.IsExplored(0,game.Cities[1].Position),"separate tribes and fog");
for(int i=0;i<16;i++)
{
    var plans=new PlayerPlan[2];
    for(int p=0;p<2;p++) { game.SetPlayer(p); Ready(game); plans[p]=game.CapturePlan(); }
    if(i==0) Check((await Request("POST","/api/rooms/"+code+"/ready",plans[1],token0)).Status==HttpStatusCode.Forbidden,"server rejects another player's orders");
    var host=await Request("POST","/api/rooms/"+code+"/ready",plans[0],token0); Check(host.Status==HttpStatusCode.OK && host.Room.turn==game.Turn,"first player alone cannot advance turn "+game.Turn);
    if(i==0)
    {
        var hidden=await Request("GET","/api/rooms/"+code,token:token1); Check(hidden.Room.pending==null && hidden.Room.history.Length==0,"uncommitted opponent orders stay secret");
        Check((await Request("DELETE","/api/rooms/"+code+"/ready?turn=1",token:token0)).Status==HttpStatusCode.OK,"readiness can be cancelled before action");
        host=await Request("POST","/api/rooms/"+code+"/ready",plans[0],token0);
    }
    var resolved=await Request("POST","/api/rooms/"+code+"/ready",plans[1],token1); Check(resolved.Status==HttpStatusCode.OK && resolved.Room.turn==game.Turn+1,"both ready resolve exactly one turn");
    Check(game.BeginAction(),"local validated action"); game.FinishAction();
    Check(game.Checksum()==resolved.Room.history[^1].checksum,"server and client agree at turn "+game.Turn);
    var replay=new Game(created.Room.seed,balance,players:2); foreach(var turn in resolved.Room.history) Check(replay.ReplayTurn(turn),"reconnect replay "+turn.turn);
    Check(replay.Checksum()==game.Checksum(),"reconnect restores full match");
    if(i==0)
    {
        Check((await Request("DELETE","/api/rooms/"+code+"/ready?turn=1",token:token0)).Status==HttpStatusCode.Conflict,"late cancellation cannot alter new turn");
        var repeat=await Request("POST","/api/rooms/"+code+"/ready",plans[1],token1); Check(repeat.Status==HttpStatusCode.OK && repeat.Room.turn==2,"retry does not execute twice");
    }
}
var saveRequest=new HttpRequestMessage(HttpMethod.Get,"/api/rooms/"+code+"/save"); saveRequest.Headers.Authorization=new("Bearer",token0); using var saveResponse=await http.SendAsync(saveRequest); var save=await saveResponse.Content.ReadFromJsonAsync<JsonElement>();
var imported=await Request("POST","/api/import",save); Check(imported.Status==HttpStatusCode.OK && imported.Room.turn==game.Turn && imported.Room.code!=code,"export/import preserves party in a new room");
Directory.CreateDirectory("TestResults/Online"); await File.WriteAllTextAsync("TestResults/Online/integration.json",JsonSerializer.Serialize(new { checks,seed=game.World.Seed,turn=game.Turn,checksum=game.Checksum(),code },json));
Console.WriteLine("ONLINE CHECKS PASSED: "+checks);
