using AmorLib.Utils;
using AmorLib.Utils.Extensions;
using ARA.LevelLayout.DefinitionData;
using FluffyUnderware.DevTools.Extensions;
using GTFO.API.Utilities;
using LevelGeneration;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace ARA.LevelLayout;

public partial class LayoutConfigManager : CustomConfigBase
{
    public static LayoutConfigDefinition Current { get; private set; } = LayoutConfigDefinition.Empty;
    private static readonly Dictionary<string, HashSet<uint>> _filepathLayoutMap = new();
    private static readonly Dictionary<uint, LayoutConfigDefinition> _customLayoutData = new();
    private static readonly Dictionary<Vector3, SpecificDataContainer> _positionToContainerMap = new();

    public static bool TryGetCurrentZoneData(LG_Zone zone, [MaybeNullWhen(false)] out ZoneCustomData zoneData)
    {
        zoneData = Current.Zones.FirstOrDefault(z => z != null && z.IntTuple == zone.ToIntTuple(), null);
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
        _positionToContainerMap.Clear();
    }

    public override void OnBeforeBatchBuild(LG_Factory.BatchName batch)
    {
        if (batch != LG_Factory.BatchName.CustomObjectCollection) return;
        
        Dictionary<int, List<LG_WorldEventObject>> preAllocWE = new(); // map existing WE objects
        foreach (var weObj in UnityEngine.Object.FindObjectsOfType<LG_WorldEventObject>())
        {
            Vector3 pos = weObj.transform.position;
            eDimensionIndex dim = Dimension.GetDimensionFromPos(pos).DimensionIndex;
            var area = CourseNodeUtil.GetCourseNode(pos, dim).m_area;
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
        var cage = UnityEngine.Object.FindObjectOfType<ElevatorCargoCage>();
        if (cage == null) return;
        foreach (var cargo in cage.GetComponentsInChildren<ItemCuller>())
        {
            cargo.MoveToNode(Builder.GetElevatorArea().m_courseNode.m_cullNode, cage.transform.position);
        }
    }    
}
