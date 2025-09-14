using ARA.LevelLayout;
using ARA.LevelLayout.DefinitionData;
using GameData;
using HarmonyLib;
using LevelGeneration;
using Player;

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
        foreach (var itemID in __state.CargoItems)
        {
            var block = ItemDataBlock.GetBlock(itemID);
            if (block == null || !block.internalEnabled)
            {
                ARALogger.Error($"Failed to find enabled ItemDataBlock {itemID}!");
                continue;
            }
            var customItem = LG_PickupItem.SpawnGenericPickupItem(ElevatorShaftLanding.CargoAlign);
            customItem.SpawnNode = Builder.GetElevatorArea().m_courseNode;

            int seed = UnityEngine.Random.Range(0, int.MaxValue);
            switch (block.inventorySlot)
            {
                case InventorySlot.Consumable:
                    customItem.SetupAsConsumable(seed, itemID);
                    break;

                case InventorySlot.ConsumableHeavy:
                case InventorySlot.InLevelCarry:
                    customItem.SetupAsBigPickupItem(seed, itemID, false, 0);
                    break;

                case InventorySlot.InPocket:
                case InventorySlot.Pickup:
                    customItem.SetupAsSmallGenericPickup(seed, itemID, false);
                    break;

                default:
                    ARALogger.Warn($"Unknown item type {block.inventorySlot} for {block.name} ({itemID}), attempting spawning as big pickup");
                    customItem.SetupAsBigPickupItem(seed, itemID, false, 0);
                    break;
            }

            __instance.m_itemsToMoveToCargo.Add(customItem.transform);
        }

        ElevatorRide.Current.m_cargoCageInUse = true;
    }
}
