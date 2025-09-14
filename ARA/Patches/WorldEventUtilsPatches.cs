using AmorLib.Utils.Extensions;
using ARA.LevelLayout;
using ARA.LevelLayout.DefinitionData;
using BepInEx;
using BepInEx.Unity.IL2CPP.Hook;
using GameData;
using GTFO.API;
using GTFO.API.Extensions;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;
using Il2CppSystem.Reflection;
using LevelGeneration;
using Player;
using UnityEngine;
using XXHashing;

namespace ARA.Patches;

[HarmonyPatch]
internal static class WorldEventUtilsPatches
{
    private static INativeDetour? ForceSpawnMarkerDetour;
    private static d_ForceSpawnMarker? orig_ForceSpawnMarker;
    private unsafe delegate bool d_ForceSpawnMarker
    (
        IntPtr position, 
        IntPtr rotation, 
        IntPtr parent, 
        uint markerBlockID, 
        int index, 
        ExpeditionFunction wantedFunc, 
        out IntPtr spawnedObject, 
        Il2CppMethodInfo* methodInfo
    );

    [HarmonyPrepare]
    private unsafe static void ApplyNativePatch()
    {
        ForceSpawnMarkerDetour ??= CreateGenericStaticDetour<d_ForceSpawnMarker>
        (
            typeof(WorldEventUtils),
            nameof(WorldEventUtils.TryForceSpawnMarkerResult),
            typeof(bool).FullName!,
            new string[]
            {
                typeof(UnityEngine.Vector3).FullName!,                   
                typeof(UnityEngine.Quaternion).FullName!,         
                typeof(UnityEngine.Transform).FullName!, 
                typeof(uint).FullName!,    
                typeof(int).FullName!,   
                typeof(GameData.ExpeditionFunction).FullName!,
                typeof(GameObject).MakeByRefType().FullName!
            },
            new Type[]
            {
                typeof(MiningMarkerDataBlock)
            },
            ForceSpawnMarkerPatch,
            out orig_ForceSpawnMarker
        );
    }

    [HarmonyPatch(typeof(WorldEventUtils), nameof(WorldEventUtils.TrySpawnItemOnAlign))]
    [HarmonyPrefix]
    [HarmonyWrapSafe]
    private static bool Pre_TryForceSpawnItem(Transform itemAlign, out SpecificDataContainer? __state)
    {
        return !LayoutConfigManager.TryGetSpecificDataContainer(itemAlign.position, out __state);
    }

    [HarmonyPatch(typeof(WorldEventUtils), nameof(WorldEventUtils.TrySpawnItemOnAlign))]
    [HarmonyPostfix]
    [HarmonyWrapSafe]
    private static void Post_TryForceSpawnItem(ref bool __result, bool __runOriginal, uint itemID, Transform itemAlign, uint seed, ref ItemInLevel spawnedItem, SpecificDataContainer? __state)
    {
        if (__runOriginal || __state == null) return;

        var block = ItemDataBlock.GetBlock(itemID);
        if (block == null || !block.internalEnabled)
        {
            ARALogger.Error($"Failed to find enabled ItemDataBlock {itemID}!");
            return;
        }
        var item = LG_PickupItem.SpawnGenericPickupItem(itemAlign);
        item.SpawnNode = __state.SpawnNode;

        int hash = (int)XXHash.Hash(seed, 0, false);
        switch (block.inventorySlot)
        {
            case InventorySlot.Consumable:
                item.SetupAsConsumable(hash, itemID);
                break;

            case InventorySlot.ConsumableHeavy:
            case InventorySlot.InLevelCarry:
                item.SetupAsBigPickupItem(hash, itemID, false, 0);
                break;

            case InventorySlot.InPocket:
            case InventorySlot.Pickup:
                item.SetupAsSmallGenericPickup(hash, itemID, false);
                break;

            default:
                ARALogger.Error($"Unknown item type {block.inventorySlot} for ItemDataBlock {itemID}!");
                return;
        }

        spawnedItem = item.GetComponentInChildren<ItemInLevel>();        
        Queue<WardenObjectiveEventData> eData = new(__state.EventsOnPickup);
        spawnedItem?.GetSyncComponent().add_OnSyncStateChange((Action<ePickupItemStatus, pPickupPlacement, PlayerAgent, bool>)((status, placement, _, isRecall) =>
        {
            if ((!placement.hasBeenPickedUp || !placement.linkedToMachine) && isRecall)
            {
                ARALogger.Warn($"isRecall for SpecificDataContainer {__state.WorldEventObjectFilter}; restore EventsOnPickup");
                eData = new(__state.EventsOnPickup);
            }
            if (status == ePickupItemStatus.PickedUp && !isRecall)
            {
                while (eData.Count > 0)
                {
                    WardenObjectiveManager.CheckAndExecuteEventsOnTrigger(eData.Dequeue(), eWardenObjectiveEventTrigger.None, true);
                }
            }
        }));
        __result = spawnedItem != null;        
    }

    private unsafe static INativeDetour CreateGenericStaticDetour<TDelegate>(Type classType, string methodName, string returnType, string[] paramTypes, Type[] genericArguments, TDelegate to, out TDelegate original)
            where TDelegate : Delegate
    {
        var classPtr = IL2CPP.GetIl2CppClass(classType.Module.Name, string.Empty, classType.Name);
        if (classPtr == IntPtr.Zero)
        {
            ARALogger.Error($"Failed to get class pointer for {methodName}?");
            original = null!;
            return null!;
        }
        IntPtr methodPtr = IL2CPP.GetIl2CppMethod(classPtr, true, methodName, returnType, paramTypes);

        MethodInfo methodInfo = new(IL2CPP.il2cpp_method_get_object(methodPtr, classPtr));
        MethodInfo genericMethodInfo = methodInfo.MakeGenericMethod(genericArguments.Select(Il2CppType.From).ToArray());

        INativeMethodInfoStruct il2cppMethodInfo = UnityVersionHandler.Wrap((Il2CppMethodInfo*)IL2CPP.il2cpp_method_get_from_reflection(genericMethodInfo.Pointer));

        return INativeDetour.CreateAndApply(il2cppMethodInfo.MethodPointer, to, out original);
    }

    private unsafe static bool ForceSpawnMarkerPatch(IntPtr position, IntPtr rotation, IntPtr parent, uint markerBlockID, int index, ExpeditionFunction wantedFunc, out IntPtr spawnedObject, Il2CppMethodInfo* methodInfo)
    {
        spawnedObject = IntPtr.Zero;
        Vector3 pos = *(Vector3*)position;
        Quaternion rot = *(Quaternion*)rotation;        

        if (!LayoutConfigManager.TryGetSpecificDataContainer(pos, out var container))
        {
            orig_ForceSpawnMarker!(position, rotation, parent, markerBlockID, index, wantedFunc, out spawnedObject, methodInfo);
            return spawnedObject != IntPtr.Zero;
        }

        GameObject terminal = new();
        Transform transform = new(parent);
        try
        {
            string prefab = container.GetCustomPrefabs(out var extraPrefab);
            var go = AssetAPI.GetLoadedAsset<GameObject>(prefab);
            terminal = go.ClonePrefabSpawners(pos, rot, transform);
            if (terminal.GetComponentInChildren<LG_ComputerTerminal>() == null && !extraPrefab.IsNullOrWhiteSpace())
            {
                bool isSpawned = false;
                foreach (var marker in terminal.GetComponentsInChildren<LG_MarkerProducer>(false))
                {
                    if (!isSpawned && HasTerminalMarker(marker.MarkerDataBlockType, marker.MarkerDataBlockID))
                    {
                        var go2 = AssetAPI.GetLoadedAsset<GameObject>(extraPrefab);
                        terminal = go2.ClonePrefabSpawners(marker.transform.position, marker.transform.rotation, marker.transform.parent);
                        isSpawned = true;
                    }
                    marker.gameObject.SetActive(false);
                }
            }
        }
        catch (Exception ex)
        {
            ARALogger.Error($"Unable to load custom terminal prefab(s), is the correct complex loaded?\n{ex}");
            orig_ForceSpawnMarker!(position, rotation, parent, markerBlockID, index, wantedFunc, out spawnedObject, methodInfo);
            return spawnedObject != IntPtr.Zero;
        }

        terminal.name = container.WorldEventObjectFilter;
        var nodeHandler = terminal.GetComponentInChildren<iLG_SpawnedInNodeHandler>();
        if (nodeHandler == null) return false;
        nodeHandler.SpawnNode = container.SpawnNode;
        spawnedObject = terminal.Pointer;

        return spawnedObject != IntPtr.Zero;
    }

    private static bool HasTerminalMarker(LG_MarkerDataBlockType type, uint id)
    {
        switch (type)
        {
            case LG_MarkerDataBlockType.Mining:
                var mining = MiningMarkerDataBlock.GetBlock(id);
                return mining != null && mining.internalEnabled && mining.GetCompositions().ToManaged().Any(comp => comp.function == ExpeditionFunction.Terminal);

            case LG_MarkerDataBlockType.Service:
                var service = ServiceMarkerDataBlock.GetBlock(id);
                return service != null && service.internalEnabled && service.GetCompositions().ToManaged().Any(comp => comp.function == ExpeditionFunction.Terminal);

            case LG_MarkerDataBlockType.Tech:
                var tech = TechMarkerDataBlock.GetBlock(id);
                return tech != null && tech.internalEnabled && tech.GetCompositions().ToManaged().Any(comp => comp.function == ExpeditionFunction.Terminal);

            default:
                return false;
        }
    }
}
