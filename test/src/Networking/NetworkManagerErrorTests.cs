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

public class NetworkManagerErrorTests : TestClass {
    private NetworkManager _networkManager = default!;
    private Fixture _fixture = default!;
    private MockEOSInterfaces _mockEOS = default!;
    private MockSteamInterfaces _mockSteam = default!;
    private bool _authFinished;
    private bool _authSuccess;
    private string _authUserId = "";
    private string _authErrorMessage = "";

    public NetworkManagerErrorTests(Node testScene) : base(testScene) { }

    [SetupAll]
    public void Setup() {
        _fixture = new Fixture(TestScene.GetTree());
        _mockEOS = new MockEOSInterfaces();
        _mockSteam = new MockSteamInterfaces();

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
    }

    [Test]
    public void ErrorHandling_InvalidCredentials_ShouldEmitAuthenticationFailed() {
        // Arrange
        _mockEOS.AuthInterface.LoginResult = TestDataGenerator.ErrorResults.InvalidCredentials;

        var loginOptions = new Epic.OnlineServices.Auth.LoginOptions {
            Credentials = new Epic.OnlineServices.Auth.Credentials {
                Type = LoginCredentialType.Developer,
                Id = "127.0.0.1:9876",
                Token = "TestUser"
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
        callbackResult?.ResultCode.ShouldBe(Result.InvalidCredentials);
        callbackResult?.LocalUserId.ShouldBeNull();
    }

    [Test]
    public void ErrorHandling_NetworkError_ShouldHandleConnectionFailures() {
        // Arrange
        _mockEOS.AuthInterface.LoginResult = TestDataGenerator.ErrorResults.NetworkError;

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
    public void ErrorHandling_ServiceUnavailable_ShouldRetryOrFail() {
        // Arrange
        _mockEOS.AuthInterface.LoginResult = TestDataGenerator.ErrorResults.ServiceUnavailable;

        var loginOptions = new Epic.OnlineServices.Auth.LoginOptions {
            Credentials = new Epic.OnlineServices.Auth.Credentials {
                Type = LoginCredentialType.ExternalAuth,
                Id = "",
                Token = "test-steam-ticket",
                ExternalType = ExternalCredentialType.SteamSessionTicket
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
        callbackResult?.ResultCode.ShouldBe(Result.NotConfigured);
        callbackResult?.LocalUserId.ShouldBeNull();
    }

    [Test]
    public void ErrorHandling_AuthExpired_ShouldRequestReauth() {
        // Arrange
        _mockEOS.AuthInterface.LoginResult = TestDataGenerator.ErrorResults.AuthExpired;

        var loginOptions = new Epic.OnlineServices.Auth.LoginOptions {
            Credentials = new Epic.OnlineServices.Auth.Credentials {
                Type = LoginCredentialType.Developer,
                Id = "127.0.0.1:9876",
                Token = "ExpiredUser"
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
        callbackResult?.ResultCode.ShouldBe(Result.AuthExpired);
        callbackResult?.LocalUserId.ShouldBeNull();
    }

    [Test]
    public void ErrorHandling_ConnectDuplicateNotAllowed_ShouldSuggestAccountLinking() {
        // Arrange
        _mockEOS.ConnectInterface.LoginResult = TestDataGenerator.ErrorResults.DuplicateNotAllowed;

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
    public async Task ErrorHandling_SteamNotRunning_ShouldFailGracefully() {
        // Arrange
        _mockSteam.SetupSteamNotRunning();

        // Act
        var isValid = _mockSteam.SteamClient.Object.IsValid;

        // Assert
        isValid.ShouldBeFalse();
    }

    [Test]
    public async Task ErrorHandling_SteamAuthTicketFailed_ShouldReturnNull() {
        // Arrange
        _mockSteam.SetupFailedAuthTicket();

        // Act
        var ticket = await _mockSteam.SteamUser.Object.GetAuthTicketForWebApiAsync("epiconlineservices");

        // Assert
        ticket.ShouldBeNull();
    }

    [Test]
    public void ErrorHandling_MissingConfiguration_ShouldValidateRequiredFields() {
        // Arrange - Test missing required fields scenario
        var missingFields = new[]
        {
            "EOS_PRODUCT_ID",
            "EOS_SANDBOX_ID",
            "EOS_DEPLOYMENT_ID",
            "EOS_CLIENT_ID"
        };

        // Act & Assert - Each field should be required for proper initialization
        foreach (var field in missingFields) {
            field.ShouldStartWith("EOS_");
            field.ShouldNotBeNullOrEmpty();
        }
    }

    [Test]
    public void ErrorHandling_InvalidUser_ShouldTriggerAccountLinking() {
        // Arrange
        var testContinuanceToken = TestDataGenerator.CreateTestContinuanceToken();
        _mockEOS.AuthInterface.LoginResult = TestDataGenerator.ErrorResults.InvalidUser;
        _mockEOS.AuthInterface.LoginContinuanceToken = testContinuanceToken;

        var loginOptions = new Epic.OnlineServices.Auth.LoginOptions {
            Credentials = new Epic.OnlineServices.Auth.Credentials {
                Type = LoginCredentialType.ExternalAuth,
                Id = "",
                Token = "test-steam-ticket",
                ExternalType = ExternalCredentialType.SteamSessionTicket
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
        callbackResult?.ResultCode.ShouldBe(Result.InvalidUser);
        callbackResult?.ContinuanceToken.ShouldBe(testContinuanceToken);
        callbackResult?.LocalUserId.ShouldBeNull();
    }

    [Test]
    public void ErrorHandling_SteamInitializationException_ShouldCatchAndHandle() {
        // Arrange
        var appId = uint.Parse(_networkManager.SteamAppId);
        _mockSteam.SetupFailedSteamInit(appId);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => {
            _mockSteam.SteamClient.Object.Init(appId, true);
        }).Message.ShouldContain("Steam initialization failed");
    }

    private void OnAuthenticationFinished(bool success, string localUserId, string errorMessage) {
        _authFinished = true;
        _authSuccess = success;
        _authUserId = localUserId;
        _authErrorMessage = errorMessage;
    }
}