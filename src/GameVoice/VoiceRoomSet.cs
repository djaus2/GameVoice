namespace GameVoice;

/// <summary>
/// A set of <see cref="VoiceRoom"/>s keyed by <typeparamref name="TKey"/> (e.g. a group id), with floor and mute
/// operations that span several rooms at once. For apps where one user talks to many rooms, such as WalkieTalkie
/// groups: a talker must hold the floor in all of their rooms, and listeners are only affected through rooms they
/// share. Rooms are created on first use. Thread-safe; change rooms through the set so multi-room operations stay atomic.
/// </summary>
public sealed class VoiceRoomSet<TKey> where TKey : notnull
{
    private readonly object _sync = new();
    private readonly Dictionary<TKey, VoiceRoom> _rooms = new();

    /// <summary>Raised after a floor or mute change.</summary>
    public event Action? StateChanged;

    public VoiceRoom GetOrAddRoom(TKey roomId)
    {
        lock (_sync)
        {
            return GetOrAddUnlocked(roomId);
        }
    }

    public VoiceRoom? FindRoom(TKey roomId)
    {
        lock (_sync)
        {
            return _rooms.TryGetValue(roomId, out var room) ? room : null;
        }
    }

    public bool RemoveRoom(TKey roomId)
    {
        bool removed;
        lock (_sync)
        {
            removed = _rooms.Remove(roomId);
        }

        if (removed) OnStateChanged();
        return removed;
    }

    /// <summary>
    /// Atomically takes (or renews) the floor in all of <paramref name="roomIds"/>.
    /// Fails, taking nothing, if another user holds an unexpired floor in any of them.
    /// </summary>
    public bool TryAcquireFloor(int userId, string? userName, IReadOnlyCollection<TKey> roomIds, int holdSeconds = VoiceRoom.DefaultFloorHoldSeconds, DateTime? nowUtc = null)
    {
        if (userId <= 0 || roomIds is null || roomIds.Count == 0) return false;
        var now = nowUtc ?? DateTime.UtcNow;

        lock (_sync)
        {
            foreach (var id in roomIds)
            {
                if (_rooms.TryGetValue(id, out var room) && room.IsFloorHeldByOther(userId, now)) return false;
            }

            foreach (var id in roomIds)
            {
                GetOrAddUnlocked(id).TryAcquireFloor(userId, userName, holdSeconds, now);
            }
        }

        OnStateChanged();
        return true;
    }

    /// <summary>Releases the floor in each of <paramref name="roomIds"/> held by <paramref name="userId"/>.</summary>
    public bool ReleaseFloor(int userId, IEnumerable<TKey> roomIds)
    {
        if (userId <= 0 || roomIds is null) return false;

        var released = false;
        lock (_sync)
        {
            foreach (var id in roomIds)
            {
                if (_rooms.TryGetValue(id, out var room) && room.ReleaseFloor(userId)) released = true;
            }
        }

        if (released) OnStateChanged();
        return released;
    }

    /// <summary>Releases every floor held by <paramref name="userId"/>.</summary>
    public bool ReleaseAllFloors(int userId)
    {
        if (userId <= 0) return false;

        var released = false;
        lock (_sync)
        {
            foreach (var room in _rooms.Values)
            {
                if (room.ReleaseFloor(userId)) released = true;
            }
        }

        if (released) OnStateChanged();
        return released;
    }

    /// <summary>The first unexpired floor holder in <paramref name="roomIds"/> other than <paramref name="excludeUserId"/>.</summary>
    public (int UserId, string UserName)? GetFloorHolder(IEnumerable<TKey> roomIds, int excludeUserId, DateTime? nowUtc = null)
    {
        if (roomIds is null) return null;
        var now = nowUtc ?? DateTime.UtcNow;

        lock (_sync)
        {
            foreach (var id in roomIds)
            {
                if (_rooms.TryGetValue(id, out var room) && room.IsFloorHeldByOther(excludeUserId, now) && room.FloorUserId is int holder)
                {
                    return (holder, room.FloorUserName ?? string.Empty);
                }
            }
        }

        return null;
    }

    public bool IsFloorHeldByOther(IEnumerable<TKey> roomIds, int userId, DateTime? nowUtc = null)
        => GetFloorHolder(roomIds, userId, nowUtc).HasValue;

    public bool IsRoomMuted(TKey roomId)
    {
        lock (_sync)
        {
            return _rooms.TryGetValue(roomId, out var room) && room.IsMuted;
        }
    }

    /// <summary>True if any of <paramref name="roomIds"/> is muted.</summary>
    public bool IsMuted(IEnumerable<TKey> roomIds) => GetMuteInfo(roomIds).HasValue;

    /// <summary>Who muted the first muted room in <paramref name="roomIds"/>, if any.</summary>
    public (int ByUserId, string ByUserName)? GetMuteInfo(IEnumerable<TKey> roomIds)
    {
        if (roomIds is null) return null;

        lock (_sync)
        {
            foreach (var id in roomIds)
            {
                if (_rooms.TryGetValue(id, out var room) && room.IsMuted)
                {
                    return (room.MutedByUserId ?? 0, room.MutedByUserName ?? string.Empty);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Mutes all of <paramref name="roomIds"/>, or, if any are muted, unmutes them.
    /// Only the user who muted can unmute.
    /// </summary>
    public (bool Ok, string? Message) ToggleMute(int userId, string? userName, IReadOnlyCollection<TKey> roomIds)
    {
        if (userId <= 0) return (false, "Invalid user");
        if (roomIds is null || roomIds.Count == 0) return (false, "Not in any voice room");

        lock (_sync)
        {
            var muted = new List<VoiceRoom>();
            foreach (var id in roomIds)
            {
                if (_rooms.TryGetValue(id, out var room) && room.IsMuted && !muted.Contains(room)) muted.Add(room);
            }

            if (muted.Count > 0)
            {
                var other = muted.FirstOrDefault(r => r.MutedByUserId != userId);
                if (other is not null)
                {
                    return (false, $"Only {other.MutedByUserName ?? "the user who muted"} can unmute");
                }

                foreach (var room in muted)
                {
                    room.ToggleMute(userId, userName);
                }
            }
            else
            {
                foreach (var room in roomIds.Distinct().Select(GetOrAddUnlocked))
                {
                    room.ToggleMute(userId, userName);
                }
            }
        }

        OnStateChanged();
        return (true, null);
    }

    private VoiceRoom GetOrAddUnlocked(TKey roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            room = new VoiceRoom();
            _rooms[roomId] = room;
        }

        return room;
    }

    private void OnStateChanged()
    {
        try
        {
            StateChanged?.Invoke();
        }
        catch
        {
        }
    }
}
