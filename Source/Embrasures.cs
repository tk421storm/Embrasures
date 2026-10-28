using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace TKS_Embrasures
{
    [StaticConstructorOnStartup]
    public static class InsertHarmony
    {
        static InsertHarmony()
        {
            new Harmony("TKS_Embrasures").PatchAll();
            Log.Message("TKS_Embrasures: Patching finished");
        }
    }

    [DefOf]
    public static class EmbrasureDefOf
    {
        public static ThingDef CCP_Embrasure;
    }

    public static class EmbrasureUtility
    {
        //the line from one cell to another passes over an embrasure (neither end cell counts)
        public static bool CrossesEmbrasure(IntVec3 from, IntVec3 to, Map map)
        {
            return !GenSight.LineOfSight(from, to, map, skipFirstCell: true,
                validator: c => c.GetEdifice(map)?.def != EmbrasureDefOf.CCP_Embrasure);
        }
    }

#if v1_5
    //1.5 keeps the interaction checks in InteractionUtility
    [HarmonyPatch(typeof(InteractionUtility), nameof(InteractionUtility.IsGoodPositionForInteraction),
        new[] { typeof(IntVec3), typeof(IntVec3), typeof(Map) })]
#else
    [HarmonyPatch(typeof(SocialInteractionUtility), nameof(SocialInteractionUtility.IsGoodPositionForInteraction),
        new[] { typeof(IntVec3), typeof(IntVec3), typeof(Map) })]
#endif
    public static class IsGoodPositionForInteraction_Cells_Patch
    {
        //embrasures are seen and shot through (fillPercent 0.95), so vanilla let pawns on opposite sides chat, insult,
        //romance and recruit through them, diagonally across a corner too. Social interactions now need a line of
        //sight that doesn't cross an embrasure
        [HarmonyPostfix]
        public static void Postfix(IntVec3 cell, IntVec3 recipientCell, Map map, ref bool __result)
        {
            if (__result && EmbrasureUtility.CrossesEmbrasure(cell, recipientCell, map))
            {
                __result = false;
            }
        }
    }

#if v1_5
    [HarmonyPatch(typeof(InteractionUtility), nameof(InteractionUtility.IsGoodPositionForInteraction),
        new[] { typeof(Pawn), typeof(Pawn) })]
#else
    [HarmonyPatch(typeof(SocialInteractionUtility), nameof(SocialInteractionUtility.IsGoodPositionForInteraction),
        new[] { typeof(Pawn), typeof(Pawn) })]
#endif
    public static class IsGoodPositionForInteraction_Pawns_Patch
    {
        //same check on the pawn overload, in case the short cell overload is inlined into it
        [HarmonyPostfix]
        public static void Postfix(Pawn p, Pawn recipient, ref bool __result)
        {
            if (__result && EmbrasureUtility.CrossesEmbrasure(p.Position, recipient.Position, p.Map))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(ReachabilityImmediate), nameof(ReachabilityImmediate.CanReachImmediate),
        new[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(Map), typeof(PathEndMode), typeof(Pawn) })]
    [HarmonyAfter("OskarPotocki.VEF")]
    public static class CanReachImmediate_Patch
    {
        //Vanilla Expanded Framework re-allows melee that vanilla's touch rule refused, when the target is within weapon
        //reach (1.42 without a reach weapon) and in line of sight. Embrasures can be seen through, so pawns hit each
        //other diagonally across an embrasure corner (or through one with a reach weapon). Only reaches vanilla refused
        //are undone, and only against pawns, so building and repairing embrasures from a corner still work
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref bool __result, IntVec3 start, LocalTargetInfo target, Map map, Pawn pawn)
        {
            if (!__result || map == null || !(target.Thing is Pawn) || start == target.Cell)
            {
                return;
            }
            if (TouchPathEndModeUtility.IsAdjacentOrInsideAndAllowedToTouch(start, target, map.pathing.For(pawn)))
            {
                return;
            }
            if (EmbrasureUtility.CrossesEmbrasure(start, target.Cell, map))
            {
                __result = false;
            }
        }
    }
}
