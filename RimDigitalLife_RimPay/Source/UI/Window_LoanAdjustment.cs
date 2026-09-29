using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_RimPay
{
    public class Window_LoanAdjustment : Window
    {
        private Faction faction;
        private bool isLending; // true = 向其放贷, false = 从其借款
        private int amount = 500;
        private string amountBuffer = "500";
        private int maxAmount = 10000;
        private Action<int> onConfirm;

        public Window_LoanAdjustment(Faction faction, bool isLending, int currentLimit, Action<int> onConfirm)
        {
            this.faction = faction;
            this.isLending = isLending;
            this.maxAmount = Mathf.Clamp(currentLimit, 100, 10000);

            // 默认金额从 RimPay 设置读取（玩家可调），并夹紧到合法区间
            int defaultAmount = Mathf.Clamp((RimPayMod.settings?.loanDefaultAmount ?? 500), 100, this.maxAmount);
            this.amount = defaultAmount;
            this.amountBuffer = defaultAmount.ToString();

            this.onConfirm = onConfirm;
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(400f, 260f);

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            string title = isLending ? $"向 {faction.Name} 放贷" : $"从 {faction.Name} 借款";
            listing.Label(title);
            Text.Font = GameFont.Small;
            listing.GapLine();

            listing.Label($"输入金额 (范围: 100 - {maxAmount} @银):");
            
            // 数值输入
            Rect inputRect = listing.GetRect(28f);
            Widgets.TextFieldNumeric(inputRect, ref amount, ref amountBuffer, 100, maxAmount);

            // 滑动条
            listing.Gap(8f);
            amount = Mathf.RoundToInt(listing.Slider(amount, 100, maxAmount));
            amountBuffer = amount.ToString();

            listing.Gap(12f);

            // 确认与取消按钮
            Rect btnRect = listing.GetRect(32f);
            Rect cancelBtn = new Rect(btnRect.x, btnRect.y, 110f, btnRect.height);
            if (Widgets.ButtonText(cancelBtn, "取消"))
            {
                Close();
            }

            Rect confirmBtn = new Rect(btnRect.xMax - 110f, btnRect.y, 110f, btnRect.height);
            Color confirmBg = isLending ? new Color(0.15f, 0.55f, 0.2f) : new Color(0.15f, 0.4f, 0.8f);
            if (ColoredButton(confirmBtn, "确认", confirmBg))
            {
                if (amount >= 100 && amount <= maxAmount)
                {
                    onConfirm?.Invoke(amount);
                    Close();
                }
                else
                {
                    Messages.Message("请输入有效的借贷金额 (不低于 100 且不超过上限)。", MessageTypeDefOf.RejectInput, false);
                }
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
            {
                Event.current.Use();
                return true;
            }
            return false;
        }
    }
}
