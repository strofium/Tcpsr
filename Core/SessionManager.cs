// SessionManager.cs
using System.Collections.Concurrent;
using System.Net.Sockets;
using StandRiseServer.Models;

namespace StandRiseServer.Core;

public class SessionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, PlayerSession> _sessions = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _friendSubscriptions = new(); // friendId -> list of subscriber tokens
    private readonly ConcurrentDictionary<string, Timer> _sessionTimers = new();
    private readonly ILogger<SessionManager> _logger;
    private readonly Timer _cleanupTimer;
    private const int SessionTimeoutMinutes = 5;

    public SessionManager(ILogger<SessionManager> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(CleanupDeadSessions, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public void AddSession(PlayerSession session)
    {
        // Удаляем старые сессии для того же клиента
        var oldSessions = _sessions.Values.Where(s => s.Client == session.Client).ToList();
        foreach (var oldSession in oldSessions)
        {
            RemoveSession(oldSession.Token);
        }
        
        session.LastActivityTime = DateTime.UtcNow;
        _sessions[session.Token] = session;
        
        // Таймер для обновления TimeInGame (каждую минуту)
        var timer = new Timer(state => UpdateSessionTime(session.Token), null, 
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        _sessionTimers[session.Token] = timer;
        
        _logger.LogInformation("Session added: {Token}, Total sessions: {Count}", 
            session.Token, _sessions.Count);
    }

    public void RemoveSession(string token)
    {
        if (_sessions.TryRemove(token, out var session))
        {
            // Останавливаем таймер
            if (_sessionTimers.TryRemove(token, out var timer))
            {
                timer?.Dispose();
            }

            // Удаляем подписки
            foreach (var subscriptions in _friendSubscriptions.Values)
            {
                subscriptions.Remove(token);
            }

            _logger.LogInformation("Session removed: {Token}, Total sessions: {Count}", 
                token, _sessions.Count);
        }
    }

    public PlayerSession? GetSessionByToken(string token)
    {
        if (_sessions.TryGetValue(token, out var session))
        {
            session.LastActivityTime = DateTime.UtcNow;
            return session;
        }
        return null;
    }

    public PlayerSession? GetSessionByClient(TcpClient client)
    {
        var session = _sessions.Values.FirstOrDefault(s => s.Client == client);
        if (session != null)
        {
            session.LastActivityTime = DateTime.UtcNow;
        }
        return session;
    }

    public PlayerSession? GetSessionByPlayerId(string playerObjectId)
    {
        var session = _sessions.Values.FirstOrDefault(s => s.PlayerObjectId == playerObjectId);
        session?.UpdateActivity();
        return session;
    }

    public IEnumerable<PlayerSession> GetSessionsByPlayerId(string playerObjectId)
    {
        return _sessions.Values.Where(s => s.PlayerObjectId == playerObjectId);
    }

    public void UpdateSessionActivity(string token)
    {
        if (_sessions.TryGetValue(token, out var session))
        {
            session.LastActivityTime = DateTime.UtcNow;
        }
    }

    private void UpdateSessionTime(string token)
    {
        if (_sessions.TryGetValue(token, out var session))
        {
            session.TimeInGame += 60; // Добавляем минуту
        }
    }

    private void CleanupDeadSessions(object? state)
    {
        var now = DateTime.UtcNow;
        var deadSessions = _sessions.Values
            .Where(s => (now - s.LastActivityTime).TotalMinutes > SessionTimeoutMinutes)
            .ToList();

        foreach (var session in deadSessions)
        {
            _logger.LogWarning("Cleaning up dead session: {Token}, last activity: {LastActivity}", 
                session.Token, session.LastActivityTime);
            
            if (session.Client?.Connected == true)
            {
                try
                {
                    session.Client.Close();
                }
                catch { /* Ignore */ }
            }
            
            RemoveSession(session.Token);
        }
    }

    // Подписки на статусы друзей
    public void SubscribeToFriendStatus(string subscriberToken, string friendId)
    {
        var subscriptions = _friendSubscriptions.GetOrAdd(friendId, _ => new HashSet<string>());
        lock (subscriptions)
        {
            subscriptions.Add(subscriberToken);
        }
        _logger.LogDebug("Subscriber {Token} subscribed to friend {FriendId}", subscriberToken, friendId);
    }

    public void UnsubscribeFromFriendStatus(string subscriberToken, string friendId)
    {
        if (_friendSubscriptions.TryGetValue(friendId, out var subscriptions))
        {
            lock (subscriptions)
            {
                subscriptions.Remove(subscriberToken);
                if (subscriptions.Count == 0)
                {
                    _friendSubscriptions.TryRemove(friendId, out _);
                }
            }
        }
    }

    public IEnumerable<string> GetFriendSubscribers(string friendId)
    {
        if (_friendSubscriptions.TryGetValue(friendId, out var subscriptions))
        {
            lock (subscriptions)
            {
                return subscriptions.ToList();
            }
        }
        return Enumerable.Empty<string>();
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        foreach (var timer in _sessionTimers.Values)
        {
            timer?.Dispose();
        }
        _sessionTimers.Clear();
    }
}
