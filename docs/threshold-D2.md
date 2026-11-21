# D2: yüz benzerliği eşiği (LFW)

*2026-11-21 · `vision/scripts/calibrate_threshold.py` ile ölçüldü. Kart: D2. Sonuç `vision/config.yaml` → `verify.match_threshold`.*

## Sonuç

| | |
|---|---|
| Hedef | FAR ≈ %1 |
| **Eşik** (`similarity ≥ eşik` → aynı kişi) | **0,164** |
| FAR | %0,97 (29 / 2997 farklı kişi çifti) |
| FRR | %0,93 (28 / 2996 aynı kişi çifti) |
| Doğruluk | %99,05 |
| FRR, yüz bulunamayan aynı kişi çiftleri de ret sayılırsa | %1,07 |

Ölçümden önceki başlangıç değeri 0,32'ydi (deepface'in ArcFace için kosinüs uzaklığı eşiği 0,68). Farklı kişi çiftlerinin %99'u 0,164'ün altında kaldığı için 0,32'de FAR sıfıra yakın olurdu, ama aynı kişi çiftlerinin %1'i 0,203'ün, %5'i 0,402'nin altında; FRR yükselirdi. Bizim hattımız için o değer gereğinden katıydı.

## Yöntem

- **Veri:** LFW, hizalanmamış orijinal görüntüler (`lfw.tgz`) ve resmî "View 2" çift listesi (`pairs.txt`: 10 kat × 300 aynı + 300 farklı kişi çifti = 6000 çift, 7701 farklı görüntü). Resmî site (`vis-www.cs.umass.edu`) erişilemez olduğu için iki dosya da scikit-learn'ün kullandığı figshare aynasından indirildi. Veri seti repoya girmiyor (`/vision/datasets/`, `.gitignore`).
- **Hat:** servisle aynı kod. `app.images.decode_bytes` → MediaPipe Face Landmarker → 5 noktayla hizalama → ArcFace `w600k_mbf` → L2 normalize; benzerlik iki vektörün iç çarpımı (kosinüs).
- **Birden fazla yüz:** 625 görüntüde arka planda başka yüzler de var. Bu görüntülerde en büyük yüz (LFW'nin ortadaki öznesi) alındı. Servis bu durumda `MULTIPLE_FACES` döner; burada ölçülen, vektörün ayırt ediciliği.
- **Yüz bulunamayanlar:** MediaPipe 7701 görüntünün 5'inde yüz bulamadı (`Amber_Tamblyn_0002`, `Franz_Muentefering_0003`, `Peter_Mugyeni_0001`, `Phil_Gramm_0002`, `Xanana_Gusmao_0002`). Bunları içeren 7 çift (4 aynı, 3 farklı kişi) eşik hesabından çıkarıldı.
- **Eşik:** farklı kişi çiftlerinden en fazla ⌊%1 × N⌋ tanesinin geçtiği en küçük eşik, üç basamağa yukarı yuvarlandı. Oranlar yuvarlanmış eşikte hesaplandı. Kat (fold) ayrımı yapılmadı: eşik 6000 çiftin tamamında seçilip aynı çiftlerde raporlandı, yani bu bir eğitim içi (in-sample) ölçüm.

## Benzerlik dağılımı

| Yüzdelik | p1 | p5 | p50 | p95 | p99 |
|---|---:|---:|---:|---:|---:|
| Aynı kişi | 0,203 | 0,402 | 0,615 | 0,795 | 0,866 |
| Farklı kişi | −0,147 | −0,103 | 0,006 | 0,112 | 0,164 |

## Sınırlılıklar

- **%1 FAR'ın anlamı:** rastgele seçilen 100 yabancıdan biri bu eşikte eşleşir. Sınavda kopya çeken kişi rastgele değil, adaya benzeyen biri olabilir; LFW bunu ölçmüyor.
- **İstatistiksel belirsizlik:** %1 FAR yalnızca 29 farklı kişi çiftine dayanıyor. Eşik, dağılımın en uç kısmında seçildiği için örnek değiştikçe birkaç binde bir oynayabilir.
- **Alan farkı:** LFW, ünlülerin web fotoğrafları. Bizde karşılaştırılan çift, adayın yüklediği kayıt fotoğrafı ile sınav başında web kamerasından alınan 3–5 kare (kare benzerliklerinin ortalaması). Işık, kamera kalitesi ve zaman farkı FRR'yi artırabilir. Eşik D6'da gerçek kayıt fotoğrafı ve kamera kareleriyle yeniden ölçülmeli; değişirse yalnızca `config.yaml` değişir (K-27, S-19).
- **Model:** MobileFaceNet (`w600k_mbf`). %99,05 doğruluk bu eşikte (FAR %1 kısıtıyla); en iyi doğruluk eşiği ayrıca aranmadı. K-28'e göre ölçüm yetersiz görülürse ResNet50'ye (`w600k_r50`) geçilir; bu sonuç bunu gerektirmiyor.

## Yeniden üretme

```powershell
cd vision
# datasets/lfw/pairs.txt ve datasets/lfw/lfw/<Ad>/<Ad>_0001.jpg hazır olmalı
.venv\Scripts\python.exe scripts/calibrate_threshold.py --far 0.01
```

Bu bilgisayarda (i5-12450H) 7701 görüntü yaklaşık 3 dakika sürüyor.
