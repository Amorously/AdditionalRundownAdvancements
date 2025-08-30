using AmorLib.Utils;
using LevelGeneration;
using UnityEngine;

namespace ARA.LevelLayout.DefinitionData;

public sealed class ZoneCustomData : GlobalBase
{
    public Vector3[] HibernateSpawnAligns { get; set; } = Array.Empty<Vector3>();
    public Vector3[] EnemySpawnPoints { get; set; } = Array.Empty<Vector3>();
    public Vector3[] BioscanSpawnPoints { get; set; } = Array.Empty<Vector3>();
    //public uint PortalMachineChainedPuzzle { get; set; } = 4u;
    public bool ForceGeneratorClusterMarkers { get; set; } = false;
    //public TerminalCustomData[] Terminals { get; set; } = Array.Empty<TerminalCustomData>();
    public WE_ObjectCustomData[] WorldEventObjects { get; set; } = Array.Empty<WE_ObjectCustomData>();

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
            var area = CourseNodeUtil.GetCourseNode(pos, DimensionIndex).m_area;
            if (area == null) continue;
            string name = "ARA_" + source + "_SpawnPoint";
            if (i > 0) name += $" ({i})";
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(pos, Quaternion.identity);
            go.transform.SetParent(area.transform, true);
            targetList(area).Add(go.transform);
        }
    }
}
