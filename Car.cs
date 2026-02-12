using Godot;
using System.Collections.Generic;

public partial class Car : VehicleBody3D, IInteractable
{
	[Export] public float MaxEngineForce = 1800f; // Force applied when accelerating
	[Export] public float MaxBrakeForce = 120f; // Brake torque
	[Export] public float MaxSteerAngleDeg = 30f; // Degrees

	private List<VehicleWheel3D> _wheels = new List<VehicleWheel3D>();
	private Character _driver = null;
	private bool _isDriving = false;

	private Camera3D _camera3D;

	// IInteractable implementation
	public string HintE => "";
	public string HintF => _isDriving ? "Exit Vehicle" : "Drive";
	public float InteractRange => 3.0f;

	public override void _Ready()
	{
		// Gather Wheel nodes in scene order - their index maps to wheel index used by VehicleBody3D
		foreach (var child in GetChildren())
		{
			if (child is VehicleWheel3D wheel)
				_wheels.Add(wheel);
		}
		if (_wheels.Count == 0)
			Log("No VehicleWheel3D nodes found as children. Make sure wheels are direct children of the VehicleBody3D.");

		_camera3D = GetNode<Camera3D>("Camera3D");
	}

	public override void _PhysicsProcess(double delta)
	{
		// Only process input if someone is driving
		if (!_isDriving)
		{
			// Apply brake when not being driven
			Brake = MaxBrakeForce;
			EngineForce = 0f;
			return;
		}

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

	// IInteractable implementation
	public void InteractE(Character character)
	{
		// E does nothing for car
	}

	public void InteractF(Character character)
	{
		if (_isDriving)
		{
			// Exit vehicle
			ExitVehicle();
		}
		else
		{
			// Enter vehicle
			EnterVehicle(character);
		}
	}

	public bool CanInteract()
	{
		return !IsQueuedForDeletion();
	}

	private void EnterVehicle(Character character)
	{
		_driver = character;
		_isDriving = true;
		
		// Switch to car camera
		if (_camera3D != null)
		{
			character.SetExternalCamera(_camera3D);
		}
		else
		{
			Error("No camera found on car! Add a Camera3D node as a child.");
		}
		
		// Disable character's physics/movement
		character.ProcessMode = ProcessModeEnum.Disabled;
		
		// Hide the character or position them in the car
		// For now, just hide them
		character.Visible = false;
		
		Log("Driver entered vehicle");
	}

	private void ExitVehicle()
	{
		if (_driver == null) return;

		// Restore character camera
		_driver.RestoreCharacterCamera();

		// Re-enable character
		_driver.ProcessMode = ProcessModeEnum.Inherit;
		_driver.Visible = true;
		
		// Position character next to the car
		_driver.GlobalPosition = GlobalPosition + new Vector3(2, 1, 0);
		
		_driver = null;
		_isDriving = false;
		
		// Apply brake
		Brake = MaxBrakeForce;
		
		Log("Driver exited vehicle");
	}
}
