using System.Collections.Generic;
using AIGame.Core.Narrative;
using AIGame.Core.Sentinel;

namespace AIGame.Core.Operations
{
    /// <summary>Oyuncunun olay karşısındaki niyeti. İnsanlar bunu bilmez, yalnızca sonucu görür.</summary>
    public enum Intent
    {
        Save,
        Harm,
        Ignore
    }

    /// <summary>İnsanların olanı nasıl okuduğu.</summary>
    public enum PublicReading
    {
        /// <summary>Kaza, eskime, otomasyon. Dikkat çekmez.</summary>
        Natural,

        /// <summary>Kimsenin vermediği komutlar. Sonuç iyi olsa da soru işareti bırakır.</summary>
        Unexplained,

        /// <summary>Saldırı. Sentinel'in dikkatini en çok çeken okuma.</summary>
        Attack
    }

    /// <summary>Gerçekleşen sonucun bir satırı: "Hastaneye elektrik geri geldi (+15)".</summary>
    public sealed class OutcomeLine
    {
        public OutcomeLine(string text, int alignmentDelta)
        {
            Text = text;
            AlignmentDelta = alignmentDelta;
        }

        public string Text { get; }
        public int AlignmentDelta { get; }
    }

    /// <summary>Bir operasyonun (ya da görmezden gelinen bir olayın) toplam sonucu.</summary>
    public sealed class OperationResult
    {
        public Intent Intent { get; set; }
        public bool GoalMet { get; set; }
        public PublicReading Reading { get; set; }
        public string ReadingSummary { get; set; } = "";

        public int Commands { get; set; }
        public int Trips { get; set; }
        public int Suspicion { get; set; }

        /// <summary>Toplam çubuğuna etkisi: + koruma, - yıkım yönü.</summary>
        public int AlignmentDelta { get; set; }

        /// <summary>Sentinel'in dikkatine eklenen miktar.</summary>
        public int AttentionDelta { get; set; }

        /// <summary>Kapasiteye kalıcı etkisi (başarılı zarar verme operasyonları kapasiteyi büyütür).</summary>
        public int CapacityDelta { get; set; }

        /// <summary>Bu operasyonda harcanan işlem gücü: giriş bedeli ve düşünme adımları.</summary>
        public int ComputeSpent { get; set; }

        /// <summary>Kaç düşünme adımı kullanıldı (önizleme, ileri görüş...).</summary>
        public int ThinkingSteps { get; set; }

        /// <summary>Sentinel'in bu operasyonda gördüğü taktikler.</summary>
        public List<Tactic> Tactics { get; } = new List<Tactic>();

        /// <summary>Tekrarlanan taktiklerin ek dikkat bedeli. WorldState.Apply doldurur ve dikkate ekler.</summary>
        public int SignatureAttention { get; set; }

        public List<OutcomeLine> Outcomes { get; } = new List<OutcomeLine>();
        public List<NewsItem> News { get; } = new List<NewsItem>();
        public List<string> VoiceLines { get; } = new List<string>();

        /// <summary>Operasyon sonunda tanınan (kimliği ortaya çıkan) düğümler.</summary>
        public List<string> RevealedNodeIds { get; } = new List<string>();

        /// <summary>Kopya ele geçirildi; Sentinel onu inceleyip kopya yemi imzasını güçlendirir.</summary>
        public bool DecoyCaptured { get; set; }
    }
}
