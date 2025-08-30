using GTFO.API;
using LevelGeneration;

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
        }

        LevelAPI.OnBuildStart += OnBuildStart;
        LevelAPI.OnBeforeBuildBatch += OnBeforeBatchBuild;
        LevelAPI.OnBuildDone += OnBuildDone;
        LevelAPI.OnEnterLevel += OnEnterLevel;
        LevelAPI.OnLevelCleanup += OnLevelCleanup;
    }

    private static void OnBuildStart() => CustomConfigs.ForEach(config => config.OnBuildStart());
    private static void OnBeforeBatchBuild(LG_Factory.BatchName batch) => CustomConfigs.ForEach(config => config.OnBeforeBatchBuild(batch));
    private static void OnBuildDone() => CustomConfigs.ForEach(config => config.OnBuildDone());
    private static void OnEnterLevel() => CustomConfigs.ForEach(config => config.OnEnterLevel());
    private static void OnLevelCleanup() => CustomConfigs.ForEach(config => config.OnLevelCleanup());
}
