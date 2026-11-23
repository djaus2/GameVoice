using Xunit;

namespace GameVoice.Tests;

public class ChatLogTests
{
    [Fact]
    public void TryAdd_normalises_and_rejects_blank_text()
    {
        var log = new ChatLog();

        Assert.False(log.TryAdd(1, "Ann", "   "));
        Assert.False(log.TryAdd(-1, "Ann", "hi"));
        Assert.True(log.TryAdd(1, "Ann", "  hello\r\nthere  "));

        Assert.Single(log);
        Assert.Equal("hello\nthere", log[0].Text);
    }

    [Fact]
    public void Format_skips_start_and_can_hide_voice()
    {
        var log = new ChatLog();
        log.AddSystem("game started");
        log.TryAdd(1, "Ann", "hi");
        log.TryAdd(2, "Bob", ChatMessage.VoicePrefix + "hello");

        Assert.Equal("System: game started\nAnn: hi\nBob: [VOICE] hello", log.Format());
        Assert.Equal("Ann: hi", log.Format(startIndex: 1, hideVoice: true));
        Assert.Equal(string.Empty, log.Format(startIndex: 3));
        Assert.True(log[2].IsVoice);
    }

    [Fact]
    public void Enumeration_is_a_snapshot()
    {
        var log = new ChatLog();
        log.TryAdd(1, "Ann", "one");

        foreach (var _ in log)
        {
            log.TryAdd(1, "Ann", "two");
        }

        Assert.Equal(2, log.Count);
        log.Clear();
        Assert.Empty(log);
    }
}

public class LobbyChatServiceTests
{
    [Fact]
    public void Group_messages_are_only_visible_to_members_but_admin_broadcasts_reach_everyone()
    {
        var chat = new LobbyChatService();
        chat.AddMessage(1, "Ann", "group 10 only", groupId: 10);
        chat.AddAdminBroadcast(99, "Admin", "everyone");

        var member = chat.GetMessages(2, new[] { 10 });
        var outsider = chat.GetMessages(3, new[] { 20 });

        Assert.Equal(2, member.Count);
        Assert.Equal("everyone", Assert.Single(outsider).Text);
        Assert.Equal("[ADMIN] Admin", outsider[0].SenderName);
    }

    [Fact]
    public void ClearChatForUser_hides_existing_messages_for_that_user_only()
    {
        var chat = new LobbyChatService();
        chat.AddMessage(1, "Ann", "before");
        chat.ClearChatForUser(2);
        chat.AddMessage(1, "Ann", "after");

        Assert.Equal("after", Assert.Single(chat.GetMessages(2)).Text);
        Assert.Equal(2, chat.GetMessages(3).Count);
    }

    [Fact]
    public void Keeps_the_last_200_messages_and_raises_ChatUpdated()
    {
        var chat = new LobbyChatService();
        var updates = 0;
        chat.ChatUpdated += () => updates++;

        for (var i = 0; i < 205; i++)
        {
            chat.AddMessage(1, "Ann", $"m{i}");
        }

        var messages = chat.GetMessages();
        Assert.Equal(200, messages.Count);
        Assert.Equal("m5", messages[0].Text);
        Assert.Equal(205, updates);
    }
}
