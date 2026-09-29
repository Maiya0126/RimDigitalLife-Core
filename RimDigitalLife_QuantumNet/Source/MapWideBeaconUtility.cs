using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimDigitalLife_QuantumNet
{
    // ============================================================
    // 全图贸易信标（方案 A）
    // 任何地图（主基地/远征营地/临时据点）只要存在任意一个通电的
    // 轨道贸易信标（OrbitalTradeBeacon），全图物品即可用于轨道贸易与吊舱发射。
    // 不依赖通讯台 / 量子网络覆盖；用于替代 Map Wide Orbital Trade Beacon 模组。
    // ============================================================
    public static class MapWideBeaconUtility
    {
        public static bool HasMapWideBeacon(Map map)
        {
            var S = QuantumNetMod.settings;
            if (S == null || !S.enableMapWideBeacon) return false;
            if (map == null) return false;
            return Building_OrbitalTradeBeacon.AllPowered(map).Any();
        }

        // 全图可贸易物品（替代信标范围枚举；处理书柜/服装架/普通物品）
        public static IEnumerable<Thing> AllLaunchableThingsForTrade(Map map, ITrader trader)
        {
            if (map == null) yield break;

            HashSet<Thing> yielded = new HashSet<Thing>();
            foreach (Thing t in map.listerThings.AllThings)
            {
                if (t == null || !t.Spawned) continue;

                if (t is Building_Bookcase bcase)
                {
                    foreach (Book book in bcase.HeldBooks)
                    {
                        if (TradeUtility.PlayerSellableNow(book, trader) && yielded.Add(book))
                        {
                            yield return book;
                        }
                    }
                }
                else if (t is Building_OutfitStand stand)
                {
                    foreach (Thing held in stand.HeldItems)
                    {
                        if (TradeUtility.PlayerSellableNow(held, trader) && yielded.Add(held))
                        {
                            yield return held;
                        }
                    }
                }
                else if (t.def.category == ThingCategory.Item
                    && TradeUtility.PlayerSellableNow(t, trader)
                    && yielded.Add(t))
                {
                    yield return t;
                }
            }
        }

        // 全图按 def 查找物品（用于吊舱发射）
        public static Thing FindThingOfDef(ThingDef resDef, Map map)
        {
            if (map == null) return null;
            foreach (Thing t in map.listerThings.AllThings)
            {
                if (t != null && t.Spawned && t.def == resDef)
                {
                    return t;
                }
            }
            return null;
        }
    }
}
