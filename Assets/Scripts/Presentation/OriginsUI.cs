using System;
using UnityEngine;

namespace Origins
{
    public sealed partial class OriginsController
    {
        GUIStyle titleStyle, labelStyle, smallStyle, headingStyle, buttonStyle, cardTitleStyle;
        Texture2D white;
        readonly Color ink=new Color(.88f,.89f,.81f), muted=new Color(.55f,.65f,.65f), gold=new Color(.93f,.73f,.37f);
        readonly Color panel=new Color(.065f,.105f,.135f,.96f), card=new Color(.12f,.18f,.21f,.98f);
        Vector2 rosterScroll, journalScroll;
        Vector2 workersScroll;
        int choiceWindow;
        float UIScale { get { return Mathf.Min(Screen.width/1600f,Screen.height/900f); } }
        float UIWidth { get { return Screen.width/UIScale; } }
        float UIHeight { get { return Screen.height/UIScale; } }
        bool OverUI(Vector3 screenPoint)
        {
            float s=UIScale;
            if(s<=0 || !started || help || choiceWindow!=0 || Game.Phase==Phase.Finished) return true;
            Vector2 p=new Vector2(screenPoint.x/s,(Screen.height-screenPoint.y)/s);
            return p.y<88 || p.y>UIHeight-94 || p.x<294 || p.x>UIWidth-358;
        }
        void InitUI()
        {
            if(white!=null) return;
            white=new Texture2D(1,1); white.SetPixel(0,0,Color.white); white.Apply();
            Font font=Resources.Load<Font>("OriginsUI") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelStyle=new GUIStyle { font=font,fontSize=17,wordWrap=true,normal={textColor=ink} };
            smallStyle=new GUIStyle(labelStyle){fontSize=14,normal={textColor=muted}};
            headingStyle=new GUIStyle(labelStyle){fontSize=13,fontStyle=FontStyle.Bold,normal={textColor=gold}};
            titleStyle=new GUIStyle(labelStyle){fontSize=38,fontStyle=FontStyle.Bold};
            cardTitleStyle=new GUIStyle(labelStyle){fontSize=19,fontStyle=FontStyle.Bold};
            buttonStyle=new GUIStyle(labelStyle){alignment=TextAnchor.MiddleCenter,fontSize=16,fontStyle=FontStyle.Bold,wordWrap=true};
        }
        void Box(Rect r,Color c) { Color previous=GUI.color; GUI.color=c; GUI.DrawTexture(r,white); GUI.color=previous; }
        void Text(float x,float y,float w,float h,string value,GUIStyle style=null) { GUI.Label(new Rect(x,y,w,h),value,style??labelStyle); }
        bool Button(Rect r,string text,bool enabled=true,bool accent=false)
        {
            bool hover=r.Contains(Event.current.mousePosition);
            Color c=!enabled ? new Color(.11f,.15f,.17f) : accent ? gold : hover ? new Color(.22f,.3f,.32f) : card;
            Box(r,c); Box(new Rect(r.x,r.y,r.width,1),accent ? gold : new Color(.28f,.36f,.36f));
            var old=buttonStyle.normal.textColor; buttonStyle.normal.textColor=accent ? new Color(.1f,.14f,.16f) : enabled ? ink : muted;
            bool was=GUI.enabled; GUI.enabled=enabled;
            bool pressed=GUI.Button(r,text,buttonStyle); GUI.enabled=was; buttonStyle.normal.textColor=old; return pressed;
        }
        void Progress(float x,float y,float w,int amount,int max,Color color)
        {
            Box(new Rect(x,y,w,4),new Color(.2f,.27f,.28f)); Box(new Rect(x,y,w*Mathf.Clamp01(amount/(float)Math.Max(1,max)),4),color);
        }
        void OnGUI()
        {
            if(Game==null || view==null || UIScale<=0) return;
            InitUI(); GUI.matrix=Matrix4x4.Scale(new Vector3(UIScale,UIScale,1));
            if(!started) { DrawStart(); return; }
            DrawTop(); DrawLeft(); DrawRight(); DrawBottom(); DrawMapLabels(); DrawOnlineStatus();
            if(help) DrawHelp();
            if(choiceWindow!=0) DrawChoiceModal();
            if(Game.Phase==Phase.Finished) DrawResult();
        }
        void DrawTop()
        {
            float w=UIWidth;
            Box(new Rect(0,0,w,78),panel);
            Text(24,13,250,36,"И С Т О К И",new GUIStyle(titleStyle){fontSize=27});
            Text(25,49,260,22,"OPENSPEC · ПЛАН И ДЕЙСТВИЯ",headingStyle);
            int science=0,culture=0,goldIncome=0;
            foreach(var c in Game.OwnCities) { Yield y=Game.Income(c); science+=y.Science; culture+=y.Culture; goldIncome+=y.Gold; }
            Text(320,16,170,28,Game.TotalPopulation+"  жителей",cardTitleStyle);
            Text(320,47,170,20,Game.OwnCities.Count+"  поселений",smallStyle);
            Text(505,16,175,28,"Наука "+Game.Knowledge+" (+"+science+")",new GUIStyle(cardTitleStyle){fontSize=17});
            Text(505,47,175,20,Game.Technologies.Count+" / "+Game.Content.Technologies.Length+" технологий",smallStyle);
            Text(690,16,180,28,"Культура "+Game.CultureStock+" (+"+culture+")",new GUIStyle(cardTitleStyle){fontSize=17});
            Text(690,47,175,20,Game.CulturalOpenings.Count+" / "+Game.Content.Cultures.Length+" культурных открытий",smallStyle);
            Text(885,16,170,28,"Золото "+Game.Gold+" (+"+goldIncome+")",new GUIStyle(cardTitleStyle){fontSize=17});
            Text(885,47,175,20,"общая казна державы",smallStyle);
            Text(w-440,12,225,28,"Ход "+Game.Turn+" · "+Math.Abs(Game.CalendarYear)+(Game.CalendarYear<=0 ? " до н. э." : " н. э."),new GUIStyle(cardTitleStyle){fontSize=16});
            Text(w-440,48,225,20,Game.Phase==Phase.Planning ? Game.PlansLocked ? "01  ПЛАН ПОДТВЕРЖДЁН" : "01  ПЛАНИРОВАНИЕ" : Game.Phase==Phase.Action ? "02  ДЕЙСТВИЯ" : "ЗАБЕГ ЗАВЕРШЁН",headingStyle);
            if(Button(new Rect(w-192,20,74,38),"?")) help=true;
            if(Button(new Rect(w-106,20,82,38),"К очагу")) Focus(selectedCity.Position);
        }
        void DrawLeft()
        {
            float h=UIHeight;
            Box(new Rect(16,94,270,h-198),panel);
            Text(32,110,238,24,"ВАШЕ ПЛЕМЯ",headingStyle);
            float y=143;
            // A scrollable roster stays usable after recruiting many units.
            float rosterHeight=Mathf.Min(270,h-620);
            rosterHeight=Mathf.Max(180,rosterHeight);
            rosterScroll=GUI.BeginScrollView(new Rect(28,y,246,rosterHeight),rosterScroll,new Rect(0,0,225,Game.OwnCities.Count*44+Game.OwnUnits.Count*46+36));
            float ry=0;
            foreach(var city in Game.OwnCities)
            {
                if(Button(new Rect(0,ry,222,38),city.Name+"  ·  "+city.Population,Game.Phase!=Phase.Action,city==selectedCity && selectedUnit==null)) SelectCity(city);
                ry+=44;
            }
            Text(0,ry+4,222,24,"ОТРЯДЫ",headingStyle); ry+=32;
            foreach(var unit in Game.OwnUnits)
            {
                string order=unit.FoundOrder ? "  [город]" : unit.Order.Count>1 ? "  [путь]" : unit.WaitOrder ? "  [ждёт]" : "  [без приказа]";
                if(Button(new Rect(0,ry,222,40),unit.Name+order,Game.CanPlan,selectedUnit==unit)) SelectUnit(unit);
                ry+=46;
            }
            GUI.EndScrollView();
            y+=rosterHeight+20;
            Box(new Rect(32,y,238,1),new Color(.25f,.32f,.32f)); y+=14;
            if(selectedUnit!=null)
            {
                Text(32,y,238,26,selectedUnit.Name,cardTitleStyle); y+=34;
                Text(32,y,238,66,"Движение: "+Game.Movement(selectedUnit)+" очков\nЛес и холмы стоят 2 очка.\nПриказ: "+(selectedUnit.FoundOrder ? "основать город" : selectedUnit.WaitOrder ? "ждать" : selectedUnit.HasOrder ? "двигаться" : "не задан"),smallStyle); y+=72;
                Text(32,y,238,50,"ПКМ по подсвеченной клетке — задать или заменить маршрут.",smallStyle); y+=60;
                if(Button(new Rect(32,y,238,36),"Остаться на месте",Game.CanPlan))
                { Game.PlanWait(selectedUnit); view.ShowOrders(selectedUnit); }
                y+=44;
                if(selectedUnit.Kind==UnitKind.Settler)
                {
                    Hex target=selectedUnit.Order.Count>0 ? selectedUnit.Order[selectedUnit.Order.Count-1] : selectedUnit.Position;
                    bool can=Game.CanPlan && Game.CanFound(selectedUnit,target);
                    if(Button(new Rect(32,y,238,40),selectedUnit.FoundOrder ? "Отменить основание" : "Основать поселение",can,true))
                    {
                        if(selectedUnit.FoundOrder) selectedUnit.FoundOrder=false;
                        else Game.PlanFound(selectedUnit);
                    }
                    y+=48; Text(32,y,238,45,"Не ближе 4 клеток от другого города. Выполнится после движения.",smallStyle); y+=52;
                }
            }
            else if(selectedTile!=null)
            {
                Text(32,y,238,26,selectedTile.Explored ? TerrainName(selectedTile.Terrain) : "Неизведанный мир",cardTitleStyle); y+=36;
                Text(32,y,238,70,selectedTile.Explored ? ResourceName(selectedTile.Resource)+(selectedTile.River ? "\nРека: +1 пищи работающей клетке" : "") : "Отправьте разведчиков к краю известных земель.",smallStyle); y+=82;
            }
            y=Mathf.Max(y+10,h-320);
            if(y<h-155)
            {
                Text(32,y,238,24,"ЛЕТОПИСЬ",headingStyle); y+=30;
                journalScroll=GUI.BeginScrollView(new Rect(30,y,244,Mathf.Max(40,h-120-y)),journalScroll,new Rect(0,0,220,Game.Journal.Count*55));
                for(int i=0;i<Game.Journal.Count;i++) Text(2,i*55,212,50,Game.Journal[i],smallStyle);
                GUI.EndScrollView();
            }
        }
        void DrawRight()
        {
            float x=UIWidth-342;
            Box(new Rect(x,94,326,UIHeight-198),panel);
            x+=16;
            Text(x,111,294,23,"ГОРОД И ЕГО ЖИТЕЛИ",headingStyle);
            float y=146;
            Text(x,y,294,26,selectedCity.Name,cardTitleStyle); y+=35;
            Yield income=Game.Income(selectedCity),gross=Game.GrossIncome(selectedCity);
            Text(x,y,294,45,"Население "+selectedCity.Population+" / "+Game.PopulationCapacity(selectedCity)+" · земля "+selectedCity.Territory.Count+"\nРасширение: "+selectedCity.BorderCulture+" / "+Game.BorderCost(selectedCity)+" культуры",smallStyle); y+=48;
            Text(x,y,294,22,"Пища: "+gross.Food+" − "+selectedCity.Population*Game.Balance.FoodPerResident+" = "+Signed(income.Food),labelStyle); y+=29;
            Text(x,y,294,22,"Труд +"+income.Production+"   Наука +"+income.Science,smallStyle); y+=29;
            Text(x,y,294,23,"Рост: "+selectedCity.Food+" / "+Game.GrowthCost(selectedCity)+" · запас до "+Game.FoodCapacity(selectedCity),smallStyle); y+=28;
            Progress(x,y,294,selectedCity.Food,Game.GrowthCost(selectedCity),gold); y+=23;
            Text(x,y,294,22,"Центр: "+Game.Balance.CenterFood+" пищи, "+Game.Balance.CenterProduction+" труда, "+Game.Balance.CenterScience+" науки",smallStyle); y+=28;
            Text(x,y,294,22,"РАБОЧИЕ МЕСТА",headingStyle); y+=27;
            float jobsHeight=70;
            workersScroll=GUI.BeginScrollView(new Rect(x,y,294,jobsHeight),workersScroll,new Rect(0,0,270,Mathf.Max(jobsHeight,selectedCity.Workers.Count*49)));
            for(int i=0;i<selectedCity.Workers.Count;i++)
            {
                var job=selectedCity.Workers[i]; Yield yield=Game.WorkIncome(selectedCity,job.Position,job.BuildingId);
                Text(0,i*49,270,24,(job.ResidentId+1)+". "+job.Profession,labelStyle);
                Text(0,i*49+26,270,20,"Клетка "+job.Position+" · "+yield.Food+" пищи / "+yield.Production+" труда",smallStyle);
            }
            GUI.EndScrollView(); y+=jobsHeight+8;
            Text(x,y,294,22,"Свободны: "+(selectedCity.Population-selectedCity.Workers.Count)+" · освоено "+selectedCity.Improvements.Count,smallStyle); y+=30;
            CityFocus targetFocus=(selectedCity.PlannedFocus??selectedCity.Focus)==CityFocus.Food ? CityFocus.Production : CityFocus.Food;
            string focusName=(selectedCity.PlannedFocus??selectedCity.Focus)==CityFocus.Food ? "еда" : "производство";
            if(Button(new Rect(x,y,294,34),"Приоритет: "+focusName+(selectedCity.PlannedFocus.HasValue ? " [план]" : ""),Game.CanPlan))
            { Game.PlanFocus(selectedCity,targetFocus); Notify("В начале действий жители заново выберут рабочие места."); }
            y+=49;
            Text(x,y,294,22,"ПРОИЗВОДСТВО",headingStyle); y+=29;
            if(selectedCity.Production!=null)
            {
                Text(x,y,294,26,selectedCity.Production.Name,labelStyle); y+=31;
                string productionDescription=selectedCity.Production.Wonder ? selectedCity.Production.Description.Split('.')[0] : selectedCity.Production.Description;
                Text(x,y,294,42,productionDescription,smallStyle); y+=47;
                Text(x,y,294,22,"Труд: "+selectedCity.ProductionProgress+" / "+selectedCity.Production.Cost,smallStyle); y+=27;
                Progress(x,y,294,selectedCity.ProductionProgress,selectedCity.Production.Cost,new Color(.39f,.72f,.72f)); y+=18;
            }
            else
            {
                if(Button(new Rect(x,y,294,36),"Выбрать заказ",Game.CanPlan))
                { if(Game.ResearchReady) choiceWindow=1; else if(Game.CultureReady) choiceWindow=3; else choiceWindow=2; }
                y+=48;
            }
            Text(x,y,294,22,"РАЗВИТИЕ ДЕРЖАВЫ",headingStyle); y+=26;
            Text(x,y,294,64,"Наука: "+Game.Knowledge+(Game.ResearchReady ? " · открытие готово" : " · накапливается")+"\nКультура: "+Game.CultureStock+(Game.CultureReady ? " · открытие готово" : " · накапливается")+"\nНовые решения — через правую кнопку.",smallStyle);
        }
        static string Signed(int value) { return value>=0 ? "+"+value : value.ToString(); }
        void AdvancePlan()
        {
            if(!started || help || !Game.CanPlan) return;
            if(Game.ResearchReady) { choiceWindow=1; return; }
            if(Game.CultureReady) { choiceWindow=3; return; }
            foreach(var city in Game.OwnCities) if(city.Production==null && city.Offers.Count>0) { SelectCity(city); choiceWindow=2; return; }
            foreach(var unit in Game.OwnUnits) if(!unit.HasOrder) { SelectUnit(unit); Notify("Задайте маршрут ПКМ или нажмите Q, чтобы ждать."); return; }
            Execute();
        }
        void DrawChoiceModal()
        {
            if(Event.current.type==EventType.KeyDown && Event.current.keyCode==KeyCode.Escape) { choiceWindow=0; Event.current.Use(); return; }
            Box(new Rect(0,0,UIWidth,UIHeight),new Color(0,0,0,.72f));
            float x=UIWidth*.5f-455,y=UIHeight*.5f-260;
            Box(new Rect(x,y,910,520),panel); Box(new Rect(x,y,910,3),gold);
            bool research=choiceWindow==1;
            bool culture=choiceWindow==3;
            Text(x+32,y+25,790,40,research ? "Выберите технологию" : culture ? "Выберите культурное открытие" : "Производство: "+selectedCity.Name,new GUIStyle(titleStyle){fontSize=29});
            Text(x+32,y+75,820,30,research || culture ? "Открытие готово. Стоимость списывается из накопленного запаса державы." : "Постройки займут отдельные клетки. Отряды появятся после завершения заказа.",smallStyle);
            if(Button(new Rect(x+842,y+24,38,34),"×")) { choiceWindow=0; return; }
            var choices=research ? Game.ResearchOffers.ToArray() : culture ? Game.CultureOffers.ToArray() : selectedCity.Offers.ToArray();
            for(int i=0;i<choices.Length;i++)
            {
                var c=choices[i]; float cx=x+32+i*286;
                Box(new Rect(cx,y+125,274,350),card);
                Box(new Rect(cx,y+125,274,5),research ? gold : new Color(.37f,.72f,.69f));
                Text(cx+18,y+147,238,54,c.Name,cardTitleStyle);
                Text(cx+18,y+211,238,160,c.Description,new GUIStyle(labelStyle){fontSize=16});
                Text(cx+18,y+391,238,25,"Стоимость: "+c.Cost+(research ? " науки" : culture ? " культуры" : " труда"),smallStyle);
                bool affordable=!research && !culture || (research ? Game.Knowledge : Game.CultureStock)>=c.Cost;
                if(Button(new Rect(cx+18,y+428,238,34),"Выбрать",Game.CanPlan && affordable,true))
                {
                    bool ok=research ? Game.ChooseResearch(c.Id) : culture ? Game.ChooseCulture(c.Id) : Game.ChooseProduction(selectedCity,c.Id);
                    if(ok) { choiceWindow=0; Notify(c.Name+": решение принято."); }
                    return;
                }
            }
        }
        void DrawChoice(float x,float y,Choice choice,bool research,City city)
        {
            Rect r=new Rect(x,y,294,70);
            bool hover=r.Contains(Event.current.mousePosition);
            Box(r,hover ? new Color(.21f,.28f,.29f) : card);
            Box(new Rect(x,y,3,70),research ? gold : new Color(.35f,.7f,.7f));
            Text(x+13,y+9,212,25,choice.Name,cardTitleStyle);
            Text(x+236,y+13,48,22,choice.Cost.ToString(),headingStyle);
            Text(x+13,y+35,267,31,choice.Description,new GUIStyle(smallStyle){fontSize=13});
            bool was=GUI.enabled; GUI.enabled=Game.CanPlan;
            if(GUI.Button(r,GUIContent.none,GUIStyle.none))
            {
                if(research) Game.ChooseResearch(choice.Id); else Game.ChooseProduction(city,choice.Id);
                Notify(research ? "Изучаем: "+choice.Name : "Производство: "+choice.Name);
            }
            GUI.enabled=was;
        }
        void DrawBottom()
        {
            float y=UIHeight-86,w=UIWidth;
            Box(new Rect(0,y,w,86),panel);
            Text(24,y+14,520,24,Game.Phase==Phase.Action ? "Жители и отряды выполняют план" : "Центр + работа жителей = доход города",labelStyle);
            Text(24,y+46,770,25,"ЛКМ — выбор   ПКМ — маршрут   WASD — камера   Колесо — масштаб   Tab — отряд",smallStyle);
            if(Time.unscaledTime<toastUntil) Text(560,y+12,w-1010,55,toast,smallStyle);
            string blocking=Game.BlockingReason();
            int missing=0; foreach(var unit in Game.OwnUnits) if(!unit.HasOrder) missing++;
            bool production=false; foreach(var city in Game.OwnCities) if(city.Production==null && city.Offers.Count>0) production=true;
            string text=Game.Phase==Phase.Action ? "ВЫПОЛНЕНИЕ…" : online!=null && Game.PlansLocked ? "ЖДЁМ ДРУГА…" : online!=null && online.players<2 ? "ПРИГЛАСИТЕ ДРУГА" : Game.ResearchReady ? "ВЫБРАТЬ ТЕХНОЛОГИЮ  →" : Game.CultureReady ? "ВЫБРАТЬ КУЛЬТУРУ  →" : production ? "ВЫБРАТЬ ПРОИЗВОДСТВО  →" : missing>0 ? "ПРИКАЗЫ ОТРЯДАМ: "+missing+"  →" : "ЗАВЕРШИТЬ ХОД  →";
            if(Button(new Rect(w-330,y+16,306,52),text,Game.CanPlan,true)) AdvancePlan();
        }
        void DrawMapLabels()
        {
            foreach(var city in Game.Cities)
            {
                if(!Game.IsExplored(Game.ActivePlayer,city.Position)) continue;
                Vector3 p=cameraView.WorldToScreenPoint(view.Position(city.Position)+Vector3.up*1.45f);
                if(p.z<0) continue;
                float x=p.x/UIScale,y=(Screen.height-p.y)/UIScale;
                if(x<380 || x>UIWidth-430 || y<125 || y>UIHeight-150) continue;
                Box(new Rect(x-100,y-14,200,34),panel);
                var style=new GUIStyle(buttonStyle){fontSize=14};
                GUI.Label(new Rect(x-96,y-11,192,27),city.Name+"  ·  "+city.Population,style);
            }
            Text(310,UIHeight-128,410,22,"МИР "+seed+"  ·  открыто "+Game.ExploredCount+" / "+Game.World.Ordered.Count+" клеток",headingStyle);
        }
        void DrawStart()
        {
            float x=UIWidth*.5f-315,y=UIHeight*.5f-280;
            Box(new Rect(x,y,630,560),panel); Box(new Rect(x,y,630,3),gold);
            Text(x+38,y+32,554,24,"ПЕРВАЯ ИСТОРИЯ ВАШЕГО НАРОДА",headingStyle);
            Text(x+38,y+70,554,70,"И С Т О К И",new GUIStyle(titleStyle){fontSize=57});
            Text(x+38,y+157,554,72,"От первого костра к союзу поселений.\nКаждый ход — план, затем действие.",new GUIStyle(labelStyle){fontSize=23});
            Text(x+38,y+254,554,72,"Один житель — одно рабочее место. Город получает доход от центра и занятых клеток. Открывайте ресурсы, стройте и наблюдайте, как растёт поселение.",labelStyle);
            Text(x+38,y+352,175,25,"Зерно мира",smallStyle);
            seedText=GUI.TextField(new Rect(x+180,y+345,240,36),seedText,11,new GUIStyle(GUI.skin.textField){fontSize=20,padding=new RectOffset(9,9,7,4)});
            if(Button(new Rect(x+432,y+345,160,36),"Случайное")) seedText=UnityEngine.Random.Range(1,99999999).ToString();
            int parsed;
            bool valid=int.TryParse(seedText,out parsed);
            if(Button(new Rect(x+38,y+402,554,56),"ЗАЖЕЧЬ ПЕРВЫЙ ОЧАГ  →",valid,true)) NewRun(parsed);
            Text(x+38,y+480,554,46,"Экономика, наука, культура и общее разрешение ходов.\nWrap-карта и три ИИ — следующие шаги.",smallStyle);
        }
        void DrawHelp()
        {
            Box(new Rect(0,0,UIWidth,UIHeight),new Color(0,0,0,.7f));
            float x=UIWidth*.5f-350,y=UIHeight*.5f-300;
            Box(new Rect(x,y,700,600),panel);
            Text(x+32,y+28,636,40,"Как живёт ваше племя",titleStyle);
            Text(x+32,y+96,636,370,
                "1. Решения\nНаука и культура копятся. Когда достаточно очков, правая кнопка предлагает открытие: сначала наука, затем культура. После производства задайте всем отрядам движение или ожидание (Q).\n\n2. Город\nЦентр даёт базовый доход. Другие клетки работают только с жителем. Избыток пищи идёт в рост; культура расширяет границы.\n\n3. Ресурсы и здания\nОткрытие разрешает ресурс. Свободный житель строит тропу и улучшение. Мастерской нужен работник; очаг и амбар пассивны. Игла разрешает работу с мехами.\n\n4. Действия\nОбщий resolver рассчитывает движение и конкуренцию за клетки. Unity показывает готовый результат. ИИ и wrap ещё не подключены; конца эпохи нет.",labelStyle);
            Text(x+32,y+465,636,60,"WASD / стрелки — камера · F — к выбранному\nСКМ — перетаскивание · Enter — следующий шаг · Esc — снять выбор",smallStyle);
            if(Button(new Rect(x+32,y+539,636,40),"Продолжить",true,true)) help=false;
        }
        void DrawResult()
        {
            Box(new Rect(0,0,UIWidth,UIHeight),new Color(0,0,0,.7f));
            float x=UIWidth*.5f-300,y=UIHeight*.5f-220;
            Box(new Rect(x,y,600,440),panel); Box(new Rect(x,y,600,3),gold);
            Text(x+32,y+31,536,60,Game.Won ? "Первые шаги цивилизации" : "История первого племени",new GUIStyle(titleStyle){fontSize=29});
            Text(x+32,y+112,536,70,Game.Won ? "Ваши поселения стали домом для нового народа. Ранняя эпоха пройдена." : "48 ходов завершены. Попробуйте другой мир и иной путь развития.",labelStyle);
            Text(x+32,y+206,536,65,Game.OwnCities.Count+" поселений    ·    "+Game.TotalPopulation+" жителей\n"+Game.Technologies.Count+" открытий    ·    "+Game.ExploredCount+" клеток мира",cardTitleStyle);
            if(Button(new Rect(x+32,y+300,536,48),"Новый мир",true,true)) NewRun(UnityEngine.Random.Range(1,99999999));
            if(Button(new Rect(x+32,y+363,536,42),"Повторить зерно "+seed)) NewRun(seed);
        }
        static string TerrainName(Terrain t)
        {
            switch(t) { case Terrain.Ocean:return "Океан"; case Terrain.Coast:return "Побережье"; case Terrain.Forest:return "Лес"; case Terrain.Hills:return "Холмы"; case Terrain.Mountain:return "Горы"; case Terrain.Desert:return "Пустыня"; case Terrain.Plains:return "Равнина"; case Terrain.Swamp:return "Болото"; case Terrain.Tundra:return "Снежная тундра"; default:return "Луга"; }
        }
        static string ResourceName(Resource r)
        {
            switch(r) { case Resource.Berries:return "Ягоды: отдельный бонус пока не подключён"; case Resource.Grain:return "Зерно: нужно земледелие и работа жителя"; case Resource.Game:return "Дичь: нужны охота и рабочий"; case Resource.Cattle:return "Скот: нужны одомашнивание и пастух"; case Resource.Furs:return "Меха: нужны культурное открытие «Игла» и работник"; case Resource.Stone:return "Камень: нужна обработка камня и рабочий"; case Resource.Fish:return "Рыба: нужны рыболовство и рабочий"; case Resource.Discovery:return "Древняя стоянка: +3 знания отряду"; default:return "Нет особого ресурса. Доход — только при работе."; }
        }
    }
}

