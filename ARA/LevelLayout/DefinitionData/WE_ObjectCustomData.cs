using AmorLib.Utils;
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
    public CustomTransform[] RandomPositions { get; set; } = Array.Empty<CustomTransform>();
    public Vector3 Position { get; set; } = Vector3.zero;
    public Vector3 Rotation { get; set;} = Vector3.zero;
    public Vector3 Scale { get; set; } = Vector3.one;
    public Dictionary<WorldEventComponent, WE_ComponentCustomData> Components { get; set; } = new();

    internal LG_Area Area { get; private set; } = null!;

    private static ILookup<int, LG_WorldEventObject> _preAllocWE = null!;
    private static System.Random _weRand = null!;

    internal static void AllocatePreexistingWorldEventObjects()
    {
         _preAllocWE = // map existing WE objects
        (
            from weObj in UnityEngine.Object.FindObjectsOfType<LG_WorldEventObject>()
            let area = weObj.ParentArea ?? CourseNodeUtil.GetCourseNode(weObj.transform.position)?.m_area
            where area != null
            select (key: area.GetInstanceID(), weObj)
        ).ToLookup(x => x.key, y => y.weObj);
        _weRand = RandomUtil.CreateSessionRandom("WE_ObjectCustomData");
    }

    internal bool ShouldCreateNewWorldEventObject(LG_Zone zone, [MaybeNull] out LG_WorldEventObject weObj)
    {
        if (!IsAreaIndexValid(zone))
        {
            weObj = null;
            return false;
        }

        if (UseRandomPosition)
        {
            if (RandomPositions.Length > 0)
            {
                var selected = RandomPositions[_weRand.Next(RandomPositions.Length)];
                Position = selected.Position;
                Rotation = selected.Rotation;
                Scale = selected.Scale;
            }
            else if (AreaIndex == -1)
            {
                var randArea = zone.m_areas[_weRand.Next(zone.m_areas.Count)];
                Position = randArea.m_courseNode.GetRandomPositionInside();
                Rotation = new(0f, _weRand.Next(360), 0f);
            }
            else
            {
                Position = Area.m_courseNode.GetRandomPositionInside();
                Rotation = new(0f, _weRand.Next(360), 0f);
            }
        }               

        if (UseExistingFilterInArea)
        {
            weObj = _preAllocWE[Area.GetInstanceID()].FirstOrDefault(w => w.WorldEventObjectKey == WorldEventObjectFilter);
            if (weObj != null)
            {
                Position = weObj.transform.position;
                if (Rotation != Vector3.zero) weObj.transform.rotation = Quaternion.Euler(Rotation);
                if (Scale != Vector3.one) weObj.transform.localScale = Scale;
            }
            else
            {
                ARALogger.Error($"Unable to find preexisting \"{WorldEventObjectFilter}\" in area");
            }
            return false;
        }

        weObj = null;
        return true;
    }

    private bool IsAreaIndexValid(LG_Zone zone)
    {        
        if (AreaIndex >= 0 && AreaIndex < zone.m_areas.Count)
        {
            Area = zone.m_areas[AreaIndex];
            return true; 
        }
        else if (AreaIndex == -1)
        {
            try
            {
                Area = CourseNodeUtil.GetCourseNode(Position, zone.DimensionIndex).m_area;
                if (!zone.m_areas.Contains(Area))
                    ARALogger.Warn($"\"{WorldEventObjectFilter}\": Area inferred from {Position} is not in {zone.ToIntTuple()}");
                return true;
            }
            catch
            {
                ARALogger.Error($"\"{WorldEventObjectFilter}\": Failed to infer AreaIndex at {Position}");
            }
        }

        ARALogger.Error($"Invalid AreaIndex {AreaIndex} for filter {WorldEventObjectFilter}!");
        Area = null!;
        return false;
    }
}
