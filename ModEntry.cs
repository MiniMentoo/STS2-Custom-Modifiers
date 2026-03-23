using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Logging;
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace CustomModifiers;

[ModInitializer("Init")]
public class ModEntry
{
    public static Harmony Harmony { get; private set; } = null!;

    public static void Init()
    {
        Log.Info("[CustomModifiers] Init() called — mod is loading");
        
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(AllRelicsX));

        Harmony = new Harmony("com.minimento.customModifiers");
        Harmony.PatchAll();
    }
}