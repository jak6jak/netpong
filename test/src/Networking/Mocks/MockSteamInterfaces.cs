namespace NetworkedDodgeball.Tests.Networking.Mocks;

using System;
using System.Threading.Tasks;
using LightMock.Generator;
using LightMoq;
using Steamworks;

public class MockSteamInterfaces
{
    public Mock<ISteamClient> SteamClient { get; }
    public Mock<ISteamUser> SteamUser { get; }

    public MockSteamInterfaces()
    {
        SteamClient = new Mock<ISteamClient>();
        SteamUser = new Mock<ISteamUser>();
    }

    public void SetupSuccessfulSteamInit(uint appId)
    {
        SteamClient.Setup(c => c.Init(appId, true)).Returns(true);
        SteamClient.Setup(c => c.IsValid).Returns(true);
        SteamClient.Setup(c => c.IsLoggedOn).Returns(true);
    }

    public void SetupFailedSteamInit(uint appId)
    {
        SteamClient.Setup(c => c.Init(appId, true)).Throws(() => new InvalidOperationException("Steam initialization failed"));
    }

    public void SetupSteamNotRunning()
    {
        SteamClient.Setup(c => c.IsValid).Returns(false);
    }

    public void SetupSteamNotLoggedIn()
    {
        SteamClient.Setup(c => c.IsValid).Returns(true);
        SteamClient.Setup(c => c.IsLoggedOn).Returns(false);
    }

    public void SetupSuccessfulAuthTicket(byte[] ticketData)
    {
        var authTicket = new AuthTicket { Data = ticketData };
        SteamUser.Setup(u => u.GetAuthTicketForWebApiAsync("epiconlineservices"))
            .Returns(Task.FromResult(authTicket));
    }

    public void SetupFailedAuthTicket()
    {
        SteamUser.Setup(u => u.GetAuthTicketForWebApiAsync("epiconlineservices"))
            .Returns(Task.FromResult<AuthTicket>(null!));
    }
}

// Interface wrappers for Steam API to enable mocking
public interface ISteamClient
{
    bool Init(uint appId, bool asyncCallbacks);
    bool IsValid { get; }
    bool IsLoggedOn { get; }
    void Shutdown();
}

public interface ISteamUser
{
    Task<AuthTicket> GetAuthTicketForWebApiAsync(string identity);
}

// Wrapper implementations that delegate to actual Steam API
public class SteamClientWrapper : ISteamClient
{
    public bool Init(uint appId, bool asyncCallbacks)
    {
        Steamworks.SteamClient.Init(appId, asyncCallbacks);
        return true;
    }
    
    public bool IsValid => Steamworks.SteamClient.IsValid;
    public bool IsLoggedOn => Steamworks.SteamClient.IsLoggedOn;
    public void Shutdown() => Steamworks.SteamClient.Shutdown();
}

public class SteamUserWrapper : ISteamUser
{
    public Task<AuthTicket> GetAuthTicketForWebApiAsync(string identity) => 
        Steamworks.SteamUser.GetAuthTicketForWebApiAsync(identity);
}