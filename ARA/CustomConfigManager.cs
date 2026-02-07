using GTFO.API;

namespace ARA;

public static class CustomConfigManager
{
    public static readonly List<CustomConfigBase> CustomConfigs = new();

    public static void Setup(IEnumerable<CustomConfigBase> configs)
    {
        foreach (var config in configs)
        {
            CustomConfigs.Add(config);
            config.Setup();
            
            LevelAPI.OnBuildStart += config.OnBuildStart;
            LevelAPI.OnBeforeBuildBatch += config.OnBeforeBatchBuild;
            LevelAPI.OnBuildDone += config.OnBuildDone;
            LevelAPI.OnEnterLevel += config.OnEnterLevel;
            LevelAPI.OnLevelCleanup += config.OnLevelCleanup;
        }
    }
}
