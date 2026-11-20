# AI-Guard — yapay zekâ araçları için bağlam

Çevrim içi sınavlar için sıfır güven (zero-trust) kopya tespit platformu. Doğuş Üniversitesi YM 491 Araştırma Projesi. Final teslim: 21 Aralık 2026.
Görev kartları Trello'da (kart kodları: E*, A*, Y*, S*, D*). Kaynak belgeler: GAB, Talimat (YM491-INST-001), Öneri Formu, Trello kartları.

## İşe başlamadan önce oku

- **`docs/MIMARI-KARARLAR.md`**: alınmış tasarım kararları (K-01…) ve açık konular (S-…). Bir karara aykırı kod yazma; değişiklik gerekirse önce ekibe sor, sonra belgeyi ve kodu aynı PR'da güncelle.
- `docs/er-diagram.md`: veritabanı modeli.
- `CONTRIBUTING.md`: dal, commit ve PR kuralları.

## Yapı

- `web/` React + TypeScript + Vite
- `api/` ASP.NET Core Web API (.NET 8), katmanlı: Api, Application, Domain, Infrastructure; EF Core + PostgreSQL 16; SignalR
- `vision/` Python 3.11 FastAPI, yalnızca iç ağ; api'den `X-Service-Key` ile çağrılır
- `agent/` .NET güvenlik ajanı (ortak çekirdek + Windows/macOS), yerel adres `http://127.0.0.1:47321`
- `docs/` belgeler

## Kurallar

- `main` ve `dev` korumalı (PR + 1 onay + 4 CI kontrolü). Doğrudan push yok. Her iş `feature/<kart-kodu>-kisa-ad` dalında, PR `dev`'e açılır.
- Commit: `<tür>(<kart-kodu>): <kısa açıklama>`; tür: feat, fix, test, docs, refactor, chore. Açıklama Türkçe.
- Yapılandırma ortam değişkenlerinden veya user-secrets'tan okunur (`.env.example`). Gizli anahtar, şifre, bağlantı dizesi koda veya repoya yazılmaz.
- Veri setleri, yüz fotoğrafları, ses/video kayıtları ve model ağırlıkları repoya eklenmez.
- Testlerde Testcontainers kullanılmaz; testler yerel veya CI PostgreSQL'ine bağlanır (K-09).
- Her değişiklikle birlikte test yaz; CI her PR'da build + test çalıştırır.
- **API'de her uç varsayılan olarak giriş ister** (K-20). Uca rol ver (`[Authorize(Roles = nameof(UserRole.Admin))]`); herkese açık uç gerekiyorsa `[AllowAnonymous]` ve gerekçesini yaz. Aday oturum uçları `SessionAccessService.IsCandidateOfAsync` ile sahiplik kontrolü yapar.
- **Oturum durumu yalnızca `session.TransitionTo(...)` ile değişir** (K-24); `State`'e doğrudan atama yapma. Sınavı başlatmak ve kesintiden dönmek için `SessionService.BeginExamAsync` / `ResumeAfterReconnectAsync` kullan.
- Kartta yazmayan bir özelliği kendiliğinden ekleme; gerekiyorsa önce sor.

## Ortak adlar (değiştirme)

- SessionState: ConsentPending, AgentPairing, IdentityVerification, InProgress, PermissionPending, Disconnected, Completed, Cancelled, Decided
- EventType: FACE_MISSING, MULTIPLE_FACES, GAZE_AWAY, PHONE, IDENTITY_MISMATCH, VERIFICATION_FAILED, SPEECH, TAB_BLUR, FULLSCREEN_EXIT, COPY_PASTE, RIGHT_CLICK, PERMISSION_REVOKED, VM_DETECTED, HYPERVISOR_INFO, FORBIDDEN_APP, VPN_PROXY, IP_CHANGE, AGENT_SIGNAL_LOST, AGENT_SIGNAL_INVALID, CONNECTION_LOST, MONITORING_GAP
- Roller: Admin, Reviewer, Candidate
- Vision uçları: /internal/face/enroll, /internal/face/verify, /internal/analyze/frame, /internal/analyze/audio
- Kimlik doğrulama uçları ve gövdeleri: `docs/MIMARI-KARARLAR.md` K-20

Trello kartındaki uç, olay ve durum adları esastır; farklı bir ad gerekiyorsa önce sor.
