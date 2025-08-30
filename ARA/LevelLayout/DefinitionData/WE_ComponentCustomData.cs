using AmorLib.Utils.JsonElementConverters;
using UnityEngine;
using UnityEngine.Animations;

namespace ARA.LevelLayout.DefinitionData;

public enum WorldEventComponent
{
    None,
    WE_ChainedPuzzle,
    WE_NavMarker,
    WE_CollisionTrigger,
    WE_LookatTrigger,
    WE_InteractTrigger
}

public enum ColliderType
{
    Box,
    Sphere,
    Capsule
}

public sealed class WE_ComponentCustomData
{
    public WorldEventComponent Type { get; set; } = WorldEventComponent.None;
    public PlaceNavMarkerOnGO.eMarkerType NavMarkerType { get; set; } = PlaceNavMarkerOnGO.eMarkerType.Waypoint;
    public bool PlaceOnStart { get; set; } = true;
    public bool IsToggle { get; set; } = false;
    public ColliderType ColliderType { get; set; } = ColliderType.Box;
    public Vector3 Center { get; set; } = Vector3.zero;
    public Vector3 Size { get; set; } = Vector3.one;
    public float Radius { get; set; } = 0.0f;
    public float Hieght { get; set; } = 0.0f;
    public Axis Direction { get; set; } = Axis.None;
    public float LookatMaxDistance { get; set; } = 0.0f;
    public LocaleText InteractionText { get; set; } = LocaleText.Empty;
    public eCarryItemInsertTargetType CarryItemInsertType { get; set; } = eCarryItemInsertTargetType.None;
    public bool RemoveItemOnInsert { get; set; } = false;
}
