using Godot;
using System;

public partial class Dialog : Panel
{
	// Called when the node enters the scene tree for the first time.
	private Label DialogText;
	private Tween tween;

	public override void _Ready()
    {
        DialogText = GetNode<Label>("Label");
		// Start with the dialog fully transparent
		Modulate = new Color(Modulate, 0.0f);
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void SendMessage(string message)
	{
		GD.Print($"Dialog: {message}");
		DialogText.Text = message;
		
		// Kill existing tween if it's still running
		if (tween != null && tween.IsValid())
		{
			tween.Kill();
		}
		
		// Reset Dialog panel opacity to fully visible
		Modulate = new Color(Modulate, 1.0f);
		
		// Create a new tween each time
		tween = CreateTween();
		tween.SetTrans(Tween.TransitionType.Circ);
		// Fade out the Dialog panel over 1 second
		tween.TweenProperty(this, "modulate:a", 0.0f, 2.0f);
	}
}
