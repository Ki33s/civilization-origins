using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Origins
{
    public sealed partial class DevelopmentGalleryViewer
    {
        IEnumerator CheckDecor(string folder)
        {
            Directory.CreateDirectory(folder);
            yield return null;
            var original=DecorProfileStore.Copy(DecorProfileStore.Active);
            string saved=File.ReadAllText(DecorProfileStore.SavePath);
            bool passed=true;
            Action<bool,string> check=(ok,name)=> { File.AppendAllText(Path.Combine(folder,"checks.txt"),(ok ? "PASS " : "FAIL ")+name+"\n"); if(!ok) passed=false; };
            try
            {
                var catalog=Resources.Load<DecorModelCatalog>("DecorModelCatalog");
                check(catalog.models.Length==51,"51 reusable model prefabs");
                bool validModels=true;
                foreach(var model in catalog.models) validModels&=model.prefab && model.prefab.GetComponent<LODGroup>() && model.triangles>0;
                check(validModels,"every model retains individual near/medium/far meshes");
                SwitchTab(0);
                var forest=Inventory(groups[0],"Forest");
                int trees=forest.large;
                check(forest.small<=20 && Inventory(groups[0],"Tundra").small<=20,"sparse small decor defaults");
                check(Inventory(groups[0],"Tundra").large==36,"36 snowy conifers preserved");
                CheckInstances(forest,check);
                SwitchTab(4);
                check(groups[4].GetComponent<WorldArtPreview>().TileCount==560,"full world retains 560 terrain tiles");
                var before=groups[4].GetComponentsInChildren<DecorTileInventory>();
                check(before.Length==560,"independent decor inventory on every hex");
                DecorTileInventory first=null,second=null;
                foreach(var inv in before) if(inv.biome=="Forest") { if(!first) first=inv; else { second=inv; break; } }
                check(first && second && first.transform.GetChild(0).GetChild(0).localPosition!=second.transform.GetChild(0).GetChild(0).localPosition,"forest hexes have different random placements");
                CheckInstances(first,check);
                var forestSettings=decorPanel.Draft.Get(Terrain.Forest);
                foreach(var quantity in forestSettings.models) if(quantity.model=="grass") quantity.min=quantity.max=0;
                Array.Resize(ref forestSettings.models,forestSettings.models.Length+1);
                forestSettings.models[forestSettings.models.Length-1]=new DecorQuantity("stone_Granite",3){min=2,max=5};
                check(decorPanel.Apply(false),"editor applies zero grass and an added stone model range");
                forest=Inventory(groups[0],"Forest");
                check(Count(forest,"grass")==0 && Count(forest,"stone_Granite")>=2 && Count(forest,"stone_Granite")<=5 && forest.large==trees,"native tile preview updates counts while preserving trees");
                bool worldMatches=true; var amounts=new System.Collections.Generic.HashSet<int>();
                foreach(var inv in groups[4].GetComponentsInChildren<DecorTileInventory>()) if(inv.biome=="Forest")
                {
                    worldMatches&=Count(inv,"grass")==0 && Count(inv,"stone_Granite")>=2 && Count(inv,"stone_Granite")<=5 && inv.large==trees;
                    amounts.Add(Count(inv,"stone_Granite"));
                }
                check(worldMatches && amounts.Count>1,"full world refresh uses edited random quantity ranges");
                string temporary=Path.Combine(folder,"saved-profile.json");
                DecorProfileStore.Save(DecorProfileStore.Active,temporary);
                var loaded=JsonUtility.FromJson<TerrainDecorProfile>(File.ReadAllText(temporary)); loaded.Validate(catalog);
                check(loaded.Get(Terrain.Forest).models[loaded.Get(Terrain.Forest).models.Length-1].max==5,"saved JSON preserves the added model and range");
                check(File.ReadAllText(DecorProfileStore.SavePath)==saved,"diagnostics preserve the user's saved profile");
                var invalid=DecorProfileStore.Copy(loaded); invalid.minScale=float.NaN;
                bool rejected=false; try { invalid.Validate(catalog); } catch(InvalidOperationException) { rejected=true; }
                check(rejected,"invalid scale rejected before generation");
                SwitchTab(5); check(items.Length==51,"model gallery contains 51 selectable examples");
            }
            catch(Exception e) { check(false,e.ToString()); }
            finally { DecorProfileStore.Active=original; RefreshDecor(); }
            decorPanel=new TerrainDecorPanel{Applied=RefreshDecor,InspectModel=InspectDecorModel,InspectBiome=InspectBiome};
            SwitchTab(0); FocusItem(0); decorPanel.open=true;
            yield return new WaitForSeconds(.5f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"forest-editor-ui.png"));
            yield return new WaitForSeconds(.5f);
            File.WriteAllText(Path.Combine(folder,"result.txt"),passed ? "PASS" : "FAIL");
            Debug.Log("DECOR CHECKS: "+(passed ? "PASS" : "FAIL")); Application.Quit(passed ? 0 : 1);
        }
        static DecorTileInventory[] Inventories(GameObject group) { return group.GetComponentsInChildren<DecorTileInventory>(true); }
        static DecorTileInventory Inventory(GameObject group,string biome)
        {
            foreach(var inv in Inventories(group)) if(inv.biome==biome) return inv;
            throw new InvalidOperationException("Missing tile "+biome);
        }
        static int Count(DecorTileInventory inventory,string id)
        {
            foreach(var quantity in inventory.counts) if(quantity.model==id) return quantity.min;
            return 0;
        }
        static void CheckInstances(DecorTileInventory inventory,Action<bool,string> check)
        {
            var individual=inventory.transform.Find("Individual models");
            bool ok=individual.childCount==inventory.total;
            var catalog=Resources.Load<DecorModelCatalog>("DecorModelCatalog");
            foreach(Transform item in individual)
            {
                var definition=catalog.Get(item.name);
                ok&=definition!=null && item.GetComponent<LODGroup>() && item.localScale.x>=.5f && item.localScale.x<=1.5f;
                if(definition!=null)
                {
                    var actual=item.GetComponentsInChildren<MeshFilter>(true);
                    var source=definition.prefab.GetComponentsInChildren<MeshFilter>(true);
                    ok&=actual.Length==source.Length;
                    for(int i=0;i<Math.Min(actual.Length,source.Length);i++) ok&=actual[i].sharedMesh==source[i].sharedMesh;
                }
            }
            check(ok,"each scattered model is a separate instance sharing only its source meshes");
        }
    }
}
