namespace NetworkedDodgeball;

using System.Threading.Tasks;
using Godot;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Drivers;
using Shouldly;

public class GameTest : TestClass {
  private Game _game = default!;
  private Fixture _fixture = default!;

  public GameTest(Node testScene) : base(testScene) { }

  [SetupAll]
  public async Task Setup() {
    _fixture = new Fixture(TestScene.GetTree());
    _game = await _fixture.LoadAndAddScene<Game>();
  }

  [CleanupAll]
  public void Cleanup() => _fixture.Cleanup();

  [Test]
  public void TestButtonUpdatesCounter() {
    // Test the button press functionality directly
    _game.ButtonPresses.ShouldBe(0); // Initial state
    
    // Simulate button press by emitting the signal
    _game.CreateGameButton.EmitSignal(Button.SignalName.Pressed);
    
    _game.ButtonPresses.ShouldBe(1);
  }
}
