using GameData;
using System.Text.Json.Serialization;
using UnityEngine;

namespace ARA.LevelLayout.DefinitionData;

public enum WorldEventComponent
{
    None,
    WE_ChainedPuzzle,
    WE_NavMarker,
    WE_CollisionTrigger,
    WE_LookatTrigger,
    WE_InteractTrigger,
    WE_SpecificTerminal,
    WE_SpecificPickup,
    WE_AnimationTrigger
}

public enum ColliderType
{
    None,
    Box,
    Sphere,
    Capsule
}

public class FilterTransform : CustomTransform
{
    public string Filter { get; set; } = string.Empty;

    [JsonIgnore]
    public bool HasFilter => !string.IsNullOrWhiteSpace(Filter);
}

public class CustomTransform
{
    public Vector3 Position { get; set; } = Vector3.zero;
    public Vector3 Rotation { get; set; } = Vector3.zero;
    public Vector3 Scale { get; set; } = Vector3.one;
}

public sealed class WE_ComponentCustomData
{
    public TerminalPrefab PrefabOverride { get; set; } = TerminalPrefab.None;
    public List<WardenObjectiveEventData> EventsOnPickup { get; set; } = new();
    public PlaceNavMarkerOnGO.eMarkerType NavMarkerType { get; set; } = PlaceNavMarkerOnGO.eMarkerType.Waypoint;
    public bool PlaceOnStart { get; set; } = true;
    public ColliderType ColliderType { get; set; } = ColliderType.None;
    public Vector3 Center { get; set; } = Vector3.zero;
    public Vector3 Size { get; set; } = Vector3.one;
    public float Radius { get; set; } = 0f;
    public float Height { get; set; } = 0f;
    public bool IsToggle { get; set; } = false;
    public float LookatMaxDistance { get; set; } = 0f;
    public uint InteractionText { get; set; } = 0u;    
    public eCarryItemInsertTargetType CarryItemInsertType { get; set; } = eCarryItemInsertTargetType.None;
    public CustomTransform CarryItemTransform { get; set; } = new();
    public bool RemoveItemOnInsert { get; set; } = false;
    public eCarryItemCustomState ItemStateAfterInsert { get; set; } = eCarryItemCustomState.Default;
    public string WorldEventAnimationFilter { get; set; } = string.Empty;
    public bool PlayResetOnStartup { get; set; } = false;
    public bool ActivationMode { get; set; } = true;
    public string[] ARAObjectsToActivate { get; set; } = Array.Empty<string>();
}
