using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_RimPay
{
    public class Window_PawnWallet : Window
    {
        private Pawn pawn;
        private int bonusInt = 50;
        private int fineInt = 0;
        private string bonusBuffer = "50";
        private string fineBuffer = "";
        private Vector2 scrollPosition = Vector2.zero;

        public Window_PawnWallet(Pawn pawn)
        {
            this.pawn = pawn;
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(480f, 560f);

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_RimPay comp = Current.Game.GetComponent<GameComponent_RimPay>();
            if (comp == null || pawn == null) return;

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // 标题
            Text.Font = GameFont.Medium;
            listing.Label("RP_W_Wallet_Title".Translate(pawn.LabelShort));
            Text.Font = GameFont.Small;
            listing.GapLine();

            // 数字国库余额参考
            listing.Label("RP_W_Wallet_TreasuryRef".Translate(comp.CloudTreasuryBalance));
            listing.Gap(4f);

            // 当前余额
            int balance = comp.GetBalance(pawn);
            Text.Font = GameFont.Medium;
            listing.Label("RP_W_Wallet_Balance".Translate(balance));
            Text.Font = GameFont.Small;
            listing.Gap();

            // 收支流水
            listing.Label("RP_W_Wallet_RecordsTitle".Translate());
            List<TransactionRecord> transactions = comp.GetTransactions(pawn);
            listing.Gap(4f);
            Rect logView = listing.GetRect(200f);

            if (transactions.Count == 0)
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(logView, "RP_W_Wallet_NoRecords".Translate());
                Text.Font = GameFont.Small;
            }
            else
            {
                // 纯 Rect 手绘行（不用内嵌 Listing_Standard，避免 BeginGroup 坐标偏移把内容裁掉）
                float rowHeight = 24f;
                float viewHeight = transactions.Count * rowHeight + 10f;
                Rect viewRect = new Rect(logView.x, logView.y, logView.width - 16f, Mathf.Max(viewHeight, logView.height));
                Widgets.BeginScrollView(logView, ref scrollPosition, viewRect);

                float rowY = viewRect.y;
                for (int i = 0; i < transactions.Count; i++)
                {
                    var t = transactions[i];
                    Rect row = new Rect(viewRect.x, rowY, viewRect.width, rowHeight);
                    Widgets.DrawBoxSolid(row, new Color(0.12f, 0.12f, 0.12f, 0.25f));

                    string colorHex = t.isIncome ? "#7fd97f" : "#ff7f7f";
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(row.x + 4f, row.y, row.width - 8f, row.height), "RP_W_Wallet_RecordLine".Translate(colorHex, t.GetSummary()));
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;

                    rowY += rowHeight;
                }

                Widgets.EndScrollView();
            }
            listing.Gap(8f);

            // ---- 发放奖金 ----
            listing.Label("RP_W_Wallet_BonusTitle".Translate());
            Rect bonusRow = listing.GetRect(30f);
            Rect bonusInput = new Rect(bonusRow.x, bonusRow.y, bonusRow.width - 110f, bonusRow.height);
            Widgets.TextFieldNumeric(bonusInput, ref bonusInt, ref bonusBuffer);
            Rect bonusBtn = new Rect(bonusRow.x + bonusRow.width - 100f, bonusRow.y, 100f, bonusRow.height);
            if (ColoredButton(bonusBtn, "RP_W_Wallet_BonusBtn".Translate(), new Color(0.15f, 0.55f, 0.2f)))
            {
                GiveBonus(comp, bonusInt);
            }
            listing.Gap(8f);

            // ---- 没收资金 ----
            listing.Label("RP_W_Wallet_FineTitle".Translate());
            Rect fineRow = listing.GetRect(30f);
            Rect fineInput = new Rect(fineRow.x, fineRow.y, fineRow.width - 220f, fineRow.height);
            Widgets.TextFieldNumeric(fineInput, ref fineInt, ref fineBuffer);
            Rect fineFullBtn = new Rect(fineInput.xMax + 5f, fineRow.y, 90f, fineRow.height);
            if (ColoredButton(fineFullBtn, "RP_W_Wallet_FineFullBtn".Translate(), new Color(0.55f, 0.15f, 0.15f)))
            {
                Confiscate(comp, comp.GetBalance(pawn));
            }
            Rect fineBtn = new Rect(fineInput.xMax + 100f, fineRow.y, 105f, fineRow.height);
            if (ColoredButton(fineBtn, "RP_W_Wallet_FineBtn".Translate(), new Color(0.55f, 0.15f, 0.15f)))
            {
                Confiscate(comp, fineInt);
            }

            listing.End();
        }

        private static bool ColoredButton(Rect rect, string label, Color bgColor)
        {
            bool hovered = Mouse.IsOver(rect);
            Color baseColor = hovered ? (bgColor * 1.2f) : bgColor;
            baseColor.a = 1f;
            Widgets.DrawRectFast(rect, baseColor);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.white;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            if (hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            { Event.current.Use(); return true; }
            return false;
        }

        private void GiveBonus(GameComponent_RimPay comp, int amount)
        {
            if (amount <= 0) return;
            if (comp.CloudTreasuryBalance >= amount)
            {
                comp.ModifyTreasury(-amount, "RP_Reason_GiveBonus");
                comp.ModifyBalance(pawn, amount, "RP_Reason_PlayerBonus");
                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("RimDigital_ReceivedBonus"));
                Messages.Message("RP_W_Wallet_BonusDone".Translate(pawn.LabelShort, amount), MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                Messages.Message("RP_W_Wallet_BonusNoFunds".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }

        private void Confiscate(GameComponent_RimPay comp, int amount)
        {
            if (amount <= 0) return;
            int current = comp.GetBalance(pawn);
            int toTake = Mathf.Min(amount, current);
            if (toTake <= 0)
            {
                Messages.Message("RP_W_Wallet_FineNoFunds".Translate(pawn.LabelShort), MessageTypeDefOf.RejectInput, false);
                return;
            }
            comp.ModifyBalance(pawn, -toTake, "RP_Reason_Confiscated");
            comp.ModifyTreasury(toTake, "RP_Reason_ConfiscateIn");
            pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("RimDigital_Fined"));
            Messages.Message("RP_W_Wallet_FineDone".Translate(pawn.LabelShort, toTake), MessageTypeDefOf.NegativeEvent, false);
        }
    }
}
