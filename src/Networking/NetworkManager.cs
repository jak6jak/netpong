namespace NetworkedDodgeball.Networking;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Epic.OnlineServices.Platform;
using Epic.OnlineServices.UserInfo;
using Godot;
using Steamworks;

public partial class NetworkManager : Node {
  public static NetworkManager Instance { get; private set; }


  private string _productId = "";
  private string _sandboxId = "";
  private string _deploymentId = "";
  private string _clientId = "";
  private string _clientSecret = "";


  // --- Non-Sensitive Config (Can still be Exported if desired) ---
  [Export] public string ProductName = "MyGodotGame";
  [Export] public string ProductVersion = "1.0";
  [Export] public LoginCredentialType LoginCredentialType = LoginCredentialType.ExternalAuth;
  [Export] public string DevAuthURL = "127.0.0.1:9876";
  [Export] public string DeveloperLoginUserName = "DevUser1";
  [Export] public string SteamAppId = "3136980"; // Default Steam App ID, change this to your game's App ID


  public string LoginCredentialId;
  public string LoginCredentialToken;
  private ContinuanceToken _continuanceToken; // Store continuance token for account linking

  [Signal]
  public delegate void AuthenticationFinishedEventHandler(bool success, string localUserId, string errorMessage);
  [Signal]
  public delegate void AccountLinkingRequiredEventHandler(); // Signal when account linking is needed

  // --- EOS SDK State ---
  public static PlatformInterface EOSPlatformInterface { get; private set; }
  private const double PlatformTickInterval = 0.1f;
  private double PlatformTickTimer = 0f;
  private const string EnvFilePath = "res://.env";
  private static readonly string[] separator = new[] { "\r\n", "\r", "\n" };

  public override void _Ready() {
    if (Instance == null) {
      Instance = this;
    }
    else {
      GD.PushWarning("EOS_ByteArray_ToString reported LimitExceeded but returned 0 for buffer size. Assuming empty string result.");

      QueueFree(); // Or handle appropriately

      return;
    }
    if (!LoadConfiguration()) {
      GD.PushError("EOSManager: Failed to load EOS configuration. Check .env file or environment variables.");

      return;
    }

    var initializeOptions = new Epic.OnlineServices.Platform.InitializeOptions() {
      ProductName = this.ProductName,
      ProductVersion = this.ProductVersion,
    };

    Epic.OnlineServices.Result initializeResult = Epic.OnlineServices.Platform.PlatformInterface.Initialize(ref initializeOptions);

    if (initializeResult != Epic.OnlineServices.Result.Success) {
      GD.PushError("Failed to initialize EOS configuration. " + initializeResult);
    }


    Epic.OnlineServices.Logging.LoggingInterface.SetLogLevel(Epic.OnlineServices.Logging.LogCategory.AllCategories,
      Epic.OnlineServices.Logging.LogLevel.VeryVerbose);

    Epic.OnlineServices.Logging.LoggingInterface.SetCallback((ref Epic.OnlineServices.Logging.LogMessage logMessage) =>
      GD.Print(logMessage.Message));


    var options = new Epic.OnlineServices.Platform.Options() {
      ProductId = _productId,
      SandboxId = _sandboxId,
      DeploymentId = _deploymentId,
      ClientCredentials =
        new Epic.OnlineServices.Platform.ClientCredentials() { ClientId = _clientId, ClientSecret = _clientSecret }
    };


    EOSPlatformInterface = Epic.OnlineServices.Platform.PlatformInterface.Create(ref options);

    if (EOSPlatformInterface == null) {
      GD.PushError("Failed to create EOS platform interface.");

      return;
    }
    Authenticate();
  }


  public override void _PhysicsProcess(double delta) {
    if (EOSPlatformInterface != null) {
      PlatformTickTimer += delta;

      if (PlatformTickTimer >= PlatformTickInterval) {
        PlatformTickTimer = 0;

        EOSPlatformInterface.Tick();
      }
    }
  }


  private async Task Authenticate() {
    GD.Print($"Attempting EOS Login with type: {LoginCredentialType}");

    Credentials credentials;

    if (LoginCredentialType == LoginCredentialType.Developer) {
      if (string.IsNullOrEmpty(DeveloperLoginUserName)) {
        GD.PushError("DeveloperLoginUsername is not set in the Inspector for Developer Auth!");

        return;
      }

      if (string.IsNullOrEmpty(DevAuthURL)) {
        GD.PushError("DevAuthToolUrl is not set in the Inspector for Developer Auth!");

        return;
      }

      if (string.IsNullOrEmpty(_clientSecret)) {
        GD.PushWarning("Client Secret is missing in configuration. It IS REQUIRED for Developer Auth Tool.");

        // Optionally return here if you know it will fail
      }


      credentials = new Credentials() {
        Type = LoginCredentialType.Developer,
        Id = DevAuthURL,
        Token = DeveloperLoginUserName,
      };

      GD.Print($"Using Developer Credentials: ID='{credentials.Id}', Token (URL)='{credentials.Token}'");
    }

    else if (LoginCredentialType == LoginCredentialType.AccountPortal) {
      // AccountPortal uses external browser, Id and Token are null initially
      credentials = new Credentials() { Type = LoginCredentialType.AccountPortal, Id = null, Token = null };
    }
    else if (LoginCredentialType == LoginCredentialType.ExternalAuth) {
      // Steam login
      string steamSessionTicket = await GetSteamSessionTicket();
      if (string.IsNullOrEmpty(steamSessionTicket)) {
        GD.PushError("Failed to get Steam Session Ticket. Make sure Steam is running and user is logged in.");
        EmitSignal(SignalName.AuthenticationFinished, false, "", "Steam not available");
        return;
      }

      credentials = new Credentials() {
        Type = LoginCredentialType.ExternalAuth,
        Id = "",
        Token = steamSessionTicket,
        ExternalType = ExternalCredentialType.SteamSessionTicket
      };

      GD.Print("Using Steam Session Ticket for authentication");
    }

    else {
      GD.PushError($"Unsupported LoginCredentialType for this example: {LoginCredentialType}");

      return;
    }


    var loginOptions = new LoginOptions() {
      Credentials = credentials,

      // Adjust scopes as needed for your game

      ScopeFlags = AuthScopeFlags.BasicProfile | AuthScopeFlags.Presence | AuthScopeFlags.FriendsList
    };


    // Ensure Auth Interface is valid before calling Login

    var authInterface = EOSPlatformInterface?.GetAuthInterface();

    if (authInterface == null) {
      GD.PushError("EOS Auth Interface is null. Cannot login.");

      return;
    }


    authInterface.Login(ref loginOptions, null, (ref LoginCallbackInfo callbackInfo) => {
      if (callbackInfo.ResultCode == Epic.OnlineServices.Result.Success) {
        GD.Print(
          $"Login successful! Local User ID: {callbackInfo.LocalUserId}, Display Name: {callbackInfo.LocalUserId.ToString()}");

        // DisplayName might not be immediately available

        // You are now logged in. Proceed with connecting to lobbies, sessions etc.

        // Store callbackInfo.LocalUserId - this is your primary identifier for this player

        CopyUserInfoOptions copyUserInfoOptions = new CopyUserInfoOptions();

        copyUserInfoOptions.TargetUserId = callbackInfo.LocalUserId;

        copyUserInfoOptions.LocalUserId = callbackInfo.LocalUserId;


        UserInfoData? userInfoData;

        EOSPlatformInterface.GetUserInfoInterface().CopyUserInfo(ref copyUserInfoOptions, out userInfoData);


        EmitSignal(SignalName.AuthenticationFinished, true, userInfoData.Value.DisplayName.ToString(), "");
      }
      else if (callbackInfo.ResultCode == Epic.OnlineServices.Result.InvalidUser) {
        // Account needs to be linked
        _continuanceToken = callbackInfo.ContinuanceToken;
        GD.Print("Steam account needs to be linked to Epic account");
        EmitSignal(SignalName.AccountLinkingRequired);
      }

      else if (Common.IsOperationComplete(callbackInfo.ResultCode)) {
        GD.PushError($"Login failed: {callbackInfo.ResultCode}");

        // Handle specific errors, e.g., InvalidUser, InvalidCredentials

        string errorMessage = $"Login Failed: ${callbackInfo.ResultCode}";


        if (callbackInfo.ResultCode == Epic.OnlineServices.Result.InvalidCredentials &&
            LoginCredentialType == LoginCredentialType.Developer) {
          errorMessage += " Dev Auth failed";

          GD.PushError(
            "Dev Auth Failed: Check if DevAuthTool is running, configured with correct Client ID/Secret, and using the correct URL/Port in Godot.");

          return;
        }

        EmitSignal(SignalName.AuthenticationFinished, false, "", errorMessage);
      }

      // Note: For AccountPortal, you might get other result codes during the external browser flow.
    });

    GD.Print("EOS Login request sent.");
  }

  private async Task<string> GetSteamSessionTicket() {
    try {
      SteamClient.Init(uint.Parse(SteamAppId), true);
    }
    catch (Exception e) {
      GD.PushError($"Failed to initialize SteamClient: {e.Message}");
      return "";
    }

    if (!SteamClient.IsValid) {
      GD.PushError("Steam is not running or not initialized");
      return "";
    }

    if (!SteamClient.IsLoggedOn) {
      GD.PushError("User is not logged into Steam");
      return "";
    }

    AuthTicket ticket = await SteamUser.GetAuthTicketForWebApiAsync("epiconlineservices");
    //if (ticket == null || ticket.Data == null || ticket.Data.Length == 0) {
    //  GD.PushError("Failed to get Steam session ticket");
    //  return "";
    // }
    string hexToken = Convert.ToHexString(ticket.Data);
    return hexToken;
  }

  public void LinkSteamAccount() {
    if (_continuanceToken == null) {
      GD.PushError("No continuance token available for account linking");
      return;
    }

    var linkAccountOptions = new LinkAccountOptions {
      ContinuanceToken = _continuanceToken,
      LinkAccountFlags = 0 // Use 0 for no flags as we're linking with external auth
    };

    var authInterface = EOSPlatformInterface?.GetAuthInterface();
    if (authInterface == null) {
      GD.PushError("EOS Auth Interface is null. Cannot link account.");
      return;
    }

    authInterface.LinkAccount(ref linkAccountOptions, null, (ref LinkAccountCallbackInfo callbackInfo) => {
      if (callbackInfo.ResultCode == Epic.OnlineServices.Result.Success) {
        GD.Print("Account linked successfully");
        // Retry authentication after successful linking
        Authenticate();
      }
      else {
        GD.PushError($"Account linking failed: {callbackInfo.ResultCode}");
        EmitSignal(SignalName.AuthenticationFinished, false, "", $"Account linking failed: {callbackInfo.ResultCode}");
      }
    });
  }

  private bool LoadConfiguration() {
    var configValues = new Dictionary<string, string>();


    // 1. Try loading from .env file using Godot's FileAccess

    if (FileAccess.FileExists(EnvFilePath)) {
      GD.Print($"EOSManager: Loading configuration from {EnvFilePath}");

      // Use 'using' to ensure the file is closed automatically

      using var file = FileAccess.Open(EnvFilePath, FileAccess.ModeFlags.Read);

      if (file == null) {
        Error fileError = FileAccess.GetOpenError();

        GD.PushError($"EOSManager: Failed to open .env file '{EnvFilePath}'. Error: {fileError}");
      }

      else {
        try {
          string content = file.GetAsText();

          // Split the content into lines, handling different line endings

          string[] lines = content.Split(separator, StringSplitOptions.None);


          foreach (string line in lines) {
            string trimmedLine = line.Trim();

            if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#')) // Skip empty lines and comments

              continue;


            int equalsIndex = trimmedLine.IndexOf('=');

            if (equalsIndex > 0) {
              string key = trimmedLine.Substring(0, equalsIndex).Trim();

              string value = trimmedLine.Substring(equalsIndex + 1).Trim();

              if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\''))) {
                value = value.Substring(1, value.Length - 2);
              }


              configValues[key] = value;
            }

            else {
              GD.PushWarning($"EOSManager: Malformed line in .env file: {line}");
            }
          }
        }

        catch (Exception ex) // Catch potential exceptions during parsing

        {
          GD.PushError($"EOSManager: Error parsing .env file: {ex.Message}");

          // Continue to check environment variables
        }

        // 'file.Close()' is called automatically by 'using'
      }
    }

    else {
      GD.Print($"EOSManager: .env file not found at {EnvFilePath}. Will check environment variables.");
    }

    // Load DeveloperUserName from command line arguments if not set

    var args = OS.GetCmdlineUserArgs();

    for (int i = 0; i < args.Length; i++) {
      if (args[i].StartsWith("--username=")) {
        DeveloperLoginUserName = args[i].Substring("--username=".Length);

        GD.Print($"Overriding DeveloperLoginUserName from command line: {DeveloperLoginUserName}");

        break; // Stop after finding the argument
      }
    }


    _productId = configValues.GetValueOrDefault("EOS_PRODUCT_ID", ""); // Otherwise, use dictionary
    _sandboxId = configValues.GetValueOrDefault("EOS_SANDBOX_ID", "");
    _deploymentId = configValues.GetValueOrDefault("EOS_DEPLOYMENT_ID", "");
    _clientId = configValues.GetValueOrDefault("EOS_CLIENT_ID", "");
    _clientSecret = configValues.GetValueOrDefault("EOS_CLIENT_SECRET", "");
    // 3. Validate required fields (This part remains the same)
    if (string.IsNullOrEmpty(_productId) || string.IsNullOrEmpty(_sandboxId) || string.IsNullOrEmpty(_deploymentId) ||
        string.IsNullOrEmpty(_clientId)) {
      GD.PushWarning(
        "EOSManager: One or more required EOS configuration values (ProductID, SandboxID, DeploymentID, ClientID) are missing.");

      if (string.IsNullOrEmpty(_clientSecret)) {
        GD.Print(
          "EOSManager: EOS ClientSecret is also missing. This might be okay for some login types (like AccountPortal) but required for others (like Dev Auth Tool).");
      }

      // Decide if this is a fatal error

      return false;
    }


    GD.Print("EOSManager: Configuration loaded.");

    return true; // Indicate success even if some values are missing, handle missing values later
  }

  public override void _ExitTree() {
    base._ExitTree();
    SteamClient.Shutdown();
  }
}
