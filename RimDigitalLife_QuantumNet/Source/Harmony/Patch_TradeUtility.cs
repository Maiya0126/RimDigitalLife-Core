using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_QuantumNet
{
    // 全图贸易信标（方案 A）：任何地图只要有通电信标 → 全图物品可贸易/发射
    // 1. 贸易物品判定：TradeUtility.AllLaunchableThingsForTrade
    // 2. 吊舱发射：TradeUtility.LaunchThingsOfType
    [HarmonyPatch(typeof(TradeUtility), "AllLaunchableThingsForTrade", new Type[] { typeof(Map), typeof(ITrader) })]
    public static class Patch_TradeUtility_AllLaunchableThingsForTrade
    {
        [HarmonyPrefix]
        public static bool Prefix(Map map, ITrader trader, ref IEnumerable<Thing> __result)
        {
            if (!MapWideBeaconUtility.HasMapWideBeacon(map)) return true;
            __result = MapWideBeaconUtility.AllLaunchableThingsForTrade(map, trader);
            return false;
        }
    }

    [HarmonyPatch(typeof(TradeUtility), "LaunchThingsOfType", new Type[] { typeof(ThingDef), typeof(int), typeof(Map), typeof(TradeShip) })]
    public static class Patch_TradeUtility_LaunchThingsOfType
    {
        [HarmonyPrefix]
        public static bool Prefix(ThingDef resDef, int debt, Map map, TradeShip trader)
        {
            if (!MapWideBeaconUtility.HasMapWideBeacon(map)) return true;

            while (debt > 0)
            {
                Thing thing = MapWideBeaconUtility.FindThingOfDef(resDef, map);
                if (thing == null)
                {
                    Verse.Log.Error("[量子网络] 未找到可转移给商船的任何 " + (resDef?.ToString() ?? "?") + "。");
                    break;
                }
                int num = Mathf.Min(debt, thing.stackCount);
                if (trader != null)
                {
                    trader.GiveSoldThingToTrader(thing, num, TradeSession.playerNegotiator);
                }
                else
                {
                    thing.SplitOff(num).Destroy();
                }
                debt -= num;
            }
            return false;
        }
    }
}
