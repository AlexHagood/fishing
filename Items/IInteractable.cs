using Godot;

/// <summary>
/// Interface for any object that can be interacted with by the player.
/// Implement this interface to make objects interactive (E and F keys).
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Hint text shown for E key interaction
    /// </summary>
    string HintE { get; }

    /// <summary>
    /// Hint text shown for F key interaction
    /// </summary>
    string HintF { get; }

    /// <summary>
    /// Maximum distance from which the player can interact with this object
    /// </summary>
    float InteractRange { get; }

    /// <summary>
    /// Called when player presses E key while looking at this object
    /// </summary>
    void InteractE(Character character);

    /// <summary>
    /// Called when player presses F key while looking at this object
    /// </summary>
    void InteractF(Character character);

    /// <summary>
    /// Check if the object can currently be interacted with
    /// </summary>
    bool CanInteract();
}
