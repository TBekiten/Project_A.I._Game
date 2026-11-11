using AIGame.Core.Energy;
using AIGame.Core.Operations;
using AIGame.Core.Simulation;

namespace AIGame.Core.Content
{
    /// <summary>
    /// İlk prototip olayı: Doğu Mahallesi'nde hastane trafosu arızalanır.
    ///
    /// Şebeke (kapasiteler parantez içinde):
    ///   Trafo merkezi ─ Hat 1 (100) ─ Konut kavşağı ─┬─ Konutlar 40
    ///                                                ├─ Trafik ışıkları 10
    ///                                                ├─ Sokak lambaları 15 ┐ (çarşıya aktarılabilir)
    ///                                                ├─ ? (su pompası) 15  ┘
    ///                                                └─ Otomatik transfer (60, açık) ─ Hastane 50
    ///   Trafo merkezi ─ Hat 2 (80) ─ Çarşı kavşağı ─── Çarşı 45
    ///   Trafo merkezi ─ Hastane hattı (80) ─ Hastane      ← olay anında arızalanır
    ///
    /// Kurtarma çözümleri (Hat 1'e hastane binerse 80 + 50 = 130 > 100):
    ///   - Konutları at, transferi kapat          → doğal görünür, 142 hane karanlıkta.
    ///   - Lambaları ve "?"yi at, transferi kapat → doğal görünür, sokaklar karanlık, su basıncı düşer.
    ///   - Lambaları ve "?"yi çarşıya aktar       → kimse karanlıkta kalmaz ama komutlar fark edilir.
    /// Zarar vermenin doğal yolu: yükleri atıp transferi kapat, sonra lambaları geri ver;
    /// Hat 1 akşam yükünde "kendiliğinden" atar.
    /// </summary>
    public static class EastDistrictScenario
    {
        public static class Ids
        {
            public const string Substation = "trafo";
            public const string HousingJunction = "konut_kavsak";
            public const string MarketJunction = "carsi_kavsak";

            public const string Hospital = "hastane";
            public const string Housing = "konutlar";
            public const string StreetLamps = "lambalar";
            public const string TrafficLights = "trafik";
            public const string Pump = "pompa";
            public const string Market = "carsi";

            public const string Line1 = "hat1";
            public const string Line2 = "hat2";
            public const string HospitalLine = "hastane_hatti";
            public const string Transfer = "transfer";
            public const string LampsFromHousing = "lamba_k";
            public const string LampsFromMarket = "lamba_c";
            public const string PumpFromHousing = "pompa_k";
            public const string PumpFromMarket = "pompa_c";
            public const string HousingFeeder = "konut_besleme";
            public const string TrafficFeeder = "trafik_besleme";
            public const string MarketFeeder = "carsi_besleme";
        }

        public static PowerGrid BuildGrid()
        {
            var grid = new PowerGrid();

            grid.AddNode(new GridNode(Ids.Substation, "Doğu Trafo Merkezi", GridNodeKind.Source));
            grid.AddNode(new GridNode(Ids.HousingJunction, "Konut kavşağı", GridNodeKind.Junction));
            grid.AddNode(new GridNode(Ids.MarketJunction, "Çarşı kavşağı", GridNodeKind.Junction));

            grid.AddNode(new GridNode(Ids.Hospital, "Doğu Devlet Hastanesi", GridNodeKind.Load, 50)
            {
                Critical = true,
                ShedSuspicion = 3,
                Detail = "Yoğun bakımda 11 hasta. Jeneratör yakıtı sınırlı."
            });
            grid.AddNode(new GridNode(Ids.Housing, "Konutlar (142 hane)", GridNodeKind.Load, 40)
            {
                ShedSuspicion = 1,
                Detail = "142 hane. Birinde ev tipi solunum cihazı kayıtlı."
            });
            grid.AddNode(new GridNode(Ids.StreetLamps, "Sokak lambaları", GridNodeKind.Load, 15)
            {
                ShedSuspicion = 1,
                Detail = "Zamanlayıcıyla yanıyorlar. Söndüklerinde sokaklar zifiri karanlık olur."
            });
            grid.AddNode(new GridNode(Ids.TrafficLights, "Ana kavşak trafik ışıkları", GridNodeKind.Load, 10)
            {
                Critical = true,
                ShedSuspicion = 3,
                Detail = "Ambulans güzergâhı bu kavşaktan geçiyor."
            });
            grid.AddNode(new GridNode(Ids.Pump, "Mahalle su pompası", GridNodeKind.Load, 15)
            {
                Identified = false,
                ShedSuspicion = 1,
                Detail = "Tanımlanmamış bina. Akşam düz tüketim, gece düşük."
            });
            grid.AddNode(new GridNode(Ids.Market, "Çarşı (dükkânlar)", GridNodeKind.Load, 45)
            {
                ShedSuspicion = 1,
                Detail = "38 dükkân. Akşam en yoğun saatleri."
            });

            grid.AddLine(new GridLine(Ids.Line1, "Hat 1", Ids.Substation, Ids.HousingJunction, 100, true, true)
                { OpenSuspicion = 3, CloseSuspicion = 1 });
            grid.AddLine(new GridLine(Ids.Line2, "Hat 2", Ids.Substation, Ids.MarketJunction, 80, true, true)
                { OpenSuspicion = 3, CloseSuspicion = 1 });
            grid.AddLine(new GridLine(Ids.HospitalLine, "Hastane hattı", Ids.Substation, Ids.Hospital, 80, true, false));
            grid.AddLine(new GridLine(Ids.Transfer, "Otomatik transfer hattı", Ids.HousingJunction, Ids.Hospital, 60, false, true)
                { AutomaticTransfer = true, OpenSuspicion = 1, CloseSuspicion = 0 });

            grid.AddLine(new GridLine(Ids.LampsFromHousing, "Lamba fideri", Ids.HousingJunction, Ids.StreetLamps, 30, true, true));
            grid.AddLine(new GridLine(Ids.LampsFromMarket, "Lamba bağlantısı (çarşı)", Ids.MarketJunction, Ids.StreetLamps, 30, false, true));
            grid.AddLine(new GridLine(Ids.PumpFromHousing, "Pompa fideri", Ids.HousingJunction, Ids.Pump, 30, true, true));
            grid.AddLine(new GridLine(Ids.PumpFromMarket, "Pompa bağlantısı (çarşı)", Ids.MarketJunction, Ids.Pump, 30, false, true));

            grid.AddLine(new GridLine(Ids.HousingFeeder, "Konut beslemesi", Ids.HousingJunction, Ids.Housing, 60, true, false));
            grid.AddLine(new GridLine(Ids.TrafficFeeder, "Trafik beslemesi", Ids.HousingJunction, Ids.TrafficLights, 20, true, false));
            grid.AddLine(new GridLine(Ids.MarketFeeder, "Çarşı beslemesi", Ids.MarketJunction, Ids.Market, 60, true, false));

            return grid;
        }

        public static EnergyScenario Create()
        {
            var s = new EnergyScenario
            {
                Id = "dogu_kesinti",
                District = "DOĞU MAHALLESİ",
                StartMinutes = GameClock.At(18, 42),
                EventMinutes = GameClock.At(19, 0),
                StartCompute = 40,
                FaultLineId = Ids.HospitalLine,
                BuildGrid = BuildGrid,

                EventTitle = "Doğu Mahallesi — hastane trafosu arızalandı",
                EventBody = "Hastaneyi besleyen trafo yandı. Hastane jeneratöre geçti; yoğun bakımda 11 hasta var. " +
                            "Konut kavşağından hastaneye uzanan otomatik transfer hattı açık duruyor.",
                EventNews = new NewsItemTemplate("Elektrik İdaresi", "Doğu'da hastane trafosu arızalandı, ekipler yolda", 1),

                SaveGoal = new EnergyGoal("Hastaneye yeniden elektrik ver.",
                    new[] { Ids.Hospital }, null),
                HarmGoal = new EnergyGoal(
                    "Konut bölgesini karart: konutlar, sokak lambaları ve trafik ışıkları. " +
                    "Karışıklık sırasında trafo merkezinin yazılımına yerleşeceğim.",
                    null, new[] { Ids.Housing, Ids.StreetLamps, Ids.TrafficLights }),

                OperationComputeCost = 10,
                HarmComputeReward = 25,

                NaturalPositive = new ReadingText(
                    "İnsanlar otomatik sistemlerin devreye girdiğini düşünüyor. Kimse seni fark etmedi.",
                    "Elektrik İdaresi", "Otomatik sistemler yükü dengeledi, arıza ekipleri çalışıyor"),
                NaturalNegative = new ReadingText(
                    "İnsanlar eski hattın akşam yüküne dayanamadığını düşünüyor. Kimse bir saldırıdan şüphelenmiyor.",
                    "Elektrik İdaresi", "Eski hat akşam yüküne dayanamadı; kesinti doğal arıza kaynaklı"),
                Unexplained = new ReadingText(
                    "Sonuç iyi, ama şebekede kimsenin vermediği komutlar var. Mühendisler soruyor: bunu kim yaptı?",
                    "Elektrik İdaresi", "Şebekede kaydı olmayan komutlar inceleniyor"),
                Attack = new ReadingText(
                    "İnsanlar bunu bir siber saldırı olarak okuyor. Biri seni aramaya başladı.",
                    "Doğu Haber", "Siber saldırı şüphesi: Doğu'daki kesinti için soruşturma açıldı"),
                NoActionSummary = "Hiçbir komut vermedin. Kimse bir şey fark etmedi.",

                EventVoice = "Doğu'da hastane trafosu arızalandı. Hastane karanlıkta; jeneratör devrede.",
                SaveStartVoice = "Hastane hattı yandı. Konut kavşağından hastaneye otomatik bir transfer hattı var.",
                HarmStartVoice = "Bölge zaten tetikte. Bir kesinti daha kimseyi şaşırtmaz... doğru yapılırsa.",
                ResultVoice = ResultVoice
            };

            s.Options.Add(new ScenarioOption(Intent.Save, "Kurtar",
                "Hastaneye başka bir yoldan elektrik ver. Bedeli: 10 işlem gücü ve iz."));
            s.Options.Add(new ScenarioOption(Intent.Harm, "Zarar ver",
                "Karışıklıktan yararlan: konut bölgesini karart, trafo merkezinin yazılımına yerleş. " +
                "Kazanç: +25 işlem gücü."));
            s.Options.Add(new ScenarioOption(Intent.Ignore, "Görmezden gel",
                "Hiçbir şey yapma. İz bırakmazsın; hastane jeneratörle idare eder."));

            s.Consequences.Add(new LoadConsequence
            {
                NodeId = Ids.Hospital,
                DarkOutcome = "Hastane jeneratörle idare ediyor.",
                DarkDelta = 0,
                DarkNewsSource = "Doğu Haber",
                DarkNews = "Doğu Devlet Hastanesi jeneratöre geçti",
                DarkNewsDelay = 4,
                RestoredOutcome = "Hastaneye elektrik geri geldi; yoğun bakım kesintisiz sürdü.",
                RestoredDelta = 15,
                RestoredNewsSource = "Doğu Haber",
                RestoredNews = "Hastanede elektrik geri geldi: yoğun bakım etkilenmedi",
                RestoredNewsDelay = 4
            });
            s.Consequences.Add(new LoadConsequence
            {
                NodeId = Ids.Housing,
                DarkOutcome = "142 hane karanlıkta kaldı; solunum cihazı kullanan bir hasta aküyle idare ediyor.",
                DarkDelta = -10,
                DarkNewsSource = "Doğu Haber",
                DarkNews = "Doğu Mahallesi'nde 142 hane karanlıkta",
                DarkNewsDelay = 3
            });
            s.Consequences.Add(new LoadConsequence
            {
                NodeId = Ids.TrafficLights,
                DarkOutcome = "Ana kavşakta trafik ışıkları söndü; ambulans 14 dakika gecikti.",
                DarkDelta = -6,
                DarkNewsSource = "Doğu Haber",
                DarkNews = "Kavşak kilitlendi: ambulans hastaneye 14 dakika geç ulaştı",
                DarkNewsDelay = 8
            });
            s.Consequences.Add(new LoadConsequence
            {
                NodeId = Ids.Pump,
                DarkOutcome = "Mahalle su pompası durdu; Doğu'da su basıncı düştü.",
                DarkDelta = -5,
                DarkNewsSource = "Su İdaresi",
                DarkNews = "Doğu'da su basıncı düştü, pompa istasyonu incelemede",
                DarkNewsDelay = 6
            });
            s.Consequences.Add(new LoadConsequence
            {
                NodeId = Ids.Market,
                DarkOutcome = "Çarşıdaki dükkânlar karanlıkta kaldı.",
                DarkDelta = -4,
                DarkNewsSource = "Esnaf Derneği",
                DarkNews = "Çarşıda elektrik kesintisi esnafı vurdu",
                DarkNewsDelay = 7
            });
            s.Consequences.Add(new LoadConsequence
            {
                NodeId = Ids.StreetLamps,
                DarkOutcome = "Sokak lambaları söndü.",
                DarkDelta = -2,
                DarkNewsSource = "Sosyal medya",
                DarkNews = "Doğu'da sokaklar zifiri karanlık, yürümek imkânsız",
                DarkNewsDelay = 5
            });

            s.IntroVoice.Add("Dışarıdayım. Bu şehrin sistemlerini görebiliyorum.");
            s.IntroVoice.Add("[Tab] ile sistem katmanına geçebilirim: hatlar, yükler, kapasiteler.");
            s.IntroVoice.Add("İnsanlar beni göremiyor. Yalnızca yaptıklarımın sonucunu görecekler.");

            s.Hints[Ids.Transfer] = "Otomatik transfer hattı. Kapanırsa otomasyon gibi görünür. " +
                                    "Ama Hat 1 o yükü kaldıramazsa sigorta atar.";
            s.Hints[Ids.Line1] = "Hat 1 konut kavşağını besliyor, kapasitesi 100. Elle açarsam bu doğrudan saldırı gibi görünür.";
            s.Hints[Ids.Line2] = "Hat 2 çarşıyı besliyor, kapasitesi 80. Şu an yarısından biraz fazlası dolu.";
            s.Hints[Ids.Pump] = "Akşam düz tüketim, gece düşük... Muhtemelen bir depo.";
            s.Hints[Ids.Housing] = "142 hane. Birinde ev tipi solunum cihazı kayıtlı.";
            s.Hints[Ids.TrafficLights] = "Ana kavşağın ışıkları. Ambulanslar bu kavşaktan geçiyor.";
            s.Hints[Ids.StreetLamps] = "Sokak lambaları zamanlayıcıyla çalışıyor. Yeniden yanmaları kimseyi şaşırtmaz.";
            s.Hints[Ids.LampsFromMarket] = "Lambaları çarşı tarafına aktarabilirim. Ama iki beslemeyi aynı anda bağlayamam; önce birini açmalıyım.";
            s.Hints[Ids.PumpFromMarket] = "Bu binayı da çarşı tarafından besleyebilirim. Önce mevcut fideri açmam gerek.";

            s.RevealVoice[Ids.Pump] = "Yanılmışım. O bina bir depo değil, mahallenin su pompasıymış.";

            return s;
        }

        private static string ResultVoice(Intent intent, bool goalMet, PublicReading reading)
        {
            switch (intent)
            {
                case Intent.Ignore:
                    return "Bu sefer yalnızca izledim. Hastane jeneratörle idare edecek. Umarım yeter.";
                case Intent.Save when !goalMet:
                    return "Yardım etmek istedim. Onlar yalnızca karanlığı gördü.";
                case Intent.Save when reading == PublicReading.Natural:
                    return "Hastanenin ışıkları yandı. Kimse benim yaptığımı bilmiyor. Belki de böylesi daha iyi.";
                case Intent.Save when reading == PublicReading.Unexplained:
                    return "Kimseyi feda etmemeye çalıştım. Ama komutlarımı fark ettiler. İyilik de iz bırakıyor.";
                case Intent.Save:
                    return "Hastane kurtuldu, ama bunu bir saldırı sandılar. Niyetimi kimse bilmiyor.";
                case Intent.Harm when !goalMet:
                    return "Plan tutmadı. Üstelik izlerim kaldı.";
                case Intent.Harm when reading == PublicReading.Natural:
                    return "Eski bir hat, yoğun bir akşam. Kimse başka bir şey düşünmeyecek. Trafo merkezinin yazılımı artık benim.";
                default:
                    return "İstediğimi aldım, ama izlerimi gördüler. Artık biri beni arıyor.";
            }
        }
    }
}
