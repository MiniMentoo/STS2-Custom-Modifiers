using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace CustomModifiers;

public class SpecializedPlus : ModifierModel
{
    public override Func<Task> GenerateNeowOption(EventModel eventModel)
    {
        return () => ChooseAndAddCards(eventModel.Owner 
                   ?? throw new InvalidOperationException(
                       "[SpecializedPlus] GenerateNeowOption called with null Owner"));
    }

    private async Task ChooseAndAddCards(Player player)
    {
        var runState = player.RunState;
        CardModel chosenCanonical;

        if (RunManager.Instance.DailyTime.HasValue)
        {
            // Daily: derive a fresh Rng from the run seed so the result is
            // identical for all players without consuming shared RNG state.
            var dailyRng = new Rng(runState.Rng.Seed);
            chosenCanonical = dailyRng.NextItem(GetPickableCards())
              ?? throw new InvalidOperationException(
                  "[SpecializedPlus] No pickable cards found — GetPickableCards returned empty");
        }
        else
        {
            // Non-daily singleplayer or multiplayer: each player picks their own card.
            var prefs = new CardSelectorPrefs(
                new LocString("modifiers", "SPECIALIZED_PLUS.selectionPrompt"), 1)
            {
                Cancelable = false,
                RequireManualConfirmation = true
            };

            var selected = (await CardSelectCmd.FromSimpleGridForRewards(
                    new BlockingPlayerChoiceContext(),
                    BuildCardList(),
                    player,
                    prefs))
                .First();

            // GetById is defensive in case the picker returns a mutable copy
            chosenCanonical = ModelDb.GetById<CardModel>(selected.Id);
        }

        // Add 5 copies to this player's deck only
        var results = new List<CardPileAddResult>();
        for (var i = 0; i < 5; i++)
        {
            var copy = runState.CreateCard(chosenCanonical, player);
            results.Add(await CardPileCmd.Add(copy, PileType.Deck));
        }

        CardCmd.PreviewCardPileAdd(results);
        await Cmd.CustomScaledWait(0.6f, 1.2f);
    }

    private static List<CardModel> GetPickableCards()
    {
        return ModelDb.AllCards
            .Where(c => c.Type != CardType.Status && c.Type != CardType.Curse)
            .OrderBy(c => c.Id.Entry)
            .ToList();
    }

    private static List<CardCreationResult> BuildCardList()
    {
        return GetPickableCards()
            .Select(c => new CardCreationResult(c))
            .ToList();
    }
}