using System;
using Godot;
using NetworkedDodgeball.Networking;

public partial class HelloLabel : Label {

  public override void _Ready() =>
      NetworkManager.Instance.AuthenticationFinished += OnAuthFinished;

  private void OnAuthFinished(bool success, string localUserId, string errorMessage) {
    if (success) {
      Text = $"Hello {localUserId}";
    }
  }


}
