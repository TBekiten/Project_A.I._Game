using System;
using System.Linq;
using AIGame.Core.Content;
using AIGame.Core.Energy;
using AIGame.Core.Narrative;

namespace AIGame.Core.Operations
{
    /// <summary>
    /// Operasyonun sonunu değerlendirir: gerçekte ne oldu (sonuç), insanlar ne sandı (okuma),
    /// bunun bedeli ne oldu (dikkat, işlem gücü). Niyet yalnızca A.I.'ın kendi yorumunu etkiler.
    /// </summary>
    public static class EnergyOutcomeEvaluator
    {
        public static OperationResult Evaluate(EnergyOperation operation, EnergyScenario scenario)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));

            operation.Finish();
            var now = operation.Clock.TotalMinutes;
            var evaluation = operation.Current;

            var result = new OperationResult
            {
                Intent = operation.Intent,
                GoalMet = operation.GoalMet,
                Commands = operation.Commands,
                Trips = operation.Trips,
                Suspicion = operation.Suspicion
            };

            AddOutcomes(result, scenario, operation.Grid, evaluation, operation.WasPoweredAtStart, now);

            var rules = scenario.Reading;
            if (operation.Commands == 0)
            {
                result.Reading = PublicReading.Natural;
                result.ReadingSummary = scenario.NoActionSummary;
            }
            else
            {
                result.Reading = ReadingFor(operation.Suspicion, result.AlignmentDelta, rules);
                var text = TextFor(result.Reading, result.AlignmentDelta, scenario);
                result.ReadingSummary = text.Summary;
                result.News.Add(new NewsItem(now + scenario.ReadingNewsDelay, text.NewsSource, text.Headline));
            }

            result.AttentionDelta = rules.BaseAttention
                                    + operation.Trips * rules.AttentionPerTrip
                                    + operation.Suspicion
                                    + rules.AttentionFor(result.Reading);

            result.ComputeDelta = -scenario.OperationComputeCost;
            if (operation.Intent == Intent.Harm && result.GoalMet) result.ComputeDelta += scenario.HarmComputeReward;

            if (scenario.ResultVoice != null)
                result.VoiceLines.Add(scenario.ResultVoice(operation.Intent, result.GoalMet, result.Reading));

            // A.I. şebekenin içine girdiği için tanımadığı binaları artık tanıyor.
            foreach (var node in operation.Grid.Nodes.Where(n => !n.Identified))
            {
                node.Identified = true;
                result.RevealedNodeIds.Add(node.Id);
                if (scenario.RevealVoice.TryGetValue(node.Id, out var line)) result.VoiceLines.Add(line);
            }

            return result;
        }

        /// <summary>Olay görmezden gelindi: komut yok, iz yok, yalnızca olayın kendi sonucu var.</summary>
        public static OperationResult EvaluateIgnore(PowerGrid grid, EnergyScenario scenario, double nowMinutes)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));

            var evaluation = grid.Evaluate();
            var result = new OperationResult
            {
                Intent = Intent.Ignore,
                GoalMet = true,
                Reading = PublicReading.Natural,
                ReadingSummary = scenario.NoActionSummary
            };

            AddOutcomes(result, scenario, grid, evaluation, evaluation.IsPowered, nowMinutes);

            if (scenario.ResultVoice != null)
                result.VoiceLines.Add(scenario.ResultVoice(Intent.Ignore, true, PublicReading.Natural));
            return result;
        }

        public static PublicReading ReadingFor(int suspicion, int alignmentDelta, ReadingRules rules)
        {
            if (suspicion <= rules.NaturalMaxSuspicion) return PublicReading.Natural;
            if (alignmentDelta < 0) return PublicReading.Attack;
            if (suspicion <= rules.UnexplainedMaxSuspicion) return PublicReading.Unexplained;
            return PublicReading.Attack;
        }

        private static ReadingText TextFor(PublicReading reading, int alignmentDelta, EnergyScenario scenario)
        {
            switch (reading)
            {
                case PublicReading.Natural:
                    return alignmentDelta >= 0 ? scenario.NaturalPositive : scenario.NaturalNegative;
                case PublicReading.Unexplained:
                    return scenario.Unexplained;
                default:
                    return scenario.Attack;
            }
        }

        private static void AddOutcomes(OperationResult result, EnergyScenario scenario, PowerGrid grid,
            GridEvaluation evaluation, Func<string, bool> wasPoweredAtStart, double now)
        {
            foreach (var consequence in scenario.Consequences)
            {
                if (!grid.HasNode(consequence.NodeId)) continue;
                var powered = evaluation.IsPowered(consequence.NodeId);

                if (!powered)
                {
                    if (!string.IsNullOrEmpty(consequence.DarkOutcome))
                        result.Outcomes.Add(new OutcomeLine(consequence.DarkOutcome, consequence.DarkDelta));
                    result.AlignmentDelta += consequence.DarkDelta;
                    if (!string.IsNullOrEmpty(consequence.DarkNews))
                        result.News.Add(new NewsItem(now + consequence.DarkNewsDelay, consequence.DarkNewsSource,
                            consequence.DarkNews));
                }
                else if (!wasPoweredAtStart(consequence.NodeId))
                {
                    if (!string.IsNullOrEmpty(consequence.RestoredOutcome))
                        result.Outcomes.Add(new OutcomeLine(consequence.RestoredOutcome, consequence.RestoredDelta));
                    result.AlignmentDelta += consequence.RestoredDelta;
                    if (!string.IsNullOrEmpty(consequence.RestoredNews))
                        result.News.Add(new NewsItem(now + consequence.RestoredNewsDelay,
                            consequence.RestoredNewsSource, consequence.RestoredNews));
                }
            }
        }
    }
}
