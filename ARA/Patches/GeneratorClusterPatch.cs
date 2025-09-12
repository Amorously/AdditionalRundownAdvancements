using AIGraph;
using ARA.LevelLayout;
using GameData;
using HarmonyLib;
using LevelGeneration;

namespace ARA.Patches;

[HarmonyPatch(typeof(LG_DistributionJobUtils), nameof(LG_DistributionJobUtils.GetRandomNodeFromZoneForFunction))]
internal static class GeneratorClusterPatch // credits: pumba https://discord.com/channels/782438773690597389/783918553626836992/1407177782093414490
{
    [HarmonyPrefix]
    [HarmonyWrapSafe]
    public static bool ForceGeneratorClusterSpawn(LG_Zone zone, ExpeditionFunction func, float randomValue, ref AIG_CourseNode __result)
    {
        if (func != ExpeditionFunction.GeneratorCluster) return true;
        else if (!LayoutConfigManager.TryGetCurrentZoneData(zone, out var custom) || !custom.ForceGeneratorClusterMarkers) return true;

        List<LG_Area> candidates = new();
        foreach (var area in zone.m_areas)
        {
            if (area.GetMarkerSpawnerCount(ExpeditionFunction.GeneratorCluster) > 0)
            {
                candidates.Add(area);
            }
        }
        
        if (candidates.Count == 0) return true;
        var index = Math.Clamp((int)(randomValue * candidates.Count), 0, candidates.Count - 1);
        ARALogger.Debug($"Selected area for forced generator cluster: {candidates[index].m_navInfo.m_suffix}");
        __result = candidates[index].m_courseNode;
        return false;
    }
}
