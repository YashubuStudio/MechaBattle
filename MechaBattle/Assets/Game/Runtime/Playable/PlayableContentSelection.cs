using System;
using System.Collections.Generic;
using MechaBattle.Battlefield;
using MechaBattle.Construction;
using MechaBattle.Contracts;
using MechaBattle.Contracts.Construction;
using UnityEngine;
namespace MechaBattle.Playable
{
    public sealed partial class PlayableEntry
    {
        private readonly List<MapCatalogEntry> mapChoices=new List<MapCatalogEntry>();
        private readonly List<MapDefinition> mapDefinitions=new List<MapDefinition>();
        private int mapChoiceIndex;
        public string SelectedMapId=>map.id;
        private string MapTheme=>mapChoices.Count==0?"industrial":mapChoices[mapChoiceIndex].theme;
        private void LoadMapChoices()
        {
            var text=Resources.Load<TextAsset>("MechaBattleMaps/MapCatalog");
            if(text!=null)
            {
                var choices=JsonUtility.FromJson<MapCatalog>(text.text);
                var maps=MapCatalogPolicy.ValidateAndLoad(choices,resource=>
                {
                    var asset=Resources.Load<TextAsset>(resource);
                    if(asset==null)throw new InvalidOperationException("Map TextAsset is missing: "+resource);
                    return JsonUtility.FromJson<MapDefinition>(asset.text);
                });
                for(int i=0;i<maps.Length;i++){mapChoices.Add(choices.entries[i]);mapDefinitions.Add(maps[i]);}
            }
            mapChoiceIndex=mapChoices.FindIndex(choice=>choice.id==map.id);
            if(mapChoiceIndex<0 && mapChoices.Count>0 && (map.id=="p0-three-lanes"||map.id=="terraced-works"))
            {mapChoiceIndex=0;map=mapDefinitions[0];}
            else if(mapChoiceIndex<0)
            {
                mapChoiceIndex=0;mapChoices.Insert(0,new MapCatalogEntry{id=map.id,title="シーンのマップ",description="シーンに設定された戦場",theme="industrial"});mapDefinitions.Insert(0,map);
            }
            else mapDefinitions[mapChoiceIndex]=map; // Keep explicitly configured scene/test map data.
        }
        public void OpenMapPicker()
        {
            if(!IsInPreparation)return;
            var labels=new string[mapChoices.Count];
            for(int i=0;i<labels.Length;i++)labels[i]=mapChoices[i].title+"\n"+(mapDefinitions[i].halfWidth*2).ToString("0")+" × "+(mapDefinitions[i].halfDepth*2).ToString("0")+" m\n各軍3本拠点 / 中央戦線と側面の進入路\n"+mapChoices[i].description;
            ui.OpenPartPicker("戦場を選択",labels,mapChoiceIndex,index=>SelectMap(mapChoices[index].id),"戦場を変えると出撃地点の選択を初期化します。デッキは維持します。",null,true);
        }
        public void SelectMap(string id)
        {
            if(!IsInPreparation)return;int index=mapChoices.FindIndex(choice=>choice.id==id);if(index<0 || index==mapChoiceIndex)return;
            mapChoiceIndex=index;map=mapDefinitions[index];initialMainBase=selectedMainBase=spawnPoint=-1;
            minimap.Dispose();prepDeploymentMap.Dispose();respawnDeploymentMap.Dispose();
            minimap=new PlayableMinimap(ui.MinimapPanel,map);
            prepDeploymentMap=new DeploymentMapView(ui.PrepDeploymentPanel,map,font,SelectDeploymentPoint);
            respawnDeploymentMap=new DeploymentMapView(ui.RespawnDeploymentPanel,map,font,SelectDeploymentPoint);
            notice=mapChoices[index].title+"を選択しました。";RefreshMapLabel();RefreshBattlePrep();
        }
        private void RefreshMapLabel()
        {ui.MapLabel.text="マップ : "+mapChoices[mapChoiceIndex].title+"  / "+(map.halfWidth*2).ToString("0")+"×"+(map.halfDepth*2).ToString("0")+"m  変更 ›";}
        public void OpenPaintPicker()
        {
            if(!IsInPreparation)return;
            var build=profile.deck[selectedDeck];var labels=new string[BuildAppearance.PaintCount];
            for(int i=0;i<labels.Length;i++)labels[i]=PaintTitle(BuildAppearance.PaintId(i))+"\n外観のみ変更 / 性能・コストは共通\n肩・ひざ・頭頂部は陣営色に固定";
            ui.OpenPartPicker("機体の塗装",labels,BuildAppearance.PaintIndex(build.appearance?.paintId),index=>SelectPaint(BuildAppearance.PaintId(index)),"塗装は構成保存に含まれ、プレビューと戦闘で反映します。",null,true);
        }
        public void SelectPaint(string id)
        {
            if(!IsInPreparation || BuildAppearance.PaintIndex(id)<0)return;
            profile.deck[selectedDeck].appearance=new AppearanceDefinition{paintId=id};
            notice=PaintTitle(id)+"を選択しました。";RefreshPreparation();RefreshBattlePrep();
        }
        private static string PaintTitle(string id)
        {return id=="White"?"ホワイト":id=="Sand"?"イエロー":id=="Olive"?"グリーン":id=="Graphite"?"ブラック":id=="Red"?"レッド":id=="Cobalt"?"ブルー":"陣営カラー";}
    }
}
