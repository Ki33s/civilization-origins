using Origins;
using Origins.Server;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
using System.Threading.RateLimiting;

var builder=WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=4194304);
builder.Services.ConfigureHttpJsonOptions(o=> { o.SerializerOptions.IncludeFields=true; o.SerializerOptions.PropertyNamingPolicy=null; });
builder.Services.AddRateLimiter(o=>o.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"local",key=>new FixedWindowRateLimiterOptions { PermitLimit=200,Window=TimeSpan.FromSeconds(10),QueueLimit=0 })));
builder.Services.AddSingleton<RoomStore>();
var app=builder.Build();
app.UseRateLimiter();
app.Use(async (context,next)=>
{
    context.Response.Headers["X-Content-Type-Options"]="nosniff";
    context.Response.Headers["Referrer-Policy"]="same-origin";
    if(context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl="no-store";
    try { await next(); }
    catch(ApiError e) { context.Response.StatusCode=e.Status; await context.Response.WriteAsJsonAsync(new { error=e.Message }); }
    catch(Exception e) { app.Logger.LogError("Request failed: {Type}",e.GetType().Name); context.Response.StatusCode=503; await context.Response.WriteAsJsonAsync(new { error="Сервер временно недоступен. Попробуйте ещё раз." }); }
});
app.UseDefaultFiles();
var types=new FileExtensionContentTypeProvider(); types.Mappings[".unityweb"]="application/octet-stream"; types.Mappings[".wasm"]="application/wasm"; types.Mappings[".data"]="application/octet-stream";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider=types,OnPrepareResponse=c=> { if(c.Context.Request.Path.StartsWithSegments("/Build")) c.Context.Response.Headers.CacheControl="public,max-age=31536000,immutable"; else c.Context.Response.Headers.CacheControl="no-cache"; } });
app.MapGet("/health",()=>new { ok=true,protocol=1,version=GameContent.Current.Version });
app.MapPost("/api/rooms",async (RoomStore store)=>Results.Json(await store.Create()));
app.MapPost("/api/import",async (SaveFile save,RoomStore store)=>Results.Json(await store.Create(save)));
app.MapPost("/api/rooms/{code}/join",async (string code,RoomStore store)=>Results.Json(await store.Join(code)));
app.MapGet("/api/rooms/{code}",async (string code,HttpContext ctx,RoomStore store)=>Results.Json(await store.Read(code,Token(ctx))));
app.MapPost("/api/rooms/{code}/ready",async (string code,PlayerPlan plan,HttpContext ctx,RoomStore store)=>Results.Json(await store.Ready(code,Token(ctx),plan)));
app.MapDelete("/api/rooms/{code}/ready",async (string code,int turn,HttpContext ctx,RoomStore store)=>Results.Json(await store.Unready(code,Token(ctx),turn)));
app.MapGet("/api/rooms/{code}/save",async (string code,HttpContext ctx,RoomStore store)=>Results.Json(await store.Export(code,Token(ctx))));
await app.Services.GetRequiredService<RoomStore>().Initialize();
app.Urls.Add("http://0.0.0.0:"+(Environment.GetEnvironmentVariable("PORT")??"5080"));
await app.RunAsync();
static string Token(HttpContext ctx) { var s=ctx.Request.Headers.Authorization.ToString(); return s.StartsWith("Bearer ",StringComparison.Ordinal) ? s.Substring(7) : ""; }
