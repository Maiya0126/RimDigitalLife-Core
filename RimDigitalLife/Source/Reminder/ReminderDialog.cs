using UnityEngine;
using Verse;

namespace RimDigitalLife
{
    public class ReminderDialog : Window
    {
        private string title;
        private string message;
        
        public override Vector2 InitialSize => new Vector2(440f, 260f);
        
        public ReminderDialog(string title, string message, bool pauseGame)
        {
            this.title = title;
            this.message = message;
            this.forcePause = pauseGame;
            this.closeOnCancel = true;
            this.closeOnAccept = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float curY = 0f;

            // 标题
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(0f, curY, inRect.width, 40f), title);
            curY += 50f;

            // 正文（如"该补充水分了！"）
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(0f, curY, inRect.width, 60f), message);
            curY += 80f;

            // 底部"该提示由..."小字（比正文更小）
            // 注意：GenText.WordWrapAt 内部会把全局字体切到 Medium，必须在它之后重新设回 Tiny
            string footer = "RDL_Reminder_Footer".Translate().ToString();
            string wrappedFooter = GenText.WordWrapAt(footer, inRect.width - 20f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            float footerHeight = Text.CalcHeight(wrappedFooter, inRect.width - 20f);
            Widgets.Label(new Rect(0f, curY, inRect.width, footerHeight), wrappedFooter);

            // "知道了"按钮：显示在最底部
            float buttonWidth = 120f;
            float buttonHeight = 40f;
            float buttonX = (inRect.width - buttonWidth) / 2f;
            float buttonY = inRect.height - buttonHeight - 12f;
            if (Widgets.ButtonText(new Rect(buttonX, buttonY, buttonWidth, buttonHeight), "RDL_Reminder_OK".Translate()))
            {
                Close();
            }

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
    }
}