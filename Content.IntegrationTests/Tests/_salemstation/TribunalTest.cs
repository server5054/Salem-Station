using Content.Server._salemstation;
using Content.Shared._salemstation;
using Content.Shared.Buckle;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._salemstation
{
    [TestFixture]
    [TestOf(typeof(TribunalPodiumSystem))]
    public sealed class TribunalTest
    {
        private const string PrisonerDummyId = "TribunalPrisonerDummy";
        private const string CaptainDummyId = "TribunalCaptainDummy";
        private const string CaptainIdCardId = "TribunalCaptainIdDummy";

        [TestPrototypes]
        private const string Prototypes = $@"
- type: entity
  name: {PrisonerDummyId}
  id: {PrisonerDummyId}
  components:
  - type: Buckle
  - type: Damageable
    damageContainer: Biological
  - type: StandingState

- type: entity
  name: {CaptainDummyId}
  id: {CaptainDummyId}
  components:
  - type: Hands
  - type: ComplexInteraction
  - type: Body
    prototype: Human
  - type: StandingState

- type: entity
  name: {CaptainIdCardId}
  id: {CaptainIdCardId}
  components:
  - type: Item
  - type: Access
    tags:
    - Captain
";

        [Test]
        public async Task GuiltyVerdictExecutesPrisoner()
        {
            await using var pair = await PoolManager.GetServerClient();
            var server = pair.Server;

            var testMap = await pair.CreateTestMap();
            var coordinates = testMap.GridCoords;
            var entMan = server.ResolveDependency<IEntityManager>();
            var buckle = entMan.System<SharedBuckleSystem>();
            var hands = entMan.System<SharedHandsSystem>();

            EntityUid podium = default;
            EntityUid prisoner = default;
            EntityUid captain = default;
            EntityUid voterA = default;
            EntityUid voterB = default;
            TribunalPodiumComponent comp = default!;

            await server.WaitAssertion(() =>
            {
                podium = entMan.SpawnEntity("TribunalPodium", coordinates);
                prisoner = entMan.SpawnEntity(PrisonerDummyId, coordinates);
                captain = entMan.SpawnEntity(CaptainDummyId, coordinates);
                voterA = entMan.SpawnEntity(CaptainDummyId, coordinates);
                voterB = entMan.SpawnEntity(CaptainDummyId, coordinates);

                comp = entMan.GetComponent<TribunalPodiumComponent>(podium);

                var idCard = entMan.SpawnEntity(CaptainIdCardId, coordinates);
                Assert.That(hands.TryPickupAnyHand(captain, idCard), "Captain could not pick up ID card");

                Assert.That(buckle.TryBuckle(prisoner, null, podium), "Could not buckle prisoner to podium");
            });

            // A crewmate without captain access cannot start the trial.
            await server.WaitAssertion(() =>
            {
                entMan.EventBus.RaiseLocalEvent(podium, new InteractHandEvent(voterA, podium));
                Assert.That(comp.CurrentState, Is.EqualTo(TrialState.Idle), "Trial started without captain access!");
            });

            // The captain starts the trial -> Discussion phase.
            await server.WaitAssertion(() =>
            {
                entMan.EventBus.RaiseLocalEvent(podium, new InteractHandEvent(captain, podium));
                Assert.That(comp.CurrentState, Is.EqualTo(TrialState.Discussion), "Captain interaction did not start the trial");
                Assert.That(comp.Prisoner, Is.EqualTo(prisoner));
            });

            // Votes cast during Discussion must be ignored.
            await server.WaitAssertion(() =>
            {
                entMan.EventBus.RaiseLocalEvent(podium, new TribunalVoteMessage(true) { Actor = voterA });
                Assert.That(comp.Votes, Is.Empty, "A vote was accepted during the discussion phase");
            });

            // Fast-forward past Discussion -> Voting phase.
            await server.WaitPost(() => comp.StateEndTime = TimeSpan.Zero);
            await server.WaitRunTicks(1);
            await server.WaitAssertion(() =>
            {
                Assert.That(comp.CurrentState, Is.EqualTo(TrialState.Voting), "Discussion did not advance to Voting");
                Assert.That(comp.StateEndTime, Is.GreaterThan(TimeSpan.Zero), "Voting phase got no timer (would fall through instantly)");
            });

            // Two guilty votes, one innocent; a re-vote must overwrite, not duplicate.
            await server.WaitAssertion(() =>
            {
                entMan.EventBus.RaiseLocalEvent(podium, new TribunalVoteMessage(false) { Actor = captain });
                entMan.EventBus.RaiseLocalEvent(podium, new TribunalVoteMessage(true) { Actor = captain });
                entMan.EventBus.RaiseLocalEvent(podium, new TribunalVoteMessage(true) { Actor = voterA });
                entMan.EventBus.RaiseLocalEvent(podium, new TribunalVoteMessage(false) { Actor = voterB });
                Assert.That(comp.Votes, Has.Count.EqualTo(3), "Vote count wrong (re-vote duplicated?)");
            });

            // Fast-forward past Voting -> guilty verdict -> Execution phase.
            await server.WaitPost(() => comp.StateEndTime = TimeSpan.Zero);
            await server.WaitRunTicks(1);
            await server.WaitAssertion(() =>
            {
                Assert.That(comp.CurrentState, Is.EqualTo(TrialState.Execution), "Guilty majority did not lead to Execution");
            });

            // Fast-forward past last words -> prisoner executed, podium resets.
            await server.WaitPost(() => comp.StateEndTime = TimeSpan.Zero);
            await server.WaitRunTicks(1);
            await server.WaitAssertion(() =>
            {
                var damage = entMan.GetComponent<DamageableComponent>(prisoner);
                Assert.That(damage.TotalDamage.Value, Is.GreaterThan(0), "Prisoner took no damage after guilty verdict");
                Assert.That(comp.CurrentState, Is.EqualTo(TrialState.Idle), "Podium did not reset after execution");
                Assert.That(comp.Prisoner, Is.Null);
                Assert.That(comp.Votes, Is.Empty);
            });

            await pair.CleanReturnAsync();
        }

        [Test]
        public async Task InnocentVerdictSparesPrisoner()
        {
            await using var pair = await PoolManager.GetServerClient();
            var server = pair.Server;

            var testMap = await pair.CreateTestMap();
            var coordinates = testMap.GridCoords;
            var entMan = server.ResolveDependency<IEntityManager>();
            var buckle = entMan.System<SharedBuckleSystem>();
            var hands = entMan.System<SharedHandsSystem>();

            EntityUid podium = default;
            EntityUid prisoner = default;
            EntityUid captain = default;
            TribunalPodiumComponent comp = default!;

            await server.WaitAssertion(() =>
            {
                podium = entMan.SpawnEntity("TribunalPodium", coordinates);
                prisoner = entMan.SpawnEntity(PrisonerDummyId, coordinates);
                captain = entMan.SpawnEntity(CaptainDummyId, coordinates);

                comp = entMan.GetComponent<TribunalPodiumComponent>(podium);

                var idCard = entMan.SpawnEntity(CaptainIdCardId, coordinates);
                Assert.That(hands.TryPickupAnyHand(captain, idCard));
                Assert.That(buckle.TryBuckle(prisoner, null, podium));

                entMan.EventBus.RaiseLocalEvent(podium, new InteractHandEvent(captain, podium));
                Assert.That(comp.CurrentState, Is.EqualTo(TrialState.Discussion));
            });

            // Discussion -> Voting, single innocent vote.
            await server.WaitPost(() => comp.StateEndTime = TimeSpan.Zero);
            await server.WaitRunTicks(1);
            await server.WaitAssertion(() =>
            {
                Assert.That(comp.CurrentState, Is.EqualTo(TrialState.Voting));
                entMan.EventBus.RaiseLocalEvent(podium, new TribunalVoteMessage(false) { Actor = captain });
            });

            // Voting ends -> acquittal, no execution, no damage.
            await server.WaitPost(() => comp.StateEndTime = TimeSpan.Zero);
            await server.WaitRunTicks(1);
            await server.WaitAssertion(() =>
            {
                Assert.That(comp.CurrentState, Is.EqualTo(TrialState.Idle), "Innocent verdict did not end the trial");
                var damage = entMan.GetComponent<DamageableComponent>(prisoner);
                Assert.That(damage.TotalDamage.Value, Is.EqualTo(0), "Prisoner was damaged despite innocent verdict!");
            });

            await pair.CleanReturnAsync();
        }
    }
}
