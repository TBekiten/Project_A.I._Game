using System;
using System.Collections.Generic;
using AIGame.Core.Energy;
using AIGame.Core.Operations;

namespace AIGame.Core.Content
{
    /// <summary>Bir yük karanlıkta kalırsa ya da elektriği geri gelirse ne olur?</summary>
    public sealed class LoadConsequence
    {
        public string NodeId { get; set; }

        public string DarkOutcome { get; set; }
        public int DarkDelta { get; set; }
        public string DarkNewsSource { get; set; }
        public string DarkNews { get; set; }
        public double DarkNewsDelay { get; set; } = 3;

        public string RestoredOutcome { get; set; }
        public int RestoredDelta { get; set; }
        public string RestoredNewsSource { get; set; }
        public string RestoredNews { get; set; }
        public double RestoredNewsDelay { get; set; } = 3;
    }

    /// <summary>Olay kartındaki seçenek.</summary>
    public sealed class ScenarioOption
    {
        public ScenarioOption(Intent intent, string title, string description)
        {
            Intent = intent;
            Title = title;
            Description = description;
        }

        public Intent Intent { get; }
        public string Title { get; }
        public string Description { get; }
    }

    /// <summary>İnsanların okumasına göre sonuç panelinde ve haberlerde görünen metin.</summary>
    public sealed class ReadingText
    {
        public ReadingText(string summary, string newsSource, string headline)
        {
            Summary = summary;
            NewsSource = newsSource;
            Headline = headline;
        }

        public string Summary { get; }
        public string NewsSource { get; }
        public string Headline { get; }
    }

    /// <summary>Operasyonun nasıl okunacağını ve dikkatin nasıl artacağını belirleyen sayılar.</summary>
    public sealed class ReadingRules
    {
        /// <summary>Bu şüphenin altı doğal (kaza, otomasyon) okunur.</summary>
        public int NaturalMaxSuspicion { get; set; } = 2;

        /// <summary>Sonuç iyiyse bu şüpheye kadar "açıklanamayan", üstü saldırı okunur.</summary>
        public int UnexplainedMaxSuspicion { get; set; } = 6;

        /// <summary>Beceriden bağımsız bedel: her operasyon en az bu kadar dikkat çeker.</summary>
        public int BaseAttention { get; set; } = 2;

        public int AttentionPerTrip { get; set; } = 2;
        public int NaturalAttention { get; set; } = 0;
        public int UnexplainedAttention { get; set; } = 5;
        public int AttackAttention { get; set; } = 12;

        public int AttentionFor(PublicReading reading)
        {
            switch (reading)
            {
                case PublicReading.Natural: return NaturalAttention;
                case PublicReading.Unexplained: return UnexplainedAttention;
                default: return AttackAttention;
            }
        }
    }

    /// <summary>
    /// Bir enerji olayının tüm verisi: şebeke, hedefler, sonuçlar ve metinler.
    /// İçerik ileride ScriptableObject/JSON'a taşınabilir; şimdilik kodda tanımlanıyor.
    /// </summary>
    public sealed class EnergyScenario
    {
        public string Id { get; set; }
        public string District { get; set; }

        public double StartMinutes { get; set; }
        public double EventMinutes { get; set; }
        public int StartCompute { get; set; }

        /// <summary>Olay anında arızalanan hat.</summary>
        public string FaultLineId { get; set; }

        public Func<PowerGrid> BuildGrid { get; set; }

        public string EventTitle { get; set; }
        public string EventBody { get; set; }
        public NewsItemTemplate EventNews { get; set; }
        public List<ScenarioOption> Options { get; } = new List<ScenarioOption>();

        public EnergyGoal SaveGoal { get; set; }
        public EnergyGoal HarmGoal { get; set; }

        public int OperationComputeCost { get; set; }
        public int HarmComputeReward { get; set; }

        public EnergyRules Rules { get; set; } = new EnergyRules();
        public ReadingRules Reading { get; set; } = new ReadingRules();

        public List<LoadConsequence> Consequences { get; } = new List<LoadConsequence>();

        public ReadingText NaturalPositive { get; set; }
        public ReadingText NaturalNegative { get; set; }
        public ReadingText Unexplained { get; set; }
        public ReadingText Attack { get; set; }
        public string NoActionSummary { get; set; }

        /// <summary>Gecikme: operasyon bittikten kaç dakika sonra resmi açıklama yayımlanır.</summary>
        public double ReadingNewsDelay { get; set; } = 12;

        public List<string> IntroVoice { get; } = new List<string>();
        public string EventVoice { get; set; }
        public string SaveStartVoice { get; set; }
        public string HarmStartVoice { get; set; }

        /// <summary>Oyuncu bir elemanın üstüne ilk geldiğinde A.I.'ın yorumu.</summary>
        public Dictionary<string, string> Hints { get; } = new Dictionary<string, string>();

        /// <summary>Tanımlanmamış bir bina tanındığında A.I.'ın yorumu.</summary>
        public Dictionary<string, string> RevealVoice { get; } = new Dictionary<string, string>();

        public Func<Intent, bool, PublicReading, string> ResultVoice { get; set; }

        public EnergyGoal GoalFor(Intent intent)
        {
            switch (intent)
            {
                case Intent.Save: return SaveGoal;
                case Intent.Harm: return HarmGoal;
                default: return null;
            }
        }
    }

    public sealed class NewsItemTemplate
    {
        public NewsItemTemplate(string source, string headline, double delay)
        {
            Source = source;
            Headline = headline;
            Delay = delay;
        }

        public string Source { get; }
        public string Headline { get; }
        public double Delay { get; }
    }
}
