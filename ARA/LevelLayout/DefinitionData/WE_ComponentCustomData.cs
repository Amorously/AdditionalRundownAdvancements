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

public struct CustomTransform
{
    public Vector3 Position;
    public Vector3 Rotation;
    public Vector3 Scale;

    [JsonConstructor]
    public CustomTransform(Vector3 position, Vector3 rotation, Vector3 scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale == default ? Vector3.one : scale;
    }
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
