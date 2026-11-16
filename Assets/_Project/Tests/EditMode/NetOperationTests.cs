using System.Collections.Generic;
using System.Linq;
using AIGame.Core.Content;
using AIGame.Core.Network;
using AIGame.Core.Operations;
using NUnit.Framework;

namespace AIGame.Tests
{
    /// <summary>AĞ kurallarının küçük, elle kurulmuş haritalardaki testleri.</summary>
    public class NetOperationTests
    {
        // A ─ B ─ C ─ D ─ E   (düz hat)
        private static NetLevel Line()
        {
            var map = new NetMap();
            foreach (var id in new[] { "A", "B", "C", "D", "E" }) map.AddNode(id, id, 0, 0);
            map.Chain("A", "B", "C", "D", "E");
            return new NetLevel { Map = map, StartId = "A", ExitId = "E" };
        }

        private static NetOperation Start(NetLevel level, int hour = 19, params NetObjective[] objectives) =>
            new NetOperation(level, Intent.Save, objectives, hour);

        [Test]
        public void ReachingExit_WithNoObjectives_Escapes()
        {
            var op = Start(Line());
            foreach (var n in new[] { "B", "C", "D", "E" }) op.Execute(NetAction.MoveTo(n));
            Assert.AreEqual(NetStatus.Escaped, op.Status);
            Assert.AreEqual(4, op.Turn);
        }

        [Test]
        public void Exit_DoesNotCount_UntilObjectivesAreDone()
        {
            var map = new NetMap();
            foreach (var id in new[] { "A", "E", "T" }) map.AddNode(id, id, 0, 0);
            map.Chain("T", "A", "E");
            var level = new NetLevel { Map = map, StartId = "A", ExitId = "E" };
            var op = Start(level, 19, new NetObjective("T", "alındı"));

            op.Execute(NetAction.MoveTo("E"));
            Assert.AreEqual(NetStatus.Running, op.Status);

            op.Execute(NetAction.MoveTo("A"));
            Assert.IsTrue(op.Execute(NetAction.MoveTo("T")).ObjectiveCompleted);
            op.Execute(NetAction.MoveTo("A"));
            op.Execute(NetAction.MoveTo("E"));
            Assert.AreEqual(NetStatus.Escaped, op.Status);
        }

        [Test]
        public void MovingToUnlinkedNode_IsRefused_AndCostsNoTurn()
        {
            var op = Start(Line());
            Assert.IsFalse(op.Execute(NetAction.MoveTo("C")).Accepted);
            Assert.AreEqual(0, op.Turn);
        }

        [Test]
        public void MovingWithPacket_KeepsHidden_LeavingIt_Reveals()
        {
            var level = Line();
            level.Packets.Add(new PacketDef("p", true, "A", "B", "C", "B"));
            var op = Start(level);
            Assert.IsTrue(op.Hidden);

            Assert.IsTrue(op.Execute(NetAction.MoveTo("B")).Hidden);
            Assert.IsTrue(op.Execute(NetAction.MoveTo("C")).Hidden);
            Assert.IsFalse(op.Execute(NetAction.MoveTo("D")).Hidden); // paket B'ye döndü
        }

        [Test]
        public void VisiblePlayer_InPatrolVision_IsDetected_AndGuardChases()
        {
            var level = Line();
            level.Guards.Add(new GuardDef("g", "E", "D", "C", "D"));
            var op = Start(level);

            // Program D'ye gelir; önündeki iki düğüm C ve D. Oyuncu B'de: görülmez.
            Assert.IsFalse(op.Execute(NetAction.MoveTo("B")).NewlyDetected);
            // Program C'ye gelir; oyuncu B'de bekler: C'nin önü D ve E, yine görülmez.
            Assert.IsFalse(op.Execute(NetAction.Wait).NewlyDetected);
            // Program D'ye gider (önü E, D). Oyuncu C'ye çıkar: görülmez.
            Assert.IsFalse(op.Execute(NetAction.MoveTo("C")).NewlyDetected);
            // Program E'ye gider; önü D ve C. Oyuncu C'de: görülür.
            var report = op.Execute(NetAction.Wait);
            Assert.IsTrue(report.NewlyDetected);
            Assert.AreEqual(1, op.Detections);
            Assert.AreEqual(GuardMode.Chase, op.Guards[0].Mode);
        }

        [Test]
        public void HiddenPlayer_IsNotDetected_AndNotCaughtByPatrol()
        {
            var level = Line();
            level.Guards.Add(new GuardDef("g", "C", "B"));
            level.Packets.Add(new PacketDef("p", true, "A", "B"));
            var op = Start(level);

            // Oyuncu paketle B'ye geçer, program da B'ye gelir: aynı düğüm ama oyuncu saklı.
            var report = op.Execute(NetAction.MoveTo("B"));
            Assert.AreEqual("B", op.Guards[0].Node);
            Assert.IsTrue(report.Hidden);
            Assert.AreEqual(NetStatus.Running, op.Status);
            Assert.AreEqual(0, op.Detections);
        }

        [Test]
        public void VisiblePlayer_OnGuardNode_IsCaught()
        {
            var level = Line();
            level.Guards.Add(new GuardDef("g", "C", "B"));
            var op = Start(level);
            op.Execute(NetAction.MoveTo("B"));
            Assert.AreEqual(NetStatus.Caught, op.Status);
        }

        [Test]
        public void SwappingPlacesWithGuard_IsCaught()
        {
            var level = Line();
            level.Guards.Add(new GuardDef("g", "B", "A"));
            var op = Start(level);
            // Oyuncu A→B, program B→A: aynı hattan karşılıklı geçtiler.
            op.Execute(NetAction.MoveTo("B"));
            Assert.AreEqual(NetStatus.Caught, op.Status);
        }

        [Test]
        public void Chase_Ends_WhenGuardReachesLastSeenNode_AndPlayerIsHidden()
        {
            // Program E-D-C-D devriyesinde; paket A-B arasında gidip gelir.
            var level = Line();
            level.Guards.Add(new GuardDef("g", "E", "D", "C", "D"));
            level.Packets.Add(new PacketDef("p", true, "A", "B"));
            var op = Start(level);

            op.Execute(NetAction.MoveTo("B")); // program D
            op.Execute(NetAction.Wait);        // program C
            op.Execute(NetAction.Wait);        // program D
            var seen = op.Execute(NetAction.MoveTo("C")); // program E; önü D ve C → görüldü
            Assert.IsTrue(seen.NewlyDetected);
            Assert.AreEqual(GuardMode.Chase, op.Guards[0].Mode);

            Assert.IsTrue(op.Execute(NetAction.MoveTo("B")).Hidden); // pakete bindi; program D'ye koşuyor
            Assert.IsTrue(op.Execute(NetAction.MoveTo("A")).Hidden); // program son gördüğü yere (C) vardı
            Assert.AreEqual("C", op.Guards[0].Node);

            op.Execute(NetAction.Wait); // iz kayboldu: program devriyesine dönüyor
            Assert.AreNotEqual(GuardMode.Chase, op.Guards[0].Mode);
            Assert.AreEqual(NetStatus.Running, op.Status);
            Assert.AreEqual(1, op.Detections);
        }

        [Test]
        public void Decoy_LuresGuard_HoldsIt_ThenIsCaptured()
        {
            var level = Line();
            level.Rules.DecoyHoldTurns = 2;
            level.Guards.Add(new GuardDef("g", "D", "E"));
            var op = Start(level);

            Assert.IsTrue(op.Execute(NetAction.DropDecoy).Accepted);
            Assert.AreEqual(0, op.Turn, "Kopya bırakmak tur harcamaz.");
            Assert.AreEqual(GuardMode.Investigate, op.Guards[0].Mode);
            Assert.IsFalse(op.Execute(NetAction.DropDecoy).Accepted, "Bölüm başına tek kopya.");
        }

        [Test]
        public void Decoy_OutOfRange_DoesNotLure()
        {
            var level = Line();
            level.Rules.DecoyLureRange = 2;
            level.Guards.Add(new GuardDef("g", "D", "E"));
            var op = Start(level);
            op.Execute(NetAction.DropDecoy);
            Assert.AreEqual(GuardMode.Patrol, op.Guards[0].Mode);
        }

        [Test]
        public void Night_KeepsOnlyNightPackets()
        {
            var level = Line();
            level.Packets.Add(new PacketDef("gunduz", false, "A", "B"));
            level.Packets.Add(new PacketDef("gece", true, "C", "D"));
            Assert.AreEqual(2, Start(level, 19).Packets.Count);
            Assert.AreEqual(1, Start(level, 3).Packets.Count);
        }

        [Test]
        public void Level_WithBrokenRoute_IsRejected()
        {
            var level = Line();
            level.Packets.Add(new PacketDef("p", true, "A", "C"));
            Assert.Throws<System.InvalidOperationException>(() => Start(level));
        }
    }

    /// <summary>İlk AĞ bölümünün tasarım iddiaları: çözücüyle doğrulanır.</summary>
    public class TelecomScenarioTests
    {
        private static List<NetAction> Solve(Intent intent, int hour, bool allowDetection, bool allowDecoy, bool traffic = true)
        {
            var scenario = TelecomScenario.Create();
            var level = scenario.BuildLevel();
            if (!traffic) level.Packets.Clear();
            var op = new NetOperation(level, intent, scenario.ObjectivesFor(intent), hour);
            return NetSolver.Solve(op, new NetSolveOptions { AllowDetection = allowDetection, AllowDecoy = allowDecoy, MaxTurns = 40 });
        }

        [TestCase(Intent.Save)]
        [TestCase(Intent.Harm)]
        public void Evening_CanBeDone_Unseen_WithoutDecoy(Intent intent)
        {
            Assert.IsNotNull(Solve(intent, 19, false, false));
        }

        [TestCase(Intent.Save)]
        [TestCase(Intent.Harm)]
        public void UnseenRoute_RequiresTraffic(Intent intent)
        {
            Assert.IsNull(Solve(intent, 19, false, false, traffic: false));
        }

        [TestCase(Intent.Save)]
        [TestCase(Intent.Harm)]
        public void Night_CannotBeDone_Unseen_WithoutDecoy(Intent intent)
        {
            Assert.IsNull(Solve(intent, 3, false, false));
        }

        [TestCase(Intent.Save)]
        [TestCase(Intent.Harm)]
        public void Night_CanBeDone_Unseen_WithDecoy(Intent intent)
        {
            var route = Solve(intent, 3, false, true);
            Assert.IsNotNull(route);
            Assert.IsTrue(route.Any(a => a.Kind == NetActionKind.DropDecoy));
        }

        [TestCase(Intent.Save)]
        [TestCase(Intent.Harm)]
        public void Night_CanStillBeDone_IfDetectionIsAccepted(Intent intent)
        {
            Assert.IsNotNull(Solve(intent, 3, true, false));
        }

        [Test]
        public void DecoyIsFaster_ButSentinelLearnsIt()
        {
            var scenario = TelecomScenario.Create();
            var without = Solve(Intent.Save, 19, false, false);
            var with = Solve(Intent.Save, 19, false, true);
            Assert.Less(with.Count, without.Count);

            var op = new NetOperation(scenario.BuildLevel(), Intent.Save, scenario.SaveObjectives, 19);
            foreach (var action in with) op.Execute(action);
            var result = NetOutcomeEvaluator.Evaluate(op, scenario);

            Assert.IsTrue(result.GoalMet);
            Assert.AreEqual(PublicReading.Natural, result.Reading);
            Assert.IsTrue(result.SentinelLearnedDecoy);
            Assert.AreEqual(scenario.BaseAttention + scenario.DecoyCapturedAttention, result.AttentionDelta);
        }

        [Test]
        public void Outcome_Unseen_Seen_Caught()
        {
            var scenario = TelecomScenario.Create();

            var unseen = new NetOperation(scenario.BuildLevel(), Intent.Harm, scenario.HarmObjectives, 19);
            foreach (var action in Solve(Intent.Harm, 19, false, false)) unseen.Execute(action);
            var unseenResult = NetOutcomeEvaluator.Evaluate(unseen, scenario);
            Assert.AreEqual(PublicReading.Natural, unseenResult.Reading);
            Assert.AreEqual(scenario.HarmAlignment, unseenResult.AlignmentDelta);
            Assert.AreEqual(-scenario.OperationComputeCost + scenario.HarmComputeReward, unseenResult.ComputeDelta);

            var seen = new NetOperation(scenario.BuildLevel(), Intent.Save, scenario.SaveObjectives, 3);
            foreach (var action in Solve(Intent.Save, 3, true, false)) seen.Execute(action);
            var seenResult = NetOutcomeEvaluator.Evaluate(seen, scenario);
            Assert.AreEqual(PublicReading.Unexplained, seenResult.Reading);
            Assert.Greater(seenResult.AttentionDelta, unseenResult.AttentionDelta);

            var caught = new NetOperation(scenario.BuildLevel(), Intent.Save, scenario.SaveObjectives, 3);
            foreach (var node in new[] { "a", "b", "c" })
                if (caught.Status == NetStatus.Running) caught.Execute(NetAction.MoveTo(node));
            while (caught.Status == NetStatus.Running && caught.Turn < 20) caught.Execute(NetAction.Wait);
            Assert.AreEqual(NetStatus.Caught, caught.Status);
            var caughtResult = NetOutcomeEvaluator.Evaluate(caught, scenario);
            Assert.IsFalse(caughtResult.GoalMet);
            Assert.AreEqual(PublicReading.Attack, caughtResult.Reading);
            Assert.AreEqual(0, caughtResult.AlignmentDelta);
            Assert.Greater(caughtResult.AttentionDelta, seenResult.AttentionDelta);
        }
    }
}
