# AI-Guard API imajı (E1b). İki hedef var:
#   target: api      -> ASP.NET Core uygulaması (port 8080)
#   target: migrate  -> EF Core göç paketi; göçleri uygulayıp çıkar (K-07 / S-4)
# Bağlam (context) api/ klasörüdür; docker-compose.yml bunu ayarlıyor.

# ---------- Derleme ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Önce yalnızca proje dosyaları: kod değişse de paketler önbellekten gelir (hızlı yeniden build).
COPY global.json Directory.Build.props ./
COPY .config/dotnet-tools.json .config/
COPY src/AIGuard.Domain/AIGuard.Domain.csproj src/AIGuard.Domain/
COPY src/AIGuard.Application/AIGuard.Application.csproj src/AIGuard.Application/
COPY src/AIGuard.Infrastructure/AIGuard.Infrastructure.csproj src/AIGuard.Infrastructure/
COPY src/AIGuard.Api/AIGuard.Api.csproj src/AIGuard.Api/
RUN dotnet restore src/AIGuard.Api/AIGuard.Api.csproj \
 && dotnet tool restore

COPY src/ src/
RUN dotnet publish src/AIGuard.Api/AIGuard.Api.csproj -c Release --no-restore -o /out/api /p:UseAppHost=false

# Repodaki dotnet-ef sürümüyle (8.0.11) tek dosyalık göç paketi. Çalışırken
# DesignTimeDbContextFactory bağlantıyı ConnectionStrings__Default'tan okuyor.
RUN dotnet ef migrations bundle \
      --project src/AIGuard.Infrastructure \
      --startup-project src/AIGuard.Api \
      --configuration Release \
      --output /out/efbundle

# ---------- Göç adımı ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS migrate
WORKDIR /app
COPY --from=build /out/efbundle ./efbundle
# Root olmayan kullanıcı (.NET 8 imajlarında hazır gelen "app").
USER $APP_UID
ENTRYPOINT ["./efbundle"]

# ---------- API ----------
# Son aşama olduğu için düz "docker build" de bu imajı üretir.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api
WORKDIR /app
COPY --from=build /out/api .
# .NET 8 imajları varsayılan olarak 8080'i dinler; kartta istenen port da bu.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Şifreli kanıt kareleri (A3, K-26). Uygulama root olmadan çalıştığı için klasör "app" kullanıcısına
# veriliyor; compose'daki volume ilk oluşturulurken bu sahipliği alıyor.
RUN mkdir -p /app/data/evidence && chown $APP_UID /app/data/evidence
USER $APP_UID
ENTRYPOINT ["dotnet", "AIGuard.Api.dll"]
