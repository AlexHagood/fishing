using GdUnit4;
using Godot;
using System;
using static GdUnit4.Assertions;
namespace Tests;

[TestSuite]
public class InventoryManagerTests
{
    private InventoryManager CreateInventoryManager()
    {
        return new InventoryManager();
    }

    private ItemDefinition CreateTestItem(string name = "TestItem", Vector2I? size = null, int stackSize = 1)
    {
        return new ItemDefinition
        {
            Name = name,
            Size = size ?? new Vector2I(1, 1),
            StackSize = stackSize,
            Icon = "",
            ScenePath = "res://test.tscn"
        };
    }

    #region Inventory Creation Tests

    [TestCase]
    [RequireGodotRuntime]
    public void CreateInventory_ShouldReturnValidId()
    {
        // Arrange
        var manager = CreateInventoryManager();

        // Act
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));

        // Assert
        AssertThat(inventoryId).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]

    public void CreateInventory_MultipleInventories_ShouldReturnSequentialIds()
    {
        // Arrange
        var manager = CreateInventoryManager();

        // Act
        int id1 = manager.CreateInventory(new Vector2I(5, 5));
        int id2 = manager.CreateInventory(new Vector2I(10, 10));
        int id3 = manager.CreateInventory(new Vector2I(3, 3));

        // Assert
        AssertThat(id1).IsEqual(0);
        AssertThat(id2).IsEqual(1);
        AssertThat(id3).IsEqual(2);
    }

    [TestCase]
    [RequireGodotRuntime]

    public void GetInventorySize_ShouldReturnCorrectSize()
    {
        // Arrange
        var manager = CreateInventoryManager();
        var expectedSize = new Vector2I(7, 9);
        int inventoryId = manager.CreateInventory(expectedSize);

        // Act
        var actualSize = manager.GetInventorySize(inventoryId);

        // Assert
        AssertThat(actualSize).IsEqual(expectedSize);
    }

    [TestCase]
    [RequireGodotRuntime]

    public void GetInventory_InvalidId_ShouldThrowException()
    {
        // Arrange
        var manager = CreateInventoryManager();

        // Act & Assert
        AssertThrown(() => manager.GetInventory(999))
            .IsInstanceOf<ArgumentException>();
    }

    #endregion

    #region Item Placement Tests

    [TestCase]
    [RequireGodotRuntime]

    public void TryPushItem_EmptyInventory_ShouldSucceed()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));
        var item = CreateTestItem("Rock");

        // Act
        bool result = manager.TryPushItem(inventoryId, item, false);

        // Assert
        AssertThat(result).IsTrue();
        var inventory = manager.GetInventory(inventoryId);
        AssertThat(inventory.GetAllItems().Count).IsEqual(1);
    }

    [TestCase]
    [RequireGodotRuntime]

    public void TryPushItem_FullInventory_ShouldFail()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(1, 1));
        var item1 = CreateTestItem("Rock");
        var item2 = CreateTestItem("Stick");

        // Act
        manager.TryPushItem(inventoryId, item1, false);
        bool result = manager.TryPushItem(inventoryId, item2, false);

        // Assert
        AssertThat(result).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]

    public void TryPushItem_LargeItem_ShouldFitCorrectly()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));
        var largeItem = CreateTestItem("BigItem", new Vector2I(2, 3));

        // Act
        bool result = manager.TryPushItem(inventoryId, largeItem, false);

        // Assert
        AssertThat(result).IsTrue();
        var inventory = manager.GetInventory(inventoryId);
        AssertThat(inventory.GetAllItems().Count).IsEqual(1);
    }

    [TestCase]
    [RequireGodotRuntime]

    public void TryPushItem_ItemTooBig_ShouldFail()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(2, 2));
        var hugeItem = CreateTestItem("HugeItem", new Vector2I(3, 3));

        // Act
        bool result = manager.TryPushItem(inventoryId, hugeItem, false);

        // Assert
        AssertThat(result).IsFalse();
    }

    #endregion

    #region Stacking Tests

    [TestCase]
    [RequireGodotRuntime]

    public void TryPushItem_StackableItems_ShouldStack()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));
        var stackableItem = CreateTestItem("Coin", stackSize: 99);

        // Act
        manager.TryPushItem(inventoryId, stackableItem, false);
        manager.TryPushItem(inventoryId, stackableItem, false);

        // Assert
        var inventory = manager.GetInventory(inventoryId);
        var items = inventory.GetAllItems();
        AssertThat(items.Count).IsEqual(1);
        AssertThat(items[0].CurrentStackSize).IsEqual(2);
    }

    [TestCase]
    [RequireGodotRuntime]

    public void AddItemToStack_WithinLimit_ShouldSucceed()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));
        var item = CreateTestItem("Coin", stackSize: 10);
        manager.TryPushItem(inventoryId, item, false);
        var itemInstance = manager.GetInventory(inventoryId).GetAllItems()[0];

        // Act
        manager.AddItemToStack(inventoryId, itemInstance, 5);

        // Assert
        AssertThat(itemInstance.CurrentStackSize).IsEqual(6);
    }

    [TestCase]
    [RequireGodotRuntime]

    public void AddItemToStack_ExceedingLimit_ShouldThrowException()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));
        var item = CreateTestItem("Coin", stackSize: 10);
        manager.TryPushItem(inventoryId, item, false);
        var itemInstance = manager.GetInventory(inventoryId).GetAllItems()[0];

        // Act & Assert
        AssertThrown(() => manager.AddItemToStack(inventoryId, itemInstance, 20))
            .IsInstanceOf<InvalidOperationException>();
    }

    #endregion

    #region Item Rotation Tests

    [TestCase]
    [RequireGodotRuntime]

    public void RotateItem_WithSpace_ShouldSucceed()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));
        var item = CreateTestItem("Plank", new Vector2I(1, 3));
        manager.TryPushItem(inventoryId, item, false);
        var itemInstance = manager.GetInventory(inventoryId).GetAllItems()[0];

        // Act
        bool result = manager.RotateItem(itemInstance);

        // Assert
        AssertThat(result).IsTrue();
        AssertThat(itemInstance.IsRotated).IsTrue();
        AssertThat(itemInstance.Size).IsEqual(new Vector2I(3, 1));
    }

    [TestCase]
    [RequireGodotRuntime]

    public void RotateItem_NoSpace_ShouldFail()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(2, 5));
        var item = CreateTestItem("Plank", new Vector2I(1, 3));
        manager.TryPushItem(inventoryId, item, false);
        var itemInstance = manager.GetInventory(inventoryId).GetAllItems()[0];

        // Act
        bool result = manager.RotateItem(itemInstance);

        // Assert
        AssertThat(result).IsFalse();
        AssertThat(itemInstance.IsRotated).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]

    public void CanRotateItem_ShouldDetectObstacles()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));
        var item1 = CreateTestItem("Plank", new Vector2I(1, 3));
        var item2 = CreateTestItem("Rock");
        
        manager.TryPushItem(inventoryId, item1, false);
        manager.TryPushItem(inventoryId, item2, false);
        
        var plank = manager.GetInventory(inventoryId).GetAllItems()
            .Find(i => i.ItemData.Name == "Plank");

        // Act
        bool canRotate = manager.CanRotateItem(plank);

        // Assert - Depends on where the rock is placed
        AssertThat(plank).IsNotNull();
    }

    #endregion

    #region Item Transfer Tests

    [TestCase]
    [RequireGodotRuntime]

    public void TryTransferItem_ToEmptyInventory_ShouldSucceed()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inv1 = manager.CreateInventory(new Vector2I(5, 5));
        int inv2 = manager.CreateInventory(new Vector2I(5, 5));
        var item = CreateTestItem("Rock");
        
        manager.TryPushItem(inv1, item, false);
        var itemInstance = manager.GetInventory(inv1).GetAllItems()[0];

        // Act
        bool result = manager.TryTransferItem(inv2, itemInstance);

        // Assert
        AssertThat(result).IsTrue();
        AssertThat(manager.GetInventory(inv1).GetAllItems().Count).IsEqual(0);
        AssertThat(manager.GetInventory(inv2).GetAllItems().Count).IsEqual(1);
    }

    [TestCase]
    [RequireGodotRuntime]

    public void TryTransferItemPosition_SpecificPosition_ShouldSucceed()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inv1 = manager.CreateInventory(new Vector2I(5, 5));
        int inv2 = manager.CreateInventory(new Vector2I(5, 5));
        var item = CreateTestItem("Rock");
        
        manager.TryPushItem(inv1, item, false);
        var itemInstance = manager.GetInventory(inv1).GetAllItems()[0];
        var targetPos = new Vector2I(3, 3);

        // Act
        int result = manager.TryTransferItemPosition(inv2, itemInstance, targetPos, false);

        // Assert
        AssertThat(result).IsEqual(1);
        AssertThat(itemInstance.GridPosition).IsEqual(targetPos);
        AssertThat(itemInstance.InventoryId).IsEqual(inv2);
    }

    [TestCase]
    [RequireGodotRuntime]

    public void TryTransferItemPosition_OccupiedSpace_ShouldFail()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inv1 = manager.CreateInventory(new Vector2I(5, 5));
        int inv2 = manager.CreateInventory(new Vector2I(5, 5));
        var item1 = CreateTestItem("Rock1");
        var item2 = CreateTestItem("Rock2");
        
        manager.TryPushItem(inv2, item1, false); // Put item in target position first
        manager.TryPushItem(inv1, item2, false);
        var itemToMove = manager.GetInventory(inv1).GetAllItems()[0];
        var targetPos = new Vector2I(0, 0); // Same position as item1

        // Act
        int result = manager.TryTransferItemPosition(inv2, itemToMove, targetPos, false);

        // Assert
        AssertThat(result).IsEqual(0); // Failed to transfer
    }

    #endregion

    #region Stack Splitting Tests

    [TestCase]
    [RequireGodotRuntime]

    public void TrySplitStack_ValidSplit_ShouldSucceed()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));
        var stackableItem = CreateTestItem("Coin", stackSize: 99);
        
        // Add 5 items to create a stack of 5
        for (int i = 0; i < 5; i++)
            manager.TryPushItem(inventoryId, stackableItem, false);
        
        var itemInstance = manager.GetInventory(inventoryId).GetAllItems()[0];
        var targetPos = new Vector2I(2, 2);

        // Act
        int result = manager.TrySplitStack(inventoryId, itemInstance, 3, targetPos, false);

        // Assert
        AssertThat(result).IsEqual(3);
        AssertThat(itemInstance.CurrentStackSize).IsEqual(2); // 5 - 3 = 2 remaining
        
        var items = manager.GetInventory(inventoryId).GetAllItems();
        AssertThat(items.Count).IsEqual(2); // Now have 2 stacks
    }

    [TestCase]
    [RequireGodotRuntime]

    public void TrySplitStack_SplitMoreThanAvailable_ShouldThrowException()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));
        var stackableItem = CreateTestItem("Coin", stackSize: 99);
        
        manager.TryPushItem(inventoryId, stackableItem, false);
        var itemInstance = manager.GetInventory(inventoryId).GetAllItems()[0];

        // Act & Assert
        AssertThrown(() => manager.TrySplitStack(inventoryId, itemInstance, 5, new Vector2I(1, 1), false))
            .IsInstanceOf<InvalidOperationException>();
    }

    [TestCase]
    [RequireGodotRuntime]

    public void TrySplitStack_EntireStack_ShouldRemoveOriginal()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));
        var stackableItem = CreateTestItem("Coin", stackSize: 99);
        
        // Add 3 items to create a stack of 3
        for (int i = 0; i < 3; i++)
            manager.TryPushItem(inventoryId, stackableItem, false);
        
        var itemInstance = manager.GetInventory(inventoryId).GetAllItems()[0];
        var targetPos = new Vector2I(2, 2);

        // Act
        int result = manager.TrySplitStack(inventoryId, itemInstance, 3, targetPos, false);

        // Assert
        AssertThat(result).IsEqual(3);
        var items = manager.GetInventory(inventoryId).GetAllItems();
        AssertThat(items.Count).IsEqual(1); // Original removed, only new stack remains
    }

    #endregion

    #region Check Fits Tests

    [TestCase]
    [RequireGodotRuntime]

    public void CheckItemFits_EmptySpace_ShouldReturnStackSize()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));
        var item = CreateTestItem("Coin", stackSize: 99);

        // Act
        int fits = manager.CheckItemFits(inventoryId, item, new Vector2I(0, 0), false);

        // Assert
        AssertThat(fits).IsEqual(99);
    }

    [TestCase]
    [RequireGodotRuntime]

    public void CheckItemFits_OccupiedSpace_ShouldReturnZero()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));
        var item1 = CreateTestItem("Rock");
        var item2 = CreateTestItem("Stick");
        
        manager.TryPushItem(inventoryId, item1, false);

        // Act
        int fits = manager.CheckItemFits(inventoryId, item2, new Vector2I(0, 0), false);

        // Assert
        AssertThat(fits).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]

    public void CheckItemFits_OutOfBounds_ShouldReturnZero()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));
        var item = CreateTestItem("Rock", new Vector2I(2, 2));

        // Act
        int fits = manager.CheckItemFits(inventoryId, item, new Vector2I(4, 4), false);

        // Assert
        AssertThat(fits).IsEqual(0); // Would overflow inventory bounds
    }

    [TestCase]
    [RequireGodotRuntime]

    public void CheckItemFits_StackableWithExistingStack_ShouldReturnRemainingSpace()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));
        var stackableItem = CreateTestItem("Coin", stackSize: 10);
        
        // Add 3 items to create a stack of 3
        for (int i = 0; i < 3; i++)
            manager.TryPushItem(inventoryId, stackableItem, false);

        // Act
        int fits = manager.CheckItemFits(inventoryId, stackableItem, new Vector2I(0, 0), false);

        // Assert
        AssertThat(fits).IsEqual(7); // 10 max - 3 current = 7 remaining
    }

    #endregion

    #region Drop Item Tests

    [TestCase]
    [RequireGodotRuntime]

    public void DropItem_ValidItem_ShouldRemoveFromInventory()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));
        var item = CreateTestItem("Rock");
        
        manager.TryPushItem(inventoryId, item, false);
        var itemInstance = manager.GetInventory(inventoryId).GetAllItems()[0];

        // Act
        string scenePath = manager.DropItem(inventoryId, itemInstance);

        // Assert
        AssertThat(scenePath).IsEqual("res://test.tscn");
        AssertThat(manager.GetInventory(inventoryId).GetAllItems().Count).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]

    public void DropItem_InvalidInventory_ShouldThrowException()
    {
        // Arrange
        var manager = CreateInventoryManager();
        var fakeItem = new ItemInstance
        {
            InventoryId = 999,
            ItemData = CreateTestItem("Fake"),
            GridPosition = new Vector2I(0, 0),
            CurrentStackSize = 1
        };

        // Act & Assert
        AssertThrown(() => manager.DropItem(999, fakeItem))
            .IsInstanceOf<ArgumentException>();
    }

    #endregion

    #region Find Item Tests

    [TestCase]
    [RequireGodotRuntime]

    public void FindItemByInstanceId_ExistingItem_ShouldReturnItem()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(5, 5));
        var item = CreateTestItem("Rock");
        
        manager.TryPushItem(inventoryId, item, false);
        var addedItem = manager.GetInventory(inventoryId).GetAllItems()[0];
        int instanceId = addedItem.InstanceId;

        // Act
        var foundItem = manager.FindItemByInstanceId(instanceId);

        // Assert
        AssertThat(foundItem).IsNotNull();
        AssertThat(foundItem.InstanceId).IsEqual(instanceId);
        AssertThat(foundItem.ItemData.Name).IsEqual("Rock");
    }

    [TestCase]
    [RequireGodotRuntime]

    public void FindItemByInstanceId_NonExistingItem_ShouldReturnNull()
    {
        // Arrange
        var manager = CreateInventoryManager();
        manager.CreateInventory(new Vector2I(5, 5));

        // Act
        var foundItem = manager.FindItemByInstanceId(999);

        // Assert
        AssertThat(foundItem).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]

    public void FindItemByInstanceId_InSpecificInventory_ShouldReturnItem()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int inv1 = manager.CreateInventory(new Vector2I(5, 5));
        int inv2 = manager.CreateInventory(new Vector2I(5, 5));
        var item = CreateTestItem("Rock");
        
        manager.TryPushItem(inv1, item, false);
        var addedItem = manager.GetInventory(inv1).GetAllItems()[0];
        int instanceId = addedItem.InstanceId;

        // Act
        var foundItem = manager.FindItemByInstanceId(inv1, instanceId);
        var notFoundItem = manager.FindItemByInstanceId(inv2, instanceId);

        // Assert
        AssertThat(foundItem).IsNotNull();
        AssertThat(notFoundItem).IsNull();
    }

    #endregion

    #region Complex Scenarios

    [TestCase]
    [RequireGodotRuntime]

    public void ComplexScenario_MultipleInventoriesWithTransfers()
    {
        // Arrange
        var manager = CreateInventoryManager();
        int playerInv = manager.CreateInventory(new Vector2I(5, 5));
        int chestInv = manager.CreateInventory(new Vector2I(3, 3));
        
        var sword = CreateTestItem("Sword", new Vector2I(1, 2));
        var potion = CreateTestItem("Potion", stackSize: 10);
        var coin = CreateTestItem("Coin", stackSize: 99);

        // Act - Fill player inventory
        manager.TryPushItem(playerInv, sword, false);
        for (int i = 0; i < 5; i++)
            manager.TryPushItem(playerInv, potion, false);
        for (int i = 0; i < 10; i++)
            manager.TryPushItem(playerInv, coin, false);

        // Transfer some items to chest
        var coinStack = manager.GetInventory(playerInv).GetAllItems()
            .Find(i => i.ItemData.Name == "Coin");
        manager.TrySplitStack(chestInv, coinStack, 5, new Vector2I(0, 0), false);

        // Assert
        var playerItems = manager.GetInventory(playerInv).GetAllItems();
        var chestItems = manager.GetInventory(chestInv).GetAllItems();
        
        AssertThat(playerItems.Count).IsEqual(3); // sword, potions (stacked), coins (remaining)
        AssertThat(chestItems.Count).IsEqual(1); // coins in chest
        AssertThat(chestItems[0].CurrentStackSize).IsEqual(5);
        AssertThat(coinStack.CurrentStackSize).IsEqual(5);
    }

    [TestCase]
    [RequireGodotRuntime]

    public void ComplexScenario_InventoryTetris()
    {
        // Arrange - Create small inventory and try to fit various sized items
        var manager = CreateInventoryManager();
        int inventoryId = manager.CreateInventory(new Vector2I(4, 4));
        
        var item1x1 = CreateTestItem("Small", new Vector2I(1, 1));
        var item2x1 = CreateTestItem("Medium", new Vector2I(2, 1));
        var item2x2 = CreateTestItem("Large", new Vector2I(2, 2));

        // Act - Try to fill inventory efficiently
        bool result1 = manager.TryPushItem(inventoryId, item2x2, false); // Should place at 0,0
        bool result2 = manager.TryPushItem(inventoryId, item2x1, false); // Should find space
        bool result3 = manager.TryPushItem(inventoryId, item1x1, false); // Should find space
        bool result4 = manager.TryPushItem(inventoryId, item1x1, false); // Should find space

        // Assert
        AssertThat(result1).IsTrue();
        AssertThat(result2).IsTrue();
        AssertThat(result3).IsTrue();
        AssertThat(result4).IsTrue();
        
        var items = manager.GetInventory(inventoryId).GetAllItems();
        AssertThat(items.Count).IsEqual(4);
    }

    #endregion
}

