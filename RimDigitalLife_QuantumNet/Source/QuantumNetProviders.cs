using System.Collections.Generic;
using Verse;

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
        // 名称与角色走翻译键（QN_Provider_*），新闻/日报/AI 剧情文本本地化
        public static readonly List<Provider> All = new List<Provider>
        {
            new Provider("Starlink", "QN_Provider_StarCom_Name", "QN_Provider_StarCom_Role"),
            new Provider("NebulaCloud", "QN_Provider_NebulaCloud_Name", "QN_Provider_NebulaCloud_Role"),
            new Provider("GameCart", "QN_Provider_CartoBoard_Name", "QN_Provider_CartoBoard_Role"),
            new Provider("CyberShop", "QN_Provider_CyberBazaar_Name", "QN_Provider_CyberBazaar_Role"),
            new Provider("MineScan", "QN_Provider_MineScan_Name", "QN_Provider_MineScan_Role")
        };

        public static string LocalizedName(this Provider p) => p.name.Translate();
        public static string LocalizedRole(this Provider p) => p.role.Translate();

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