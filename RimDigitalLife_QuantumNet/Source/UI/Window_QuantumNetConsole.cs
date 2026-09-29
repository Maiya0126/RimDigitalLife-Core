using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_QuantumNet
{
    // 量子网络控制台：网络覆盖状态 + 殖民者套餐管理
    public class Window_QuantumNetConsole : Window
    {
        private Map map;
        private Vector2 scroll = Vector2.zero;

        public Window_QuantumNetConsole(Map map)
        {
            this.map = map;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.forcePause = false;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(1040f, 540f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), "量子网络控制台 (Quantum Net Console)");
            Text.Font = GameFont.Small;

            float y = inRect.y + 38f;
            var comp = GameComponent_QuantumNet.Instance;
            var S = QuantumNetMod.settings;

            // 网络覆盖状态
            bool coverage = comp != null && comp.HasNetworkCoverage(map);
            Rect coverageRow = new Rect(inRect.x, y, inRect.width, 24f);
            GUI.color = coverage ? new Color(0.3f, 0.9f, 0.3f) : new Color(1f, 0.3f, 0.3f);
            Widgets.Label(coverageRow, coverage
                ? "● 当前地图已接入量子网络 (星链覆盖 100%)"
                : "● 当前地图无网络覆盖 - 请建造并通电通讯台");
            GUI.color = Color.white;
            y += 30f;

            // 运营商事件状态
            string eventStatus = NetworkEventManager.GetStatusText();
            if (eventStatus != "无")
            {
                Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f), "⚡ 运营商事件：" + eventStatus);
            }
            y += 26f;

            // 套餐价格
            int period = S?.planPeriodDays ?? 15;
            int hp = S?.highSpeedPrice ?? 60;
            int inf = S?.quantumInfinitePrice ?? 180;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 20f),
                $"套餐价格 (每 {period} 天):  高速畅享包 {hp} @银  |  量子无限包 {inf} @银  |  本地流量包 免费");
            y += 26f;

            if (!QuantumNetRimPayBridge.IsRimPayAvailable)
            {
                GUI.color = new Color(1f, 0.8f, 0.3f);
                Widgets.Label(new Rect(inRect.x, y, inRect.width, 20f),
                    "未检测到 RimPay 模组：付费套餐不可用，将自动降级为本地流量包。");
                GUI.color = Color.white;
                y += 26f;
            }

            y += 4f;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f), "<b>殖民者套餐</b>");
            y += 28f;

            Rect viewRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y - 10f);
            List<Pawn> colonists = map != null && map.mapPawns != null
                ? map.mapPawns.FreeColonists
                : new List<Pawn>();
            float contentH = colonists.Count * 34f + 10f;
            Rect scrollRect = new Rect(0f, 0f, viewRect.width - 20f, contentH);
            Widgets.BeginScrollView(viewRect, ref scroll, scrollRect);

            float itemY = 0f;
            foreach (Pawn pawn in colonists)
            {
                if (pawn.Dead || pawn.Destroyed) continue;
                QuantumPlan plan = comp != null ? comp.GetPawnPlan(pawn) : QuantumPlan.Local;

                Rect row = new Rect(0f, itemY, scrollRect.width, 30f);
                Widgets.DrawBoxSolid(row, new Color(0.2f, 0.2f, 0.2f, 0.15f));
                Widgets.Label(new Rect(row.x + 8f, row.y + 4f, 120f, 22f), pawn.LabelShort);
                Widgets.Label(new Rect(row.x + 132f, row.y + 4f, 150f, 22f), "当前: " + plan.GetLabel());

                float bx = row.x + 290f;
                DrawPlanButton(new Rect(bx, row.y + 3f, 200f, 24f), pawn, QuantumPlan.Local, plan);
                bx += 208f;
                DrawPlanButton(new Rect(bx, row.y + 3f, 200f, 24f), pawn, QuantumPlan.HighSpeed, plan);
                bx += 208f;
                DrawPlanButton(new Rect(bx, row.y + 3f, 200f, 24f), pawn, QuantumPlan.Infinite, plan);

                itemY += 34f;
            }
            Widgets.EndScrollView();
        }

        private void DrawPlanButton(Rect rect, Pawn pawn, QuantumPlan target, QuantumPlan current)
        {
            if (current == target)
            {
                GUI.color = new Color(0.3f, 0.7f, 1f);
                Widgets.Label(rect, target.GetLabel());
                GUI.color = Color.white;
                return;
            }
            if (Widgets.ButtonText(rect, target.GetLabel()))
            {
                GameComponent_QuantumNet.Instance?.TrySetPlan(pawn, target);
            }
        }
    }
}
