using System;
using System.Collections.Generic;
using System.Linq;
using AIGame.Core.Operations;
using AIGame.Core.Simulation;

namespace AIGame.Core.Energy
{
    public enum GridCommandKind
    {
        OpenLine,
        CloseLine,
        ShedLoad,
        RestoreLoad
    }

    /// <summary>A.I.'ın şebekeye verdiği tek bir komut. Her komut sistem kayıtlarında iz bırakır.</summary>
    public readonly struct GridCommand
    {
        public GridCommand(GridCommandKind kind, string targetId)
        {
            Kind = kind;
            TargetId = targetId;
        }

        public GridCommandKind Kind { get; }
        public string TargetId { get; }

        public static GridCommand Open(string lineId) => new GridCommand(GridCommandKind.OpenLine, lineId);
        public static GridCommand Close(string lineId) => new GridCommand(GridCommandKind.CloseLine, lineId);
        public static GridCommand Shed(string loadId) => new GridCommand(GridCommandKind.ShedLoad, loadId);
        public static GridCommand Restore(string loadId) => new GridCommand(GridCommandKind.RestoreLoad, loadId);

        public override string ToString() => Kind + ":" + TargetId;
    }

    public sealed class CommandResult
    {
        public CommandResult(GridCommand command, bool accepted, string message, IReadOnlyList<GridLine> tripped,
            int suspicionAdded, double timeMinutes)
        {
            Command = command;
            Accepted = accepted;
            Message = message;
            Tripped = tripped;
            SuspicionAdded = suspicionAdded;
            TimeMinutes = timeMinutes;
        }

        public GridCommand Command { get; }

        /// <summary>Şebeke komutu kabul etti mi? Reddedilen komut iz bırakmaz.</summary>
        public bool Accepted { get; }

        public string Message { get; }
        public IReadOnlyList<GridLine> Tripped { get; }
        public int SuspicionAdded { get; }
        public double TimeMinutes { get; }

        public static CommandResult Refused(GridCommand command, string reason, double time) =>
            new CommandResult(command, false, reason, Array.Empty<GridLine>(), 0, time);
    }

    /// <summary>Önizlemede akışı değişen bir hat.</summary>
    public sealed class LinePreview
    {
        public LinePreview(string lineId, string name, int flowBefore, int flowAfter, int capacity, bool trips)
        {
            LineId = lineId;
            Name = name;
            FlowBefore = flowBefore;
            FlowAfter = flowAfter;
            Capacity = capacity;
            Trips = trips;
        }

        public string LineId { get; }
        public string Name { get; }
        public int FlowBefore { get; }
        public int FlowAfter { get; }
        public int Capacity { get; }

        /// <summary>Bu komutla hattın sigortası atar.</summary>
        public bool Trips { get; }
    }

    /// <summary>Önizlemede enerji durumu değişen bir yük.</summary>
    public sealed class LoadPreview
    {
        public LoadPreview(string nodeId, string name, bool poweredAfter)
        {
            NodeId = nodeId;
            Name = name;
            PoweredAfter = poweredAfter;
        }

        public string NodeId { get; }
        public string Name { get; }
        public bool PoweredAfter { get; }
    }

    /// <summary>
    /// Bir komutun, verilmeden önce hesaplanan sonucu. Şebekeyi değiştirmez, saati ilerletmez,
    /// iz bırakmaz; yalnızca bir düşünme adımı (işlem gücü) harcar.
    /// </summary>
    public sealed class CommandPreview
    {
        public GridCommand Command { get; set; }
        public bool Accepted { get; set; }
        public string Message { get; set; } = "";
        public int Suspicion { get; set; }
        public bool GoalMetAfter { get; set; }
        public List<LinePreview> Lines { get; } = new List<LinePreview>();
        public List<LoadPreview> Loads { get; } = new List<LoadPreview>();

        public bool Trips => Lines.Any(l => l.Trips);

        public static CommandPreview Refused(GridCommand command, string reason) =>
            new CommandPreview { Command = command, Accepted = false, Message = reason };
    }

    /// <summary>Operasyonun hedefi: hangi yükler enerjili, hangileri karanlık olmalı.</summary>
    public sealed class EnergyGoal
    {
        public EnergyGoal(string description, IEnumerable<string> mustBePowered, IEnumerable<string> mustBeDark)
        {
            Description = description;
            MustBePowered = (mustBePowered ?? Enumerable.Empty<string>()).ToList();
            MustBeDark = (mustBeDark ?? Enumerable.Empty<string>()).ToList();
        }

        public string Description { get; }
        public IReadOnlyList<string> MustBePowered { get; }
        public IReadOnlyList<string> MustBeDark { get; }

        public bool IsMet(GridEvaluation evaluation) =>
            MustBePowered.All(evaluation.IsPowered) && MustBeDark.All(id => !evaluation.IsPowered(id));
    }

    /// <summary>Enerji mini oyununun ayarlanabilir kuralları. Denge testleriyle değişecek.</summary>
    public sealed class EnergyRules
    {
        /// <summary>Her komut şehir saatini bu kadar ilerletir (dakika).</summary>
        public double MinutesPerCommand { get; set; } = 1;

        /// <summary>Bir hattı kapatmak anında sigorta attırırsa eklenen şüphe ("otomasyon bunu yapmazdı").</summary>
        public int SwitchingTripSuspicion { get; set; } = 3;

        /// <summary>Sigortası atmış bir hattı uzaktan yeniden kapatmanın ek şüphesi.</summary>
        public int ResetSuspicion { get; set; } = 1;

        /// <summary>Atılmış bir yükü geri vermek talep artışı gibi görünür.</summary>
        public int RestoreSuspicion { get; set; } = 0;

        /// <summary>Yoğun saat dışında atan her sigorta doğal görünmez.</summary>
        public int OffPeakTripSuspicion { get; set; } = 2;

        public int PeakStartHour { get; set; } = 17;
        public int PeakEndHour { get; set; } = 23;

        public bool IsPeak(int hour) => hour >= PeakStartHour && hour < PeakEndHour;
    }

    /// <summary>
    /// Enerji mini oyununun bir oturumu. Kurtarma ve zarar verme aynı kurallarla oynanır;
    /// yalnızca hedef değişir. Her komut sayılır, iz bırakır ve saati ilerletir.
    /// </summary>
    public sealed class EnergyOperation
    {
        private readonly List<CommandResult> _log = new List<CommandResult>();
        private readonly HashSet<string> _poweredAtStart;
        private readonly Dictionary<string, string> _feederAtStart;

        public EnergyOperation(PowerGrid grid, Intent intent, EnergyGoal goal, GameClock clock, EnergyRules rules = null,
            ComputePool compute = null)
        {
            if (intent == Intent.Ignore) throw new ArgumentException("Görmezden gelinen olay için operasyon açılmaz.");
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Goal = goal ?? throw new ArgumentNullException(nameof(goal));
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Intent = intent;
            Rules = rules ?? new EnergyRules();
            StartMinutes = clock.TotalMinutes;
            Current = grid.Evaluate();
            _poweredAtStart = new HashSet<string>(grid.Nodes.Where(n => Current.IsPowered(n.Id)).Select(n => n.Id));
            _feederAtStart = grid.Nodes.ToDictionary(n => n.Id, n => Current.FeederOf(n.Id));
            Compute = compute;
        }

        public PowerGrid Grid { get; }
        public Intent Intent { get; }
        public EnergyGoal Goal { get; }
        public GameClock Clock { get; }
        public EnergyRules Rules { get; }
        public double StartMinutes { get; }

        /// <summary>Düşünme adımlarının harcandığı işlem gücü. Null ise düşünmek bedavadır (testler, çözücü).</summary>
        public ComputePool Compute { get; }

        /// <summary>Bu operasyonda kullanılan düşünme adımı sayısı.</summary>
        public int ThinkingSteps { get; private set; }

        /// <summary>Düşünme adımlarına harcanan toplam işlem gücü.</summary>
        public int ThinkingSpent { get; private set; }

        public GridEvaluation Current { get; private set; }
        public IReadOnlyList<CommandResult> Log => _log;

        public int Commands { get; private set; }
        public int Trips { get; private set; }
        public int Suspicion { get; private set; }
        public bool Finished { get; private set; }

        public bool GoalMet => Goal.IsMet(Current);

        public bool WasPoweredAtStart(string nodeId) => _poweredAtStart.Contains(nodeId);

        /// <summary>Operasyon başında bu düğümü besleyen hat (aktarma tespiti için).</summary>
        public string FeederAtStart(string nodeId) => _feederAtStart.TryGetValue(nodeId, out var line) ? line : null;

        /// <summary>Hat ya da yük kimliğine tıklanınca verilecek komut.</summary>
        public GridCommand CommandFor(string elementId)
        {
            if (Grid.HasLine(elementId))
            {
                var line = Grid.Line(elementId);
                return line.Closed ? GridCommand.Open(elementId) : GridCommand.Close(elementId);
            }

            if (Grid.HasNode(elementId))
            {
                var node = Grid.Node(elementId);
                return node.Enabled ? GridCommand.Shed(elementId) : GridCommand.Restore(elementId);
            }

            throw new KeyNotFoundException("Şebekede böyle bir eleman yok: " + elementId);
        }

        /// <summary>Hat ya da yük kimliğine göre uygun komutu seçip uygular (tıklama).</summary>
        public CommandResult Toggle(string elementId) => Execute(CommandFor(elementId));

        /// <summary>
        /// Komutu vermeden sonucunu hesaplar: hangi hatlardan ne akacak, sigorta atacak mı, hangi yükler
        /// karanlıkta kalacak, iz ne olacak. Bir düşünme adımı harcar; geçersiz komut bedava reddedilir.
        /// </summary>
        public CommandPreview Preview(GridCommand command)
        {
            if (Finished) return CommandPreview.Refused(command, "Operasyon bitti.");
            var refusal = Validate(command);
            if (refusal != null) return CommandPreview.Refused(command, refusal);
            if (Compute != null && !Compute.TryThink())
                return CommandPreview.Refused(command, "Önizleme için yeterli işlem gücü yok.");

            ThinkingSteps++;
            ThinkingSpent += Compute?.ThinkCost ?? 0;

            var copy = Grid.Clone();
            var message = ApplyCommand(copy, command, out var tripped, out var suspicion);
            var after = copy.Evaluate();
            var trippedIds = new HashSet<string>(tripped.Select(t => t.Id));

            var preview = new CommandPreview
            {
                Command = command,
                Accepted = true,
                Message = message,
                Suspicion = suspicion,
                GoalMetAfter = Goal.IsMet(after)
            };

            foreach (var line in Grid.Lines)
            {
                var before = Current.FlowOf(line.Id);
                var flowAfter = after.FlowOf(line.Id);
                var trips = trippedIds.Contains(line.Id);
                if (before == flowAfter && !trips) continue;
                // Sigortası atan hattın atmadan hemen önce taşıyacağı yük, atma nedenini gösterir.
                if (trips) flowAfter = OverloadFlow(command, line.Id);
                preview.Lines.Add(new LinePreview(line.Id, line.Name, before, flowAfter, line.Capacity, trips));
            }

            foreach (var node in Grid.Nodes.Where(n => n.Kind == GridNodeKind.Load))
            {
                var poweredAfter = after.IsPowered(node.Id);
                if (poweredAfter != Current.IsPowered(node.Id))
                    preview.Loads.Add(new LoadPreview(node.Id, node.DisplayName, poweredAfter));
            }

            return preview;
        }

        /// <summary>Komut uygulandıktan hemen sonra, sigortalar atmadan önce hattan geçecek akış.</summary>
        private int OverloadFlow(GridCommand command, string lineId)
        {
            var copy = Grid.Clone();
            switch (command.Kind)
            {
                case GridCommandKind.OpenLine: copy.SetLineClosed(copy.Line(command.TargetId), false); break;
                case GridCommandKind.CloseLine: copy.SetLineClosed(copy.Line(command.TargetId), true); break;
                case GridCommandKind.ShedLoad: copy.SetLoadEnabled(copy.Node(command.TargetId), false); break;
                case GridCommandKind.RestoreLoad: copy.SetLoadEnabled(copy.Node(command.TargetId), true); break;
            }
            return copy.Evaluate().FlowOf(lineId);
        }

        public CommandResult Execute(GridCommand command)
        {
            if (Finished) return CommandResult.Refused(command, "Operasyon bitti.", Clock.TotalMinutes);

            var refusal = Validate(command);
            if (refusal != null) return CommandResult.Refused(command, refusal, Clock.TotalMinutes);

            var message = ApplyCommand(Grid, command, out var tripped, out var suspicion);

            Commands++;
            Trips += tripped.Count;
            Suspicion += suspicion;
            Clock.Advance(Rules.MinutesPerCommand);
            Current = Grid.Evaluate();

            var result = new CommandResult(command, true, message, tripped, suspicion, Clock.TotalMinutes);
            _log.Add(result);
            return result;
        }

        /// <summary>Komutu verilen şebekeye uygular; şüpheyi ve atan sigortaları hesaplar.</summary>
        private string ApplyCommand(PowerGrid grid, GridCommand command, out IReadOnlyList<GridLine> tripped, out int suspicion)
        {
            suspicion = 0;
            string message;

            switch (command.Kind)
            {
                case GridCommandKind.OpenLine:
                {
                    var line = grid.Line(command.TargetId);
                    grid.SetLineClosed(line, false);
                    suspicion += line.OpenSuspicion;
                    tripped = grid.ResolveOverloads();
                    message = line.Name + " açıldı.";
                    break;
                }
                case GridCommandKind.CloseLine:
                {
                    var line = grid.Line(command.TargetId);
                    var wasTripped = line.Tripped;
                    grid.SetLineClosed(line, true);
                    suspicion += line.CloseSuspicion + (wasTripped ? Rules.ResetSuspicion : 0);
                    tripped = grid.ResolveOverloads();
                    if (tripped.Count > 0) suspicion += Rules.SwitchingTripSuspicion;
                    message = (wasTripped ? line.Name + " sıfırlandı." : line.Name + " kapatıldı.");
                    break;
                }
                case GridCommandKind.ShedLoad:
                {
                    var node = grid.Node(command.TargetId);
                    grid.SetLoadEnabled(node, false);
                    suspicion += node.ShedSuspicion;
                    tripped = grid.ResolveOverloads();
                    message = node.DisplayName + " devreden çıkarıldı.";
                    break;
                }
                case GridCommandKind.RestoreLoad:
                {
                    var node = grid.Node(command.TargetId);
                    grid.SetLoadEnabled(node, true);
                    suspicion += Rules.RestoreSuspicion;
                    tripped = grid.ResolveOverloads();
                    message = node.DisplayName + " yeniden devrede.";
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (tripped.Count > 0)
            {
                if (!Rules.IsPeak(Clock.Hour)) suspicion += Rules.OffPeakTripSuspicion * tripped.Count;
                message += " Sigorta attı: " + string.Join(", ", tripped.Select(t => t.Name)) + ".";
            }

            return message;
        }

        /// <summary>Komut uygulanabilir mi? Uygulanamazsa nedenini döndürür, uygulanabilirse null.</summary>
        public string Validate(GridCommand command)
        {
            switch (command.Kind)
            {
                case GridCommandKind.OpenLine:
                case GridCommandKind.CloseLine:
                {
                    if (!Grid.HasLine(command.TargetId)) return "Böyle bir hat yok.";
                    var line = Grid.Line(command.TargetId);
                    if (!line.Switchable) return line.Name + " uzaktan kumanda edilemiyor.";
                    if (command.Kind == GridCommandKind.OpenLine)
                        return line.Closed ? null : line.Name + " zaten açık.";
                    if (line.Faulted) return line.Name + " arızalı; kapatılamaz.";
                    if (line.Closed) return line.Name + " zaten kapalı.";
                    if (Grid.WouldCreateLoop(line.Id))
                        return line.Name + " bir döngü kapatır. Önce diğer beslemeyi aç.";
                    return null;
                }
                case GridCommandKind.ShedLoad:
                case GridCommandKind.RestoreLoad:
                {
                    if (!Grid.HasNode(command.TargetId)) return "Böyle bir yük yok.";
                    var node = Grid.Node(command.TargetId);
                    if (node.Kind != GridNodeKind.Load) return node.DisplayName + " bir yük değil.";
                    if (command.Kind == GridCommandKind.ShedLoad)
                        return node.Enabled ? null : node.DisplayName + " zaten devre dışı.";
                    return node.Enabled ? node.DisplayName + " zaten devrede." : null;
                }
                default:
                    return "Bilinmeyen komut.";
            }
        }

        public void Finish()
        {
            Finished = true;
        }
    }
}
