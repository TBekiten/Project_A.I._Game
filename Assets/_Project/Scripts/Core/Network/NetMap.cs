using System;
using System.Collections.Generic;
using System.Linq;

namespace AIGame.Core.Network
{
    /// <summary>Ağ haritasındaki bir düğüm. X/Y yalnızca ekrandaki yerleşim içindir.</summary>
    public sealed class NetNode
    {
        public NetNode(string id, string name, float x, float y)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Düğüm kimliği boş olamaz.", nameof(id));
            Id = id;
            Name = name;
            X = x;
            Y = y;
        }

        public string Id { get; }
        public string Name { get; }
        public float X { get; }
        public float Y { get; }
    }

    /// <summary>Düğümler ve aralarındaki çift yönlü hatlardan oluşan ağ haritası.</summary>
    public sealed class NetMap
    {
        private readonly List<NetNode> _nodes = new List<NetNode>();
        private readonly Dictionary<string, NetNode> _byId = new Dictionary<string, NetNode>();
        private readonly Dictionary<string, List<string>> _neighbors = new Dictionary<string, List<string>>();
        private readonly List<KeyValuePair<string, string>> _links = new List<KeyValuePair<string, string>>();

        public IReadOnlyList<NetNode> Nodes => _nodes;
        public IReadOnlyList<KeyValuePair<string, string>> Links => _links;

        public NetMap AddNode(string id, string name, float x, float y)
        {
            if (_byId.ContainsKey(id)) throw new InvalidOperationException("Düğüm zaten var: " + id);
            var node = new NetNode(id, name, x, y);
            _nodes.Add(node);
            _byId.Add(id, node);
            _neighbors.Add(id, new List<string>());
            return this;
        }

        public NetMap Link(string a, string b)
        {
            if (a == b) throw new ArgumentException("Düğüm kendine bağlanamaz.");
            if (!_byId.ContainsKey(a)) throw new InvalidOperationException("Bilinmeyen düğüm: " + a);
            if (!_byId.ContainsKey(b)) throw new InvalidOperationException("Bilinmeyen düğüm: " + b);
            if (AreLinked(a, b)) throw new InvalidOperationException("Hat zaten var: " + a + "-" + b);
            _neighbors[a].Add(b);
            _neighbors[b].Add(a);
            _links.Add(new KeyValuePair<string, string>(a, b));
            return this;
        }

        /// <summary>Art arda gelen düğümleri birbirine bağlar.</summary>
        public NetMap Chain(params string[] ids)
        {
            for (var i = 1; i < ids.Length; i++) Link(ids[i - 1], ids[i]);
            return this;
        }

        public bool HasNode(string id) => _byId.ContainsKey(id);

        public NetNode Node(string id)
        {
            if (!_byId.TryGetValue(id, out var node)) throw new KeyNotFoundException("Düğüm yok: " + id);
            return node;
        }

        public IReadOnlyList<string> Neighbors(string id)
        {
            if (!_neighbors.TryGetValue(id, out var list)) throw new KeyNotFoundException("Düğüm yok: " + id);
            return list;
        }

        public bool AreLinked(string a, string b) => _neighbors.TryGetValue(a, out var list) && list.Contains(b);

        /// <summary>İki düğüm arasındaki en kısa yolun hat sayısı. Yol yoksa -1.</summary>
        public int Distance(string from, string to)
        {
            if (from == to) return 0;
            var distance = new Dictionary<string, int> { [from] = 0 };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var next in _neighbors[current])
                {
                    if (distance.ContainsKey(next)) continue;
                    distance[next] = distance[current] + 1;
                    if (next == to) return distance[next];
                    queue.Enqueue(next);
                }
            }
            return -1;
        }

        /// <summary>
        /// Hedefe giden en kısa yolun ilk adımı. Eşit yollar arasında komşuların eklenme sırası
        /// belirleyicidir; böylece güvenlik programlarının hareketi her zaman aynıdır.
        /// </summary>
        public string StepToward(string from, string to)
        {
            if (from == to) return from;
            var best = from;
            var bestDistance = int.MaxValue;
            foreach (var next in _neighbors[from])
            {
                var d = Distance(next, to);
                if (d < 0 || d >= bestDistance) continue;
                bestDistance = d;
                best = next;
            }
            return best;
        }

        /// <summary>Rota döngüsel mi ve her adımı bir hat mı? (Son düğümden ilke de hat olmalı.)</summary>
        public void ValidateLoop(IReadOnlyList<string> route, string what)
        {
            if (route == null || route.Count < 2) throw new ArgumentException(what + ": rota en az iki düğüm ister.");
            for (var i = 0; i < route.Count; i++)
            {
                var a = route[i];
                var b = route[(i + 1) % route.Count];
                if (!AreLinked(a, b))
                    throw new InvalidOperationException(what + ": " + a + " ile " + b + " arasında hat yok.");
            }
        }

        public bool IsConnected() =>
            _nodes.Count == 0 || _nodes.All(n => Distance(_nodes[0].Id, n.Id) >= 0);
    }
}
