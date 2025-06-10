namespace NetworkedDodgeball.Tests.Networking;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Drivers;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Godot;
using NetworkedDodgeball.Networking;
using NetworkedDodgeball.Tests.Networking.Mocks;
using Shouldly;

public class NetworkManagerTests : TestClass {
    private NetworkManager _networkManager = default!;
    private Fixture _fixture = default!;
    private MockEOSInterfaces _mockEOS = default!;
    private MockSteamInterfaces _mockSteam = default!;

    public NetworkManagerTests(Node testScene) : base(testScene) { }

    [SetupAll]
    public void Setup() {
        _fixture = new Fixture(TestScene.GetTree());
        _mockEOS = new MockEOSInterfaces();
        _mockSteam = new MockSteamInterfaces();

        // Access the singleton autoload instance
        _networkManager = NetworkManager.Instance;

        // Wait a frame for initialization if needed
    }

    [CleanupAll]
    public void Cleanup() => _fixture.Cleanup();

    [Test]
    public void NetworkManager_ShouldBeSingleton() {
        // Arrange & Act
        var instance1 = NetworkManager.Instance;
        var instance2 = NetworkManager.Instance;

        // Assert
        instance1.ShouldBe(instance2);
        instance1.ShouldNotBeNull();
    }

    [Test]
    public void NetworkManager_ShouldSetDefaultLoginCredentialType() {
        // Assert - Default should be ExternalAuth (Steam)
        _networkManager.LoginCredentialType.ShouldBe(LoginCredentialType.ExternalAuth);
    }

    [Test]
    public void NetworkManager_ShouldHaveDefaultSteamAppId() {
        // Assert
        _networkManager.SteamAppId.ShouldBe("3136980");
    }

    [Test]
    public void NetworkManager_ShouldHaveDefaultProductName() {
        // Assert
        _networkManager.ProductName.ShouldBe("MyGodotGame");
        _networkManager.ProductVersion.ShouldBe("1.0");
    }

    [Test]
    public void NetworkManager_ShouldHaveDefaultDeveloperSettings() {
        // Assert
        _networkManager.DevAuthURL.ShouldBe("127.0.0.1:9876");
        _networkManager.DeveloperLoginUserName.ShouldBe("DevUser1");
    }
}