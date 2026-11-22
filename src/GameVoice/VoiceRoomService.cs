namespace GameVoice;

/// <summary>Room-id based voice operations that notify the <see cref="IVoiceRoomHost"/> after each change.</summary>
public sealed class VoiceRoomService
{
    private readonly IVoiceRoomHost _host;

    public VoiceRoomService(IVoiceRoomHost host)
    {
        _host = host;
    }

    public VoiceRoom? GetRoom(string? roomId)
        => string.IsNullOrWhiteSpace(roomId) ? null : _host.FindVoiceRoom(roomId);

    public bool TryAcquireFloor(string? roomId, int userId, string? userName, int holdSeconds = VoiceRoom.DefaultFloorHoldSeconds)
    {
        if (userId <= 0) return false;
        var room = GetRoom(roomId);
        if (room is null) return false;
        if (!room.TryAcquireFloor(userId, userName, holdSeconds)) return false;
        _host.OnVoiceRoomChanged(roomId!, VoiceRoomChange.Floor);
        return true;
    }

    public bool ReleaseFloor(string? roomId, int userId)
    {
        if (userId <= 0) return false;
        var room = GetRoom(roomId);
        if (room is null) return false;
        if (!room.ReleaseFloor(userId)) return false;
        _host.OnVoiceRoomChanged(roomId!, VoiceRoomChange.Floor);
        return true;
    }

    public (bool Ok, string? Message) ToggleMute(string? roomId, int userId, string? userName)
    {
        if (string.IsNullOrWhiteSpace(roomId)) return (false, "No game");
        if (userId <= 0) return (false, "Invalid user");
        var room = GetRoom(roomId);
        if (room is null) return (false, "Game not found");

        var result = room.ToggleMute(userId, userName);
        if (result.Ok) _host.OnVoiceRoomChanged(roomId, VoiceRoomChange.Mute);
        return result;
    }

    public bool SetPreference(string? roomId, int userId, string? voiceKey, string? language, string? region)
        => SetPreference(roomId, userId, VoicePreference.Normalize(voiceKey, language, region));

    public bool SetPreference(string? roomId, int userId, VoicePreference preference)
    {
        if (userId <= 0) return false;
        var room = GetRoom(roomId);
        if (room is null) return false;
        room.SetPreference(userId, preference);
        _host.OnVoiceRoomChanged(roomId!, VoiceRoomChange.Preferences);
        return true;
    }

    public VoicePreference GetPreference(string? roomId, int userId)
        => GetRoom(roomId)?.GetPreference(userId) ?? VoicePreference.Default;
}
