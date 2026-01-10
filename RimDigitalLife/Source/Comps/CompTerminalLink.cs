using Verse;
using RimWorld;

namespace RimDigitalLife
{
    public class CompProperties_TerminalLink : CompProperties
    {
        public CompProperties_TerminalLink() { this.compClass = typeof(CompTerminalLink); }
    }

    public class CompTerminalLink : ThingComp
    {
        public override void Notify_Equipped(Pawn pawn) { base.Notify_Equipped(pawn); AddTerminalHediff(pawn); }
        public override void Notify_Unequipped(Pawn pawn) { base.Notify_Unequipped(pawn); RemoveTerminalHediff(pawn); }

        private void AddTerminalHediff(Pawn pawn)
        {
            if (pawn == null || pawn.health == null) return;
            var hediffDef = DefDatabase<HediffDef>.GetNamed("DigitalStorage_TerminalImplant", false);
            if (hediffDef == null) return; // 没装 Mod 就跳过

            if (!pawn.health.hediffSet.HasHediff(hediffDef))
                pawn.health.AddHediff(hediffDef);
        }

        private void RemoveTerminalHediff(Pawn pawn)
        {
            if (pawn == null || pawn.health == null) return;
            var hediffDef = DefDatabase<HediffDef>.GetNamed("DigitalStorage_TerminalImplant", false);
            if (hediffDef == null) return;

            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff != null && !HasOtherTerminal(pawn))
                pawn.health.RemoveHediff(hediff);
        }

        private bool HasOtherTerminal(Pawn pawn)
        {
            if (pawn.apparel == null) return false;
            foreach (var app in pawn.apparel.WornApparel)
            {
                if (app == this.parent) continue;
                if (app.TryGetComp<CompTerminalLink>() != null) return true;
            }
            return false;
        }
    }
}