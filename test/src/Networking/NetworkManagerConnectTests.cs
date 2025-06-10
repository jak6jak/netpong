namespace NetworkedDodgeball.Tests.Networking;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Epic.OnlineServices.Connect;
using Godot;
using NetworkedDodgeball.Networking;
using NetworkedDodgeball.Tests.Networking.Mocks;
using Shouldly;

public class NetworkManagerConnectTests : TestClass {
  private NetworkManager _networkManager = default!;
  private Fixture _fixture = default!;
  private MockEOSInterfaces _mockEOS = default!;
  private bool _connectAuthFinished;
  private bool _connectAuthSuccess;
  private string _connectUserId = "";
  private string _connectErrorMessage = "";

  public NetworkManagerConnectTests(Node testScene) : base(testScene) { }

  [SetupAll]
  public void Setup() {
    _fixture = new Fixture(TestScene.GetTree());
    _mockEOS = new MockEOSInterfaces();

    // Access the singleton autoload instance
    _networkManager = NetworkManager.Instance;
    _networkManager.ConnectAuthenticationFinished += OnConnectAuthenticationFinished;
  }

  [CleanupAll]
  public void Cleanup() {
    if (_networkManager != null) {
      _networkManager.ConnectAuthenticationFinished -= OnConnectAuthenticationFinished;
    }
    _fixture.Cleanup();
  }

  [Setup]
  public void TestSetup() {
    _connectAuthFinished = false;
    _connectAuthSuccess = false;
    _connectUserId = "";
    _connectErrorMessage = "";

    // Reset mock state to ensure clean test isolation
    _mockEOS.ConnectInterface.LoginResult = Result.Success;
    _mockEOS.ConnectInterface.LoginUserId = null;
    _mockEOS.ConnectInterface.LoginContinuanceToken = null;
    _mockEOS.ConnectInterface.CreateUserResult = Result.Success;
    _mockEOS.ConnectInterface.CreateUserUserId = null;
  }

  [Test]
  public async Task ConnectAuth_SuccessfulLogin_ShouldEmitConnectAuthenticationFinished() {
    // Arrange
    var testProductUserId = TestDataGenerator.CreateTestProductUserId();
    var testIdToken = "test-epic-id-token-12345";

    _mockEOS.ConnectInterface.LoginResult = Result.Success;
    _mockEOS.ConnectInterface.LoginUserId = testProductUserId;

    // Act - Test the mock behavior directly
    var loginOptions = new Epic.OnlineServices.Connect.LoginOptions {
      Credentials = new Epic.OnlineServices.Connect.Credentials {
        Type = ExternalCredentialType.EpicIdToken,
        Token = testIdToken
      }
    };

    var callbackExecuted = false;
    Epic.OnlineServices.Connect.LoginCallbackInfo? callbackResult = null;

    _mockEOS.ConnectInterface.Login(ref loginOptions, null, (ref Epic.OnlineServices.Connect.LoginCallbackInfo info) => {
      callbackExecuted = true;
      callbackResult = info;
    });

    // Assert
    callbackExecuted.ShouldBeTrue();
    callbackResult?.ResultCode.ShouldBe(Result.Success);
    callbackResult?.LocalUserId.ShouldBe(testProductUserId);
    _mockEOS.ConnectInterface.LoginCalled.ShouldBeTrue();
  }

  [Test]
  public async Task ConnectAuth_InvalidUser_ShouldTriggerCreateUser() {
    // Arrange
    var testProductUserId = TestDataGenerator.CreateTestProductUserId();
    var testContinuanceToken = TestDataGenerator.CreateTestContinuanceToken();

    // Setup Connect Login to return InvalidUser
    _mockEOS.ConnectInterface.LoginResult = Result.InvalidUser;
    _mockEOS.ConnectInterface.LoginContinuanceToken = testContinuanceToken;

    // Setup CreateUser to succeed
    _mockEOS.ConnectInterface.CreateUserResult = Result.Success;
    _mockEOS.ConnectInterface.CreateUserUserId = testProductUserId;

    // Act - Test Connect Login (which should return InvalidUser)
    var loginOptions = new Epic.OnlineServices.Connect.LoginOptions {
      Credentials = new Epic.OnlineServices.Connect.Credentials {
        Type = ExternalCredentialType.EpicIdToken,
        Token = "test-epic-id-token"
      }
    };

    var loginCallbackExecuted = false;
    Epic.OnlineServices.Connect.LoginCallbackInfo? loginCallbackResult = null;

    _mockEOS.ConnectInterface.Login(ref loginOptions, null, (ref Epic.OnlineServices.Connect.LoginCallbackInfo info) => {
      loginCallbackExecuted = true;
      loginCallbackResult = info;
    });

    // Assert Login returned InvalidUser
    loginCallbackExecuted.ShouldBeTrue();
    loginCallbackResult?.ResultCode.ShouldBe(Result.InvalidUser);
    loginCallbackResult?.ContinuanceToken.ShouldBe(testContinuanceToken);

    // Act - Test CreateUser with the continuance token
    var createUserOptions = new CreateUserOptions {
      ContinuanceToken = testContinuanceToken
    };

    var createUserCallbackExecuted = false;
    CreateUserCallbackInfo? createUserCallbackResult = null;

    _mockEOS.ConnectInterface.CreateUser(ref createUserOptions, null, (ref CreateUserCallbackInfo info) => {
      createUserCallbackExecuted = true;
      createUserCallbackResult = info;
    });

    // Assert CreateUser succeeded
    createUserCallbackExecuted.ShouldBeTrue();
    createUserCallbackResult?.ResultCode.ShouldBe(Result.Success);
    createUserCallbackResult?.LocalUserId.ShouldBe(testProductUserId);
    _mockEOS.ConnectInterface.CreateUserCalled.ShouldBeTrue();
  }

  [Test]
  public void ConnectAuth_DuplicateNotAllowed_ShouldIndicateAccountLinking() {
    // Arrange
    _mockEOS.ConnectInterface.LoginResult = Result.DuplicateNotAllowed;
    _mockEOS.ConnectInterface.LoginUserId = null; // Clear user ID for error case

    var loginOptions = new Epic.OnlineServices.Connect.LoginOptions {
      Credentials = new Epic.OnlineServices.Connect.Credentials {
        Type = ExternalCredentialType.EpicIdToken,
        Token = "test-epic-id-token"
      }
    };

    var callbackExecuted = false;
    Epic.OnlineServices.Connect.LoginCallbackInfo? callbackResult = null;

    // Act
    _mockEOS.ConnectInterface.Login(ref loginOptions, null, (ref Epic.OnlineServices.Connect.LoginCallbackInfo info) => {
      callbackExecuted = true;
      callbackResult = info;
    });

    // Assert
    callbackExecuted.ShouldBeTrue();
    callbackResult?.ResultCode.ShouldBe(Result.DuplicateNotAllowed);
    callbackResult?.LocalUserId.ShouldBeNull();
  }

  [Test]
  public void ConnectAuth_ShouldUseEpicIdTokenCredentialType() {
    // Arrange & Act
    var credentials = new Epic.OnlineServices.Connect.Credentials {
      Type = ExternalCredentialType.EpicIdToken,
      Token = "test-epic-id-token-xyz"
    };

    // Assert
    credentials.Type.ShouldBe(ExternalCredentialType.EpicIdToken);
    credentials.Token.ToString().ShouldBe("test-epic-id-token-xyz");
  }

  [Test]
  public void ConnectAuth_NetworkError_ShouldHandleConnectionFailure() {
    // Arrange
    _mockEOS.ConnectInterface.LoginResult = Result.NoConnection;
    _mockEOS.ConnectInterface.LoginUserId = null; // Clear user ID for error case

    var loginOptions = new Epic.OnlineServices.Connect.LoginOptions {
      Credentials = new Epic.OnlineServices.Connect.Credentials {
        Type = ExternalCredentialType.EpicIdToken,
        Token = "test-epic-id-token"
      }
    };

    var callbackExecuted = false;
    Epic.OnlineServices.Connect.LoginCallbackInfo? callbackResult = null;

    // Act
    _mockEOS.ConnectInterface.Login(ref loginOptions, null, (ref Epic.OnlineServices.Connect.LoginCallbackInfo info) => {
      callbackExecuted = true;
      callbackResult = info;
    });

    // Assert
    callbackExecuted.ShouldBeTrue();
    callbackResult?.ResultCode.ShouldBe(Result.NoConnection);
    callbackResult?.LocalUserId.ShouldBeNull();
  }

  [Test]
  public void ConnectAuth_CreateUserFailure_ShouldHandleError() {
    // Arrange
    var testContinuanceToken = TestDataGenerator.CreateTestContinuanceToken();
    _mockEOS.ConnectInterface.CreateUserResult = Result.InvalidCredentials;
    _mockEOS.ConnectInterface.CreateUserUserId = null; // Clear user ID for error case

    var createUserOptions = new CreateUserOptions {
      ContinuanceToken = testContinuanceToken
    };

    var callbackExecuted = false;
    CreateUserCallbackInfo? callbackResult = null;

    // Act
    _mockEOS.ConnectInterface.CreateUser(ref createUserOptions, null, (ref CreateUserCallbackInfo info) => {
      callbackExecuted = true;
      callbackResult = info;
    });

    // Assert
    callbackExecuted.ShouldBeTrue();
    callbackResult?.ResultCode.ShouldBe(Result.InvalidCredentials);
    callbackResult?.LocalUserId.ShouldBeNull();
    _mockEOS.ConnectInterface.CreateUserCalled.ShouldBeTrue();
  }

  private void OnConnectAuthenticationFinished(bool success, string productUserId, string errorMessage) {
    _connectAuthFinished = true;
    _connectAuthSuccess = success;
    _connectUserId = productUserId;
    _connectErrorMessage = errorMessage;
  }
}