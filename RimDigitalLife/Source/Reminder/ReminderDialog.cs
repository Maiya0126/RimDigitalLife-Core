using UnityEngine;
using Verse;

namespace RimDigitalLife
{
    public class ReminderDialog : Window
    {
        private string title;
        private string message;
        
        public override Vector2 InitialSize => new Vector2(420f, 240f);
        
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
            
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(0f, curY, inRect.width, 40f), title);
            curY += 50f;
            
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(0f, curY, inRect.width, 60f), message);
            curY += 80f;
            
            Text.Anchor = TextAnchor.UpperLeft;
            
            float buttonWidth = 120f;
            float buttonHeight = 40f;
            float buttonX = (inRect.width - buttonWidth) / 2f;
            
            if (Widgets.ButtonText(new Rect(buttonX, curY, buttonWidth, buttonHeight), "RDL_Reminder_OK".Translate()))
            {
                Close();
            }
            
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerRight;
            Widgets.Label(new Rect(0f, inRect.height - 20f, inRect.width - 10f, 20f), "@麦丫Maiya 大叔提醒");
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}