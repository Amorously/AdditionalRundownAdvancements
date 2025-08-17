using System.Text.Json.Serialization;

namespace ARA.LevelLayout.DefinitionData;

public sealed class ElevatorCargoCustomData
{
    public uint StartingConsumables { get; set; } = 0u;

    [JsonPropertyName("DisableDefaultElevatorCargoItems")]
    public bool OverrideCargoItems { get; set; } = false;

    [JsonPropertyName("CustomElevatorCargoConsumables")]
    public uint[] CargoConsumables { get; set; } = Array.Empty<uint>();

    [JsonPropertyName("CustomElevatorCargoBigPickups")]
    public uint[] CargoBigPickups { get; set; } = Array.Empty<uint>();
}
