using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_QuantumNet
{
    // ============================================================
    // 私有网购（方案 A 心情版）
    // 小人用自己的 RimPay 数字钱包网购虚拟产品（零食/玩具/游戏卡带/衣服/化妆品/虚拟主题）
    // 触发条件：网络覆盖 + 付费套餐 + 携带数码设备 + 闲暇 + 钱包有钱
    // 效果：从自己钱包扣 5~30 币（外部流出不进国库）→ 获得「收到网购包裹」心情
    // ============================================================
    public static class PrivateShoppingManager
    {
        public const string ThoughtDefName = "QuantumNet_OnlineShopping";

        private static int lastShopTick = 0;

        // 由 GameComponent 低频驱动（每 3000 tick 约 1 游戏小时一次机会）
        public static void Tick()
        {
            var S = QuantumNetMod.settings;
            if (S == null || !S.enablePrivateShopping) return;
            var comp = GameComponent_QuantumNet.Instance;
            if (comp == null) return;

            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            if (!comp.HasNetworkCoverage(map)) return;
            if (!QuantumNetRimPayBridge.IsRimPayAvailable) return; // 无 RimPay 无法扣费

            // 冷却：约每 2 游戏小时尝试一次机会
            int tick = Find.TickManager.TicksGame;
            if (tick - lastShopTick < 120000) return;
            lastShopTick = tick;

            // 卡带新品发售事件：网购热情高涨（概率翻倍）
            float chance = NetworkEventManager.ShoppingBoost ? 0.7f : 0.35f;

            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.Dead || pawn.Destroyed) continue;
                if (!comp.HasPaidPlan(pawn)) continue;               // 畅享/无限包功能
                if (!RimSeekBuffManager.HasDigitalDevice(pawn)) continue; // 需携带数码设备
                if (!IsLeisureTime(pawn)) continue;                  // 闲暇时才网购
                if (Rand.Chance(chance))
                {
                    TryShop(pawn);
                    break; // 每次机会只让一人网购
                }
            }
        }

        // 闲暇判定：非工作状态（娱乐/休息/游荡）
        private static bool IsLeisureTime(Pawn pawn)
        {
            if (pawn.CurJob == null) return true;
            return pawn.CurJob.def.joyKind != null
                || pawn.CurJob.def == JobDefOf.LayDown
                || pawn.CurJob.def == JobDefOf.Wait_Wander;
        }

        private static void TryShop(Pawn pawn)
        {
            int amount = Rand.RangeInclusive(5, 30);
            // 赛博百货大促事件：全部半价
            if (NetworkEventManager.ShoppingHalfPrice)
            {
                amount = Mathf.Max(1, Mathf.CeilToInt(amount / 2f));
            }
            int balance = QuantumNetRimPayBridge.GetBalance(pawn);
            if (balance < amount) return; // 钱不够就不买

            string item = RandomShopItem();
            if (QuantumNetRimPayBridge.TryModifyBalance(pawn, -amount, "私有网购·" + item))
            {
                ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail(ThoughtDefName);
                if (thought != null)
                {
                    pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
                }
                string saleTag = NetworkEventManager.ShoppingHalfPrice ? "（大促半价）" : "";
                Messages.Message($"[网购] {pawn.LabelShort} 用数字钱包在赛博百货购买了「{item}」{saleTag}，花费 {amount} @银，包裹已送达！", MessageTypeDefOf.NeutralEvent, false);
            }
        }

        private static string RandomShopItem()
        {
            string[] items = new string[]
            {
                "零食大礼包",
                "限量版玩具",
                "游戏卡带",
                "新款衣服",
                "化妆品",
                "虚拟主题皮肤"
            };
            return items[UnityEngine.Random.Range(0, items.Length)];
        }
    }
}
