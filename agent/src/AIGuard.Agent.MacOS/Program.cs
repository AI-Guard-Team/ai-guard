using AIGuard.Agent.Core;
using AIGuard.Agent.Core.Platform;
using AIGuard.Agent.MacOS;

var builder = AgentHost.CreateBuilder(args);

// Platforma özgü tek parça: ortak çekirdek geri kalanını kuruyor.
builder.Services.AddSingleton<IDeviceInspector, MacDeviceInspector>();

await AgentHost.Build(builder).RunAsync();
