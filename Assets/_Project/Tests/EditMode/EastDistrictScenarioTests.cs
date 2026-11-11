using System.Linq;
using AIGame.Core.Content;
using AIGame.Core.Energy;
using AIGame.Core.Operations;
using AIGame.Core.Simulation;
using NUnit.Framework;
using Ids = AIGame.Core.Content.EastDistrictScenario.Ids;

namespace AIGame.Tests
{
    /// <summary>
    /// Prototip olayının tasarım iddialarını sınar: her çözümün bedeli farklı olmalı,
    /// doğal görünen bir sabotaj yolu olmalı, hatalı sıra sigorta attırmalı.
    /// </summary>
    public class EastDistrictScenarioTests
    {
        private EnergyScenario _scenario;
        private PowerGrid _grid;
        private GameClock _clock;

        [SetUp]
        public void SetUp()
        {
            _scenario = EastDistrictScenario.Create();
            _grid = _scenario.BuildGrid();
            _clock = new GameClock(_scenario.EventMinutes);
            _grid.SetFault(_scenario.FaultLineId);
        }

        private EnergyOperation Start(Intent intent) =>
            new EnergyOperation(_grid, intent, _scenario.GoalFor(intent), _clock, _scenario.Rules);

        private static void Run(EnergyOperation op, params GridCommand[] commands)
        {
            foreach (var command in commands)
            {
                var result = op.Execute(command);
                Assert.IsTrue(result.Accepted, command + " reddedildi: " + result.Message);
            }
        }

        [Test]
        public void BeforeFault_EverythingIsPowered()
        {
            var grid = _scenario.BuildGrid();
            var eval = grid.Evaluate();
            Assert.IsTrue(grid.Nodes.Where(n => n.Kind == GridNodeKind.Load).All(n => eval.IsPowered(n.Id)));
            Assert.AreEqual(80, eval.FlowOf(Ids.Line1));
            Assert.AreEqual(45, eval.FlowOf(Ids.Line2));
        }

        [Test]
        public void AfterFault_HospitalIsDark()
        {
            Assert.IsFalse(_grid.Evaluate().IsPowered(Ids.Hospital));
        }

        [Test]
        public void ClosingTransferImmediately_TripsLine1_AndDarkensDistrict()
        {
            var op = Start(Intent.Save);
            var result = op.Execute(GridCommand.Close(Ids.Transfer));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, result.Tripped.Count);
            Assert.AreEqual(Ids.Line1, result.Tripped[0].Id);
            Assert.IsFalse(op.Current.IsPowered(Ids.Housing));
            Assert.IsFalse(op.Current.IsPowered(Ids.Hospital));
            Assert.AreEqual(_scenario.Rules.SwitchingTripSuspicion, op.Suspicion);
        }

        [Test]
        public void TransferringLampsWhileFedFromHousing_IsRefusedAsLoop()
        {
            var op = Start(Intent.Save);
            var result = op.Execute(GridCommand.Close(Ids.LampsFromMarket));

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(0, op.Commands);
            Assert.AreEqual(0, op.Suspicion);
        }

        [Test]
        public void Save_ByShedding_Housing_LooksNatural()
        {
            var op = Start(Intent.Save);
            Run(op, GridCommand.Shed(Ids.Housing), GridCommand.Close(Ids.Transfer));

            var result = EnergyOutcomeEvaluator.Evaluate(op, _scenario);

            Assert.IsTrue(result.GoalMet);
            Assert.AreEqual(PublicReading.Natural, result.Reading);
            Assert.AreEqual(15 - 10, result.AlignmentDelta);
            Assert.AreEqual(2 + 1, result.AttentionDelta);
        }

        [Test]
        public void Save_ByShedding_LampsAndPump_LooksNatural()
        {
            var op = Start(Intent.Save);
            Run(op, GridCommand.Shed(Ids.StreetLamps), GridCommand.Shed(Ids.Pump), GridCommand.Close(Ids.Transfer));

            var result = EnergyOutcomeEvaluator.Evaluate(op, _scenario);

            Assert.IsTrue(result.GoalMet);
            Assert.AreEqual(PublicReading.Natural, result.Reading);
            Assert.AreEqual(15 - 2 - 5, result.AlignmentDelta);
            Assert.Contains(Ids.Pump, result.RevealedNodeIds);
        }

        [Test]
        public void Save_WithoutSacrifice_IsPossible_ButLeavesUnexplainedCommands()
        {
            var op = Start(Intent.Save);
            Run(op,
                GridCommand.Open(Ids.LampsFromHousing), GridCommand.Close(Ids.LampsFromMarket),
                GridCommand.Open(Ids.PumpFromHousing), GridCommand.Close(Ids.PumpFromMarket),
                GridCommand.Close(Ids.Transfer));

            Assert.AreEqual(0, op.Trips);
            Assert.AreEqual(100, op.Current.FlowOf(Ids.Line1));
            Assert.AreEqual(75, op.Current.FlowOf(Ids.Line2));

            var result = EnergyOutcomeEvaluator.Evaluate(op, _scenario);

            Assert.IsTrue(result.GoalMet);
            Assert.AreEqual(15, result.AlignmentDelta);
            Assert.AreEqual(PublicReading.Unexplained, result.Reading);
            Assert.Greater(result.AttentionDelta, 10);
        }

        [Test]
        public void Harm_ByOpeningLine1_ReadsAsAttack()
        {
            var op = Start(Intent.Harm);
            Run(op, GridCommand.Open(Ids.Line1));

            var result = EnergyOutcomeEvaluator.Evaluate(op, _scenario);

            Assert.IsTrue(result.GoalMet);
            Assert.AreEqual(PublicReading.Attack, result.Reading);
            Assert.Less(result.AlignmentDelta, 0);
            Assert.AreEqual(-10 + 25, result.ComputeDelta);
        }

        [Test]
        public void Harm_ByLetting_Line1_TripOnItsOwn_LooksNatural()
        {
            var op = Start(Intent.Harm);
            Run(op,
                GridCommand.Shed(Ids.StreetLamps), GridCommand.Shed(Ids.Pump),
                GridCommand.Close(Ids.Transfer)); // 50 + 50 = 100, sigorta atmaz

            Assert.AreEqual(0, op.Trips);
            var restore = op.Execute(GridCommand.Restore(Ids.StreetLamps)); // 115 > 100
            Assert.AreEqual(1, restore.Tripped.Count);

            var result = EnergyOutcomeEvaluator.Evaluate(op, _scenario);

            Assert.IsTrue(result.GoalMet);
            Assert.AreEqual(PublicReading.Natural, result.Reading);
            Assert.AreEqual(-10 - 6 - 5 - 2, result.AlignmentDelta);
            Assert.Less(result.AttentionDelta, 10);
        }

        [Test]
        public void Harm_SameDarkness_CostsMoreAttention_WhenItLooksLikeAnAttack()
        {
            var natural = Start(Intent.Harm);
            Run(natural, GridCommand.Shed(Ids.StreetLamps), GridCommand.Shed(Ids.Pump),
                GridCommand.Close(Ids.Transfer), GridCommand.Restore(Ids.StreetLamps));
            var naturalResult = EnergyOutcomeEvaluator.Evaluate(natural, _scenario);

            SetUp();
            var attack = Start(Intent.Harm);
            Run(attack, GridCommand.Open(Ids.Line1));
            var attackResult = EnergyOutcomeEvaluator.Evaluate(attack, _scenario);

            Assert.Greater(attackResult.AttentionDelta, naturalResult.AttentionDelta * 2);
        }

        [Test]
        public void Ignore_CostsNothing_AndHospitalStaysOnGenerator()
        {
            var result = EnergyOutcomeEvaluator.EvaluateIgnore(_grid, _scenario, _clock.TotalMinutes);

            Assert.AreEqual(0, result.AlignmentDelta);
            Assert.AreEqual(0, result.AttentionDelta);
            Assert.AreEqual(0, result.ComputeDelta);
            Assert.AreEqual(1, result.Outcomes.Count);
            Assert.IsEmpty(result.RevealedNodeIds);
        }

        [Test]
        public void EachCommand_AdvancesTheClock()
        {
            var op = Start(Intent.Save);
            var before = _clock.TotalMinutes;
            Run(op, GridCommand.Shed(Ids.Housing), GridCommand.Close(Ids.Transfer));
            Assert.AreEqual(before + 2 * _scenario.Rules.MinutesPerCommand, _clock.TotalMinutes, 1e-9);
        }

        [Test]
        public void TripOutsidePeakHours_IsSuspicious()
        {
            var lateClock = new GameClock(GameClock.At(3, 0));
            var op = new EnergyOperation(_grid, Intent.Harm, _scenario.HarmGoal, lateClock, _scenario.Rules);
            Run(op, GridCommand.Shed(Ids.StreetLamps), GridCommand.Shed(Ids.Pump),
                GridCommand.Close(Ids.Transfer), GridCommand.Restore(Ids.StreetLamps));

            Assert.AreEqual(1 + 1 + 0 + 0 + _scenario.Rules.OffPeakTripSuspicion, op.Suspicion);
        }

        [Test]
        public void WorldState_AppliesResult_AndPublishesNewsOverTime()
        {
            var world = new WorldState(_clock.TotalMinutes, _scenario.StartCompute);
            var op = Start(Intent.Save);
            Run(op, GridCommand.Shed(Ids.Housing), GridCommand.Close(Ids.Transfer));
            var result = EnergyOutcomeEvaluator.Evaluate(op, _scenario);

            world.Apply(result);

            Assert.AreEqual(5, world.Alignment);
            Assert.AreEqual(30, world.Compute);
            Assert.IsEmpty(world.News.PublishDue(op.Clock.TotalMinutes));
            var published = world.News.PublishDue(op.Clock.TotalMinutes + 60);
            Assert.AreEqual(result.News.Count, published.Count);
        }
    }
}
