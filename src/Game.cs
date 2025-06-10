namespace NetworkedDodgeball;

using Godot;
using Godot.Collections;
using NetworkedDodgeball.Networking;
public partial class Game : Control {
  public Button CreateGameButton { get; private set; } = default!;
  public Button JoinButton { get; private set; } = default!;
  public int ButtonPresses { get; private set; }
  public ItemList PlayerList { get; private set; } = default!;
  private Godot.Collections.Array<Godot.Collections.Dictionary> _lastSearchResults = new();
  //private Lobby _lobby;
  public override void _Ready() {
    CreateGameButton = GetNode<Button>("%CreateGame");
    JoinButton = GetNode<Button>("%JoinGame");
    PlayerList = GetNode<ItemList>("%PlayerList");

    NetworkManager.Instance.AuthenticationFinished += OnAuthFinished;

    // Connect ItemSelected signal directly
    PlayerList.ItemSelected += OnSessionSelected;

    // Connect button press to increment counter for tests
    CreateGameButton.Pressed += OnCreateGameButtonPressed;
  }

  private void OnCreateGameButtonPressed() => ButtonPresses++;
  private void OnAuthFinished(bool success, string localUserId, string errorMessage) {
    if (!success) {
      GD.PrintErr($"Authentication failed for user: {localUserId}");
      return;
    }
    var sessionManager = SessionManager.Instance;
    if (sessionManager != null) {
      sessionManager.SessionCreated += OnSessionCreated;
      sessionManager.SessionJoined += OnSessionJoined;
      sessionManager.SessionSearchFinished += OnSessionSearchFinished;
      GD.Print("Connected to Session Manager signals");
      JoinButton.Pressed += OnSearchEOSSessionsPressed;
      CreateGameButton.Pressed += OnCreateEOSSessionPressed;

    }

  }
  private void OnSessionCreated(bool success, string sessionId, string errorMessage) {
    if (success) {
      GD.Print($"EOS Session created successfully: {sessionId}");
    }
    else {
      GD.PushError($"EOS Session creation failed: {errorMessage}");
    }
  }

  private void OnSessionJoined(bool success, string sessionId, string errorMessage) {
    if (success) {
      GD.Print($"EOS Session joined successfully: {sessionId}");
    }
    else {
      GD.PushError($"EOS Session join failed: {errorMessage}");
    }
  }

  private void OnSessionSearchFinished(bool success, Godot.Collections.Array<Godot.Collections.Dictionary> searchResults, string errorMessage) {
    if (success) {
      GD.Print($"EOS Session search completed. Found {searchResults.Count} sessions:");

      // Store search results for joining
      _lastSearchResults = searchResults;

      // Clear the player list and repurpose it as session list
      PlayerList.Clear();

      for (var i = 0; i < searchResults.Count; i++) {
        var result = searchResults[i];
        var sessionDisplay = $"Session: {result["SessionId"]} ({result["NumOpenPublicConnections"]}/{result["MaxPlayers"]}) - {result["MapName"]}";
        PlayerList.AddItem(sessionDisplay);
        GD.Print($"  - Session ID: {result["SessionId"]}, Players: {result["NumOpenPublicConnections"]}/{result["MaxPlayers"]}, Map: {result["MapName"]}");
      }

    }
    else {
      GD.PushError($"EOS Session search failed: {errorMessage}");
    }
  }

  private void OnSessionSelected(long index) {
    if (index >= 0 && index < _lastSearchResults.Count) {
      var sessionManager = SessionManager.Instance;
      if (sessionManager != null) {
        // Get the SessionDetails for the selected session
        var sessionDetails = sessionManager.GetSessionDetailsFromLastSearch((int)index);
        if (sessionDetails is not null) {
          // Join the selected session using a simple local session name
          var clientSessionKey = "JoinedSession";
          sessionManager.JoinEOSSession(sessionDetails, clientSessionKey);
          GD.Print($"Attempting to join session at index {index}...");
        }
        else {
          GD.PushError("Session details not available for selected session");
        }
      }
    }
  }

  private void OnServerDisconnected() => throw new System.NotImplementedException();

  // Test methods for Session Manager
  public void OnCreateEOSSessionPressed() {
    var sessionManager = SessionManager.Instance;
    if (sessionManager != null) {
      sessionManager.CreateEOSSession("TestSession", 4, true, "DefaultBucket:AnyRegion:AnyMap", "TestMap");
      GD.Print("Creating EOS Session...");
    }
    else {
      GD.PushError("Session Manager not available");
    }
  }

  public void OnSearchEOSSessionsPressed() {
    var sessionManager = SessionManager.Instance;
    if (sessionManager != null) {
      sessionManager.FindEOSSessions("TestMap", 10);
      GD.Print("Searching for EOS Sessions...");
    }
    else {
      GD.PushError("Session Manager not available");
    }
  }
}
