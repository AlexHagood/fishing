using Godot;

public partial class Pickaxe : ToolItem
{
    [Export] public float HitForce = 15.0f;
    [Export] public float RaycastRange = 5.0f;

    public override void OnPrimaryFire()
    {
        GD.Print("Pickaxe swing!");
        
        // Find the character and camera by traversing up the tree
        Node3D holdPosition = GetParent() as Node3D;
        if (holdPosition == null)
        {
            GD.PrintErr("Pickaxe: No HoldPosition parent found!");
            return;
        }
        
        var camera = holdPosition.GetParent() as Camera3D;
        if (camera == null)
        {
            GD.PrintErr("Pickaxe: No camera found!");
            return;
        }
        
        // Get the character (camera's grandparent)
        var character = camera.GetParent() as Character;
        if (character == null)
        {
            GD.PrintErr("Pickaxe: No character found!");
            return;
        }
        
        // Perform raycast from camera
        var spaceState = GetWorld3D().DirectSpaceState;
        var from = camera.GlobalTransform.Origin;
        var to = from + camera.GlobalTransform.Basis.Z * -RaycastRange;
        
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.Exclude = new Godot.Collections.Array<Rid> { character.GetRid() };
        
        var result = spaceState.IntersectRay(query);
        
        if (result.Count > 0)
        {
            var collider = result["collider"].AsGodotObject();
            
            // Check if we hit a GameItem
            GameItem gameItem = null;
            Node nodeToCheck = collider as Node;
            
            while (nodeToCheck != null && gameItem == null)
            {
                gameItem = nodeToCheck as GameItem;
                if (gameItem != null) break;
                nodeToCheck = nodeToCheck.GetParent();
            }
            
            if (gameItem != null)
            {
                // Calculate direction from camera to hit point
                var hitPoint = (Vector3)result["position"];
                var direction = (hitPoint - from).Normalized();
                
                // Apply impulse to the GameItem
                gameItem.ApplyImpulse(direction * HitForce, hitPoint - gameItem.GlobalPosition);
                GD.Print($"Pickaxe hit {gameItem.ItemName}! Applied force.");
            }
            else
            {
                GD.Print("Pickaxe hit something, but it's not a GameItem.");
            }
        }
        else
        {
            GD.Print("Pickaxe swing missed!");
        }
    }

    public override void OnSecondaryFire()
    {
        GD.Print("Pickaxe secondary action!");
    }
}
