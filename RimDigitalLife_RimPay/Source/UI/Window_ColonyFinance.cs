using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_RimPay
{
    // 殖民地 RimPay 财报总览窗口
    public class Window_ColonyFinance : Window
    {
        private Vector2 pawnScroll = Vector2.zero;
        private Map map;

        public Window_ColonyFinance(Map map)
        {
            this.map = map;
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(600f, 680f);

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_RimPay comp = Current.Game.GetComponent<GameComponent_RimPay>();
            if (comp == null || map == null) return;

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // 标题
            Text.Font = GameFont.Medium;
            listing.Label("RP_W_Finance_Title".Translate());
            Text.Font = GameFont.Small;
            listing.GapLine();

            // 资产总览
            int physicalSilver = map.resourceCounter.GetCount(ThingDefOf.Silver);
            int treasury = comp.CloudTreasuryBalance;
            listing.Label("RP_W_Finance_AssetTitle".Translate());
            listing.Label("RP_W_Finance_AssetPhysical".Translate(physicalSilver));
            listing.Label("RP_W_Finance_AssetTreasury".Translate(treasury));
            listing.Label("RP_W_Finance_AssetTotal".Translate(physicalSilver + treasury));
            listing.Gap();

            // 殖民者财政名册
            listing.Label("RP_W_Finance_RosterTitle".Translate());
            listing.Gap(4f);
            Rect pawnRect = listing.GetRect(360f);

            List<Pawn> colonists = map.mapPawns.FreeColonists.Where(p => !p.Dead).OrderByDescending(p => comp.GetBalance(p)).ToList();

            Widgets.BeginScrollView(pawnRect, ref pawnScroll, new Rect(pawnRect.x, pawnRect.y, pawnRect.width - 16f, colonists.Count * 26f + 20f));
            Listing_Standard inner = new Listing_Standard();
            inner.Begin(new Rect(pawnRect.x, pawnRect.y, pawnRect.width - 16f, colonists.Count * 26f + 20f));

            if (colonists.Count == 0)
            {
                inner.Label("RP_W_Finance_NoColonists".Translate());
            }
            else
            {
                foreach (Pawn p in colonists)
                {
                    int bal = comp.GetBalance(p);
                    int salary = GetSalary(comp, p);
                    // 显示姓名、余额、预估日薪
                    inner.Label("RP_W_Finance_RosterLine".Translate(p.LabelShort, bal, salary));
                    inner.Gap(3f);
                }
            }

            inner.End();
            Widgets.EndScrollView();

            listing.Gap(8f);

            // 说明文字
            listing.Label("RP_W_Finance_Footnote".Translate());

            listing.End();
        }

        private int GetSalary(GameComponent_RimPay comp, Pawn pawn)
        {
            // 与薪资计算保持一致
            int salary = 10;
            if (pawn.DevelopmentalStage == DevelopmentalStage.Baby || pawn.DevelopmentalStage == DevelopmentalStage.Child) return 5;
            if (pawn.IsSlaveOfColony) return 2;
            int bestSkill = 0;
            if (pawn.skills != null)
            {
                foreach (var skill in pawn.skills.skills)
                {
                    if (skill.Level > bestSkill) bestSkill = skill.Level;
                }
            }
            if (bestSkill >= 15) salary += 40;
            else if (bestSkill >= 10) salary += 20;
            else if (bestSkill >= 6) salary += 10;
            return salary;
        }
    }
}