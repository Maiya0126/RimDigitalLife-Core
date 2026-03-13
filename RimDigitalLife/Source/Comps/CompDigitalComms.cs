using System.Collections.Generic;
using Verse;
using RimWorld;
using Verse.AI;

namespace RimDigitalLife
{
    public class CompProperties_DigitalComms : CompProperties
    {
        public CompProperties_DigitalComms()
        {
            this.compClass = typeof(CompDigitalComms);
        }
    }

    public class CompDigitalComms : ThingComp
    {
        public Pawn Wearer
        {
            get
            {
                Apparel apparel = this.parent as Apparel;
                return apparel?.Wearer;
            }
        }

        public IEnumerable<FloatMenuOption> GetFloatMenuOptionsForPawn(Pawn pawn)
        {
            Pawn wearer = Wearer;
            if (wearer == null || pawn != wearer)
            {
                yield break;
            }

            if (!pawn.IsColonistPlayerControlled)
            {
                yield break;
            }

            if (pawn.Drafted)
            {
                yield break;
            }

            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
            {
                yield return new FloatMenuOption("CannotUseReason".Translate("IncapableOfCapacity".Translate(PawnCapacityDefOf.Talking.label, pawn.Named("PAWN"))), null);
                yield break;
            }

            if (!CommsConsoleUtility.PlayerHasPoweredCommsConsole())
            {
                yield return new FloatMenuOption("NeedPoweredComms".Translate(), null);
                yield break;
            }

            yield return new FloatMenuOption("UseComms".Translate(), delegate
            {
                TryOpenComms(pawn);
            });
        }

        private void TryOpenComms(Pawn pawn)
        {
            if (pawn.Drafted)
            {
                Messages.Message("CannotUseWhileDrafted".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            List<FloatMenuOption> list = new List<FloatMenuOption>();

            foreach (Faction faction in Find.FactionManager.AllFactionsVisibleInViewOrder)
            {
                if (faction.temporary || faction.IsPlayer || !faction.HostileTo(Faction.OfPlayer))
                {
                    FloatMenuOption option = new FloatMenuOption(faction.Name, delegate
                    {
                        faction.TryOpenComms(pawn);
                    });
                    list.Add(option);
                }
            }

            if (list.Count == 0)
            {
                foreach (PassingShip ship in pawn.Map.passingShipManager.passingShips)
                {
                    FloatMenuOption option = ship.CommFloatMenuOption(null, pawn);
                    if (!option.Disabled)
                    {
                        list.Add(option);
                    }
                }
            }

            if (list.Count == 0)
            {
                Messages.Message("NoOneToComm".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            if (list.Count == 1)
            {
                list[0].action();
            }
            else
            {
                FloatMenu menu = new FloatMenu(list);
                menu.layer = WindowLayer.GameUI;
                Find.WindowStack.Add(menu);
            }
        }

        private IEnumerable<ICommunicable> GetCommTargets(Pawn myPawn)
        {
            if (myPawn.Map == null) yield break;
            
            foreach (PassingShip ship in myPawn.Map.passingShipManager.passingShips)
            {
                yield return ship;
            }

            foreach (Faction faction in Find.FactionManager.AllFactionsVisibleInViewOrder)
            {
                if (!faction.temporary && !faction.IsPlayer)
                {
                    yield return faction;
                }
            }
        }
    }
}