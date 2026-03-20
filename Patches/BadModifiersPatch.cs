using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
namespace CustomModifiers;

[HarmonyPatch(typeof(ModelDb), "get_BadModifiers")]
public class BadModifiersPatch
{
    public static void Postfix(ref IReadOnlyList<ModifierModel> __result)
    {
        // whenever the game looks for the BadModifiers, take the result and add our modifiers to the result
        var extended = __result.ToList();
        extended.Add(ModelDb.Modifier<Famine>());
        extended.Add(ModelDb.Modifier<Colourless>());
        __result = extended.AsReadOnly();
    }
}