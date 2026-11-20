using AIGuard.Agent.Core;
using AIGuard.Agent.Core.Platform;
using AIGuard.Agent.Windows;

var builder = AgentHost.CreateBuilder(args);

// Platforma özgü tek parça: ortak çekirdek geri kalanını kuruyor.
builder.Services.AddSingleton<IDeviceInspector, WindowsDeviceInspector>();

await AgentHost.Build(builder).RunAsync();
