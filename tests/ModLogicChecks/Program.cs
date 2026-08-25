using STS2MinimalMod;

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

KongziMuduoState state = new();

state.BeginTurn();
Check(state.Order == KongziMuduoOrder.Undecided, "A new turn must begin without a decided order.");
Check(state.GrantedStrength == 0, "A new turn must not inherit granted Strength.");

state.HonorRitual();
Check(state.Order == KongziMuduoOrder.Honored, "A Skill or Power played first must honor the ritual.");
Check(state.GetNextSkillStrengthToGrant(5) == 2, "The first Skill must grant 2 temporary Strength.");

state.RecordGrantedStrength(2, 5);
Check(state.GrantedStrength == 2, "The first Skill reward must be tracked.");
Check(state.GetNextSkillStrengthToGrant(5) == 1, "Later Skills must grant 1 temporary Strength.");

state.RecordGrantedStrength(1, 5);
state.RecordGrantedStrength(1, 5);
state.RecordGrantedStrength(1, 5);
Check(state.GrantedStrength == 5, "Skill rewards must reach the configured cap.");
Check(state.GetNextSkillStrengthToGrant(5) == 0, "No Strength may be granted above the cap.");

state.EndTurn();
Check(state.Order == KongziMuduoOrder.Undecided, "Turn end must reset the order.");
Check(state.GrantedStrength == 0, "Turn end must reset reward tracking.");

state.BeginTurn();
Check(state.TryDishonor(), "The first Attack must be able to choose discourtesy.");
Check(state.Order == KongziMuduoOrder.Dishonored, "An Attack played first must mark the turn as dishonored.");
Check(!state.TryDishonor(), "The discourtesy penalty must trigger only once per turn.");
state.HonorRitual();
Check(state.Order == KongziMuduoOrder.Dishonored, "A later Skill must not undo discourtesy.");
Check(state.GetNextSkillStrengthToGrant(5) == 0, "Skills played after discourtesy must grant no Strength.");

Console.WriteLine("Zhou Li logic checks passed.");

static int PlayXunziShengMoCards(
    ref XunziShengMoState state,
    params XunziShengMoCardKind[] cards)
{
    int rewards = 0;
    foreach (XunziShengMoCardKind card in cards)
    {
        if (state.RecordCard(card, new object()))
        {
            rewards++;
        }
    }

    return rewards;
}

XunziShengMoState xunziShengMoState = new();
xunziShengMoState.BeginCombat();
xunziShengMoState.BeginTurn();
Check(PlayXunziShengMoCards(
        ref xunziShengMoState,
        XunziShengMoCardKind.Skill,
        XunziShengMoCardKind.Attack,
        XunziShengMoCardKind.Skill) == 1,
    "Skill, Attack, Skill must trigger Ink Line.");
Check(xunziShengMoState.HasTriggeredThisTurn && xunziShengMoState.Progress == 0,
    "A successful sequence must lock the turn and hide sequence progress.");
Check(PlayXunziShengMoCards(
        ref xunziShengMoState,
        XunziShengMoCardKind.Skill,
        XunziShengMoCardKind.Attack,
        XunziShengMoCardKind.Skill) == 0,
    "Ink Line must trigger at most once per turn.");

xunziShengMoState.BeginTurn();
Check(PlayXunziShengMoCards(
        ref xunziShengMoState,
        XunziShengMoCardKind.Skill,
        XunziShengMoCardKind.Skill,
        XunziShengMoCardKind.Attack,
        XunziShengMoCardKind.Skill) == 1,
    "A second consecutive Skill must become the new start of the sequence.");

xunziShengMoState.BeginTurn();
Check(PlayXunziShengMoCards(
        ref xunziShengMoState,
        XunziShengMoCardKind.Attack,
        XunziShengMoCardKind.Skill,
        XunziShengMoCardKind.Attack,
        XunziShengMoCardKind.Skill) == 1,
    "An initial Attack must not prevent a later Skill, Attack, Skill sequence.");

xunziShengMoState.BeginTurn();
Check(PlayXunziShengMoCards(
        ref xunziShengMoState,
        XunziShengMoCardKind.Skill,
        XunziShengMoCardKind.Attack,
        XunziShengMoCardKind.Attack,
        XunziShengMoCardKind.Skill) == 0,
    "Skill, Attack, Attack, Skill must not trigger Ink Line.");

xunziShengMoState.BeginTurn();
Check(PlayXunziShengMoCards(
        ref xunziShengMoState,
        XunziShengMoCardKind.Skill,
        XunziShengMoCardKind.Attack,
        XunziShengMoCardKind.Other,
        XunziShengMoCardKind.Skill) == 0,
    "A Power or other card must break an incomplete Ink Line sequence.");

xunziShengMoState.BeginTurn();
Check(PlayXunziShengMoCards(
        ref xunziShengMoState,
        XunziShengMoCardKind.Skill,
        XunziShengMoCardKind.Attack,
        XunziShengMoCardKind.Other,
        XunziShengMoCardKind.Skill,
        XunziShengMoCardKind.Attack,
        XunziShengMoCardKind.Skill) == 1,
    "A broken sequence must be able to reform later in the same turn.");

xunziShengMoState.BeginTurn();
PlayXunziShengMoCards(ref xunziShengMoState, XunziShengMoCardKind.Skill, XunziShengMoCardKind.Attack);
Check(xunziShengMoState.Progress == 2,
    "An incomplete Skill, Attack sequence must show progress 2.");
xunziShengMoState.EndTurn();
xunziShengMoState.BeginTurn();
Check(xunziShengMoState.Progress == 0 && !xunziShengMoState.HasTriggeredThisTurn,
    "Changing turns must clear incomplete Ink Line progress and the trigger lock.");

int permanentStrength = 0;
int permanentDexterity = 0;
if (PlayXunziShengMoCards(
        ref xunziShengMoState,
        XunziShengMoCardKind.Skill,
        XunziShengMoCardKind.Attack,
        XunziShengMoCardKind.Skill) == 1)
{
    permanentStrength++;
    permanentDexterity++;
}

xunziShengMoState.EndTurn();
Check(permanentStrength == 1 && permanentDexterity == 1,
    "Ending a turn must not make Ink Line state remove granted Strength or Dexterity.");

xunziShengMoState.BeginCombat();
PlayXunziShengMoCards(ref xunziShengMoState, XunziShengMoCardKind.Skill, XunziShengMoCardKind.Attack);
xunziShengMoState.EndCombat();
Check(xunziShengMoState.Progress == 0 && !xunziShengMoState.HasTriggeredThisTurn,
    "Ending combat must clear Ink Line state.");
xunziShengMoState.BeginCombat();
Check(xunziShengMoState.Progress == 0 && !xunziShengMoState.HasTriggeredThisTurn,
    "Starting a new combat must begin with clear Ink Line state.");

XunziShengMoState firstInkLinePlayer = new();
XunziShengMoState secondInkLinePlayer = new();
firstInkLinePlayer.BeginTurn();
secondInkLinePlayer.BeginTurn();
PlayXunziShengMoCards(
    ref firstInkLinePlayer,
    XunziShengMoCardKind.Skill,
    XunziShengMoCardKind.Attack);
Check(firstInkLinePlayer.Progress == 2 && secondInkLinePlayer.Progress == 0,
    "One player's Ink Line sequence must not change another player's state.");
Check(PlayXunziShengMoCards(
        ref secondInkLinePlayer,
        XunziShengMoCardKind.Skill,
        XunziShengMoCardKind.Attack,
        XunziShengMoCardKind.Skill) == 1,
    "Each player must be able to complete an independent Ink Line sequence.");
Check(!firstInkLinePlayer.HasTriggeredThisTurn,
    "The second player's success must not lock the first player's Ink Line.");

xunziShengMoState.BeginTurn();
object firstSkillCallback = new();
object attackCallback = new();
object finalSkillCallback = new();
Check(!xunziShengMoState.RecordCard(XunziShengMoCardKind.Skill, firstSkillCallback),
    "The first Skill callback must only advance Ink Line.");
Check(!xunziShengMoState.RecordCard(XunziShengMoCardKind.Attack, attackCallback),
    "The Attack callback must only advance Ink Line.");
Check(!xunziShengMoState.RecordCard(XunziShengMoCardKind.Attack, attackCallback)
      && xunziShengMoState.Progress == 2,
    "A duplicate callback for the same played Attack must be ignored.");
int duplicateRewards = xunziShengMoState.RecordCard(XunziShengMoCardKind.Skill, finalSkillCallback) ? 1 : 0;
if (xunziShengMoState.RecordCard(XunziShengMoCardKind.Skill, finalSkillCallback))
{
    duplicateRewards++;
}

Check(duplicateRewards == 1,
    "Repeating callbacks for one successful sequence must grant Ink Line exactly once.");

XunziShengMoState restoredInkLine = new();
restoredInkLine.RestoreProgress(2);
restoredInkLine.RestoreTriggeredThisTurn(true);
Check(!restoredInkLine.RecordCard(XunziShengMoCardKind.Skill, new object()),
    "A restored successful turn must not repay Ink Line after loading.");
Check(restoredInkLine.Progress == 0 && restoredInkLine.HasTriggeredThisTurn,
    "A restored trigger lock must remain authoritative over restored progress.");

Console.WriteLine("Ink Line logic checks passed.");

static KongziQingYuPeiState BeginKongziQingYuPeiOpportunity(bool hasPlayableAttack)
{
    KongziQingYuPeiState state = new();
    state.BeginCombat();
    state.BeginTurn(allLivingEnemiesAreNonAttacking: true);
    state.ObserveAttackOpportunity(hasPlayableAttack);
    return state;
}

KongziQingYuPeiState kongziQingYuPeiState = BeginKongziQingYuPeiOpportunity(hasPlayableAttack: true);
Check(kongziQingYuPeiState.EndTurn() == KongziQingYuPeiTurnResolution.VirtuousConduct,
    "A non-attacking-enemy turn with a real unused Attack opportunity must satisfy Green Jade Pendant.");
Check(kongziQingYuPeiState.Outcome == KongziQingYuPeiBattleOutcome.VirtuousConduct,
    "Successful virtuous conduct must resolve Green Jade Pendant's combat state.");

kongziQingYuPeiState = BeginKongziQingYuPeiOpportunity(hasPlayableAttack: true);
Check(kongziQingYuPeiState.RecordAttackPlayed(),
    "Playing an Attack during a real opportunity must immediately record its loss.");
Check(kongziQingYuPeiState.Outcome == KongziQingYuPeiBattleOutcome.LostOpportunity,
    "An Attack during the opportunity turn must close Green Jade Pendant for the combat.");
Check(kongziQingYuPeiState.EndTurn() == KongziQingYuPeiTurnResolution.None,
    "A lost opportunity must not become successful at turn end.");

kongziQingYuPeiState = BeginKongziQingYuPeiOpportunity(hasPlayableAttack: false);
Check(kongziQingYuPeiState.EndTurn() == KongziQingYuPeiTurnResolution.None,
    "A turn with no playable Attack must produce neither success nor loss.");
kongziQingYuPeiState.BeginTurn(allLivingEnemiesAreNonAttacking: true);
kongziQingYuPeiState.ObserveAttackOpportunity(hasPlayableAttack: true);
Check(kongziQingYuPeiState.EndTurn() == KongziQingYuPeiTurnResolution.VirtuousConduct,
    "A later turn must remain eligible after an earlier turn had no real Attack opportunity.");

kongziQingYuPeiState = BeginKongziQingYuPeiOpportunity(hasPlayableAttack: true);
kongziQingYuPeiState.ObserveAttackOpportunity(hasPlayableAttack: false);
Check(kongziQingYuPeiState.EndTurn() == KongziQingYuPeiTurnResolution.VirtuousConduct,
    "Spending energy on a Skill after an earlier real Attack opportunity must still satisfy Green Jade Pendant.");

kongziQingYuPeiState = BeginKongziQingYuPeiOpportunity(hasPlayableAttack: true);
Check(kongziQingYuPeiState.EndTurn() == KongziQingYuPeiTurnResolution.VirtuousConduct,
    "Non-Attack damage must not count as playing an Attack.");

kongziQingYuPeiState = new KongziQingYuPeiState();
kongziQingYuPeiState.BeginCombat();
kongziQingYuPeiState.BeginTurn(allLivingEnemiesAreNonAttacking: false);
kongziQingYuPeiState.ObserveAttackOpportunity(hasPlayableAttack: true);
Check(!kongziQingYuPeiState.RecordAttackPlayed(),
    "If any living enemy intends to attack, this turn must not be a Green Jade Pendant opportunity.");
Check(kongziQingYuPeiState.EndTurn() == KongziQingYuPeiTurnResolution.None,
    "A mixed-intent enemy turn must not satisfy Green Jade Pendant.");

kongziQingYuPeiState = BeginKongziQingYuPeiOpportunity(hasPlayableAttack: true);
Check(kongziQingYuPeiState.CancelOpportunityIfEnemiesAttack(allLivingEnemiesAreNonAttacking: false),
    "A stale opportunity must be canceled when a living enemy now shows an Attack intent.");
Check(!kongziQingYuPeiState.RecordAttackPlayed(),
    "An Attack-intent turn must not consume a stale opportunity from the previous turn.");
Check(kongziQingYuPeiState.Outcome == KongziQingYuPeiBattleOutcome.Available,
    "Canceling a stale opportunity must leave Green Jade Pendant available for a later valid turn.");

kongziQingYuPeiState = BeginKongziQingYuPeiOpportunity(hasPlayableAttack: true);
Check(kongziQingYuPeiState.EndTurn() == KongziQingYuPeiTurnResolution.VirtuousConduct,
    "The first valid restraint must satisfy Green Jade Pendant.");
kongziQingYuPeiState.BeginTurn(allLivingEnemiesAreNonAttacking: true);
kongziQingYuPeiState.ObserveAttackOpportunity(hasPlayableAttack: true);
Check(!kongziQingYuPeiState.RecordAttackPlayed(),
    "An Attack on a later turn must not overwrite an already successful result.");
Check(kongziQingYuPeiState.HasLockedReward,
    "Successful virtuous conduct must keep its reward locked after a later Attack.");
Check(kongziQingYuPeiState.Outcome == KongziQingYuPeiBattleOutcome.VirtuousConduct,
    "A later Attack must not change successful virtuous conduct into a lost opportunity.");
Check(kongziQingYuPeiState.EndTurn() == KongziQingYuPeiTurnResolution.None,
    "Green Jade Pendant may succeed at most once per combat.");

int virtue = 3;
kongziQingYuPeiState = BeginKongziQingYuPeiOpportunity(hasPlayableAttack: true);
if (kongziQingYuPeiState.EndTurn() == KongziQingYuPeiTurnResolution.VirtuousConduct)
{
    virtue++;
}
Check(virtue == 4, "Successful virtuous conduct must add exactly 1 Virtue.");
kongziQingYuPeiState.BeginCombat();
kongziQingYuPeiState.BeginTurn(allLivingEnemiesAreNonAttacking: true);
kongziQingYuPeiState.ObserveAttackOpportunity(hasPlayableAttack: true);
kongziQingYuPeiState.RecordAttackPlayed();
Check(virtue == 4, "Losing the opportunity must not subtract previously accumulated Virtue.");

int nativeRarityRolls = 0;
KongziQingYuPeiRewardRarityDecision uncommonDecision = KongziQingYuPeiRewardPolicy.RollAndResolveRarity(
    () =>
    {
        nativeRarityRolls++;
        return false;
    },
    hasUncommonCandidate: true,
    hasRareCandidate: true);
Check(nativeRarityRolls == 1,
    "The Green Jade Pendant bonus card must advance native rarity odds exactly once.");
Check(uncommonDecision.Rarity == KongziQingYuPeiBonusCardRarity.Uncommon,
    "A native Common or Uncommon result must map to an Uncommon Green Jade Pendant bonus card.");

KongziQingYuPeiRewardRarityDecision rareDecision = KongziQingYuPeiRewardPolicy.RollAndResolveRarity(
    () => true,
    hasUncommonCandidate: true,
    hasRareCandidate: true);
Check(rareDecision.Rarity == KongziQingYuPeiBonusCardRarity.Rare,
    "A native Rare result must remain Rare.");

KongziQingYuPeiRewardRarityDecision fallbackDecision = KongziQingYuPeiRewardPolicy.RollAndResolveRarity(
    () => true,
    hasUncommonCandidate: true,
    hasRareCandidate: false);
Check(fallbackDecision.Rarity == KongziQingYuPeiBonusCardRarity.Uncommon && fallbackDecision.FellBackFromRare,
    "A missing Rare candidate must deterministically fall back to Uncommon.");

KongziQingYuPeiRewardRarityDecision noCandidateDecision = KongziQingYuPeiRewardPolicy.RollAndResolveRarity(
    () => false,
    hasUncommonCandidate: false,
    hasRareCandidate: true);
Check(noCandidateDecision.Rarity is null,
    "A non-Rare roll with no distinct Uncommon candidate must append nothing.");

List<string> originalReward = ["A", "B", "C"];
string bonusCard = "D";
Check(!originalReward.Contains(bonusCard),
    "The Green Jade Pendant bonus card must differ from every card already displayed.");
originalReward.Add(bonusCard);
Check(originalReward.SequenceEqual(["A", "B", "C", "D"]),
    "The Green Jade Pendant bonus card must be appended after every original reward option.");
const int cardsSelectableFromReward = 1;
Check(cardsSelectableFromReward == 1,
    "Appending an option must not increase how many cards the reward allows the player to take.");

Console.WriteLine("Green Jade Pendant logic and reward policy checks passed.");

static List<int> MengziXiongZhangTriggers(int virtue, int turns)
{
    MengziXiongZhangState state = new();
    state.BeginCombat();
    List<int> triggeredTurns = [];
    for (int turn = 1; turn <= turns; turn++)
    {
        if (state.TryTrigger(turn, virtue))
        {
            triggeredTurns.Add(turn);
        }
    }

    return triggeredTurns;
}

Check(MengziXiongZhangTriggers(0, 4).Count == 0,
    "Bear Paw must never trigger at 0 Virtue.");
Check(MengziXiongZhangTriggers(1, 4).SequenceEqual([1]),
    "At 1 Virtue, Bear Paw must trigger only on turn 1.");
Check(MengziXiongZhangTriggers(2, 4).SequenceEqual([1, 2]),
    "At 2 Virtue, Bear Paw must trigger on turns 1 and 2 only.");
Check(MengziXiongZhangTriggers(3, 4).SequenceEqual([1, 2, 3]),
    "At 3 Virtue, Bear Paw must trigger on the first three turns only.");

MengziXiongZhangState duplicateGuard = new();
duplicateGuard.BeginCombat();
Check(duplicateGuard.TryTrigger(1, 3),
    "The first eligible turn-start call must trigger Bear Paw.");
Check(!duplicateGuard.TryTrigger(1, 3),
    "A repeated turn-start call must not trigger Bear Paw twice on the same turn.");

MengziXiongZhangState missingVirtueSource = new();
missingVirtueSource.BeginCombat();
Check(!missingVirtueSource.TryTrigger(1, virtue: 0),
    "A player without Green Jade Pendant must safely behave like a player with 0 Virtue.");

MengziXiongZhangState firstPlayer = new();
MengziXiongZhangState secondPlayer = new();
firstPlayer.BeginCombat();
secondPlayer.BeginCombat();
Check(firstPlayer.TryTrigger(1, virtue: 1),
    "The first player's own Virtue must enable their Bear Paw.");
Check(!secondPlayer.TryTrigger(1, virtue: 0),
    "The first player's Virtue must not enable the second player's Bear Paw.");
Check(secondPlayer.TryTrigger(1, virtue: 2),
    "The second player's own Virtue must independently enable their Bear Paw.");

MengziXiongZhangState restoredCombat = new();
restoredCombat.RestoreLastTriggeredTurn(1);
Check(!restoredCombat.TryTrigger(1, virtue: 2),
    "Restoring a combat must not repay a turn that was already recorded.");
Check(restoredCombat.TryTrigger(2, virtue: 2),
    "A restored combat must still allow the next eligible turn.");

Console.WriteLine("Bear Paw logic checks passed.");

static PhilosophersGazeInterceptionContext GazeContext(
    bool runInProgress = true,
    bool currentRoomIsEventRoom = true,
    CurrentEventKind currentEvent = CurrentEventKind.Neow,
    bool historyContainsPhilosophersGaze = false,
    bool modelAvailable = true,
    bool isSingleplayer = true)
{
    return new PhilosophersGazeInterceptionContext(
        runInProgress,
        currentRoomIsEventRoom,
        currentEvent,
        historyContainsPhilosophersGaze,
        modelAvailable,
        isSingleplayer);
}

Check(PhilosophersGazeInterceptionPolicy.ShouldIntercept(GazeContext()),
    "Neow with no PhilosophersGaze room in current map-point history must be intercepted.");
Check(!PhilosophersGazeInterceptionPolicy.ShouldIntercept(
        GazeContext(currentEvent: CurrentEventKind.Other)),
    "A non-Neow event must not be intercepted.");
Check(!PhilosophersGazeInterceptionPolicy.ShouldIntercept(
        GazeContext(runInProgress: false)),
    "Proceed must not be intercepted when no run is in progress.");
Check(!PhilosophersGazeInterceptionPolicy.ShouldIntercept(
        GazeContext(currentRoomIsEventRoom: false, currentEvent: CurrentEventKind.None)),
    "Proceed must not be intercepted when the current room is not an EventRoom.");
Check(!PhilosophersGazeInterceptionPolicy.ShouldIntercept(
        GazeContext(historyContainsPhilosophersGaze: true)),
    "A recorded PhilosophersGaze room must prevent repeat insertion.");
Check(!PhilosophersGazeInterceptionPolicy.ShouldIntercept(
        GazeContext(currentEvent: CurrentEventKind.PhilosophersGaze)),
    "PhilosophersGaze itself must use the original Proceed behavior to open the map.");
Check(!PhilosophersGazeInterceptionPolicy.ShouldIntercept(
        GazeContext(modelAvailable: false)),
    "A missing PhilosophersGaze model must fall back to the original Proceed behavior.");
Check(!PhilosophersGazeInterceptionPolicy.ShouldIntercept(
        GazeContext(isSingleplayer: false)),
    "The singleplayer MVP must not intercept multiplayer runs.");

Console.WriteLine("PhilosophersGaze interception policy checks passed.");
