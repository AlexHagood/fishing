using System.Collections.Generic;
using Godot;
using System.Linq;

/// <summary>
/// Represents a single entry in the fish loot table with drop chance
/// </summary>
public class FishLootEntry
{
    public ItemDefinition Fish { get; set; }
    public float DropChance { get; set; } // Percentage (0-100)
    
    public FishLootEntry(ItemDefinition fish, float dropChance)
    {
        Fish = fish;
        DropChance = dropChance;
    }
}

public partial class FishManager : Node
{
    private List<FishLootEntry> _fishLootTable = new List<FishLootEntry>();

    public override void _Ready()
    {
        InitializeLootTable();
    }
    
    private void InitializeLootTable()
    {
        // Load fish item definitions
        var bluegill = GD.Load<ItemDefinition>("res://Items/Fish/Bluegill.tres");
        var smallmouth = GD.Load<ItemDefinition>("res://Items/Fish/Smallmouth.tres");
        
        // Build loot table with drop chances (should total 100%)
        _fishLootTable.Add(new FishLootEntry(bluegill, 70.0f));      // 70% chance - Common
        _fishLootTable.Add(new FishLootEntry(smallmouth, 30.0f));    // 30% chance - Uncommon
        
        // Validate that percentages add up to 100%
        float totalChance = _fishLootTable.Sum(entry => entry.DropChance);
        if (Mathf.Abs(totalChance - 100.0f) > 0.01f)
        {
            GD.PushWarning($"[FishManager] Loot table percentages don't add up to 100%! Current total: {totalChance}%");
        }
    }
    
    /// <summary>
    /// Rolls the loot table and returns a random fish based on drop chances
    /// </summary>
    public ItemDefinition GetFishingLoot()
    {
        if (_fishLootTable.Count == 0)
        {
            GD.PushError("[FishManager] Loot table is empty!");
            return null;
        }
        
        // Roll a random number between 0 and 100
        float roll = (float)GD.RandRange(0.0, 100.0);
        
        GD.Print($"[FishManager] Rolled {roll:F2}%");
        
        // Find which fish was rolled
        float cumulative = 0.0f;
        foreach (var entry in _fishLootTable)
        {
            cumulative += entry.DropChance;
            if (roll <= cumulative)
            {
                GD.Print($"[FishManager] Caught: {entry.Fish.Name}!");
                Rpc("UI.Chat.SendChatMessage", "System", $"You caught a {entry.Fish.Name}!");
                return entry.Fish;
            }
        }
        
        // Fallback to last item if something goes wrong (shouldn't happen if percentages add to 100)
        GD.PushWarning("[FishManager] Fallback to last item in loot table");
        return _fishLootTable[_fishLootTable.Count - 1].Fish;
    }
}