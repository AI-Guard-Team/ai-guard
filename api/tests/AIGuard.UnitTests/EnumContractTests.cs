using AIGuard.Domain.Enums;

namespace AIGuard.UnitTests;

/// <summary>
/// Enum adları diğer servislerle ortak kullanılıyor (K-05). Bir ad değişirse bu testler
/// hata verir; değişiklik öncesinde ekiple konuşulmalı.
/// </summary>
public class EnumContractTests
{
    [Fact]
    public void EventType_names_match_shared_contract_exactly()
    {
        string[] expected =
        [
            "FACE_MISSING", "MULTIPLE_FACES", "GAZE_AWAY", "PHONE", "IDENTITY_MISMATCH",
            "VERIFICATION_FAILED", "SPEECH", "TAB_BLUR", "FULLSCREEN_EXIT", "COPY_PASTE",
            "RIGHT_CLICK", "PERMISSION_REVOKED", "VM_DETECTED", "HYPERVISOR_INFO", "FORBIDDEN_APP",
            "VPN_PROXY", "IP_CHANGE", "AGENT_SIGNAL_LOST", "AGENT_SIGNAL_INVALID", "CONNECTION_LOST",
            "MONITORING_GAP",
        ];

        Assert.Equal(expected, Enum.GetNames<EventType>());
    }

    [Fact]
    public void SessionState_names_match_shared_contract_exactly()
    {
        string[] expected =
        [
            "ConsentPending", "AgentPairing", "IdentityVerification", "InProgress",
            "PermissionPending", "Disconnected", "Completed", "Cancelled", "Decided",
        ];

        Assert.Equal(expected, Enum.GetNames<SessionState>());
    }

    [Fact]
    public void SessionFlags_numeric_values_are_stable()
    {
        // Bayraklar veritabanında sayı olarak tutuluyor; mevcut değerler değişirse eski kayıtlar yanlış okunur.
        Assert.Equal(0, (int)SessionFlags.None);
        Assert.Equal(1, (int)SessionFlags.Risky);
        Assert.Equal(2, (int)SessionFlags.Indicated);
        Assert.Equal(4, (int)SessionFlags.NoAgent);
        Assert.Equal(8, (int)SessionFlags.MonitoringGap);
    }

    [Fact]
    public void UserRole_names_match_E2()
    {
        Assert.Equal(["Admin", "Reviewer", "Candidate"], Enum.GetNames<UserRole>());
    }
}
