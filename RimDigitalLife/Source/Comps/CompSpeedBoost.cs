using Verse;
using RimWorld;

namespace RimDigitalLife
{
    public class CompProperties_SpeedBoost : CompProperties
    {
        public string hediffDefName = "";

        public CompProperties_SpeedBoost()
        {
            this.compClass = typeof(CompSpeedBoost);
        }
    }

    public class CompSpeedBoost : ThingComp
    {
        private Pawn lastWearer;
        private bool initialized = false;

        public override void CompTick()
        {
            base.CompTick();

            Apparel apparel = this.parent as Apparel;
            Pawn currentWearer = apparel?.Wearer;

            if (currentWearer == null)
            {
                if (lastWearer != null)
                {
                    RemoveHediff(lastWearer);
                    lastWearer = null;
                }
                return;
            }

            if (currentWearer != lastWearer || !initialized)
            {
                if (lastWearer != null)
                {
                    RemoveHediff(lastWearer);
                }
                
                lastWearer = currentWearer;
                AddHediff(currentWearer);
                initialized = true;
            }
        }

        private void AddHediff(Pawn pawn)
        {
            if (pawn == null || pawn.health == null) return;
            CompProperties_SpeedBoost props = this.props as CompProperties_SpeedBoost;
            if (string.IsNullOrEmpty(props.hediffDefName)) return;
            
            var hediffDef = DefDatabase<HediffDef>.GetNamed(props.hediffDefName, false);
            if (hediffDef == null)
            {
                Log.Error("Hediff not found: " + props.hediffDefName);
                return;
            }

            if (!pawn.health.hediffSet.HasHediff(hediffDef))
            {
                pawn.health.AddHediff(hediffDef);
            }
        }

        private void RemoveHediff(Pawn pawn)
        {
            if (pawn == null || pawn.health == null) return;
            CompProperties_SpeedBoost props = this.props as CompProperties_SpeedBoost;
            if (string.IsNullOrEmpty(props.hediffDefName)) return;
            
            var hediffDef = DefDatabase<HediffDef>.GetNamed(props.hediffDefName, false);
            if (hediffDef == null) return;

            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }
    }
}