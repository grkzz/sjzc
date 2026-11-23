using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ShenYi.Prototype
{
    public sealed partial class PrototypeGame
    {
        readonly CardSlotFeedback[,] eggFeedback=new CardSlotFeedback[2,5], monsterFeedback=new CardSlotFeedback[2,5];
        int draggingCard=-1;
        Button hoveredSlot;
        bool wasFeedbackActive;
        Card TargetCard { get { int id=draggingCard>=0?draggingCard:selectedCard; return Battle.Players[0].Hand.Find(x=>x.Id==id); } }
        bool HasTargetIntent { get { return Interactive() && (TargetCard!=null || selectedEgg>=0 || questTarget || selectedMonster>=0); } }
        void BeginTargetDrag(int id,Vector2 point)
        { draggingCard=id; MoveTargetDrag(point); RefreshSlotFeedback(); }
        void MoveTargetDrag(Vector2 point)
        { Button target; perspective.TryGetSlotAtScreen(point,null,out target); if(target!=hoveredSlot) { hoveredSlot=target; RefreshSlotFeedback(); } }
        void EndTargetDrag() { draggingCard=-1; hoveredSlot=null; RefreshSlotFeedback(); }
        void UpdateTargetHover()
        {
            if(Battle==null || perspective==null || overlay==null) return;
            bool active=HasTargetIntent && !overlay.gameObject.activeSelf;
            if(!active)
            {
                if(wasFeedbackActive) { hoveredSlot=null; RefreshSlotFeedback(); }
                wasFeedbackActive=false; return;
            }
            wasFeedbackActive=true;
            if(draggingCard>=0 || AutoSmokeTest) return;
            MoveTargetDrag(Input.mousePosition);
        }
        bool TargetAllowed(int owner,int slot,bool egg,out string reason)
        {
            reason="";
            if(!Interactive()) { reason="请等待自己的行动回合。"; return false; }
            if(slot>=Battle.OpenSlots(owner)) { reason="该位置将在第 4 个己方回合开放。"; return false; }
            var card=TargetCard; var player=Battle.Players[0];
            if(card!=null)
            {
                if(owner!=0) { reason="请放到下方黄绿色己方区域。"; return false; }
                if(card.IsEgg)
                { reason=egg ? "这里已有蛋，请选择空蛋位。" : "蛋卡需要放到最下排孵化区。"; return egg && player.Eggs[slot]==null; }
                if(card.Key=="watergun" || card.Key=="fold")
                { reason="这个道具需要选择己方已有的蛋。"; return egg && player.Eggs[slot]!=null; }
                if(card.Key=="craft")
                { reason="特殊工艺需要选择己方已有的怪兽。"; return !egg && player.Monsters[slot]!=null; }
                if(card.Key=="revive")
                { reason=player.LastDead==null ? "暂无阵亡怪兽可复活。" : "复活需要一个己方空怪兽位。"; return !egg && player.LastDead!=null && player.Monsters[slot]==null; }
                if(card.Key=="draw") { reason="抽卡道具可点按使用，或拖到己方怪兽位。"; return !egg; }
            }
            if(selectedEgg>=0 || questTarget)
            {
                reason="请选择己方已开放的空怪兽位。";
                return owner==0 && !egg && player.Monsters[slot]==null;
            }
            if(selectedMonster>=0)
            {
                var source=player.Monsters[selectedMonster]; reason="请选择敌方怪兽；攻击次数用尽时需等待下回合。";
                return owner==1 && !egg && Battle.Players[1].Monsters[slot]!=null && source!=null && source.Attacks>0;
            }
            return false;
        }
        void RefreshSlotFeedback()
        {
            if(Battle==null || hint==null) return;
            bool intent=HasTargetIntent && !overlay.gameObject.activeSelf;
            var card=TargetCard;
            string hoverHint=null;
            for(int p=0;p<2;p++) for(int i=0;i<5;i++)
            {
                bool locked=i>=Battle.OpenSlots(p); var e=Battle.Players[p].Eggs[i]; var m=Battle.Players[p].Monsters[i];
                for(int type=0;type<2;type++)
                {
                    bool egg=type==0; var button=egg?eggButtons[p,i]:monsterButtons[p,i];
                    var view=egg?eggFeedback[p,i]:monsterFeedback[p,i]; if(view==null) continue;
                    string reason; bool allowed=TargetAllowed(p,i,egg,out reason); bool legal=intent && allowed;
                    bool hovered=intent && hoveredSlot==button;
                    bool ready=egg && e!=null && e.Remaining==0 && p==0 && Interactive() && !overlay.gameObject.activeSelf;
                    bool attack=!egg && m!=null && p==0 && m.Attacks>0 && Interactive() && !overlay.gameObject.activeSelf;
                    string main=locked?"未开放":egg ? e==null?"":e.Remaining==0?"✓ 可孵化":"◷ 剩 "+e.Remaining : m==null?"":"<color=#FFD875>攻</color>"+m.Attack+" <color=#FF828F>♥</color>"+Math.Max(0,m.Health);
                    string extra=locked?"第4回合开放":egg?e==null?"":e.Tier+"阶  ✦"+e.Spirit : m==null?"":(m.Male?"♂":"♀")+" "+(m.IsBoss?"霸主":m.Tier+"阶")+" ✦"+Battle.TotalSpirit(p,m);
                    string action=hovered && !legal?"× 不可放置":legal?hovered?(draggingCard>=0?"↓ 松开放置":"✓ 点击确认"):"＋ 可选目标":ready?"↑ 点击孵化":attack?"可攻击 ×"+m.Attacks:"";
                    Texture preview=card!=null && card.IsEgg && egg && e==null?EggFace(data.Egg(card.Key).elements):null;
                    view.Present(legal,ready,attack,hovered,hovered && !legal,main,extra,action,preview);
                    if(hovered) hoverHint=legal?(draggingCard>=0?"松开卡牌放到此处。":"点击高亮位置完成操作。") : reason;
                }
            }
            if(hoverHint!=null) hint.text=hoverHint;
            else if(draggingCard>=0 && card!=null) hint.text=card.IsEgg?"绿色框是可放蛋位，移到目标后松手。":BattleModel.ToolDescription(card.Key);
            else if(Interactive())
            {
                int readyCount=Battle.Players[0].Eggs.Count(x=>x!=null && x.Remaining==0);
                hint.text=readyCount>0 && !intent ? "有 "+readyCount+" 枚蛋可孵化：点击发光的蛋，再点空怪兽位。" : actionHint;
            }
        }
    }
}
