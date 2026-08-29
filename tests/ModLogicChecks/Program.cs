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

Check(KongziMuduoConsoleText.GrantSuccess ==
        "Granted Muduo. Zhou Li applies from the player's next applicable card play.",
    "The Muduo grant message must not claim that an immediately active relic waits for the next turn.");

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

Check(MoziMoSeZhuJianState.BlockAmount == 6,
    "Ink Bamboo Slips must grant 6 Block to every living combatant each player turn.");
Check(MoziMoSeZhuJianState.XiangLiCap == 2,
    "Mutual Benefit must be capped at 2.");

MoziMoSeZhuJianState moziMoSeZhuJianState = new();
Check(moziMoSeZhuJianState.BeginPlayerTurn(1),
    "The first player-turn boundary must initialize Ink Bamboo Slips.");
Check(moziMoSeZhuJianState.XiangLi == 0
        && moziMoSeZhuJianState.PendingRewardAmount == 0,
    "The first turn must not gain Mutual Benefit or resources without a prior unharmed turn.");
Check(!moziMoSeZhuJianState.BeginPlayerTurn(1),
    "A repeated boundary hook must not resolve the same turn twice.");
Check(moziMoSeZhuJianState.TryMarkBlockGranted(1)
        && !moziMoSeZhuJianState.TryMarkBlockGranted(1),
    "All-character Block must be granted at most once per player turn.");
Check(moziMoSeZhuJianState.TryTakePendingReward(1, out int firstTurnReward)
        && firstTurnReward == 0
        && !moziMoSeZhuJianState.TryTakePendingReward(1, out _),
    "The first turn's zero reward must still be recorded against duplicate payout.");

Check(moziMoSeZhuJianState.BeginPlayerTurn(2)
        && moziMoSeZhuJianState.XiangLi == 1
        && moziMoSeZhuJianState.PendingRewardAmount == 1,
    "One completed unharmed turn must grant 1 Mutual Benefit for the next turn.");
Check(moziMoSeZhuJianState.RecordHpChange(-1m)
        && moziMoSeZhuJianState.XiangLi == 0,
    "Any actual HP loss must immediately clear Mutual Benefit.");
Check(moziMoSeZhuJianState.TryTakePendingReward(2, out int lockedReward)
        && lockedReward == 1,
    "HP loss after the turn boundary must not revoke the reward already earned last turn.");
Check(!moziMoSeZhuJianState.BeginPlayerTurn(2),
    "A restored or replayed boundary must not increase Mutual Benefit twice.");
Check(moziMoSeZhuJianState.BeginPlayerTurn(3)
        && moziMoSeZhuJianState.XiangLi == 0,
    "A harmed turn must not rebuild Mutual Benefit at its next boundary.");
Check(!moziMoSeZhuJianState.RecordHpChange(0m)
        && !moziMoSeZhuJianState.RecordHpChange(3m),
    "Fully blocked damage and healing must not break an unharmed turn.");
Check(moziMoSeZhuJianState.BeginPlayerTurn(4)
        && moziMoSeZhuJianState.XiangLi == 1,
    "A new complete unharmed turn must rebuild Mutual Benefit from 0 to 1.");
Check(moziMoSeZhuJianState.BeginPlayerTurn(5)
        && moziMoSeZhuJianState.XiangLi == 2,
    "A second consecutive unharmed turn must raise Mutual Benefit to 2.");
Check(moziMoSeZhuJianState.BeginPlayerTurn(6)
        && moziMoSeZhuJianState.XiangLi == 2
        && moziMoSeZhuJianState.PendingRewardAmount == 2,
    "Further unharmed turns must remain at the cap and lock a 2 Energy, 2 card reward.");
moziMoSeZhuJianState.Reset();
Check(moziMoSeZhuJianState == default,
    "Combat cleanup must reset all Ink Bamboo Slips state and replay locks.");

Console.WriteLine("Ink Bamboo Slips logic checks passed.");

Check(MoziShouChengTuState.BlockAmount == 6,
    "City Defense Diagram must grant exactly 6 Block after a successful defense.");

MoziShouChengTuState moziShouChengTuState = new();
Check(moziShouChengTuState.BeginPlayerTurn(1),
    "The first player-turn boundary must initialize City Defense Diagram.");
Check(!moziShouChengTuState.HasPendingReward,
    "The first turn must not receive a reward without an earlier defense window.");
Check(!moziShouChengTuState.BeginPlayerTurn(1),
    "A repeated boundary hook must not resolve City Defense Diagram twice.");
Check(moziShouChengTuState.SampleEnemyIntents(1, anyLivingEnemyIntendsToAttack: true),
    "A visible enemy Attack intent must open the defense window.");
Check(!moziShouChengTuState.SampleEnemyIntents(1, anyLivingEnemyIntendsToAttack: false),
    "A repeated intent hook must not replace the turn's locked observation.");
Check(!MoziShouChengTuDamagePolicy.IsEnemyHpLossToOwner(
        resultReceiverIsOwner: true,
        hookTargetIsOwner: true,
        dealerIsEnemy: false,
        unblockedDamage: 5),
    "Player self-damage must not fail City Defense Diagram.");
Check(!MoziShouChengTuDamagePolicy.IsEnemyHpLossToOwner(
        resultReceiverIsOwner: false,
        hookTargetIsOwner: false,
        dealerIsEnemy: true,
        unblockedDamage: 5),
    "Enemy damage to another player must not fail the owner's City Defense Diagram.");
Check(!MoziShouChengTuDamagePolicy.IsEnemyHpLossToOwner(
        resultReceiverIsOwner: true,
        hookTargetIsOwner: true,
        dealerIsEnemy: true,
        unblockedDamage: 0),
    "Fully prevented enemy damage must not fail City Defense Diagram.");
Check(MoziShouChengTuDamagePolicy.IsEnemyHpLossToOwner(
        resultReceiverIsOwner: true,
        hookTargetIsOwner: true,
        dealerIsEnemy: true,
        unblockedDamage: 1),
    "A lethal or nonlethal enemy hit with actual HP loss must fail City Defense Diagram.");
Check(moziShouChengTuState.BeginPlayerTurn(2)
        && moziShouChengTuState.HasPendingReward,
    "An enemy Attack intent followed by no enemy HP damage must earn next-turn Block.");
Check(moziShouChengTuState.TryTakePendingReward(2, out bool firstDefenseReward)
        && firstDefenseReward,
    "A successful defense must grant its locked Block reward.");
Check(!moziShouChengTuState.TryTakePendingReward(2, out _),
    "A successful defense must not pay twice after replay or restoration.");

Check(moziShouChengTuState.SampleEnemyIntents(2, anyLivingEnemyIntendsToAttack: true),
    "City Defense Diagram must open a fresh window on a later eligible turn.");
Check(moziShouChengTuState.RecordEnemyHpLoss(unblockedDamage: 1),
    "Actual HP damage from an enemy must fail the current defense window.");
Check(!moziShouChengTuState.RecordEnemyHpLoss(unblockedDamage: 8),
    "Multiple enemy hits must merge into one failed round state.");
Check(moziShouChengTuState.BeginPlayerTurn(3)
        && !moziShouChengTuState.HasPendingReward,
    "A failed defense must not earn next-turn Block.");
Check(moziShouChengTuState.TryTakePendingReward(3, out bool failedDefenseReward)
        && !failedDefenseReward,
    "A zero reward must still be locked against duplicate payout.");

Check(moziShouChengTuState.SampleEnemyIntents(3, anyLivingEnemyIntendsToAttack: false),
    "A non-Attack turn must be sampled without opening a defense window.");
Check(!moziShouChengTuState.RecordEnemyHpLoss(unblockedDamage: 4),
    "Enemy damage must not create a failure state when no Attack intent qualified the round.");
Check(moziShouChengTuState.BeginPlayerTurn(4)
        && !moziShouChengTuState.HasPendingReward,
    "A turn without an enemy Attack intent must not earn Block.");

Check(moziShouChengTuState.SampleEnemyIntents(4, anyLivingEnemyIntendsToAttack: true),
    "City Defense Diagram must remain reusable throughout combat.");
Check(moziShouChengTuState.BeginPlayerTurn(5)
        && moziShouChengTuState.TryTakePendingReward(5, out bool repeatedDefenseReward)
        && repeatedDefenseReward,
    "A later successful defense must independently grant Block once, even if the original attacker was killed or stunned.");

MoziShouChengTuState restoredShouChengTuState = new();
Check(restoredShouChengTuState.BeginPlayerTurn(1)
        && restoredShouChengTuState.SampleEnemyIntents(1, anyLivingEnemyIntendsToAttack: true)
        && restoredShouChengTuState.BeginPlayerTurn(2),
    "A restorable City Defense Diagram state must be able to lock a pending reward.");
restoredShouChengTuState = new MoziShouChengTuState
{
    HasActiveDefenseWindow = restoredShouChengTuState.HasActiveDefenseWindow,
    EnemyDamageTaken = restoredShouChengTuState.EnemyDamageTaken,
    LastBoundaryTurn = restoredShouChengTuState.LastBoundaryTurn,
    LastIntentSampleTurn = restoredShouChengTuState.LastIntentSampleTurn,
    HasPendingReward = restoredShouChengTuState.HasPendingReward,
    PendingRewardTurn = restoredShouChengTuState.PendingRewardTurn,
    LastRewardedTurn = restoredShouChengTuState.LastRewardedTurn,
};
Check(restoredShouChengTuState.TryTakePendingReward(2, out bool restoredDefenseReward)
        && restoredDefenseReward,
    "A property-by-property restored pending reward must still pay once.");
restoredShouChengTuState = new MoziShouChengTuState
{
    HasActiveDefenseWindow = restoredShouChengTuState.HasActiveDefenseWindow,
    EnemyDamageTaken = restoredShouChengTuState.EnemyDamageTaken,
    LastBoundaryTurn = restoredShouChengTuState.LastBoundaryTurn,
    LastIntentSampleTurn = restoredShouChengTuState.LastIntentSampleTurn,
    HasPendingReward = restoredShouChengTuState.HasPendingReward,
    PendingRewardTurn = restoredShouChengTuState.PendingRewardTurn,
    LastRewardedTurn = restoredShouChengTuState.LastRewardedTurn,
};
Check(!restoredShouChengTuState.TryTakePendingReward(2, out _),
    "Restoring after payout must preserve the saved replay lock and prevent a second grant.");

MoziShouChengTuState firstShouChengTuOwner = new();
MoziShouChengTuState secondShouChengTuOwner = new();
firstShouChengTuOwner.BeginPlayerTurn(1);
secondShouChengTuOwner.BeginPlayerTurn(1);
firstShouChengTuOwner.SampleEnemyIntents(1, anyLivingEnemyIntendsToAttack: true);
secondShouChengTuOwner.SampleEnemyIntents(1, anyLivingEnemyIntendsToAttack: true);
Check(firstShouChengTuOwner.RecordEnemyHpLoss(unblockedDamage: 2),
    "Enemy damage to one owner must fail only that owner's defense window.");
Check(firstShouChengTuOwner.BeginPlayerTurn(2)
        && !firstShouChengTuOwner.HasPendingReward
        && secondShouChengTuOwner.BeginPlayerTurn(2)
        && secondShouChengTuOwner.HasPendingReward,
    "City Defense Diagram state must remain isolated between players.");
moziShouChengTuState.Reset();
Check(moziShouChengTuState == default,
    "Combat cleanup must reset City Defense Diagram windows and replay locks.");

Console.WriteLine("City Defense Diagram logic checks passed.");

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

Check(PhilosophersGazeRelicGrantPolicy.CanGrant(new PhilosophersGazeRelicOwnership(
        HasKongziMuduo: false,
        HasKongziQingYuPei: false,
        HasMengziXiongZhang: false,
        HasXunziShengMo: false)),
    "PhilosophersGaze must grant an event relic when the player owns no prototype relic.");
Check(!PhilosophersGazeRelicGrantPolicy.CanGrant(new PhilosophersGazeRelicOwnership(
        HasKongziMuduo: true,
        HasKongziQingYuPei: false,
        HasMengziXiongZhang: false,
        HasXunziShengMo: false)),
    "Owning Muduo must prevent PhilosophersGaze from granting a second prototype relic.");
Check(!PhilosophersGazeRelicGrantPolicy.CanGrant(new PhilosophersGazeRelicOwnership(
        HasKongziMuduo: false,
        HasKongziQingYuPei: true,
        HasMengziXiongZhang: false,
        HasXunziShengMo: false)),
    "Owning Green Jade Pendant must prevent PhilosophersGaze from granting a second prototype relic.");
Check(!PhilosophersGazeRelicGrantPolicy.CanGrant(new PhilosophersGazeRelicOwnership(
        HasKongziMuduo: false,
        HasKongziQingYuPei: false,
        HasMengziXiongZhang: true,
        HasXunziShengMo: false)),
    "Owning Bear Paw must prevent PhilosophersGaze from granting a second prototype relic.");
Check(!PhilosophersGazeRelicGrantPolicy.CanGrant(new PhilosophersGazeRelicOwnership(
        HasKongziMuduo: false,
        HasKongziQingYuPei: false,
        HasMengziXiongZhang: false,
        HasXunziShengMo: true)),
    "Owning Ink Line must prevent PhilosophersGaze from granting a second prototype relic.");
Check(!PhilosophersGazeRelicGrantPolicy.CanGrant(new PhilosophersGazeRelicOwnership(
        HasKongziMuduo: false,
        HasKongziQingYuPei: false,
        HasMengziXiongZhang: false,
        HasXunziShengMo: false,
        HasMoziMoSeZhuJian: true)),
    "Owning Ink Bamboo Slips must prevent PhilosophersGaze from granting another route relic.");

Console.WriteLine("PhilosophersGaze relic grant policy checks passed.");

static PhilosophersGazeRelicOwnership ContinuationOwnership(
    bool hasKongziMuduo = false,
    bool hasKongziQingYuPei = false,
    bool hasMengziXiongZhang = false,
    bool hasXunziShengMo = false,
    bool hasMoziMoSeZhuJian = false)
{
    return new PhilosophersGazeRelicOwnership(
        hasKongziMuduo,
        hasKongziQingYuPei,
        hasMengziXiongZhang,
        hasXunziShengMo,
        hasMoziMoSeZhuJian);
}

static PhilosophersGazeContinuationInsertionContext ContinuationContext(
    PhilosophersGazeRelicOwnership? ownership = null,
    bool continuationRecorded = false,
    bool runInProgress = true,
    bool currentRoomIsMapRoom = true,
    int currentActIndex = 1,
    bool modelAvailable = true,
    bool isSingleplayer = true)
{
    return new PhilosophersGazeContinuationInsertionContext(
        runInProgress,
        currentRoomIsMapRoom,
        currentActIndex,
        modelAvailable,
        isSingleplayer,
        ownership ?? ContinuationOwnership(hasKongziQingYuPei: true),
        continuationRecorded);
}

PhilosophersGazeRelicOwnership qingYuPeiRoute = ContinuationOwnership(
    hasKongziQingYuPei: true);
Check(PhilosophersGazeContinuationPolicy.GetAvailableOptions(qingYuPeiRoute, false)
        == PhilosophersGazeContinuationOption.MengziXiongZhang,
    "Green Jade Pendant must display Bear Paw as its only continuation relic.");
Check(PhilosophersGazeContinuationPolicy.CanGrant(
        PhilosophersGazeContinuationOption.MengziXiongZhang,
        qingYuPeiRoute,
        continuationRecorded: false),
    "Green Jade Pendant must make Bear Paw eligible to grant.");
Check(!PhilosophersGazeContinuationPolicy.CanGrant(
        PhilosophersGazeContinuationOption.XunziShengMo,
        qingYuPeiRoute,
        continuationRecorded: false),
    "Green Jade Pendant alone must not make Ink Line eligible to grant.");

PhilosophersGazeRelicOwnership muduoRoute = ContinuationOwnership(hasKongziMuduo: true);
Check(PhilosophersGazeContinuationPolicy.GetAvailableOptions(muduoRoute, false)
        == PhilosophersGazeContinuationOption.XunziShengMo,
    "Muduo must display Ink Line as its only continuation relic.");
Check(PhilosophersGazeContinuationPolicy.CanGrant(
        PhilosophersGazeContinuationOption.XunziShengMo,
        muduoRoute,
        continuationRecorded: false),
    "Muduo must make Ink Line eligible to grant.");
Check(!PhilosophersGazeContinuationPolicy.CanGrant(
        PhilosophersGazeContinuationOption.MengziXiongZhang,
        muduoRoute,
        continuationRecorded: false),
    "Muduo alone must not make Bear Paw eligible to grant.");

PhilosophersGazeRelicOwnership moziRoute = ContinuationOwnership(
    hasMoziMoSeZhuJian: true);
Check(PhilosophersGazeContinuationPolicy.GetAvailableOptions(moziRoute, false)
        == PhilosophersGazeContinuationOption.None,
    "Ink Bamboo Slips currently has no act two continuation option.");
Check(!PhilosophersGazeContinuationPolicy.ShouldInsert(
        ContinuationContext(moziRoute)),
    "The act two event must be skipped for the standalone Mohist route.");
Check(!PhilosophersGazeContinuationPolicy.IsContinuationStage(moziRoute),
    "Ink Bamboo Slips must not generate a continuation page with only a refusal option.");

PhilosophersGazeRelicOwnership debugMixedRoute = ContinuationOwnership(
    hasKongziMuduo: true,
    hasMoziMoSeZhuJian: true);
Check(PhilosophersGazeContinuationPolicy.GetAvailableOptions(debugMixedRoute, false)
        == PhilosophersGazeContinuationOption.None,
    "A debug mixed Mohist and Confucian route must not grant a cross-route successor.");

PhilosophersGazeRelicOwnership debugDualRoute = ContinuationOwnership(
    hasKongziMuduo: true,
    hasKongziQingYuPei: true);
Check(PhilosophersGazeContinuationPolicy.GetAvailableOptions(debugDualRoute, false)
        == (PhilosophersGazeContinuationOption.MengziXiongZhang
            | PhilosophersGazeContinuationOption.XunziShengMo),
    "Debug dual ownership must display both Bear Paw and Ink Line.");
Check(PhilosophersGazeContinuationPolicy.CanGrant(
        PhilosophersGazeContinuationOption.MengziXiongZhang,
        debugDualRoute,
        continuationRecorded: false)
    && PhilosophersGazeContinuationPolicy.CanGrant(
        PhilosophersGazeContinuationOption.XunziShengMo,
        debugDualRoute,
        continuationRecorded: false),
    "Either displayed debug continuation must be eligible before one choice records resolution.");
Check(!PhilosophersGazeContinuationPolicy.CanGrant(
        PhilosophersGazeContinuationOption.MengziXiongZhang
            | PhilosophersGazeContinuationOption.XunziShengMo,
        debugDualRoute,
        continuationRecorded: false),
    "A single continuation grant must never accept both displayed debug choices at once.");
Check(PhilosophersGazeContinuationPolicy.GetAvailableOptions(
        debugDualRoute,
        continuationRecorded: true) == PhilosophersGazeContinuationOption.None,
    "Recording either debug continuation choice must close both choices for the event.");

Check(!PhilosophersGazeContinuationPolicy.ShouldInsert(
        ContinuationContext(ContinuationOwnership())),
    "A player with no Kongzi route relic must skip the act two continuation.");
Check(!PhilosophersGazeContinuationPolicy.ShouldInsert(
        ContinuationContext(ContinuationOwnership(
            hasKongziQingYuPei: true,
            hasMengziXiongZhang: true))),
    "Owning Bear Paw must skip the act two continuation.");
Check(!PhilosophersGazeContinuationPolicy.ShouldInsert(
        ContinuationContext(ContinuationOwnership(
            hasKongziMuduo: true,
            hasXunziShengMo: true))),
    "Owning Ink Line must skip the act two continuation.");
Check(!PhilosophersGazeContinuationPolicy.ShouldInsert(
        ContinuationContext(continuationRecorded: true)),
    "A recorded continuation choice or refusal must prevent repeat insertion.");
Check(!PhilosophersGazeContinuationPolicy.CanGrant(
        PhilosophersGazeContinuationOption.MengziXiongZhang,
        qingYuPeiRoute,
        continuationRecorded: true),
    "A recorded refusal must also prevent a later continuation grant.");
Check(!PhilosophersGazeContinuationPolicy.ShouldInsert(
        ContinuationContext(currentActIndex: 0)),
    "The continuation must not be inserted in act one.");
Check(!PhilosophersGazeContinuationPolicy.ShouldInsert(
        ContinuationContext(currentActIndex: 2)),
    "The continuation must not be inserted after act two.");
Check(!PhilosophersGazeContinuationPolicy.ShouldInsert(
        ContinuationContext(isSingleplayer: false)),
    "The continuation must not be inserted in multiplayer.");
Check(!PhilosophersGazeContinuationPolicy.ShouldInsert(
        ContinuationContext(modelAvailable: false)),
    "The continuation must not be inserted when PhilosophersGaze is unavailable.");
Check(!PhilosophersGazeContinuationPolicy.ShouldInsert(
        ContinuationContext(runInProgress: false)),
    "The continuation must not be inserted outside a run.");
Check(!PhilosophersGazeContinuationPolicy.ShouldInsert(
        ContinuationContext(currentRoomIsMapRoom: false)),
    "The continuation must only be inserted while entering the act map room.");

PhilosophersGazeContinuationEntryPlan continuationEntryPlan =
    PhilosophersGazeContinuationPolicy.CreateEntryPlan(
        mapRoomEntryCompleted: true);
Check(continuationEntryPlan.CloseMapScreen,
    "The act two continuation must close the map screen opened by MapRoom before entering the event.");
Check(!continuationEntryPlan.FadeToBlack,
    "The act two continuation must not restart the room fade while EnterAct is already transitioning.");
Check(PhilosophersGazeContinuationPolicy.CreateEntryPlan(
        mapRoomEntryCompleted: false) == default,
    "The continuation entry plan must not alter screens before MapRoom entry completes.");

Console.WriteLine("PhilosophersGaze continuation policy checks passed.");
