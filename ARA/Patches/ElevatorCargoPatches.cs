using ARA.LevelLayout;
using ARA.LevelLayout.DefinitionData;
using HarmonyLib;
using LevelGeneration;

namespace ARA.Patches;

[HarmonyPatch(typeof(ElevatorCargoCage), nameof(ElevatorCargoCage.SpawnObjectiveItemsInLandingArea))]
internal static class ElevatorCargoPatches
{
    [HarmonyPrefix]
    [HarmonyWrapSafe]
    private static bool Pre_SpawnItems(out ElevatorCargoCustomData? __state)
    {
        if (LayoutConfigManager.Current == LayoutConfigDefinition.Empty)
        {
            __state = null;
            return true;
        }

        __state = LayoutConfigManager.Current.Elevator;
        if (__state.DisableElevatorCargo)
        {
            ElevatorRide.Current.m_cargoCageInUse = false;
            return false;
        }
        
        return !__state.OverrideCargoItems;        
    }

    [HarmonyPostfix]
    [HarmonyWrapSafe]
    private static void Post_SpawnItems(ElevatorCargoCage __instance, ElevatorCargoCustomData? __state)
    {
        if (__state == null || __state.CargoItems.Length == 0) return;

        __instance.m_itemsToMoveToCargo ??= new();
        foreach (var item in __state.CargoItems)
        {
            var customItem = LG_PickupItem.SpawnGenericPickupItem(ElevatorShaftLanding.CargoAlign);
            customItem.SpawnNode = Builder.GetElevatorArea().m_courseNode;
            customItem.SetupAsBigPickupItem(UnityEngine.Random.Range(0, int.MaxValue), item, false, 0);
            __instance.m_itemsToMoveToCargo.Add(customItem.transform);
        }

        ElevatorRide.Current.m_cargoCageInUse = true;
    }
}
