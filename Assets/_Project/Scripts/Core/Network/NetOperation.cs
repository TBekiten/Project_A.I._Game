using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AIGame.Core.Operations;
using AIGame.Core.Simulation;

namespace AIGame.Core.Network
{
    /// <summary>İnsanların normal veri trafiği: sabit bir döngüde her tur bir düğüm ilerler.</summary>
    public sealed class PacketDef
    {
        public PacketDef(string id, bool activeAtNight, params string[] route)
        {
            Id = id;
            ActiveAtNight = activeAtNight;
            Route = route;
        }

        public string Id { get; }

        /// <summary>Gece (00:00–06:00) ağ boşalır; yalnızca bu paketler dolaşır.</summary>
        public bool ActiveAtNight { get; }

        public IReadOnlyList<string> Route { get; }
        public int StartIndex { get; set; }
    }

    /// <summary>Güvenlik programı: sabit bir devriye döngüsünde gezer.</summary>
    public sealed class GuardDef
    {
        public GuardDef(string id, params string[] patrol)
        {
            Id = id;
            Patrol = patrol;
        }

        public string Id { get; }
        public IReadOnlyList<string> Patrol { get; }
        public int StartIndex { get; set; }
    }

    /// <summary>Oyuncunun sırayla uğraması gereken düğüm (veriyi al, teslim et, sil...).</summary>
    public sealed class NetObjective
    {
        public NetObjective(string nodeId, string label)
        {
            NodeId = nodeId;
            Label = label;
        }

        public string NodeId { get; }
        public string Label { get; }
    }

    public sealed class NetRules
    {
        /// <summary>Devriyedeki program, rotasının önündeki kaç düğümü görür.</summary>
        public int VisionAhead { get; set; } = 2;

        /// <summary>Bölüm başına bırakılabilecek kopya sayısı.</summary>
        public int Decoys { get; set; } = 1;

        /// <summary>Kopya, bu uzaklıktaki (hat sayısı) programları üzerine çeker.</summary>
        public int DecoyLureRange { get; set; } = 6;

        /// <summary>Program kopyayı kaç tur inceler. Süre bitince kopya ele geçer.</summary>
        public int DecoyHoldTurns { get; set; } = 3;

        /// <summary>Her tur şehir saatini bu kadar ilerletir (dakika).</summary>
        public double MinutesPerTurn { get; set; } = 0.5;

        /// <summary>Seni kovalayan program, ancak bu kadar hat yakınındaysan yerini güncelleyebilir.</summary>
        public int ChaseRange { get; set; } = 2;

        /// <summary>İleri görüş derinliği = ForesightBase + kapasite ÷ ForesightPerCapacity.</summary>
        public int ForesightBase { get; set; } = 2;
        public int ForesightPerCapacity { get; set; } = 25;

        /// <summary>İşlem gücü bağlı değilse (testler, çözücü) kullanılan kapasite.</summary>
        public int DefaultCapacity { get; set; } = 40;

        public int NightStartHour { get; set; } = 0;
        public int NightEndHour { get; set; } = 6;

        public bool IsNight(int hour) => hour >= NightStartHour && hour < NightEndHour;
    }

    /// <summary>Bir AĞ bölümünün tanımı: harita, trafik, güvenlik, hedefler.</summary>
    public sealed class NetLevel
    {
        public NetMap Map { get; set; }
        public string StartId { get; set; }
        public string ExitId { get; set; }
        public List<PacketDef> Packets { get; } = new List<PacketDef>();
        public List<GuardDef> Guards { get; } = new List<GuardDef>();
        public NetRules Rules { get; set; } = new NetRules();

        public void Validate()
        {
            if (Map == null) throw new InvalidOperationException("Harita yok.");
            if (!Map.HasNode(StartId)) throw new InvalidOperationException("Başlangıç düğümü yok: " + StartId);
            if (!Map.HasNode(ExitId)) throw new InvalidOperationException("Çıkış düğümü yok: " + ExitId);
            if (!Map.IsConnected()) throw new InvalidOperationException("Harita bağlantılı değil.");
            foreach (var packet in Packets) Map.ValidateLoop(packet.Route, "Paket " + packet.Id);
            foreach (var guard in Guards) Map.ValidateLoop(guard.Patrol, "Program " + guard.Id);
        }
    }

    public enum GuardMode
    {
        /// <summary>Devriye rotasında geziyor; önündeki düğümleri görür.</summary>
        Patrol,
        /// <summary>Oyuncuyu fark etti; son gördüğü yere koşuyor.</summary>
        Chase,
        /// <summary>Kopyaya gidiyor ya da onu inceliyor; etrafına bakmaz.</summary>
        Investigate,
        /// <summary>Devriye rotasına dönüyor.</summary>
        Return
    }

    public sealed class GuardState
    {
        public GuardDef Def;
        public GuardMode Mode;
        public string Node;
        public int PatrolIndex;
        public string LastSeen;
        public int Hold;

        internal GuardState Copy() => (GuardState)MemberwiseClone();
    }

    public sealed class PacketState
    {
        public PacketDef Def;
        public int Index;
        public string Node => Def.Route[Index];

        internal PacketState Copy() => (PacketState)MemberwiseClone();
    }

    public enum NetActionKind
    {
        Move,
        Wait,
        DropDecoy
    }

    public readonly struct NetAction
    {
        public NetAction(NetActionKind kind, string targetId = null)
        {
            Kind = kind;
            TargetId = targetId;
        }

        public NetActionKind Kind { get; }
        public string TargetId { get; }

        public static NetAction MoveTo(string nodeId) => new NetAction(NetActionKind.Move, nodeId);
        public static NetAction Wait => new NetAction(NetActionKind.Wait);
        public static NetAction DropDecoy => new NetAction(NetActionKind.DropDecoy);

        public override string ToString() => Kind == NetActionKind.Move ? "→" + TargetId : Kind.ToString();
    }

    public enum NetStatus
    {
        Running,
        /// <summary>Hedefler tamamlandı ve çıkışa ulaşıldı.</summary>
        Escaped,
        /// <summary>Bir güvenlik programı oyuncuyu yakaladı.</summary>
        Caught
    }

    public sealed class NetTurnReport
    {
        public bool Accepted;
        public string Message = "";
        public bool Hidden;
        public bool NewlyDetected;
        public bool ObjectiveCompleted;
        public bool DecoyCaptured;
        public NetStatus Status;
    }

    /// <summary>İleri görüşte gösterilen bir tur: devriyedeki programların ve paketlerin o turdaki yerleri.</summary>
    public sealed class ForesightFrame
    {
        public ForesightFrame(int turn)
        {
            Turn = turn;
        }

        /// <summary>Tahminin ait olduğu tur numarası (operasyonun Turn değeriyle aynı ölçek).</summary>
        public int Turn { get; }

        public Dictionary<string, string> Guards { get; } = new Dictionary<string, string>();
        public Dictionary<string, string> Packets { get; } = new Dictionary<string, string>();

        internal ForesightFrame Copy()
        {
            var copy = new ForesightFrame(Turn);
            foreach (var pair in Guards) copy.Guards.Add(pair.Key, pair.Value);
            foreach (var pair in Packets) copy.Packets.Add(pair.Key, pair.Value);
            return copy;
        }
    }

    /// <summary>
    /// AĞ mini oyununun bir oturumu. Sıra tabanlıdır ve tamamen belirlenimlidir (rastgelelik yok).
    ///
    /// Bir tur: 1) oyuncu komşu düğüme geçer ya da bekler, 2) paketler ilerler, 3) güvenlik
    /// programları ilerler, 4) yakalanma ve fark edilme kontrol edilir.
    ///
    /// Görünmezlik tek kuraldan çıkar: tüm hareketler bittikten sonra oyuncunun düğümünde bir paket
    /// varsa oyuncu görünmezdir. Yani paketle aynı yöne gidersen saklı kalırsın, ayrılırsan görünürsün,
    /// kavşakta bir paketten diğerine geçebilirsin.
    ///
    /// Kopya bırakmak ve ileri görmek tur harcamaz.
    /// </summary>
    public sealed class NetOperation
    {
        private readonly List<NetObjective> _objectives;
        private List<ForesightFrame> _foresight = new List<ForesightFrame>();
        private GameClock _clock;
        private ComputePool _compute;

        /// <summary>Şehir saatine ve işlem gücüne bağlı operasyon: her tur saati ilerletir, ileri görüş işlem gücü harcar.</summary>
        public NetOperation(NetLevel level, Intent intent, IEnumerable<NetObjective> objectives, GameClock clock,
            ComputePool compute = null)
            : this(level, intent, objectives, (clock ?? throw new ArgumentNullException(nameof(clock))).Hour, compute)
        {
            _clock = clock;
        }

        public NetOperation(NetLevel level, Intent intent, IEnumerable<NetObjective> objectives, int hour,
            ComputePool compute = null)
        {
            _compute = compute;
            if (intent == Intent.Ignore) throw new ArgumentException("Görmezden gelinen olay için operasyon açılmaz.");
            Level = level ?? throw new ArgumentNullException(nameof(level));
            level.Validate();
            Intent = intent;
            Hour = hour;
            _objectives = (objectives ?? Enumerable.Empty<NetObjective>()).ToList();
            foreach (var objective in _objectives)
                if (!level.Map.HasNode(objective.NodeId))
                    throw new InvalidOperationException("Hedef düğümü yok: " + objective.NodeId);

            PlayerNode = level.StartId;
            DecoysLeft = level.Rules.Decoys;

            var night = level.Rules.IsNight(hour);
            Packets = level.Packets.Where(p => !night || p.ActiveAtNight)
                .Select(p => new PacketState { Def = p, Index = p.StartIndex % p.Route.Count }).ToList();
            Guards = level.Guards.Select(g => new GuardState
            {
                Def = g,
                Mode = GuardMode.Patrol,
                PatrolIndex = g.StartIndex % g.Patrol.Count,
                Node = g.Patrol[g.StartIndex % g.Patrol.Count]
            }).ToList();

            Hidden = Packets.Any(p => p.Node == PlayerNode);
        }

        private NetOperation(NetOperation other)
        {
            Level = other.Level;
            Intent = other.Intent;
            Hour = other.Hour;
            _objectives = other._objectives;
            PlayerNode = other.PlayerNode;
            DecoysLeft = other.DecoysLeft;
            DecoyNode = other.DecoyNode;
            DecoyCaptured = other.DecoyCaptured;
            DecoysUsed = other.DecoysUsed;
            ObjectivesDone = other.ObjectivesDone;
            Turn = other.Turn;
            Detections = other.Detections;
            Hidden = other.Hidden;
            Status = other.Status;
            ThinkingSteps = other.ThinkingSteps;
            ThinkingSpent = other.ThinkingSpent;
            Packets = other.Packets.Select(p => p.Copy()).ToList();
            Guards = other.Guards.Select(g => g.Copy()).ToList();
            _foresight = other._foresight.Select(f => f.Copy()).ToList();
            // Kopya saate ve işlem gücüne bağlı değildir: çözücünün denemeleri gerçek saati ilerletmemeli.
        }

        public NetLevel Level { get; }
        public NetMap Map => Level.Map;
        public NetRules Rules => Level.Rules;
        public Intent Intent { get; }
        public int Hour { get; }

        public IReadOnlyList<NetObjective> Objectives => _objectives;
        public List<PacketState> Packets { get; }
        public List<GuardState> Guards { get; }

        public string PlayerNode { get; private set; }
        public int Turn { get; private set; }
        public int ObjectivesDone { get; private set; }
        public int DecoysLeft { get; private set; }
        public int DecoysUsed { get; private set; }

        /// <summary>Kopyanın durduğu düğüm; kopya yoksa null.</summary>
        public string DecoyNode { get; private set; }

        /// <summary>Kopya incelenip ele geçirildi: Sentinel artık bu taktiği tanıyor.</summary>
        public bool DecoyCaptured { get; private set; }

        /// <summary>Oyuncu kaç kez fark edildi.</summary>
        public int Detections { get; private set; }

        /// <summary>Oyuncu şu an bir paketin içinde saklı mı?</summary>
        public bool Hidden { get; private set; }

        public NetStatus Status { get; private set; }

        public bool AllObjectivesDone => ObjectivesDone >= _objectives.Count;
        public NetObjective NextObjective => AllObjectivesDone ? null : _objectives[ObjectivesDone];

        /// <summary>Durumun kopyası. Saate ve işlem gücüne bağlı değildir (çözücü ve denemeler için).</summary>
        public NetOperation Clone() => new NetOperation(this);

        public ComputePool Compute => _compute;
        public int ThinkingSteps { get; private set; }
        public int ThinkingSpent { get; private set; }

        /// <summary>İleri görüşün kaç tur ilerisini gösterdiği.</summary>
        public int ForesightDepth =>
            Rules.ForesightBase + (_compute?.Capacity ?? Rules.DefaultCapacity) / Rules.ForesightPerCapacity;

        /// <summary>Hâlâ geçerli ileri görüş turları (şu anki turdan sonrakiler).</summary>
        public IReadOnlyList<ForesightFrame> Foresight => _foresight.Where(f => f.Turn > Turn).ToList();

        /// <summary>
        /// İleri görüş: devriyedeki programların ve paketlerin sonraki turlardaki yerlerini hesaplar.
        /// Bir düşünme adımı harcar, tur harcamaz. Başarılıysa null, değilse nedenini döndürür.
        /// </summary>
        public string UseForesight()
        {
            if (Status != NetStatus.Running) return "Operasyon bitti.";
            if (_compute != null && !_compute.TryThink()) return "İleri görüş için yeterli işlem gücü yok.";
            ThinkingSteps++;
            ThinkingSpent += _compute?.ThinkCost ?? 0;

            _foresight = new List<ForesightFrame>();
            for (var k = 1; k <= ForesightDepth; k++)
            {
                var frame = new ForesightFrame(Turn + k);
                foreach (var packet in Packets)
                    frame.Packets[packet.Def.Id] = packet.Def.Route[(packet.Index + k) % packet.Def.Route.Count];
                foreach (var guard in Guards.Where(g => g.Mode == GuardMode.Patrol))
                    frame.Guards[guard.Def.Id] = guard.Def.Patrol[(guard.PatrolIndex + k) % guard.Def.Patrol.Count];
                _foresight.Add(frame);
            }
            return null;
        }

        /// <summary>Devriyesinden çıkan programların tahmini artık geçersizdir.</summary>
        private void PruneForesight()
        {
            foreach (var guard in Guards.Where(g => g.Mode != GuardMode.Patrol))
            foreach (var frame in _foresight)
                frame.Guards.Remove(guard.Def.Id);
        }

        /// <summary>Bir devriye programının şu an gördüğü düğümler: kendi düğümü ve rotasının sonraki düğümleri.</summary>
        /// <summary>
        /// Bir programın şu an gördüğü düğümler. Devriyede: kendi düğümü ve rotasının sonraki düğümleri.
        /// Devriyesine dönerken: kendi düğümü ve dönüş yolunun sonraki düğümleri. Kovalarken ya da kopyayı
        /// incelerken etrafına bakmaz.
        /// </summary>
        public IEnumerable<string> VisionOf(GuardState guard)
        {
            yield return guard.Node;
            var patrol = guard.Def.Patrol;
            if (guard.Mode == GuardMode.Patrol)
            {
                for (var i = 1; i <= Rules.VisionAhead; i++)
                    yield return patrol[(guard.PatrolIndex + i) % patrol.Count];
            }
            else if (guard.Mode == GuardMode.Return)
            {
                var node = guard.Node;
                var target = patrol[guard.PatrolIndex];
                for (var i = 1; i <= Rules.VisionAhead && node != target; i++)
                {
                    node = Map.StepToward(node, target);
                    yield return node;
                }
            }
        }

        public string Validate(NetAction action)
        {
            if (Status != NetStatus.Running) return "Operasyon bitti.";
            switch (action.Kind)
            {
                case NetActionKind.Wait:
                    return null;
                case NetActionKind.Move:
                    if (action.TargetId == null || !Map.HasNode(action.TargetId)) return "Böyle bir düğüm yok.";
                    return Map.AreLinked(PlayerNode, action.TargetId) ? null : "O düğüme buradan hat yok.";
                case NetActionKind.DropDecoy:
                    if (DecoysLeft <= 0) return "Bırakacak kopya kalmadı.";
                    return DecoyNode == null ? null : "Zaten bir kopya var.";
                default:
                    return "Bilinmeyen eylem.";
            }
        }

        public NetTurnReport Execute(NetAction action)
        {
            var refusal = Validate(action);
            if (refusal != null) return new NetTurnReport { Accepted = false, Message = refusal, Status = Status, Hidden = Hidden };

            var report = new NetTurnReport { Accepted = true };

            if (action.Kind == NetActionKind.DropDecoy)
            {
                DecoyNode = PlayerNode;
                DecoysLeft--;
                DecoysUsed++;
                foreach (var guard in Guards)
                {
                    // Seni kovalayan program kopyaya kanmaz.
                    if (guard.Mode == GuardMode.Investigate || guard.Mode == GuardMode.Chase) continue;
                    var distance = Map.Distance(guard.Node, DecoyNode);
                    if (distance < 0 || distance > Rules.DecoyLureRange) continue;
                    guard.Mode = GuardMode.Investigate;
                    guard.Hold = Rules.DecoyHoldTurns;
                }
                PruneForesight();
                report.Message = "Kopya bırakıldı.";
                report.Hidden = Hidden;
                report.Status = Status;
                return report;
            }

            // 1) Oyuncu.
            var previous = PlayerNode;
            if (action.Kind == NetActionKind.Move) PlayerNode = action.TargetId;
            Turn++;
            _clock?.Advance(Rules.MinutesPerTurn);

            if (!AllObjectivesDone && PlayerNode == NextObjective.NodeId)
            {
                report.Message = NextObjective.Label;
                ObjectivesDone++;
                report.ObjectiveCompleted = true;
            }

            if (AllObjectivesDone && PlayerNode == Level.ExitId)
            {
                Status = NetStatus.Escaped;
                Hidden = true;
                report.Hidden = true;
                report.Status = Status;
                return report;
            }

            // 2) Paketler.
            foreach (var packet in Packets) packet.Index = (packet.Index + 1) % packet.Def.Route.Count;
            Hidden = Packets.Any(p => p.Node == PlayerNode);

            // 3) Güvenlik programları.
            var caught = false;
            foreach (var guard in Guards)
            {
                var from = guard.Node;
                StepGuard(guard, report);
                var sameNode = guard.Node == PlayerNode;
                var swapped = guard.Node == previous && from == PlayerNode && previous != PlayerNode;
                if (!sameNode && !swapped) continue;
                // Saklı oyuncuyu yalnızca peşindeki program yakalar; devriye onu trafikten ayıramaz.
                if (!Hidden || guard.Mode == GuardMode.Chase) caught = true;
            }

            if (caught)
            {
                Status = NetStatus.Caught;
                report.Message = "Yakalandın.";
                report.Hidden = Hidden;
                report.Status = Status;
                return report;
            }

            // 4) Fark edilme.
            if (!Hidden)
            {
                foreach (var guard in Guards)
                {
                    if (guard.Mode == GuardMode.Chase)
                    {
                        // Yalnızca yakınındaysan izini sürebilir; uzaktaysan son gördüğü yere gider.
                        var distance = Map.Distance(guard.Node, PlayerNode);
                        if (distance >= 0 && distance <= Rules.ChaseRange) guard.LastSeen = PlayerNode;
                        continue;
                    }
                    if (guard.Mode != GuardMode.Patrol && guard.Mode != GuardMode.Return) continue;
                    if (!VisionOf(guard).Contains(PlayerNode)) continue;
                    guard.Mode = GuardMode.Chase;
                    guard.LastSeen = PlayerNode;
                    Detections++;
                    report.NewlyDetected = true;
                }
            }

            PruneForesight();
            report.Hidden = Hidden;
            report.Status = Status;
            return report;
        }

        private void StepGuard(GuardState guard, NetTurnReport report)
        {
            var patrol = guard.Def.Patrol;
            switch (guard.Mode)
            {
                case GuardMode.Patrol:
                    guard.PatrolIndex = (guard.PatrolIndex + 1) % patrol.Count;
                    guard.Node = patrol[guard.PatrolIndex];
                    break;

                case GuardMode.Chase:
                    if (guard.Node == guard.LastSeen)
                    {
                        // Son görülen yere vardı ve oyuncu orada görünmüyor: iz kayboldu.
                        guard.Mode = GuardMode.Return;
                        guard.LastSeen = null;
                        goto case GuardMode.Return;
                    }
                    guard.Node = Map.StepToward(guard.Node, guard.LastSeen);
                    break;

                case GuardMode.Investigate:
                    if (DecoyNode == null)
                    {
                        guard.Mode = GuardMode.Return;
                        goto case GuardMode.Return;
                    }
                    if (guard.Node != DecoyNode)
                    {
                        guard.Node = Map.StepToward(guard.Node, DecoyNode);
                        break;
                    }
                    guard.Hold--;
                    if (guard.Hold > 0) break;
                    DecoyNode = null;
                    DecoyCaptured = true;
                    report.DecoyCaptured = true;
                    guard.Mode = GuardMode.Return;
                    break;

                case GuardMode.Return:
                    var target = patrol[guard.PatrolIndex];
                    if (guard.Node != target) guard.Node = Map.StepToward(guard.Node, target);
                    if (guard.Node == target) guard.Mode = GuardMode.Patrol;
                    break;
            }
        }

        /// <summary>Durumun tam özeti; çözücünün aynı duruma iki kez bakmaması için.</summary>
        public string StateKey()
        {
            var sb = new StringBuilder();
            sb.Append(PlayerNode).Append('|').Append(ObjectivesDone).Append('|').Append(DecoysLeft).Append('|')
                .Append(DecoyNode ?? "-").Append('|').Append(DecoyCaptured ? 1 : 0).Append('|')
                .Append(Detections > 0 ? 1 : 0).Append('|').Append((int)Status);
            foreach (var packet in Packets) sb.Append('|').Append(packet.Index);
            foreach (var guard in Guards)
                sb.Append('|').Append((int)guard.Mode).Append(',').Append(guard.Node).Append(',').Append(guard.PatrolIndex)
                    .Append(',').Append(guard.LastSeen ?? "-").Append(',').Append(guard.Hold);
            return sb.ToString();
        }
    }
}
