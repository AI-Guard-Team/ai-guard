using System.Text.Json.Serialization;
using AIGuard.Application;
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

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers().AddJsonOptions(o =>
    // Enum'lar kartlardaki adlarla ("TAB_BLUR", "InProgress"); sayı olarak kabul edilmiyor (K-05).
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)));
builder.Services.AddProblemDetails();

// Docker Compose ve CI, API'nin ve veritabanı bağlantısının ayakta olduğunu bu uçla kontrol ediyor.
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapControllers();

// Göçler burada uygulanmıyor (K-07); örnek veri yalnızca Development'ta yükleniyor (K-06).
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DevDataSeeder>().SeedAsync();
}

app.Run();

// Entegrasyon testlerinde WebApplicationFactory<Program> kullanabilmek için.
public partial class Program;
