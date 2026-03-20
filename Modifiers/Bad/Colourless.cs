using System.Diagnostics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
 
// ⚠ Replace with the correct namespace for Strike/Defend card models once confirmed
using MegaCrit.Sts2.Core.Models.Cards;
 
namespace CustomModifiers;
 
public class Colourless : ModifierModel
{

    public override bool ClearsPlayerDeck => false;
    
    public override Func<Task> GenerateNeowOption(EventModel eventModel)
    {
        return () =>
        {
            Debug.Assert(eventModel.Owner != null, "eventModel.Owner != null");
            return SetupColourless(eventModel.Owner);
        };
    }
 
    private static async Task SetupColourless(Player player)
    {
        var startingRelicTypes = new RelicModel[]
        {
            ModelDb.Relic<BurningBlood>(),
            ModelDb.Relic<RingOfTheSnake>(),
            ModelDb.Relic<DivineRight>(),
            ModelDb.Relic<BoundPhylactery>(),
            ModelDb.Relic<CrackedCore>(),
        };
 
        foreach (var p in player.RunState.Players)
        {
            foreach (var canonicalRelic in startingRelicTypes)
            {
                var owned = p.Relics.FirstOrDefault(r => r.Id == canonicalRelic.Id);
                if (owned != null)
                {
                    await RelicCmd.Remove(owned);
                }
            }
            p.Creature.SetMaxHpInternal(75);
        }
 
        Log.Info("[CustomModifiers] Colourless: Removed starting relics");
    }
    
    protected override void AfterRunCreated(RunState runState)
    {
        StripCharacterRelics(runState);

        // Strip non-strike/defend cards from all players' decks before Neow fires,
        // so other modifiers' Neow options can add cards freely afterward.
        var targetIds = new HashSet<ModelId>
        {
            ModelDb.Card<StrikeIronclad>().Id,
            ModelDb.Card<DefendIronclad>().Id,
            ModelDb.Card<StrikeSilent>().Id,
            ModelDb.Card<DefendSilent>().Id,
            ModelDb.Card<StrikeRegent>().Id,
            ModelDb.Card<DefendRegent>().Id,
            ModelDb.Card<StrikeNecrobinder>().Id,
            ModelDb.Card<DefendNecrobinder>().Id,
            ModelDb.Card<StrikeDefect>().Id,
            ModelDb.Card<DefendDefect>().Id,
        };

        foreach (var player in runState.Players)
        {
            foreach (var card in player.Deck.Cards.Where(c => !targetIds.Contains(c.Id)).ToList())
            {
                player.Deck.RemoveInternal(card);
                runState.RemoveCard(card);
            }
        }
    }
    
    protected override void AfterRunLoaded(RunState runState)
    {
        StripCharacterRelics(runState);
    }
 
    private static void StripCharacterRelics(RunState runState)
    {
        var characterRelics = ModelDb.AllCharacterRelicPools
            .SelectMany(pool => pool.AllRelics)
            .ToList();
 
        foreach (var player in runState.Players)
        {
            foreach (var relic in characterRelics)
            {
                player.RelicGrabBag.Remove(relic);
            }
        }
 
        // To be safe, we also clear the class relics from the shared relic grab bag, since im not sure how the relic grab bags actually work
        foreach (var relic in characterRelics)
        {
            runState.SharedRelicGrabBag.Remove(relic);
        }
 
        Log.Info("[CustomModifiers] Colourless: Stripped character relics from all grab bags");
    }
    
    public override CardCreationOptions ModifyCardRewardCreationOptions(
        Player player, CardCreationOptions options)
    {
        if (options.Flags.HasFlag(CardCreationFlags.NoCardPoolModifications))
            return options;
 
        return options.WithCustomPool(GetColourlessCards(player));
    }
 
    public override CardCreationOptions ModifyCardRewardCreationOptionsLate(
        Player player, CardCreationOptions options)
    {
        if (options.Flags.HasFlag(CardCreationFlags.NoCardPoolModifications))
            return options;
 
        return options.WithCustomPool(GetColourlessCards(player));
    }
    
    public override IEnumerable<CardModel> ModifyMerchantCardPool(
        Player player, IEnumerable<CardModel> options)
    {
        return GetColourlessCards(player);
    }
 
    private static IEnumerable<CardModel> GetColourlessCards(Player player)
    {
        return ModelDb.CardPool<ColorlessCardPool>()
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint);
    }
}