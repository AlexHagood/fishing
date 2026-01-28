using Godot;

public partial class Character : CharacterBody3D
{
    [Signal]
    public delegate void InventoryRequestedEventHandler(int inventoryId);

    [Export] public float Speed = 5.0f;
    [Export] public float JumpVelocity = 8.0f;
    [Export] public float Gravity = 20.0f;
    [Export] public float MouseSensitivity = 0.01f;

    private bool isOnFloor = false;
    private Vector2 mouseDelta = Vector2.Zero;

    private float _baseSpeed = 5.0f;
    private float _sprintSpeed = 10.0f;
    private bool _isSprinting = false;

    private Camera3D camera;
    private Node3D _holdPosition;
    private PhysItem _heldPhysItem;

    private InventoryManager _inventoryManager;

    public int inventoryId;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        camera = GetNode<Camera3D>("Camera3D");
        
        // Create hold position for physics items
        _holdPosition = new Node3D();
        _holdPosition.Name = "HoldPosition";
        camera.AddChild(_holdPosition);
        _holdPosition.Position = new Vector3(0, -0.5f, -2.0f);
        
        _inventoryManager = GetNode<InventoryManager>("/root/InventoryManager");

        inventoryId = _inventoryManager.CreateInventory(new Vector2I(5, 5));
        

    }

    public void OpenInventory(int id)
    {
        GD.Print("Opening inventory with Id " + id);
        EmitSignal(SignalName.InventoryRequested, id);
    }

    public override void _Process(double delta)
    {
        // Apply floaty physics to held physics item
        if (_heldPhysItem != null && _holdPosition != null)
        {
            _heldPhysItem.ApplyFloatyPhysics(_holdPosition.GlobalPosition, (float)delta);
        }
    }

    public override void _Input(InputEvent @event)
    {        
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode != Input.MouseModeEnum.Visible)
        {
            mouseDelta = mouseMotion.Relative;
        }

        // Handle mouse clicks for throw/drop physics items
        if (@event is InputEventMouseButton mouseButton)
        {
            // Don't process item actions in menu mode
            
            
            // Left mouse button - throw
            if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
            {
                if (_heldPhysItem != null)
                {
                    ThrowPhysItem();
                }
            }
            // Right mouse button - drop
            else if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
            {
                if (_heldPhysItem != null)
                {
                    DropPhysItem();
                }
            }
        }

        // Tab or I key - toggle inventory
        if (Input.IsActionJustPressed("inventory"))
        {
            GD.Print($"Trying to open inventory {inventoryId}");
            OpenInventory(inventoryId);
            return;
        }

        // E key - interact with WorldItem (don't allow in menu mode)
        if (Input.IsActionJustPressed("interact"))
        {
            TryInteractE();
        }

        // F key - interact with WorldItem (don't allow in menu mode)
        if (Input.IsActionJustPressed("pickup"))
        {
            TryInteractF();
        }
        
        // Sprint input handling (don't allow in menu mode)
        if (Input.IsActionPressed("sprint"))
        {
            _isSprinting = true;
        }
        else
        {
            _isSprinting = false;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector3 direction = Vector3.Zero;

        if (Input.IsActionPressed("fwd"))
        {
            direction -= Transform.Basis.Z;
        }
        if (Input.IsActionPressed("back"))
        {
            direction += Transform.Basis.Z;
        }
        if (Input.IsActionPressed("left"))
        {
            direction -= Transform.Basis.X;
        }
        if (Input.IsActionPressed("right"))
        {
            direction += Transform.Basis.X;
        }

        direction = direction.Normalized();

        // Apply movement speed
        float currentSpeed = _isSprinting ? _sprintSpeed : _baseSpeed;
        Velocity = new Vector3(
            direction.X * currentSpeed,
            Velocity.Y, // preserve vertical component for gravity/jump
            direction.Z * currentSpeed
        );

        // Apply gravity
        if (!isOnFloor)
            Velocity = new Vector3(Velocity.X, Velocity.Y - Gravity * (float)delta, Velocity.Z);

        // Handle jump
        if (isOnFloor && Input.IsActionJustPressed("jump"))
            Velocity = new Vector3(Velocity.X, JumpVelocity, Velocity.Z);

        // Move the character
        MoveAndSlide();
        isOnFloor = IsOnFloor();

        // Push RigidBody objects when colliding
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);
            var collider = collision.GetCollider();
            
            if (collider is PhysItem rigidBody)
            {
                // Get the collision normal (direction away from the collider)
                Vector3 pushDirection = -collision.GetNormal();
                
                // Calculate push force based on velocity
                Vector3 pushVelocity = pushDirection * Velocity.Length();
                
                // Apply force (not impulse) - this is continuous pushing
                float pushPower = 5.0f; // Adjust this value for push strength
                rigidBody.ApplyCentralForce(pushVelocity * pushPower);
            }
        }

        // Handle mouse look
        RotateY(-mouseDelta.X * MouseSensitivity);
        camera.RotateX(-mouseDelta.Y * MouseSensitivity);
        mouseDelta = Vector2.Zero;

        // Clamp camera rotation
        var rotation = camera.RotationDegrees;
        rotation.X = Mathf.Clamp(rotation.X, -90, 90);
        camera.RotationDegrees = rotation;
    }

    private GodotObject RaycastFromCamera(float range = 5.0f)
    {
        var spaceState = GetWorld3D().DirectSpaceState;
        var from = camera.GlobalTransform.Origin;
        var to = from + camera.GlobalTransform.Basis.Z * -range;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var result = spaceState.IntersectRay(query);
        
        if (result.Count > 0 && result.ContainsKey("collider"))
        {
            return result["collider"].AsGodotObject();
        }
        return null;
    }

    private void TryInteractE()
    {
        var collider = RaycastFromCamera(5.0f);
        if (collider == null)
            return;

        // Look for WorldItem in the parent chain
        Node nodeToCheck = collider as Node;
        while (nodeToCheck != null)
        {
            if (nodeToCheck is WorldItem worldItem && worldItem.CanInteract())
            {
                float distance = GlobalPosition.DistanceTo(worldItem.GlobalPosition);
                
                if (distance <= worldItem.InteractRange)
                {
                    worldItem.InteractE(this);
                }
                else
                {
                    GD.Print($"[Character] Too far away: {distance:F1}m");
                }
                return;
            }
            nodeToCheck = nodeToCheck.GetParent();
        }
    }

    private void TryInteractF()
    {
        var collider = RaycastFromCamera(5.0f);
        if (collider == null)
            return;

        // Look for WorldItem in the parent chain
        Node nodeToCheck = collider as Node;
        while (nodeToCheck != null)
        {
            if (nodeToCheck is WorldItem worldItem && worldItem.CanInteract())
            {
                float distance = GlobalPosition.DistanceTo(worldItem.GlobalPosition);
                
                if (distance <= worldItem.InteractRange)
                {
                    worldItem.InteractF(this);
                }
                else
                {
                    GD.Print($"[Character] Too far away: {distance:F1}m");
                }
                return;
            }
            nodeToCheck = nodeToCheck.GetParent();
        }
    }

    public void PickupPhysItem(PhysItem physItem)
    {
        if (_heldPhysItem != null)
        {
            GD.Print("[Character] Already holding an item!");
            return;
        }

        _heldPhysItem = physItem;
        _heldPhysItem.OnPickedUp();
        GD.Print($"[Character] Picked up: {physItem.InvItemData.Name}");
    }

    private void DropPhysItem()
    {
        if (_heldPhysItem == null) return;

        var itemToDrop = _heldPhysItem;
        _heldPhysItem = null;    
        itemToDrop.OnDropped();
        GD.Print($"[Character] Dropped: {itemToDrop.Name}");
    }

    private void ThrowPhysItem()
    {
        if (_heldPhysItem == null) return;

        var throwDirection = -camera.GlobalTransform.Basis.Z;
        var itemToThrow = _heldPhysItem;
        var throwForce = _heldPhysItem.ThrowForce;
        
        // Clear the reference FIRST so floaty physics stops applying
        _heldPhysItem = null;
        
        // Then throw the item
        itemToThrow.OnThrown(throwDirection, throwForce);
        GD.Print($"[Character] Threw: {itemToThrow.Name}");
    }
}
