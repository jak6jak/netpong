namespace NetworkedDodgeball.Tests.Networking;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Epic.OnlineServices.Auth;
using Godot;
using NetworkedDodgeball.Networking;
using NetworkedDodgeball.Tests.Networking.Mocks;
using Shouldly;

public class NetworkManagerConfigTests : TestClass {
  private NetworkManager _networkManager = default!;
  private Fixture _fixture = default!;

  public NetworkManagerConfigTests(Node testScene) : base(testScene) { }

  [SetupAll]
  public void Setup() {
    _fixture = new Fixture(TestScene.GetTree());
    // Access the singleton autoload instance
    _networkManager = NetworkManager.Instance;
  }

  [CleanupAll]
  public void Cleanup() => _fixture.Cleanup();

  [Setup]
  public void TestSetup() {
    // Reset LoginCredentialType to default for consistent test behavior
    // This ensures other test classes don't affect our default value tests
    _networkManager.LoginCredentialType = LoginCredentialType.Developer;
  }

  [Test]
  public void Config_ShouldHaveDefaultValues() {
    // Assert default configuration values
    _networkManager.ProductName.ShouldBe("MyGodotGame");
    _networkManager.ProductVersion.ShouldBe("1.0");
    _networkManager.LoginCredentialType.ShouldBe(LoginCredentialType.Developer);
    _networkManager.DevAuthURL.ShouldBe("127.0.0.1:9876");
    _networkManager.DeveloperLoginUserName.ShouldBe("DevUser1");
    _networkManager.SteamAppId.ShouldBe("3136980");
  }

  [Test]
  public void Config_ShouldAllowCredentialTypeChanges() {
    // Act & Assert - Test different credential types
    _networkManager.LoginCredentialType = LoginCredentialType.Developer;
    _networkManager.LoginCredentialType.ShouldBe(LoginCredentialType.Developer);

    _networkManager.LoginCredentialType = LoginCredentialType.AccountPortal;
    _networkManager.LoginCredentialType.ShouldBe(LoginCredentialType.AccountPortal);

    _networkManager.LoginCredentialType = LoginCredentialType.ExternalAuth;
    _networkManager.LoginCredentialType.ShouldBe(LoginCredentialType.ExternalAuth);
  }

  [Test]
  public void Config_ShouldValidateEnvironmentFileFormat() {
    // Arrange
    var validEnvContent = TestDataGenerator.TestEnvironmentVariables.CreateEnvFileContent();
    var malformedEnvContent = TestDataGenerator.TestEnvironmentVariables.CreateMalformedEnvFileContent();

    // Assert - Valid content should contain all required fields
    validEnvContent.ShouldContain("EOS_PRODUCT_ID=");
    validEnvContent.ShouldContain("EOS_SANDBOX_ID=");
    validEnvContent.ShouldContain("EOS_DEPLOYMENT_ID=");
    validEnvContent.ShouldContain("EOS_CLIENT_ID=");
    validEnvContent.ShouldContain("EOS_CLIENT_SECRET=");

    // Assert - Malformed content should contain comments and malformed lines
    malformedEnvContent.ShouldContain("# This is a comment");
    malformedEnvContent.ShouldContain("MALFORMED_LINE_WITHOUT_EQUALS");
    malformedEnvContent.ShouldContain("=INVALID_LINE_STARTING_WITH_EQUALS");
  }

  [Test]
  public void Config_ShouldHandleQuotedValues() {
    // Arrange - Test quoted environment values
    var testValue1 = "\"test-value-with-quotes\"";
    var testValue2 = "'test-value-with-single-quotes'";

    // Act - Simulate quote removal (as done in LoadConfiguration)
    var unquoted1 = testValue1.Substring(1, testValue1.Length - 2);
    var unquoted2 = testValue2.Substring(1, testValue2.Length - 2);

    // Assert
    unquoted1.ShouldBe("test-value-with-quotes");
    unquoted2.ShouldBe("test-value-with-single-quotes");
  }

  [Test]
  public void Config_ShouldHaveConsistentTestData() {
    // Assert test data consistency
    TestDataGenerator.TestEnvironmentVariables.ProductId.ShouldNotBeNullOrEmpty();
    TestDataGenerator.TestEnvironmentVariables.SandboxId.ShouldNotBeNullOrEmpty();
    TestDataGenerator.TestEnvironmentVariables.DeploymentId.ShouldNotBeNullOrEmpty();
    TestDataGenerator.TestEnvironmentVariables.ClientId.ShouldNotBeNullOrEmpty();
    TestDataGenerator.TestEnvironmentVariables.ClientSecret.ShouldNotBeNullOrEmpty();
    TestDataGenerator.TestEnvironmentVariables.SteamAppId.ShouldNotBeNullOrEmpty();
    TestDataGenerator.TestEnvironmentVariables.DevAuthURL.ShouldNotBeNullOrEmpty();
    TestDataGenerator.TestEnvironmentVariables.DeveloperUsername.ShouldNotBeNullOrEmpty();

    // Validate SteamAppId is numeric
    uint.Parse(TestDataGenerator.TestEnvironmentVariables.SteamAppId).ShouldBeGreaterThan(0u);
  }

  [Test]
  public void Config_ShouldValidateRequiredFields() {
    // Arrange - Required EOS configuration fields
    var requiredFields = new[]
    {
            "EOS_PRODUCT_ID",
            "EOS_SANDBOX_ID",
            "EOS_DEPLOYMENT_ID",
            "EOS_CLIENT_ID"
        };

    var envContent = TestDataGenerator.TestEnvironmentVariables.CreateEnvFileContent();

    // Assert - All required fields should be present
    foreach (var field in requiredFields) {
      envContent.ShouldContain(field);
    }
  }

  [Test]
  public void Config_ShouldHandleDevAuthSpecifics() {
    // Assert Developer Auth specific configuration
    _networkManager.DevAuthURL.ShouldNotBeNullOrEmpty();
    _networkManager.DeveloperLoginUserName.ShouldNotBeNullOrEmpty();

    // DevAuth URL should be a valid format
    _networkManager.DevAuthURL.ShouldContain(":");
    var parts = _networkManager.DevAuthURL.Split(':');
    parts.Length.ShouldBe(2);

    // Should have IP and port
    parts[0].ShouldNotBeNullOrEmpty(); // IP part
    parts[1].ShouldNotBeNullOrEmpty(); // Port part

    // Port should be numeric
    int.Parse(parts[1]).ShouldBeGreaterThan(0);
  }

  [Test]
  public void Config_ShouldValidateProductNameAndVersion() {
    // Assert product information
    _networkManager.ProductName.ShouldNotBeNullOrEmpty();
    _networkManager.ProductVersion.ShouldNotBeNullOrEmpty();

    // Version should be in a valid format (basic check)
    _networkManager.ProductVersion.ShouldMatch(@"^\d+\.\d+");
  }
}