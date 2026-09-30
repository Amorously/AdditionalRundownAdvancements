using AIGraph;
using GameData;

namespace ARA.LevelLayout.DefinitionData;

public enum TerminalPrefab
{
    None,
    Default,
    MiningCover,
    MiniTerminal,
    DataCenterCube,
    ServiceCover,
    CyberDeck,
    GardensCover,
    GardensShelf
}

public sealed class SpecificDataContainer
{
    private const string DefaultPrefab = "Assets/AssetPrefabs/Complex/Generic/FunctionMarkers/Terminal_Floor.prefab";
    private const string MiniPrefab = "Assets/AssetPrefabs/Complex/Generic/FunctionMarkers/Terminal_Mini.prefab";
    private readonly record struct PrefabSet(string Prefab, string? Extra = null);
    private static readonly Dictionary<TerminalPrefab, PrefabSet> _prefabMap = new()
    {
        [TerminalPrefab.Default] = new(DefaultPrefab),
        [TerminalPrefab.MiningCover] = new("Assets/AssetPrefabs/Complex/Mining/SubMarkers/Props/submarker_mining_cover_120x120x240/submarker_mining_cover_120x120x240_Terminal_t.prefab"),
        [TerminalPrefab.MiniTerminal] = new(MiniPrefab),
        [TerminalPrefab.ServiceCover] = new("Assets/AssetPrefabs/Complex/Service/MarkerCompositions/Floodways_terminal_wall_120x120x240/Floodways_terminal_wall_120x120x240_01.prefab"),
        [TerminalPrefab.CyberDeck] = new("Assets/AssetPrefabs/Complex/Generic/FunctionMarkers/Terminal_CyberDeck.prefab"),
        [TerminalPrefab.DataCenterCube] = new
        (
            Prefab: "Assets/AssetPrefabs/Complex/Tech/Markers/MarkerCompositions/DataCenter_submarker_240x240x400/DataCenter_submarker_240x240x400_V01_terminal.prefab",
            Extra: MiniPrefab
        ),
        [TerminalPrefab.GardensCover] = new
        (
            Prefab: "Assets/AssetPrefabs/Complex/Service/MarkerCompositions/Gardens_Concrete_Submarker_Station_180x180x240/Gardens_Concrete_Submarker_Station_180x180x240_V02.prefab",
            Extra: "Assets/AssetPrefabs/Complex/Service/MarkerCompositions/Gardens_concrete_terminal_locker_200x300x50/Gardens_concrete_terminal_locker_200x300x50.prefab"
        ),
        [TerminalPrefab.GardensShelf] = new
        (
            Prefab: "Assets/AssetPrefabs/Complex/Service/MarkerCompositions/Gardens_Concrete_Submarker_Station_180x180x240/Gardens_Concrete_Submarker_Station_180x180x240_V03.prefab",
            Extra: MiniPrefab
        )
    };

    public string WorldEventObjectFilter;
    public AIG_CourseNode SpawnNode;
    public TerminalPrefab TerminalPrefabOverride;
    public List<WardenObjectiveEventData> EventsOnPickup;

    public SpecificDataContainer(string filter, AIG_CourseNode node, TerminalPrefab prefabType, List<WardenObjectiveEventData> events)
    {
        WorldEventObjectFilter = filter;
        SpawnNode = node;
        TerminalPrefabOverride = prefabType;
        EventsOnPickup = events;
    }

    public string GetCustomPrefabs(out string? extraPrefab)
    {
        if (_prefabMap.TryGetValue(TerminalPrefabOverride, out var pair))
        {
            extraPrefab = pair.Extra;
            return pair.Prefab;
        }

        ARALogger.Warn($"Invalid PrefabOverride \"{TerminalPrefabOverride}\" for filter {WorldEventObjectFilter}, using default prefab");
        extraPrefab = null;
        return DefaultPrefab;
    }
}