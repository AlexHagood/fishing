using Godot;
using System.Collections.Generic;

/// <summary>
/// Simplified water system with buoyancy for fishing game.
/// Handles splash sounds and realistic floating for objects.
/// </summary>
public partial class Water : Node3D
{
	private Area3D _waterVolume;
	private AudioStreamPlayer3D _splashPlayer;
	private HashSet<RigidBody3D> _bodiesInWater = new HashSet<RigidBody3D>();
	private float _waterSurfaceY;
	
	public override void _Ready()
	{
		// Get the WaterVolume Area3D child
		_waterVolume = GetNodeOrNull<Area3D>("WaterVolume");
		if (_waterVolume == null)
		{
			GD.PushError("Water: No WaterVolume Area3D child found!");
			return;
		}
		
		// Get the actual water surface Y from the mesh (accounting for ALL scales)
		var meshInstance = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
		if (meshInstance != null && meshInstance.Mesh is BoxMesh boxMesh)
		{
			// Water surface is at the top of the scaled mesh
			// We need to account for both the Water node's scale AND the mesh's local scale
			float meshHalfHeight = boxMesh.Size.Y * 0.5f;
			
			// Get the global scale by using the transform basis
			Vector3 globalScale = meshInstance.GlobalTransform.Basis.Scale;
			float scaledHalfHeight = meshHalfHeight * globalScale.Y;
			
			_waterSurfaceY = meshInstance.GlobalPosition.Y + scaledHalfHeight;
		}
		else
		{
			// Fallback: use the Water node's position + its scale
			_waterSurfaceY = GlobalPosition.Y + (Scale.Y * 0.5f);
		}
		
		// Get splash sound player
		_splashPlayer = _waterVolume.GetNodeOrNull<AudioStreamPlayer3D>("SplashPlayer");
		
		// Connect to water volume signals
		_waterVolume.BodyEntered += OnBodyEnteredWater;
		_waterVolume.BodyExited += OnBodyExitedWater;
		
		GD.Print($"Water system initialized. Water surface at Y={_waterSurfaceY}");
	}
	
	public override void _PhysicsProcess(double delta)
	{
		// Apply buoyancy to all bodies in water
		foreach (var body in _bodiesInWater)
		{
			if (!IsInstanceValid(body) || body.Freeze)
				continue;
			
			ApplyBuoyancy(body, (float)delta);
		}
	}
	
	private void OnBodyEnteredWater(Node3D body)
	{
		if (body is RigidBody3D rigidBody)
		{
			_bodiesInWater.Add(rigidBody);
			PlaySplash(rigidBody.GlobalPosition);
			GD.Print($"{rigidBody.Name} entered water");
		}
	}
	
	private void OnBodyExitedWater(Node3D body)
	{
		if (body is RigidBody3D rigidBody)
		{
			_bodiesInWater.Remove(rigidBody);
			GD.Print($"{rigidBody.Name} exited water");
		}
	}
	
	private void ApplyBuoyancy(RigidBody3D body, float delta)
	{
		// Get buoyancy value from GameItem if available
		float buoyancy = 1.0f;
		
		// if (body is Node3D gameItem && gameItem.ItemDef != null)
		// {
		// 	buoyancy = gameItem.ItemDef.Buoyancy;
		// }
		// // Check for "buoyant" group (for bobbers and other non-GameItem floaters)
		// else if (body.IsInGroup("buoyant"))
		// {
		// 	buoyancy = 1.5f; // Floats well
		// }
		// // Check for bobbers by name (fallback)
		// else if (body.Name.ToString().Contains("Bobber", System.StringComparison.OrdinalIgnoreCase))
		// {
		// 	buoyancy = 1.5f; // Bobbers float!
		// }
		
		// // Get the object's size (rotation-independent)
		// float objectRadius = GetBodyRadius(body);
		// float objectCenterY = body.GlobalPosition.Y;
		
		// // Calculate how deep the center is below the water surface
		// float depthBelowSurface = _waterSurfaceY - objectCenterY;
		
		// // Calculate submersion ratio based on center depth
		// // If center is above water by radius, submersion = 0
		// // If center is at water level, submersion = 0.5
		// // If center is below water by radius, submersion = 1.0
		// float submersionRatio = Mathf.Clamp((depthBelowSurface + objectRadius) / (objectRadius * 2.0f), 0.0f, 1.0f);
		
		// if (submersionRatio <= 0)
		// 	return; // Not submerged
		
		// // Physics: 
		// // Buoyancy force = buoyancy * gravity * mass * submersion
		// // For objects to float half-submerged, buoyancy should be ~1.0
		// float gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 9.8);
		// float buoyantForce = buoyancy * gravity * body.Mass * submersionRatio;
		
		// // Apply upward buoyancy force
		// body.ApplyCentralForce(Vector3.Up * buoyantForce);
		
		// // Apply water drag (resists movement)
		// float drag = 0.5f;
		// body.ApplyCentralForce(-body.LinearVelocity * drag * body.Mass);
		
		// // Apply angular drag (resists rotation)
		// float angularDrag = 0.3f;
		// body.ApplyTorque(-body.AngularVelocity * angularDrag * body.Mass);
		
		// // Surface damping to prevent bobbing
		// float centerY = body.GlobalPosition.Y;
		// float distanceFromSurface = Mathf.Abs(centerY - _waterSurfaceY);
		// if (distanceFromSurface < objectRadius * 0.5f && body.LinearVelocity.Y > 0.5f)
		// {
		// 	// Damp vertical velocity near surface
		// 	body.LinearVelocity = new Vector3(
		// 		body.LinearVelocity.X,
		// 		body.LinearVelocity.Y * 0.9f,
		// 		body.LinearVelocity.Z
		// 	);
		// }
	}
	
	private float GetBodyRadius(RigidBody3D body)
	{
		// Try to get size from collision shape (most accurate)
		foreach (var child in body.GetChildren())
		{
			if (child is CollisionShape3D collisionShape)
			{
				if (collisionShape.Shape is BoxShape3D boxShape)
				{
					// Use half the height as radius
					return boxShape.Size.Y * 0.5f;
				}
				else if (collisionShape.Shape is SphereShape3D sphereShape)
				{
					return sphereShape.Radius;
				}
				else if (collisionShape.Shape is CapsuleShape3D capsuleShape)
				{
					return capsuleShape.Height * 0.5f;
				}
				else if (collisionShape.Shape is CylinderShape3D cylinderShape)
				{
					return cylinderShape.Height * 0.5f;
				}
			}
		}
		
		// Fallback: use 0.5m radius
		return 0.5f;
	}
	
	private Aabb GetBodyAABB(RigidBody3D body)
	{
		// Try to get AABB from mesh
		foreach (var child in body.GetChildren())
		{
			if (child is MeshInstance3D meshInstance && meshInstance.Mesh != null)
			{
				var localAabb = meshInstance.Mesh.GetAabb();
				var globalCenter = meshInstance.GlobalTransform * localAabb.GetCenter();
				var globalSize = localAabb.Size * meshInstance.GlobalTransform.Basis.Scale;
				return new Aabb(globalCenter - globalSize / 2, globalSize);
			}
		}
		
		// Try collision shape
		foreach (var child in body.GetChildren())
		{
			if (child is CollisionShape3D collisionShape && collisionShape.Shape is BoxShape3D boxShape)
			{
				var size = boxShape.Size * collisionShape.GlobalTransform.Basis.Scale;
				var center = collisionShape.GlobalPosition;
				return new Aabb(center - size / 2, size);
			}
			else if (child is CollisionShape3D collisionShape2 && collisionShape2.Shape is SphereShape3D sphereShape)
			{
				float radius = sphereShape.Radius;
				var center = collisionShape2.GlobalPosition;
				var size = Vector3.One * radius * 2;
				return new Aabb(center - Vector3.One * radius, size);
			}
		}
		
		// Fallback: use 0.5m cube
		return new Aabb(body.GlobalPosition - Vector3.One * 0.25f, Vector3.One * 0.5f);
	}
	
	private void PlaySplash(Vector3 position)
	{
		if (_splashPlayer != null && _splashPlayer.Stream != null)
		{
			_splashPlayer.GlobalPosition = position;
			_splashPlayer.Play();
		}
		else
		{
			GD.PushWarning("Water: No splash sound configured");
		}
	}
}
