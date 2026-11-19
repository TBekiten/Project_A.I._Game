using System.Collections.Generic;
using System.Linq;

namespace AIGame.Core.Sentinel
{
    /// <summary>Sentinel'in operasyon kayıtlarından tanıyabildiği oyuncu taktikleri.</summary>
    public enum Tactic
    {
        /// <summary>AĞ'da kopya bırakmak.</summary>
        Decoy,
        /// <summary>AKIŞ'ta bir yükü geri vererek sigortayı attırmak.</summary>
        SelfTrip,
        /// <summary>AKIŞ'ta yük atıp kurtarma hedefine ulaşmak.</summary>
        ShedToSave,
        /// <summary>AKIŞ'ta bir yükü başka bir hatta aktarmak.</summary>
        Transfer
        // Operasyon saati bir taktik değil: oyuncu operasyonun saatini seçemiyor. Zamanlama oyuncunun
        // kararı olursa saat dilimleri buraya geri eklenebilir.
    }

    public static class TacticNames
    {
        public static string Of(Tactic tactic)
        {
            switch (tactic)
            {
                case Tactic.Decoy: return "Kopya yemi";
                case Tactic.SelfTrip: return "Kendiliğinden attırma";
                case Tactic.ShedToSave: return "Yük atarak kurtarma";
                default: return "Aktarma";
            }
        }
    }

    public sealed class SentinelRules
    {
        /// <summary>Bir taktiği kullanmanın ek dikkat bedeli = bu sayı × taktiğin imza seviyesi.</summary>
        public int AttentionPerLevel { get; set; } = 2;

        /// <summary>Kopya ele geçirilince kopya yemi imzasına eklenen seviye.</summary>
        public int DecoyCaptureLevels { get; set; } = 2;

        /// <summary>Bir operasyonda kullanılmayan taktiğin kaybettiği seviye.</summary>
        public int DecayPerOperation { get; set; } = 1;

        /// <summary>
        /// İmza seviyesinin üst sınırı. Sınır olmazsa aynı taktiğin bedeli her kullanımda büyür ve
        /// tek taktikle oynayan oyuncu birkaç operasyonda bulunur (bkz. Tools/Balance/sentinel_attention.py).
        /// </summary>
        public int MaxLevel { get; set; } = 3;
    }

    /// <summary>
    /// Sentinel'in oyuncu hakkında öğrendikleri. Aynı taktik tekrarlandıkça imzaya dönüşür ve
    /// her kullanımı daha fazla dikkat çeker; kullanılmayan taktikleri Sentinel yavaş yavaş unutur.
    /// </summary>
    public sealed class SentinelProfile
    {
        private readonly Dictionary<Tactic, int> _levels = new Dictionary<Tactic, int>();

        public SentinelProfile(SentinelRules rules = null)
        {
            Rules = rules ?? new SentinelRules();
        }

        public SentinelRules Rules { get; }

        public int LevelOf(Tactic tactic) => _levels.TryGetValue(tactic, out var level) ? level : 0;

        /// <summary>Sentinel'in tanıdığı taktikler, en çok tanınan önce.</summary>
        public IReadOnlyList<KeyValuePair<Tactic, int>> Known =>
            _levels.Where(p => p.Value > 0).OrderByDescending(p => p.Value).ThenBy(p => p.Key).ToList();

        /// <summary>Bu taktikleri şimdi kullanmanın ek dikkat bedeli (profili değiştirmez).</summary>
        public int CostOf(IEnumerable<Tactic> tactics) =>
            tactics.Distinct().Sum(t => Rules.AttentionPerLevel * LevelOf(t));

        /// <summary>
        /// Bir operasyonu değerlendirir: kullanılan taktiklerin ek dikkat bedelini döndürür, sonra
        /// kullanılanların seviyesini artırır, kullanılmayanlarınkini azaltır.
        /// </summary>
        public int Assess(IEnumerable<Tactic> used, bool decoyCaptured)
        {
            var usedSet = new HashSet<Tactic>(used ?? Enumerable.Empty<Tactic>());
            var cost = CostOf(usedSet);

            foreach (Tactic tactic in System.Enum.GetValues(typeof(Tactic)))
            {
                var level = LevelOf(tactic);
                level = usedSet.Contains(tactic) ? level + 1 : System.Math.Max(0, level - Rules.DecayPerOperation);
                if (tactic == Tactic.Decoy && decoyCaptured) level += Rules.DecoyCaptureLevels;
                _levels[tactic] = System.Math.Min(level, Rules.MaxLevel);
            }

            return cost;
        }
    }
}
