using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Origins
{
    // Gallery editor: changes actual model quantities, previews them, and saves the shared game profile.
    public sealed class TerrainDecorPanel
    {
        public bool open;
        TerrainDecorProfile draft;
        DecorModelCatalog catalog;
        Terrain[] kinds=(Terrain[])Enum.GetValues(typeof(Terrain));
        int biome=3;
        Vector2 scroll,pickerScroll;
        bool picking;
        string filter="",message="Количество указано на одну клетку.";
        readonly Dictionary<string,string> inputs=new Dictionary<string,string>();
        public Action Applied;
        public Action<string> InspectModel;
        public Action<Terrain> InspectBiome;
        public TerrainDecorProfile Draft { get { Ensure(); return draft; } }
        public Rect Area { get; private set; }
        public void Ensure()
        {
            if(draft!=null) return; catalog=Resources.Load<DecorModelCatalog>("DecorModelCatalog"); draft=DecorProfileStore.Copy(DecorProfileStore.Active);
        }
        public void Choose(Terrain type) { biome=Array.IndexOf(kinds,type); picking=false; }
        public bool OverMouse()
        {
            return open && Area.Contains(new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y));
        }
        public void Draw()
        {
            if(!open) return; Ensure(); Area=new Rect(Screen.width-416,96,404,Screen.height-148);
            GUI.color=new Color(.06f,.11f,.10f,.98f); GUI.DrawTexture(Area,Texture2D.whiteTexture); GUI.color=Color.white;
            GUILayout.BeginArea(new Rect(Area.x+14,Area.y+12,Area.width-28,Area.height-24));
            GUILayout.BeginHorizontal(); GUILayout.Label("ПАРАМЕТРЫ КЛЕТКИ",new GUIStyle(GUI.skin.label){fontSize=19,fontStyle=FontStyle.Bold});
            if(GUILayout.Button("×",GUILayout.Width(28))) { open=false; GUIUtility.keyboardControl=0; } GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); if(GUILayout.Button("‹",GUILayout.Width(30))) ChangeBiome(-1);
            GUILayout.Label(TerrainDecorProfile.Name(kinds[biome]),new GUIStyle(GUI.skin.label){fontSize=20,alignment=TextAnchor.MiddleCenter});
            if(GUILayout.Button("›",GUILayout.Width(30))) ChangeBiome(1); GUILayout.EndHorizontal();
            GUILayout.Label("Модели на клетку: минимум / максимум",new GUIStyle(GUI.skin.label){fontSize=13});
            var settings=draft.Get(kinds[biome]); int smallMin=0,smallMax=0,largeMin=0,largeMax=0;
            foreach(var q in settings.models) { if(catalog.Get(q.model).large) { largeMin+=q.min; largeMax+=q.max; } else { smallMin+=q.min; smallMax+=q.max; } }
            GUILayout.Label("Мелкие: "+Range(smallMin,smallMax)+"   Крупные: "+Range(largeMin,largeMax));
            GUILayout.Label(Landscape(kinds[biome]),new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=12});
            scroll=GUILayout.BeginScrollView(scroll,GUILayout.Height(Mathf.Max(120,Area.height-410-(picking ? 150 : 0))));
            int remove=-1;
            for(int i=0;i<settings.models.Length;i++)
            {
                var q=settings.models[i]; var model=catalog.Get(q.model); GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                if(GUILayout.Button(model.label,GUILayout.Height(25))) InspectModel?.Invoke(model.id);
                if(GUILayout.Button("×",GUILayout.Width(24))) remove=i; GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(); GUILayout.Label(model.id,GUILayout.Width(190));
                q.min=IntField(settings.terrain+"_"+q.model+"_min",q.min,42); GUILayout.Label("—",GUILayout.Width(14)); q.max=IntField(settings.terrain+"_"+q.model+"_max",q.max,42);
                GUILayout.EndHorizontal(); GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
            if(remove>=0)
            {
                inputs.Remove(settings.terrain+"_"+settings.models[remove].model+"_min"); inputs.Remove(settings.terrain+"_"+settings.models[remove].model+"_max");
                var list=new List<DecorQuantity>(settings.models); list.RemoveAt(remove); settings.models=list.ToArray();
            }
            if(GUILayout.Button(picking ? "Закрыть список моделей" : "+ Добавить модель")) picking=!picking;
            if(picking)
            {
                filter=GUILayout.TextField(filter); pickerScroll=GUILayout.BeginScrollView(pickerScroll,GUILayout.Height(118));
                foreach(var model in catalog.models)
                {
                    bool present=false; foreach(var q in settings.models) if(q.model==model.id) { present=true; break; }
                    if(present || !string.IsNullOrEmpty(filter) && (model.label+model.id+model.category).IndexOf(filter,StringComparison.OrdinalIgnoreCase)<0) continue;
                    if(GUILayout.Button(model.label+" · "+model.category)) { var list=new List<DecorQuantity>(settings.models); list.Add(new DecorQuantity(model.id,model.large ? 1 : 3)); settings.models=list.ToArray(); picking=false; break; }
                }
                GUILayout.EndScrollView();
            }
            GUILayout.Space(5); GUILayout.BeginHorizontal(); GUILayout.Label("Масштаб",GUILayout.Width(80));
            draft.minScale=FloatField("scaleMin",draft.minScale); GUILayout.Label("—",GUILayout.Width(15)); draft.maxScale=FloatField("scaleMax",draft.maxScale); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label("Seed декора",GUILayout.Width(100)); draft.seed=IntField("seed",draft.seed,160); GUILayout.EndHorizontal();
            GUILayout.Label("Повороты моделей: 0–360°. Одинаковые числа — точное количество.",new GUIStyle(GUI.skin.label){fontSize=12,wordWrap=true});
            GUILayout.BeginHorizontal(); if(GUILayout.Button("Применить",GUILayout.Height(31))) Apply(false);
            if(GUILayout.Button("Сохранить для игры",GUILayout.Height(31))) Apply(true); GUILayout.EndHorizontal();
            if(GUILayout.Button("Загрузить сохранённые"))
            {
                try { DecorProfileStore.Reload(); draft=DecorProfileStore.Copy(DecorProfileStore.Active); inputs.Clear(); Applied?.Invoke(); message="Сохранённые параметры загружены."; }
                catch(Exception e) { message=e.Message; }
            }
            GUILayout.Label(message,new GUIStyle(GUI.skin.label){fontSize=12,wordWrap=true}); GUILayout.EndArea();
        }
        public bool Apply(bool save)
        {
            try
            {
                foreach(var input in inputs)
                {
                    float number; int integer;
                    bool valid=input.Key=="scaleMin" || input.Key=="scaleMax" ? float.TryParse(input.Value.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out number) : int.TryParse(input.Value,out integer);
                    if(!valid) throw new InvalidOperationException("Проверьте числовые поля.");
                }
                draft.Validate(catalog); DecorProfileStore.Active=DecorProfileStore.Copy(draft);
                if(save) DecorProfileStore.Save(DecorProfileStore.Active); Applied?.Invoke();
                message=save ? "Сохранено. Новая игра использует эти параметры." : "Просмотр обновлён. Сохраните, чтобы перенести в новую игру.";
                return true;
            }
            catch(Exception e) { message=e.Message; return false; }
        }
        void ChangeBiome(int step)
        {
            biome=(biome+kinds.Length+step)%kinds.Length; picking=false;
            InspectBiome?.Invoke(kinds[biome]);
        }
        int IntField(string key,int value,float width)
        {
            string text; if(!inputs.TryGetValue(key,out text)) text=value.ToString(); GUI.SetNextControlName("Decor_"+key); text=GUILayout.TextField(text,GUILayout.Width(width)); inputs[key]=text;
            int parsed; return int.TryParse(text,out parsed) ? parsed : value;
        }
        float FloatField(string key,float value)
        {
            string text; if(!inputs.TryGetValue(key,out text)) text=value.ToString("0.##",CultureInfo.InvariantCulture); GUI.SetNextControlName("Decor_"+key); text=GUILayout.TextField(text,GUILayout.Width(60)); inputs[key]=text;
            float parsed; return float.TryParse(text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out parsed) ? parsed : value;
        }
        static string Range(int a,int b) { return a==b ? a.ToString() : a+"–"+b; }
        static string Landscape(Terrain t)
        {
            switch(t) { case Terrain.Desert:return "Основа сохранена: 4 дюны. Дюны — часть рельефа."; case Terrain.Mountain:return "Основа сохранена: 3 горных пика."; case Terrain.Hills:return "Основа сохранена: 2 подъёма рельефа."; case Terrain.Coast: case Terrain.Ocean:return "Берег и движение воды сохранены. Это часть поверхности."; case Terrain.Swamp:return "Водные участки болота сохранены."; default:return "Рельеф не меняется. Нажмите модель, чтобы посмотреть её отдельно."; }
        }
    }
}
