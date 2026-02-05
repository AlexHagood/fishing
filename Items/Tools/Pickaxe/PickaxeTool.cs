using Godot;
public partial class PickaxeTool : ToolScript
{
    public override void _Ready()
    {
        
        // Position and rotation are now controlled by the hand bone and tool scene positioning
        // Animations are now handled by the character, not the tool
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
        else if (Target is Character TargetCharacter)
        {
            TargetCharacter.ApplyCharacterImpulse((TargetCharacter.GlobalTransform.Origin - character.GlobalTransform.Origin).Normalized() * 20.0f + (Vector3.Up * 5.0f));
        }
        else
        {
            GD.Print("[PickaxeTool] No valid target hit.");
        }

        GD.Print($"[PickaxeTool] Swinging pickaxe: {itemInstance.ItemData.Name}");
        character.animTree.Swing();
        
        // Character will play the animation automatically based on primaryAnimation field
        // Implement pickaxe swinging logic here (e.g., damage to rocks)
    }

    public override void SecondaryFire(Character character)
    {
        GD.Print($"[PickaxeTool] Aiming with pickaxe: {itemInstance.ItemData.Name}");
        base.SecondaryFire(character);
        // Implement secondary action logic here (e.g., zoom in)
    }
}