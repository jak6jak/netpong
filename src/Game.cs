namespace NetworkedDodgeball;

using Godot;
using Godot.Collections;

public partial class Game : Control {
  public Button CreateGameButton { get; private set; } = default!;
  public Button JoinButton { get; private set; } = default!;
  public int ButtonPresses { get; private set; }
  public ItemList PlayerList { get; private set; } = default!;
  private Lobby _lobby;
  public override void _Ready() {
    CreateGameButton = GetNode<Button>("%CreateGame");
    JoinButton = GetNode<Button>("%JoinGame");
    PlayerList = GetNode<ItemList>("%PlayerList");
    _lobby = Lobby.Instance;

    _lobby.PlayerConnected += OnPlayerConnected;
    _lobby.PlayerDisconnected += OnPlayerDisconnected;
    _lobby.ServerDisconnected += OnServerDisconnected;

  }

  private void OnServerDisconnected() {
    throw new System.NotImplementedException();
  }

  private void OnPlayerDisconnected(int peerid) {
    GD.Print(peerid + " was disconnected");
    UpdatePlayerList();
  }

  private void OnPlayerConnected(int peerid, Dictionary<string, string> playerinfo) => UpdatePlayerList();


  private void UpdatePlayerList() {
    PlayerList.Clear();

    // Add all connected players to the list
    foreach (var playerEntry in _lobby._players)
    {
      long playerId = playerEntry.Key;
      var playerInfo = playerEntry.Value;
      string playerName = playerInfo["Name"];

      // You can format the display text however you want
      string displayText = playerName;

      // Add additional info if needed (e.g., show which one is the host)
      if (playerId == 1)
      {
        displayText += " (Host)";
      }


      // Add the player to the list
      // You can use a different icon for each player if you want
      int index = PlayerList.AddItem(displayText);
      if (playerId == Multiplayer.MultiplayerPeer.GetUniqueId()) {
        PlayerList.SetItemCustomFgColor(index, new Color(0, .5f, 1));
      }
    }
  }
  public void OnCreateGamePressed() {


    Error error = _lobby.CreateGame();
    if (error != Error.Ok) {
      GD.PushError("Failed to create game");
    }
    else {
      string playerName = "Jacob" + Multiplayer.MultiplayerPeer.GetUniqueId();
      _lobby._playerInfo["Name"] = playerName;
    }
  }

  public void OnJoinGamePressed() {


    Error error = _lobby.JoinGame("127.0.0.1");
    if (error != Error.Ok) {
      GD.PushError("Failed to Join game");
    }
    else {
      string playerName = "Jacob" + Multiplayer.MultiplayerPeer.GetUniqueId();
      _lobby._playerInfo["Name"] = playerName;
    }

  }
}
