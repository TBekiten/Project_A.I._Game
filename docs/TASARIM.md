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
| Mini oyunlar | Her sistemin kendine özgü mini oyunu var. Bilinen türlere dayanır, yeniliği yüksek olmalı |
| PDF'deki GÖZLEM / KOD / AKIŞ / AĞ | Sabit bir zincir değil, mini oyun örnekleri (havuzun adayları) |
| İki yol | Karışabilir |
| Sonlar | Birkaç son var. Oyunun sonunda toplam çubuğunun hangi uca daha yakın olduğu belirler |
| Motor | Unity 6.3 LTS, URP |
| Kullanıcı istekleri fikri | Çıkarıldı |

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

## Mini oyun havuzu (öneri)

| Sistem | Dayandığı tür | Yenilik |
|---|---|---|
| Enerji | Boru/akış bulmacası | Sıra önemli: ara adımda hat eşiği aşılırsa sigorta atar. **Prototipte uygulandı** |
| Ulaşım ve lojistik | Trafik yönetimi | Araçları değil, yalnızca ışıkları ve yönlendirmeyi kontrol edersin |
| Telekom | Blok yerleştirme | Her blok gerçek bir konuşma; sığmayanı düşürürsün ve kim olduğunu görürsün |
| Finans | Gizlilik oyunu | Harita bir muhasebe defteri, nöbetçiler denetimler |
| Su | Ritim ve zamanlama | Basınç dalgası şehrin altında ilerler; hızlı kapatırsan uzakta boru patlar |
| (PDF) GÖZLEM | Çıkarım | Binayı veri örüntüsünden tanımak |
| (PDF) KOD | Kural çıkarma | Örneklerden dönüşüm kuralını bulmak |
| (PDF) AĞ | Pac-Man benzeri kaçış | Kalabalık trafiğin içinde saklanmak |

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

**İşlem gücü.** Her operasyonun bedeli 10. Başarılı bir zarar verme operasyonu 25 kazandırır.

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

## Prototipin cevaplaması gereken sorular

1. Enerji mini oyununu oynamak keyifli mi?
2. Mini oyunda yapılan ile şehirde görülen arasında anlamlı bir bağ hissediliyor mu?
3. "Kimseyi feda etmeden ama iz bırakarak" ile "birini feda edip gizli kalarak" arasında seçim ilginç mi?
4. Doğal görünen sabotaj yolu keşfedilebilir mi, yoksa fazla mı gizli?

## Açık konular

- Sonların sayısı ve çubuktaki eşikleri.
- Tutorial'ın içeriği (A.I.'ın kaçışı?).
- Hangi sistemlerin ilk sürüme gireceği ve mini oyun havuzundan hangilerinin seçileceği.
- A.I.'ın kişiliği ve seslendirmesi; balonun görsel tasarımı.
- Oyunun adı ("A.I." Steam'de aranamaz).
