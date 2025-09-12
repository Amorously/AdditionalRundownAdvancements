using ARA.LevelLayout;
using BepInEx;
using BepInEx.Unity.IL2CPP.Hook;
using GameData;
using GTFO.API;
using GTFO.API.Extensions;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;
using Il2CppSystem.Reflection;
using LevelGeneration;
using UnityEngine;

namespace ARA.Patches;

internal static class SpecificTerminalNativePatch
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

    internal unsafe static void ApplyNativePatch()
    {
        var type = typeof(WorldEventUtils);
        var classPtr = IL2CPP.GetIl2CppClass(type.Module.Name, string.Empty, type.Name);

        ForceSpawnMarkerDetour = CreateGenericStaticDetour<d_ForceSpawnMarker>
        (
            classPtr,
            nameof(WorldEventUtils.TryForceSpawnMarkerResult),
            "System.bool",
            new string[] 
            {
                nameof(Vector3),
                nameof(Quaternion),
                nameof(Transform),
                "System.uint",
                "System.int",
                nameof(ExpeditionFunction),
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

    private static unsafe INativeDetour CreateGenericStaticDetour<TDelegate>(IntPtr classPtr, string methodName, string returnType, string[] paramTypes, Type[] genericArguments, TDelegate to, out TDelegate original)
            where TDelegate : Delegate
    {
        if (classPtr == IntPtr.Zero)
        {
            ARALogger.Error($"Failed to get class pointer for {nameof(WorldEventUtils)}?");
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

        if (!LayoutConfigManager.TryGetTerminalPrefabContainer(pos, out var container))
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
            terminal = InstantiatePrefabSpawners(go, pos, rot, transform);
            if (terminal.GetComponentInChildren<LG_ComputerTerminal>() == null && !extraPrefab.IsNullOrWhiteSpace())
            {
                bool isSpawned = false;
                foreach (var marker in terminal.GetComponentsInChildren<LG_MarkerProducer>(false))
                {
                    if (!isSpawned && HasTerminalMarker(marker.MarkerDataBlockType, marker.MarkerDataBlockID))
                    {
                        var go2 = AssetAPI.GetLoadedAsset<GameObject>(extraPrefab);
                        terminal = InstantiatePrefabSpawners(go2, marker.transform.position, marker.transform.rotation, marker.transform.parent);
                        isSpawned = true;
                    }
                    marker.gameObject.SetActive(false);
                }
            }
        }
        catch (Exception ex)
        {
            ARALogger.Error($"Unable to load custom terminal prefab(s), is the correct complex loaded?\n{ex}");
            return false;
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

    private static GameObject InstantiatePrefabSpawners(GameObject original, Vector3 position, Quaternion rotation, Transform parent)
    {
        var clone = UnityEngine.Object.Instantiate(original, position, rotation, parent);

        foreach (var spawner in clone.GetComponentsInChildren<LG_PrefabSpawner>())
        {
            try
            {
                GameObject prefab = UnityEngine.Object.Instantiate(spawner.m_prefab, spawner.transform.position, spawner.transform.rotation, spawner.transform.parent);
                if (spawner.m_disableCollision)
                {
                    foreach (Collider collider in prefab.GetComponentsInChildren<Collider>())
                    { 
                        collider.enabled = false; 
                    }
                }
                if (spawner.m_applyScale)
                {
                    prefab.transform.localScale = spawner.transform.localScale;
                }
                prefab.transform.SetParent(spawner.transform);
            }
            catch
            {
                continue;
            }
        }

        return clone;
    }
}
