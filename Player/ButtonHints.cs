using Godot;
using System;

public partial class ButtonHints : VBoxContainer
{
    private Label _labelE;
    private Label _labelF;
    private Control _nodeE;
    private Control _nodeF;
    
    private string _hintE = "";
    public string HintE 
    { 
        get => _hintE;
        set
        {
            _hintE = value;
            if (_labelE != null)
            {
                _labelE.Text = value;
            }
        }
    }
    
    private string _hintF = "";
    public string HintF 
    { 
        get => _hintF;
        set
        {
            _hintF = value;
            if (_labelF != null)
            {
                _labelF.Text = value;
            }
        }
    }

    private bool _visibleE = false;
    public bool VisibleE
    {
        get => _visibleE;
        set
        {
            _visibleE = value;
            if (_nodeE != null)
            {
                _nodeE.Visible = value;
            }
        }
    }

    private bool _visibleF = false;
    public bool VisibleF
    {
        get => _visibleF;
        set
        {
            _visibleF = value;
            if (_nodeF != null)
            {
                _nodeF.Visible = value;
            }
        }
    }
    
    public override void _Ready()
    {
        _nodeF = GetNode<Control>("F");
        _nodeE = GetNode<Control>("E");
        _labelF = GetNode<Label>("F/Label");
        _labelE = GetNode<Label>("E/Label");
        
        // Apply any values that were set before _Ready
        _labelE.Text = _hintE;
        _labelF.Text = _hintF;
        _nodeE.Visible = _visibleE;
        _nodeF.Visible = _visibleF;
    }
}

