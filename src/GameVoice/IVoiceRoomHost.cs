namespace GameVoice;

public enum VoiceRoomChange
{
    Floor,
    Mute,
    Preferences
}

/// <summary>Implemented by the app service that owns the rooms (e.g. the game service).</summary>
public interface IVoiceRoomHost
{
    VoiceRoom? FindVoiceRoom(string roomId);

    /// <summary>Called after a room changed, so the host can touch its timeouts and notify its UI.</summary>
    void OnVoiceRoomChanged(string roomId, VoiceRoomChange change);
}
