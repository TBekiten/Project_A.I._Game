using System;
using AIGame.Core.Content;
using AIGame.Core.Network;

namespace AIGame.Core.Operations
{
    /// <summary>
    /// AĞ operasyonunun sonucunu ortak sonuç biçimine çevirir.
    /// Okuma: hiç fark edilmediysen doğal, fark edilip kaçtıysan açıklanamayan, yakalandıysan saldırı.
    /// </summary>
    public static class NetOutcomeEvaluator
    {
        public static OperationResult Evaluate(NetOperation operation, NetScenario scenario)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));

            var escaped = operation.Status == NetStatus.Escaped;
            var caught = operation.Status == NetStatus.Caught;
            var result = new OperationResult
            {
                Intent = operation.Intent,
                GoalMet = escaped,
                Commands = operation.Turn,
                Suspicion = operation.Detections,
                SentinelLearnedDecoy = operation.DecoyCaptured
            };

            if (escaped)
            {
                var harm = operation.Intent == Intent.Harm;
                var delta = harm ? scenario.HarmAlignment : scenario.SaveAlignment;
                result.Outcomes.Add(new OutcomeLine(harm ? scenario.HarmOutcome : scenario.SaveOutcome, delta));
                result.AlignmentDelta = delta;
            }
            else
            {
                result.Outcomes.Add(new OutcomeLine(scenario.CaughtOutcome, 0));
            }

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

            result.ComputeDelta = -scenario.OperationComputeCost;
            if (escaped && operation.Intent == Intent.Harm) result.ComputeDelta += scenario.HarmComputeReward;
            return result;
        }
    }
}
