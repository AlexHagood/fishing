using Godot;
using System.Collections.Generic;

public partial class Car : VehicleBody3D
{
	[Export] public float MaxEngineForce = 1800f; // Force applied when accelerating
	[Export] public float MaxBrakeForce = 120f; // Brake torque
	[Export] public float MaxSteerAngleDeg = 30f; // Degrees

	private List<VehicleWheel3D> _wheels = new List<VehicleWheel3D>();

	public override void _Ready()
	{
		// Gather Wheel nodes in scene order - their index maps to wheel index used by VehicleBody3D
		foreach (var child in GetChildren())
		{
			if (child is VehicleWheel3D wheel)
				_wheels.Add(wheel);
		}
		if (_wheels.Count == 0)
			GD.Print("[Car] No VehicleWheel3D nodes found as children. Make sure wheels are direct children of the VehicleBody3D.");
	}

	public override void _PhysicsProcess(double delta)
	{
		// Read input (project uses "fwd", "back", "left", "right" actions elsewhere)
		float accel = 0f;
		if (Input.IsActionPressed("fwd")) accel += 1f;
		if (Input.IsActionPressed("back")) accel -= 1f;

		float steerInput = 0f;
		if (Input.IsActionPressed("left")) steerInput -= 1f;
		if (Input.IsActionPressed("right")) steerInput += 1f;

		// Compute desired forces
		float engine = accel * MaxEngineForce;
		float steerAngle = Mathf.DegToRad(MaxSteerAngleDeg) * steerInput;

		// Apply to each wheel by index; use wheel properties to decide traction/steering
		for (int i = 0; i < _wheels.Count; i++)
		{
			var w = _wheels[i];
			// Steering
			if (w.UseAsSteering)
			{
				// Call engine API (GDScript name) via dynamic call so C# compiles regardless of exact signature
				// apply_engine_force, set_steering and apply_brake are the runtime methods on VehicleBody3D
				Steering = steerAngle;
			}

			// Engine force / traction
			if (w.UseAsTraction)
			{
				EngineForce = engine;
			}
			else
			{
				EngineForce = 0f;
			}
		}
	}
}
