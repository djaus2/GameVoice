namespace GameVoice;

/// <summary>Room-id based voice operations that notify the <see cref="IVoiceRoomHost"/> after each change.</summary>
public sealed class VoiceRoomService
{
    private readonly IVoiceRoomHost _host;

    public VoiceRoomService(IVoiceRoomHost host)
    {
        _host = host;
    }

    /// <summary>Raised after the host is notified of a change (room id, change kind). Used by <c>VoiceControls</c> to re-render.</summary>
    public event Action<string, VoiceRoomChange>? RoomChanged;

    private void Notify(string roomId, VoiceRoomChange change)
    {
        _host.OnVoiceRoomChanged(roomId, change);
        RoomChanged?.Invoke(roomId, change);
    }

    public VoiceRoom? GetRoom(string? roomId)
        => string.IsNullOrWhiteSpace(roomId) ? null : _host.FindVoiceRoom(roomId);

    public bool TryAcquireFloor(string? roomId, int userId, string? userName, int holdSeconds = VoiceRoom.DefaultFloorHoldSeconds)
    {
        if (userId <= 0) return false;
        var room = GetRoom(roomId);
        if (room is null) return false;
        if (!room.TryAcquireFloor(userId, userName, holdSeconds)) return false;
        Notify(roomId!, VoiceRoomChange.Floor);
        return true;
    }

    public bool ReleaseFloor(string? roomId, int userId)
    {
        if (userId <= 0) return false;
        var room = GetRoom(roomId);
        if (room is null) return false;
        if (!room.ReleaseFloor(userId)) return false;
        Notify(roomId!, VoiceRoomChange.Floor);
        return true;
    }

    public (bool Ok, string? Message) ToggleMute(string? roomId, int userId, string? userName)
    {
        if (string.IsNullOrWhiteSpace(roomId)) return (false, "No game");
        if (userId <= 0) return (false, "Invalid user");
        var room = GetRoom(roomId);
        if (room is null) return (false, "Game not found");

        var result = room.ToggleMute(userId, userName);
        if (result.Ok) Notify(roomId, VoiceRoomChange.Mute);
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
        Notify(roomId!, VoiceRoomChange.Preferences);
        return true;
    }

    public VoicePreference GetPreference(string? roomId, int userId)
        => GetRoom(roomId)?.GetPreference(userId) ?? VoicePreference.Default;
}
