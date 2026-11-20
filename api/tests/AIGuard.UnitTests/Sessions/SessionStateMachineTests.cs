using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Domain.Sessions;

namespace AIGuard.UnitTests.Sessions;

/// <summary>Durum makinesi (E4, GAB 3.4.4). Kart: her geçiş için test; tablo dışı her geçiş reddedilmeli.</summary>
public class SessionStateMachineTests
{
    // GAB 3.4.4 tablosu; kodla ayrı yazıldı ki biri değişirse test fark etsin.
    private static readonly (SessionState From, SessionState To)[] Expected =
    [
        (SessionState.ConsentPending, SessionState.AgentPairing),
        (SessionState.ConsentPending, SessionState.Cancelled),
        (SessionState.AgentPairing, SessionState.IdentityVerification),
        (SessionState.IdentityVerification, SessionState.InProgress),
        (SessionState.InProgress, SessionState.PermissionPending),
        (SessionState.PermissionPending, SessionState.InProgress),
        (SessionState.InProgress, SessionState.Disconnected),
        (SessionState.Disconnected, SessionState.InProgress),
        (SessionState.InProgress, SessionState.Completed),
        (SessionState.PermissionPending, SessionState.Completed),
        (SessionState.Disconnected, SessionState.Completed),
        (SessionState.InProgress, SessionState.Cancelled),
        (SessionState.PermissionPending, SessionState.Cancelled),
        (SessionState.Disconnected, SessionState.Cancelled),
        (SessionState.Completed, SessionState.Decided),
    ];

    public static IEnumerable<object[]> AllPairs() =>
        from source in Enum.GetValues<SessionState>()
        from target in Enum.GetValues<SessionState>()
        select new object[] { source, target };

    private static ExamSession Session(SessionState state) => new() { State = state };

    [Fact]
    public void Table_matches_the_requirements_document()
    {
        Assert.Equal(Expected.ToHashSet(), SessionStateMachine.Transitions.ToHashSet());
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void Every_pair_is_allowed_only_if_it_is_in_the_table(SessionState from, SessionState to)
    {
        var session = Session(from);

        if (Expected.Contains((from, to)))
        {
            session.TransitionTo(to);
            Assert.Equal(to, session.State);
        }
        else
        {
            var ex = Assert.Throws<InvalidSessionTransitionException>(() => session.TransitionTo(to));
            Assert.Equal((from, to), (ex.From, ex.To));
            Assert.Equal(from, session.State); // reddedilen geçiş durumu değiştirmiyor
        }
    }

    [Fact]
    public void Closed_states_have_no_way_out_except_decision()
    {
        Assert.All(SessionStateMachine.Closed, closed =>
            Assert.All(Enum.GetValues<SessionState>().Where(s => s != SessionState.Decided), to =>
                Assert.False(closed != SessionState.Completed && SessionStateMachine.CanTransition(closed, to))));
        Assert.Equal([SessionState.InProgress, SessionState.PermissionPending, SessionState.Disconnected],
            SessionStateMachine.Running.OrderBy(s => s));
    }
}
