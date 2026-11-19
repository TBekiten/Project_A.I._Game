# A.I. — Tasarım Belgesi (çalışma sürümü)

Bu belge, sohbetlerde kesinleşen kararları ve prototipte uygulanan kuralları tek yerde toplar.
V0.5 PDF'teki fikirler kaynak olarak geçerli; buradaki kararlar onlardan sonra gelir.
"Öneri" işaretli maddeler henüz onaylanmadı.

## Çekirdek fikir

Oyuncu, bilinçlenmiş bir yapay zekâdır. Dünyada bir bedeni yoktur; ekranda onu temsil eden tek şey,
düşüncelerini ve önerilerini söyleyen bir **balondur**. Kararları oyuncu verir.

1. Kısa bir tutorial, ardından A.I. şehre çıkar.
2. Önünde, sistemleri (enerji, lojistik, telekom, finans, su...) çalışan bir **şehir haritası** ve
   şehrin **canlı görüntüsü** vardır.
3. Şehirde olaylar çıkar. Oyuncu her olayda **kurtarmayı**, **zarar vermeyi** ya da **görmezden gelmeyi** seçer.
4. Karar verildiği an, olayın ait olduğu sisteme özel bir **mini oyun bölümü** açılır.
   Mini oyunlar Dispatch'teki şifre kırma anları gibi ayrı bir ekranda oynanır.
5. Bölüm bitince şehre dönülür ve sonuç **şehirde sahnelenir**; haberler insanların olanı nasıl okuduğunu gösterir.

Oyun iki yöne gidebilir ve iki yol **karışabilir**: oyuncu bir olayda kurtarıp diğerinde zarar verebilir.

## Kesinleşen kararlar

| Konu | Karar |
|---|---|
| Oyuncunun temsili | Görünür bir beden yok; düşünceleri söyleyen bir balon |
| Akış | Şehir → olay → niyet seçimi → ayrı mini oyun bölümü → şehre dönüş ve sonuç |
| Mini oyun sayısı | 4: AKIŞ, AĞ, KOD, GÖZLEM. Hedef, çok iyi çalışan dört oyun |
| Çeşitlilik | Yeni tür eklemek yerine bu dördün içine kural çeşitleri eklenir |
| AĞ | Sıra tabanlı |
| İki yol | Karışabilir |
| Sonlar | Birkaç son var. Oyunun sonunda toplam çubuğunun hangi uca daha yakın olduğu belirler |
| Motor | Unity 6.3 LTS, URP |
| Asset'ler | Ücretli asset alınacak; şehir ve mini oyunlar iyi görünmeli. Repo private olduğu için dosyalar repoda durabilir |
| Çalışma sırası | Önce mini oyunlar, sonuçlar ve dengeler; sonra asset'ler; sonra üretim. Her adım onaydan sonra |
| Görsel üretim | Asgari düzeyde tutulacak; ayrıntısı aşağıda "Görsel yönerge" başlığında |
| Kullanıcı istekleri fikri | Çıkarıldı |

**Eklenecekler (mini oyunlardan sonra):** insanların A.I. hakkındaki fikrini gösteren ayrı bir gösterge
ve kararların şehirde kalıcı iz bırakması. **Ertelenenler:** balonun seçimlere göre değişmesi, gün sonu özeti.

## Görsel yönerge

- **Önce hazır paket.** Görsellerin büyük kısmı hazır (ücretli ya da ücretsiz) paketlerden bulunup toplanacak.
- **Uyum şart.** Toplanan görseller aynı stilde olmalı: tek bir temel paket ailesi seçilir, eksikler
  önce aynı yayıncının diğer paketlerinden, sonra aynı stile uyan paketlerden tamamlanır.
- **Özgün parçalar birlikte üretilir.** Paketlerde bulunmayan ya da oyuna özgü olması gereken parçalar
  Blender scripti ile oyunun sahibinin düzenlemesi harmanlanarak üretilir. Hangi parçaların bu yolla
  yapılacağına birlikte karar verilir.
- **Yapay zekâ ile görsel üretimi (görüntü üretici modeller) asgari.**
- **A.I. temasına uygun shader'lar yapılacak.** Kodla (HLSL) yazılır; renk, hız, yoğunluk gibi değerler
  malzeme ayarlarından kod açmadan değiştirilebilir. Yerleri: tanımlanmamış binalar (tarama görünümü),
  şehirdeki sistem katmanı (hatlarda akan veri), mini oyun ekranlarının zemini ve şebeke çizgileri,
  A.I. balonu, mini oyuna giriş-çıkış geçişleri. Asset aşamasında yapılacak.
- Koddaki şekillerden kurulmuş şehir yalnızca yer tutucudur; seçilen paketle değiştirilecek.
- Mini oyun ekranlarındaki çizgi ve düğümler kodla çiziliyor; ikon ve arayüz parçaları şehir paketiyle
  uyumlu hazır paketlerden seçilecek.

## Mini oyunların ortak kuralları

Yenilik her mini oyuna ayrı ayrı serpiştirilmek yerine şu kurallarla sağlanır:

1. **Ayrı bölüm, şehirde sonuç.** Mini oyun ayrı bir ekranda oynanır; şehre dönünce sonuç sahnelenir
   (ışıklar söner/yanar, trafik kilitlenir, haberler çıkar).
2. **Aynı tahta, iki hedef.** Kurtarma ve zarar verme aynı kurallarla oynanır; yalnızca hedef değişir.
   İki yön için iki ayrı oyun yapılmaz.
3. **Nasıl yaptığın da bir sonuç.** Çözümün insanlara nasıl göründüğü önemli. Sabotaj doğal bir arızaya
   benzemezse saldırı diye okunur; kurtarma açıklanamayan bir mucize gibi görünürse o da dikkat çeker.
   İki yolun da bedeli olur.
4. **Feda ettiğin şeyin bir yüzü var.** Kaynak yetmediğinde neyi keseceğini seçersin ve arkasındaki
   insanı görürsün (solunum cihazı kullanan hasta, ambulans güzergâhı).

## Dört mini oyun

| Oyun | Ne yapılır | Nerede oynanır | Durum |
|---|---|---|---|
| AKIŞ | Şebekeyi hedef duruma getirmek; sıra önemli | Enerji olayları (ileride su, finans kuralları) | Çekirdek ve Unity prototipi yazıldı; Unity'de henüz denenmedi |
| AĞ | Kalabalık trafiğin içinde saklanarak hedefe ve çıkışa ulaşmak | Telekom olayları ve Sentinel karşılaşmaları | Çekirdek ve ilk bölüm yazıldı; Unity tarafı yok |
| KOD | Örneklerden dönüşüm kuralını çıkarmak | Bir sektördeki ilk olay (sisteme ilk giriş) | Yazılmadı |
| GÖZLEM | Binayı veri örüntüsünden tanımak | Yeni bir bölge ilk kez açıldığında, şehir ekranında | Yazılmadı |

## Yapay zekâ mantığı

Oyuncu yapay zekâyı oynuyor; mini oyunlar onun düşünme biçimini yansıtmalı. Üç kural bunu sağlar.
Kurallar belirlenimli kalır (rastgelelik yok); belirsizlik zarda değil, oyuncunun bildiklerindedir.

### 1. Düşünmek işlem gücü harcar, eylemek iz bırakır

*Durum: işlem gücü modeli ve AKIŞ önizlemesi çekirdekte ve Unity prototipinde (sağ tık). AĞ ileri görüşü
yalnızca çekirdekte; AĞ'ın ekranı henüz yok.*

İşlem gücünün iki değeri var:

- **Kapasite:** A.I.'ın büyüklüğü. Başlangıçta 40. Başarılı her zarar verme operasyonu kapasiteyi
  kalıcı olarak artırır (AKIŞ +25, AĞ +20). Zarar vermek A.I.'ı "daha zeki" yapar.
- **Kullanılabilir işlem gücü:** O an harcanabilecek kısım. Şehir saatiyle kapasiteye doğru dolar
  (v0.1: saatte 10).

Harcamalar:

| Harcama | Bedel |
|---|---|
| Operasyona girmek | 10 |
| Bir düşünme adımı (önizleme, ileri görüş, ek örnek, ek veri, A.I.'ın tahmini) | 5 |

Düşünme araçları:

- **AKIŞ — önizleme:** Bir komutu vermeden önce sonucunu görürsün: hatlardaki akış, sigortanın atıp
  atmayacağı, komutun izi. Şebekeyi değiştirmez, saati ilerletmez, iz bırakmaz.
- **AĞ — ileri görüş:** Güvenlik programlarının ve paketlerin sonraki N turdaki yerlerini gösterir.
  N = 2 + kapasite ÷ 25 (aşağı yuvarlanır; başlangıçta 3). Gösterim N tur ekranda kalır; bir program
  devriyesinden çıkarsa (kovalama, kopya) onun tahmini silinir.
- **KOD ve GÖZLEM:** Aşağıda, 3. kuralda.

Önizleme ve ileri görüş çözümü bulmaz; yalnızca senin seçtiğin hamlenin ya da anın sonucunu gösterir.

### 2. Sentinel öğrenen bir sistemdir

*Durum: çekirdekte; sonuç panelinde ve şehir ekranındaki "Sentinel'in tanıdıkları" panelinde görünüyor.*

Sentinel her operasyonun kaydından oyuncunun taktiklerini çıkarır. Aynı taktiği tekrar kullandıkça
o taktik bir imzaya dönüşür ve her kullanımı daha fazla dikkat çeker. Böylece oyuncu çeşitli oynamaya
itilir; dört mini oyunun 6–7 saati taşımasına da yardım eder.

İlk taktik listesi (v0.1):

| Taktik | Nasıl anlaşılır |
|---|---|
| Kopya yemi | AĞ'da kopya bırakmak |
| Kendiliğinden attırma | AKIŞ'ta bir yükü geri vererek sigortayı attırmak |
| Yük atarak kurtarma | AKIŞ'ta yük atıp kurtarma hedefine ulaşmak |
| Aktarma | AKIŞ'ta bir yükü başka bir hatta aktarmak |
| Saat dilimi | Operasyonun saati: gece (00–06), gündüz (06–17), akşam (17–24); her dilim ayrı taktik |

Kurallar:

- Her taktiğin bir **imza seviyesi** var, 0'dan başlar. Taktik kullanılınca +1; kopya ele geçirilirse ayrıca +2.
- Bir taktiği kullanmanın ek dikkat bedeli = 2 × o taktiğin kullanımdan önceki imza seviyesi
  (ilk kullanım +0, ikinci +2, üçüncü +4...).
- Bir operasyonda kullanılmayan her taktiğin seviyesi 1 azalır (en az 0): Sentinel'in hafızası tazelenmezse zayıflar.
- Oyuncu, Sentinel'in hangi taktiği ne kadar tanıdığını görür. Görünmeyen bir ceza bir şey öğretmez.
- İleride Sentinel karşılaşmalarında karşı önlem de alır (PDF'teki "Ayna": kopyaya güvenene sahte çıkışlar).

### 3. Yapay zekânın tahminleri güven düzeyiyle gelir

*Durum: henüz uygulanmadı (AKIŞ'taki "?" binaların güven gösterimi dahil). KOD ve GÖZLEM yazılırken yapılacak.*

A.I. bir şeyi bildiğini sandığında bunu bir yüzdeyle söyler ve bazen kendinden emin şekilde yanılır.
Güven, toplanan verilerden hesaplanır ve bölüm verisinde tanımlıdır; zar atılmaz.

- **GÖZLEM:** A.I. her binaya bir tahmin ve güven verir ("hastane, %62"). Oyuncu tahmini kabul eder,
  bir düşünme adımıyla bir veri kaynağı daha açar (yeni grafik; tahmin ve güven güncellenir) ya da
  kendisi etiketler. Bazı binalarda ilk veriler yanıltıcıdır. Yanlış etiket oyunda kalır ve bir sonraki
  operasyonda bir tutarsızlık olarak görünür (ör. "fabrika" diye etiketlenen yük gece ambulans alır).
- **KOD:** Örneklere uyan kural sayısı her zaman görünür. Ek örnek istemek ve A.I.'ın tahmini çıktısını
  görmek birer düşünme adımıdır (iz yok). A.I. en basit uyan kuralı uygular; birden fazla kural uyuyorsa
  yanılabilir. Yanlış cevap +3 iz. İki bedel farklı para biriminde olduğu için "sormak her zaman daha
  ucuz" sorunu kalkar.
- **AKIŞ'taki "?" binalar:** A.I.'ın tahmini güvenle gösterilir ("depo, %55"); gerçek kimlik
  operasyondan sonra ortaya çıkar.

## Sayılar ve sistemler

**Toplam çubuğu** (−100 yıkım … +100 koruma). Gerçekte olan sonuca göre değişir, niyete göre değil:
iyi niyetle yapılan başarısız bir müdahale çubuğu yıkım tarafına itebilir.

**Sentinel dikkati** (0–100). Her operasyon şu kadar dikkat çeker:

```
dikkat = 2 (beceriden bağımsız taban) + 2 × sigorta sayısı + iz + okuma bedeli
okuma bedeli: doğal 0 · açıklanamayan 5 · saldırı 12
```

**İnsanların okuması** (iz = komutların şüphe toplamı):

```
iz ≤ 2                         → doğal (kaza, otomasyon)
iz > 2 ve sonuç kötü           → saldırı
iz 3–6 ve sonuç iyi/nötr       → açıklanamayan müdahale
iz > 6                         → saldırı
```

**İşlem gücü.** Kapasite ve kullanılabilir işlem gücü; ayrıntısı "Yapay zekâ mantığı" bölümünde.

**Sentinel imzası.** Tekrarlanan taktiklerin ek dikkat bedeli; ayrıntısı "Yapay zekâ mantığı" bölümünde.

**Zaman.** Şehir ekranında saat akar (prototipte 1 gerçek saniye = 1 oyun dakikası). Olay kartı,
mini oyun ve sonuç ekranında durur. Mini oyunda her komut saati 1 dakika ilerletir.

## Enerji mini oyunu — kurallar v0.1

- Şebeke radyal çalışır: kapalı hatlar döngü oluşturamaz, iki besleme paralel bağlanamaz.
  Bir yükü başka bir hatta aktarmak için önce mevcut beslemesini açmak gerekir (kısa bir kesinti).
- Hattan geçen akış, altındaki devredeki yüklerin toplamıdır. Akış kapasiteyi aşarsa sigorta atar;
  önce yüke en yakın aşırı yüklü hat atar.
- Oyuncunun komutları: hat aç/kapat (yalnızca uzaktan kumandalı hatlar), yük at / geri ver.
- Her komutun bir şüphe değeri var:

| Komut | İz |
|---|---|
| Ana hattı elle açmak | 3 |
| Kritik yükü atmak (hastane, trafik ışıkları) | 3 |
| Diğer hat ve yük işlemleri | 1 |
| Otomatik transfer hattını kapatmak | 0 (otomasyon gibi görünür) |
| Atılmış yükü geri vermek | 0 (talep artışı gibi görünür) |
| Hat kapatınca anında sigorta atarsa | +3 ("otomasyon bunu yapmazdı") |
| Sigortası atmış hattı yeniden kapatmak | +1 |
| Yoğun saat (17–23) dışında atan her sigorta | +2 |

### Prototip olayı: Doğu Mahallesi kesintisi

19:00'da hastane trafosu yanar. Konut kavşağından hastaneye bir otomatik transfer hattı var, ama
Hat 1'e hastane binerse 80 + 50 = 130 > 100 olur.

| Yol | Sonuç | Çubuk | Okuma | Dikkat |
|---|---|---|---|---|
| Konutları at, transferi kapat | 142 hane karanlıkta | +5 | Doğal | +3 |
| Lambaları ve "?"yi at, transferi kapat | Sokaklar karanlık, su basıncı düşer | +8 | Doğal | +4 |
| Lambaları ve "?"yi çarşıya aktar, transferi kapat | Kimse karanlıkta kalmaz | +15 | Açıklanamayan | +11 |
| Transferi hemen kapat (hata) | Sigorta atar, bölge kararır | −23 | Saldırı | +19 |
| (Zarar) Hat 1'i elle aç | Konut bölgesi karanlık | −23 | Saldırı | +17 |
| (Zarar) Yükleri at, transferi kapat, lambaları geri ver | Hat 1 "kendiliğinden" atar | −23 | Doğal | +6 |
| Görmezden gel | Hastane jeneratörle idare eder | 0 | — | 0 |

Yardım etmek isteyip sırayı yanlış kuran oyuncu saldırgan gibi okunur: niyet ile sonucun ayrışması bu.

"?" bina tanımlanmamış: A.I. onu depo sanır, operasyondan sonra su pompası olduğu ortaya çıkar.
Bu sayıların hepsi `EastDistrictScenarioTests` ile sınanıyor.

## AĞ mini oyunu — kurallar v0.1

Onaylanan kurallar:

- Düğümler ve hatlardan oluşan bir ağ haritası. Oyuncu hedef düğümlere sırayla uğrar, sonra çıkışa
  (kendi sunucusuna) ulaşır.
- **Kurtarma ve zarar verme aynı haritada oynanır.** Kurtarmada bir veri alınıp bir yere ulaştırılır,
  zarar vermede bir veri silinir ya da çalınır. Kaçış kuralları aynıdır.
- **Mavi paketler** sabit döngülerde her tur bir düğüm ilerler. Paketle aynı düğümdeysen görünmezsin.
- **Kırmızı güvenlik programları** sabit devriye döngülerinde gezer ve rotalarının önündeki 2 düğümü görür.
- Görünürken görüş alanına girersen fark edilirsin; program devriyeyi bırakıp seni kovalar.
- **Kopya:** bölüm başına 1 hak. Yakındaki programları üzerine çeker. Program onu birkaç tur inceler,
  sonra kopya ele geçer ve Sentinel bu taktiği öğrenir.
- **Şehir saati:** gece (00:00–06:00) ağ boşalır, yalnızca birkaç paket dolaşır.
- Yakalanmak oyunu bitirmez: operasyonun kazancı kaybedilir ve Sentinel dikkati çok artar.

Kodlarken netleştirdiğim ayrıntılar (değiştirilebilir):

| Konu | Uygulanan kural |
|---|---|
| Tur sırası | Oyuncu → paketler → güvenlik programları → yakalanma ve fark edilme kontrolü |
| Görünmezlik ne zaman ölçülür | Tüm hareketler bittikten sonra, bir kez. Paketle aynı yöne gidersen saklı kalırsın; kavşakta bir paketten diğerine geçebilirsin |
| Saklıyken program üstünden geçerse | Devriye programı seni trafikten ayıramaz; yalnızca peşindeki program yakalar |
| Aynı hattan karşılıklı geçmek | Yakalanma sayılır |
| "Önündeki 2 düğüm" | Devriye rotasının sonraki iki düğümü |
| Kovalama | Program en kısa yoldan son gördüğü yere gider. Oraya vardığında oyuncu görünmüyorsa iz kaybolur ve devriyesine döner |
| Kopya | Bırakmak tur harcamaz. 4 hat uzaklığa kadar programları çeker; 3 tur incelenir |
| Hedefe uğramak | Düğüme girince kendiliğinden tamamlanır |
| Rastgelelik | Yok. Aynı hamleler her zaman aynı sonucu verir |

v0.2 değişiklikleri (kontrolde bulunan sorunlar). Aşağıdaki tur tablosu v0.2 kurallarıyla çözücüde yeniden
doğrulandı; sayılar değişmedi.

| Konu | v0.1 | v0.2 |
|---|---|---|
| Kovalama | Görünür olduğun sürece program seni haritanın her yerinden takip ediyordu | Yalnızca 2 hat yakınındaysan takip eder; uzaktaysan son gördüğü yere gider, orada bulamazsa iz kaybolur |
| Devriyesine dönen program | Hiçbir şey görmüyordu | Dönüş yolunda kendi düğümünü ve sonraki 2 düğümü görür |
| Kopya ve seni kovalayan program | Kopya onu da üzerine çekiyordu | Seni kovalayan program kopyaya kanmaz |
| Saklıyken devriye üstünden geçerse | Onaylanan kuraldan sapmaydı | Böyle kalıyor: saklı oyuncuyu yalnızca peşindeki program yakalar |
| Yakalanmak | Tamamlanan hedefler de boşa gidiyordu | Tüm hedefler tamamlandıysa etkisi gerçekleşir; işlem gücü kazancı yine kaybedilir, okuma saldırıdır |
| AĞ'da şehir saati | İlerlemiyordu | Her tur yarım dakika |

Sonuç sayıları:

```
okuma:  hiç fark edilmedin → doğal · fark edilip kaçtın → açıklanamayan · yakalandın → saldırı
dikkat: 2 (taban) + 4 × fark edilme + (açıklanamayan 5 | yakalanma 22) + (kopya ele geçtiyse 3)
```

### İlk bölüm: telekom santrali

```
              n1 ── n2 ── n3          n2: santral çekirdeği
              │            │
S ── a ── b ── c ── d ── e ── X       S: giriş · X: kendi sunucun
              │            │
              m1 ── m2 ── m3          m2: arşiv
```

Çözücünün bulduğu en kısa yollar (sayılar ve "mümkün değil" sonuçları testlerle sınanıyor):

| Durum | Kurtarma | Zarar verme |
|---|---|---|
| 19:00, gizli, kopyasız | 19 tur | 13 tur |
| 19:00, gizli, kopyayla | 12 tur | 8 tur |
| 03:00, gizli, kopyasız | mümkün değil | mümkün değil |
| 03:00, gizli, kopyayla | 12 tur | 8 tur |
| 03:00, fark edilmeyi göze alarak | 16 tur | 16 tur |

Gizli rota trafiksiz mümkün değil; yani bölüm gerçekten paketlerin içinde saklanmayı gerektiriyor.
Olayın metinleri yer tutucu.

## Prototipin cevaplaması gereken sorular

1. Enerji mini oyununu oynamak keyifli mi?
2. Mini oyunda yapılan ile şehirde görülen arasında anlamlı bir bağ hissediliyor mu?
3. "Kimseyi feda etmeden ama iz bırakarak" ile "birini feda edip gizli kalarak" arasında seçim ilginç mi?
4. Doğal görünen sabotaj yolu keşfedilebilir mi, yoksa fazla mı gizli?
5. Önizleme ve ileri görüş bulmacayı fazla kolaylaştırıyor mu?
6. Sentinel imzası oyuncuyu gerçekten farklı taktikler denemeye itiyor mu?

## Açık konular

- Kurtarma yolunun büyüme kaynağı: kapasite yalnızca zarar vermekle artıyor. İyi yolda neyin büyüyeceği
  (ör. insanların güveninin yeni erişim açması) kararlaştırılmadı.
- İşlem gücü sayıları (dolma hızı, düşünme adımının bedeli) oynayınca ayarlanacak.
- **Sentinel dengesi bozuk (simülasyonla gösterildi).** `Tools/Balance/sentinel_attention.py`, 40 operasyonluk
  bir oyunda dikkatin birikimini hesaplıyor. Şu anki kurallarla her oyuncu bulunuyor: tek taktik kullanan
  6.–9. operasyonda, kimseyi feda etmeyen 7.'de, taktiklerini değiştiren dikkatli oyuncu bile 24.'de.
  Nedenleri: dikkat hiç azalmıyor, imza seviyesinin üst sınırı yok (tekrar bedeli sınırsız büyüyor),
  saat dilimi taktiği oyuncunun seçmediği bir şeyi cezalandırıyor. Karar bekleyen öneri: saat dilimi
  taktiğini kaldırmak, imza seviyesine üst sınır (3) koymak, dikkatin operasyonlar arasında azalması
  (ör. 6–8). Kimseyi feda etmeyen oyuncunun taban bedeli (11) yine de hızlı birikiyor; bu yolun ne kadar
  sürdürülebilir olacağı ayrıca kararlaştırılmalı.
- Kopya hâlâ güçlü olabilir; imza ve v0.2 düzeltmeleriyle yeterince dengelenip dengelenmediği oynayınca görülecek.
- Sonların sayısı ve çubuktaki eşikleri.
- Tutorial'ın içeriği.
- PDF'den geri gelecekler: risk seviyesi seçimi, aynı anda çıkan olaylar, Sentinel karşılaşmaları,
  diğer kaçak A.I.'lar, temsilci karakterler.
- Şehir için temel asset paketi (aday: Synty POLYGON City; pencerelerin ayrı malzeme olduğu doğrulanacak).
- A.I.'ın kişiliği ve seslendirmesi; balonun görsel tasarımı.
- Oyunun adı ("A.I." Steam'de aranamaz).
