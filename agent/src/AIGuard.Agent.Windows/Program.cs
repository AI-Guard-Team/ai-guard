using AIGuard.Agent.Core;
using AIGuard.Agent.Core.Platform;
using AIGuard.Agent.Windows;
using Microsoft.Extensions.Options;

var builder = AgentHost.CreateBuilder(args);

// Platforma özgü parçalar: ortak çekirdek geri kalanını kuruyor.
builder.Services.AddSingleton<IWindowsSystemProbe, WindowsSystemProbe>();
builder.Services.AddSingleton(sp =>
    ForbiddenAppList.Load(sp.GetRequiredService<IOptions<AgentOptions>>().Value.ResolveForbiddenAppsPath()));
builder.Services.AddSingleton<IDeviceInspector, WindowsDeviceInspector>();

await AgentHost.Build(builder).RunAsync();
