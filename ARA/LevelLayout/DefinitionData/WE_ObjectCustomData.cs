using AmorLib.Utils.Extensions;
using LevelGeneration;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace ARA.LevelLayout.DefinitionData;

public sealed class WE_ObjectCustomData
{
    public string WorldEventObjectFilter { get; set; } = string.Empty;
    public int AreaIndex { get; set; } = 0;
    public bool UseExistingFilterInArea { get; set; } = false;
    public bool UseRandomPosition { get; set; } = false;
    public Vector3 Position { get; set; } = Vector3.zero;
    public Vector3 Rotation { get; set;} = Vector3.zero;
    public Vector3 Scale { get; set; } = Vector3.one;
    public Dictionary<WorldEventComponent, WE_ComponentCustomData> Components { get; set; } = new();

    public bool IsAreaIndexValid(LG_Zone zone, [MaybeNullWhen(false)] out LG_Area area)
    {
        if (AreaIndex >= 0 && AreaIndex < zone.m_areas.Count)
        {
            area = zone.m_areas[AreaIndex];
            return true; 
        }

        ARALogger.Error($"Invalid AreaIndex {AreaIndex} for filter {WorldEventObjectFilter}!");
        area = null;
        return false;
    }

    public bool TryGetExistingFilterInArea(Dictionary<int, List<LG_WorldEventObject>> preAllocWE, LG_Area area, [MaybeNullWhen(false)] out LG_WorldEventObject weObj)
    {
        weObj = preAllocWE.GetOrAddNew(area.GetInstanceID()).FirstOrDefault(we => we.WorldEventObjectKey == WorldEventObjectFilter);
        if (weObj == null) return false;
        
        Position = weObj.transform.position;
        if (Rotation != Vector3.zero) weObj.transform.rotation = Quaternion.Euler(Rotation);
        if (Scale != Vector3.one) weObj.transform.localScale = Scale;

        return true;
    }
}
