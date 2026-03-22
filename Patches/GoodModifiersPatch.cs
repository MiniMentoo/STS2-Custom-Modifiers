using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
namespace CustomModifiers;

[HarmonyPatch(typeof(ModelDb), "get_GoodModifiers")]
public class GoodModifiersPatch
{
    public static void Postfix(ref IReadOnlyList<ModifierModel> __result)
    {
        // whenever the game looks for the GoodModifiers, take the result and add our modifiers to the result
        var extended = __result.ToList();
        extended.Add(ModelDb.Modifier<VintagePlus>());
        extended.Add(ModelDb.Modifier<SpecializedPlus>());
        extended.Add(ModelDb.Modifier<AllRelicsX>());
        __result = extended.AsReadOnly();
    }
}