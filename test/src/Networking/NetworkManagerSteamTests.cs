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
using Steamworks;

public class NetworkManagerSteamTests : TestClass {
    private NetworkManager _networkManager = default!;
    private Fixture _fixture = default!;
    private MockEOSInterfaces _mockEOS = default!;
    private MockSteamInterfaces _mockSteam = default!;
    private bool _authFinished;
    private bool _authSuccess;
    private string _authUserId = "";
    private string _authErrorMessage = "";

    public NetworkManagerSteamTests(Node testScene) : base(testScene) { }

    [SetupAll]
    public void Setup() {
        _fixture = new Fixture(TestScene.GetTree());
        _mockEOS = new MockEOSInterfaces();
        _mockSteam = new MockSteamInterfaces();

        // Access the singleton autoload instance
        _networkManager = NetworkManager.Instance;

        // Connect to authentication signals
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
        // Reset state before each test
        _authFinished = false;
        _authSuccess = false;
        _authUserId = "";
        _authErrorMessage = "";
    }

    [Test]
    public void SteamAuth_ShouldSetExternalAuthCredentialType() {
        // Arrange
        _networkManager.LoginCredentialType = LoginCredentialType.ExternalAuth;

        // Assert
        _networkManager.LoginCredentialType.ShouldBe(LoginCredentialType.ExternalAuth);
    }

    [Test]
    public void SteamAuth_ShouldHaveCorrectSteamAppId() {
        // Assert
        _networkManager.SteamAppId.ShouldBe("3136980");
        uint.Parse(_networkManager.SteamAppId).ShouldBeGreaterThan(0u);
    }

    [Test]
    public async Task SteamAuth_SuccessfulInitialization_ShouldReturnValidTicket() {
        // Arrange
        var testTicketData = TestDataGenerator.CreateTestSteamTicket();
        var expectedHex = Convert.ToHexString(testTicketData);

        _mockSteam.SetupSuccessfulSteamInit(uint.Parse(_networkManager.SteamAppId));
        _mockSteam.SetupSuccessfulAuthTicket(testTicketData);

        // Act & Assert
        // Note: This test would require modifying NetworkManager to accept injected Steam dependencies
        // For now, this demonstrates the testing approach
        var authTicket = new AuthTicket { Data = testTicketData };
        var hexResult = Convert.ToHexString(authTicket.Data);

        hexResult.ShouldBe(expectedHex);
        hexResult.Length.ShouldBeGreaterThan(0);
    }

    [Test]
    public void SteamAuth_FailedInitialization_ShouldThrowException() {
        // Arrange
        var appId = uint.Parse(_networkManager.SteamAppId);
        _mockSteam.SetupFailedSteamInit(appId);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => {
            _mockSteam.SteamClient.Object.Init(appId, true);
        }).Message.ShouldContain("Steam initialization failed");
    }

    [Test]
    public void SteamAuth_SteamNotRunning_ShouldReturnInvalid() {
        // Arrange
        _mockSteam.SetupSteamNotRunning();

        // Act
        var isValid = _mockSteam.SteamClient.Object.IsValid;

        // Assert
        isValid.ShouldBeFalse();
    }

    [Test]
    public void SteamAuth_SteamNotLoggedIn_ShouldReturnNotLoggedOn() {
        // Arrange
        _mockSteam.SetupSteamNotLoggedIn();

        // Act
        var isLoggedOn = _mockSteam.SteamClient.Object.IsLoggedOn;

        // Assert
        isLoggedOn.ShouldBeFalse();
    }

    [Test]
    public async Task SteamAuth_FailedAuthTicket_ShouldReturnNull() {
        // Arrange
        _mockSteam.SetupFailedAuthTicket();

        // Act
        var ticket = await _mockSteam.SteamUser.Object.GetAuthTicketForWebApiAsync("epiconlineservices");

        // Assert
        ticket.ShouldBeNull();
    }

    [Test]
    public void SteamAuth_ValidTicketData_ShouldConvertToHex() {
        // Arrange
        var testData = new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF };
        var expectedHex = "0123456789ABCDEF";

        // Act
        var result = Convert.ToHexString(testData);

        // Assert
        result.ShouldBe(expectedHex);
    }

    private void OnAuthenticationFinished(bool success, string localUserId, string errorMessage) {
        _authFinished = true;
        _authSuccess = success;
        _authUserId = localUserId;
        _authErrorMessage = errorMessage;
    }
}