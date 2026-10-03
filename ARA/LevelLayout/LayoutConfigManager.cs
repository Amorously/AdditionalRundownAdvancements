using AmorLib.Utils;
using AmorLib.Utils.Extensions;
using ARA.LevelLayout.DefinitionData;
using BepInEx;
using FluffyUnderware.DevTools.Extensions;
using GTFO.API.Utilities;
using LevelGeneration;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace ARA.LevelLayout;

public sealed class LayoutConfigManager : CustomConfigBase
{
    public static LayoutConfigDefinition Current { get; private set; } = LayoutConfigDefinition.Empty;
    private static readonly Dictionary<string, HashSet<uint>> _filepathLayoutMap = new();
    private static readonly Dictionary<uint, LayoutConfigDefinition> _customLayoutData = new();
    private static readonly Dictionary<Vector3, SpecificDataContainer> _positionToContainerMap = new();
    private static readonly Dictionary<string, List<GameObject>> _currentARAFilters = new();

    public static bool TryGetCurrentZoneData(LG_Zone zone, [MaybeNullWhen(false)] out ZoneCustomData zoneData)
    {
        var matchingData = Current.Zones.Where(zData => zData != null && zData.IntTuple == zone.ToIntTuple()).ToList();
        zoneData = matchingData.Count switch
        {
            0 => null,
            1 => matchingData[0],
            _ => new ZoneCustomData
            {
                Dimension = zone.Dimension,
                Zone = zone,
                DimensionIndex = zone.DimensionIndex,
                Layer = zone.Layer.m_type,
                LocalIndex = zone.LocalIndex,
                HibernateSpawnAligns = matchingData.SelectMany(zData => zData.HibernateSpawnAligns).ToArray(),
                EnemySpawnPoints = matchingData.SelectMany(zData => zData.EnemySpawnPoints).ToArray(),
                BioscanSpawnPoints = matchingData.SelectMany(zData => zData.BioscanSpawnPoints).ToArray(),
                InvisibleWalls = matchingData.SelectMany(zData => zData.InvisibleWalls).ToArray(),
                ForceGeneratorClusterMarkers = matchingData.Any(zData => zData.ForceGeneratorClusterMarkers),
                AllWorldEventLights = matchingData.Any(zData => zData.AllWorldEventLights),
                WorldEventObjects = matchingData.SelectMany(zData => zData.WorldEventObjects).ToArray(),
                StaticEventsOnTrigger = matchingData.SelectMany(zData => zData.StaticEventsOnTrigger).ToList(),
                StaticWorldEventChainedPuzzleDatas = matchingData.SelectMany(zData => zData.StaticWorldEventChainedPuzzleDatas).ToList(),
                StaticSpecificPickupSpawnDatas = matchingData.SelectMany(zData => zData.StaticSpecificPickupSpawnDatas).ToList(),
                StaticSpecificTerminalSpawnDatas = matchingData.SelectMany(zData => zData.StaticSpecificTerminalSpawnDatas).ToList()
            }
        };
        return zoneData != null;
    }

    public static bool TryGetSpecificDataContainer(Vector3 position, [MaybeNullWhen(false)] out SpecificDataContainer container)
    {
        foreach (var kvp in _positionToContainerMap)
        {
            if (kvp.Key.Approximately(position))
            {
                container = kvp.Value;
                return true;
            }
        }

        //ARALogger.Error($"No SpecificDataContainer found at world position {position.ToDetailedString()}!");
        container = null;
        return false;
    }

    internal static void AddARAFilter(string filter, GameObject go)
    {
        _currentARAFilters.GetOrAddNew(filter).Add(go);
    }

    public override string ModulePath => Module + "/LevelLayout";

    public override void Setup()
    {
        Directory.CreateDirectory(ModulePath);
        string templatePath = Path.Combine(ModulePath, "Template.json");
        var templateData = new LayoutConfigDefinition()
        {
            Zones = new ZoneCustomData[] { new() }
        };
        File.WriteAllText(templatePath, ARAJson.Serialize(templateData, typeof(LayoutConfigDefinition)));

        foreach (string customFile in Directory.EnumerateFiles(ModulePath, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                string content = File.ReadAllText(customFile);
                ReadFileContent(customFile, content);
            }
            catch (Exception ex)
            {
                ARALogger.Error($"Error reading file {customFile}:\n{ex.Message}");
            }
        }

        var sfsw = SafeFileSystemWatcher.Create(ModulePath, new[] { "*.json" }, true);
        sfsw.OnCreated += FileCreated;
        sfsw.OnChanged += FileChanged;
        sfsw.OnDeleted += FileDeleted;
    }

    private static uint ReadFileContent(string file, string content) // returns MainLayoutID
    {
        var layoutSet = _filepathLayoutMap.GetOrAddNew(file);
        foreach (uint id in layoutSet)
        {
            _customLayoutData.Remove(id);
        }
        layoutSet.Clear();

        var data = ARAJson.Deserialize<LayoutConfigDefinition>(content);
        if (data != null && data.MainLevelLayout != 0u)
        {
            layoutSet.Add(data.MainLevelLayout);
            _customLayoutData[data.MainLevelLayout] = data;
        }
        return data?.MainLevelLayout ?? 0u;
    }

    private void FileCreated(FileEventArgs e)
    {
        ARALogger.Warn($"LiveEdit file created: {e.FullPath}");
        ReadFileContent(e.FullPath, e.ReadContent());
    }

    private void FileChanged(FileEventArgs e)
    {
        ARALogger.Warn($"LiveEdit file changed: {e.FullPath}");
        uint changedLayoutID = ReadFileContent(e.FullPath, e.ReadContent());
        if (Current == LayoutConfigDefinition.Empty || Current.MainLevelLayout != changedLayoutID || GameStateManager.CurrentStateName != eGameStateName.InLevel)
            return; // early exit if not in current level

        foreach (var weData in _customLayoutData[changedLayoutID].Zones.SelectMany(zData => zData.WorldEventObjects))
        {
            if (!TryGetAndRepositionObject(weData.WorldEventObjectFilter, weData.Transform, out var weObj, 
                    weData.InstanceIndex, !weData.UseExistingFilterInArea && !weData.UseRandomPosition, go => go.HasComponent<LG_WorldEventObject>()))
                continue;

            foreach (var weComp in weData.Components.Values)
            {
                switch (weComp.ColliderType)
                {
                    case ColliderType.Box when weObj.TryAndGetComponent<BoxCollider>(out var box):
                        box.center = weComp.Center;
                        box.size = weComp.Size;
                        break;

                    case ColliderType.Sphere when weObj.TryAndGetComponent<SphereCollider>(out var sphere):
                        sphere.center = weComp.Center;
                        sphere.radius = weComp.Radius;
                        break;

                    case ColliderType.Capsule when weObj.TryAndGetComponent<CapsuleCollider>(out var capsule):
                        capsule.center = weComp.Center;
                        capsule.radius = weComp.Radius;
                        capsule.height = weComp.Height;
                        break;
                }
            }
        }

        foreach (var zone in Builder.CurrentFloor.allZones)
        {
            if (!TryGetCurrentZoneData(zone, out var zData)) 
                continue;

            for (int i = 0; i < zData.InvisibleWalls.Length; i++)
            {
                var wall = zData.InvisibleWalls[i];
                if (wall == null) continue;
                TryGetAndRepositionObject(zData.GetSourceFilter("InvisWall", i), wall, out _);
            }
        }
    }

    private static bool TryGetAndRepositionObject(string filter, CustomTransform transform, [MaybeNullWhen(false)] out GameObject go, 
        int instanceIndex = 0, bool preCondition = true, Func<GameObject, bool>? postConditon = null) 
    {
        go = null;
        if (!preCondition || !_currentARAFilters.TryGetValue(filter, out var list) || list.Count == 0)
            return false;

        int idx = instanceIndex >= 0 && instanceIndex < list.Count ? instanceIndex : 0;
        var target = list[idx];
        if (target == null || (postConditon != null && !postConditon(target)))
            return false;

        target.transform.SetPositionRotationScale(transform.Position, transform.Rotation, transform.Scale);
        go = target;
        return true;
    }

    private void FileDeleted(FileEventArgs e)
    {
        ARALogger.Warn($"LiveEdit file deleted: {e.FullPath}");
        foreach (uint id in _filepathLayoutMap[e.FullPath])
        {
            _customLayoutData.Remove(id);
        }
        _filepathLayoutMap.Remove(e.FullPath);
    }

    public override void OnBuildStart()
    {
        var layout = RundownManager.ActiveExpedition.LevelLayoutData;
        Current = _customLayoutData.TryGetValue(layout, out var config) ? config : LayoutConfigDefinition.Empty;
        _positionToContainerMap.Clear();
        _currentARAFilters.Clear();
    }

    public override void OnBeforeBatchBuild(LG_Factory.BatchName batch)
    {
        switch (batch)
        {
            case LG_Factory.BatchName.FunctionMarkerFallback:
                ARALogger.Debug("Adding spawnpoints and invisible walls");
                foreach (var zone in Builder.CurrentFloor.allZones)
                {
                    if (!TryGetCurrentZoneData(zone, out var zoneData)) continue;
                    zoneData.AddSpawnPointsAndInvisibleWalls();
                }
                break;

            case LG_Factory.BatchName.CustomObjectCollection:
                ARALogger.Debug("Adding world event objects");
                WE_ObjectCustomData.AllocatePreexistingWorldEventObjects();
                foreach (var zone in Builder.CurrentFloor.allZones)
                {
                    AddWorldEventObjectsToTerminals(zone);
                    ApplyLayoutZoneData(zone);
                }
                SetupAnimationTriggers();
                break;
        }        
    }

    public override void OnEnterLevel() // fix cargo with dimension level layouts
    {
        var elevatorArea = Builder.GetElevatorArea();
        foreach (var cage in UnityEngine.Object.FindObjectsOfType<ElevatorCargoCage>())
        {
            if (cage == null) return;
            foreach (var cargo in cage.GetComponentsInChildren<ItemCuller>())
            {
                cargo.MoveToNode(elevatorArea.m_courseNode.m_cullNode, cage.transform.position);
            }
        }
    }

    private static void ApplyLayoutZoneData(LG_Zone zone)
    {
        if (!TryGetCurrentZoneData(zone, out var zoneData) || zoneData?.Zone == null) 
            return;

        /* Add Auto & Static WE Objects */
        AddWorldEventObjectsToLights(zoneData);
        zoneData.InjectStaticDimensionWorldEventData();

        /* Add Custom WE Objects */
        foreach (var weData in zoneData.WorldEventObjects)
        {
            if (weData.ShouldCreateNewWorldEventObject(zone, out var weObj))
            {
                weObj = weData.Area.AddChildGameObject<LG_WorldEventObject>(weData.WorldEventObjectFilter);
                weObj.transform.SetPositionRotationScale(weData.Position, weData.Rotation, weData.Scale);
                weObj.WorldEventComponents = Array.Empty<IWorldEventComponent>();
                AddARAFilter(weData.WorldEventObjectFilter, weObj.gameObject);
            }
            if (weObj == null) continue;

            /* Setup Custom WE Components */
            foreach ((var type, var weComp) in weData.Components)
            {
                /* Add Collider if has Trigger Component */
                if (type >= WorldEventComponent.WE_CollisionTrigger && type <= WorldEventComponent.WE_InteractTrigger)
                {
                    switch (weComp.ColliderType)
                    {
                        case ColliderType.Box:
                            var box = weObj.gameObject.AddComponent<BoxCollider>();
                            box.gameObject.layer = 14;
                            box.center = weComp.Center;
                            box.size = weComp.Size;
                            break;

                        case ColliderType.Sphere:
                            var collider = weObj.gameObject.AddComponent<SphereCollider>();
                            collider.gameObject.layer = 14;
                            collider.center = weComp.Center;
                            collider.radius = weComp.Radius;
                            break;

                        case ColliderType.Capsule:
                            var capsule = weObj.gameObject.AddComponent<CapsuleCollider>();
                            capsule.gameObject.layer = 14;
                            capsule.center = weComp.Center;
                            capsule.radius = weComp.Radius;
                            capsule.height = weComp.Height;
                            break;
                    }
                }

                /* Setup Component Types */
                switch (type)
                {
                    case WorldEventComponent.WE_SpecificTerminal when weComp.PrefabOverride != TerminalPrefab.None:
                    case WorldEventComponent.WE_SpecificPickup:
                        _positionToContainerMap[weData.Position] = new(weData.WorldEventObjectFilter, weData.Area.m_courseNode, weComp.PrefabOverride, weComp.EventsOnPickup);
                        break;

                    case WorldEventComponent.WE_ChainedPuzzle:
                        weObj.gameObject.AddOrGetComponent<LG_WorldEventChainPuzzle>();
                        break;

                    case WorldEventComponent.WE_NavMarker:
                        var weNav = weObj.gameObject.AddOrGetComponent<PlaceNavMarkerOnGO>();
                        weNav.type = weComp.NavMarkerType;
                        weNav.m_placeOnStart = weComp.PlaceOnStart;
                        weObj.gameObject.AddOrGetComponent<LG_WorldEventNavMarker>();
                        break;

                    case WorldEventComponent.WE_CollisionTrigger:
                        var collisionTrigger = weObj.gameObject.AddOrGetComponent<LG_CollisionWorldEventTrigger>();
                        collisionTrigger.m_isToggle = weComp.IsToggle;
                        break;

                    case WorldEventComponent.WE_LookatTrigger:
                        var lookAtTrigger = weObj.gameObject.AddOrGetComponent<LG_LookatWorldEventTrigger>();
                        lookAtTrigger.m_lookatMaxDistance = weComp.LookatMaxDistance;
                        lookAtTrigger.m_isToggle = weComp.IsToggle;
                        break;

                    case WorldEventComponent.WE_InteractTrigger:
                        var interactTrigger = weObj.gameObject.AddOrGetComponent<LG_InteractWorldEventTrigger>();
                        interactTrigger.m_colliderToOwn ??= weObj.gameObject.GetComponent<Collider>();
                        interactTrigger.m_interactionText = weComp.InteractionText;
                        interactTrigger.m_isToggle = weComp.IsToggle;
                        interactTrigger.m_insertType = weComp.CarryItemInsertType;
                        if (interactTrigger.m_carryAlign == null)
                        {
                            var carryTransform = weComp.CarryItemTransform ?? new();
                            var carryAlignGO = new GameObject("CarryAlign");
                            carryAlignGO.transform.SetPositionRotationScale(carryTransform.Position, carryTransform.Rotation, carryTransform.Scale);
                            carryAlignGO.transform.SetParent(weObj.transform, true);
                            interactTrigger.m_carryAlign = carryAlignGO.transform;
                        }
                        interactTrigger.m_removeItemOnInsert = weComp.RemoveItemOnInsert;
                        interactTrigger.m_itemStateAfterInsert = weComp.ItemStateAfterInsert;
                        break;
                }
            }
        }
    }

    private static void AddWorldEventObjectsToTerminals(LG_Zone zone)
    {
        if (!Current.AllWorldEventTerminals) 
            return;

        string prefix = string.Format(Current.AutoWorldEventObjectPrefix, "Term");
        for (int i = 0; i < zone.TerminalsSpawnedInZone.Count; i++)
        {
            var term = zone.TerminalsSpawnedInZone[i];
            var parentMarker = term.GetComponentInParent<LG_MarkerProducer>();
            if (parentMarker == null) continue;
            string name = $"{prefix}{(int)zone.DimensionIndex}_{(int)zone.Layer.m_type}_{(int)zone.LocalIndex}_{i}";
            var weTerm = parentMarker.AddChildGameObject<LG_WorldEventObject>(name);
            weTerm.transform.localPosition = Vector3.zero;
            weTerm.WorldEventComponents = Array.Empty<IWorldEventComponent>();
        }

        foreach (var area in zone.m_areas)
        {
            if (area.m_geomorph.gameObject.TryAndGetComponent<LG_WardenObjective_Reactor>(out var reactor))
            {
                string name = $"{prefix}{(int)zone.DimensionIndex}_{(int)zone.Layer.m_type}_{(int)zone.LocalIndex}_Reactor";
                var weTerm = reactor.m_terminalAlign?.AddChildGameObject<LG_WorldEventObject>(name);
                if (weTerm == null) continue;
                weTerm.transform.localPosition = Vector3.zero;
                weTerm.WorldEventComponents = Array.Empty<IWorldEventComponent>();
                break;
            }
        }
    }

    private static void AddWorldEventObjectsToLights(ZoneCustomData zoneData)
    {
        var zone = zoneData.Zone;
        if (zone == null || !zoneData.AllWorldEventLights) 
            return;
        
        string prefix = string.Format(Current.AutoWorldEventObjectPrefix, "Light");
        int num = 0;
        for (int idx = 0; idx < zone.m_areas.Count; idx++)
        {
            foreach (var light in zone.m_areas[idx].GetComponentsInChildren<LG_Light>(false))
            {
                string name = $"{prefix}{(int)zone.DimensionIndex}_{(int)zone.Layer.m_type}_{(int)zone.LocalIndex}_{num++}";
                var weLight = light.AddChildGameObject<LG_WorldEventObject>(name);
                weLight.transform.localPosition = Vector3.zero;
                weLight.WorldEventComponents = Array.Empty<IWorldEventComponent>();
            }
        }
    }

    private static void SetupAnimationTriggers() 
    {
        foreach (var weData in Current.Zones.SelectMany(zData => zData.WorldEventObjects))
        {
            if (!_currentARAFilters.TryGetValue(weData.WorldEventObjectFilter, out var list) || !weData.Components.TryGetValue(WorldEventComponent.WE_AnimationTrigger, out var weComp))
                continue;

            List<GameObject> targets = new();
            var weObj = list.First();
            var weAnimObj = weObj;
            if (!weComp.WorldEventAnimationFilter.IsNullOrWhiteSpace())
            {
                var weAnimHolder = weData.Area.AddChildGameObject<LG_WorldEventObject>(weComp.WorldEventAnimationFilter);
                weAnimHolder.transform.position = new(weData.Position.x, weData.Position.y + 1f, weData.Position.z);
                weAnimHolder.WorldEventComponents = Array.Empty<IWorldEventComponent>();
                weAnimObj = weAnimHolder.gameObject;
                targets.Add(weObj);
            }
            foreach (var filter in weComp.ARAObjectsToActivate)
            {
                if (!_currentARAFilters.TryGetValue(filter, out var targetList)) continue;
                targets.AddRange(targetList);
            }
            if (targets.Count == 0) continue;

            var weAnimTrigger = weAnimObj.AddOrGetComponent<LG_WorldEventAnimationTrigger>();
            weAnimTrigger.m_playResetOnSetup = weComp.PlayResetOnStartup;
            weAnimTrigger.m_gameObjectsToActivateOnTrigger = targets.Select(go => new LG_WorldEventAnimationTrigger.GameObjectActivationPair
            {
                GameObjectToSet = go,
                ActivationMode = weComp.ActivationMode
            }).ToArray();
            weAnimTrigger.m_gameObjectsToActivateOnReset = targets.Select(go => new LG_WorldEventAnimationTrigger.GameObjectActivationPair
            {
                GameObjectToSet = go,
                ActivationMode = !weComp.ActivationMode
            }).ToArray();
        }
    }
}
