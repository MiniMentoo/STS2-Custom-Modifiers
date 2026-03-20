using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace CustomModifiers;


public class VintagePlus : ModifierModel
{
    public override bool TryModifyRewardsLate(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (room is not CombatRoom combatRoom)
            return false;

        if (combatRoom.Encounter.RoomType != RoomType.Monster)
            return false;
        
        for (int i = rewards.Count - 1; i >= 0; i--)
        {
            if (rewards[i] is CardReward)
            {
                rewards.RemoveAt(i);
                // Insert two relics at the same position
                rewards.Insert(i, new RelicReward(player));
                rewards.Insert(i, new RelicReward(player));
            }
        }

        return true;
    }
}