using System;
using Godot;

namespace NetworkedDodgeball.Networking;

public partial class ListPeopleInSession : VBoxContainer {
  private ItemList PlayerList;

  public override void _Ready() {
    // This method is called when the node is added to the scene.
    // You can initialize your UI elements or connect signals here.
    GD.Print("ListPeopleInSession is ready.");
    PlayerList = GetNode<ItemList>("List");
    SessionManager.Instance.SessionJoined += OnSessionJoined;
    //SessionManager.Instance.SessionCreated += OnSessionJoined;
  }
  private void OnSessionJoined(bool success, string sessionId, string errorMessage) {
    var players = SessionManager.Instance.GetPlayersInSession(sessionId);
    foreach (var player in players) {
      PlayerList.AddItem(player.ToString());
    }
  }


  public override void _ExitTree() {
    // This method is called when the node is removed from the scene.
    // Disconnect signals or clean up resources here.
    GD.Print("ListPeopleInSession is exiting.");
    //SessionManager.Instance.SessionJoined -= OnSessionJoined;
  }
  // private void OnSessionJoined(bool success, string sessionId, string errorMessage) {
  //}

}
