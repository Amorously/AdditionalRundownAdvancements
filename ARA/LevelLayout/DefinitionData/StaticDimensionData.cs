using AmorLib.Utils;
using AmorLib.Utils.JsonElementConverters;
using ARA.Utils;
using GameData;
using GTFO.API.Extensions;
using LevelGeneration;

namespace ARA.LevelLayout.DefinitionData;

public sealed class StaticDimensionWECPData // world event chanined puzzle
{
    public uint ChainedPuzzle { get; set; } = 0u;
    public string WorldEventObjectFilter { get; set; } = string.Empty;
    public List<WardenObjectiveEventData> EventsOnScanDone { get; set; } = new();

    public SpecificChainPuzzleSpawnData ToWECP()
    {
        return new()
        {
            ChainedPuzzle = ChainedPuzzle,
            WorldEventObjectFilter = WorldEventObjectFilter,
            EventsOnScanDone = EventsOnScanDone.ToIl2Cpp()
        };
    }
}

public sealed class StaticDimensionWESTData // world event specfic terminal (cool acronym)
{
    public string WorldEventObjectFilter { get; set; } = string.Empty;
    public bool IsWardenObjective { get; set; } = false;
    public int WardenObjectiveChainIndex { get; set; } = -1;
    public List<TerminalLogFileData> LocalLogFiles { get; set; } = new();
    public List<LocaleCommandData> UniqueCommands { get; set; } = new();
    public LocaleStartingStateData StartingStateData { get; set; } = new();

    public SpecificTerminalSpawnData ToWEST()
    {
        return new()
        {
            WorldEventObjectFilter = WorldEventObjectFilter,
            IsWardenObjective = IsWardenObjective,
            WardenObjectiveChainIndex = WardenObjectiveChainIndex,
            LocalLogFiles = LocalLogFiles.ToIl2Cpp(),
            UniqueCommands = UniqueCommands.ToIl2Cpp(s => s.ToTerminalCommand()),
            StartingStateData = StartingStateData.ToStartingState()
        };
    }
}

#region MANAGED_TERMINAL_DATA
public sealed class LocaleCommandData
{
    public string Command { get; set; } = string.Empty;
    public LocaleText CommandDesc { get; set; } = new();
    public List<LocaleTerminalOutput> PostCommandOutputs { get; set; } = new();
    public List<WardenObjectiveEventData> CommandEvents { get; set; } = new();
    public TERM_CommandRule SpecialCommandRule { get; set; } = TERM_CommandRule.Normal;

    public CustomTerminalCommand ToTerminalCommand()
    {
        return new()
        {
            Command = Command,
            CommandDesc = CommandDesc.ParseToLocalizedText(),
            PostCommandOutputs = PostCommandOutputs.ToIl2Cpp(x => x.ToTerminalOutput()),
            CommandEvents = CommandEvents.ToIl2Cpp(),
            SpecialCommandRule = SpecialCommandRule
        };
    }
}

public sealed class LocaleTerminalOutput
{
    public TerminalLineType LineType { get; set; }
    public LocaleText Output { get; set; }
    public float Time { get; set; }

    public TerminalOutput ToTerminalOutput()
    {
        return new()
        {
            LineType = LineType,
            Output = Output.ParseToLocalizedText(),
            Time = Time,
        };
    }
}

public sealed class LocaleStartingStateData
{
    public TERM_State StartingState { get; set; } = TERM_State.Sleeping;
    public bool UseCustomInfoText { get; set; } = false;
    public LocaleText CustomInfoText { get; set; } = new();
    public bool KeepShowingLocalLogCount { get; set; } = false;
    public uint AudioEventEnter { get; set; } = 0;
    public uint AudioEventExit { get; set; } = 0;
    public bool PasswordProtected { get; set; } = false;
    public string Password { get; set; } = string.Empty;
    public string PasswordHintText { get; set; } = string.Empty;
    public bool GeneratePassword { get; set; } = false;
    public int PasswordPartCount { get; set; } = 0;
    public bool ShowPasswordLength { get; set; } = false;
    public bool ShowPasswordPartPositions { get; set; } = false;
    public List<List<PasswordSelectionData>> TerminalZoneSelectionDatas { get; set; } = new();

    public TerminalStartStateData ToStartingState()
    {
        return new()
        {
            StartingState = StartingState,
            UseCustomInfoText = UseCustomInfoText,
            CustomInfoText = CustomInfoText,
            KeepShowingLocalLogCount = KeepShowingLocalLogCount,
            AudioEventEnter = AudioEventEnter,
            AudioEventExit = AudioEventExit,
            PasswordProtected = PasswordProtected,
            Password = Password,
            PasswordHintText = PasswordHintText,
            GeneratePassword = GeneratePassword,
            PasswordPartCount = PasswordPartCount,
            ShowPasswordLength = ShowPasswordLength,
            ShowPasswordPartPositions = ShowPasswordPartPositions,
            TerminalZoneSelectionDatas = TerminalZoneSelectionDatas.ToIl2Cpp(inner => inner.ToIl2Cpp(x => x.ToPasswordSelection()))
        };
    }
}

public sealed class PasswordSelectionData : GlobalBase
{
    public eSeedType SeedType { get; set; } = eSeedType.SessionSeed;
    public int TerminalIndex { get; set; } = 0;
    public int StaticSeed { get; set; } = 0;

    public PasswordSelectionData()
    {
        TerminalIndex = Math.Max(0, TerminalIndex);
    }

    public TerminalZoneSelectionData ToPasswordSelection()
    {
        return new()
        {
            LocalIndex = LocalIndex,
            SeedType = SeedType,
            TerminalIndex = TerminalIndex,
            StaticSeed = StaticSeed
        };
    }
}
#endregion