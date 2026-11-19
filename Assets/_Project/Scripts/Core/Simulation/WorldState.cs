using System;
using AIGame.Core.Narrative;
using AIGame.Core.Operations;
using AIGame.Core.Sentinel;

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

        public WorldState(double startMinutes, int computeCapacity)
        {
            Clock = new GameClock(startMinutes);
            Compute = new ComputePool(computeCapacity);
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

        /// <summary>İşlem gücü: kapasite ve kullanılabilir kısım.</summary>
        public ComputePool Compute { get; }

        /// <summary>Sentinel'in oyuncu hakkında öğrendikleri.</summary>
        public SentinelProfile Sentinel { get; } = new SentinelProfile();

        /// <summary>
        /// Her olayın sonunda (görmezden gelmek dahil), yeni dikkat eklenmeden önce dikkat bu kadar azalır:
        /// Sentinel uzun süre iz görmezse ilgisi söner. Değer simülasyonla seçildi
        /// (Tools/Balance/sentinel_attention.py): taktiklerini değiştiren oyuncu bulunmaz, tek taktikle
        /// oynayan oyunun ikinci yarısında bulunur.
        /// </summary>
        public int AttentionDecayPerEvent { get; set; } = 6;

        public bool FoundBySentinel => Attention >= AttentionMax;

        /// <summary>Çubuğun -1..1 aralığındaki konumu.</summary>
        public float AlignmentNormalized => Alignment / (float)AlignmentMax;

        /// <summary>Şehir saatini ilerletir; işlem gücü bu sırada dolar.</summary>
        public void AdvanceTime(double minutes)
        {
            Clock.Advance(minutes);
            Compute.Regenerate(minutes);
        }

        /// <summary>
        /// Operasyon sonucunu dünyaya uygular. Sentinel imzasının ek dikkat bedelini hesaplayıp sonuca yazar
        /// (SignatureAttention) ve dikkate ekler; bu yüzden her sonuç için bir kez çağrılmalıdır.
        /// </summary>
        public void Apply(OperationResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.AttentionDecay = Math.Min(Attention, AttentionDecayPerEvent);
            Attention -= result.AttentionDecay;
            if (result.Intent != Intent.Ignore)
            {
                result.SignatureAttention = Sentinel.Assess(result.Tactics, result.DecoyCaptured);
                result.AttentionDelta += result.SignatureAttention;
            }
            Alignment = Clamp(Alignment + result.AlignmentDelta, AlignmentMin, AlignmentMax);
            Attention = Clamp(Attention + result.AttentionDelta, 0, AttentionMax);
            Compute.Grow(result.CapacityDelta);
            foreach (var item in result.News) News.Schedule(item);
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
