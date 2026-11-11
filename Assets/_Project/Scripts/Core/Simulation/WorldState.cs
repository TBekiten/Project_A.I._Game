using System;
using AIGame.Core.Narrative;
using AIGame.Core.Operations;

namespace AIGame.Core.Simulation
{
    /// <summary>
    /// Oyunun kalıcı durumu: saat, toplam çubuğu, Sentinel'in dikkati, işlem gücü ve haberler.
    /// </summary>
    public sealed class WorldState
    {
        public const int AlignmentMin = -100;
        public const int AlignmentMax = 100;
        public const int AttentionMax = 100;

        public WorldState(double startMinutes, int compute)
        {
            Clock = new GameClock(startMinutes);
            Compute = compute;
        }

        public GameClock Clock { get; }
        public NewsFeed News { get; } = new NewsFeed();

        /// <summary>
        /// Toplam çubuğu. Negatif uç yıkım, pozitif uç koruma. Oyunun sonu, oyuncunun
        /// bitirdiği noktada çubuğun hangi uca daha yakın olduğuna göre belirlenir.
        /// </summary>
        public int Alignment { get; private set; }

        /// <summary>Sentinel'in dikkati. 100'e ulaşırsa A.I. bulunur.</summary>
        public int Attention { get; private set; }

        public int Compute { get; private set; }

        public bool FoundBySentinel => Attention >= AttentionMax;

        /// <summary>Çubuğun -1..1 aralığındaki konumu.</summary>
        public float AlignmentNormalized => Alignment / (float)AlignmentMax;

        public void Apply(OperationResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            Alignment = Clamp(Alignment + result.AlignmentDelta, AlignmentMin, AlignmentMax);
            Attention = Clamp(Attention + result.AttentionDelta, 0, AttentionMax);
            Compute = Math.Max(0, Compute + result.ComputeDelta);
            foreach (var item in result.News) News.Schedule(item);
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
