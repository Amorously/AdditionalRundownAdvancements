using GameData;

namespace ARA.LevelLayout.DefinitionData;

public sealed class TerminalCustomData
{
    public int TerminalIndex { get; set; } = 0;
    public bool HideCover { get; set; } = false;
    public LogFileCustomData[] LogFiles { get; set; } = Array.Empty<LogFileCustomData>();
}

public sealed class LogFileCustomData
{
    public string FileName { get; set; } = string.Empty;
    public List<WardenObjectiveEventData> EventsOnFileRead { get; set; } = new();
}