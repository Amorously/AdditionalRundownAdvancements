using AmorLib.Utils;
using AmorLib.Utils.Extensions;
using ARA.LevelLayout.DefinitionData;
using FluffyUnderware.DevTools.Extensions;
using GTFO.API.Utilities;
using LevelGeneration;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace ARA.LevelLayout;

public class LayoutConfigManager : CustomConfigBase
{
    public static LayoutConfigDefinition Current { get; private set; } = LayoutConfigDefinition.Empty;
    private static readonly Dictionary<string, HashSet<uint>> _filepathLayoutMap = new();
    private static readonly Dictionary<uint, LayoutConfigDefinition> _customLayoutData = new();
    private static readonly Dictionary<Vector3, TerminalCustomPrefabContainer> _positionTerminalContainerMap = new();

    public static bool TryGetCurrentZoneData(LG_Zone zone, [MaybeNullWhen(false)] out ZoneCustomData zoneData)
    {
        zoneData = Current.Zones.FirstOrDefault(z => z != null && z.IntTuple == zone.ToIntTuple(), null);
        return zoneData != null;
    }

    public static bool TryGetTerminalPrefabContainer(Vector3 position, [MaybeNullWhen(false)] out TerminalCustomPrefabContainer container)
    {
        foreach (var kvp in _positionTerminalContainerMap)
        {
            if (kvp.Key.Approximately(position))
            {
                container = kvp.Value;
                return true;
            }
        }

        container = null;
        return false;
    }

    public override string ModulePath => Module + "/LevelLayout";

    public override void Setup() 
    {
        Directory.CreateDirectory(ModulePath);

        //if (Configuration.CreateTemplate)
        //{
            string templatePath = Path.Combine(ModulePath, "Template.json");
            var templateData = new LayoutConfigDefinition()
            {
                Zones = new ZoneCustomData[] { new() }
            };
            File.WriteAllText(templatePath, ARAJson.Serialize(templateData, typeof(LayoutConfigDefinition)));
        //}

        foreach (string customFile in Directory.EnumerateFiles(ModulePath, "*.json", SearchOption.AllDirectories))
        {
            string content = File.ReadAllText(customFile);
            ReadFileContent(customFile, content);
        }

        var listener = LiveEdit.CreateListener(ModulePath, "*.json", true);
        listener.FileCreated += FileCreatedOrChanged;
        listener.FileChanged += FileCreatedOrChanged;
        listener.FileDeleted += FileDeleted;
    }

    private static void ReadFileContent(string file, string content)
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
    }

    private void FileCreatedOrChanged(LiveEditEventArgs e)
    {
        ARALogger.Warn($"LiveEdit file changed: {e.FullPath}");
        LiveEdit.TryReadFileContent(e.FullPath, (content) =>
        {
            ReadFileContent(e.FullPath, content);
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
        _positionTerminalContainerMap.Clear();
    }

    public override void OnBeforeBatchBuild(LG_Factory.BatchName batch)
    {
        if (batch != LG_Factory.BatchName.CustomObjectCollection) return;
        ARALogger.Debug("Applying layout data");
        ApplyLayoutData();
    }

    public override void OnEnterLevel() // fix cargo with dimension level layouts
    {
        var cage = UnityEngine.Object.FindObjectOfType<ElevatorCargoCage>();
        if (cage == null) return;
        foreach (var cargo in cage.GetComponentsInChildren<ItemCuller>())
        {
            cargo.MoveToNode(Builder.GetElevatorArea().m_courseNode.m_cullNode, cage.transform.position);
        }
    }

    private static void ApplyLayoutData()
    {
        foreach (var zone in Builder.CurrentFloor.allZones)
        {
            /* Setup All WE Terminals */
            if (Current.AllWorldEventTerminals) // doesn't get the reactor terminal rn
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
            }
            
            if (!TryGetCurrentZoneData(zone, out var zoneData) || zoneData?.Zone == null) continue;

            /* Add Spawnpoints to Zone Areas */
            zoneData.AddSpawnPoints();

            /* Add Custom WE Objects */
            foreach (var weData in zoneData.WorldEventObjects)
            {
                if (!weData.IsAreaIndexValid(zone, out var area)) continue;
                var weObj = area.AddChildGameObject<LG_WorldEventObject>(weData.WorldEventObjectFilter);
                if (weData.UseRandomPosition) weData.Position = area.m_courseNode.GetRandomPositionInside();
                weObj.transform.SetPositionAndRotation(weData.Position, Quaternion.Euler(weData.Rotation));
                weObj.transform.localScale = weData.Scale;
                weObj.WorldEventComponents = Array.Empty<IWorldEventComponent>(); 

                /* Setup Custom WE Component(s) */
                foreach (var weComp in weData.Components)
                {
                    switch (weComp.Type)
                    {
                        case WorldEventComponent.WE_Terminal when weComp.TerminalPrefabOverride != TerminalPrefab.None:
                            _positionTerminalContainerMap[weData.Position] = new(weData.WorldEventObjectFilter, area.m_courseNode, weComp.TerminalPrefabOverride);
                            break;

                        case WorldEventComponent.WE_ChainedPuzzle:
                            weObj.gameObject.AddComponent<LG_WorldEventChainPuzzle>();
                            break;

                        case WorldEventComponent.WE_NavMarker:
                            var weNav = weObj.gameObject.AddComponent<PlaceNavMarkerOnGO>();
                            weNav.type = weComp.NavMarkerType;
                            weNav.m_placeOnStart = weComp.PlaceOnStart;
                            weObj.gameObject.AddComponent<LG_WorldEventNavMarker>();
                            break;

                        case WorldEventComponent.WE_CollisionTrigger:
                        case WorldEventComponent.WE_LookatTrigger:
                        case WorldEventComponent.WE_InteractTrigger:
                            break;
                    }
                }
            }
        }
    }    
}
