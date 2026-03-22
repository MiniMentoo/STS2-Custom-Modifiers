using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace CustomModifiers;

[HarmonyPatch]
public class AllRelicsX : ModifierModel
{
    private ModelId? _chosenRelicId;

    [ThreadStatic]
    private static RelicModel? _preSwapRelic;

    [SavedProperty]
    public ModelId ChosenRelicId
    {
        get => _chosenRelicId ?? throw new InvalidOperationException(
            "[AllRelicsAreX] ChosenRelicId accessed before it was set!");
        set
        {
            AssertMutable();
            _chosenRelicId = value;
        }
    }

    private bool HasChosenRelic => _chosenRelicId != null;

    public override Func<Task> GenerateNeowOption(EventModel eventModel)
    {
        return () => ChooseAndSetupRelic(
            eventModel.Owner ?? throw new InvalidOperationException(
                "[AllRelicsAreX] GenerateNeowOption called with null Owner"));
    }

    private async Task ChooseAndSetupRelic(Player player)
    {
        var allRelics = ModelDb.AllRelics.ToList();
        RelicModel chosenCanonical;

        if (RunManager.Instance.DailyTime.HasValue)
        {
            var dailyRng = new Rng(player.RunState.Rng.Seed);
            chosenCanonical = dailyRng.NextItem(allRelics)
                ?? throw new InvalidOperationException(
                    "[AllRelicsAreX] No relics available for daily pick");
        }
        else
        {
            var rng = new Rng(player.RunState.Rng.Seed );
                        rng.Shuffle(allRelics);
                        var fiveRelics = allRelics.Take(5).ToList();
                        var picked = await RelicSelectCmd.FromChooseARelicScreen(player, fiveRelics);
            // gui can't handle ALL the relics at once currently so just randomly generating 5 for now
            //var picked = await RelicSelectCmd.FromChooseARelicScreen(player, allRelics);
            
            if (picked == null)
                throw new InvalidOperationException(
                    "[AllRelicsAreX] Player did not select a relic");

            chosenCanonical = ModelDb.GetById<RelicModel>(picked.Id);
        }
        
        ChosenRelicId = chosenCanonical.Id;
        Log.Info($"[AllRelicsAreX] Chosen relic: {chosenCanonical.Id}");

        foreach (var relic in player.Relics.ToList())
        {
            await RelicCmd.Remove(relic);
        }
        
        await RelicCmd.Obtain(chosenCanonical.ToMutable(), player);
    }
    

    [HarmonyPrefix]
    [HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Obtain),
        new[] { typeof(RelicModel), typeof(Player), typeof(int) })]
    static void ObtainPrefix(ref RelicModel relic, Player player)
    {
        _preSwapRelic = null;

        var modifier = GetModifier(player.RunState);
        if (modifier == null || !modifier.HasChosenRelic) return;
        if (relic.Id == modifier.ChosenRelicId) return;

        var canonical = ModelDb.GetById<RelicModel>(modifier.ChosenRelicId);
        _preSwapRelic = relic;          // save original so postfix can restore it as return value
        relic = canonical.ToMutable();  // swap — Obtain now animates/AfterObtained with chosen relic

        Log.Info($"[AllRelicsAreX] ObtainPrefix: redirected to {canonical.Id}");
    }
    

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RelicGrabBag), nameof(RelicGrabBag.PullFromFront))]
    static void RedirectPullFromFront(ref RelicModel? __result, IRunState runState)
    {
        if (__result == null) return;

        var modifier = GetModifier(runState);
        if (modifier == null || !modifier.HasChosenRelic) return;

        __result = ModelDb.GetById<RelicModel>(modifier.ChosenRelicId);
        Log.Info($"[AllRelicsAreX] Redirected PullFromFront to {__result.Id}");
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Obtain),
        new[] { typeof(RelicModel), typeof(Player), typeof(int) })]
    static void ObtainPostfix(ref Task<RelicModel> __result)
    {
        if (_preSwapRelic == null) return;
        var original = _preSwapRelic;
        _preSwapRelic = null;
        __result = __result.ContinueWith(_ => original);
    }
    

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RelicGrabBag), nameof(RelicGrabBag.PullFromBack))]
    static void RedirectPullFromBack(ref RelicModel? __result, IRunState runState)
    {
        if (__result == null) return;

        var modifier = GetModifier(runState);
        if (modifier == null || !modifier.HasChosenRelic) return;

        __result = ModelDb.GetById<RelicModel>(modifier.ChosenRelicId);
        Log.Info($"[AllRelicsAreX] Redirected PullFromBack to {__result.Id}");
    }


    private static AllRelicsX? GetModifier(IRunState runState)
    {
        if (runState is not RunState rs) return null;
        return rs.Modifiers.OfType<AllRelicsX>().FirstOrDefault();
    }
}