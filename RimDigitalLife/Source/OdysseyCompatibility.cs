using System;
using RimWorld;
using Verse;
using HarmonyLib;

namespace RimDigitalLife
{
    public static class OdysseyCompatibility
    {
        public static bool HasOdysseyDLC => ModLister.GetActiveModWithIdentifier("Ludeon.RimWorld.Odyssey") != null;

        public static bool IsOnShip(Pawn pawn)
        {
            if (pawn?.Map?.Parent == null) return false;
            if (pawn.Map.Parent.def.defName.Contains("OdysseyShip")) return true;
            if (pawn.Map.Biome.defName.Contains("OuterSpace")) return true;
            return false;
        }

        public static bool IsInInterstellarTravel(Pawn pawn)
        {
            // 假设逻辑：检查地图是否在 FTL 中
            // 如果未来 API 变动，可在此修改
            return false;
        }

        public static float GetSpaceCommunicationBonus(Pawn pawn)
        {
            if (!HasOdysseyDLC || !IsOnShip(pawn)) return 0f;
            return 0.2f;
        }

        public static bool CanUseDevice(Pawn pawn, Thing device)
        {
            if (pawn == null || device == null) return false;
            if (device.def.defName == "Apparel_Nokirim") return true;
            if (IsInInterstellarTravel(pawn) && device.def.defName == "Apparel_Rimdle") return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_PhoneCall), "RandomSelectionWeight")]
    public static class Patch_OdysseyCommunication
    {
        public static void Postfix(Pawn initiator, Pawn recipient, ref float __result)
        {
            if (OdysseyCompatibility.HasOdysseyDLC && OdysseyCompatibility.IsOnShip(initiator))
            {
                __result *= 1.5f;
            }
        }
    }
}