using AmorLib.Dependencies;
using ARA.LevelLayout;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using GTFO.API;
using HarmonyLib;
using System.Runtime.CompilerServices;

namespace ARA;

[BepInPlugin("Amor.ARA", "AdditionalRundownAdvancements", "0.0.1")]
[BepInDependency("dev.gtfomodding.gtfo-api", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("com.dak.MTFO", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("Amor.AmorLib", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency(PData_Wrapper.PLUGIN_GUID, BepInDependency.DependencyFlags.SoftDependency)]
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
        RuntimeHelpers.RunClassConstructor(typeof(LayoutConfigManager).TypeHandle);
    }
}