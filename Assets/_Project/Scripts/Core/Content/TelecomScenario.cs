using System.Collections.Generic;
using AIGame.Core.Network;
using AIGame.Core.Operations;

namespace AIGame.Core.Content
{
    /// <summary>Bir AĞ olayının verisi: bölüm, iki yönün hedefleri, sayılar ve metinler.</summary>
    public sealed class NetScenario
    {
        public string Id { get; set; }
        public string EventTitle { get; set; }
        public string EventBody { get; set; }
        public System.Func<NetLevel> BuildLevel { get; set; }

        public List<NetObjective> SaveObjectives { get; } = new List<NetObjective>();
        public List<NetObjective> HarmObjectives { get; } = new List<NetObjective>();

        public string SaveOutcome { get; set; }
        public int SaveAlignment { get; set; }
        public string HarmOutcome { get; set; }
        public int HarmAlignment { get; set; }
        public string CaughtOutcome { get; set; }

        public int OperationComputeCost { get; set; } = 10;
        public int HarmComputeReward { get; set; } = 20;

        /// <summary>Beceriden bağımsız bedel.</summary>
        public int BaseAttention { get; set; } = 2;
        public int AttentionPerDetection { get; set; } = 4;
        public int UnexplainedAttention { get; set; } = 5;
        public int CaughtAttention { get; set; } = 22;
        public int DecoyCapturedAttention { get; set; } = 3;

        public string UnseenSummary { get; set; }
        public string SeenSummary { get; set; }
        public string CaughtSummary { get; set; }

        public IReadOnlyList<NetObjective> ObjectivesFor(Intent intent) =>
            intent == Intent.Harm ? HarmObjectives : SaveObjectives;
    }

    /// <summary>
    /// İlk AĞ bölümü: telekom santrali. Metinler yer tutucudur; olayın hikâyesi sonra yazılacak.
    ///
    ///                 n1 ── n2 ── n3            n2: santral çekirdeği
    ///                 │            │
    ///   S ── a ── b ── c ── d ── e ── X         S: giriş, X: kendi sunucun
    ///                 │            │
    ///                 m1 ── m2 ── m3            m2: arşiv
    ///
    /// Orta hatta ve üst halkada birer güvenlik programı devriye gezer. Akşam dört paket dolaşır;
    /// gece yalnızca girişteki paket kalır, bu yüzden gece gizli geçmek için kopya gerekir.
    /// </summary>
    public static class TelecomScenario
    {
        public static class Ids
        {
            public const string Start = "S";
            public const string Exit = "X";
            public const string Archive = "m2";
            public const string Core = "n2";
        }

        public static NetLevel BuildLevel()
        {
            var map = new NetMap();
            map.AddNode("S", "Giriş", 0, 0).AddNode("a", "Yönlendirici", 1, 0).AddNode("b", "Batı kavşağı", 2, 0)
                .AddNode("c", "Omurga", 3, 0).AddNode("d", "Doğu kavşağı", 4, 0).AddNode("e", "Çıkış geçidi", 5, 0)
                .AddNode("X", "Kendi sunucun", 6, 0)
                .AddNode("n1", "Santral batı", 2, 1).AddNode("n2", "Santral çekirdeği", 3, 1).AddNode("n3", "Santral doğu", 4, 1)
                .AddNode("m1", "Arşiv batı", 2, -1).AddNode("m2", "Arşiv", 3, -1).AddNode("m3", "Arşiv doğu", 4, -1);
            map.Chain("S", "a", "b", "c", "d", "e", "X").Chain("b", "n1", "n2", "n3", "d").Chain("b", "m1", "m2", "m3", "d");

            var level = new NetLevel { Map = map, StartId = Ids.Start, ExitId = Ids.Exit };
            level.Rules.DecoyLureRange = 4;

            level.Guards.Add(new GuardDef("omurga", "c", "d", "e", "d", "c", "b"));
            level.Guards.Add(new GuardDef("santral", "n1", "n2", "n3", "n2"));

            level.Packets.Add(new PacketDef("arsiv_halkasi", false, "b", "m1", "m2", "m3", "d", "c"));
            level.Packets.Add(new PacketDef("santral_halkasi", false, "b", "c", "d", "n3", "n2", "n1") { StartIndex = 3 });
            level.Packets.Add(new PacketDef("giris", true, "S", "a", "b", "a"));
            level.Packets.Add(new PacketDef("cikis", false, "d", "e", "X", "e") { StartIndex = 1 });
            return level;
        }

        public static NetScenario Create()
        {
            var s = new NetScenario
            {
                Id = "telekom_santral",
                EventTitle = "Telekom — acil çağrı yönlendirmesi bozuldu",
                EventBody = "Santraldeki yönlendirme tablosu bozuldu; acil çağrılar yanlış merkeze düşüyor. " +
                            "Sağlam bir kopyası arşivde duruyor.",
                BuildLevel = BuildLevel,

                SaveOutcome = "Acil çağrılar yeniden doğru merkeze ulaşıyor.",
                SaveAlignment = 12,
                HarmOutcome = "Çağrı kayıtları silindi; santral saatlerce kör kaldı.",
                HarmAlignment = -12,
                CaughtOutcome = "Güvenlik programı seni santralde yakaladı. Hiçbir şey değişmedi.",

                UnseenSummary = "Kayıtlarda yalnızca olağan trafik var. Kimse seni fark etmedi.",
                SeenSummary = "Güvenlik programı tanımadığı bir süreç gördü ve rapor etti.",
                CaughtSummary = "Santralde yakalandın. Sentinel artık senin neye benzediğini biliyor."
            };

            s.SaveObjectives.Add(new NetObjective(Ids.Archive, "Sağlam yönlendirme tablosu arşivden alındı."));
            s.SaveObjectives.Add(new NetObjective(Ids.Core, "Tablo santral çekirdeğine yüklendi."));
            s.HarmObjectives.Add(new NetObjective(Ids.Core, "Çağrı kayıtları silindi."));
            return s;
        }
    }
}
