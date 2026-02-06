using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
public partial class Rope : Node3D
{

	[Export]
	public int Segments {
		get => _segments;
		set {
			if (value < 1) value = 1; // Minimum 1 segment
			_segments = value;
			UpdateRope();
		}
	}

	List<RigidBody3D> _segmentBodies = new List<RigidBody3D>();
	[Export]
	public float segmentLength 
	{
		get => _segmentLength;
		set
		{
			if (value < 0.1f) value = 0.1f; // Minimum length to avoid issues
			_segmentLength = value;
			UpdateRope();
		}
	}
	private float _segmentLength = 2.0f;
	[Export]
	public NodePath AnchorPoint { 
		get => _anchorPoint;
		set {
			_anchorPoint = value;
			
			// Convert to absolute path if it's a relative path
			if (!value.IsEmpty)
			{
				Node anchorNode = GetNodeOrNull(value);
				if (anchorNode != null)
				{
					GetNode<Generic6DofJoint3D>("AnchorJoint").NodeA = anchorNode.GetPath();
				}
				else
				{
					GetNode<Generic6DofJoint3D>("AnchorJoint").NodeA = value;
				}
			}
		}
	}

	private NodePath _anchorPoint;

	private int _segments;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
    {
		_segmentBodies.Add(GetNode<RigidBody3D>("Segment0"));
        UpdateRope();
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void UpdateSegmentHeight(RigidBody3D segment)
	{
		// Mesh and collision shape are centered at segment origin
		// They extend segmentLength/2 up and down
		MeshInstance3D meshInstance = segment.GetNode<MeshInstance3D>("MeshInstance3D");
		(meshInstance.Mesh as CylinderMesh).Height = segmentLength;
		meshInstance.Position = new Vector3(0, 0, 0);

		CollisionShape3D collisionShape = segment.GetNode<CollisionShape3D>("CollisionShape3D");
		(collisionShape.Shape as CylinderShape3D).Height = segmentLength;
		collisionShape.Position = new Vector3(0, 0, 0);

		// Joint is at top of segment + half the gap (0.05)
		Generic6DofJoint3D joint = segment.GetNode<Generic6DofJoint3D>("RopeJoint");
		joint.Position = new Vector3(0, segmentLength / 2 + 0.05f, 0);
	}

	public void UpdateRope()
    {
		AnchorPoint = AnchorPoint;
		// Check if Segment0 exists, if not we're not ready yet
		RigidBody3D firstSegment = GetNodeOrNull<RigidBody3D>("Segment0");
		if (firstSegment == null)
			return;
		
		// Re-enable all joints and unfreeze all segments first
		foreach (var segment in _segmentBodies)
		{
			if (segment != null && IsInstanceValid(segment))
			{
				segment.Freeze = false;
				Generic6DofJoint3D segmentJoint = segment.GetNode<Generic6DofJoint3D>("RopeJoint");
				segmentJoint.SetPhysicsProcessInternal(true);
			}
		}
		
		// Remove extra segments if rope is shortened
		while (_segmentBodies.Count > Segments)
		{
			int lastIndex = _segmentBodies.Count - 1;
			if (_segmentBodies[lastIndex] != null && IsInstanceValid(_segmentBodies[lastIndex]))
			{
				_segmentBodies[lastIndex].QueueFree();
			}
			_segmentBodies.RemoveAt(lastIndex);
		}

		UpdateSegmentHeight(firstSegment);

		Generic6DofJoint3D joint = firstSegment.GetNode<Generic6DofJoint3D>("RopeJoint");
		joint.NodeA = firstSegment.GetPath();

		// Update all existing segments' heights and positions
		RigidBody3D lastSegment = firstSegment;
		Generic6DofJoint3D lastJoint = joint;
		
		for (int i = 0; i < _segmentBodies.Count; i++)
		{
			if (i == 0 && _segmentBodies[i] == firstSegment)
			{
				// Skip firstSegment as it's already updated above
				lastSegment = _segmentBodies[i];
				lastJoint = lastSegment.GetNode<Generic6DofJoint3D>("RopeJoint");
				continue;
			}
			
			RigidBody3D segment = _segmentBodies[i];
			UpdateSegmentHeight(segment);
			
			// Reposition existing segment
			segment.Position = lastSegment.Position + new Vector3(0, segmentLength + 0.1f, 0);
			
			Generic6DofJoint3D segmentJoint = segment.GetNode<Generic6DofJoint3D>("RopeJoint");
			segmentJoint.NodeA = segment.GetPath();
			lastJoint.NodeB = segment.GetPath();
			
			lastSegment = segment;
			lastJoint = segmentJoint;
		}

		// Start from the current count and add only new segments needed
		int startIndex = _segmentBodies.Count;
		for (int i = startIndex; i < Segments; i++)
		{
			RigidBody3D newSegment = (RigidBody3D)firstSegment.Duplicate();
			newSegment.Name = $"Segment{i+1}";
			AddChild(newSegment);
			
			// Set owner so nodes appear in editor (for Tool scripts)
			
			// Position: last segment position + segmentLength + gap of 0.1
			newSegment.Position = lastSegment.Position + new Vector3(0, segmentLength + 0.1f, 0);
			Generic6DofJoint3D newJoint = newSegment.GetNode<Generic6DofJoint3D>("RopeJoint");
			newJoint.NodeA = newSegment.GetPath();
			lastJoint.NodeB = newSegment.GetPath();
			lastSegment = newSegment;
			lastJoint = newJoint;
			_segmentBodies.Add(newSegment);
		}

		// Set the last joint to connect back to itself (or disable if it would create a loop)
		if (lastJoint.NodeA == lastSegment.GetPath())
		{
			GD.Print("Last joint would connect segment to itself. Disabling joint.");
			// If the joint would connect the segment to itself, disable it
			lastJoint.NodeB = lastSegment.GetPath();
			lastJoint.SetPhysicsProcessInternal(false);
			// Optionally freeze the last segment in place
		}
    }
}
