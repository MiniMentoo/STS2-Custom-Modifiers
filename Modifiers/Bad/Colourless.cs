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

    public override bool ClearsPlayerDeck => true;
    
    public override Func<Task> GenerateNeowOption(EventModel eventModel)
    {
        return () =>
        {
            return SetupColourless(eventModel.Owner);
        };
    }
 
    private static async Task SetupColourless(Player player)
    {
        var cardTypes = new CardModel[]
        {
            ModelDb.Card<StrikeIronclad>(),
            ModelDb.Card<DefendIronclad>(),
            ModelDb.Card<StrikeSilent>(),
            ModelDb.Card<DefendSilent>(),
            ModelDb.Card<StrikeRegent>(),
            ModelDb.Card<DefendRegent>(),
            ModelDb.Card<StrikeNecrobinder>(),
            ModelDb.Card<DefendNecrobinder>(),
            ModelDb.Card<StrikeDefect>(),
            ModelDb.Card<DefendDefect>(),
        };

        foreach (var canonicalCard in cardTypes)
        {
            var card = player.RunState.CreateCard(canonicalCard, player);
            player.Deck.AddInternal(card);
        }

        Log.Info("[CustomModifiers] Colourless: Added 10 starting cards");
        
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