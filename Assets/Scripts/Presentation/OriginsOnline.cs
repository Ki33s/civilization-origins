using System;
using System.Collections;
using System.Text;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

namespace Origins
{
    public sealed partial class OriginsController
    {
        OnlineRoom online;
        bool onlineBusy, onlineAnimating;
        string onlineStatus="";
        string apiRoot;
        OnlineTurn incomingTurn;
        [DllImport("__Internal")] static extern void OriginsOnlineReady();
        [DllImport("__Internal")] static extern void OriginsInvite(string code);
        [DllImport("__Internal")] static extern void OriginsOnlineReport(string text);
        void InitOnline()
        {
            if(Application.platform==RuntimePlatform.WebGLPlayer) OriginsOnlineReady();
        }
        // Called by the HTML lobby after obtaining a seat-specific token.
        [UnityEngine.Scripting.Preserve]
        public void ConnectOnline(string json)
        {
            try
            {
                var room=JsonUtility.FromJson<OnlineRoom>(json);
                if(room==null || room.protocol!=1 || room.seat<0 || room.seat>1 || string.IsNullOrEmpty(room.token)) throw new Exception("Неверные данные комнаты");
                Debug.Log("ORIGINS ONLINE: connecting seat "+room.seat+" at turn "+room.turn);
                online=room; apiRoot=new Uri(new Uri(Application.absoluteURL),"/api/").AbsoluteUri;
                InstallOnlineGame(room); StartCoroutine(PollOnline());
                Debug.Log("ORIGINS ONLINE: connected");
            }
            catch(Exception e) { Debug.LogError("ORIGINS ONLINE: "+e); onlineStatus=e.Message; if(Application.platform==RuntimePlatform.WebGLPlayer) OriginsOnlineReport(onlineStatus); }
        }
        void InstallOnlineGame(OnlineRoom room)
        {
            var model=new Game(room.seed,JsonUtility.FromJson<DemoBalance>(Resources.Load<TextAsset>("DemoBalance").text),ContentLoader.Load(),2);
            if(room.version!=model.Content.Version) throw new Exception("Версии игры отличаются. Обновите страницу.");
            if(room.history!=null) foreach(var turn in room.history) if(!model.ReplayTurn(turn)) throw new Exception("Не удалось восстановить партию: ход "+turn.turn);
            model.SetPlayer(room.seat);
            // JsonUtility can materialize a JSON null as an empty nested class in IL2CPP.
            // Real plans always carry a positive turn; an empty placeholder is no draft.
            if(room.pending!=null && room.pending.turn>0 && !model.ApplyPlan(room.pending)) throw new Exception("Не удалось восстановить подтверждённый план");
            model.PlansLocked=room.ready!=null && room.ready[room.seat];
            if(view!=null) view.Dispose(); Game=model;
            view=new WorldView(Game,new GameObject("Online shared world").transform);
            seed=room.seed; selectedCity=Game.OwnCities[0]; selectedUnit=null; selectedTile=Game.World.Get(selectedCity.Position);
            cameraTarget=view.Position(selectedCity.Position); zoom=5.5f; yaw=30; started=true; help=false; choiceWindow=0;
            view.Select(selectedCity.Position); view.ShowOrders(null); UpdateCamera();
            UpdateOnlineStatus(); Notify("Вы управляете племенем "+(room.seat+1)+". Отправьте другу ссылку на комнату.");
        }
        void UpdateOnlineStatus()
        {
            onlineStatus=online.players<2 ? "Ждём друга · комната "+online.code : online.ready!=null && online.ready[online.seat] ? "План принят · ждём друга" : online.ready!=null && online.ready[1-online.seat] ? "Друг готов · составьте свой план" : "Два племени · оба планируют";
        }
        IEnumerator PollOnline()
        {
            while(online!=null)
            {
                if(!onlineBusy && !onlineAnimating)
                {
                    using(var request=UnityWebRequest.Get(apiRoot+"rooms/"+online.code))
                    {
                        request.SetRequestHeader("Authorization","Bearer "+online.token); request.timeout=90;
                        yield return request.SendWebRequest();
                        if(onlineBusy) { yield return new WaitForSecondsRealtime(2); continue; }
                        if(request.result==UnityWebRequest.Result.Success)
                        {
                            var next=JsonUtility.FromJson<OnlineRoom>(request.downloadHandler.text);
                            if(next.protocol!=1 || next.version!=Game.Content.Version) { onlineStatus="Обновите игру: версия сервера изменилась"; Game.PlansLocked=true; }
                            else
                            {
                                next.token=online.token; online=next; UpdateOnlineStatus();
                                if(next.history!=null && next.history.Length>=Game.Turn)
                                {
                                    incomingTurn=next.history[Game.Turn-1];
                                    bool ok=true;
                                    foreach(var plan in incomingTurn.plans) if(!Game.ApplyPlan(plan,plan.player==online.seat)) ok=false;
                                    if(ok && Game.BeginAction()) { onlineAnimating=true; StartCoroutine(ActionRoutine()); }
                                    else { InstallOnlineGame(next); onlineStatus="Партию восстановили с сервера"; }
                                }
                                else Game.PlansLocked=next.ready!=null && next.ready[online.seat];
                            }
                        }
                        else onlineStatus=request.responseCode==404 ? "Комната не найдена. Вернитесь в меню комнат." : "Связь прервана · пытаемся подключиться снова";
                    }
                }
                yield return new WaitForSecondsRealtime(2);
            }
        }
        IEnumerator SubmitOnline()
        {
            if(onlineBusy || Game.PlansLocked || online.players<2) { Notify("Сначала дождитесь друга в комнате."); yield break; }
            onlineBusy=true; Game.PlansLocked=true;
            var plan=Game.CapturePlan();
            using(var request=new UnityWebRequest(apiRoot+"rooms/"+online.code+"/ready","POST"))
            {
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(plan))); request.downloadHandler=new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type","application/json"); request.SetRequestHeader("Authorization","Bearer "+online.token); request.timeout=90;
                yield return request.SendWebRequest();
                if(request.result==UnityWebRequest.Result.Success) { onlineStatus="План принят · ждём друга"; Notify("Сервер принял приказы. Ход начнётся, когда готовы оба."); }
                else { Game.PlansLocked=false; var error=JsonUtility.FromJson<OnlineRoom>(request.downloadHandler.text); Notify(error!=null && error.error!=null ? error.error : "Не удалось подтвердить план. Можно попробовать снова."); }
            }
            onlineBusy=false;
        }
        IEnumerator CancelOnlineReady()
        {
            if(onlineBusy || onlineAnimating) yield break;
            onlineBusy=true;
            using(var request=new UnityWebRequest(apiRoot+"rooms/"+online.code+"/ready?turn="+Game.Turn,"DELETE"))
            {
                request.downloadHandler=new DownloadHandlerBuffer(); request.SetRequestHeader("Authorization","Bearer "+online.token); request.timeout=90;
                yield return request.SendWebRequest();
                if(request.result==UnityWebRequest.Result.Success) { Game.PlansLocked=false; onlineStatus="Можно изменить план"; }
                else Notify("Ход уже начался или связь потеряна. Дождитесь синхронизации.");
            }
            onlineBusy=false;
        }
        void FinishOnlineTurn()
        {
            if(online==null) return;
            onlineAnimating=false;
            if(incomingTurn!=null && Game.Checksum()!=incomingTurn.checksum)
            {
                try { InstallOnlineGame(online); Notify("Восстановлена версия партии с сервера."); }
                catch(Exception e) { Game.PlansLocked=true; onlineStatus=e.Message; }
            }
            incomingTurn=null;
        }
        void DrawOnlineStatus()
        {
            if(online==null) return;
            Text(310,UIHeight-159,650,23,"ОНЛАЙН · ПЛЕМЯ "+(online.seat+1)+" · "+onlineStatus,headingStyle);
            float x=UIWidth-700,y=UIHeight-164;
            if(Button(new Rect(x,y,170,32),"Ссылка другу")) OriginsInvite(online.code);
            if(Game.PlansLocked && !onlineAnimating && Button(new Rect(x+180,y,170,32),"Изменить план",!onlineBusy)) StartCoroutine(CancelOnlineReady());
        }
    }
}
