using System.Collections.Generic;
using System.Linq;
using AIGame.Core.Energy;
using AIGame.Core.Network;
using AIGame.Core.Operations;

namespace AIGame.Core.Sentinel
{
    /// <summary>Operasyon kayıtlarından oyuncunun hangi taktikleri kullandığını çıkarır.</summary>
    public static class TacticDetector
    {
        public static List<Tactic> FromEnergy(EnergyOperation operation)
        {
            var tactics = new List<Tactic>();

            if (operation.Log.Any(r => r.Command.Kind == GridCommandKind.RestoreLoad && r.Tripped.Count > 0))
                tactics.Add(Tactic.SelfTrip);

            if (operation.Intent == Intent.Save && operation.GoalMet &&
                operation.Grid.Nodes.Any(n => n.Kind == GridNodeKind.Load && !n.Enabled))
                tactics.Add(Tactic.ShedToSave);

            foreach (var node in operation.Grid.Nodes.Where(n => n.Kind == GridNodeKind.Load))
            {
                var before = operation.FeederAtStart(node.Id);
                var after = operation.Current.FeederOf(node.Id);
                if (before == null || after == null || before == after) continue;
                tactics.Add(Tactic.Transfer);
                break;
            }

            return tactics;
        }

        public static List<Tactic> FromNet(NetOperation operation)
        {
            var tactics = new List<Tactic>();
            if (operation.DecoysUsed > 0) tactics.Add(Tactic.Decoy);
            return tactics;
        }
    }
}
