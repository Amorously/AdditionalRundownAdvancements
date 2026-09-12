using ARA.LevelLayout.DefinitionData;
using System.Text.Json.Serialization;

namespace ARA.LevelLayout;

public sealed class LayoutConfigDefinition
{
    [JsonIgnore]
    public static readonly LayoutConfigDefinition Empty = new();
    public uint MainLevelLayout { get; set; } = 0u;
    public ElevatorCargoCustomData Elevator { get; set; } = new();
    public string AutoWorldEventObjectPrefix { get; set; } = "WE_ARA_{0}_";
    [JsonPropertyName("SetupWorldEventObjectOnAllTerminals")]
    public bool AllWorldEventTerminals { get; set; } = false;
    public ZoneCustomData[] Zones { get; set; } = Array.Empty<ZoneCustomData>();
}
