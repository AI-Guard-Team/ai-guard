using AIGuard.Application.Agents;

namespace AIGuard.UnitTests.Agents;

public class AgentIndicatorTests
{
    private static AgentIndicator I(string type, string name) => new(type, name);

    [Theory]
    [InlineData("VM_DETECTED", "VirtualBox", null, true)]
    [InlineData("HYPERVISOR_INFO", "Hipervizör", "WSL", true)]
    [InlineData("FORBIDDEN_APP", "AnyDesk", "AnyDesk.exe", true)]
    [InlineData("PHONE", "AnyDesk", null, false)]        // ajan yalnızca cihaz göstergesi gönderebilir
    [InlineData("forbidden_app", "AnyDesk", null, false)] // ortak adlar birebir yazılmalı
    [InlineData(null, "AnyDesk", null, false)]
    [InlineData("FORBIDDEN_APP", "", null, false)]
    [InlineData("FORBIDDEN_APP", null, null, false)]
    public void Only_device_indicator_types_with_a_name_are_accepted(string? type, string? name, string? detail, bool ok)
    {
        Assert.Equal(ok, new AgentIndicator(type, name, detail).IsWellFormed);
    }

    [Fact]
    public void Only_the_hypervisor_signal_is_informational()
    {
        Assert.True(I("HYPERVISOR_INFO", "Hipervizör").IsInformational);
        Assert.False(I("VM_DETECTED", "VMware").IsInformational);
        Assert.False(I("FORBIDDEN_APP", "Zoom").IsInformational);
    }

    [Fact]
    public void Indicator_is_new_only_when_it_was_not_in_the_previous_signal()
    {
        AgentIndicator[] previous = [I("FORBIDDEN_APP", "AnyDesk"), I("HYPERVISOR_INFO", "Hipervizör")];
        AgentIndicator[] current =
        [
            new("FORBIDDEN_APP", "AnyDesk", "başka ayrıntı"), // aynı tür + ad: ayrıntı değişse de yeni değil
            I("FORBIDDEN_APP", "Zoom"),
            I("FORBIDDEN_APP", "Zoom"),                        // aynı sinyalde iki kez: bir olay
            I("HYPERVISOR_INFO", "Hipervizör"),
        ];

        var fresh = AgentSignalService.NewIndicators(current, previous).ToList();

        Assert.Equal([I("FORBIDDEN_APP", "Zoom")], fresh);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("""["VM_DETECTED"]""")] // Y2'deki eski biçim
    [InlineData("bozuk")]
    public void Unreadable_stored_indicators_count_as_none(string? json)
    {
        Assert.Empty(AgentIndicator.ParseStored(json));
    }

    [Fact]
    public void Stored_indicators_round_trip()
    {
        AgentIndicator[] list = [new("VM_DETECTED", "VMware", "wmi:bios-serial")];

        Assert.Equal(list, AgentIndicator.ParseStored(System.Text.Json.JsonSerializer.Serialize(list)));
    }
}
