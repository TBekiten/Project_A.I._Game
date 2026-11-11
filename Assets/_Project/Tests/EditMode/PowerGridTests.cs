using System;
using AIGame.Core.Energy;
using NUnit.Framework;

namespace AIGame.Tests
{
    public class PowerGridTests
    {
        private static PowerGrid SimpleGrid()
        {
            // S ─ A (cap 50) ─ X (yük 30)
            //               └ Y (yük 15)
            // S ─ B (cap 40) ─ Z (yük 10)
            // A ─ tie ─ Z (açık)
            var grid = new PowerGrid();
            grid.AddNode(new GridNode("S", "Kaynak", GridNodeKind.Source));
            grid.AddNode(new GridNode("A", "A kavşağı", GridNodeKind.Junction));
            grid.AddNode(new GridNode("B", "B kavşağı", GridNodeKind.Junction));
            grid.AddNode(new GridNode("X", "X", GridNodeKind.Load, 30));
            grid.AddNode(new GridNode("Y", "Y", GridNodeKind.Load, 15));
            grid.AddNode(new GridNode("Z", "Z", GridNodeKind.Load, 10));
            grid.AddLine(new GridLine("SA", "SA", "S", "A", 50, true, true));
            grid.AddLine(new GridLine("SB", "SB", "S", "B", 40, true, true));
            grid.AddLine(new GridLine("AX", "AX", "A", "X", 40, true, false));
            grid.AddLine(new GridLine("AY", "AY", "A", "Y", 40, true, false));
            grid.AddLine(new GridLine("BZ", "BZ", "B", "Z", 40, true, true));
            grid.AddLine(new GridLine("tie", "tie", "A", "Z", 40, false, true));
            return grid;
        }

        [Test]
        public void Flow_IsSumOfDownstreamEnabledLoads()
        {
            var grid = SimpleGrid();
            var eval = grid.Evaluate();

            Assert.AreEqual(45, eval.FlowOf("SA"));
            Assert.AreEqual(10, eval.FlowOf("SB"));
            Assert.AreEqual(30, eval.FlowOf("AX"));
            Assert.AreEqual(0, eval.FlowOf("tie"));
            Assert.AreEqual(55, eval.LoadBelow("S"));
            Assert.IsTrue(eval.IsPowered("X"));
        }

        [Test]
        public void ShedLoad_IsNotCounted_AndNotPowered()
        {
            var grid = SimpleGrid();
            grid.SetLoadEnabled(grid.Node("X"), false);
            var eval = grid.Evaluate();

            Assert.AreEqual(15, eval.FlowOf("SA"));
            Assert.IsTrue(eval.IsEnergized("X"));
            Assert.IsFalse(eval.IsPowered("X"));
        }

        [Test]
        public void ClosingTie_BetweenTwoFedParts_IsALoop()
        {
            var grid = SimpleGrid();
            Assert.IsTrue(grid.WouldCreateLoop("tie"));

            grid.SetLineClosed(grid.Line("BZ"), false);
            Assert.IsFalse(grid.WouldCreateLoop("tie"));
        }

        [Test]
        public void AddLine_RejectsClosedLoop()
        {
            var grid = SimpleGrid();
            Assert.Throws<InvalidOperationException>(() =>
                grid.AddLine(new GridLine("SX", "SX", "S", "X", 10, true, true)));
        }

        [Test]
        public void Overload_TripsDeepestOverloadedLineFirst()
        {
            var grid = SimpleGrid();
            grid.SetLineClosed(grid.Line("BZ"), false);
            grid.SetLineClosed(grid.Line("tie"), true); // SA: 30 + 15 + 10 = 55 > 50

            var tripped = grid.ResolveOverloads();

            Assert.AreEqual(1, tripped.Count);
            Assert.AreEqual("SA", tripped[0].Id);
            Assert.IsTrue(grid.Line("SA").Tripped);
            var eval = grid.Evaluate();
            Assert.IsFalse(eval.IsPowered("X"));
            Assert.IsFalse(eval.IsPowered("Z"));
        }

        [Test]
        public void FaultedLine_CarriesNothing()
        {
            var grid = SimpleGrid();
            grid.SetFault("SB");
            var eval = grid.Evaluate();
            Assert.IsFalse(eval.IsPowered("Z"));
            Assert.IsFalse(grid.Line("SB").Closed);
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var grid = SimpleGrid();
            var copy = grid.Clone();
            copy.SetLoadEnabled(copy.Node("X"), false);
            Assert.IsTrue(grid.Node("X").Enabled);
            Assert.AreEqual(45, grid.Evaluate().FlowOf("SA"));
            Assert.AreEqual(15, copy.Evaluate().FlowOf("SA"));
        }
    }
}
