using System.Collections.Generic;

namespace RimDigitalLife_QuantumNet
{
    // ============================================================
    // 量子网络虚拟供应商（剧情/服务提供方设定）
    // 仅用于新闻、套餐、AI 对话等剧情文本，不加入 RimPay 股票市场。
    // ============================================================
    public static class QuantumNetProviders
    {
        public class Provider
        {
            public string id;
            public string name;
            public string role;

            public Provider(string id, string name, string role)
            {
                this.id = id;
                this.name = name;
                this.role = role;
            }
        }

        // 3~5 家虚拟供应商：星链提供商 / 云算力商 / 数字内容商
        public static readonly List<Provider> All = new List<Provider>
        {
            new Provider("Starlink", "星链通信 (StarCom)", "量子网络星链覆盖提供商"),
            new Provider("NebulaCloud", "星云算力 (NebulaCloud)", "云算力与 RimSeek 智算服务商"),
            new Provider("GameCart", "游戏卡带发行商 (CartoBoard)", "数字娱乐内容与游戏卡带供应商"),
            new Provider("CyberShop", "赛博百货 (CyberBazaar)", "虚拟网购与数字商品平台"),
            new Provider("MineScan", "深矿雷达 (MineScan)", "云端深矿雷达与地质预测服务商")
        };

        public static Provider GetById(string id)
        {
            foreach (Provider p in All)
            {
                if (p.id == id) return p;
            }
            return null;
        }

        public static Provider RandomProvider()
        {
            if (All.Count == 0) return null;
            return All[UnityEngine.Random.Range(0, All.Count)];
        }
    }
}