using AmorLib.Utils;
using AmorLib.Utils.Extensions;
using ARA.Utils;
using GameData;
using GTFO.API.Extensions;
using LevelGeneration;
using System.Text.Json.Serialization;
using UnityEngine;

namespace ARA.LevelLayout.DefinitionData;

public sealed class ZoneCustomData : GlobalBase
{
    public Vector3[] HibernateSpawnAligns { get; set; } = Array.Empty<Vector3>();
    public Vector3[] EnemySpawnPoints { get; set; } = Array.Empty<Vector3>();
    public Vector3[] BioscanSpawnPoints { get; set; } = Array.Empty<Vector3>();
    public FilterTransform[] InvisibleWalls { get; set; } = Array.Empty<FilterTransform>();
    public bool ForceGeneratorClusterMarkers { get; set; } = false;
    [JsonPropertyName("SetupWorldEventObjectOnLightsInArea")]
    public int[] AllWorldEventLights { get; set; } = Array.Empty<int>();
    public WE_ObjectCustomData[] WorldEventObjects { get; set; } = Array.Empty<WE_ObjectCustomData>();
    public List<WorldEventFromSourceData> StaticEventsOnTrigger { get; set; } = new();
    public List<StaticDimensionWECPData> StaticWorldEventChainedPuzzleDatas { get; set; } = new();
    public List<SpecificPickupSpawnData> StaticSpecificPickupSpawnDatas { get; set; } = new();
    public List<StaticDimensionWESTData> StaticSpecificTerminalSpawnDatas { get; set; } = new();

    public void AddSpawnPointsAndInvisibleWalls()
    {
        AddSpawnPoints("HSA", HibernateSpawnAligns, area => area.m_spawnAligns);
        AddSpawnPoints("ESP", EnemySpawnPoints, area => area.m_enemySpawnPoints);
        AddSpawnPoints("SBP", BioscanSpawnPoints, area => area.m_bioscanSpawnPoints);
        AddInvisibleWalls();
    }

    private void AddSpawnPoints(string source, Vector3[] positions, Func<LG_Area, Il2CppSystem.Collections.Generic.List<Transform>> targetList)
    {
        for (int i = 0;  i < positions.Length; i++) 
        {       
            Vector3 pos = positions[i];
            var area = CourseNodeUtil.GetCourseNode(pos, DimensionIndex)?.m_area;
            if (area == null) continue;
            string name = "ARA_" + source + "_SpawnPoint";
            if (i > 0) name += $" ({i})";
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(pos, Quaternion.identity);
            go.transform.SetParent(area.transform, true);
            targetList(area).Add(go.transform);
        }
    }
    
    private void AddInvisibleWalls()
    {
        for (int i = 0; i < InvisibleWalls.Length; i++)
        {
            var wall = InvisibleWalls[i];
            if (wall == null) continue;
            var area = CourseNodeUtil.GetCourseNode(wall.Position, DimensionIndex)?.m_area;
            if (area == null) continue;
            string name = $"ARA_InvisWall_{(int)area.m_zone.DimensionIndex}_{(int)area.m_zone.Layer.m_type}_{(int)area.m_zone.LocalIndex}";
            if (i > 0) name += $" ({i})";
            var go = new GameObject(name) { layer = 13 };
            go.transform.SetParent(area.transform, false); 
            go.transform.SetPositionRotationScale(wall.Position, wall.Rotation, wall.Scale);
            var box = go.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = Vector3.one;
            LayoutConfigManager.TryRegisterARAFilter(wall.HasFilter ? wall.Filter : name, go);
        }
    }

    public void InjectStaticDimensionWorldEventData()
    {
        if (Dimension?.DimensionData?.IsStaticDimension == false || Zone?.m_settings?.m_zoneData == null) 
            return;

        var zoneSettings = Zone.m_settings.m_zoneData;
        zoneSettings.EventsOnTrigger = StaticEventsOnTrigger.ToIl2Cpp();
        zoneSettings.WorldEventChainedPuzzleDatas = StaticWorldEventChainedPuzzleDatas.ToIl2Cpp(s => s.ToWECP());
        zoneSettings.SpecificPickupSpawningDatas = StaticSpecificPickupSpawnDatas.ToIl2Cpp();
        zoneSettings.SpecificTerminalSpawnDatas = StaticSpecificTerminalSpawnDatas.ToIl2Cpp(s => s.ToWEST());
    }    
}
