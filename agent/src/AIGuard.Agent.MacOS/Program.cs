using AIGuard.Agent.Core;
using AIGuard.Agent.Core.Platform;
using AIGuard.Agent.MacOS;

var builder = AgentHost.CreateBuilder(args);

// Platforma özgü parçalar: ortak çekirdek geri kalanını kuruyor.
builder.Services.AddSingleton<IMacSystemProbe, MacSystemProbe>();
builder.Services.AddSingleton<IDeviceInspector, MacDeviceInspector>();

await AgentHost.Build(builder).RunAsync();
