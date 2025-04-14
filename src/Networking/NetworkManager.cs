using Godot;
using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.Platform;
// Explicitly use specific namespaces where needed to avoid ambiguity
using Epic.OnlineServices.Auth;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.Logging;
using AddNotifyLoginStatusChangedOptions = Epic.OnlineServices.Connect.AddNotifyLoginStatusChangedOptions;
using PlatformOptions = Epic.OnlineServices.Platform.Options; // Alias Platform.Options
using PlatformInitializeOptions = Epic.OnlineServices.Platform.InitializeOptions; // Alias Platform.InitializeOptions
using AuthCredentials = Epic.OnlineServices.Auth.Credentials; // Alias Auth.Credentials
using AuthLoginOptions = Epic.OnlineServices.Auth.LoginOptions; // Alias Auth.LoginOptions
using ConnectCredentials = Epic.OnlineServices.Connect.Credentials; // Alias Connect.Credentials
using ConnectLoginOptions = Epic.OnlineServices.Connect.LoginOptions;
using CopyIdTokenOptions = Epic.OnlineServices.Auth.CopyIdTokenOptions;
using IdToken = Epic.OnlineServices.Auth.IdToken; // Alias Connect.LoginOptions


// Add or modify your enum
public enum EOSLoginType
{
    Developer, // Auth -> Connect
    AccountPortal, // Auth -> Connect
    DeviceID // Direct Connect
}


public partial class NetworkManager : Node
{
    // --- Configuration ---
    private string _productId = "";
    private string _sandboxId = "";
    private string _deploymentId = "";
    private string _clientId = "";
    private string _clientSecret = "";

    [Export] public string ProductName = "MyGodotGame";
    [Export] public string ProductVersion = "1.0";
    [Export] public EOSLoginType LoginType = EOSLoginType.Developer;
    [Export] public string DevAuthURL = "127.0.0.1:9876";
    [Export] public string DeveloperLoginUserName = "DevUser1";


    // --- EOS SDK State ---
    // CS8618 Fix: Mark as nullable or ensure initialization in constructor/_Ready
    public static PlatformInterface? EOSPlatformInterface { get; private set; }
    // CA1805 Fix: Remove explicit null initialization
    private AuthInterface? _authInterface;
    private ConnectInterface? _connectInterface;

    // --- User State ---
    // CA1805 Fix: Remove explicit null initialization
    private EpicAccountId? _localEpicAccountId;
    private ProductUserId? _localProductUserId;
    private ContinuanceToken? _continuanceToken; // This is a nullable struct from SDK

    // --- Timers & Notifications ---
    private const double PlatformTickInterval = 0.1f;
    // CA1805 Fix: Remove explicit 0 initialization
    private double _platformTickTimer;
    private ulong _authExpirationNotifyId;
    private ulong _loginStatusChangedNotifyId;

    // --- Config Loading ---
    private const string EnvFilePath = "res://.env";
    private static readonly string[] separator = new[] { "\r\n", "\r", "\n" };

    // --- Signals ---
    [Signal] public delegate void LoginSuccessEventHandler(string puidString);
    [Signal] public delegate void LoginFailureEventHandler(long resultCode, string message);


    public override void _Ready()
    {
        if (!LoadConfiguration())
        {
            GD.PushError("EOSManager: Failed to load EOS configuration. Check .env file or environment variables.");
            SetProcess(false);
            return;
        }

        InitializeEOS();

        if (EOSPlatformInterface != null)
        {
            _authInterface = EOSPlatformInterface.GetAuthInterface();
            _connectInterface = EOSPlatformInterface.GetConnectInterface();

            if (_authInterface == null || _connectInterface == null)
            {
                GD.PushError("EOSManager: Failed to get Auth or Connect interface.");
                CleanupEOS(); // Cleanup if interfaces are bad
                SetProcess(false); // Stop processing
                return;
            }

            StartLoginProcess();
        }
        else
        {
             GD.PushError("EOSManager: Platform Interface is null after initialization.");
             SetProcess(false); // Stop processing if platform failed
        }
    }

    public override void _Process(double delta)
    {
        // Check for null before ticking
        if (EOSPlatformInterface != null)
        {
            _platformTickTimer += delta;
            if (_platformTickTimer >= PlatformTickInterval)
            {
                _platformTickTimer = 0;
                EOSPlatformInterface.Tick();
            }
        }
        else
        {
             // If platform becomes null unexpectedly, stop processing
             // This shouldn't happen if initialization guards are correct
             if(IsProcessing())
             {
                GD.PushWarning("EOSManager: Platform Interface is null during _Process. Stopping processing.");
                SetProcess(false);
             }
        }
    }

    public override void _ExitTree()
    {
        CleanupEOS();
    }

    // --- Initialization & Cleanup ---

    private void InitializeEOS()
    {
        var initializeOptions = new PlatformInitializeOptions()
        {
            ProductName = this.ProductName,
            ProductVersion = this.ProductVersion,
            // CS8625 Warning Fix: Explicitly provide null delegates if needed, or rely on defaults
            //AllocateMemoryFunction = null,
            //ReallocateMemoryFunction = null,
           // ReleaseMemoryFunction = null,
        };

        Result initializeResult = PlatformInterface.Initialize(ref initializeOptions);
        if (initializeResult != Result.Success)
        {
            GD.PushError($"EOSManager: Failed to initialize EOS SDK. Result: {initializeResult}");
            return;
        }
        GD.Print("EOSManager: SDK Initialized.");

        LoggingInterface.SetLogLevel(LogCategory.AllCategories, LogLevel.VeryVerbose);
        LoggingInterface.SetCallback((ref LogMessage logMessage) => GD.Print($"[EOS Log] {logMessage.Category}: {logMessage.Message}"));

        var options = new PlatformOptions()
        {
            ProductId = _productId,
            SandboxId = _sandboxId,
            DeploymentId = _deploymentId,
            ClientCredentials = new ClientCredentials() { ClientId = _clientId, ClientSecret = _clientSecret },
            IsServer = false,
            Flags = PlatformFlags.None
        };

        EOSPlatformInterface = PlatformInterface.Create(ref options);
        if (EOSPlatformInterface == null)
        {
            GD.PushError("EOSManager: Failed to create EOS platform interface. Check ProductId, SandboxId, DeploymentId, Client Credentials.");
            // Don't shutdown here if Create fails, as Initialize might still need shutdown later
            return;
        }
        GD.Print("EOSManager: Platform Interface Created.");
    }

    private void CleanupEOS()
    {
        GD.Print("EOSManager: Cleaning up...");

        // Unregister notifications FIRST
        if (_connectInterface != null)
        {
            if (_authExpirationNotifyId != 0)
            {
                _connectInterface.RemoveNotifyAuthExpiration(_authExpirationNotifyId);
                _authExpirationNotifyId = 0;
            }
            if (_loginStatusChangedNotifyId != 0)
            {
                _connectInterface.RemoveNotifyLoginStatusChanged(_loginStatusChangedNotifyId);
                _loginStatusChangedNotifyId = 0;
            }
        }

        // Release Platform Interface
        if (EOSPlatformInterface != null)
        {
            EOSPlatformInterface.Release();
            EOSPlatformInterface = null; // Set to null after release
            GD.Print("EOSManager: Platform Interface Released.");
        }

        // Shutdown SDK
        Result shutdownResult = PlatformInterface.Shutdown();
        if (shutdownResult != Result.Success && shutdownResult != Result.NotConfigured) // Ignore NotConfigured if already shutdown/never started
        {
            GD.PushWarning($"EOSManager: EOS SDK Shutdown returned: {shutdownResult}");
        } else {
            GD.Print("EOSManager: SDK Shutdown.");
        }

         // Clear local handles
         _authInterface = null;
         _connectInterface = null;
         _localEpicAccountId = null;
         _localProductUserId = null;
         _continuanceToken = null; // Clear nullable struct
    }


    // --- Login Flow ---

    private void StartLoginProcess()
    {
        GD.Print($"EOSManager: Starting login process with type: {LoginType}");
        switch (LoginType)
        {
            case EOSLoginType.Developer:
            case EOSLoginType.AccountPortal:
                AuthenticateEpicAccount();
                break;
            case EOSLoginType.DeviceID:
                ConnectLoginWithDeviceID();
                break;
            default:
                GD.PushError($"EOSManager: Unsupported LoginType: {LoginType}");
                EmitSignal(SignalName.LoginFailure, (long)Result.InvalidParameters, $"Unsupported LoginType: {LoginType}");
                break;
        }
    }

    private void AuthenticateEpicAccount()
    {
        if (_authInterface == null)
        {
            GD.PushError("EOSManager: Auth Interface is null. Cannot authenticate Epic account.");
            EmitSignal(SignalName.LoginFailure, (long)Result.InvalidState, "Auth Interface not available.");
            return;
        }

        AuthCredentials credentials = new AuthCredentials();
        // CS0219 Fix: Removed unused 'credentialType' variable

        if (LoginType == EOSLoginType.Developer)
        {
            if (string.IsNullOrEmpty(DeveloperLoginUserName) || string.IsNullOrEmpty(DevAuthURL)) {
                GD.PushError("EOSManager: DeveloperLoginUsername or DevAuthToolUrl is not set for Developer Auth!");
                EmitSignal(SignalName.LoginFailure, (long)Result.InvalidParameters, "Missing Developer Auth credentials.");
                return;
            }
            credentials.Type = LoginCredentialType.Developer;
            credentials.Id = DevAuthURL;
            credentials.Token = DeveloperLoginUserName;
             GD.Print($"EOSManager: Using Developer Credentials: URL='{credentials.Id}', User='{credentials.Token}'");
        }
        else if (LoginType == EOSLoginType.AccountPortal)
        {
            credentials.Type = LoginCredentialType.AccountPortal;
            // CS8625 Fix: Explicitly set null for nullable fields if needed by API, C# handles reference types
            credentials.Id = null;
            credentials.Token = null;
            GD.Print("EOSManager: Using Account Portal Credentials.");
        }
        else {
             GD.PushError($"EOSManager: Invalid LoginType for AuthenticateEpicAccount: {LoginType}");
             EmitSignal(SignalName.LoginFailure, (long)Result.InvalidParameters, "Invalid LoginType for Epic Account Auth.");
             return;
        }

        var loginOptions = new AuthLoginOptions()
        {
            Credentials = credentials,
            ScopeFlags = AuthScopeFlags.BasicProfile | AuthScopeFlags.Presence | AuthScopeFlags.FriendsList
        };

        GD.Print("EOSManager: Calling AuthInterface.Login...");
        // CS8625 Fix: ClientData (second arg) can typically be null
        _authInterface.Login(ref loginOptions, null, OnEpicAccountLoginCallback);
    }

    private void OnEpicAccountLoginCallback(ref Epic.OnlineServices.Auth.LoginCallbackInfo data)
    {
        if (data.ResultCode == Result.Success)
        {
            _localEpicAccountId = data.LocalUserId;
            GD.Print($"EOSManager: AuthInterface Login SUCCESS! EpicAccountID: {_localEpicAccountId}");
            FetchEpicIdTokenAndConnect();
        }
        else
        {
            GD.PushError($"EOSManager: AuthInterface Login FAILED! Result: {data.ResultCode}");
             if (data.ResultCode == Result.InvalidCredentials && LoginType == EOSLoginType.Developer) {
                  GD.PushError("EOSManager: Developer Auth Failed: Check if DevAuthTool is running, configured with correct Client ID/Secret, and using the correct URL/Port in Godot.");
            }
             EmitSignal(SignalName.LoginFailure, (long)data.ResultCode, $"Epic Account Auth Failed: {data.ResultCode}");
        }
    }

    private void FetchEpicIdTokenAndConnect()
    {
        if (_authInterface == null || _localEpicAccountId == null || !_localEpicAccountId.IsValid())
        {
            GD.PushError("EOSManager: Cannot fetch ID token - Auth interface or Epic Account ID is invalid.");
            EmitSignal(SignalName.LoginFailure, (long)Result.InvalidState, "Cannot fetch Epic ID token.");
            return;
        }

        // CS0117 Fix: Use LocalUserId
        var copyIdTokenOptions = new CopyIdTokenOptions() { AccountId = _localEpicAccountId };
        GD.Print("EOSManager: Calling AuthInterface.CopyIdToken (synchronous)...");

        // CS1501 & CS1061 Fixes: Assume synchronous pattern with 'out' parameter
        IdToken? outIdToken; // Use nullable IdToken struct
        // Pass options by ref, declare out parameter
        Result copyResult = _authInterface.CopyIdToken(ref copyIdTokenOptions, out outIdToken);

        if (copyResult == Result.Success)
        {
            // Check the 'out' parameter (nullable struct pattern)
            if (outIdToken.HasValue)
            {
                GD.Print("EOSManager: CopyIdToken SUCCESS.");
                string idTokenJwt = outIdToken.Value.JsonWebToken;

                // IMPORTANT: Release the copied token structure when done.
                //outIdToken.Value.Release(); // Release the IdToken handle

                ConnectLoginWithEpicIdToken(idTokenJwt);
            }
            else
            {
                GD.PushError($"EOSManager: CopyIdToken SUCCESS but outIdToken is invalid or null!");
                EmitSignal(SignalName.LoginFailure, (long)Result.UnexpectedError, $"CopyIdToken Success but outIdToken is invalid.");
            }
        }
        else
        {
            GD.PushError($"EOSManager: CopyIdToken FAILED! Result: {copyResult}");
            EmitSignal(SignalName.LoginFailure, (long)copyResult, $"Failed to copy Epic ID Token: {copyResult}");
        }
    }
    // Removed the non-existent OnCopyIdTokenComplete callback

    private void ConnectLoginWithEpicIdToken(string idTokenJwt)
    {
        if (_connectInterface == null) {
            GD.PushError("EOSManager: Connect Interface is null.");
            EmitSignal(SignalName.LoginFailure, (long)Result.InvalidState, "Connect Interface not available.");
            return;
        }

        var connectCredentials = new ConnectCredentials()
        {
            Type = ExternalCredentialType.EpicIdToken,
            Token = idTokenJwt
        };

        var connectLoginOptions = new ConnectLoginOptions()
        {
             Credentials = connectCredentials,
             // CS8625 Fix: UserLoginInfo can be null here
             UserLoginInfo = null
        };

        GD.Print("EOSManager: Calling ConnectInterface.Login with Epic ID Token...");
        // CS8625 Fix: ClientData can be null
        _connectInterface.Login(ref connectLoginOptions, null, OnConnectLoginCallback);
    }

    private void ConnectLoginWithDeviceID()
    {
         if (_connectInterface == null) {
            GD.PushError("EOSManager: Connect Interface is null.");
            EmitSignal(SignalName.LoginFailure, (long)Result.InvalidState, "Connect Interface not available.");
            return;
        }

        var connectCredentials = new ConnectCredentials()
        {
             Type = ExternalCredentialType.DeviceidAccessToken,
             // CS8625 Fix: Token is intentionally null for DeviceID Access Token
             Token = null
        };

        var userLoginInfo = new UserLoginInfo() { DisplayName = $"Player_{Guid.NewGuid().ToString().Substring(0, 6)}" };

        var connectLoginOptions = new ConnectLoginOptions()
        {
             Credentials = connectCredentials,
             UserLoginInfo = userLoginInfo
        };

        GD.Print("EOSManager: Calling ConnectInterface.Login with Device ID Access Token...");
        // CS8625 Fix: ClientData can be null
        _connectInterface.Login(ref connectLoginOptions, null, OnConnectLoginCallback);
    }

    private void OnConnectLoginCallback(ref Epic.OnlineServices.Connect.LoginCallbackInfo data)
    {
        if (data.ResultCode == Result.Success)
        {
            _localProductUserId = data.LocalUserId;
            _continuanceToken = null; // Clear after successful login
            GD.Print($"EOSManager: ConnectInterface Login SUCCESS! ProductUserID: {_localProductUserId}");
            RegisterConnectNotifications();
            EmitSignal(SignalName.LoginSuccess, _localProductUserId.ToString());
        }
        else if (data.ResultCode == Result.InvalidUser)
        {
            GD.Print("EOSManager: ConnectInterface Login returned InvalidUser. Need to create or link account.");
            // CS1061 Fix: Check if ContinuanceToken is null, not IsValid()
            if(data.ContinuanceToken == null)
            {
                // This is unexpected - InvalidUser should provide a token
                GD.PushError("EOSManager: InvalidUser result but ContinuanceToken is null!");
                EmitSignal(SignalName.LoginFailure, (long)Result.InvalidState, "InvalidUser result but ContinuanceToken is null!");
                return;
            }
            // Assign the nullable struct
            _continuanceToken = data.ContinuanceToken;

            GD.Print("EOSManager: Attempting automatic user creation...");
            CreateConnectUser();
        }
        else if (data.ResultCode == Result.DuplicateNotAllowed && LoginType == EOSLoginType.DeviceID)
        {
             GD.PushWarning("EOSManager: Connect Login failed with DuplicateNotAllowed for Device ID. Retrying login...");
             ConnectLoginWithDeviceID();
        }
        else
        {
            GD.PushError($"EOSManager: ConnectInterface Login FAILED! Result: {data.ResultCode}");
             _continuanceToken = null;
             EmitSignal(SignalName.LoginFailure, (long)data.ResultCode, $"Connect Login Failed: {data.ResultCode}");
        }
    }

    private void CreateConnectUser()
    {
        // CS1061 Fix: Check if ContinuanceToken is null
        if (_connectInterface == null || _continuanceToken == null) // Simple null check for nullable struct
        {
            GD.PushError("EOSManager: Cannot create connect user - Interface or ContinuanceToken invalid/null.");
             EmitSignal(SignalName.LoginFailure, (long)Result.InvalidState, "Cannot create connect user.");
            return;
        }

        // ContinuanceToken is nullable, pass it directly
        var createUserOptions = new CreateUserOptions() { ContinuanceToken = _continuanceToken };

        GD.Print("EOSManager: Calling ConnectInterface.CreateUser...");
        // CS8625 Fix: ClientData can be null
        _connectInterface.CreateUser(ref createUserOptions, null, OnCreateUserCallback);
    }

    private void OnCreateUserCallback(ref Epic.OnlineServices.Connect.CreateUserCallbackInfo data)
    {
        if (data.ResultCode == Result.Success)
        {
            _localProductUserId = data.LocalUserId;
            _continuanceToken = null; // Consumed token
            GD.Print($"EOSManager: ConnectInterface CreateUser SUCCESS! ProductUserID: {_localProductUserId}");
            RegisterConnectNotifications();
            EmitSignal(SignalName.LoginSuccess, _localProductUserId.ToString());
        }
        else
        {
            GD.PushError($"EOSManager: ConnectInterface CreateUser FAILED! Result: {data.ResultCode}");
            _continuanceToken = null;
            EmitSignal(SignalName.LoginFailure, (long)data.ResultCode, $"Connect User Creation Failed: {data.ResultCode}");
        }
    }

    private void CreateDeviceID()
    {
         if (_connectInterface == null) {
            GD.PushError("EOSManager: Connect Interface is null.");
            EmitSignal(SignalName.LoginFailure, (long)Result.InvalidState, "Connect Interface not available.");
            return;
        }

        string deviceModel = OS.GetName() + " " + OS.GetModelName();
        var createDeviceOptions = new CreateDeviceIdOptions() { DeviceModel = deviceModel };

        GD.Print("EOSManager: Calling ConnectInterface.CreateDeviceId...");
        // CS8625 Fix: ClientData can be null
        _connectInterface.CreateDeviceId(ref createDeviceOptions, null, OnCreateDeviceIdCallback);
    }

     private void OnCreateDeviceIdCallback(ref CreateDeviceIdCallbackInfo data)
    {
        if (data.ResultCode == Result.Success)
        {
            GD.Print("EOSManager: CreateDeviceId SUCCESS.");
            GD.Print("EOSManager: Retrying Connect Login with Device ID...");
            ConnectLoginWithDeviceID();
        }
        else if (data.ResultCode == Result.DuplicateNotAllowed)
        {
             GD.Print("EOSManager: CreateDeviceId returned DuplicateNotAllowed (Device ID likely exists). Proceeding with login attempt...");
             ConnectLoginWithDeviceID();
        }
        else
        {
             GD.PushError($"EOSManager: CreateDeviceId FAILED! Result: {data.ResultCode}");
             EmitSignal(SignalName.LoginFailure, (long)data.ResultCode, $"Device ID Creation Failed: {data.ResultCode}");
        }
    }

    private void RegisterConnectNotifications()
    {
        if (_connectInterface == null || _localProductUserId == null || !_localProductUserId.IsValid()) // Need .Value for nullable struct
        {
            GD.PushWarning("EOSManager: Cannot register notifications - Connect interface or PUID invalid.");
            return;
        }

        if (_authExpirationNotifyId == 0)
        {
            var addNotifyAuthExpirationOptions = new AddNotifyAuthExpirationOptions();
            // CS8625 Fix: ClientData can be null
            _authExpirationNotifyId = _connectInterface.AddNotifyAuthExpiration(ref addNotifyAuthExpirationOptions, null, OnAuthExpirationCallback);
            if (_authExpirationNotifyId == 0) GD.PushWarning("EOSManager: Failed to add Auth Expiration notification handler.");
            else GD.Print("EOSManager: Registered for Auth Expiration notifications.");
        }

        if (_loginStatusChangedNotifyId == 0)
        {
            // CS0104 Fix: Qualify ambiguous type
            var addNotifyLoginStatusChangedOptions = new AddNotifyLoginStatusChangedOptions();
            // CS8625 Fix: ClientData can be null
             _loginStatusChangedNotifyId = _connectInterface.AddNotifyLoginStatusChanged(ref addNotifyLoginStatusChangedOptions, null, OnLoginStatusChangedCallback);
             if (_loginStatusChangedNotifyId == 0) GD.PushWarning("EOSManager: Failed to add Login Status Changed notification handler.");
             else GD.Print("EOSManager: Registered for Login Status Changed notifications.");
        }
    }

    private void OnAuthExpirationCallback(ref AuthExpirationCallbackInfo data)
    {
        GD.Print($"EOSManager: Connect Auth Token for PUID {data.LocalUserId} is expiring! Need to re-login.");
        GD.Print("EOSManager: Attempting to refresh login due to token expiration...");
        // Potentially add a delay or backoff mechanism here to avoid spamming login on repeated failures
        StartLoginProcess();
    }

    private void OnLoginStatusChangedCallback(ref Epic.OnlineServices.Connect.LoginStatusChangedCallbackInfo data)
    {
        GD.Print($"EOSManager: Connect Login Status Changed for PUID {data.LocalUserId}. Previous: {data.PreviousStatus}, Current: {data.CurrentStatus}");

        if (data.CurrentStatus == LoginStatus.NotLoggedIn)
        {
            GD.PushWarning("EOSManager: User is now logged out according to Connect Interface status change!");
            _localProductUserId = null; // Mark as logged out
            // CS0117 Fix: Removed non-existent Result code
            // Use a generic or existing relevant code like InvalidAuth or just a specific message
            EmitSignal(SignalName.LoginFailure, (long)Result.InvalidAuth, "User logged out via status change.");
        }
    }

    public bool IsProductLoggedIn()
    {
        // Check nullable struct pattern
        return  _localProductUserId.IsValid();
    }

     public ProductUserId? GetProductUserId() // Return nullable PUID
     {
         if(IsProductLoggedIn()) {
             return _localProductUserId;
         }
         return null;
     }

     // --- Configuration Loading (Keep your existing robust loading) ---
     private bool LoadConfiguration()
    {
        // (Your existing LoadConfiguration code - verified and looks correct)
        var configValues = new Dictionary<string, string>();

        if (FileAccess.FileExists(EnvFilePath))
        {
            GD.Print($"EOSManager: Loading configuration from {EnvFilePath}");
            using var file = FileAccess.Open(EnvFilePath, FileAccess.ModeFlags.Read);
            if (file == null) {
                Error fileError = FileAccess.GetOpenError();
                GD.PushError($"EOSManager: Failed to open .env file '{EnvFilePath}'. Error: {fileError}");
            }
            else {
                try {
                    string content = file.GetAsText();
                    string[] lines = content.Split(separator, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string line in lines) {
                        string trimmedLine = line.Trim();
                        if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#')) continue;

                        int equalsIndex = trimmedLine.IndexOf('=');
                        if (equalsIndex > 0) {
                            string key = trimmedLine.Substring(0, equalsIndex).Trim();
                            string value = trimmedLine.Substring(equalsIndex + 1).Trim();
                            if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\''))) {
                                value = value.Substring(1, value.Length - 2);
                            }
                            configValues[key] = value;
                        } else {
                             GD.PushWarning($"EOSManager: Malformed line in .env file: {line}");
                        }
                    }
                } catch (Exception ex) {
                    GD.PushError($"EOSManager: Error parsing .env file: {ex.Message}");
                }
            }
        } else {
             GD.Print($"EOSManager: .env file not found at {EnvFilePath}. Will check environment variables.");
        }

        string GetSetting(string key) {
            string envVar = OS.GetEnvironment(key);
            if (!string.IsNullOrEmpty(envVar)) return envVar;
            return configValues.GetValueOrDefault(key, "");
        }

        _productId = GetSetting("EOS_PRODUCT_ID");
        _sandboxId = GetSetting("EOS_SANDBOX_ID");
        _deploymentId = GetSetting("EOS_DEPLOYMENT_ID");
        _clientId = GetSetting("EOS_CLIENT_ID");
        _clientSecret = GetSetting("EOS_CLIENT_SECRET");

        if (string.IsNullOrEmpty(_productId) || string.IsNullOrEmpty(_sandboxId) || string.IsNullOrEmpty(_deploymentId) || string.IsNullOrEmpty(_clientId))
        {
            GD.PushWarning("EOSManager: One or more required EOS configuration values (ProductID, SandboxID, DeploymentID, ClientID) are missing.");
            if (string.IsNullOrEmpty(_clientSecret)) {
                GD.Print("EOSManager: EOS ClientSecret is also missing. Required for Dev Auth Tool and some other operations.");
            }
            return false;
        }

        GD.Print("EOSManager: Configuration loaded.");
        GD.Print($"EOSManager: ProductID='{_productId}', SandboxID='{_sandboxId}', DeploymentID='{_deploymentId}', ClientID='{_clientId}', ClientSecret='{(string.IsNullOrEmpty(_clientSecret) ? "MISSING" : "PRESENT")}'");
        return true;
    }
}
