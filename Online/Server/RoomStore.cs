using Origins;
using Npgsql;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Origins.Server;
public sealed class ApiError : Exception { public int Status; public ApiError(int status,string message):base(message) { Status=status; } }
public sealed class SaveFile { public int protocol=1,seed; public string version; public OnlineTurn[] history; }
public sealed class StoredRoom
{
    public string code;
    public int seed;
    public string[] hashes=new string[2];
    public PlayerPlan[] pending=new PlayerPlan[2];
    public List<OnlineTurn> history=new();
    public DateTime updated=DateTime.UtcNow;
}
public sealed class RoomStore
{
    public static readonly JsonSerializerOptions Json=new() { IncludeFields=true,PropertyNamingPolicy=null };
    readonly ConcurrentDictionary<string,SemaphoreSlim> locks=new();
    readonly ConcurrentDictionary<string,StoredRoom> memory=new();
    readonly GameContent content;
    readonly DemoBalance balance;
    readonly string database;
    readonly string localPath;
    public RoomStore(IWebHostEnvironment environment)
    {
        string root=AppContext.BaseDirectory;
        content=new GameContent(JsonSerializer.Deserialize<ContentRecords>(File.ReadAllText(Path.Combine(root,"Content/EarlyEraContent.json")),Json)); GameContent.Current=content;
        balance=JsonSerializer.Deserialize<DemoBalance>(File.ReadAllText(Path.Combine(root,"Content/DemoBalance.json")),Json);
        database=Environment.GetEnvironmentVariable("DATABASE_URL"); localPath=Path.Combine(environment.ContentRootPath,"data");
    }
    string Connection()
    {
        var uri=new Uri(database); var user=uri.UserInfo.Split(':',2);
        return new NpgsqlConnectionStringBuilder { Host=uri.Host,Port=uri.Port>0 ? uri.Port : 5432,Username=Uri.UnescapeDataString(user[0]),Password=Uri.UnescapeDataString(user.Length>1 ? user[1] : ""),Database=uri.AbsolutePath.Trim('/'),SslMode=SslMode.Prefer,MaxPoolSize=5,Timeout=20 }.ConnectionString;
    }
    public async Task Initialize()
    {
        if(string.IsNullOrEmpty(database))
        {
            if(Environment.GetEnvironmentVariable("RENDER")=="true") throw new InvalidOperationException("DATABASE_URL is required on Render to preserve matches across restarts");
            Directory.CreateDirectory(localPath); return;
        }
        await using var c=new NpgsqlConnection(Connection()); await c.OpenAsync();
        await using var cmd=new NpgsqlCommand("CREATE TABLE IF NOT EXISTS origins_rooms (code TEXT PRIMARY KEY, payload TEXT NOT NULL, updated TIMESTAMPTZ NOT NULL DEFAULT NOW())",c); await cmd.ExecuteNonQueryAsync();
    }
    SemaphoreSlim Gate(string code) { if(code==null || code.Length!=16 || !code.All(ch=>ch>='a' && ch<='f' || ch>='0' && ch<='9')) throw new ApiError(404,"Комната не найдена"); return locks.GetOrAdd(code,_=>new SemaphoreSlim(1,1)); }
    async Task<StoredRoom> Load(string code)
    {
        if(memory.TryGetValue(code,out var room)) return room;
        string json=null;
        if(!string.IsNullOrEmpty(database))
        {
            await using var c=new NpgsqlConnection(Connection()); await c.OpenAsync(); await using var cmd=new NpgsqlCommand("SELECT payload FROM origins_rooms WHERE code=@code",c); cmd.Parameters.AddWithValue("code",code); json=await cmd.ExecuteScalarAsync() as string;
        }
        else { var file=Path.Combine(localPath,code+".json"); if(File.Exists(file)) json=await File.ReadAllTextAsync(file); }
        if(json==null) throw new ApiError(404,"Комната не найдена");
        room=JsonSerializer.Deserialize<StoredRoom>(json,Json); memory[code]=room; return room;
    }
    async Task Save(StoredRoom room)
    {
        room.updated=DateTime.UtcNow; string json=JsonSerializer.Serialize(room,Json);
        if(!string.IsNullOrEmpty(database))
        {
            await using var c=new NpgsqlConnection(Connection()); await c.OpenAsync();
            await using var cmd=new NpgsqlCommand("INSERT INTO origins_rooms(code,payload,updated) VALUES(@code,@payload,NOW()) ON CONFLICT(code) DO UPDATE SET payload=EXCLUDED.payload,updated=NOW()",c);
            cmd.Parameters.AddWithValue("code",room.code); cmd.Parameters.AddWithValue("payload",json); await cmd.ExecuteNonQueryAsync();
        }
        else { string path=Path.Combine(localPath,room.code+".json"), temp=path+".tmp"; await File.WriteAllTextAsync(temp,json); File.Move(temp,path,true); }
        memory[room.code]=room;
    }
    static StoredRoom Copy(StoredRoom room)=>JsonSerializer.Deserialize<StoredRoom>(JsonSerializer.Serialize(room,Json),Json);
    static string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
    static string NewToken()=>Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    int Seat(StoredRoom r,string token)
    {
        if(token==null || token.Length!=64) throw new ApiError(401,"Войдите в своё племя через меню комнат");
        string hash=Hash(token); for(int p=0;p<2;p++) if(r.hashes[p]!=null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash),Encoding.ASCII.GetBytes(r.hashes[p]))) return p;
        throw new ApiError(403,"Эта ссылка входа не принадлежит вашему племени");
    }
    OnlineRoom View(StoredRoom r,int seat,string token=null)=>new() { protocol=1,code=r.code,seed=r.seed,seat=seat,token=token,turn=r.history.Count+1,players=r.hashes[1]==null ? 1 : 2,ready=new[] {r.pending[0]!=null,r.pending[1]!=null},history=r.history.ToArray(),pending=r.pending[seat],version=content.Version };
    Game Restore(StoredRoom r)
    {
        var game=new Game(r.seed,balance,content,2);
        foreach(var turn in r.history) if(!game.ReplayTurn(turn)) throw new InvalidOperationException("Stored match failed verification");
        return game;
    }
    public async Task<OnlineRoom> Create(SaveFile saved=null)
    {
        var r=new StoredRoom { code=Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant(),seed=12026 };
        if(saved!=null)
        {
            if(saved.protocol!=1 || saved.version!=content.Version || saved.history==null || saved.history.Length>500) throw new ApiError(400,"Сохранение имеет неподдерживаемую версию");
            r.seed=saved.seed; r.history=saved.history.ToList();
            try { Restore(r); } catch { throw new ApiError(400,"Сохранение повреждено или приказы недопустимы"); }
        }
        else
        {
            for(int attempt=0;attempt<20;attempt++) { r.seed=RandomNumberGenerator.GetInt32(1,99999999); try { Restore(r); break; } catch when(attempt<19) { } }
        }
        string token=NewToken(); r.hashes[0]=Hash(token); await Save(r); return View(r,0,token);
    }
    public async Task<OnlineRoom> Join(string code)
    {
        var gate=Gate(code); await gate.WaitAsync(); try
        {
            var r=Copy(await Load(code)); if(r.hashes[1]!=null) throw new ApiError(409,"Оба места заняты. Для возвращения используйте тот же браузер.");
            string token=NewToken(); r.hashes[1]=Hash(token); await Save(r); return View(r,1,token);
        } finally { gate.Release(); }
    }
    public async Task<OnlineRoom> Read(string code,string token)
    {
        var gate=Gate(code); await gate.WaitAsync(); try { var r=await Load(code); return View(r,Seat(r,token)); } finally { gate.Release(); }
    }
    public async Task<OnlineRoom> Ready(string code,string token,PlayerPlan plan)
    {
        var gate=Gate(code); await gate.WaitAsync(); try
        {
            var r=Copy(await Load(code)); int seat=Seat(r,token);
            if(r.hashes[1]==null) throw new ApiError(409,"Дождитесь второго игрока");
            if(plan==null || plan.player!=seat) throw new ApiError(403,"Нельзя отдавать приказы другому племени");
            if(plan.turn<r.history.Count+1) return View(r,seat); // Retry after a lost acknowledgement.
            if(plan.turn!=r.history.Count+1 || plan.decisions==null || plan.decisions.Length>64 || plan.units==null || plan.units.Length>256) throw new ApiError(400,"Недопустимый план хода");
            if(r.pending[seat]!=null) return View(r,seat); // Once committed, a draft cannot silently change.
            try { var check=Restore(r); if(!check.ApplyPlan(plan)) throw new ApiError(400,"Проверьте исследования, производство и приказы своим отрядам"); }
            catch(ApiError) { throw; } catch { throw new ApiError(400,"Приказы не соответствуют правилам игры"); }
            r.pending[seat]=plan;
            if(r.pending[0]!=null && r.pending[1]!=null)
            {
                var game=Restore(r); var turn=new OnlineTurn { turn=game.Turn,plans=r.pending.ToArray() };
                if(!game.ReplayTurn(turn)) throw new ApiError(400,"Планы не удалось разрешить вместе");
                turn.checksum=game.Checksum(); r.history.Add(turn); r.pending=new PlayerPlan[2];
            }
            await Save(r); return View(r,seat);
        } finally { gate.Release(); }
    }
    public async Task<OnlineRoom> Unready(string code,string token,int turn)
    {
        var gate=Gate(code); await gate.WaitAsync(); try
        {
            var r=Copy(await Load(code)); int seat=Seat(r,token); if(turn!=r.history.Count+1) throw new ApiError(409,"Ход уже начался");
            r.pending[seat]=null; await Save(r); return View(r,seat);
        } finally { gate.Release(); }
    }
    public async Task<SaveFile> Export(string code,string token)
    {
        var gate=Gate(code); await gate.WaitAsync(); try { var r=await Load(code); Seat(r,token); return new SaveFile { seed=r.seed,version=content.Version,history=r.history.ToArray() }; } finally { gate.Release(); }
    }
}
