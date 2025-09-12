using LevelGeneration;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace ARA.LevelLayout.DefinitionData;

public sealed class WE_ObjectCustomData
{
    public string WorldEventObjectFilter { get; set; } = string.Empty;
    public int AreaIndex { get; set; } = 0;
    public bool UseRandomPosition { get; set; } = false;
    public Vector3 Position { get; set; } = Vector3.zero;
    public Vector3 Rotation { get; set;} = Vector3.zero;
    public Vector3 Scale { get; set; } = Vector3.one;
    public HashSet<WE_ComponentCustomData> Components { get; set; } = new();
    //public Dictionary<WorldEventComponent, WE_ComponentCustomData> Components = new();

    public bool IsAreaIndexValid(LG_Zone zone, [MaybeNullWhen(false)] out LG_Area area)
    {
        if (AreaIndex < 0 || AreaIndex >= zone.m_areas.Count)
        {
            area = null;
            return false;
        }
        area = zone.m_areas[AreaIndex];
        return true; 
    }
}
