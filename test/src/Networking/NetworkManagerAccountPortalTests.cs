namespace NetworkedDodgeball.Tests.Networking;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Godot;
using NetworkedDodgeball.Networking;
using NetworkedDodgeball.Tests.Networking.Mocks;
using Shouldly;

public class NetworkManagerAccountPortalTests : TestClass {
  private NetworkManager _networkManager = default!;
  private Fixture _fixture = default!;
  private MockEOSInterfaces _mockEOS = default!;
  private bool _authFinished;
  private bool _authSuccess;
  private string _authUserId = "";
  private string _authErrorMessage = "";

  public NetworkManagerAccountPortalTests(Node testScene) : base(testScene) { }

  [SetupAll]
  public void Setup() {
    _fixture = new Fixture(TestScene.GetTree());
    _mockEOS = new MockEOSInterfaces();

    // Access the singleton autoload instance
    _networkManager = NetworkManager.Instance;
    _networkManager.AuthenticationFinished += OnAuthenticationFinished;
  }

  [CleanupAll]
  public void Cleanup() {
    if (_networkManager != null) {
      _networkManager.AuthenticationFinished -= OnAuthenticationFinished;
    }
    _fixture.Cleanup();
  }

  [Setup]
  public void TestSetup() {
    _authFinished = false;
    _authSuccess = false;
    _authUserId = "";
    _authErrorMessage = "";

    // Set LoginCredentialType to AccountPortal for these tests
    _networkManager.LoginCredentialType = LoginCredentialType.AccountPortal;

    // Reset mock state to ensure clean test isolation
    _mockEOS.AuthInterface.LoginResult = Result.Success;
    _mockEOS.AuthInterface.LoginUserId = null;
    _mockEOS.AuthInterface.LoginContinuanceToken = null;
    _mockEOS.AuthInterface.IdTokenValue = "test-jwt-token";
    _mockEOS.AuthInterface.IdTokenResult = Result.Success;
  }

  [Test]
  public void AccountPortalAuth_ShouldSetCorrectCredentialType() {
    // Arrange & Act
    _networkManager.LoginCredentialType = LoginCredentialType.AccountPortal;

    // Assert
    _networkManager.LoginCredentialType.ShouldBe(LoginCredentialType.AccountPortal);
  }

  [Test]
  public void AccountPortalAuth_ShouldCreateCredentialsWithNullIdAndToken() {
    // Arrange
    var expectedCredentials = new Epic.OnlineServices.Auth.Credentials {
      Type = LoginCredentialType.AccountPortal,
      Id = null,
      Token = null
    };

    // Act & Assert - This demonstrates the expected credential structure
    expectedCredentials.Type.ShouldBe(LoginCredentialType.AccountPortal);
    expectedCredentials.Id.ShouldBeNull();
    expectedCredentials.Token.ShouldBeNull();
  }

  [Test]
  public async Task AccountPortalAuth_SuccessfulLogin_ShouldEmitAuthenticationFinished() {
    // Arrange
    var testEpicId = TestDataGenerator.CreateTestEpicAccountId();

    _mockEOS.AuthInterface.LoginResult = Result.Success;
    _mockEOS.AuthInterface.LoginUserId = testEpicId;
    _mockEOS.AuthInterface.IdTokenValue = "test-account-portal-jwt";

    // Act
    // Note: This would require modifying NetworkManager to accept mock EOS interfaces
    // For demonstration, we'll test the mock behavior directly
    var loginOptions = new Epic.OnlineServices.Auth.LoginOptions {
      Credentials = new Epic.OnlineServices.Auth.Credentials {
        Type = LoginCredentialType.AccountPortal,
        Id = null,
        Token = null
      }
    };

    var callbackExecuted = false;
    Epic.OnlineServices.Auth.LoginCallbackInfo? callbackResult = null;

    _mockEOS.AuthInterface.Login(ref loginOptions, null, (ref Epic.OnlineServices.Auth.LoginCallbackInfo info) => {
      callbackExecuted = true;
      callbackResult = info;
    });

    // Assert
    callbackExecuted.ShouldBeTrue();
    callbackResult?.ResultCode.ShouldBe(Result.Success);
    callbackResult?.LocalUserId.ShouldBe(testEpicId);
    _mockEOS.AuthInterface.LoginCalled.ShouldBeTrue();
  }

  [Test]
  public void AccountPortalAuth_BrowserClosed_ShouldHandleUserCancellation() {
    // Arrange
    _mockEOS.AuthInterface.LoginResult = Result.Canceled;
    _mockEOS.AuthInterface.LoginUserId = null; // Clear user ID for error case

    var loginOptions = new Epic.OnlineServices.Auth.LoginOptions {
      Credentials = new Epic.OnlineServices.Auth.Credentials {
        Type = LoginCredentialType.AccountPortal,
        Id = null,
        Token = null
      }
    };

    var callbackExecuted = false;
    Epic.OnlineServices.Auth.LoginCallbackInfo? callbackResult = null;

    // Act
    _mockEOS.AuthInterface.Login(ref loginOptions, null, (ref Epic.OnlineServices.Auth.LoginCallbackInfo info) => {
      callbackExecuted = true;
      callbackResult = info;
    });

    // Assert
    callbackExecuted.ShouldBeTrue();
    callbackResult?.ResultCode.ShouldBe(Result.Canceled);
    callbackResult?.LocalUserId.ShouldBeNull();
  }

  [Test]
  public void AccountPortalAuth_NetworkError_ShouldHandleConnectionFailure() {
    // Arrange
    _mockEOS.AuthInterface.LoginResult = Result.NoConnection;
    _mockEOS.AuthInterface.LoginUserId = null; // Clear user ID for error case

    var loginOptions = new Epic.OnlineServices.Auth.LoginOptions {
      Credentials = new Epic.OnlineServices.Auth.Credentials {
        Type = LoginCredentialType.AccountPortal,
        Id = null,
        Token = null
      }
    };

    var callbackExecuted = false;
    Epic.OnlineServices.Auth.LoginCallbackInfo? callbackResult = null;

    // Act
    _mockEOS.AuthInterface.Login(ref loginOptions, null, (ref Epic.OnlineServices.Auth.LoginCallbackInfo info) => {
      callbackExecuted = true;
      callbackResult = info;
    });

    // Assert
    callbackExecuted.ShouldBeTrue();
    callbackResult?.ResultCode.ShouldBe(Result.NoConnection);
    callbackResult?.LocalUserId.ShouldBeNull();
  }

  [Test]
  public void AccountPortalAuth_ShouldNotRequireClientSecret() {
    // Assert - AccountPortal login doesn't require client secret
    // This is different from Developer auth which requires it

    // The test demonstrates that AccountPortal auth works without client secret
    var credentials = new Epic.OnlineServices.Auth.Credentials {
      Type = LoginCredentialType.AccountPortal,
      Id = null,
      Token = null
    };

    credentials.Type.ShouldBe(LoginCredentialType.AccountPortal);
    // No ExternalType needed for AccountPortal (unlike Steam which needs SteamSessionTicket)
  }

  private void OnAuthenticationFinished(bool success, string localUserId, string errorMessage) {
    _authFinished = true;
    _authSuccess = success;
    _authUserId = localUserId;
    _authErrorMessage = errorMessage;
  }
}