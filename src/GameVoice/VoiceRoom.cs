namespace GameVoice;

/// <summary>
/// Shared voice state for one room (a game, a group, a channel...): who holds the talk floor,
/// whether voice is muted, and each user's preferred TTS voice. Owned by the host app; thread-safe.
/// </summary>
public sealed class VoiceRoom
{
    public const int DefaultFloorHoldSeconds = 15;
    public const int MinFloorHoldSeconds = 3;

    private readonly object _sync = new();
    private readonly Dictionary<int, VoicePreference> _preferences = new();

    public int? FloorUserId { get; private set; }
    public string? FloorUserName { get; private set; }
    public DateTime? FloorUntilUtc { get; private set; }

    public bool IsMuted { get; private set; }
    public int? MutedByUserId { get; private set; }
    public string? MutedByUserName { get; private set; }

    /// <summary>Takes or renews the talk floor. Fails if another user holds an unexpired floor.</summary>
    public bool TryAcquireFloor(int userId, string? userName, int holdSeconds = DefaultFloorHoldSeconds, DateTime? nowUtc = null)
    {
        if (userId <= 0) return false;
        var now = nowUtc ?? DateTime.UtcNow;

        lock (_sync)
        {
            if (FloorUntilUtc.HasValue && FloorUntilUtc.Value <= now)
            {
                ClearFloorUnlocked();
            }

            if (FloorUserId.HasValue && FloorUserId.Value != userId) return false;

            FloorUserId = userId;
            FloorUserName = userName ?? string.Empty;
            FloorUntilUtc = now.AddSeconds(Math.Max(MinFloorHoldSeconds, holdSeconds));
            return true;
        }
    }

    /// <summary>Releases the floor if <paramref name="userId"/> holds it.</summary>
    public bool ReleaseFloor(int userId)
    {
        if (userId <= 0) return false;
        lock (_sync)
        {
            if (FloorUserId != userId) return false;
            ClearFloorUnlocked();
            return true;
        }
    }

    public void ClearFloor()
    {
        lock (_sync)
        {
            ClearFloorUnlocked();
        }
    }

    public bool IsFloorHeldBy(int userId, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        lock (_sync)
        {
            return FloorUserId.HasValue && FloorUserId.Value == userId
                && FloorUntilUtc.HasValue && FloorUntilUtc.Value > now;
        }
    }

    public bool IsFloorHeldByOther(int userId, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        lock (_sync)
        {
            return FloorUserId.HasValue && FloorUserId.Value != userId
                && FloorUntilUtc.HasValue && FloorUntilUtc.Value > now;
        }
    }

    /// <summary>Mutes voice for everyone in the room, or unmutes it. Only the user who muted can unmute.</summary>
    public (bool Ok, string? Message) ToggleMute(int userId, string? userName)
    {
        if (userId <= 0) return (false, "Invalid user");

        lock (_sync)
        {
            if (IsMuted)
            {
                if (MutedByUserId != userId)
                {
                    return (false, $"Only {MutedByUserName ?? "the player who muted"} can unmute");
                }

                IsMuted = false;
                MutedByUserId = null;
                MutedByUserName = null;
            }
            else
            {
                IsMuted = true;
                MutedByUserId = userId;
                MutedByUserName = userName;
            }

            return (true, null);
        }
    }

    public bool CanUnmute(int userId) => IsMuted && MutedByUserId == userId;

    public void SetPreference(int userId, VoicePreference preference)
    {
        if (userId <= 0) return;
        lock (_sync)
        {
            _preferences[userId] = preference ?? VoicePreference.Default;
        }
    }

    public VoicePreference GetPreference(int userId)
    {
        if (userId <= 0) return VoicePreference.Default;
        lock (_sync)
        {
            return _preferences.TryGetValue(userId, out var p) ? p : VoicePreference.Default;
        }
    }

    public string? GetPreferredVoiceKey(int userId) => GetPreference(userId).VoiceKey;
    public string? GetPreferredLanguage(int userId) => GetPreference(userId).Language;
    public string? GetPreferredRegion(int userId) => GetPreference(userId).Region;

    public void ClearPreferences()
    {
        lock (_sync)
        {
            _preferences.Clear();
        }
    }

    /// <summary>Clears the floor, preferences and mute state.</summary>
    public void Reset()
    {
        lock (_sync)
        {
            ClearFloorUnlocked();
            _preferences.Clear();
            IsMuted = false;
            MutedByUserId = null;
            MutedByUserName = null;
        }
    }

    private void ClearFloorUnlocked()
    {
        FloorUserId = null;
        FloorUserName = null;
        FloorUntilUtc = null;
    }
}
