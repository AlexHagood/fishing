using Godot;
using System;

public partial class ShovelScript : ToolScript
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public override void PrimaryFire(Character character)
	{
		var hit = character.RaycastRaw(40f);
		
		if (hit == null || !hit.ContainsKey("position"))
			return;
		
		// Get the terrain
		var collider = hit["collider"];
		if (collider.Obj is not StaticBody3D body)
			return;
		
		var terrain = body.GetParent()?.GetParent() as Terrain;
		if (terrain == null)
			return;
		
		// Get hit position
		var hitPosition = (Vector3)hit["position"];
		
		Log($"Hit position: {hitPosition}");
		Log($"Collider: {collider.Obj.GetType().Name}, Parent: {body.GetParent()?.Name}");
		
		// Find the triangle and closest vertex
		int triangleIndex = terrain.GetTriangleAtPosition(hitPosition);
		if (triangleIndex == -1)
		{
			Log($"No triangle found at position {hitPosition}");
			Log($"Terrain ChunkSize: {terrain.ChunkSize}, Total chunks: {terrain.ChunkMap.Count}");
			return;
		}
		
		int vertexIndex = terrain.GetClosestVertexInTriangle(triangleIndex, hitPosition);
		if (vertexIndex == -1)
		{
			Log("No vertex found");
			return;
		}
		
		// Lower the vertex by 1 unit
		var currentPos = terrain.Vertices[vertexIndex];
		terrain.ModifyVertex(vertexIndex, currentPos - Vector3.Up * 1.0f);
		
		Log($"Lowered vertex {vertexIndex} at {currentPos}");
	}
	
	public override void SecondaryFire(Character character)
	{
		var hit = character.RaycastRaw(40f);
		
		if (hit == null || !hit.ContainsKey("position"))
			return;
		
		// Get the terrain
		var collider = hit["collider"];
		if (collider.Obj is not StaticBody3D body)
			return;
		
		var terrain = body.GetParent()?.GetParent() as Terrain;
		if (terrain == null)
			return;
		
		// Get hit position
		var hitPosition = (Vector3)hit["position"];
		
		// Find the triangle and closest vertex
		int triangleIndex = terrain.GetTriangleAtPosition(hitPosition);
		if (triangleIndex == -1)
		{
			Log("No triangle found at position");
			return;
		}
		
		int vertexIndex = terrain.GetClosestVertexInTriangle(triangleIndex, hitPosition);
		if (vertexIndex == -1)
		{
			Log("No vertex found");
			return;
		}
		
		// Raise the vertex by 1 unit
		var currentPos = terrain.Vertices[vertexIndex];
		terrain.ModifyVertex(vertexIndex, currentPos + Vector3.Up * 1.0f);
		
		Log($"Raised vertex {vertexIndex} at {currentPos}");
	}
}
