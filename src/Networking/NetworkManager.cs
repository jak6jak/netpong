using Godot;

namespace NetworkedDodgeball.Networking;

using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Epic.OnlineServices.Platform;
using Environment = Godot.Environment;

public partial class NetworkManager : Node {
  private string _productId = "";
  private string _sandboxId = "";
  private string _deploymentId = "";
  private string _clientId = "";
  private string _clientSecret = ""; // Be extra careful with this!

  // --- Non-Sensitive Config (Can still be Exported if desired) ---
  [Export] public string ProductName = "MyGodotGame";
  [Export] public string ProductVersion = "1.0";
  [Export] public LoginCredentialType LoginCredentialType = LoginCredentialType.AccountPortal;
  public string LoginCredentialId = null;
  public string LoginCredentialToken = null;
  // ... other non-secret exports ...

  // --- EOS SDK State ---
  public static PlatformInterface EOSPlatformInterface { get; private set; }
  // ... rest of the variables ...
  private const double PlatformTickInterval = 0.1f;
  private double PlatformTickTimer = 0f;
  // Path to the config file
  private const string EnvFilePath = "res://.env";
  private static readonly string[] separator = new[] { "\r\n", "\r", "\n" };

  public override void _Ready() {
    if (!LoadConfiguration()) {
      GD.PushError("EOSManager: Failed to load EOS configuration. Check .env file or environment variables.");
      // Optionally disable EOS functionality or quit
      // GetTree().Quit();
      return; // Stop initialization
    }

    var initializeOptions = new Epic.OnlineServices.Platform.InitializeOptions() {
      ProductName = this.ProductName, ProductVersion = this.ProductVersion,
    };
    Result initializeResult = Epic.OnlineServices.Platform.PlatformInterface.Initialize(ref initializeOptions);
    if (initializeResult != Result.Success) {
      GD.PushError("Failed to initialize EOS configuration. " + initializeResult);
    }

    Epic.OnlineServices.Logging.LoggingInterface.SetLogLevel(Epic.OnlineServices.Logging.LogCategory.AllCategories, Epic.OnlineServices.Logging.LogLevel.VeryVerbose);
    Epic.OnlineServices.Logging.LoggingInterface.SetCallback((ref Epic.OnlineServices.Logging.LogMessage logMessage) => GD.Print(logMessage.Message));

    var options = new Epic.OnlineServices.Platform.Options()
    {
      ProductId = _productId,
      SandboxId = _sandboxId,
      DeploymentId = _deploymentId,
      ClientCredentials = new Epic.OnlineServices.Platform.ClientCredentials()
      {
        ClientId = _clientId,
        ClientSecret = _clientSecret
      }
    };


    EOSPlatformInterface = Epic.OnlineServices.Platform.PlatformInterface.Create(ref options);
    if (EOSPlatformInterface == null) {
      GD.PushError("Failed to create EOS platform interface.");
    }

    var loginOptions = new LoginOptions()
    {
      Credentials = new Credentials()
      {
        Type = LoginCredentialType,
        Id = LoginCredentialId,
        Token = LoginCredentialToken
      },
      // Change these scopes to match the ones set up on your product on the Developer Portal.
      ScopeFlags = Epic.OnlineServices.Auth.AuthScopeFlags.BasicProfile | Epic.OnlineServices.Auth.AuthScopeFlags.FriendsList | Epic.OnlineServices.Auth.AuthScopeFlags.Presence
    };

    EOSPlatformInterface.GetAuthInterface().Login(ref loginOptions, null, (ref LoginCallbackInfo loginCallbackInfo) =>
    {
      if (loginCallbackInfo.ResultCode == Result.Success)
      {
        GD.Print("Login succeeded");
      }
      else if (Common.IsOperationComplete(loginCallbackInfo.ResultCode))
      {
        GD.Print("Login failed: " + loginCallbackInfo.ResultCode);
      }
    });
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




  private bool LoadConfiguration()
    {
        var configValues = new Dictionary<string, string>();

        // 1. Try loading from .env file using Godot's FileAccess
        if (FileAccess.FileExists(EnvFilePath))
        {
            GD.Print($"EOSManager: Loading configuration from {EnvFilePath}");
            // Use 'using' to ensure the file is closed automatically
            using var file = FileAccess.Open(EnvFilePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                Error fileError = FileAccess.GetOpenError();
                GD.PushError($"EOSManager: Failed to open .env file '{EnvFilePath}'. Error: {fileError}");
            }
            else
            {
                try
                {
                    string content = file.GetAsText();
                    // Split the content into lines, handling different line endings
                    string[] lines = content.Split(separator, StringSplitOptions.None);

                    foreach (string line in lines)
                    {
                        string trimmedLine = line.Trim();
                        if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#')) // Skip empty lines and comments
                            continue;

                        int equalsIndex = trimmedLine.IndexOf('=');
                        if (equalsIndex > 0)
                        {
                            string key = trimmedLine.Substring(0, equalsIndex).Trim();
                            string value = trimmedLine.Substring(equalsIndex + 1).Trim();
                            if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
                            {
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
        else
        {
            GD.Print($"EOSManager: .env file not found at {EnvFilePath}. Will check environment variables.");
        }
        string envProductId = OS.GetEnvironment("EOS_PRODUCT_ID");
        _productId = !string.IsNullOrEmpty(envProductId)
          ? envProductId // Use env var if it's not null or empty
          : configValues.GetValueOrDefault("EOS_PRODUCT_ID", ""); // Otherwise, use dictionary

        string envSandboxId = OS.GetEnvironment("EOS_SANDBOX_ID");
        _sandboxId = !string.IsNullOrEmpty(envSandboxId)
          ? envSandboxId
          : configValues.GetValueOrDefault("EOS_SANDBOX_ID", "");

        string envDeploymentId = OS.GetEnvironment("EOS_DEPLOYMENT_ID");
        _deploymentId = !string.IsNullOrEmpty(envDeploymentId)
          ? envDeploymentId
          : configValues.GetValueOrDefault("EOS_DEPLOYMENT_ID", "");

        string envClientId = OS.GetEnvironment("EOS_CLIENT_ID");
        _clientId = !string.IsNullOrEmpty(envClientId)
          ? envClientId
          : configValues.GetValueOrDefault("EOS_CLIENT_ID", "");

        string envClientSecret = OS.GetEnvironment("EOS_CLIENT_SECRET");
        _clientSecret = !string.IsNullOrEmpty(envClientSecret)
          ? envClientSecret
          : configValues.GetValueOrDefault("EOS_CLIENT_SECRET", "");
        // 3. Validate required fields (This part remains the same)
        if (string.IsNullOrEmpty(_productId) || string.IsNullOrEmpty(_sandboxId) || string.IsNullOrEmpty(_deploymentId) || string.IsNullOrEmpty(_clientId))
        {
            GD.PushWarning("EOSManager: One or more required EOS configuration values (ProductID, SandboxID, DeploymentID, ClientID) are missing.");
            if (string.IsNullOrEmpty(_clientSecret))
            {
                GD.Print("EOSManager: EOS ClientSecret is also missing. This might be okay for some login types (like AccountPortal) but required for others (like Dev Auth Tool).");
            }
            // Decide if this is a fatal error
            return false;
        }

        GD.Print("EOSManager: Configuration loaded.");
        return true; // Indicate success even if some values are missing, handle missing values later
    }
}
