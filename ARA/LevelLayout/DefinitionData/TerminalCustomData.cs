using GameData;

namespace ARA.LevelLayout.DefinitionData;

public enum TerminalPrefab
{
    None,
    Default,
    MiningCover,
    MiniTerminal,
    ServiceCover,
    CyberDeck
}

public sealed class TerminalCustomData
{
    public int TerminalIndex { get; set; } = 0;
    public TerminalPrefab TerminalPrefabOverride { get; set; } = TerminalPrefab.None;
    public LogFileCustomData[] LogFiles { get; set; } = Array.Empty<LogFileCustomData>();
}

public sealed class LogFileCustomData
{
    public string FileName { get; set; } = string.Empty;
    public List<WardenObjectiveEventData> EventsOnFileRead { get; set; } = new();
}