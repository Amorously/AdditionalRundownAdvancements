using LevelGeneration;
using MTFO.API;

namespace ARA;

public abstract class CustomConfigBase
{
    public static readonly string Module = Path.Combine(MTFOPathAPI.CustomPath, EntryPoint.MODNAME);
    public abstract string ModulePath { get; }
    public abstract void Setup();
    public virtual void OnBuildStart() { }
    public virtual void OnBeforeBatchBuild(LG_Factory.BatchName batch) { }
    public virtual void OnBuildDone() { }
    public virtual void OnEnterLevel() { }
    public virtual void OnLevelCleanup() { }
}
