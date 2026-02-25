using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf2;
using StandRiseServer.Core;
using StandRiseServer.Models;
using Google.Protobuf;

namespace StandRiseServer.Services;

public class StorageService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public StorageService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;
        
        _handler.RegisterHandler("StorageRemoteService", "readAllFiles", ReadAllFilesAsync);
        _handler.RegisterHandler("StorageRemoteService", "readFile", ReadFileAsync);
        _handler.RegisterHandler("StorageRemoteService", "writeFile", WriteFileAsync);
        _handler.RegisterHandler("StorageRemoteService", "deleteFile", DeleteFileAsync);
        _handler.RegisterHandler("StorageRemoteService", "getStorage", ReadAllFilesAsync);
        _handler.RegisterHandler("StorageRemoteService", "setStorage", WriteFileAsync);
        _handler.RegisterHandler("StorageRemoteService", "getUserConfig", ReadAllFilesAsync);
        _handler.RegisterHandler("StorageRemoteService", "setUserConfig", WriteFileAsync);
    }

    private async Task ReadAllFilesAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var result = new BinaryValue { IsNull = false };
            
            foreach (var file in player.FileStorage)
            {
                var storage = new Storage
                {
                    Filename = file.Filename,
                    File = ByteString.CopyFrom(file.File.Select(i => (byte)i).ToArray())
                };
                
                result.Array.Add(ByteString.CopyFrom(storage.ToByteArray()));
            }

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ReadAllFiles: {ex.Message}");
        }
    }

    private async Task WriteFileAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("📝 WriteFile Request");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null) session.Client = client;
            }
            
            if (session == null || request.Params.Count < 2)
            {
                Console.WriteLine("❌ WriteFile: No session or params");
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var filename = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
            var fileData = ByteArray.Parser.ParseFrom(request.Params[1].One);
            
            Console.WriteLine($"📝 Writing file: {filename.Value} ({fileData.Value.Length} bytes)");

            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                Console.WriteLine("❌ WriteFile: Player not found");
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            // Find existing file or create new
            var existingFile = player.FileStorage.FirstOrDefault(f => f.Filename == filename.Value);
            if (existingFile != null)
            {
                // Update existing file
                existingFile.File = fileData.Value.ToByteArray().Select(b => (int)b).ToList();
                Console.WriteLine($"📝 Updated existing file: {filename.Value}");
            }
            else
            {
                // Create new file
                player.FileStorage.Add(new FileStorageItem
                {
                    Filename = filename.Value,
                    File = fileData.Value.ToByteArray().Select(b => (int)b).ToList()
                });
                Console.WriteLine($"📝 Created new file: {filename.Value}");
            }
            
            await _database.UpdatePlayerAsync(player);
            Console.WriteLine($"📝 File {filename.Value} saved to database");

            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ WriteFile: {ex.Message}");
            Console.WriteLine($"❌ Stack: {ex.StackTrace}");
        }
    }

    private async Task ReadFileAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("📁 ReadFile Request");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null) session.Client = client;
            }
            
            if (session == null || request.Params.Count == 0)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var filename = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
            Console.WriteLine($"📁 Reading file: {filename.Value}");

            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var file = player.FileStorage.FirstOrDefault(f => f.Filename == filename.Value);
            
            if (file != null)
            {
                var fileBytes = file.File.Select(i => (byte)i).ToArray();
                var byteArray = new ByteArray { Value = ByteString.CopyFrom(fileBytes) };
                var result = new BinaryValue 
                { 
                    IsNull = false, 
                    One = ByteString.CopyFrom(byteArray.ToByteArray()) 
                };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                Console.WriteLine($"📁 File {filename.Value} sent: {fileBytes.Length} bytes");
            }
            else
            {
                // Файл не найден - возвращаем пустой массив
                var result = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                Console.WriteLine($"📁 File {filename.Value} not found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ReadFile: {ex.Message}");
        }
    }

    private async Task DeleteFileAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🗑️ DeleteFile Request");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null) session.Client = client;
            }
            
            if (session == null || request.Params.Count == 0)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var filename = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
            Console.WriteLine($"🗑️ Deleting file: {filename.Value}");

            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var file = player.FileStorage.FirstOrDefault(f => f.Filename == filename.Value);
            if (file != null)
            {
                player.FileStorage.Remove(file);
                await _database.UpdatePlayerAsync(player);
                Console.WriteLine($"🗑️ File {filename.Value} deleted");
            }

            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ DeleteFile: {ex.Message}");
        }
    }

    private async Task SendUnauthorizedAsync(TcpClient client, string guid)
    {
        await _handler.WriteProtoResponseAsync(client, guid, null,
            new RpcException { Id = guid, Code = 401, Property = null });
    }
}
