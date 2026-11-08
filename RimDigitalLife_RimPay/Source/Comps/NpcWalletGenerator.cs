using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimDigitalLife_RimPay
{
    // ========================================================================
    // NPC 数字钱包生成器：按派系科技等级给非玩家派系小人随机生成钱包余额
    //
    // 触发：每日结算（ProcessDailyPayroll 末尾）为"当前所有地图上的外来访客
    // 类小人"补钱包——每个小人一次性获得按科技档的随机初始金额；
    // 已有余额（>0）的小人不重复发放（一次性开户，不每日累加）。
    //
    // 覆盖范围（严格控制，避免误发）：
    // - 人类小人（排除动物/机械体的 RaceProps.Animal）
    // - 非玩家派系（排除殖民者/奴隶/囚犯/玩家派系临时成员）
    // - 排除敌对入侵者（raider 正在打架，发钱包没有消费场景，且被击杀会触发搜刮双算）
    // - 只针对"当前在玩家地图上、非敌对"的访客/商队/宾客（RimMarket/RSMF 消费主体）
    //
    // 科技档金额（对齐原版访客商队随身白银量级：外邦商队带 750~1200 银、
    // 新石器访客 50~250 银；钱包在此基础上按科技再放大——大老远来做生意的
    // 不可能只带零花钱，电子钱包约等于随身白银的 1~2 倍量级）：
    //   Animal     20~60     Neolithic  150~500   Medieval   300~800
    //   Industrial 750~2000  Spacer     1500~3000 Ultra      2500~5000
    //   Archotech  4000~8000 无派系     50~150
    // ========================================================================
    public static class NpcWalletGenerator
    {
        // 发放记录（ThingID → 已发过），存档跟随 personalWallets 体系：
        // 钱包余额>0 即视为已开户，无需独立清单；但为防"花光后重复开户刷钱"，
        // 用独立 HashSet 记录已开户 ID（存档持久化）
        private static HashSet<string> seededIds = new HashSet<string>();

        public static void ExposeData()
        {
            Scribe_Collections.Look(ref seededIds, "npcWalletSeededIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && seededIds == null)
            {
                seededIds = new HashSet<string>();
            }
        }

        // 每日结算入口
        public static void SeedDailyWallets()
        {
            var S = RimPayMod.settings;
            if (S == null || !S.enableNpcWallets) return;

            foreach (Map map in Find.Maps)
            {
                if (map == null) continue;
                // 快照遍历，发放过程不改 pawn 列表
                List<Pawn> pawns = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
                foreach (Pawn p in pawns)
                {
                    TrySeedOne(p, S);
                }
            }
        }

        // 单个小人开户判定 + 发放
        private static void TrySeedOne(Pawn p, RimPaySettings S)
        {
            try
            {
                if (p == null || p.Dead || !p.RaceProps.Humanlike) return;

                // 只给非玩家派系小人发（殖民者/奴隶/囚犯由工资系统覆盖）
                if (p.Faction == null || p.Faction.IsPlayer) return;
                if (p.HostFaction == Faction.OfPlayer) return;        // 囚犯/旅馆客人挂在玩家 Host 下
                if (p.IsPrisonerOfColony) return;

                // 排除正在入侵的敌对者（GenAttack.IncomingRaider 不可靠，用敌对判定）
                if (GenHostility.HostileTo(p, Faction.OfPlayer)) return;

                // 只给"访客类"发：有 Lord（商队/宾客/远行队等组织化访问），
                // 或携带白银的商人——散养野生动物部落小人（无 Lord 游民）不发，
                // 避免全地图野生小人集体刷钱包
                Lord lord = LordUtility.GetLord(p);
                if (lord == null) return;

                // 一次性开户：已发过（余额>0 或在 seededIds 里）不重复
                var comp = Current.Game?.GetComponent<GameComponent_RimPay>();
                if (comp == null) return;
                string key = p.ThingID;
                if (seededIds.Contains(key) || comp.GetBalance(p) > 0) return;

                int amount = RollAmountByTechLevel(p.Faction.def.techLevel, S);
                if (amount <= 0) return;

                comp.ModifyBalance(p, amount, "RP_Reason_PersonalSavings".Translate());
                seededIds.Add(key);
            }
            catch (System.Exception ex)
            {
                Verse.Log.Warning($"[RimPay] NPC wallet seeding exception ({p?.LabelShort ?? "null"}): {ex.Message}");
            }
        }

        // 按科技档随机金额（受设置倍率缩放）
        private static int RollAmountByTechLevel(TechLevel tl, RimPaySettings S)
        {
            float mult = Mathf.Max(0.1f, S.npcWalletMultiplier);
            int min, max;
            switch (tl)
            {
                case TechLevel.Animal:     min = 20;   max = 60;   break;
                case TechLevel.Neolithic:  min = 150;  max = 500;  break;
                case TechLevel.Medieval:   min = 300;  max = 800;  break;
                case TechLevel.Industrial: min = 750;  max = 2000; break;
                case TechLevel.Spacer:     min = 1500; max = 3000; break;
                case TechLevel.Ultra:      min = 2500; max = 5000; break;
                case TechLevel.Archotech:  min = 4000; max = 8000; break;
                default:                   min = 50;   max = 150;  break; // Undefined/无派系科技
            }
            return Mathf.RoundToInt(Rand.Range(min, max) * mult);
        }
    }
}
