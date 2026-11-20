using AIGuard.Agent.Core;
using AIGuard.Agent.Core.Platform;
using AIGuard.Agent.Windows;
using Microsoft.Extensions.Options;

// Aday ajanı çift tıklayıp açıyor; açılan pencere ajanın çalıştığını gösteriyor (Y4, kurulum notu).
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Title = "AI-Guard güvenlik ajanı";
Console.WriteLine("AI-Guard güvenlik ajanı çalışıyor. Sınav bitene kadar bu pencereyi kapatmayın.");
Console.WriteLine();

var builder = AgentHost.CreateBuilder(args);

// Platforma özgü parçalar: ortak çekirdek geri kalanını kuruyor.
builder.Services.AddSingleton<IWindowsSystemProbe, WindowsSystemProbe>();
builder.Services.AddSingleton(sp =>
    ForbiddenAppList.Load(sp.GetRequiredService<IOptions<AgentOptions>>().Value.ResolveForbiddenAppsPath()));
builder.Services.AddSingleton<IDeviceInspector, WindowsDeviceInspector>();

await AgentHost.Build(builder).RunAsync();
