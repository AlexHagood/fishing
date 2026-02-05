using Godot;

public static class Extensions
{
    /// <summary>
    /// Returns a new Vector2I with X and Y swapped.
    /// </summary>
    public static Vector2I Flip(this Vector2I v)
    {
        return new Vector2I(v.Y, v.X);
    }

    public static Vector2 Flip(this Vector2 v)
    {
        return new Vector2(v.Y, v.X);
    }
}