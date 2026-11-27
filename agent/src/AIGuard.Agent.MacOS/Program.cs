using AIGuard.Agent.Core;
using AIGuard.Agent.Core.Platform;
using AIGuard.Agent.MacOS;
using Microsoft.Extensions.Options;

// Aday ajanı çift tıklayıp açıyor (Ajanı Başlat.command); açılan Terminal penceresi ajanın çalıştığını gösteriyor (S6, kurulum notu).
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("AI-Guard güvenlik ajanı çalışıyor. Sınav bitene kadar bu pencereyi kapatmayın.");
Console.WriteLine();

var builder = AgentHost.CreateBuilder(args);

// Platforma özgü parçalar: ortak çekirdek geri kalanını kuruyor.
builder.Services.AddSingleton<IMacSystemProbe, MacSystemProbe>();
builder.Services.AddSingleton(sp =>
    ForbiddenAppList.Load(sp.GetRequiredService<IOptions<AgentOptions>>().Value.ResolveForbiddenAppsPath()));
builder.Services.AddSingleton<IDeviceInspector, MacDeviceInspector>();

await AgentHost.Build(builder).RunAsync();
