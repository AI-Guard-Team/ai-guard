using AIGuard.Application;
using AIGuard.Infrastructure;
using AIGuard.Infrastructure.Persistence;
using AIGuard.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Docker Compose ve CI, API'nin ve veritabanı bağlantısının ayakta olduğunu bu uçla kontrol ediyor.
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

app.MapHealthChecks("/health");

// Göçler burada uygulanmıyor (K-07); örnek veri yalnızca Development'ta yükleniyor (K-06).
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DevDataSeeder>().SeedAsync();
}

app.Run();

// Entegrasyon testlerinde WebApplicationFactory<Program> kullanabilmek için.
public partial class Program;
