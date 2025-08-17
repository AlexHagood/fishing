using Godot;
using System;

public partial class InvTile : Panel
{
    public Vector2 InvPos;

    public InvItem item = null;

    public InvTile(Vector2 pos)
    {
        InvPos = pos;
    }

    
}