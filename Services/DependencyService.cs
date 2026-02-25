using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using StandRiseServer.Core;
using Google.Protobuf;
using System.IO.Compression;
using System.Text;

namespace StandRiseServer.Services;

public class DependencyService
{
    private readonly ProtobufHandler _handler;
    private readonly string _dependenciesPath;

    public DependencyService(ProtobufHandler handler)
    {
        _handler = handler;
        _dependenciesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies");
        
        _handler.RegisterHandler("DependencyService", "getDependencies", GetDependenciesAsync);
        _handler.RegisterHandler("DependencyService", "getScripts", GetScriptsAsync);
        
        InitializeDependencies();
    }

    private void InitializeDependencies()
    {
        try
        {
            Directory.CreateDirectory(_dependenciesPath);
            
            // Создаем список зависимостей Unity
            var dependencies = new Dictionary<string, string>
            {
                ["Axlebolt.RpcSupport.dll"] = "Network RPC Support Library",
                ["Axlebolt.Bolt.Api.dll"] = "Bolt API Library", 
                ["Google.Protobuf.dll"] = "Google Protobuf Library",
                ["TextMeshPro.dll"] = "TextMeshPro UI Library"
            };

            var dependencyList = Path.Combine(_dependenciesPath, "dependencies.txt");
            var sb = new StringBuilder();
            
            foreach (var dep in dependencies)
            {
                sb.AppendLine($"{dep.Key}|{dep.Value}");
            }
            
            File.WriteAllText(dependencyList, sb.ToString());
            Console.WriteLine($"✅ Dependencies list created: {dependencies.Count} items");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error initializing dependencies: {ex.Message}");
        }
    }

    private async Task GetDependenciesAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== Get Dependencies Request ===");
            
            var dependencyList = Path.Combine(_dependenciesPath, "dependencies.txt");
            
            if (!File.Exists(dependencyList))
            {
                await SendErrorAsync(client, request.Id, 404, "Dependencies not found");
                return;
            }

            var content = await File.ReadAllTextAsync(dependencyList);
            var result = new BinaryValue 
            { 
                IsNull = false, 
                One = ByteString.CopyFromUtf8(content)
            };
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("✅ Dependencies list sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetDependencies: {ex.Message}");
            await SendErrorAsync(client, request.Id, 500, "Internal server error");
        }
    }

    private async Task GetScriptsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== Get Scripts Request ===");
            
            if (request.Params.Count == 0)
            {
                await SendErrorAsync(client, request.Id, 400, "Script name required");
                return;
            }

            var scriptName = request.Params[0].One.ToStringUtf8();
            Console.WriteLine($"Requested script: {scriptName}");

            // Генерируем упрощенные Unity скрипты без зависимостей
            var scriptContent = GenerateUnityScript(scriptName);
            
            if (string.IsNullOrEmpty(scriptContent))
            {
                await SendErrorAsync(client, request.Id, 404, "Script not found");
                return;
            }

            var result = new BinaryValue 
            { 
                IsNull = false, 
                One = ByteString.CopyFromUtf8(scriptContent)
            };
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"✅ Script sent: {scriptName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetScripts: {ex.Message}");
            await SendErrorAsync(client, request.Id, 500, "Internal server error");
        }
    }

    private string GenerateUnityScript(string scriptName)
    {
        return scriptName.ToLower() switch
        {
            "networkmanager" => GenerateNetworkManager(),
            "gamemanager" => GenerateGameManager(),
            "authmanager" => GenerateAuthManager(),
            "uimanager" => GenerateUIManager(),
            _ => string.Empty
        };
    }

    private string GenerateNetworkManager()
    {
        return @"using System;
using System.Collections;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace MyGame.Network
{
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }
        
        [Header(""Server Settings"")]
        public string serverIP = ""127.0.0.1"";
        public int serverPort = 2222;
        
        private TcpClient tcpClient;
        private NetworkStream stream;
        private bool isConnected = false;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        public bool ConnectToServer()
        {
            try
            {
                tcpClient = new TcpClient();
                tcpClient.Connect(serverIP, serverPort);
                stream = tcpClient.GetStream();
                isConnected = true;
                Debug.Log(""Connected to server"");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($""Connection failed: {ex.Message}"");
                return false;
            }
        }
        
        public bool AuthenticateWithToken(string token)
        {
            if (!isConnected) return false;
            
            try
            {
                var message = $""TOKEN_AUTH:{token}"";
                var data = Encoding.UTF8.GetBytes(message);
                stream.Write(data, 0, data.Length);
                
                // Read response
                var buffer = new byte[1024];
                var bytesRead = stream.Read(buffer, 0, buffer.Length);
                var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                
                return response.StartsWith(""SUCCESS"");
            }
            catch (Exception ex)
            {
                Debug.LogError($""Auth failed: {ex.Message}"");
                return false;
            }
        }
        
        public void Disconnect()
        {
            if (isConnected)
            {
                stream?.Close();
                tcpClient?.Close();
                isConnected = false;
                Debug.Log(""Disconnected from server"");
            }
        }
        
        private void OnDestroy()
        {
            Disconnect();
        }
    }
}";
    }

    private string GenerateGameManager()
    {
        return @"using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGame.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        
        [Header(""Game Settings"")]
        public string gameVersion = ""0.10.22"";
        public string gameId = ""mygame"";
        
        public bool IsAuthenticated { get; private set; }
        public string PlayerToken { get; private set; }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        private void Start()
        {
            InitializeGame();
        }
        
        private void InitializeGame()
        {
            Debug.Log($""Game Manager initialized - Version: {gameVersion}"");
        }
        
        public void SetAuthenticated(string token)
        {
            IsAuthenticated = true;
            PlayerToken = token;
            Debug.Log(""Player authenticated successfully"");
        }
        
        public void LoadMainScene()
        {
            if (IsAuthenticated)
            {
                SceneManager.LoadScene(""Main"");
            }
            else
            {
                Debug.LogError(""Cannot load main scene - not authenticated"");
            }
        }
        
        public void Logout()
        {
            IsAuthenticated = false;
            PlayerToken = null;
            SceneManager.LoadScene(""Login"");
        }
    }
}";
    }

    private string GenerateAuthManager()
    {
        return @"using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame.Auth
{
    public class AuthManager : MonoBehaviour
    {
        [Header(""UI Elements"")]
        public GameObject loginPanel;
        public GameObject tokenPanel;
        public InputField tokenInput;
        public Button tokenButton;
        public Button loginButton;
        public Text statusText;
        
        private void Start()
        {
            SetupUI();
        }
        
        private void SetupUI()
        {
            if (tokenButton != null)
                tokenButton.onClick.AddListener(OnTokenLogin);
                
            if (loginButton != null)
                loginButton.onClick.AddListener(ShowTokenPanel);
        }
        
        private void ShowTokenPanel()
        {
            if (loginPanel != null) loginPanel.SetActive(false);
            if (tokenPanel != null) tokenPanel.SetActive(true);
        }
        
        private void OnTokenLogin()
        {
            var token = tokenInput?.text?.Trim();
            
            if (string.IsNullOrEmpty(token))
            {
                ShowStatus(""Введите токен"");
                return;
            }
            
            StartCoroutine(AuthenticateWithToken(token));
        }
        
        private IEnumerator AuthenticateWithToken(string token)
        {
            ShowStatus(""Подключение к серверу..."");
            
            if (!Network.NetworkManager.Instance.ConnectToServer())
            {
                ShowStatus(""Ошибка подключения к серверу"");
                yield break;
            }
            
            ShowStatus(""Авторизация..."");
            
            if (Network.NetworkManager.Instance.AuthenticateWithToken(token))
            {
                ShowStatus(""Успешно!"");
                Core.GameManager.Instance.SetAuthenticated(token);
                
                yield return new WaitForSeconds(1f);
                Core.GameManager.Instance.LoadMainScene();
            }
            else
            {
                ShowStatus(""Неверный или истекший токен"");
            }
        }
        
        private void ShowStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
            Debug.Log($""Auth Status: {message}"");
        }
    }
}";
    }

    private string GenerateUIManager()
    {
        return @"using UnityEngine;
using UnityEngine.UI;

namespace MyGame.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }
        
        [Header(""Panels"")]
        public GameObject splashPanel;
        public GameObject authPanel;
        public GameObject mainPanel;
        
        [Header(""Splash UI"")]
        public Text loadingText;
        public Slider progressBar;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        private void Start()
        {
            ShowSplash();
        }
        
        public void ShowSplash()
        {
            SetActivePanel(splashPanel);
            SetLoadingText(""Загрузка..."");
            SetProgress(0f);
        }
        
        public void ShowAuth()
        {
            SetActivePanel(authPanel);
        }
        
        public void ShowMain()
        {
            SetActivePanel(mainPanel);
        }
        
        public void SetLoadingText(string text)
        {
            if (loadingText != null)
                loadingText.text = text;
        }
        
        public void SetProgress(float progress)
        {
            if (progressBar != null)
                progressBar.value = progress;
        }
        
        private void SetActivePanel(GameObject panel)
        {
            if (splashPanel != null) splashPanel.SetActive(false);
            if (authPanel != null) authPanel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(false);
            
            if (panel != null) panel.SetActive(true);
        }
        
        public void ShowDialog(string title, string message)
        {
            Debug.Log($""Dialog - {title}: {message}"");
            // Простая реализация через Debug.Log
            // В реальном проекте здесь будет UI диалог
        }
    }
}";
    }

    private async Task SendErrorAsync(TcpClient client, string guid, int code, string message)
    {
        await _handler.WriteProtoResponseAsync(client, guid, null,
            new RpcException { Id = guid, Code = code, Property = null });
    }
}