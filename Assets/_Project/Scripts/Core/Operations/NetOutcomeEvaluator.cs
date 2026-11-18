using System;
using AIGame.Core.Content;
using AIGame.Core.Network;
using AIGame.Core.Sentinel;

namespace AIGame.Core.Operations
{
    /// <summary>
    /// AĞ operasyonunun sonucunu ortak sonuç biçimine çevirir.
    /// Hedeflerin tamamı yapıldıysa etkisi gerçekleşir, sonradan yakalansan bile; ama yakalanırsan
    /// kapasite kazancını alamazsın. Okuma: hiç fark edilmediysen doğal, fark edilip kaçtıysan
    /// açıklanamayan, yakalandıysan saldırı.
    /// </summary>
    public static class NetOutcomeEvaluator
    {
        public static OperationResult Evaluate(NetOperation operation, NetScenario scenario)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));

            var done = operation.AllObjectivesDone;
            var escaped = operation.Status == NetStatus.Escaped;
            var caught = operation.Status == NetStatus.Caught;
            var harm = operation.Intent == Intent.Harm;

            var result = new OperationResult
            {
                Intent = operation.Intent,
                GoalMet = done,
                Commands = operation.Turn,
                Suspicion = operation.Detections,
                DecoyCaptured = operation.DecoyCaptured,
                ThinkingSteps = operation.ThinkingSteps,
                ComputeSpent = scenario.EntryComputeCost + operation.ThinkingSpent
            };
            result.Tactics.AddRange(TacticDetector.FromNet(operation));

            if (done)
            {
                var delta = harm ? scenario.HarmAlignment : scenario.SaveAlignment;
                result.Outcomes.Add(new OutcomeLine(harm ? scenario.HarmOutcome : scenario.SaveOutcome, delta));
                result.AlignmentDelta = delta;
            }
            if (caught) result.Outcomes.Add(new OutcomeLine(scenario.CaughtOutcome, 0));
            if (!done) result.Outcomes.Add(new OutcomeLine(scenario.NothingChangedOutcome, 0));

            if (caught)
            {
                result.Reading = PublicReading.Attack;
                result.ReadingSummary = scenario.CaughtSummary;
            }
            else if (operation.Detections > 0)
            {
                result.Reading = PublicReading.Unexplained;
                result.ReadingSummary = scenario.SeenSummary;
            }
            else
            {
                result.Reading = PublicReading.Natural;
                result.ReadingSummary = scenario.UnseenSummary;
            }

            result.AttentionDelta = scenario.BaseAttention
                                    + operation.Detections * scenario.AttentionPerDetection
                                    + (caught ? scenario.CaughtAttention
                                        : operation.Detections > 0 ? scenario.UnexplainedAttention : 0)
                                    + (operation.DecoyCaptured ? scenario.DecoyCapturedAttention : 0);

            if (harm && done && escaped) result.CapacityDelta = scenario.HarmCapacityGain;
            return result;
        }
    }
}
