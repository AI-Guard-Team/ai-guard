# D1 benchmark: görüntü/ses modellerinin işlemci süreleri

*2026-11-21 · `vision/scripts/benchmark.py` ile üretildi; elle düzenlemeyin, betiği yeniden çalıştırın. Kart: D1, Talimat 9. adım. Kararlar: MIMARI-KARARLAR K-28.*

## Bilgisayar

| | |
|---|---|
| İşlemci | 12th Gen Intel(R) Core(TM) i5-12450H |
| Çekirdek / iş parçacığı | 8 fiziksel / 12 mantıksal |
| RAM | 15.7 GB |
| İşletim sistemi | Microsoft Windows 11 Home Single Language (derleme 26300) |
| Python | 3.11.15 |
| Paketler | numpy 2.4.6, onnxruntime 1.32.0, mediapipe 1.2.3, opencv-contrib-python 5.1.0.95 |
| GPU | Kullanılmıyor (yalnızca işlemci) |

## Yöntem

- Kareler: bilgisayarın kamerası, 640x480, 100 kare. Görüntüler yalnızca bellekte tutuldu, diske yazılmadı; bu belgede görüntü yok.
- Yüz bulunan kare oranı: **%100**. ArcFace, canlılık ve zincirler yalnızca yüzlü karelerde ölçüldü.
- Ses: 1 sn, 16 kHz mono **sentetik** sinyal (harmonikli, hece zarflı ses + gürültü). Silero'nun süresi içerikten neredeyse bağımsız.
- Her ölçüm 105 kez çalıştı; ilk 5'i ısınma sayılıp atıldı. Süreler tek bir çağrının duvar saati süresi (ms); isteğin çözülmesi (Base64, JPEG) ve ağ dahil değil.
- Kart hedefleri p95 ile karşılaştırıldı.
- `VISION_ORT_THREADS` yalnızca onnxruntime modellerini (ArcFace, MiniFASNet, YOLO11n, Silero) etkiler; MediaPipe'ın Python API'sinde iş parçacığı ayarı yok. 0 = onnxruntime varsayılanı.

## Sonuçlar

### VISION_ORT_THREADS = 1

| İş | Ortalama | p50 | p95 | En kötü | Kart hedefi (p95) |
|---|---:|---:|---:|---:|---|
| Yüz noktaları + kafa yönü (MediaPipe) | 9.1 | 9.0 | 10.1 | 10.6 | 150 ms: geçti |
| ArcFace (hizalama dahil) | 10.8 | 10.7 | 11.8 | 12.2 | — |
| Eşleştirme: yüz noktaları + ArcFace | 20.4 | 20.3 | 21.5 | 21.8 | 200 ms: geçti |
| Canlılık: MiniFASNetV2 + V1SE (kırpma dahil) | 4.1 | 4.0 | 4.8 | 5.5 | — |
| Doğrulama zinciri: yüz noktaları + ArcFace + canlılık (1 kare) | 24.6 | 24.4 | 25.7 | 30.0 | — |
| Telefon: YOLO11n (letterbox dahil) | 78.8 | 78.5 | 81.6 | 85.8 | 300 ms: geçti |
| Konuşma: Silero VAD (1 sn, 16 kHz) | 4.0 | 3.9 | 4.5 | 5.6 | — |

### VISION_ORT_THREADS = 2

| İş | Ortalama | p50 | p95 | En kötü | Kart hedefi (p95) |
|---|---:|---:|---:|---:|---|
| Yüz noktaları + kafa yönü (MediaPipe) | 9.8 | 9.4 | 12.2 | 16.4 | 150 ms: geçti |
| ArcFace (hizalama dahil) | 6.7 | 6.5 | 7.5 | 10.3 | — |
| Eşleştirme: yüz noktaları + ArcFace | 16.9 | 16.7 | 19.5 | 19.8 | 200 ms: geçti |
| Canlılık: MiniFASNetV2 + V1SE (kırpma dahil) | 3.3 | 3.2 | 4.0 | 4.3 | — |
| Doğrulama zinciri: yüz noktaları + ArcFace + canlılık (1 kare) | 20.4 | 20.1 | 22.8 | 25.1 | — |
| Telefon: YOLO11n (letterbox dahil) | 48.1 | 46.7 | 57.6 | 88.4 | 300 ms: geçti |
| Konuşma: Silero VAD (1 sn, 16 kHz) | 3.6 | 3.5 | 4.1 | 5.1 | — |

### VISION_ORT_THREADS = 0 (onnxruntime varsayılanı)

| İş | Ortalama | p50 | p95 | En kötü | Kart hedefi (p95) |
|---|---:|---:|---:|---:|---|
| Yüz noktaları + kafa yönü (MediaPipe) | 11.1 | 9.9 | 16.5 | 26.3 | 150 ms: geçti |
| ArcFace (hizalama dahil) | 4.4 | 4.4 | 4.8 | 5.7 | — |
| Eşleştirme: yüz noktaları + ArcFace | 16.5 | 16.1 | 19.4 | 24.3 | 200 ms: geçti |
| Canlılık: MiniFASNetV2 + V1SE (kırpma dahil) | 9.3 | 4.1 | 36.0 | 62.4 | — |
| Doğrulama zinciri: yüz noktaları + ArcFace + canlılık (1 kare) | 52.3 | 50.8 | 114.1 | 193.1 | — |
| Telefon: YOLO11n (letterbox dahil) | 32.3 | 31.9 | 36.2 | 40.5 | 300 ms: geçti |
| Konuşma: Silero VAD (1 sn, 16 kHz) | 4.7 | 4.6 | 5.6 | 7.0 | — |

## İşçi kapasitesi (teorik)

Bir adayın saniyede gerektirdiği işlemci süresi = yüz+kafa + telefon/3 + kimlik/60 + ses = **42.2 ms/sn** (VISION_ORT_THREADS = 1 ölçümünün p95 değerleriyle).

| Aday | Toplam yük (ms/sn) | Gereken çekirdek | Önerilen VISION_WORKERS (VISION_ORT_THREADS=1) | Bu bilgisayara sığıyor mu (8 fiziksel çekirdek) |
|---:|---:|---:|---:|---|
| 10 | 422 | 0.42 | 1 | evet |
| 25 | 1055 | 1.06 | 2 | evet |
| 50 | 2110 | 2.11 | 4 | evet |

Varsayımlar:

- Zamanlama D4'e göre: yüz + kafa yönü her saniye, telefon 3 sn'de bir, kimlik 60 sn'de bir; ses 1 sn'lik parçalar (S-22). Kimlik için eşleştirme zinciri (yüz noktaları + ArcFace) sayıldı.
- Sınav başı doğrulama (3–5 kare) adaybaşına bir kez yapıldığı için sürekli yüke katılmadı.
- Her işçi istekleri sırayla, `VISION_ORT_THREADS=1` ile yaklaşık bir çekirdekte işler. İşçi sayısı = ⌈gereken çekirdek / 0.7⌉; %70 kullanım payı MediaPipe'ın kendi iş parçacıkları, Base64/JPEG çözme, JSON ve ağ için.
- p95 değerleri toplandığı için hesap kötümser; istekler aynı anda gelirse kuyruk gecikmesi hesaba katılmadı.
- **Bu teorik bir hesap.** Gerçek eşzamanlılık H3 deneyinde (A6, Locust) 10, 25 ve 50 adayla ölçülecek.

## Varsayılanlar ve H3

- `.env.example` ve `docker-compose.yml`: `VISION_WORKERS=2`, `VISION_ORT_THREADS=1`. Her işçi modelleri ayrı yüklediği için (aşağıdaki tablo) günlük kullanım ve entegrasyon için 2 işçi seçildi; 25 adaya kadar yetiyor.
- H3'ün 50 adaylık senaryosunda .env'de VISION_WORKERS=4 yapılmalı (A6).
- Bu ölçüm geliştirme bilgisayarında yapıldı. H3 başka bir bilgisayarda koşacaksa benchmark orada da çalıştırılmalı ve işçi sayısı o sonuca göre seçilmeli:

  ```
  cd vision
  python scripts/download_models.py
  python scripts/benchmark.py --threads 1,2,0
  ```

## İşçi başına bellek

uvicorn `--workers 2` ile açıldı, modeller yüklendikten sonra ölçüldü (modeller her işçide ayrı yükleniyor).

| İşçi | Çalışma kümesi (RSS) | Özel bellek |
|---:|---:|---:|
| 1 | 224 MB | 606 MB |
| 2 | 224 MB | 606 MB |
