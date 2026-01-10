using RimWorld;
using Verse;

namespace RimDigitalLife
{
    public class ThoughtWorker_DigitalComfort : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p.apparel == null) return ThoughtState.Inactive;

            foreach (var apparel in p.apparel.WornApparel)
            {
                if (apparel.def.apparel.tags != null &&
                    apparel.def.apparel.tags.Contains("DigitalStorage_TerminalAccess"))
                {
                    return ThoughtState.ActiveAtStage(0);
                }
            }
            return ThoughtState.Inactive;
        }
    }
}