using AmorLib.Utils;
using AmorLib.Utils.Extensions;
using ARA.LevelLayout.DefinitionData;
using FluffyUnderware.DevTools.Extensions;
using GTFO.API;
using GTFO.API.Utilities;
using LevelGeneration;
using MTFO.API;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace ARA.LevelLayout;

public static class LayoutConfigManager // make a config manager parent class
{
    public static LayoutConfigDefinition Current { get; private set; } = LayoutConfigDefinition.Empty;

    private static readonly Dictionary<string, HashSet<uint>> _filepathLayoutMap = new();
    private static readonly Dictionary<uint, LayoutConfigDefinition> _customLayoutData = new();
    
    public static string ModulePath { get; private set; } = string.Empty;  

    // elevator cargo handled in patch
    // portal chained puzzle handled in patch
    // force gen cluster mark handled in patch

    static LayoutConfigManager() 
    {
        ModulePath = Path.Combine(MTFOPathAPI.CustomPath, EntryPoint.MODNAME);
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

        LevelAPI.OnBuildStart += OnBuildStart;
        LevelAPI.OnBeforeBuildBatch += OnBeforeBuildBatch;        
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

    private static void FileCreatedOrChanged(LiveEditEventArgs e)
    {
        ARALogger.Warn($"LiveEdit file changed: {e.FullPath}");
        LiveEdit.TryReadFileContent(e.FullPath, (content) =>
        {
            ReadFileContent(e.FullPath, content);
        });
    }
    
    private static void FileDeleted(LiveEditEventArgs e)
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

    private static void OnBuildStart()
    {
        var layout = RundownManager.ActiveExpedition.LevelLayoutData;
        Current = _customLayoutData.TryGetValue(layout, out var config) ? config : LayoutConfigDefinition.Empty;
    }

    private static void OnBeforeBuildBatch(LG_Factory.BatchName batch)
    {
        if (batch == LG_Factory.BatchName.CustomObjectCollection)
        {
            ApplyLayoutData();
        }
    }

    public static void ApplyLayoutData()
    {
        foreach (var zone in Builder.CurrentFloor.allZones)
        {
            if (Current.AllWorldEventTerminals)
            {
                for (int i = 0; i < zone.TerminalsSpawnedInZone.Count; i++)
                {
                    var term = zone.TerminalsSpawnedInZone[i];
                    var parentMarker = term.GetComponentInParent<LG_MarkerProducer>();
                    if (parentMarker == null || parentMarker.GetComponentInChildren<LG_WorldEventObject>() != null) continue;
                    string name = $"WE_ARA_Term_{(int)zone.DimensionIndex}_{(int)zone.Layer.m_type}_{(int)zone.LocalIndex}_{i}";
                    var weTerm = parentMarker.AddChildGameObject<LG_WorldEventObject>(name);
                    weTerm.transform.localPosition = Vector3.zero;
                    weTerm.WorldEventComponents = Array.Empty<IWorldEventComponent>();
                }
            }
            
            if (!TryGetCurrentZoneData(zone, out var zoneData) || zoneData?.Zone == null) continue;

            zoneData.AddSpawnPoints();

            foreach (var weData in zoneData.WorldEventObjects)
            {
                if (!weData.IsAreaIndexValid(zone, out var area)) continue;
                var weObj = area.AddChildGameObject<LG_WorldEventObject>(weData.WorldEventObjectFilter);
                weObj.transform.SetPositionAndRotation(!weData.UseRandomPosition ? weData.Position : area.m_courseNode.GetRandomPositionInside(), Quaternion.Euler(weData.Rotation));
                weObj.transform.localScale = weData.Scale;
                weObj.WorldEventComponents = Array.Empty<IWorldEventComponent>();

                foreach (var weComp in weData.Components)
                {
                    switch (weComp.Type)
                    {
                        case WorldEventComponent.WE_Terminal:

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

                        default:
                            break;
                    }
                }
            }
        }
    }

    public static bool TryGetCurrentZoneData(LG_Zone zone, [MaybeNullWhen(false)] out ZoneCustomData zoneData)
    {
        zoneData = Current.Zones.FirstOrDefault(z => zone.ToIntTuple() == z.Zone?.ToIntTuple());
        return zoneData != null;
    }
}
