using System;
using System.Collections.Generic;
using System.Linq;

namespace AIGame.Core.Energy
{
    public enum GridNodeKind
    {
        Source,
        Junction,
        Load
    }

    /// <summary>Şebekedeki bir düğüm: trafo merkezi, kavşak ya da tüketici (yük).</summary>
    public sealed class GridNode
    {
        public GridNode(string id, string name, GridNodeKind kind, int demand = 0)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Düğüm kimliği boş olamaz.", nameof(id));
            if (demand < 0) throw new ArgumentOutOfRangeException(nameof(demand));
            Id = id;
            Name = name;
            Kind = kind;
            Demand = demand;
            Enabled = true;
            Identified = true;
        }

        public string Id { get; }

        /// <summary>Gerçek adı. Tanımlanmamışsa oyuncuya "?" gösterilir.</summary>
        public string Name { get; }

        public GridNodeKind Kind { get; }

        /// <summary>Yükün tükettiği güç (birim). Kaynak ve kavşaklarda 0.</summary>
        public int Demand { get; }

        /// <summary>Yük devrede mi? Yük atıldığında false olur.</summary>
        public bool Enabled { get; internal set; }

        /// <summary>A.I. bu binayı tanıyor mu?</summary>
        public bool Identified { get; set; }

        /// <summary>Kritik altyapı (hastane, trafik ışıkları). Bilgi amaçlı.</summary>
        public bool Critical { get; set; }

        /// <summary>Fedakârlığın yüzü: bu yükün arkasındaki insanlar.</summary>
        public string Detail { get; set; } = "";

        /// <summary>A.I.'ın bu yükü atması ne kadar şüphe uyandırır.</summary>
        public int ShedSuspicion { get; set; } = 1;

        public string DisplayName => Identified ? Name : "?";

        internal GridNode CloneNode()
        {
            return new GridNode(Id, Name, Kind, Demand)
            {
                Enabled = Enabled,
                Identified = Identified,
                Critical = Critical,
                Detail = Detail,
                ShedSuspicion = ShedSuspicion
            };
        }
    }

    /// <summary>İki düğüm arasındaki hat. Kapalı hat enerji iletir, açık hat iletmez.</summary>
    public sealed class GridLine
    {
        public GridLine(string id, string name, string fromId, string toId, int capacity, bool closed, bool switchable)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Hat kimliği boş olamaz.", nameof(id));
            if (fromId == toId) throw new ArgumentException("Hat aynı düğüme bağlanamaz.");
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Id = id;
            Name = name;
            FromId = fromId;
            ToId = toId;
            Capacity = capacity;
            Closed = closed;
            Switchable = switchable;
        }

        public string Id { get; }
        public string Name { get; }
        public string FromId { get; }
        public string ToId { get; }
        public int Capacity { get; }

        /// <summary>Anahtar kapalı mı (enerji geçebilir mi)?</summary>
        public bool Closed { get; internal set; }

        /// <summary>Oyuncu bu hattı açıp kapatabilir mi?</summary>
        public bool Switchable { get; }

        /// <summary>Fiziksel arıza: kapatılamaz.</summary>
        public bool Faulted { get; internal set; }

        /// <summary>Aşırı yükten sigorta attı. Yeniden kapatmak "sıfırlama" sayılır.</summary>
        public bool Tripped { get; internal set; }

        /// <summary>Otomatik transfer hattı: kapanması otomasyon gibi görünür.</summary>
        public bool AutomaticTransfer { get; set; }

        public int OpenSuspicion { get; set; } = 1;
        public int CloseSuspicion { get; set; } = 1;

        /// <summary>Enerji iletebilir durumda mı?</summary>
        public bool IsLive => Closed && !Faulted;

        public bool Touches(string nodeId) => FromId == nodeId || ToId == nodeId;

        public string OtherEnd(string nodeId)
        {
            if (FromId == nodeId) return ToId;
            if (ToId == nodeId) return FromId;
            throw new ArgumentException("Hat bu düğüme bağlı değil: " + nodeId);
        }

        internal GridLine CloneLine()
        {
            return new GridLine(Id, Name, FromId, ToId, Capacity, Closed, Switchable)
            {
                Faulted = Faulted,
                Tripped = Tripped,
                AutomaticTransfer = AutomaticTransfer,
                OpenSuspicion = OpenSuspicion,
                CloseSuspicion = CloseSuspicion
            };
        }
    }

    /// <summary>
    /// Radyal (ağaç yapılı) dağıtım şebekesi. Kapalı hatlar döngü oluşturamaz;
    /// her yük tek bir yoldan beslenir. Akış, alt ağaçtaki devredeki yüklerin toplamıdır.
    /// </summary>
    public sealed class PowerGrid
    {
        private readonly List<GridNode> _nodes = new List<GridNode>();
        private readonly List<GridLine> _lines = new List<GridLine>();
        private readonly Dictionary<string, GridNode> _nodeById = new Dictionary<string, GridNode>();
        private readonly Dictionary<string, GridLine> _lineById = new Dictionary<string, GridLine>();

        public IReadOnlyList<GridNode> Nodes => _nodes;
        public IReadOnlyList<GridLine> Lines => _lines;

        public GridNode AddNode(GridNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (_nodeById.ContainsKey(node.Id) || _lineById.ContainsKey(node.Id))
                throw new InvalidOperationException("Kimlik zaten kullanılıyor: " + node.Id);
            _nodes.Add(node);
            _nodeById.Add(node.Id, node);
            return node;
        }

        public GridLine AddLine(GridLine line)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (_nodeById.ContainsKey(line.Id) || _lineById.ContainsKey(line.Id))
                throw new InvalidOperationException("Kimlik zaten kullanılıyor: " + line.Id);
            if (!_nodeById.ContainsKey(line.FromId)) throw new InvalidOperationException("Bilinmeyen düğüm: " + line.FromId);
            if (!_nodeById.ContainsKey(line.ToId)) throw new InvalidOperationException("Bilinmeyen düğüm: " + line.ToId);
            _lines.Add(line);
            _lineById.Add(line.Id, line);
            if (line.IsLive && CreatesLoopExcluding(line))
                throw new InvalidOperationException("Kapalı hat döngü oluşturuyor: " + line.Id);
            return line;
        }

        public GridNode Node(string id)
        {
            if (!_nodeById.TryGetValue(id, out var node)) throw new KeyNotFoundException("Düğüm yok: " + id);
            return node;
        }

        public GridLine Line(string id)
        {
            if (!_lineById.TryGetValue(id, out var line)) throw new KeyNotFoundException("Hat yok: " + id);
            return line;
        }

        public bool HasNode(string id) => _nodeById.ContainsKey(id);
        public bool HasLine(string id) => _lineById.ContainsKey(id);

        /// <summary>Hattı kalıcı olarak arızalı yapar (ör. trafo yandı).</summary>
        public void SetFault(string lineId)
        {
            var line = Line(lineId);
            line.Faulted = true;
            line.Closed = false;
        }

        internal void SetLineClosed(GridLine line, bool closed)
        {
            line.Closed = closed;
            if (closed) line.Tripped = false;
        }

        internal void SetLoadEnabled(GridNode node, bool enabled)
        {
            node.Enabled = enabled;
        }

        /// <summary>
        /// Bu hat kapatılırsa iki besleme paralel bağlanır ya da bir döngü oluşur mu?
        /// Tüm kaynaklar ortak bir "toprak" düğümüne bağlı sayılır; böylece iki farklı
        /// kaynağı birleştirmek de döngü kabul edilir.
        /// </summary>
        public bool WouldCreateLoop(string lineId) => CreatesLoopExcluding(Line(lineId));

        private bool CreatesLoopExcluding(GridLine candidate)
        {
            var parent = new Dictionary<string, string>();
            const string ground = "\u0000ground";
            parent[ground] = ground;
            foreach (var node in _nodes)
                parent[node.Id] = node.Kind == GridNodeKind.Source ? ground : node.Id;

            string Find(string x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            foreach (var line in _lines)
            {
                if (line == candidate || !line.IsLive) continue;
                var a = Find(line.FromId);
                var b = Find(line.ToId);
                if (a != b) parent[a] = b;
            }

            return Find(candidate.FromId) == Find(candidate.ToId);
        }

        /// <summary>Şebekenin anlık durumunu hesaplar: hangi düğüm enerjili, hangi hattan ne akıyor.</summary>
        public GridEvaluation Evaluate()
        {
            var energized = new HashSet<string>();
            var depth = new Dictionary<string, int>();
            var parentLine = new Dictionary<string, GridLine>();
            var order = new List<string>();

            var adjacency = new Dictionary<string, List<GridLine>>();
            foreach (var node in _nodes) adjacency[node.Id] = new List<GridLine>();
            foreach (var line in _lines)
            {
                if (!line.IsLive) continue;
                adjacency[line.FromId].Add(line);
                adjacency[line.ToId].Add(line);
            }

            var queue = new Queue<string>();
            foreach (var source in _nodes.Where(n => n.Kind == GridNodeKind.Source))
            {
                energized.Add(source.Id);
                depth[source.Id] = 0;
                order.Add(source.Id);
                queue.Enqueue(source.Id);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var line in adjacency[current])
                {
                    var next = line.OtherEnd(current);
                    if (energized.Contains(next)) continue;
                    energized.Add(next);
                    depth[next] = depth[current] + 1;
                    parentLine[next] = line;
                    order.Add(next);
                    queue.Enqueue(next);
                }
            }

            var subtree = new Dictionary<string, int>();
            foreach (var id in order) subtree[id] = 0;
            var flows = new Dictionary<string, int>();
            var lineDepth = new Dictionary<string, int>();

            for (var i = order.Count - 1; i >= 0; i--)
            {
                var id = order[i];
                var node = _nodeById[id];
                if (node.Kind == GridNodeKind.Load && node.Enabled) subtree[id] += node.Demand;
                if (parentLine.TryGetValue(id, out var feeder))
                {
                    flows[feeder.Id] = subtree[id];
                    lineDepth[feeder.Id] = depth[id];
                    subtree[feeder.OtherEnd(id)] += subtree[id];
                }
            }

            return new GridEvaluation(this, energized, flows, lineDepth, subtree);
        }

        /// <summary>
        /// Aşırı yüklü hatları sigortalarıyla açar. Önce en alttaki (yüke en yakın) aşırı yüklü
        /// hatlar atar, sonra durum yeniden hesaplanır. Atan hatları sırasıyla döndürür.
        /// </summary>
        public IReadOnlyList<GridLine> ResolveOverloads()
        {
            var tripped = new List<GridLine>();
            for (var guard = 0; guard <= _lines.Count; guard++)
            {
                var evaluation = Evaluate();
                var overloaded = _lines.Where(l => l.IsLive && evaluation.FlowOf(l.Id) > l.Capacity).ToList();
                if (overloaded.Count == 0) return tripped;

                var deepest = overloaded.Max(l => evaluation.DepthOf(l.Id));
                foreach (var line in overloaded.Where(l => evaluation.DepthOf(l.Id) == deepest))
                {
                    line.Closed = false;
                    line.Tripped = true;
                    tripped.Add(line);
                }
            }

            throw new InvalidOperationException("Aşırı yük çözümlemesi sonlanmadı.");
        }

        public PowerGrid Clone()
        {
            var copy = new PowerGrid();
            foreach (var node in _nodes) copy.AddNode(node.CloneNode());
            foreach (var line in _lines)
            {
                var lineCopy = line.CloneLine();
                copy._lines.Add(lineCopy);
                copy._lineById.Add(lineCopy.Id, lineCopy);
            }
            return copy;
        }
    }

    /// <summary>Şebekenin belirli bir andaki durumu. Şebeke değişince yeniden hesaplanmalıdır.</summary>
    public sealed class GridEvaluation
    {
        private readonly PowerGrid _grid;
        private readonly HashSet<string> _energized;
        private readonly Dictionary<string, int> _flows;
        private readonly Dictionary<string, int> _lineDepth;
        private readonly Dictionary<string, int> _subtreeLoad;

        internal GridEvaluation(PowerGrid grid, HashSet<string> energized, Dictionary<string, int> flows,
            Dictionary<string, int> lineDepth, Dictionary<string, int> subtreeLoad)
        {
            _grid = grid;
            _energized = energized;
            _flows = flows;
            _lineDepth = lineDepth;
            _subtreeLoad = subtreeLoad;
        }

        public bool IsEnergized(string nodeId) => _energized.Contains(nodeId);

        /// <summary>Yük enerjili ve devrede mi? (Binanın ışıkları yanıyor mu?)</summary>
        public bool IsPowered(string nodeId)
        {
            var node = _grid.Node(nodeId);
            return _energized.Contains(nodeId) && (node.Kind != GridNodeKind.Load || node.Enabled);
        }

        public int FlowOf(string lineId) => _flows.TryGetValue(lineId, out var flow) ? flow : 0;

        public int DepthOf(string lineId) => _lineDepth.TryGetValue(lineId, out var d) ? d : 0;

        /// <summary>Bu düğümün altındaki toplam yük (kaynakta bölgenin toplam tüketimi).</summary>
        public int LoadBelow(string nodeId) => _subtreeLoad.TryGetValue(nodeId, out var load) ? load : 0;

        public float LoadRatio(string lineId)
        {
            var line = _grid.Line(lineId);
            return line.Capacity <= 0 ? 0f : (float)FlowOf(lineId) / line.Capacity;
        }

        public IEnumerable<GridLine> OverloadedLines() =>
            _grid.Lines.Where(l => l.IsLive && FlowOf(l.Id) > l.Capacity);
    }
}
