using Verse;
using RimWorld;
using UnityEngine;

namespace RimDigitalLife
{
    public class CompProperties_ElectricSkateboard : CompProperties
    {
        public float normalFallChance = 0.05f;
        public float roughTerrainFallChance = 0.33f;
        public bool affectedByTerrain = true;
        public string hediffDefName = "RimDigital_Hediff_Skateboard";

        public CompProperties_ElectricSkateboard()
        {
            this.compClass = typeof(CompElectricSkateboard);
        }
    }

    public class CompElectricSkateboard : ThingComp
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

            if (currentWearer.Spawned && !currentWearer.Downed && currentWearer.pather.Moving)
            {
                if (currentWearer.IsHashIntervalTick(600))
                {
                    CompProperties_ElectricSkateboard props = this.props as CompProperties_ElectricSkateboard;
                    float fallChance = props.normalFallChance;

                    if (props.affectedByTerrain)
                    {
                        TerrainDef terrain = currentWearer.Position.GetTerrain(currentWearer.Map);
                        if (terrain != null && terrain.pathCost > 20)
                        {
                            fallChance = props.roughTerrainFallChance;
                        }
                    }

                    if (Rand.Value < fallChance)
                    {
                        currentWearer.stances.stunner.StunFor(180, currentWearer, addBattleLog: false);
                        MoteMaker.ThrowText(currentWearer.DrawPos + new Vector3(0, 0, 0.5f), currentWearer.Map, "哎呀，摔倒了！", Color.red);
                        DamageInfo dinfo = new DamageInfo(DamageDefOf.Crush, 2f, 0f, -1f, this.parent);
                        currentWearer.TakeDamage(dinfo);
                    }
                }
            }
        }

        private void AddHediff(Pawn pawn)
        {
            if (pawn == null || pawn.health == null) return;
            CompProperties_ElectricSkateboard props = this.props as CompProperties_ElectricSkateboard;
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
            CompProperties_ElectricSkateboard props = this.props as CompProperties_ElectricSkateboard;
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