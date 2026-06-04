using System.Collections.Concurrent;

namespace CarRentalSystem.Services;

public class ChatMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

public class ChatSession
{
    public List<ChatMessage> Messages { get; set; } = new();
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
}

public interface IChatSessionService
{
    List<ChatMessage> GetOrCreateSession(string sessionId);
    void AddUserMessage(string sessionId, string content);
    void AddAssistantMessage(string sessionId, string content);
    void ClearSession(string sessionId);
}

public class ChatSessionService : IChatSessionService
{
    private static readonly ConcurrentDictionary<string, ChatSession> _sessions = new();
    private static readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);

    public List<ChatMessage> GetOrCreateSession(string sessionId)
    {
        CleanupExpiredSessions();
        var session = _sessions.GetOrAdd(sessionId, _ => new ChatSession());
        session.LastActivity = DateTime.UtcNow;
        return session.Messages;
    }

    public void AddUserMessage(string sessionId, string content)
    {
        var session = _sessions.GetOrAdd(sessionId, _ => new ChatSession());
        session.LastActivity = DateTime.UtcNow;
        session.Messages.Add(new ChatMessage { Role = "user", Content = content });
        CleanupExpiredSessions();
    }

    public void AddAssistantMessage(string sessionId, string content)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.LastActivity = DateTime.UtcNow;
            session.Messages.Add(new ChatMessage { Role = "assistant", Content = content });
        }
    }

    public void ClearSession(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }

    private void CleanupExpiredSessions()
    {
        var now = DateTime.UtcNow;
        foreach (var key in _sessions.Keys.ToList())
        {
            if (_sessions.TryGetValue(key, out var session) &&
                now - session.LastActivity > _sessionTimeout)
            {
                _sessions.TryRemove(key, out _);
            }
        }
    }
}
