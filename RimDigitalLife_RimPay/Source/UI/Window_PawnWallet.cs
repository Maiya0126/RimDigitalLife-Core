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
            listing.Label($"{pawn.LabelShort} 的 RimPay 数字钱包");
            Text.Font = GameFont.Small;
            listing.GapLine();

            // 数字国库余额参考
            listing.Label($"<i>数字国库余额: {comp.CloudTreasuryBalance} @银</i>");
            listing.Gap(4f);

            // 当前余额
            int balance = comp.GetBalance(pawn);
            Text.Font = GameFont.Medium;
            listing.Label($"当前余额: {balance} @银");
            Text.Font = GameFont.Small;
            listing.Gap();

            // 收支流水
            listing.Label("<b>近期收支记录 (单位: @银):</b>");
            List<TransactionRecord> transactions = comp.GetTransactions(pawn);
            listing.Gap(4f);
            Rect logView = listing.GetRect(200f);

            if (transactions.Count == 0)
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(logView, "暂无收支记录。\n每日游戏时间 00:00 完成结算后，会自动生成「每日工资 / 住宿房租 / 基金利息」等记录。\n请确保数字国库有余额（可先用通讯台的【数字国库】存入白银），再等待下一个凌晨。");
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
                    Widgets.Label(new Rect(row.x + 4f, row.y, row.width - 8f, row.height), $"<color={colorHex}>{t.GetSummary()}</color>");
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;

                    rowY += rowHeight;
                }

                Widgets.EndScrollView();
            }
            listing.Gap(8f);

            // ---- 发放奖金 ----
            listing.Label("<b>发放奖金 (从数字国库划拨):</b>");
            Rect bonusRow = listing.GetRect(30f);
            Rect bonusInput = new Rect(bonusRow.x, bonusRow.y, bonusRow.width - 110f, bonusRow.height);
            Widgets.TextFieldNumeric(bonusInput, ref bonusInt, ref bonusBuffer);
            Rect bonusBtn = new Rect(bonusRow.x + bonusRow.width - 100f, bonusRow.y, 100f, bonusRow.height);
            if (ColoredButton(bonusBtn, "发放", new Color(0.15f, 0.55f, 0.2f)))
            {
                GiveBonus(comp, bonusInt);
            }
            listing.Gap(8f);

            // ---- 没收资金 ----
            listing.Label("<b>没收资金 (充入数字国库):</b>");
            Rect fineRow = listing.GetRect(30f);
            Rect fineInput = new Rect(fineRow.x, fineRow.y, fineRow.width - 220f, fineRow.height);
            Widgets.TextFieldNumeric(fineInput, ref fineInt, ref fineBuffer);
            Rect fineFullBtn = new Rect(fineInput.xMax + 5f, fineRow.y, 90f, fineRow.height);
            if (ColoredButton(fineFullBtn, "全额没收", new Color(0.55f, 0.15f, 0.15f)))
            {
                Confiscate(comp, comp.GetBalance(pawn));
            }
            Rect fineBtn = new Rect(fineInput.xMax + 100f, fineRow.y, 105f, fineRow.height);
            if (ColoredButton(fineBtn, "没收", new Color(0.55f, 0.15f, 0.15f)))
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
                comp.ModifyTreasury(-amount, "发放奖金");
                comp.ModifyBalance(pawn, amount, "玩家发放奖金");
                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("RimDigital_ReceivedBonus"));
                Messages.Message($"已向 {pawn.LabelShort} 发放 {amount} @银 奖金！", MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                Messages.Message("数字国库余额不足，无法发放奖金！", MessageTypeDefOf.RejectInput, false);
            }
        }

        private void Confiscate(GameComponent_RimPay comp, int amount)
        {
            if (amount <= 0) return;
            int current = comp.GetBalance(pawn);
            int toTake = Mathf.Min(amount, current);
            if (toTake <= 0)
            {
                Messages.Message($"{pawn.LabelShort} 身无分文，无可没收。", MessageTypeDefOf.RejectInput, false);
                return;
            }
            comp.ModifyBalance(pawn, -toTake, "资金被没收");
            comp.ModifyTreasury(toTake, "没收充公");
            pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("RimDigital_Fined"));
            Messages.Message($"已没收 {pawn.LabelShort} 的 {toTake} @银 充入数字国库！", MessageTypeDefOf.NegativeEvent, false);
        }
    }
}