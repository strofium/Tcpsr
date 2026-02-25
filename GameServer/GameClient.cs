using System.Net.Sockets;

namespace StandRiseServer.GameServer;

public class GameClient : IDisposable
{
    public string Id { get; }
    public string? PlayerId { get; set; }
    public string? PlayerName { get; set; }
    public GameRoom? CurrentRoom { get; set; }
    public bool IsReady { get; set; }
    public int Team { get; set; }
    
    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private bool _disposed;

    public GameClient(string id, TcpClient tcpClient)
    {
        Id = id;
        _tcpClient = tcpClient;
        _stream = tcpClient.GetStream();
    }

    public async Task SendAsync(byte[] data)
    {
        if (_disposed) return;
        
        try
        {
            // Send length prefix + data
            var lengthBytes = BitConverter.GetBytes(data.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);
            
            await _stream.WriteAsync(lengthBytes);
            await _stream.WriteAsync(data);
            await _stream.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Send error to {Id}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        try
        {
            _stream?.Close();
            _tcpClient?.Close();
        }
        catch { }
    }
}
