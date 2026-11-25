using Xunit;

namespace GameVoice.Tests;

public class VoiceRoomSetTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Floor_spans_all_rooms_and_blocks_only_shared_rooms()
    {
        var set = new VoiceRoomSet<int>();

        Assert.True(set.TryAcquireFloor(1, "Ann", new[] { 1, 2 }, nowUtc: Now));
        Assert.False(set.TryAcquireFloor(2, "Bob", new[] { 2, 3 }, nowUtc: Now));
        Assert.True(set.TryAcquireFloor(3, "Cara", new[] { 3 }, nowUtc: Now));

        Assert.True(set.IsFloorHeldByOther(new[] { 2, 3 }, 2, Now));
        Assert.Equal((1, "Ann"), set.GetFloorHolder(new[] { 2, 3 }, 2, Now));
        Assert.Null(set.GetFloorHolder(new[] { 1, 2 }, 1, Now));

        Assert.True(set.ReleaseAllFloors(1));
        Assert.False(set.TryAcquireFloor(2, "Bob", new[] { 2, 3 }, nowUtc: Now));
        Assert.True(set.ReleaseFloor(3, new[] { 3 }));
        Assert.True(set.TryAcquireFloor(2, "Bob", new[] { 2, 3 }, nowUtc: Now));
    }

    [Fact]
    public void Failed_acquire_takes_no_rooms()
    {
        var set = new VoiceRoomSet<int>();

        Assert.True(set.TryAcquireFloor(3, "Cara", new[] { 3 }, nowUtc: Now));
        Assert.False(set.TryAcquireFloor(2, "Bob", new[] { 2, 3 }, nowUtc: Now));
        Assert.True(set.TryAcquireFloor(1, "Ann", new[] { 1, 2 }, nowUtc: Now));
    }

    [Fact]
    public void Holder_can_renew_and_stale_floor_expires()
    {
        var set = new VoiceRoomSet<int>();

        Assert.True(set.TryAcquireFloor(1, "Ann", new[] { 1 }, holdSeconds: 15, nowUtc: Now));
        Assert.True(set.TryAcquireFloor(1, "Ann", new[] { 1 }, holdSeconds: 15, nowUtc: Now.AddSeconds(10)));
        Assert.False(set.TryAcquireFloor(2, "Bob", new[] { 1 }, nowUtc: Now.AddSeconds(20)));
        Assert.False(set.IsFloorHeldByOther(new[] { 1 }, 2, Now.AddSeconds(26)));
        Assert.True(set.TryAcquireFloor(2, "Bob", new[] { 1 }, nowUtc: Now.AddSeconds(26)));
    }

    [Fact]
    public void Mute_covers_the_users_rooms_and_only_the_muter_can_unmute()
    {
        var set = new VoiceRoomSet<int>();

        Assert.True(set.ToggleMute(1, "Ann", new[] { 1, 2 }).Ok);
        Assert.True(set.IsMuted(new[] { 2, 3 }));
        Assert.True(set.IsRoomMuted(1));
        Assert.False(set.IsRoomMuted(3));
        Assert.Equal((1, "Ann"), set.GetMuteInfo(new[] { 2 }));

        var (ok, message) = set.ToggleMute(2, "Bob", new[] { 2, 3 });
        Assert.False(ok);
        Assert.Contains("Ann", message);
        Assert.False(set.IsRoomMuted(3));

        Assert.True(set.ToggleMute(1, "Ann", new[] { 1, 2 }).Ok);
        Assert.False(set.IsMuted(new[] { 1, 2 }));
        Assert.Null(set.GetMuteInfo(new[] { 1, 2 }));
    }

    [Fact]
    public void Toggle_with_some_rooms_muted_unmutes_them()
    {
        var set = new VoiceRoomSet<int>();

        Assert.True(set.ToggleMute(1, "Ann", new[] { 1 }).Ok);
        Assert.True(set.ToggleMute(1, "Ann", new[] { 1, 2 }).Ok);
        Assert.False(set.IsRoomMuted(1));
        Assert.False(set.IsRoomMuted(2));
    }

    [Fact]
    public void StateChanged_fires_on_changes_only()
    {
        var set = new VoiceRoomSet<string>();
        var changes = 0;
        set.StateChanged += () => changes++;

        Assert.True(set.TryAcquireFloor(1, "Ann", new[] { "red" }, nowUtc: Now));
        Assert.False(set.TryAcquireFloor(2, "Bob", new[] { "red" }, nowUtc: Now));
        Assert.False(set.ReleaseFloor(2, new[] { "red" }));
        Assert.True(set.ToggleMute(1, "Ann", new[] { "red" }).Ok);

        Assert.Equal(2, changes);
    }

    [Fact]
    public void Invalid_input_is_rejected()
    {
        var set = new VoiceRoomSet<int>();

        Assert.False(set.TryAcquireFloor(0, "Nobody", new[] { 1 }, nowUtc: Now));
        Assert.False(set.TryAcquireFloor(1, "Ann", Array.Empty<int>(), nowUtc: Now));
        Assert.False(set.ToggleMute(1, "Ann", Array.Empty<int>()).Ok);
        Assert.False(set.ReleaseAllFloors(1));
        Assert.Null(set.FindRoom(1));
    }
}
