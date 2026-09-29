using System;
using System.Reflection;
using RimWorld;
using Verse;

namespace RimDigitalLife_QuantumNet
{
    // RimPay 跨模组桥（可选联动，反射调用，避免编译期强依赖）
    // 用途：套餐月租从 RimPay 钱包自动扣（外部流出，不进国库）
    public static class QuantumNetRimPayBridge
    {
        private static bool _initialized = false;
        private static bool _available = false;
        private static Type _gameCompType;
        private static MethodInfo _getBalanceMethod;
        private static MethodInfo _modifyBalanceMethod;
        private static MethodInfo _modifyTreasuryMethod;

        public static bool IsRimPayAvailable
        {
            get
            {
                if (!_initialized) Initialize();
                return _available;
            }
        }

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (ModLister.GetActiveModWithIdentifier("maiya.RimDigitalLife.RimPay") == null)
            {
                return;
            }

            try
            {
                var asm = Assembly.Load("RimDigitalLife_RimPay");
                if (asm == null) return;
                _gameCompType = asm.GetType("RimDigitalLife_RimPay.GameComponent_RimPay");
                if (_gameCompType == null) return;

                _getBalanceMethod = _gameCompType.GetMethod("GetBalance", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(Pawn) }, null);
                _modifyBalanceMethod = _gameCompType.GetMethod("ModifyBalance", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(Pawn), typeof(int), typeof(string) }, null);
                _modifyTreasuryMethod = _gameCompType.GetMethod("ModifyTreasury", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(int) }, null);

                _available = _getBalanceMethod != null && _modifyBalanceMethod != null && _modifyTreasuryMethod != null;
                if (_available)
                {
                    Verse.Log.Message("[量子网络] RimPay 钱包桥初始化成功，套餐月租将从小人钱包自动扣除。");
                }
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[量子网络] RimPay 桥初始化失败: {ex.Message}");
            }
        }

        private static object GetComp()
        {
            if (!IsRimPayAvailable) return null;
            if (Current.Game == null) return null;
            return Current.Game.GetComponent(_gameCompType);
        }

        public static int GetBalance(Pawn pawn)
        {
            var comp = GetComp();
            if (comp == null || pawn == null) return 0;
            try
            {
                return (int)_getBalanceMethod.Invoke(comp, new object[] { pawn });
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[量子网络] 读取 RimPay 余额失败: {ex.Message}");
                return 0;
            }
        }

        public static bool TryModifyBalance(Pawn pawn, int amount, string reason)
        {
            var comp = GetComp();
            if (comp == null || pawn == null) return false;
            try
            {
                _modifyBalanceMethod.Invoke(comp, new object[] { pawn, amount, reason });
                return true;
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[量子网络] 修改 RimPay 余额失败: {ex.Message}");
                return false;
            }
        }

        // 打赏资金直接进数字国库（玩家=主播）
        public static bool TryModifyTreasury(int amount)
        {
            var comp = GetComp();
            if (comp == null) return false;
            try
            {
                _modifyTreasuryMethod.Invoke(comp, new object[] { amount });
                return true;
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[量子网络] 修改 RimPay 国库失败: {ex.Message}");
                return false;
            }
        }
    }
}
