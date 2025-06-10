namespace NetworkedDodgeball.Tests.Networking.Mocks;

using System;
using System.Text;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Epic.OnlineServices.Connect;

public static class TestDataGenerator
{
    public static EpicAccountId CreateTestEpicAccountId(string userId = "test-epic-user-123")
    {
        return EpicAccountId.FromString(userId);
    }

    public static ProductUserId CreateTestProductUserId(string userId = "test-product-user-456")
    {
        return ProductUserId.FromString(userId);
    }

    public static byte[] CreateTestSteamTicket(int size = 1024)
    {
        var random = new Random(42); // Use fixed seed for reproducible tests
        var ticket = new byte[size];
        random.NextBytes(ticket);
        return ticket;
    }

    public static string CreateTestSteamTicketHex()
    {
        var ticket = CreateTestSteamTicket(128);
        return Convert.ToHexString(ticket);
    }

    public static ContinuanceToken CreateTestContinuanceToken()
    {
        return new ContinuanceToken();
    }

    public static Epic.OnlineServices.Auth.IdToken CreateTestAuthIdToken(string token = "test-auth-jwt-token-12345")
    {
        return new Epic.OnlineServices.Auth.IdToken { JsonWebToken = token };
    }

    public static class ErrorResults
    {
        public static readonly Result InvalidCredentials = Result.InvalidCredentials;
        public static readonly Result InvalidUser = Result.InvalidUser;
        public static readonly Result NetworkError = Result.NoConnection;
        public static readonly Result ServiceUnavailable = Result.NotConfigured;
        public static readonly Result AuthExpired = Result.AuthExpired;
        public static readonly Result DuplicateNotAllowed = Result.DuplicateNotAllowed;
    }

    public static class TestEnvironmentVariables
    {
        public static readonly string ProductId = "test-product-id-123";
        public static readonly string SandboxId = "test-sandbox-id-456";
        public static readonly string DeploymentId = "test-deployment-id-789";
        public static readonly string ClientId = "test-client-id-abc";
        public static readonly string ClientSecret = "test-client-secret-def";
        public static readonly string SteamAppId = "3136980";
        public static readonly string DevAuthURL = "127.0.0.1:9876";
        public static readonly string DeveloperUsername = "TestDevUser";

        public static string CreateEnvFileContent()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"EOS_PRODUCT_ID={ProductId}");
            sb.AppendLine($"EOS_SANDBOX_ID={SandboxId}");
            sb.AppendLine($"EOS_DEPLOYMENT_ID={DeploymentId}");
            sb.AppendLine($"EOS_CLIENT_ID={ClientId}");
            sb.AppendLine($"EOS_CLIENT_SECRET={ClientSecret}");
            return sb.ToString();
        }

        public static string CreateMalformedEnvFileContent()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# This is a comment");
            sb.AppendLine("EOS_PRODUCT_ID=test-product-id-123");
            sb.AppendLine("MALFORMED_LINE_WITHOUT_EQUALS");
            sb.AppendLine("EOS_SANDBOX_ID=\"test-sandbox-id-456\"");
            sb.AppendLine("EOS_DEPLOYMENT_ID='test-deployment-id-789'");
            sb.AppendLine("=INVALID_LINE_STARTING_WITH_EQUALS");
            sb.AppendLine("EOS_CLIENT_ID=test-client-id-abc");
            return sb.ToString();
        }
    }
}