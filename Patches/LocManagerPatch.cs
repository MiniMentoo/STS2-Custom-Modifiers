using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace CustomModifiers;

// hooking onto set language so the localization info doesn't go away when the language changes (unfortunately the descriptions still only work in english)
[HarmonyPatch(typeof(LocManager), nameof(LocManager.SetLanguage))]
public class LocManagerPatch
{
    public static void Postfix(LocManager __instance)
    {
        __instance.GetTable("modifiers").MergeWith(
            new Dictionary<string, string>
            {
                {"VINTAGE_PLUS.title", "Vintage+" },
                {"VINTAGE_PLUS.description", "Normal enemies drop 2 relics for each card reward." },
                
                {"FAMINE.title", "Famine"},
                {"FAMINE.description", "You cannot gain relics."},
                
                {"COLOURLESS.title", "Colourless"},
                {"COLOURLESS.description", "Lose your class specific cards and relics, you can only draft colourless cards. Lose your starting relic."},
                
                {"SPECIALIZED_PLUS.title", "Specialized+"},
                {"SPECIALIZED_PLUS.description", "Choose ANY card, add 5 copies of that card to your deck."},
                {"SPECIALIZED_PLUS.selectionPrompt", "Choose a card to add 5 copies of to your deck"},
                
                {"ALL_RELICS_X.title", "All Relics are X"},
                {"ALL_RELICS_X.description", "Choose ANY relic, your starting relic and all relics obtained become that relic."},
                {"ALL_RELICS_X.selectionPrompt", "Choose the relic you'll see this run"}
            });
    }
}