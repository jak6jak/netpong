namespace NetworkedDodgeball.Tests.Networking.Mocks;

using System;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.Platform;
using Epic.OnlineServices.Sessions;
using Epic.OnlineServices.UserInfo;

public class MockEOSInterfaces {
  public MockPlatformInterface PlatformInterface { get; }
  public MockAuthInterface AuthInterface { get; }
  public MockConnectInterface ConnectInterface { get; }
  public MockSessionsInterface SessionsInterface { get; }
  public MockUserInfoInterface UserInfoInterface { get; }

  public MockEOSInterfaces() {
    PlatformInterface = new MockPlatformInterface();
    AuthInterface = new MockAuthInterface();
    ConnectInterface = new MockConnectInterface();
    SessionsInterface = new MockSessionsInterface();
    UserInfoInterface = new MockUserInfoInterface();

    PlatformInterface.AuthInterface = AuthInterface;
    PlatformInterface.ConnectInterface = ConnectInterface;
    PlatformInterface.SessionsInterface = SessionsInterface;
    PlatformInterface.UserInfoInterface = UserInfoInterface;
  }
}

public class MockPlatformInterface {
  public MockAuthInterface AuthInterface { get; set; } = default!;
  public MockConnectInterface ConnectInterface { get; set; } = default!;
  public MockSessionsInterface SessionsInterface { get; set; } = default!;
  public MockUserInfoInterface UserInfoInterface { get; set; } = default!;

  public void Tick() { }
}

public class MockAuthInterface {
  public Result LoginResult { get; set; } = Result.Success;
  public EpicAccountId? LoginUserId { get; set; }
  public ContinuanceToken? LoginContinuanceToken { get; set; }
  public string IdTokenValue { get; set; } = "test-jwt-token";
  public Result IdTokenResult { get; set; } = Result.Success;
  public Result LinkAccountResult { get; set; } = Result.Success;
  public bool LoginCalled { get; private set; }
  public bool CopyIdTokenCalled { get; private set; }
  public bool LinkAccountCalled { get; private set; }

  public void Login(ref Epic.OnlineServices.Auth.LoginOptions options, object? clientData, Epic.OnlineServices.Auth.OnLoginCallback completionDelegate) {
    LoginCalled = true;
    var callbackInfo = new Epic.OnlineServices.Auth.LoginCallbackInfo {
      ResultCode = LoginResult,
      LocalUserId = LoginResult == Result.Success ? LoginUserId : null,
      ContinuanceToken = LoginContinuanceToken
    };
    completionDelegate(ref callbackInfo);
  }

  public Result CopyIdToken(ref Epic.OnlineServices.Auth.CopyIdTokenOptions options, out Epic.OnlineServices.Auth.IdToken? outIdToken) {
    CopyIdTokenCalled = true;
    if (IdTokenResult == Result.Success) {
      outIdToken = new Epic.OnlineServices.Auth.IdToken { JsonWebToken = IdTokenValue };
    }
    else {
      outIdToken = null;
    }
    return IdTokenResult;
  }

  public void LinkAccount(ref Epic.OnlineServices.Auth.LinkAccountOptions options, object? clientData, Epic.OnlineServices.Auth.OnLinkAccountCallback completionDelegate) {
    LinkAccountCalled = true;
    var callbackInfo = new Epic.OnlineServices.Auth.LinkAccountCallbackInfo {
      ResultCode = LinkAccountResult
    };
    completionDelegate(ref callbackInfo);
  }
}

public class MockConnectInterface {
  public Result LoginResult { get; set; } = Result.Success;
  public ProductUserId? LoginUserId { get; set; }
  public ContinuanceToken? LoginContinuanceToken { get; set; }
  public Result CreateUserResult { get; set; } = Result.Success;
  public ProductUserId? CreateUserUserId { get; set; }
  public bool LoginCalled { get; private set; }
  public bool CreateUserCalled { get; private set; }

  public void Login(ref Epic.OnlineServices.Connect.LoginOptions options, object? clientData, Epic.OnlineServices.Connect.OnLoginCallback completionDelegate) {
    LoginCalled = true;
    var callbackInfo = new Epic.OnlineServices.Connect.LoginCallbackInfo {
      ResultCode = LoginResult,
      LocalUserId = LoginResult == Result.Success ? LoginUserId : null,
      ContinuanceToken = LoginContinuanceToken
    };
    completionDelegate(ref callbackInfo);
  }

  public void CreateUser(ref CreateUserOptions options, object? clientData, OnCreateUserCallback completionDelegate) {
    CreateUserCalled = true;
    var callbackInfo = new CreateUserCallbackInfo {
      ResultCode = CreateUserResult,
      LocalUserId = CreateUserResult == Result.Success ? CreateUserUserId : null
    };
    completionDelegate(ref callbackInfo);
  }
}

public class MockSessionsInterface {
  // Add session interface methods as needed
}

public class MockUserInfoInterface {
  public Result CopyUserInfoResult { get; set; } = Result.Success;
  public UserInfoData? UserInfo { get; set; }
  public bool CopyUserInfoCalled { get; private set; }

  public Result CopyUserInfo(ref CopyUserInfoOptions options, out UserInfoData? outUserInfo) {
    CopyUserInfoCalled = true;
    outUserInfo = UserInfo;
    return CopyUserInfoResult;
  }
}