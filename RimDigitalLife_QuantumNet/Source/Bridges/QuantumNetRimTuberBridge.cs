using System;
using System.Reflection;
using Verse;

namespace RimDigitalLife_QuantumNet
{
    // RimTuber 跨模组桥（可选联动，反射调用 RimTuberAPI）
    // 用途：判断玩家（主播）当前是否在直播
    public static class QuantumNetRimTuberBridge
    {
        private static bool _initialized = false;
        private static bool _available = false;
        private static MethodInfo _isLiveMethod;

        public static bool IsRimTuberAvailable
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

            bool installed = false;
            foreach (Mod m in LoadedModManager.ModHandles)
            {
                if (m?.Content?.PackageIdPlayerFacing != null
                    && m.Content.PackageIdPlayerFacing.IndexOf("RimTuber", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    installed = true;
                    break;
                }
            }
            if (!installed) return;

            try
            {
                var asm = Assembly.Load("RimTuber");
                if (asm == null) return;
                var apiType = asm.GetType("RimTuber.RimTuberAPI");
                if (apiType == null) return;
                _isLiveMethod = apiType.GetMethod("IsLive", BindingFlags.Public | BindingFlags.Static, null, new Type[0], null);
                _available = _isLiveMethod != null;
                if (_available)
                {
                    Verse.Log.Message("[量子网络] RimTuber 桥初始化成功，可联动直播打赏。");
                }
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[量子网络] RimTuber 桥初始化失败: {ex.Message}");
            }
        }

        // 玩家当前是否正在直播（RimTuber 直播功能启用）
        public static bool IsLive()
        {
            if (!IsRimTuberAvailable || _isLiveMethod == null) return false;
            try
            {
                return (bool)_isLiveMethod.Invoke(null, null);
            }
            catch
            {
                return false;
            }
        }
    }
}
