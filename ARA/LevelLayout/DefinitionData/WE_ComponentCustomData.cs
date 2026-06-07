using GameData;
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

public struct CarryItemTransform
{
    public Vector3 Position;
    public Vector3 Rotation;
    public Vector3 Scale;
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
    public CarryItemTransform CarryItemTransform { get; set; } = new();
    public bool RemoveItemOnInsert { get; set; } = false;
    public eCarryItemCustomState ItemStateAfterInsert { get; set; } = eCarryItemCustomState.Default;
    public string WorldEventAnimationFilter { get; set; } = string.Empty;
    public bool PlayResetOnStartup { get; set; } = false;
    public bool ActivationMode { get; set; } = true;
}
