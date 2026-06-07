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
    private static readonly Dictionary<string, IntPtr> _currentARAFilters = new();

    public static bool TryGetCurrentZoneData(LG_Zone zone, [MaybeNullWhen(false)] out ZoneCustomData zoneData)
    { 
        var matchingData = Current.Zones.Where(zData => zData != null && zData.IntTuple == zone.ToIntTuple()).ToList();
        zoneData = matchingData.Count switch
        {
            0 => null,
            1 => matchingData[0],
            _ => new ZoneCustomData
            {
                HibernateSpawnAligns = matchingData.SelectMany(zData => zData.HibernateSpawnAligns).ToArray(),
                EnemySpawnPoints = matchingData.SelectMany(zData => zData.EnemySpawnPoints).ToArray(),
                BioscanSpawnPoints = matchingData.SelectMany(zData => zData.BioscanSpawnPoints).ToArray(),
                ForceGeneratorClusterMarkers = matchingData.Any(zData => zData.ForceGeneratorClusterMarkers),
                WorldEventObjects = matchingData.SelectMany(zData => zData.WorldEventObjects).ToArray()
                
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
            string content = File.ReadAllText(customFile);
            ReadFileContent(customFile, content);
        }

        var listener = LiveEdit.CreateListener(ModulePath, "*.json", true);
        listener.FileCreated += FileCreated;
        listener.FileChanged += FileChanged;
        listener.FileDeleted += FileDeleted;
    }

    private static uint ReadFileContent(string file, string content)
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

    private void FileCreated(LiveEditEventArgs e)
    {
        ARALogger.Warn($"LiveEdit file created: {e.FullPath}");
        LiveEdit.TryReadFileContent(e.FullPath, (content) =>
        {
            ReadFileContent(e.FullPath, content);
        });
    }

    private void FileChanged(LiveEditEventArgs e)
    {
        ARALogger.Warn($"LiveEdit file changed: {e.FullPath}");
        LiveEdit.TryReadFileContent(e.FullPath, (content) =>
        {
            uint changedLayoutID = ReadFileContent(e.FullPath, content);
            if (Current == LayoutConfigDefinition.Empty || Current.MainLevelLayout != changedLayoutID || GameStateManager.CurrentStateName != eGameStateName.InLevel)
                return;

            foreach (var weData in _customLayoutData[changedLayoutID].Zones.SelectMany(zData => zData.WorldEventObjects))
            {
                if (!_currentARAFilters.TryGetValue(weData.WorldEventObjectFilter, out var ptr) || weData.UseExistingFilterInArea || weData.UseRandomPosition)
                    continue;

                LG_WorldEventObject weObj = new(ptr);
                weObj.transform.position = weData.Position;
                weObj.transform.rotation = Quaternion.Euler(weData.Rotation);
                weObj.transform.localScale = weData.Scale;

                if (!weObj.gameObject.TryAndGetComponent<Collider>(out var collider))
                    continue;

                foreach (var weComp in weData.Components.Values)
                {
                    switch (weComp.ColliderType)
                    {
                        case ColliderType.Box:
                            var box = collider.Cast<BoxCollider>();
                            box.center = weComp.Center;
                            box.size = weComp.Size;
                            break;

                        case ColliderType.Sphere:
                            var sphere = collider.Cast<SphereCollider>();
                            sphere.center = weComp.Center;
                            sphere.radius = weComp.Radius;
                            break;

                        case ColliderType.Capsule:
                            var capsule = collider.Cast<CapsuleCollider>();
                            capsule.center = weComp.Center;
                            capsule.radius = weComp.Radius;
                            capsule.height = weComp.Height;
                            break;
                    }
                }
            }
        });
    }
    
    private void FileDeleted(LiveEditEventArgs e)
    {
        ARALogger.Warn($"LiveEdit file deleted: {e.FullPath}");
        LiveEdit.TryReadFileContent(e.FullPath, (content) =>
        {
            foreach (uint id in _filepathLayoutMap[e.FullPath])
            {
                _customLayoutData.Remove(id);
            }
            _filepathLayoutMap.Remove(e.FullPath);
        });
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
        if (batch != LG_Factory.BatchName.CustomObjectCollection) return;
        
        Dictionary<int, List<LG_WorldEventObject>> preAllocWE = new(); // map existing WE objects
        foreach (var weObj in UnityEngine.Object.FindObjectsOfType<LG_WorldEventObject>())
        {
            var area = weObj.ParentArea ?? CourseNodeUtil.GetCourseNode(weObj.transform.position)?.m_area;
            if (area == null) continue;
            preAllocWE.GetOrAddNew(area.GetInstanceID()).Add(weObj);
        }

        ARALogger.Debug("Applying layout data");
        foreach (var zone in Builder.CurrentFloor.allZones)
        {
            ApplyLayoutData(zone, preAllocWE);
        }
    }

    public override void OnEnterLevel() // fix cargo with dimension level layouts
    {
        foreach (var cage in UnityEngine.Object.FindObjectsOfType<ElevatorCargoCage>())
        {
            if (cage == null) return;
            foreach (var cargo in cage.GetComponentsInChildren<ItemCuller>())
            {
                cargo.MoveToNode(Builder.GetElevatorArea().m_courseNode.m_cullNode, cage.transform.position);
            }
        }
    }

    private static void ApplyLayoutData(LG_Zone zone, Dictionary<int, List<LG_WorldEventObject>> preAllocWE)
    {
        /* Setup all WE Terminals */
        if (Current.AllWorldEventTerminals)
        {
            AddWorldEventObjectToTerminals(zone);
        }

        if (!TryGetCurrentZoneData(zone, out var zoneData) || zoneData?.Zone == null) return;

        /* Add Spawnpoints to Zone Areas */
        zoneData.AddSpawnPoints();

        /* Add Custom WE Objects */
        foreach (var weData in zoneData.WorldEventObjects)
        {
            if (!weData.IsAreaIndexValid(zone, out var area)) continue;
            if (!weData.UseExistingFilterInArea || !weData.TryGetExistingFilterInArea(preAllocWE, area, out var weObj))
            {
                weObj = area.AddChildGameObject<LG_WorldEventObject>(weData.WorldEventObjectFilter);
                if (weData.UseRandomPosition) weData.Position = area.m_courseNode.GetRandomPositionInside();
                weObj.transform.SetPositionAndRotation(weData.Position, Quaternion.Euler(weData.Rotation));
                weObj.transform.localScale = weData.Scale;
                weObj.WorldEventComponents = Array.Empty<IWorldEventComponent>();                
                if (!_currentARAFilters.TryAdd(weData.WorldEventObjectFilter, weObj.Pointer))
                {
                    ARALogger.Debug($"Duplicates of WorldEventObjectFilter \"{weData.WorldEventObjectFilter}\" will be illegible for LiveEdit mid-level");
                }
            }

            /* Setup Custom WE Components */
            foreach ((var type, var weComp) in weData.Components)
            {
                /* Add Collider if has Trigger Component */
                if (type >= WorldEventComponent.WE_CollisionTrigger && type <= WorldEventComponent.WE_InteractTrigger)
                {
                    if (weComp.ColliderType == ColliderType.Box)
                    {
                        var collider = weObj.gameObject.AddComponent<BoxCollider>();
                        collider.gameObject.layer = 14;
                        collider.center = weComp.Center;
                        collider.size = weComp.Size;
                    }
                    else if (weComp.ColliderType == ColliderType.Sphere)
                    {
                        var collider = weObj.gameObject.AddComponent<SphereCollider>();
                        collider.gameObject.layer = 14;
                        collider.center = weComp.Center;
                        collider.radius = weComp.Radius;
                    }
                    else if (weComp.ColliderType == ColliderType.Capsule)
                    {
                        var collider = weObj.gameObject.AddComponent<CapsuleCollider>();
                        collider.gameObject.layer = 14;
                        collider.center = weComp.Center;
                        collider.radius = weComp.Radius;
                        collider.height = weComp.Height;
                    }
                }

                switch (type)
                {
                    case WorldEventComponent.WE_SpecificTerminal when weComp.PrefabOverride != TerminalPrefab.None:
                    case WorldEventComponent.WE_SpecificPickup:
                        _positionToContainerMap[weData.Position] = new(weData.WorldEventObjectFilter, area.m_courseNode, weComp.PrefabOverride, weComp.EventsOnPickup);
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
                        interactTrigger.m_carryAlign ??= new() 
                        {
                            position = weComp.CarryItemTransform.Position, 
                            rotation = Quaternion.Euler(weComp.CarryItemTransform.Rotation), 
                            localScale = weComp.CarryItemTransform.Scale 
                        };
                        interactTrigger.m_removeItemOnInsert = weComp.RemoveItemOnInsert;
                        interactTrigger.m_itemStateAfterInsert = weComp.ItemStateAfterInsert;
                        break;

                    case WorldEventComponent.WE_AnimationTrigger:
                        if (weComp.WorldEventAnimationFilter.IsNullOrWhiteSpace()) continue;
                        var weAnimObj = area.AddChildGameObject<LG_WorldEventObject>(weComp.WorldEventAnimationFilter);
                        weAnimObj.transform.position = new(weData.Position.x, weData.Position.y + 1f, weData.Position.z);
                        weAnimObj.WorldEventComponents = Array.Empty<IWorldEventComponent>();
                        var weAnim = weAnimObj.gameObject.AddComponent<LG_WorldEventAnimationTrigger>();
                        weAnim.m_playResetOnSetup = weComp.PlayResetOnStartup;
                        LG_WorldEventAnimationTrigger.GameObjectActivationPair[] onTrigger = new[]
                        {
                            new LG_WorldEventAnimationTrigger.GameObjectActivationPair
                            {
                                GameObjectToSet = weObj.gameObject,
                                ActivationMode = weComp.ActivationMode
                            }
                        };
                        LG_WorldEventAnimationTrigger.GameObjectActivationPair[] onReset = new[]
                        {
                            new LG_WorldEventAnimationTrigger.GameObjectActivationPair
                            {
                                GameObjectToSet = weObj.gameObject,
                                ActivationMode = !weComp.ActivationMode
                            }
                        };
                        weAnim.m_gameObjectsToActivateOnTrigger = onTrigger;
                        weAnim.m_gameObjectsToActivateOnReset = onReset;
                        break;
                }
            }
        }
    }

    private static void AddWorldEventObjectToTerminals(LG_Zone zone)
    {
        for (int i = 0; i < zone.TerminalsSpawnedInZone.Count; i++)
        {
            var term = zone.TerminalsSpawnedInZone[i];
            var parentMarker = term.GetComponentInParent<LG_MarkerProducer>();
            if (parentMarker == null) continue;
            string name = $"WE_ARA_Term_{(int)zone.DimensionIndex}_{(int)zone.Layer.m_type}_{(int)zone.LocalIndex}_{i}";
            var weTerm = parentMarker.AddChildGameObject<LG_WorldEventObject>(name);
            weTerm.transform.localPosition = Vector3.zero;
            weTerm.WorldEventComponents = Array.Empty<IWorldEventComponent>();
        }

        if (zone.gameObject.TryAndGetComponent<LG_WardenObjective_Reactor>(out var reactor))
        {
            string name = $"WE_ARA_Term_{(int)zone.DimensionIndex}_{(int)zone.Layer.m_type}_{(int)zone.LocalIndex}_Reactor";
            var weTerm = reactor.m_terminalAlign?.AddChildGameObject<LG_WorldEventObject>(name);
            if (weTerm == null) return;
            weTerm.transform.localPosition = Vector3.zero;
            weTerm.WorldEventComponents = Array.Empty<IWorldEventComponent>();
        }
    }
}
