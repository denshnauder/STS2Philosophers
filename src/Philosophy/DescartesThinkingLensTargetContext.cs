using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

// The native cost hook has no target argument. UI preview is synchronous;
// payment obtains its target from the native action, before CurrentTarget is set.
internal static class DescartesThinkingLensTargetContext
{
    [ThreadStatic] private static CardModel? _previewCard;
    [ThreadStatic] private static Creature? _previewTarget;

    internal sealed class PreviewScope : IDisposable
    {
        private readonly CardModel? _previousCard = _previewCard;
        private readonly Creature? _previousTarget = _previewTarget;
        public PreviewScope(CardModel card, Creature? target) { _previewCard = card; _previewTarget = target; }
        public void Dispose() { _previewCard = _previousCard; _previewTarget = _previousTarget; }
    }

    internal static Creature? Resolve(CardModel card)
    {
        if (card.TargetType != TargetType.AnyEnemy) return null;
        Creature? target;
        if (RunManager.Instance.ActionExecutor?.CurrentlyRunningAction is PlayCardAction action
            && action.Player == card.Owner && ReferenceEquals(action.NetCombatCard.ToCardModelOrNull(), card))
            target = action.Target;
        else if (ReferenceEquals(_previewCard, card)) target = _previewTarget;
        else if (card.CurrentTarget is { } playingTarget) target = playingTarget;
        else
        {
            var enemies = card.CombatState?.Enemies.Where(enemy => enemy.IsAlive && enemy.IsEnemy).ToArray();
            target = enemies is { Length: 1 } ? enemies[0] : null;
        }
        return target is { IsAlive: true, IsEnemy: true } ? target : null;
    }
}
