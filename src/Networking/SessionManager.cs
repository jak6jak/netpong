namespace NetworkedDodgeball.Networking;

using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.Sessions;
using Godot;

public partial class SessionManager : Node {
  public static SessionManager Instance { get; private set; } = null!;

  private string? _currentSessionId; // Server-side ID of the current session the player is in
  private string? _currentSessionNameClientKey; // Local name/key used by this client for the current session
  private bool _isSessionOwner; // Tracks if the local player is the owner of the current session
  private readonly List<SessionDetails> _lastSearchResults = new(); // Stores results from the last session search
  private readonly Dictionary<string, SessionDetails> _pendingInvitesDetails = new(); // Stores SessionDetails handles for received invites, keyed by InviteId
  private ulong _sessionInviteNotificationId; // ID for the session invite received notification
  private SessionsInterface? _sessionsInterface;
  private ProductUserId? _localPlayerProductID;



  [Signal]
  public delegate void SessionCreatedEventHandler(bool success, string sessionId, string errorMessage);
  [Signal]
  public delegate void SessionSearchFinishedEventHandler(bool success, Godot.Collections.Array<Godot.Collections.Dictionary> searchResults, string errorMessage); // Changed to pass serializable data
  [Signal]
  public delegate void SessionJoinedEventHandler(bool success, string sessionId, string errorMessage);
  [Signal]
  public delegate void SessionLeftEventHandler(bool success, string errorMessage);

  [Signal]
  public delegate void SessionInviteReceivedEventHandler(string inviteId, string fromUserIdString, string fromUserDisplayName, string sessionId); // Added display name and session ID
  [Signal]
  public delegate void SessionInviteAcceptedEventHandler(bool success, string sessionId, string errorMessage);
  [Signal]
  public delegate void SessionInviteRejectedEventHandler(bool success, string errorMessage);

  [Signal]
  public delegate void PlayerRegisteredEventHandler(bool success, string playerId, string errorMessage);
  [Signal]
  public delegate void PlayerUnregisteredEventHandler(bool success, string playerId, string errorMessage);


  public SessionManager() {
    Instance = this;
  }

  public void Initialize(SessionsInterface sessionsInterface, ProductUserId localPlayerProductID) {
    _sessionsInterface = sessionsInterface;
    _localPlayerProductID = localPlayerProductID;

    // Subscribe to session invite notifications
    AddNotifySessionInviteReceived();

    GD.Print($"Session Manager initialized with ProductUserId: {localPlayerProductID}");
  }

  public void CreateEOSSession(string clientSessionKey, uint maxPlayers, bool isPublic, string bucketId = "DefaultBucket:AnyRegion:AnyMap", string mapName = "DefaultMap") {
    if (_sessionsInterface is null || _localPlayerProductID is null) {
      GD.PushError("Sessions interface or local user ID not initialized for CreateEOSSession.");
      EmitSignal(SignalName.SessionCreated, false, "", "Not initialized.");
      return;
    }

    // Parameter validation
    if (string.IsNullOrEmpty(clientSessionKey)) {
      GD.PushError("ClientSessionKey cannot be null or empty.");
      EmitSignal(SignalName.SessionCreated, false, "", "Invalid clientSessionKey.");
      return;
    }
    if (maxPlayers <= 0 || maxPlayers > 1000) {
      GD.PushError($"MaxPlayers must be between 1-1000, got: {maxPlayers}");
      EmitSignal(SignalName.SessionCreated, false, "", "Invalid maxPlayers.");
      return;
    }
    if (string.IsNullOrEmpty(bucketId) || bucketId.Length > 1000) {
      GD.PushError("BucketId must be valid and not exceed 1000 characters.");
      EmitSignal(SignalName.SessionCreated, false, "", "Invalid bucketId.");
      return;
    }
    if (string.IsNullOrEmpty(mapName) || mapName.Length > 32) {
      GD.PushError("MapName must be valid and not exceed 32 characters for attribute key.");
      EmitSignal(SignalName.SessionCreated, false, "", "Invalid mapName.");
      return;
    }

    if (!string.IsNullOrEmpty(_currentSessionId) || !string.IsNullOrEmpty(_currentSessionNameClientKey)) {
      GD.PushWarning("Already in a session or a session operation is pending. Please leave the current session first.");
      EmitSignal(SignalName.SessionCreated, false, "", "Already in a session.");
      return;
    }
    _currentSessionNameClientKey = clientSessionKey;
    var createModOptions = new CreateSessionModificationOptions {
      SessionName = _currentSessionNameClientKey, // This is the local name, NOT the backend SessionId yet.
      LocalUserId = _localPlayerProductID,
      MaxPlayers = maxPlayers,
      BucketId = bucketId,
      // bSanctionsEnabled can be set here if you use EOS Sanctions service
    };


    Result createModResult = _sessionsInterface.CreateSessionModification(ref createModOptions, out SessionModification sessionModificationHandle);

    if (createModResult != Result.Success) {
      GD.PushError($"Failed to create session modification: {createModResult}");
      _currentSessionNameClientKey = null; // Clear if creation failed early
      EmitSignal(SignalName.SessionCreated, false, "", $"CreateMod failed: {createModResult}");
      return;
    }

    GD.Print($"Session modification handle created for session key: {_currentSessionNameClientKey}");
    // Set common session properties
    // Host address can be an IP or a P2P socket ID. For dedicated servers, it's the server IP.
    // For P2P, you might set this after P2P negotiation or use a placeholder if EOS P2P handles it.
    var setHostAddressOptions = new SessionModificationSetHostAddressOptions { HostAddress = "" };
    sessionModificationHandle.SetHostAddress(ref setHostAddressOptions);

    var setBucketIdOptions = new SessionModificationSetBucketIdOptions { BucketId = bucketId };
    sessionModificationHandle.SetBucketId(ref setBucketIdOptions);

    var setMaxPlayersOptions = new SessionModificationSetMaxPlayersOptions { MaxPlayers = maxPlayers };
    sessionModificationHandle.SetMaxPlayers(ref setMaxPlayersOptions);

    var setJoinInProgressOptions = new SessionModificationSetJoinInProgressAllowedOptions { AllowJoinInProgress = true };
    sessionModificationHandle.SetJoinInProgressAllowed(ref setJoinInProgressOptions);

    var permissionLevel = isPublic ? OnlineSessionPermissionLevel.PublicAdvertised : OnlineSessionPermissionLevel.InviteOnly;
    var setPermissionOptions = new SessionModificationSetPermissionLevelOptions { PermissionLevel = permissionLevel };
    sessionModificationHandle.SetPermissionLevel(ref setPermissionOptions);

    // Example of adding a custom attribute for the map name
    var mapAttributeData = new AttributeData {
      Key = "MAPNAME_S", // Suffix _S for String, _I for Int64, _B for Bool, _D for Double
      Value = new AttributeDataValue { AsUtf8 = mapName }
    };
    var addMapAttributeOptions = new SessionModificationAddAttributeOptions {
      SessionAttribute = mapAttributeData,
      AdvertisementType = SessionAttributeAdvertisementType.Advertise // Make it searchable
    };
    sessionModificationHandle.AddAttribute(ref addMapAttributeOptions);

    var updateSessionOptions = new UpdateSessionOptions { SessionModificationHandle = sessionModificationHandle };
    _sessionsInterface.UpdateSession(ref updateSessionOptions, null, OnUpdateSessionCompleted);
    GD.Print("UpdateSession (for creation) request sent.");
    // SessionModificationHandle is consumed by UpdateSession, no need to release it manually.
  }

  private void OnUpdateSessionCompleted(ref UpdateSessionCallbackInfo data) {
    // This callback is used for both creating a new session and modifying an existing one.
    if (data.ResultCode == Result.Success) {
      // If _currentSessionId was null, this was a creation.
      var wasCreation = string.IsNullOrEmpty(_currentSessionId);
      _currentSessionId = data.SessionId; // This is the actual backend Session ID

      if (wasCreation) {
        _isSessionOwner = true; // The creator is the owner
        GD.Print($"Session '{data.SessionName}' (ID: {data.SessionId}) CREATED successfully!");
        EmitSignal(SignalName.SessionCreated, true, data.SessionId.ToString(), "");
        // As owner, you might want to register yourself if your game logic requires it,
        // though EOS often handles the owner's presence implicitly for some features.
        // RegisterPlayerInSession(_localPlayerProductID);
      }
      else {
        GD.Print($"Session '{data.SessionName}' (ID: {data.SessionId}) UPDATED successfully!");
        // Emit a different signal or handle update confirmation if needed
      }
    }
    else {
      GD.PushError($"Failed to update/create session '{data.SessionName}': {data.ResultCode}");
      if (string.IsNullOrEmpty(_currentSessionId)) // If it was a creation attempt that failed
      {
        _currentSessionNameClientKey = null; // Clear client key if creation failed
        _isSessionOwner = false;
        EmitSignal(SignalName.SessionCreated, false, "", $"UpdateSession (create) failed: {data.ResultCode}");
      }
      else {
        // Handle update failure if needed
        GD.PushError($"Failed to UPDATE existing session '{_currentSessionNameClientKey}': {data.ResultCode}");
      }
    }
  }

  public void FindEOSSessions(string? mapNameFilter = null, int maxResults = 20) {
    if (_sessionsInterface is null || _localPlayerProductID is null) {
      GD.PushError("Sessions interface or local user ID not initialized for FindEOSSessions.");
      EmitSignal(SignalName.SessionSearchFinished, false, new Godot.Collections.Array<Godot.Collections.Dictionary>(), "Not initialized.");
      return;
    }

    var createSearchOptions = new CreateSessionSearchOptions { MaxSearchResults = (uint)maxResults };
    Result createSearchResult = _sessionsInterface.CreateSessionSearch(ref createSearchOptions, out SessionSearch searchHandle);

    if (createSearchResult != Result.Success) {
      GD.PushError($"Failed to create session search handle: {createSearchResult}");
      EmitSignal(SignalName.SessionSearchFinished, false, new Godot.Collections.Array<Godot.Collections.Dictionary>(), $"CreateSearch failed: {createSearchResult}");
      return;
    }

    // Example: Set a search parameter for MAPNAME_S if provided
    if (!string.IsNullOrEmpty(mapNameFilter)) {
      var parameter = new AttributeData {
        Key = "MAPNAME_S", // Match the key used during creation
        Value = new AttributeDataValue { AsUtf8 = mapNameFilter }
      };
      var setSearchParamOptions = new SessionSearchSetParameterOptions {
        Parameter = parameter,
        ComparisonOp = ComparisonOp.Equal
      };
      searchHandle.SetParameter(ref setSearchParamOptions);
    }

    // Example: Search for sessions with at least 1 open slot (not full)
    var slotsAvailableParam = new AttributeData {
      Key = SessionsInterface.SearchMinslotsavailable, // Predefined search key
      Value = new AttributeDataValue { AsInt64 = 1 }
    };
    var setSlotsParamOptions = new SessionSearchSetParameterOptions {
      Parameter = slotsAvailableParam,
      ComparisonOp = ComparisonOp.Greaterthanorequal
    };
    searchHandle.SetParameter(ref setSlotsParamOptions);

    var findOptions = new SessionSearchFindOptions { LocalUserId = _localPlayerProductID };
    // Pass searchHandle as client data so we can release it in the callback
    searchHandle.Find(ref findOptions, searchHandle, OnFindSessionsCompleted);
    GD.Print("Session search request sent.");
  }

  private void OnFindSessionsCompleted(ref SessionSearchFindCallbackInfo data) {
    // Clear previous native search results before processing new ones
    foreach (var detail in _lastSearchResults) { detail?.Release(); }
    _lastSearchResults.Clear();

    var resultsForSignal = new Godot.Collections.Array<Godot.Collections.Dictionary>();

    SessionSearch searchHandle = data.ClientData as SessionSearch; // Retrieve the handle passed as client data
    if (searchHandle == null) {
      GD.PushError("Search handle was null in OnFindSessionsCompleted callback.");
      EmitSignal(SignalName.SessionSearchFinished, false, resultsForSignal, "Search handle null in callback.");
      return; // Cannot proceed without the handle
    }

    if (data.ResultCode == Result.Success) {
      GD.Print("Session search successful!");
      var resultCountOptions = new SessionSearchGetSearchResultCountOptions(); // API Version is implicitly set
      uint numResults = searchHandle.GetSearchResultCount(ref resultCountOptions);
      GD.Print($"Found {numResults} sessions.");
      for (uint i = 0; i < numResults; i++) {
        var copyResultOptions = new SessionSearchCopySearchResultByIndexOptions { SessionIndex = i };
        Result copyResult = searchHandle.CopySearchResultByIndex(ref copyResultOptions, out SessionDetails sessionDetailsHandle); // This is the EOS_HSessionDetails [cite: 288]

        if (copyResult == Result.Success && sessionDetailsHandle != null) {
          _lastSearchResults.Add(sessionDetailsHandle); // Store the native handle for potential direct use (e.g., Join) and later release [cite: 43, 290]

          var sessionDict = new Godot.Collections.Dictionary();
          // Initialize with defaults
          sessionDict["SessionId"] = "N/A";
          sessionDict["OwnerUserId"] = "N/A"; // Default if not found by other means
          sessionDict["NumOpenPublicConnections"] = 0U;
          sessionDict["MaxPlayers"] = 0U;
          sessionDict["MapName"] = "N/A";

          // Call CopyInfo on the sessionDetailsHandle to get SessionDetailsInfo [cite: 187]
          var copyInfoOptions = new SessionDetailsCopyInfoOptions { };
          Result infoResult = sessionDetailsHandle.CopyInfo(ref copyInfoOptions, out SessionDetailsInfo? sessionInfo);

          if (infoResult == Result.Success && sessionInfo.HasValue) {
            // Access SessionId and NumOpenPublicConnections from SessionDetailsInfo [cite: 185]
            sessionDict["SessionId"] = sessionInfo.Value.SessionId?.ToString() ?? "N/A";
            sessionDict["NumOpenPublicConnections"] = sessionInfo.Value.NumOpenPublicConnections;

            if (sessionInfo.Value.Settings.HasValue) {
              sessionDict["MaxPlayers"] = sessionInfo.Value.Settings.Value.NumPublicConnections;

              // Retrieve custom attributes like MAPNAME_S from sessionDetailsHandle
              var getAttrCountOptions = new SessionDetailsGetSessionAttributeCountOptions { };
              uint attributesCount = sessionDetailsHandle.GetSessionAttributeCount(ref getAttrCountOptions);

              for (uint attrIdx = 0; attrIdx < attributesCount; attrIdx++) {
                var copyAttrOptions = new SessionDetailsCopySessionAttributeByIndexOptions { AttrIndex = attrIdx };
                Result attrCopyResult = sessionDetailsHandle.CopySessionAttributeByIndex(ref copyAttrOptions, out SessionDetailsAttribute? sessionAttribute);

                if (attrCopyResult == Result.Success && sessionAttribute.HasValue &&
                    sessionAttribute.Value.Data.HasValue && sessionAttribute.Value.Data.Value.Key == "MAPNAME_S") {
                  sessionDict["MapName"] = sessionAttribute.Value.Data.Value.Value.AsUtf8.ToString() ?? "N/A";
                  // Note: SessionAttribute and its members are typically structs or managed types in C# SDK,
                  // so explicit release here is usually not needed for the copied attribute data itself.
                  break;
                }
              }
            }
            //sessionInfo.Value.Release(); // IMPORTANT: Release the SessionDetailsInfo data when no longer needed [cite: 190]
          }
          else {
            GD.PushWarning($"OnFindSessionsCompleted: Failed to copy session info for search result index {i}: {infoResult}");
          }

          resultsForSignal.Add(sessionDict);
          GD.Print($"  Session {i}: ID={sessionDict["SessionId"]}, Owner={sessionDict["OwnerUserId"]}, OpenSlots={sessionDict["NumOpenPublicConnections"]}, MaxPlayers={sessionDict["MaxPlayers"]}, Map={sessionDict["MapName"]}");
        }
        else {
          GD.PushWarning($"Failed to copy search result at index {i}: {copyResult}");
          sessionDetailsHandle?.Release(); // If copy failed but a handle was returned, release it.
        }
      }

      EmitSignal(SignalName.SessionSearchFinished, true, resultsForSignal, "");
    }
    else {
      GD.PushError($"Session search failed: {data.ResultCode}");
      EmitSignal(SignalName.SessionSearchFinished, false, resultsForSignal, $"Search failed: {data.ResultCode}");
    }

    searchHandle.Release(); // Release the search handle itself
  }

  public SessionDetails? GetSessionDetailsFromLastSearch(int index) {
    if (index >= 0 && index < _lastSearchResults.Count) {
      return _lastSearchResults[index]; // Be careful with the lifetime of this handle if stored elsewhere
    }
    return null;
  }


  public void JoinEOSSession(SessionDetails? sessionToJoin, string clientSessionKey) {
    if (sessionToJoin is null) {
      GD.PushError("SessionDetails to join is null.");
      EmitSignal(SignalName.SessionJoined, false, "", "SessionDetails null.");
      return;
    }
    if (_sessionsInterface is null || _localPlayerProductID is null) {
      GD.PushError("Sessions interface or local user ID not initialized for JoinEOSSession.");
      EmitSignal(SignalName.SessionJoined, false, "", "Not initialized.");
      return;
    }
    if (!string.IsNullOrEmpty(_currentSessionId) || !string.IsNullOrEmpty(_currentSessionNameClientKey)) {
      GD.PushWarning("Already in a session or a session operation is pending. Please leave the current session first.");
      EmitSignal(SignalName.SessionJoined, false, "", "Already in a session.");
      return;
    }

    _currentSessionNameClientKey = clientSessionKey; // Client-side key for the session being joined

    var joinOptions = new JoinSessionOptions {
      SessionName = _currentSessionNameClientKey, // The local name this client will use for this session
      SessionHandle = sessionToJoin, // This is the SessionDetails obtained from search or invite
      LocalUserId = _localPlayerProductID,
      PresenceEnabled = true // Set if this session should be associated with user's presence
    };

    _sessionsInterface.JoinSession(ref joinOptions, null, OnJoinSessionCompleted);
    //GD.Print($"Attempting to join session: {sessionToJoin.SessionId} with local key '{clientSessionKey}'");
    // The SessionDetails handle (sessionToJoin) is typically "consumed" or copied by JoinSession.
    // The original handle from search results remains in _lastSearchResults and is managed there.
    // If this `sessionToJoin` came from an invite, its specific handling for release is in AcceptSessionInvite.
  }
  private void OnJoinSessionCompleted(ref JoinSessionCallbackInfo data) {
    if (data.ResultCode == Result.Success) {
      // Note: JoinSessionCallbackInfo doesn't contain SessionId or SessionName in this EOS SDK version
      _isSessionOwner = false; // When joining, you are not the owner by default
      GD.Print($"Successfully joined session with local key '{_currentSessionNameClientKey}'!");
      EmitSignal(SignalName.SessionJoined, true, _currentSessionNameClientKey ?? "", "");
      // An active session is now formed on this client's machine.
    }
    else {
      GD.PushError($"Failed to join session with local key '{_currentSessionNameClientKey}': {data.ResultCode}");
      _currentSessionNameClientKey = null; // Clear local key if join failed
      _currentSessionId = null;
      EmitSignal(SignalName.SessionJoined, false, "", $"Join failed: {data.ResultCode}");
    }
  }

  public void LeaveCurrentEOSSession() {
    if (_sessionsInterface == null || string.IsNullOrEmpty(_currentSessionNameClientKey)) {
      GD.PushWarning("Not in a session or sessions interface not ready to leave session.");
      EmitSignal(SignalName.SessionLeft, false, "Not in a session or not initialized.");
      return;
    }

    var destroyOptions = new DestroySessionOptions {
      SessionName = _currentSessionNameClientKey, // Use the client-side key of the session to destroy/leave
    };

    GD.Print($"Attempting to destroy/leave session with client key: {_currentSessionNameClientKey}");
    // Pass client key as client data for context in callback
    _sessionsInterface.DestroySession(ref destroyOptions, _currentSessionNameClientKey, OnDestroySessionCompleted);
  }

  private void OnDestroySessionCompleted(ref DestroySessionCallbackInfo data) {
    string clientKeyInCallback = data.ClientData as string;
    if (data.ResultCode == Result.Success) {
      GD.Print($"Successfully left/destroyed session (Client Key: {clientKeyInCallback})!");
      EmitSignal(SignalName.SessionLeft, true, "");
    }
    else {
      GD.PushError($"Failed to destroy session (Client Key: {clientKeyInCallback}): {data.ResultCode}");
      EmitSignal(SignalName.SessionLeft, false, $"Destroy failed: {data.ResultCode}");
    }

    // Always clear current session info on client after attempting to leave/destroy,
    // but only if the callback matches the session we thought we were in.
    if (clientKeyInCallback == _currentSessionNameClientKey) {
      _currentSessionId = null;
      _currentSessionNameClientKey = null;
      _isSessionOwner = false;
    }
    else if (!string.IsNullOrEmpty(clientKeyInCallback)) {
      GD.PushWarning($"DestroySession callback for '{clientKeyInCallback}', but current client key is '{_currentSessionNameClientKey}'. State might be inconsistent.");
    }
    // _lastSearchResults.Clear(); // Optionally clear search results as they might be stale.
  }

  // --- Player Registration (Typically Session Owner Only) ---
  public void RegisterPlayerInSession(ProductUserId? playerToRegister) {
    if (!_isSessionOwner || string.IsNullOrEmpty(_currentSessionNameClientKey) || _sessionsInterface == null) {
      GD.PushWarning("Cannot register player: Not session owner or not in a session.");
      EmitSignal(SignalName.PlayerRegistered, false, playerToRegister?.ToString() ?? "", "Not owner or not in session.");
      return;
    }

    if (playerToRegister == null) {
      GD.PushError("Cannot register null player.");
      EmitSignal(SignalName.PlayerRegistered, false, "", "Player is null.");
      return;
    }

    var registerOptions = new RegisterPlayersOptions {
      SessionName = _currentSessionNameClientKey,
      PlayersToRegister = new ProductUserId[] { playerToRegister }
    };
    _sessionsInterface.RegisterPlayers(ref registerOptions, playerToRegister, OnRegisterPlayersCompleted);
    GD.Print($"Attempting to register player {playerToRegister} in session {_currentSessionNameClientKey}");
  }

  private void OnRegisterPlayersCompleted(ref RegisterPlayersCallbackInfo data) {
    ProductUserId registeredPlayerId = data.ClientData as ProductUserId; // Get player from client data
    string playerIdStr = registeredPlayerId?.ToString() ?? "Unknown";

    if (data.ResultCode == Result.Success) {
      bool actuallyRegistered = data.RegisteredPlayers != null && Array.Exists(data.RegisteredPlayers, puid => puid == registeredPlayerId);
      bool wasSanctioned = data.SanctionedPlayers != null && Array.Exists(data.SanctionedPlayers, puid => puid == registeredPlayerId);

      if (actuallyRegistered) {
        GD.Print($"Player {playerIdStr} successfully registered in session.");
        EmitSignal(SignalName.PlayerRegistered, true, playerIdStr, "");
      }
      else if (wasSanctioned) {
        GD.PushWarning($"Player {playerIdStr} failed to register in session due to sanction.");
        EmitSignal(SignalName.PlayerRegistered, false, playerIdStr, "Player sanctioned.");
      }
      else {
        // This case might occur if the array is empty or the player isn't in either list, despite overall success.
        GD.PushWarning($"Player {playerIdStr} registration status unclear. ResultCode: {data.ResultCode}, Not in Registered or Sanctioned lists.");
        EmitSignal(SignalName.PlayerRegistered, false, playerIdStr, "Registration status unclear despite overall success.");
      }
    }
    else {
      GD.PushError($"RPC to register player {playerIdStr} failed: {data.ResultCode}");
      EmitSignal(SignalName.PlayerRegistered, false, playerIdStr, $"Registration RPC failed: {data.ResultCode}");
    }
  }

  public void UnregisterPlayerFromSession(ProductUserId? playerToUnregister) {
    if (!_isSessionOwner || string.IsNullOrEmpty(_currentSessionNameClientKey) || _sessionsInterface == null) {
      GD.PushWarning("Cannot unregister player: Not session owner or not in a session.");
      EmitSignal(SignalName.PlayerUnregistered, false, playerToUnregister?.ToString() ?? "", "Not owner or not in session.");
      return;
    }
    if (playerToUnregister == null) {
      GD.PushError("Cannot unregister null player.");
      EmitSignal(SignalName.PlayerUnregistered, false, "", "Player is null.");
      return;
    }

    var unregisterOptions = new UnregisterPlayersOptions {
      SessionName = _currentSessionNameClientKey,
      PlayersToUnregister = new ProductUserId[] { playerToUnregister }
    };
    _sessionsInterface.UnregisterPlayers(ref unregisterOptions, playerToUnregister, OnUnregisterPlayersCompleted);
    GD.Print($"Attempting to unregister player {playerToUnregister} from session {_currentSessionNameClientKey}");
  }

  private void OnUnregisterPlayersCompleted(ref UnregisterPlayersCallbackInfo data) {
    ProductUserId unregisteredPlayerId = data.ClientData as ProductUserId;
    string playerIdStr = unregisteredPlayerId?.ToString() ?? "Unknown";
    if (data.ResultCode == Result.Success) {
      GD.Print($"Player {playerIdStr} successfully unregistered from session.");
      EmitSignal(SignalName.PlayerUnregistered, true, playerIdStr, "");
    }
    else {
      GD.PushError($"Failed to unregister player {playerIdStr} from session: {data.ResultCode}");
      EmitSignal(SignalName.PlayerUnregistered, false, playerIdStr, $"Unregistration RPC failed: {data.ResultCode}");
    }
  }

  // --- Session Invites ---
  private void AddNotifySessionInviteReceived() {
    if (_sessionsInterface == null)
      return;
    var options = new AddNotifySessionInviteReceivedOptions(); // API Version is implicit
    _sessionInviteNotificationId = _sessionsInterface.AddNotifySessionInviteReceived(ref options, null, OnSessionInviteReceived);
    if (_sessionInviteNotificationId != 0) {
      GD.Print("Successfully subscribed to session invite notifications.");
    }
    else {
      GD.PushError("Failed to subscribe to session invite notifications.");
    }
  }

  private void OnSessionInviteReceived(ref SessionInviteReceivedCallbackInfo data) {
    GD.Print($"Session invite received! Invite ID: {data.InviteId}, For PUID (Local User): {data.TargetUserId}");

    var copyHandleOptions = new CopySessionHandleByInviteIdOptions { InviteId = data.InviteId };
    Result copyRes = _sessionsInterface.CopySessionHandleByInviteId(ref copyHandleOptions, out SessionDetails sessionDetailsHandle);

    string inviterDisplayName = "Unknown";
    string sessionActualId = "Unknown";

    if (copyRes == Result.Success && sessionDetailsHandle != null) {
      // Note: SessionDetails doesn't expose SessionId/SessionOwner properties directly
      GD.Print($" Invite details retrieved successfully");

      inviterDisplayName = "Unknown";

      // Store the handle for potential accept/reject, or pass to UI
      // Ensure no duplicate storage and release old if any
      if (_pendingInvitesDetails.TryGetValue(data.InviteId, out SessionDetails oldHandle)) {
        oldHandle?.Release(); // Release previous handle for this invite ID if it existed
      }
      _pendingInvitesDetails[data.InviteId] = sessionDetailsHandle; // Store the new handle
    }
    else {
      GD.PushWarning($"Failed to get session details from invite {data.InviteId}: {copyRes}");
      sessionDetailsHandle?.Release(); // Release if not stored
    }
    EmitSignal(SignalName.SessionInviteReceived, data.InviteId.ToString(), "N/A", inviterDisplayName, sessionActualId);
  }

  public void AcceptSessionInvite(string inviteId, string clientSessionKeyToUse) {
    if (!_pendingInvitesDetails.TryGetValue(inviteId, out SessionDetails sessionDetailsToJoin) || sessionDetailsToJoin == null) {
      GD.PushError($"No valid pending invite found with ID: {inviteId}. Cannot accept.");
      EmitSignal(SignalName.SessionInviteAccepted, false, "", "Invite not found or invalid.");
      return;
    }
    if (string.IsNullOrEmpty(clientSessionKeyToUse)) {
      GD.PushError($"Client session key cannot be empty when accepting invite {inviteId}.");
      EmitSignal(SignalName.SessionInviteAccepted, false, "", "Client session key empty.");
      // Do not release sessionDetailsToJoin here, it's still in _pendingInvitesDetails
      return;
    }

    if (!string.IsNullOrEmpty(_currentSessionId) || !string.IsNullOrEmpty(_currentSessionNameClientKey)) {
      GD.PushWarning("Cannot accept invite: Already in a session. Please leave the current session first.");
      EmitSignal(SignalName.SessionInviteAccepted, false, "", "Already in a session.");
      // Do not release sessionDetailsToJoin here, it's still in _pendingInvitesDetails
      return;
    }

    GD.Print($"Attempting to accept invite {inviteId} with local key {clientSessionKeyToUse}.");
    _currentSessionNameClientKey = clientSessionKeyToUse; // Set before JoinSession call

    var joinOptions = new JoinSessionOptions {
      SessionName = clientSessionKeyToUse,
      SessionHandle = sessionDetailsToJoin, // Use the handle obtained from CopySessionHandleByInviteId
      LocalUserId = _localPlayerProductID,
      PresenceEnabled = true
    };
    // Pass inviteId as client data to handle releasing the original SessionDetails handle from _pendingInvitesDetails in the callback
    _sessionsInterface.JoinSession(ref joinOptions, inviteId, OnInviteJoinSessionCompleted);
  }

  private void OnInviteJoinSessionCompleted(ref JoinSessionCallbackInfo data) {
    string inviteId = data.ClientData as string; // Retrieve the inviteId

    if (data.ResultCode == Result.Success) {
      _currentSessionId = _currentSessionNameClientKey; // Use client key as session ID for tracking
      _isSessionOwner = false; // Joined via invite, so not the owner
      GD.Print($"Successfully joined session from invite '{inviteId}'!");
      EmitSignal(SignalName.SessionInviteAccepted, true, _currentSessionId ?? "", "");
    }
    else {
      GD.PushError($"Failed to join session from invite '{inviteId}': {data.ResultCode}");
      _currentSessionNameClientKey = null; // Clear local key if join failed
      _currentSessionId = null;
      EmitSignal(SignalName.SessionInviteAccepted, false, "", $"Join from invite failed: {data.ResultCode}");
    }

    // Clean up the SessionDetails handle associated with this invite, whether join succeeded or failed
    if (!string.IsNullOrEmpty(inviteId) && _pendingInvitesDetails.TryGetValue(inviteId, out SessionDetails detailsHandle)) {
      detailsHandle?.Release();
      _pendingInvitesDetails.Remove(inviteId);
      GD.Print($"Released and removed SessionDetails handle for invite {inviteId}.");
    }
  }

  public void RejectSessionInvite(string inviteId) {
    if (_sessionsInterface == null || _localPlayerProductID == null) {
      GD.PushError("Cannot reject invite: Sessions interface or local user ID not initialized.");
      EmitSignal(SignalName.SessionInviteRejected, false, "Not initialized.");
      return;
    }

    var rejectOptions = new RejectInviteOptions {
      InviteId = inviteId,
      LocalUserId = _localPlayerProductID
    };
    // Pass inviteId as client data to handle releasing the SessionDetails handle
    _sessionsInterface.RejectInvite(ref rejectOptions, inviteId, OnRejectInviteCompleted);
    GD.Print($"Attempting to reject invite: {inviteId}");
  }

  private void OnRejectInviteCompleted(ref RejectInviteCallbackInfo data) {
    string inviteId = data.ClientData as string; // Retrieve the inviteId
    if (data.ResultCode == Result.Success) {
      GD.Print($"Successfully rejected invite '{inviteId}'.");
      EmitSignal(SignalName.SessionInviteRejected, true, "");
    }
    else {
      GD.PushError($"Failed to reject invite '{inviteId}': {data.ResultCode}");
      EmitSignal(SignalName.SessionInviteRejected, false, $"Reject failed: {data.ResultCode}");
    }

    // Clean up the SessionDetails handle associated with this invite, whether rejection succeeded or failed
    if (!string.IsNullOrEmpty(inviteId) && _pendingInvitesDetails.TryGetValue(inviteId, out SessionDetails detailsHandle)) {
      detailsHandle?.Release();
      _pendingInvitesDetails.Remove(inviteId);
      GD.Print($"Released and removed SessionDetails handle for invite {inviteId} after rejection attempt.");
    }
  }

  public void QueryPendingInvites() {
    if (_sessionsInterface == null || _localPlayerProductID == null)
      return;

    var queryInvitesOptions = new QueryInvitesOptions { LocalUserId = _localPlayerProductID };
    _sessionsInterface.QueryInvites(ref queryInvitesOptions, null, OnQueryInvitesComplete);
    GD.Print("Querying pending invites...");
  }

  private void OnQueryInvitesComplete(ref QueryInvitesCallbackInfo data) {
    if (data.ResultCode == Result.Success) {
      GD.Print("QueryInvites successful. Cached pending invites.");
      var countOptions = new GetInviteCountOptions { LocalUserId = _localPlayerProductID };
      uint inviteCount = _sessionsInterface.GetInviteCount(ref countOptions);
      GD.Print($"Found {inviteCount} pending invites in cache.");

      for (uint i = 0; i < inviteCount; i++) {
        var idOptions = new GetInviteIdByIndexOptions { LocalUserId = _localPlayerProductID, Index = i };
        Result getIdRes = _sessionsInterface.GetInviteIdByIndex(ref idOptions, out Utf8String inviteIdObj);
        if (getIdRes == Result.Success && inviteIdObj != null) {
          string inviteId = inviteIdObj; // Implicit conversion from Utf8String
          GD.Print($" Processing cached Invite ID: {inviteId}");
          // To avoid re-processing if AddNotifySessionInviteReceived already handled it, check _pendingInvitesDetails
          if (!_pendingInvitesDetails.ContainsKey(inviteId)) {
            // This invite wasn't caught by the live notification, so process it now.
            // This will fetch details and emit SessionInviteReceived.
            // Manually trigger a similar flow to OnSessionInviteReceived's core logic.
            var fakeCallbackInfo = new SessionInviteReceivedCallbackInfo {
              InviteId = inviteId,
              TargetUserId = _localPlayerProductID // The invite is for us
            };
            OnSessionInviteReceived(ref fakeCallbackInfo); // This will fetch details and emit
          }
          else {
            GD.Print($" Invite {inviteId} already processed by live notification or previous query.");
          }
        }
      }
    }
    else {
      GD.PushError($"QueryInvites failed: {data.ResultCode}");
    }
  }
}

