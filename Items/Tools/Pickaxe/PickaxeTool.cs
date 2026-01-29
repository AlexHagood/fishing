using Godot;
public partial class PickaxeTool : ToolScript
{
    private AnimationPlayer _animationPlayer;
    public override void _Ready()
    {
        _animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        Position = new Vector3(0.5f, 0, -1);
        RotationDegrees = new Vector3(0, 90f, 0);
    }

    public override void PrimaryFire(Character character)
    {
        GodotObject? Target = character.RaycastFromCamera();
        if (Target is RigidBody3D rb)
        {
            GD.Print($"[PickaxeTool] Hit object: {rb.Name}");
            // Calculate direction from character to target
            Vector3 direction = (rb.GlobalTransform.Origin - character.GlobalTransform.Origin).Normalized();
            
            // Apply impulse (impulse, then optional position offset)
            float impulseStrength = 10.0f;
            rb.ApplyImpulse(direction * impulseStrength);
            
            GD.Print($"[PickaxeTool] Applied impulse: {direction * impulseStrength}");
        }
        else
        {
            GD.Print("[PickaxeTool] No valid target hit.");
        }

        GD.Print($"[PickaxeTool] Swinging pickaxe: {itemInstance.ItemData.Name}");
        _animationPlayer.Play("Swing");
        // Implement pickaxe swinging logic here (e.g., damage to rocks)
    }

    public override void SecondaryFire(Character character)
    {
        GD.Print($"[PickaxeTool] Aiming with pickaxe: {itemInstance.ItemData.Name}");
        // Implement secondary action logic here (e.g., zoom in)
    }
}