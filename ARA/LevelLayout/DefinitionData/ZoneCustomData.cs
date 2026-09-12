using AmorLib.Utils;
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
    public bool ForceGeneratorClusterMarkers { get; set; } = false;
    [JsonPropertyName("SetupWorldEventObjectOnLightsInArea")]
    public int[] AllWorldEventLights { get; set; } = Array.Empty<int>();
    public WE_ObjectCustomData[] WorldEventObjects { get; set; } = Array.Empty<WE_ObjectCustomData>();
    public List<WorldEventFromSourceData> StaticEventsOnTrigger { get; set; } = new();
    public List<StaticDimWECPData> StaticWorldEventChainedPuzzleDatas { get; set; } = new();
    public List<SpecificPickupSpawnData> StaticSpecificPickupSpawnDatas { get; set; } = new();
    public List<StaticDimWESTData> StaticSpecificTerminalSpawnDatas { get; set; } = new();

    public void AddSpawnPoints()
    {
        AddSpawnPoints("HSA", HibernateSpawnAligns, area => area.m_spawnAligns);
        AddSpawnPoints("ESP", EnemySpawnPoints, area => area.m_enemySpawnPoints);
        AddSpawnPoints("SBP", BioscanSpawnPoints, area => area.m_bioscanSpawnPoints);
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

    public void InjectStaticDimensionWorldEventData()
    {
        if (Dimension?.DimensionData?.IsStaticDimension == false || Zone?.m_settings?.m_zoneData == null) return;

        var zoneData = Zone.m_settings.m_zoneData;
        zoneData.EventsOnTrigger = StaticEventsOnTrigger.ToIl2Cpp();
        zoneData.WorldEventChainedPuzzleDatas = StaticWorldEventChainedPuzzleDatas.ToIl2Cpp(s => s.ToWECP());
        zoneData.SpecificPickupSpawningDatas = StaticSpecificPickupSpawnDatas.ToIl2Cpp();
        zoneData.SpecificTerminalSpawnDatas = StaticSpecificTerminalSpawnDatas.ToIl2Cpp(s => s.ToWEST());
    }

    public void SetupInvisibleWalls()
    {
        
    }
}
