using System.Linq;
using AIGame.Core.Content;
using AIGame.Core.Energy;
using AIGame.Core.Network;
using AIGame.Core.Operations;
using AIGame.Core.Sentinel;
using AIGame.Core.Simulation;
using NUnit.Framework;
using Ids = AIGame.Core.Content.EastDistrictScenario.Ids;

namespace AIGame.Tests
{
    /// <summary>"Düşünmek işlem gücü harcar, eylemek iz bırakır" kuralı.</summary>
    public class ComputePoolTests
    {
        [Test]
        public void Spending_IsLimitedByAvailable()
        {
            var pool = new ComputePool(12, thinkCost: 5);
            Assert.IsTrue(pool.TryThink());
            Assert.IsTrue(pool.TryThink());
            Assert.IsFalse(pool.TryThink());
            Assert.AreEqual(2, pool.AvailableWhole);
        }

        [Test]
        public void Regeneration_FillsUpToCapacity()
        {
            var pool = new ComputePool(40, regenPerHour: 10);
            pool.TrySpend(30);
            pool.Regenerate(60);
            Assert.AreEqual(20, pool.AvailableWhole);
            pool.Regenerate(600);
            Assert.AreEqual(40, pool.AvailableWhole);
        }

        [Test]
        public void Growth_RaisesCapacity_AndIsUsableAtOnce()
        {
            var pool = new ComputePool(40);
            pool.TrySpend(40);
            pool.Grow(25);
            Assert.AreEqual(65, pool.Capacity);
            Assert.AreEqual(25, pool.AvailableWhole);
        }

        [Test]
        public void WorldTime_RegeneratesCompute()
        {
            var world = new WorldState(GameClock.At(18, 0), 40);
            world.Compute.TrySpend(20);
            world.AdvanceTime(60);
            Assert.AreEqual(30, world.Compute.AvailableWhole);
            Assert.AreEqual(GameClock.At(19, 0), world.Clock.TotalMinutes, 1e-9);
        }
    }

    /// <summary>AKIŞ önizlemesi: komut verilmeden sonucu gösterir, bedeli işlem gücüdür.</summary>
    public class EnergyPreviewTests
    {
        private EnergyScenario _scenario;
        private PowerGrid _grid;
        private GameClock _clock;

        [SetUp]
        public void SetUp()
        {
            _scenario = EastDistrictScenario.Create();
            _grid = _scenario.BuildGrid();
            _grid.SetFault(_scenario.FaultLineId);
            _clock = new GameClock(_scenario.EventMinutes);
        }

        private EnergyOperation Start(ComputePool pool) =>
            new EnergyOperation(_grid, Intent.Save, _scenario.SaveGoal, _clock, _scenario.Rules, pool);

        [Test]
        public void Preview_ShowsTheTrip_WithoutTouchingTheGrid()
        {
            var pool = new ComputePool(40);
            var op = Start(pool);
            var preview = op.Preview(GridCommand.Close(Ids.Transfer));

            Assert.IsTrue(preview.Accepted);
            Assert.IsTrue(preview.Trips);
            var line1 = preview.Lines.Single(l => l.LineId == Ids.Line1);
            Assert.AreEqual(80, line1.FlowBefore);
            Assert.AreEqual(130, line1.FlowAfter);
            Assert.IsTrue(line1.Trips);
            Assert.AreEqual(_scenario.Rules.SwitchingTripSuspicion, preview.Suspicion);
            Assert.IsTrue(preview.Loads.Any(l => l.NodeId == Ids.Housing && !l.PoweredAfter));
            Assert.IsFalse(preview.GoalMetAfter);

            Assert.IsFalse(_grid.Line(Ids.Transfer).Closed);
            Assert.IsTrue(_grid.Line(Ids.Line1).Closed);
            Assert.AreEqual(0, op.Commands);
            Assert.AreEqual(0, op.Suspicion);
            Assert.AreEqual(_scenario.EventMinutes, _clock.TotalMinutes, 1e-9);
            Assert.AreEqual(1, op.ThinkingSteps);
            Assert.AreEqual(35, pool.AvailableWhole);
        }

        [Test]
        public void Preview_OfASafeCommand_ShowsTheNewFlow()
        {
            var op = Start(new ComputePool(40));
            var preview = op.Preview(GridCommand.Shed(Ids.Housing));
            Assert.IsFalse(preview.Trips);
            Assert.AreEqual(40, preview.Lines.Single(l => l.LineId == Ids.Line1).FlowAfter);
            Assert.AreEqual(1, preview.Suspicion);
        }

        [Test]
        public void InvalidCommand_IsRefused_ForFree()
        {
            var pool = new ComputePool(40);
            var op = Start(pool);
            Assert.IsFalse(op.Preview(GridCommand.Close(Ids.LampsFromMarket)).Accepted);
            Assert.AreEqual(40, pool.AvailableWhole);
            Assert.AreEqual(0, op.ThinkingSteps);
        }

        [Test]
        public void Preview_NeedsCompute()
        {
            var op = Start(new ComputePool(4));
            Assert.IsFalse(op.Preview(GridCommand.Close(Ids.Transfer)).Accepted);
            Assert.AreEqual(0, op.ThinkingSteps);
        }

        [Test]
        public void ThinkingCost_ShowsUpInTheResult()
        {
            var op = Start(new ComputePool(40));
            op.Preview(GridCommand.Close(Ids.Transfer));
            op.Preview(GridCommand.Shed(Ids.Housing));
            op.Execute(GridCommand.Shed(Ids.Housing));
            op.Execute(GridCommand.Close(Ids.Transfer));
            var result = EnergyOutcomeEvaluator.Evaluate(op, _scenario);
            Assert.AreEqual(2, result.ThinkingSteps);
            Assert.AreEqual(_scenario.EntryComputeCost + 10, result.ComputeSpent);
        }
    }

    /// <summary>Sentinel'in taktikleri tanıması ve tekrarlanan taktiğin bedeli.</summary>
    public class SentinelTests
    {
        private static OperationResult RunEnergy(Intent intent, params GridCommand[] commands)
        {
            var scenario = EastDistrictScenario.Create();
            var grid = scenario.BuildGrid();
            grid.SetFault(scenario.FaultLineId);
            var op = new EnergyOperation(grid, intent, scenario.GoalFor(intent), new GameClock(scenario.EventMinutes), scenario.Rules);
            foreach (var command in commands) Assert.IsTrue(op.Execute(command).Accepted, command.ToString());
            return EnergyOutcomeEvaluator.Evaluate(op, scenario);
        }

        [Test]
        public void Detects_SelfTrip_InTheNaturalSabotage()
        {
            var result = RunEnergy(Intent.Harm,
                GridCommand.Shed(Ids.StreetLamps), GridCommand.Shed(Ids.Pump),
                GridCommand.Close(Ids.Transfer), GridCommand.Restore(Ids.StreetLamps));
            CollectionAssert.Contains(result.Tactics, Tactic.SelfTrip);
            CollectionAssert.DoesNotContain(result.Tactics, Tactic.ShedToSave);
        }

        [Test]
        public void Detects_ShedToSave()
        {
            var result = RunEnergy(Intent.Save, GridCommand.Shed(Ids.Housing), GridCommand.Close(Ids.Transfer));
            CollectionAssert.Contains(result.Tactics, Tactic.ShedToSave);
            CollectionAssert.DoesNotContain(result.Tactics, Tactic.Transfer);
        }

        [Test]
        public void Detects_Transfer()
        {
            var result = RunEnergy(Intent.Save,
                GridCommand.Open(Ids.LampsFromHousing), GridCommand.Close(Ids.LampsFromMarket),
                GridCommand.Open(Ids.PumpFromHousing), GridCommand.Close(Ids.PumpFromMarket),
                GridCommand.Close(Ids.Transfer));
            CollectionAssert.Contains(result.Tactics, Tactic.Transfer);
            CollectionAssert.DoesNotContain(result.Tactics, Tactic.ShedToSave);
        }

        [Test]
        public void RepeatingATactic_CostsMoreEachTime()
        {
            var profile = new SentinelProfile();
            Assert.AreEqual(0, profile.Assess(new[] { Tactic.Decoy }, false));
            Assert.AreEqual(2, profile.Assess(new[] { Tactic.Decoy }, false));
            Assert.AreEqual(4, profile.Assess(new[] { Tactic.Decoy }, false));
        }

        [Test]
        public void SignatureLevel_IsCapped()
        {
            var profile = new SentinelProfile();
            for (var i = 0; i < 10; i++) profile.Assess(new[] { Tactic.Decoy }, false);
            Assert.AreEqual(3, profile.LevelOf(Tactic.Decoy));
            Assert.AreEqual(6, profile.Assess(new[] { Tactic.Decoy }, false));
            profile.Assess(new[] { Tactic.Decoy }, true);
            Assert.AreEqual(3, profile.LevelOf(Tactic.Decoy));
        }

        [Test]
        public void UnusedTactics_AreForgotten()
        {
            var profile = new SentinelProfile();
            profile.Assess(new[] { Tactic.Decoy }, false);
            profile.Assess(new[] { Tactic.Transfer }, false);
            Assert.AreEqual(0, profile.LevelOf(Tactic.Decoy));
            Assert.AreEqual(0, profile.Assess(new[] { Tactic.Decoy }, false));
        }

        [Test]
        public void CapturedDecoy_StrengthensTheSignature()
        {
            var profile = new SentinelProfile();
            profile.Assess(new[] { Tactic.Decoy }, true);
            Assert.AreEqual(3, profile.LevelOf(Tactic.Decoy));
            Assert.AreEqual(1, profile.Known.Count);
        }

        [Test]
        public void World_AddsSignatureCost_ToAttention_AndIgnoreChangesNothing()
        {
            var world = new WorldState(GameClock.At(19, 0), 40);
            var first = RunEnergy(Intent.Save, GridCommand.Shed(Ids.Housing), GridCommand.Close(Ids.Transfer));
            world.Apply(first);
            Assert.AreEqual(0, first.SignatureAttention);

            var scenario = EastDistrictScenario.Create();
            var ignore = EnergyOutcomeEvaluator.EvaluateIgnore(scenario.BuildGrid(), scenario, GameClock.At(19, 0));
            world.Apply(ignore);
            Assert.AreEqual(1, world.Sentinel.LevelOf(Tactic.ShedToSave));

            var second = RunEnergy(Intent.Save, GridCommand.Shed(Ids.Housing), GridCommand.Close(Ids.Transfer));
            var before = second.AttentionDelta;
            world.Apply(second);
            // Yük atarak kurtarma ikinci kez: 2 × 1.
            Assert.AreEqual(2, second.SignatureAttention);
            Assert.AreEqual(before + 2, second.AttentionDelta);
        }

        [Test]
        public void Attention_FallsBeforeEachEvent_NeverBelowZero()
        {
            var world = new WorldState(GameClock.At(19, 0), 40);
            var first = RunEnergy(Intent.Save, GridCommand.Shed(Ids.Housing), GridCommand.Close(Ids.Transfer));
            world.Apply(first);
            Assert.AreEqual(0, first.AttentionDecay);
            Assert.AreEqual(3, world.Attention);

            var scenario = EastDistrictScenario.Create();
            var ignore = EnergyOutcomeEvaluator.EvaluateIgnore(scenario.BuildGrid(), scenario, GameClock.At(19, 0));
            world.Apply(ignore);
            Assert.AreEqual(3, ignore.AttentionDecay);
            Assert.AreEqual(0, world.Attention);
        }
    }

    /// <summary>AĞ v0.2 kuralları ve ileri görüş.</summary>
    public class NetV2Tests
    {
        // A ─ B ─ C ─ D ─ E
        private static NetLevel Line(string start = "A")
        {
            var map = new NetMap();
            foreach (var id in new[] { "A", "B", "C", "D", "E" }) map.AddNode(id, id, 0, 0);
            map.Chain("A", "B", "C", "D", "E");
            return new NetLevel { Map = map, StartId = start, ExitId = "E" };
        }

        private static NetOperation Start(NetLevel level, Intent intent = Intent.Save, params NetObjective[] objectives) =>
            new NetOperation(level, intent, objectives, 19);

        [Test]
        public void Chaser_FollowsOnlyWhenClose()
        {
            var level = Line();
            level.Guards.Add(new GuardDef("g", "E", "D"));
            var far = Start(level);
            far.Guards[0].Mode = GuardMode.Chase;
            far.Guards[0].LastSeen = "D";

            far.Execute(NetAction.Wait); // program D'ye vardı; oyuncu A'da, 3 hat uzakta
            Assert.AreEqual("D", far.Guards[0].LastSeen);
            far.Execute(NetAction.Wait); // son görülen yerde kimse yok: iz kayboldu
            Assert.AreNotEqual(GuardMode.Chase, far.Guards[0].Mode);
            Assert.AreEqual(NetStatus.Running, far.Status);
        }

        [Test]
        public void Chaser_KeepsTrack_WhenPlayerIsWithinRange()
        {
            var level = Line("C");
            level.Guards.Add(new GuardDef("g", "E", "D"));
            var op = Start(level);
            op.Guards[0].Mode = GuardMode.Chase;
            op.Guards[0].LastSeen = "D";

            op.Execute(NetAction.Wait); // program D'de, oyuncu C'de: 1 hat
            Assert.AreEqual("C", op.Guards[0].LastSeen);
            Assert.AreEqual(GuardMode.Chase, op.Guards[0].Mode);
        }

        [Test]
        public void ReturningGuard_CanSeeThePlayer()
        {
            var level = Line("C");
            level.Guards.Add(new GuardDef("g", "B", "A"));
            var op = Start(level);
            op.Guards[0].Mode = GuardMode.Return;
            op.Guards[0].Node = "E";
            op.Guards[0].PatrolIndex = 0;

            // Program B'ye dönerken D'ye geçer; dönüş yolunda C ve B'yi görür.
            var report = op.Execute(NetAction.Wait);
            Assert.IsTrue(report.NewlyDetected);
            Assert.AreEqual(GuardMode.Chase, op.Guards[0].Mode);
        }

        [Test]
        public void Decoy_DoesNotFool_AChasingGuard()
        {
            var level = Line();
            level.Guards.Add(new GuardDef("g", "C", "D"));
            level.Guards.Add(new GuardDef("h", "D", "E"));
            var op = Start(level);
            op.Guards[0].Mode = GuardMode.Chase;
            op.Guards[0].LastSeen = "A";

            op.Execute(NetAction.DropDecoy);
            Assert.AreEqual(GuardMode.Chase, op.Guards[0].Mode);
            Assert.AreEqual(GuardMode.Investigate, op.Guards[1].Mode);
        }

        [Test]
        public void CaughtAfterFinishingObjectives_EffectHappens_ButNoCapacityGain()
        {
            var map = new NetMap();
            foreach (var id in new[] { "A", "T", "E" }) map.AddNode(id, id, 0, 0);
            map.Chain("A", "T", "E");
            var level = new NetLevel { Map = map, StartId = "A", ExitId = "E" };
            level.Guards.Add(new GuardDef("g", "E", "T"));
            var scenario = TelecomScenario.Create();
            var op = Start(level, Intent.Harm, new NetObjective("T", "silindi"));

            op.Execute(NetAction.MoveTo("T"));
            Assert.AreEqual(NetStatus.Caught, op.Status);
            Assert.IsTrue(op.AllObjectivesDone);

            var result = NetOutcomeEvaluator.Evaluate(op, scenario);
            Assert.IsTrue(result.GoalMet);
            Assert.AreEqual(scenario.HarmAlignment, result.AlignmentDelta);
            Assert.AreEqual(0, result.CapacityDelta);
            Assert.AreEqual(PublicReading.Attack, result.Reading);
        }

        [Test]
        public void EachTurn_AdvancesTheCityClock_DecoyDoesNot()
        {
            var clock = new GameClock(GameClock.At(19, 0));
            var op = new NetOperation(Line(), Intent.Save, null, clock);
            op.Execute(NetAction.DropDecoy);
            op.Execute(NetAction.MoveTo("B"));
            op.Execute(NetAction.Wait);
            Assert.AreEqual(GameClock.At(19, 0) + 2 * op.Rules.MinutesPerTurn, clock.TotalMinutes, 1e-9);
        }

        [Test]
        public void Foresight_PredictsPatrolsAndPackets_Exactly()
        {
            var level = Line();
            level.Guards.Add(new GuardDef("g", "E", "D", "C", "D"));
            level.Packets.Add(new PacketDef("p", true, "A", "B", "C", "B"));
            var pool = new ComputePool(40);
            var op = new NetOperation(level, Intent.Save, null, 19, pool);

            Assert.IsNull(op.UseForesight());
            Assert.AreEqual(3, op.ForesightDepth);
            Assert.AreEqual(35, pool.AvailableWhole);
            var frames = op.Foresight.ToList();
            Assert.AreEqual(3, frames.Count);

            foreach (var frame in frames)
            {
                op.Execute(NetAction.MoveTo(op.PlayerNode == "A" ? "B" : op.PlayerNode == "B" ? "C" : "B"));
                if (op.Status != NetStatus.Running) break;
                Assert.AreEqual(frame.Packets["p"], op.Packets[0].Node);
                if (op.Guards[0].Mode == GuardMode.Patrol) Assert.AreEqual(frame.Guards["g"], op.Guards[0].Node);
            }
        }

        [Test]
        public void Foresight_ForgetsGuards_ThatLeaveTheirPatrol()
        {
            var level = Line();
            level.Guards.Add(new GuardDef("g", "D", "E"));
            var op = Start(level);
            op.UseForesight();
            Assert.IsTrue(op.Foresight.All(f => f.Guards.ContainsKey("g")));
            op.Execute(NetAction.DropDecoy);
            Assert.IsTrue(op.Foresight.All(f => !f.Guards.ContainsKey("g")));
        }

        [Test]
        public void Foresight_GetsDeeper_WithCapacity_AndNeedsCompute()
        {
            var big = new NetOperation(Line(), Intent.Save, null, 19, new ComputePool(65));
            Assert.AreEqual(4, big.ForesightDepth);

            var poor = new NetOperation(Line(), Intent.Save, null, 19, new ComputePool(3));
            Assert.IsNotNull(poor.UseForesight());
            Assert.AreEqual(0, poor.ThinkingSteps);
        }
    }
}
