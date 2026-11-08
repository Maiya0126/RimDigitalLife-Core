using System;
using System.Reflection;
using HarmonyLib;

namespace RimDigitalLife_RimPay
{
    // ========================================================================
    // 跨可选依赖 mod 的 Harmony patch 公共工具（纯反射版）
    //
    // 原因：Harmony 3.0（RimWorld 1.7/Waylith）引用 System.Runtime 10，
    // net472 程序集编译期绑定其成员签名会 CS1705；
    // 1.6(Harmony 2.4)/1.7(Harmony 3.0) 的 Patch(MethodBase, HarmonyMethod x4)
    // 与 HarmonyMethod(MethodInfo) 运行时签名一致，反射调用双版本共用。
    // 供 RimSimIntegration / RimMarketIntegration 共用。
    // ========================================================================
    internal static class CompatPatchHelper
    {
        // 纯反射调用 harmony.Patch(original, prefix, postfix, null, null)。
        // 注意：method 为 null 的方向必须传"字面 null"而非空 HarmonyMethod 实例——
        // Harmony 2.x 会静默过滤空实例，但 Harmony 3.0（1.7/Waylith）不会，
        // 空 instance 会进入 PatchProcessor 内部直接抛 TargetInvocationException
        // （1.7 实测：RimMarket/Bunnygirl/Hospitality 三个联动因此全部初始化失败）。
        public static void Patch(Harmony harmony, MethodBase original, MethodInfo prefix, MethodInfo postfix)
        {
            if (original == null) return;
            if (prefix == null && postfix == null) return; // 两个方向都没有，无需 patch
            harmony.GetType()
                .GetMethod("Patch", new[] { typeof(MethodBase), typeof(HarmonyMethod), typeof(HarmonyMethod), typeof(HarmonyMethod), typeof(HarmonyMethod) })
                .Invoke(harmony, new object[] { original, ToHM(prefix), ToHM(postfix), null, null });
        }

        // 仅 postfix 的便捷重载
        public static void PatchPostfix(Harmony harmony, MethodBase original, MethodInfo postfix)
        {
            Patch(harmony, original, null, postfix);
        }

        // MethodInfo → HarmonyMethod 实例；null → 字面 null（不造空实例，见上）
        private static object ToHM(MethodInfo mi)
        {
            if (mi == null) return null;
            Type hmType = typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonyMethod");
            return Activator.CreateInstance(hmType, new object[] { mi });
        }

        // 跨程序集按全名找类型（等价 AccessTools.TypeByName，未加载时返回 null）
        public static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(fullName, throwOnError: false);
                if (t != null) return t;
            }
            return null;
        }

        // 取某类型内 private static patch 方法
        public static MethodInfo GetPostfix(Type declaringType, string name)
        {
            return declaringType.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        }
    }
}
