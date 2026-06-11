using Godot;

public partial class Mob : CharacterBody3D
{
	[Export]
	public int MinSpeed {get; set;} = 10;
	
	[Export]
	public int MaxSpeed {get; set;} = 15;

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		MoveAndSlide();
	}
	public void Initialize(Vector3 mobPosition, Vector3 playerPosition)
	{
		LookAtFromPosition(mobPosition,playerPosition, Vector3.Up);
		var speed = GD.RandRange(MinSpeed, MaxSpeed);
		Velocity = Vector3.Forward * speed;
		Velocity = Velocity.Rotated(Vector3.Up, Rotation.Y);
	}
	// We also specified this function name in PascalCase in the editor's connection window.
	private void OnVisibilityNotifierScreenExited()
	{
	 QueueFree();
	}
}
