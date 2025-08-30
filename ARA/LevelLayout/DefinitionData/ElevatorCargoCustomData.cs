using System.Text.Json.Serialization;

namespace ARA.LevelLayout.DefinitionData;

public sealed class ElevatorCargoCustomData
{
    [JsonPropertyName("ForceDisableElevatorCargo")]
    public bool DisableElevatorCargo { get; set; } = false;
    
    [JsonPropertyName("OverrideDefaultElevatorCargoItems")]
    public bool OverrideCargoItems { get; set; } = false;

    [JsonPropertyName("CustomElevatorCargoItems")]
    public uint[] CargoItems { get; set; } = Array.Empty<uint>();
}
