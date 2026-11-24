using System.Text.Json.Serialization;
using AIGuard.Api;
using AIGuard.Api.Auth;
using AIGuard.Application;
using AIGuard.Application.Biometrics;
using AIGuard.Application.Events;
using AIGuard.Application.Risk;
using AIGuard.Infrastructure;
using AIGuard.Infrastructure.Persistence;
using AIGuard.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

// Risk ağırlıkları ve eşikler (A2). Dosya eksik veya hatalıysa uygulama açılmıyor.
builder.Configuration.AddJsonFile("riskconfig.json", optional: false, reloadOnChange: false);
builder.Services.AddOptions<RiskOptions>()
    .Bind(builder.Configuration.GetSection(RiskOptions.SectionName))
    .ValidateOnStart();

// Bilinen VPN/veri merkezi IP blokları (Y4, FR-23). Bozuk CIDR varsa uygulama açılmıyor.
builder.Configuration.AddJsonFile("vpn-ip-blocks.json", optional: false, reloadOnChange: false);
builder.Services.AddOptions<VpnIpBlockOptions>()
    .Bind(builder.Configuration.GetSection(VpnIpBlockOptions.SectionName))
    .ValidateOnStart();

// Doğrulama eşikleri ve rıza metni sürümleri (A3); değerlerin varsayılanı sınıfta.
builder.Services.AddOptions<BiometricOptions>()
    .Bind(builder.Configuration.GetSection(BiometricOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// JWT doğrulaması; varsayılan olarak her uç giriş istiyor (E2, K-20).
builder.Services.AddJwtAuthentication();

builder.Services.AddControllers().AddJsonOptions(o =>
    // Enum'lar kartlardaki adlarla ("TAB_BLUR", "InProgress"); sayı olarak kabul edilmiyor (K-05).
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)));
builder.Services.AddProblemDetails();

// A4: değerlendirici bildirimleri (/hubs/review). Enum'lar REST yanıtlarıyla aynı biçimde (ad olarak).
builder.Services.AddSignalR().AddJsonProtocol(o =>
    o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)));
builder.Services.AddSingleton<AIGuard.Application.Abstractions.IReviewNotifier, AIGuard.Api.Review.SignalRReviewNotifier>();

// Swagger (E3): yalnızca Development'ta /swagger adresinde.
builder.Services.AddApiDocumentation();

// Docker Compose ve CI, API'nin ve veritabanı bağlantısının ayakta olduğunu bu uçla kontrol ediyor.
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

// Compose'da web (nginx) API'nin önünde; gerçek istemci IP'si yalnızca ondan kabul ediliyor (E1b, K-16).
var behindProxy = builder.Services.AddTrustedProxies(builder.Configuration);

// Y2: 30 sn sinyal gelmeyen oturumlara AGENT_SIGNAL_LOST, biten oturumların anahtarlarını silme.
builder.Services.AddHostedService<AIGuard.Api.Agents.AgentSignalWatchdog>();

// E4: süresi dolan oturumu otomatik teslim, istek gelmeyen oturumu bağlantı kesik sayma.
builder.Services.AddHostedService<AIGuard.Api.Sessions.SessionTimekeeperService>();

// A5: saklama süresi dolan kanıt kareleri, izleme verisi, yüz vektörleri, rıza ve denetim kayıtları (günde bir).
builder.Services.AddHostedService<AIGuard.Api.DataProtection.DataRetentionWorker>();

// E4: tabloda olmayan durum geçişi 409 (ProblemDetails).
builder.Services.AddExceptionHandler<AIGuard.Api.Sessions.SessionTransitionExceptionHandler>();

var app = builder.Build();

// Yakalanmayan istisnalar ProblemDetails olarak; durum geçişi hatası 409 (E4).
app.UseExceptionHandler();

if (behindProxy)
{
    // Diğer her şeyden önce: SessionIpGuard RemoteIpAddress'i okurken gerçek istemci IP'sini görsün.
    app.UseForwardedHeaders();
}

// Kimlik doğrulamadan önce: Swagger sayfası girişsiz açılıyor, uçlar "Authorize" ile deneniyor.
app.UseApiDocumentation();

app.UseAuthentication();
app.UseAuthorization();

// Compose ve CI giriş yapmadan kontrol ediyor.
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();
// Belirtecin süresi dolunca (15 dk) bağlantı kapanıyor; istemci yeni belirteçle yeniden bağlanıyor. Böylece
// pasife alınan ya da rolü değişen kullanıcı açık bağlantıyla bildirim almaya devam edemiyor (E2, FR-02).
app.MapHub<AIGuard.Api.Review.ReviewHub>(AIGuard.Api.Review.ReviewHub.Path, o => o.CloseOnAuthenticationExpiration = true);

// Göçler burada uygulanmıyor (K-07); örnek veri yalnızca Development'ta yükleniyor (K-06).
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DevDataSeeder>().SeedAsync();
}

app.Run();

// Entegrasyon testlerinde WebApplicationFactory<Program> kullanabilmek için.
public partial class Program;
