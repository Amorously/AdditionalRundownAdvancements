using AmorLib.Dependencies;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using GTFO.API;
using HarmonyLib;

namespace ARA;

[BepInPlugin("Amor.ARA", MODNAME, "0.2.4")]
[BepInDependency("dev.gtfomodding.gtfo-api", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("com.dak.MTFO", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("Amor.AmorLib", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency(PData_Wrapper.PLUGIN_GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(InjectLib_Wrapper.PLUGIN_GUID, BepInDependency.DependencyFlags.SoftDependency)]
internal sealed class EntryPoint : BasePlugin
{
    public const string MODNAME = "AdditionalRundownAdvancements";

    public override void Load()
    {
        new Harmony("Amor.ARA").PatchAll();
        AssetAPI.OnStartupAssetsLoaded += OnStartupAssetsLoaded; 
        ARALogger.Info("ARA is done loading!");
    }

    private void OnStartupAssetsLoaded()
    {
        var configs = AccessTools.GetTypesFromAssembly(GetType().Assembly)
            .Where(t => typeof(CustomConfigBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => (CustomConfigBase)Activator.CreateInstance(t)!);
        CustomConfigManager.Setup(configs);
    }
}