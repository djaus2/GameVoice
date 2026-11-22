using Xunit;

namespace GameVoice.Tests;

public class VoiceRoomTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Floor_is_exclusive_until_released()
    {
        var room = new VoiceRoom();

        Assert.True(room.TryAcquireFloor(1, "Ann", nowUtc: Now));
        Assert.False(room.TryAcquireFloor(2, "Bob", nowUtc: Now));
        Assert.True(room.IsFloorHeldByOther(2, Now));
        Assert.True(room.IsFloorHeldBy(1, Now));

        Assert.False(room.ReleaseFloor(2));
        Assert.True(room.ReleaseFloor(1));
        Assert.True(room.TryAcquireFloor(2, "Bob", nowUtc: Now));
        Assert.Equal("Bob", room.FloorUserName);
    }

    [Fact]
    public void Holder_can_renew_and_stale_floor_expires()
    {
        var room = new VoiceRoom();

        Assert.True(room.TryAcquireFloor(1, "Ann", holdSeconds: 15, nowUtc: Now));
        Assert.True(room.TryAcquireFloor(1, "Ann", holdSeconds: 15, nowUtc: Now.AddSeconds(10)));
        Assert.Equal(Now.AddSeconds(25), room.FloorUntilUtc);

        Assert.False(room.TryAcquireFloor(2, "Bob", nowUtc: Now.AddSeconds(24)));
        Assert.True(room.TryAcquireFloor(2, "Bob", nowUtc: Now.AddSeconds(25)));
    }

    [Fact]
    public void Hold_time_has_a_minimum()
    {
        var room = new VoiceRoom();
        room.TryAcquireFloor(1, "Ann", holdSeconds: 0, nowUtc: Now);
        Assert.Equal(Now.AddSeconds(VoiceRoom.MinFloorHoldSeconds), room.FloorUntilUtc);
    }

    [Fact]
    public void Only_the_muter_can_unmute()
    {
        var room = new VoiceRoom();

        Assert.True(room.ToggleMute(1, "Ann").Ok);
        Assert.True(room.IsMuted);
        Assert.Equal("Ann", room.MutedByUserName);

        var (ok, msg) = room.ToggleMute(2, "Bob");
        Assert.False(ok);
        Assert.Equal("Only Ann can unmute", msg);
        Assert.True(room.IsMuted);

        Assert.True(room.ToggleMute(1, "Ann").Ok);
        Assert.False(room.IsMuted);
        Assert.Null(room.MutedByUserId);
    }

    [Fact]
    public void Preferences_are_normalized_and_per_user()
    {
        var room = new VoiceRoom();
        room.SetPreference(1, VoicePreference.Normalize("  name:Jenny|lang:en-AU ", " EN ", " au "));

        var p = room.GetPreference(1);
        Assert.Equal("name:Jenny|lang:en-AU", p.VoiceKey);
        Assert.Equal("en", p.Language);
        Assert.Equal("AU", p.Region);
        Assert.True(room.GetPreference(2).IsDefault);
        Assert.True(VoicePreference.Normalize(" ", "", null).IsDefault);
    }

    [Fact]
    public void Invalid_users_are_rejected()
    {
        var room = new VoiceRoom();
        Assert.False(room.TryAcquireFloor(0, "x"));
        Assert.False(room.ToggleMute(0, "x").Ok);
        room.SetPreference(0, new VoicePreference("k", null, null));
        Assert.True(room.GetPreference(0).IsDefault);
    }

    [Fact]
    public void Reset_clears_everything()
    {
        var room = new VoiceRoom();
        room.TryAcquireFloor(1, "Ann");
        room.ToggleMute(1, "Ann");
        room.SetPreference(1, new VoicePreference("k", "en", "AU"));

        room.Reset();

        Assert.Null(room.FloorUserId);
        Assert.False(room.IsMuted);
        Assert.True(room.GetPreference(1).IsDefault);
    }
}

public class VoiceRoomServiceTests
{
    private sealed class Host : IVoiceRoomHost
    {
        public Dictionary<string, VoiceRoom> Rooms { get; } = new();
        public List<(string RoomId, VoiceRoomChange Change)> Changes { get; } = new();
        public VoiceRoom? FindVoiceRoom(string roomId) => Rooms.GetValueOrDefault(roomId);
        public void OnVoiceRoomChanged(string roomId, VoiceRoomChange change) => Changes.Add((roomId, change));
    }

    [Fact]
    public void Notifies_host_only_on_successful_changes()
    {
        var host = new Host();
        host.Rooms["g1"] = new VoiceRoom();
        var svc = new VoiceRoomService(host);
        var events = new List<(string, VoiceRoomChange)>();
        svc.RoomChanged += (id, c) => events.Add((id, c));

        Assert.True(svc.TryAcquireFloor("g1", 1, "Ann"));
        Assert.False(svc.TryAcquireFloor("g1", 2, "Bob"));
        Assert.False(svc.TryAcquireFloor("missing", 1, "Ann"));
        Assert.True(svc.ReleaseFloor("g1", 1));
        Assert.True(svc.SetPreference("g1", 1, "k", "EN", "au"));
        Assert.True(svc.ToggleMute("g1", 1, "Ann").Ok);
        Assert.Equal((false, "Game not found"), svc.ToggleMute("missing", 1, "Ann"));

        Assert.Equal(
            new[] { VoiceRoomChange.Floor, VoiceRoomChange.Floor, VoiceRoomChange.Preferences, VoiceRoomChange.Mute },
            host.Changes.Select(c => c.Change));
        Assert.Equal("en", svc.GetPreference("g1", 1).Language);
        Assert.Equal(host.Changes, events);
    }
}

public class VoiceCatalogTests
{
    [Theory]
    [InlineData("Microsoft Natasha Online (Natural)", "en_AU", "microsoft_natasha_online_natural_en_au")]
    [InlineData("Alex", "", "alex")]
    [InlineData("", "en-US", "en_us")]
    [InlineData(null, null, "")]
    public void NormalizeToken(string? name, string? lang, string expected)
        => Assert.Equal(expected, VoiceCatalog.NormalizeToken(name, lang));

    [Fact]
    public void BuildCanonicalVoiceKey()
    {
        Assert.Equal("name:Alex|lang:en-US", VoiceCatalog.BuildCanonicalVoiceKey(" Alex ", "en-US"));
        Assert.Equal("name:Alex", VoiceCatalog.BuildCanonicalVoiceKey("Alex", null));
        Assert.Equal("", VoiceCatalog.BuildCanonicalVoiceKey("  ", "en"));
    }
}
