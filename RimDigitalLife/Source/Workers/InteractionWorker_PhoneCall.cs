using RimWorld;
using Verse;

namespace RimDigitalLife
{
    public class InteractionWorker_PhoneCall : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            if (!HasPhone(initiator) || !HasPhone(recipient)) return 0f;
            return 0.8f;
        }

        private bool HasPhone(Pawn p)
        {
            if (p.apparel == null) return false;
            foreach (var app in p.apparel.WornApparel)
            {
                if (app.def.apparel.tags != null && app.def.apparel.tags.Contains("RimPhone_Communication"))
                    return true;
            }
            return false;
        }
    }
}