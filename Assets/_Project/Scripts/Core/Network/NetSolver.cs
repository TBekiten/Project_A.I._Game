using System.Collections.Generic;

namespace AIGame.Core.Network
{
    public sealed class NetSolveOptions
    {
        /// <summary>Fark edilmeye izin var mı? false ise yalnızca tamamen gizli rotalar aranır.</summary>
        public bool AllowDetection { get; set; }

        /// <summary>Kopya kullanılabilir mi?</summary>
        public bool AllowDecoy { get; set; } = true;

        public int MaxTurns { get; set; } = 60;
        public int MaxStates { get; set; } = 400000;
    }

    /// <summary>
    /// AĞ bölümleri için genişlik öncelikli çözücü. Bölüm tasarlarken "bu bölüm çözülebilir mi,
    /// en kısa çözüm kaç tur, kopyasız geçilebilir mi" sorularını cevaplar.
    /// </summary>
    public static class NetSolver
    {
        /// <summary>En az turda kaçış sağlayan eylem dizisi; yoksa null.</summary>
        public static List<NetAction> Solve(NetOperation start, NetSolveOptions options = null)
        {
            options = options ?? new NetSolveOptions();
            var seen = new HashSet<string> { start.StateKey() };
            var queue = new Queue<KeyValuePair<NetOperation, List<NetAction>>>();
            queue.Enqueue(new KeyValuePair<NetOperation, List<NetAction>>(start.Clone(), new List<NetAction>()));

            while (queue.Count > 0 && seen.Count < options.MaxStates)
            {
                var entry = queue.Dequeue();
                var state = entry.Key;
                if (state.Turn >= options.MaxTurns) continue;

                foreach (var action in Actions(state, options))
                {
                    var next = state.Clone();
                    if (!next.Execute(action).Accepted) continue;
                    if (next.Status == NetStatus.Caught) continue;
                    if (!options.AllowDetection && next.Detections > 0) continue;

                    var path = new List<NetAction>(entry.Value) { action };
                    if (next.Status == NetStatus.Escaped) return path;
                    if (!seen.Add(next.StateKey())) continue;
                    queue.Enqueue(new KeyValuePair<NetOperation, List<NetAction>>(next, path));
                }
            }

            return null;
        }

        private static IEnumerable<NetAction> Actions(NetOperation state, NetSolveOptions options)
        {
            foreach (var neighbor in state.Map.Neighbors(state.PlayerNode)) yield return NetAction.MoveTo(neighbor);
            yield return NetAction.Wait;
            if (options.AllowDecoy && state.DecoysLeft > 0 && state.DecoyNode == null) yield return NetAction.DropDecoy;
        }
    }
}
