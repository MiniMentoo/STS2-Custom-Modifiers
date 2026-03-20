using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Logging;


namespace CustomModifiers;

[HarmonyPatch]
public class Famine : ModifierModel
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Obtain), 
        new[] { typeof(RelicModel), typeof(Player), typeof(int) })]
    static bool PreventRelicObtain(RelicModel relic, Player player, ref Task<RelicModel> __result)
    {
        Log.Info("[CustomModifiers] PreventRelicObtain has been called");
        if (player.RunState.Modifiers.Any(m => m is Famine))
        {
            Log.Info("[CustomModifiers] PreventRelicObtain detects it's in Famine");
            __result = Task.FromResult(relic);
            return false;
        }
        Log.Info("[CustomModifiers] PreventRelicObtain detects no Famine");
        return true;
    }
}