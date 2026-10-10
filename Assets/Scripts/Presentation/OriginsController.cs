using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Origins
{
    public sealed partial class OriginsController : MonoBehaviour
    {
        public Game Game { get; private set; }
        WorldView view;
        Camera cameraView;
        Light sunlight;
        Unit selectedUnit;
        City selectedCity;
        Tile selectedTile;
        Vector3 cameraTarget;
        float zoom=15, yaw=0;
        string toast="Назначьте производство и приказы. Наука и культура копятся каждый ход.";
        float toastUntil=14;
        bool help, started;
        string seedText="12026";
        int seed=12026;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(Object.FindFirstObjectByType<ArtPreviewScene>()!=null) return;
            if(Object.FindFirstObjectByType<OriginsController>()==null) new GameObject("Origins").AddComponent<OriginsController>();
        }
        void Start()
        {
            Application.targetFrameRate=60;
            QualitySettings.lodBias=1;
            foreach(var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) cam.gameObject.SetActive(false);
            cameraView=new GameObject("World camera").AddComponent<Camera>(); cameraView.tag="MainCamera";
            cameraView.orthographic=true; cameraView.nearClipPlane=.1f; cameraView.farClipPlane=150;
            cameraView.clearFlags=CameraClearFlags.SolidColor; cameraView.backgroundColor=new Color(.075f,.12f,.16f);
            cameraView.allowHDR=false;
            sunlight=new GameObject("Sun").AddComponent<Light>(); sunlight.type=LightType.Directional;
            sunlight.transform.rotation=Quaternion.Euler(48,-35,0); sunlight.shadows=LightShadows.Soft; sunlight.intensity=1.1f;
            RenderSettings.ambientLight=new Color(.58f,.64f,.63f);
            QualitySettings.shadows=ShadowQuality.All; QualitySettings.shadowDistance=70;
            NewRun(seed,false);
            StartSmokeIfRequested();
            InitOnline();
        }
        public void NewRun(int worldSeed,bool play=true)
        {
            StopAllCoroutines();
            if(view!=null) view.Dispose();
            seed=worldSeed; seedText=seed.ToString();
            DecorProfileStore.Reload();
            Game=new Game(seed,JsonUtility.FromJson<DemoBalance>(Resources.Load<TextAsset>("DemoBalance").text),ContentLoader.Load());
            var map=new GameObject("Procedural world"); view=new WorldView(Game,map.transform);
            selectedCity=Game.Cities[0]; selectedUnit=null; selectedTile=Game.World.Get(Game.World.Start);
            cameraTarget=view.Position(Game.World.Start); zoom=5.5f; yaw=30; started=play; help=false; choiceWindow=0;
            view.Select(Game.World.Start); view.ShowOrders(null);
            UpdateCamera();
            Notify("Первый очаг основан. Начинается каменный век.");
        }
        void Update()
        {
            if(view==null) return;
            view.Animate(Time.time,Time.deltaTime);
            sunlight.intensity=.95f+Mathf.Sin(Time.time*.025f)*.15f;
            if(!started || help || choiceWindow!=0 || Game.Phase==Phase.Finished) return;
            float dx=(Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0)-(Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            float dz=(Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0)-(Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            cameraTarget+=Quaternion.Euler(0,yaw,0)*new Vector3(dx,0,dz)*Time.deltaTime*zoom*.9f;
            if(!OverUI(Input.mousePosition))
            {
                zoom=Mathf.Clamp(zoom-Input.mouseScrollDelta.y,3.5f,33);
                if(Input.GetMouseButton(2)) cameraTarget+=Quaternion.Euler(0,yaw,0)*new Vector3(-Input.GetAxis("Mouse X"),0,-Input.GetAxis("Mouse Y"))*zoom*.055f;
            }
            cameraTarget.x=Mathf.Clamp(cameraTarget.x,-3,Game.World.Width*Mathf.Sqrt(3)+3);
            cameraTarget.z=Mathf.Clamp(cameraTarget.z,-3,Game.World.Height*1.5f+3);
            UpdateCamera();
            if(Input.GetKeyDown(KeyCode.F)) Focus(selectedUnit!=null ? selectedUnit.Position : selectedCity.Position);
            if(Input.GetKeyDown(KeyCode.M))
            {
                if(zoom>15) { zoom=5.5f; Focus(selectedUnit!=null ? selectedUnit.Position : selectedCity.Position); }
                else { cameraTarget=view.WorldBounds.center; zoom=27; }
            }
            if(Input.GetKeyDown(KeyCode.Tab) && Game.CanPlan && Game.OwnUnits.Count>0)
            {
                var roster=Game.OwnUnits; int i=selectedUnit==null ? -1 : roster.IndexOf(selectedUnit);
                SelectUnit(roster[(i+1)%roster.Count]);
            }
            if(Input.GetKeyDown(KeyCode.Q) && selectedUnit!=null && Game.PlanWait(selectedUnit)) { view.ShowOrders(selectedUnit); Notify("Отряд будет ждать в этом ходу."); }
            if(Input.GetKeyDown(KeyCode.Escape)) { selectedUnit=null; view.ShowOrders(null); }
            if(Input.GetKeyDown(KeyCode.Return) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt)) AdvancePlan();
            if(OverUI(Input.mousePosition) || !Game.CanPlan) return;
            RaycastHit hit;
            if(!Physics.Raycast(cameraView.ScreenPointToRay(Input.mousePosition),out hit,200)) return;
            var tileView=hit.collider.GetComponent<TileView>();
            if(tileView==null) return;
            Tile tile=tileView.Tile;
            if(Input.GetMouseButtonDown(1) && selectedUnit!=null)
            {
                if(Game.PlanMove(selectedUnit,tile.Hex)) { view.ShowOrders(selectedUnit); Notify("Маршрут запланирован. Отряд пойдёт в фазе действий."); }
                else Notify("Клетка недоступна за этот ход. Выберите подсвеченную клетку.");
            }
            if(Input.GetMouseButtonDown(0))
            {
                selectedTile=tile; view.Select(tile.Hex);
                if(!tile.Explored) return;
                foreach(var c in Game.OwnCities) if(c.Position.Equals(tile.Hex)) { SelectCity(c); return; }
                foreach(var u in Game.OwnUnits) if(u.Position.Equals(tile.Hex)) { SelectUnit(u); return; }
                selectedUnit=null; view.ShowOrders(null);
            }
        }
        void UpdateCamera()
        {
            var rotation=Quaternion.Euler(55,yaw,0);
            cameraView.transform.position=cameraTarget+rotation*new Vector3(0,0,-48);
            cameraView.transform.rotation=rotation; cameraView.orthographicSize=zoom;
        }
        void Focus(Hex h) { cameraTarget=view.Position(h); }
        void SelectUnit(Unit unit)
        {
            selectedUnit=unit; selectedTile=Game.World.Get(unit.Position); view.Select(unit.Position); view.ShowOrders(unit); Focus(unit.Position);
        }
        void SelectCity(City city)
        {
            selectedCity=city; selectedUnit=null; selectedTile=Game.World.Get(city.Position); view.Select(city.Position); view.ShowOrders(null); Focus(city.Position);
        }
        void Notify(string message) { toast=message; toastUntil=Time.unscaledTime+7; }
        void Execute()
        {
            if(!started || help || !Game.CanPlan) return;
            string reason=Game.BlockingReason();
            if(reason!=null) { Notify(reason); return; }
            if(online!=null) { StartCoroutine(SubmitOnline()); return; }
            if(Game.BeginAction()) StartCoroutine(ActionRoutine()); else Notify(Game.ValidatePlans()??"План не удалось зафиксировать.");
        }
        IEnumerator ActionRoutine()
        {
            view.ShowOrders(null); Notify("Отряды выполняют приказы. Поселения растут и работают.");
            TurnResolution result=Game.ResolveTurn();
            int steps=0; foreach(var move in result.Moves) steps=Mathf.Max(steps,move.Tick);
            for(int step=1;step<=steps;step++)
            {
                float elapsed=0;
                while(elapsed<.5f)
                {
                    elapsed+=Time.deltaTime;
                    float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.5f));
                    foreach(var move in result.Moves) if(move.Tick==step)
                    {
                        var tr=view.UnitTransform(move.UnitId); if(!tr) continue;
                        if(Game.Multiplayer) tr.gameObject.SetActive(Game.IsExplored(Game.ActivePlayer,move.From) || Game.IsExplored(Game.ActivePlayer,move.To));
                        Vector3 a=view.Position(move.From),b=view.Position(move.To);
                        tr.position=view.OnSurface(Vector3.Lerp(a,b,t))+Vector3.up*(.05f+Mathf.Sin(t*Mathf.PI)*.09f);
                        tr.rotation=Quaternion.LookRotation(new Vector3(b.x-a.x,0,b.z-a.z));
                    }
                    yield return null;
                }
                foreach(var move in result.Moves) if(move.Tick==step) view.ShowRoad(move.From,move.To);
            }
            yield return new WaitForSeconds(.65f);
            Game.FinishAction(); view.Sync();
            if(selectedUnit!=null && !Game.Units.Contains(selectedUnit)) selectedUnit=null;
            view.ShowOrders(selectedUnit);
            view.Select(selectedUnit!=null ? selectedUnit.Position : selectedCity.Position);
            Notify(Game.Phase==Phase.Finished ? "Забег завершён." : "Новый ход. Можно изменить приказы отрядов.");
            FinishOnlineTurn();
        }
        void OnDestroy()
        {
            if(view!=null) view.Dispose();
            if(cameraView!=null) Destroy(cameraView.gameObject);
            if(sunlight!=null) Destroy(sunlight.gameObject);
        }
    }
}
