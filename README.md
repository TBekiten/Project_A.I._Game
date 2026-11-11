# A.I. — Unity prototipi

Bilinçlenen bir yapay zekâyı oynadığımız strateji, simülasyon ve bulmaca oyunu.
Tasarım kararları: [docs/TASARIM.md](docs/TASARIM.md).

## İlk prototip: Doğu Mahallesi kesintisi

Akşam 18:42'de mahalleyi izleyerek başlarsın. 19:00'da hastane trafosu yanar ve bir olay kartı açılır:
**kurtar**, **zarar ver** ya da **görmezden gel**. Kurtarmayı ya da zarar vermeyi seçersen ayrı bir
enerji şebekesi ekranına geçersin. Orada hatları açıp kapatır, yükleri atar ya da geri verirsin. Bitirince
şehre dönersin ve sonucu orada izlersin: ışıklar söner ya da yanar, trafik kilitlenir, ambulans gecikir,
haberler olanı yorumlar.

## Kurulum

Repoda yalnızca kod ve belgeler var. Unity proje ayarlarını (URP dahil) bir kez Unity'nin kendi şablonundan alacağız.

1. Unity Hub'dan **Unity 6.3 LTS** sürümünü kur.
2. Repoyu bilgisayarına klonla.
3. Unity Hub → **New project** → **Universal 3D** şablonu → herhangi bir geçici konumda oluştur, sonra Unity'yi kapat.
4. Oluşan projenin içindeki **Assets**, **Packages** ve **ProjectSettings** klasörlerini klonladığın repo
   klasörüne kopyala. Assets klasörü birleşecek; üzerine yazma sorusu çıkarsa evet de.
   (Şablonun `Assets/TutorialInfo` klasörünü ve `Readme` dosyasını istersen silebilirsin.)
5. Unity Hub → **Add project from disk** → repo klasörünü seç ve projeyi aç.
6. Unity'nin üst menüsünde **A.I. → Prototip sahnesini oluştur**'a tıkla, sonra **Play**'e bas.
7. Unity'nin ürettiği `.meta` dosyalarını, `Packages/` ve `ProjectSettings/` klasörlerini commit'leyip push'la.
   Böylece sonraki oturumlarda gerçek proje ayarları üzerinden çalışabilirim.

Konsolda hata çıkarsa metnini olduğu gibi bana gönder.

## Kontroller

| Tuş | İşlev |
|---|---|
| Tab | Şehirde sistem katmanını aç/kapat (hatlar, yükler) |
| W A S D / oklar | Kamerayı kaydır |
| Fare tekerleği | Yakınlaş / uzaklaş |
| Boşluk | Şehir saatini durdur |
| Sol tık (mini oyunda) | Anahtar: hattı aç/kapat · Yük: devreden çıkar / geri ver |
| Balona tıklamak | A.I.'ın sözünü hızlandır / geç |
| R (sonda) | Baştan başla |

## Klasör yapısı

```
Assets/_Project/
  Scripts/Core/      Unity'den bağımsız simülasyon: şebeke, operasyon, sonuç değerlendirmesi, senaryo
  Scripts/Game/      Unity tarafı: şehir, trafik, mini oyun ekranı, kamera, arayüz, oyun akışı
  Scripts/Editor/    "A.I." menüsü (sahne kurulumu)
  Tests/EditMode/    Çekirdek testleri (Unity Test Runner'da da görünür)
Tools/DotnetCheck/   Unity açmadan derleme ve test (aşağıya bak)
docs/                Tasarım belgeleri
```

Sahnede tek bir bileşen var (`PrototypeGame`); şehir, kamera, mini oyun ekranı ve arayüz çalışma anında
koddan kuruluyor. Şehir şimdilik Unity'nin hazır şekilleriyle yapılmış bir yer tutucu; gerçek low-poly
modeller geldiğinde aynı bağlantı noktalarıyla değiştirilecek.

## Unity açmadan kontrol

.NET 8 SDK ile:

```bash
dotnet test Tools/DotnetCheck/Tests
```

```bash
dotnet build Tools/DotnetCheck/Game
```

İlki çekirdek simülasyonu derleyip testleri çalıştırır. İkincisi oyun kodunu Unity motor modüllerinin referans
derlemelerine karşı derler. URP'ye bağlı küçük kısım ve Editor kodu yalnızca Unity içinde doğrulanır.

## Bilinen sınırlar

- Arayüz IMGUI ile yapıldı. Kurulum gerektirmediği için seçildi; gerçek arayüz tasarımında UI Toolkit'e geçilecek.
- Sokak lambalarının zemine düşen ışıkları eksik görünürse URP renderer'ında Rendering Path'i **Forward+** yap
  (`Assets/Settings` içindeki PC renderer dosyası).
