using Godot;
using System;

public partial class DayNightCycle : Node3D
{
    [Export] public float CycleDuration = 10.0f; // 60 seconds for full day/night cycle
    [Export] public Texture2D DaySkyboxTexture; // The EXR texture
    [Export] public Color DayHorizonColor = new Color(0.85f, 0.9f, 1.0f); // Bright horizon
    [Export] public Color DayZenithColor = new Color(0.4f, 0.6f, 0.9f); // Blue sky
    [Export] public Color DayGroundColor = new Color(0.6f, 0.55f, 0.5f); // Ground reflection
    [Export] public Color DayAmbientColor = new Color(0.4f, 0.45f, 0.5f);
    [Export] public Color NightAmbientColor = new Color(0.15f, 0.18f, 0.25f);
    [Export] public float DaySunEnergy = 1.0f;
    [Export] public float NightMoonEnergy = 0.8f; // 80% of daytime brightness for gameplay
    [Export] public float TransitionDuration = 0.2f; // Fraction of cycle for sunrise/sunset (0.0 to 0.5)
    
    private DirectionalLight3D _sun;
    private DirectionalLight3D _moon;
    private WorldEnvironment _worldEnvironment;
    private Godot.Environment _environment;
    private ShaderMaterial _nightSkyMaterial;
    private PanoramaSkyMaterial _daySkyMaterial;
    private Sky _sky;
    
    private float _timeOfDay = 0.0f; // 0 = midnight, 0.5 = noon
    private bool _isDay = false;
    
    public override void _Ready()
    {
        // Get references
        _worldEnvironment = GetNode<WorldEnvironment>("WorldEnvironment");
        _sun = GetNode<DirectionalLight3D>("SunLight");
        // Moon directional light (opposite the sun)
        _moon = GetNodeOrNull<DirectionalLight3D>("MoonLight");
        
        if (_worldEnvironment != null)
        {
            _environment = _worldEnvironment.Environment;
            if (_environment != null)
            {
                _sky = _environment.Sky;
                if (_sky != null && _sky.SkyMaterial is ShaderMaterial)
                {
                    _nightSkyMaterial = (ShaderMaterial)_sky.SkyMaterial;
                }
            }
        }
        
        // Create day skybox material
        if (DaySkyboxTexture != null)
        {
            _daySkyMaterial = new PanoramaSkyMaterial();
            _daySkyMaterial.Panorama = DaySkyboxTexture;
            // Set day sky gradient colors in the shader
            if (_nightSkyMaterial != null)
            {
                _nightSkyMaterial.SetShaderParameter("day_horizon_color", DayHorizonColor);
                _nightSkyMaterial.SetShaderParameter("day_zenith_color", DayZenithColor);
                _nightSkyMaterial.SetShaderParameter("day_ground_color", DayGroundColor);
            }
        }
        else
        {
            GD.PrintErr("DayNightCycle: No day skybox texture assigned (optional for color reference)");
        }
        
        // Start at sunrise
        _timeOfDay = 0.0f;
        _isDay = true;
        UpdateCycle();
    }
    
    public override void _Process(double delta)
    {
        // Advance time
        _timeOfDay += (float)delta / CycleDuration;
        if (_timeOfDay >= 1.0f)
        {
            _timeOfDay -= 1.0f;
        }
        
        UpdateCycle();
    }
    
    private void UpdateCycle()
    {
        // Calculate sun angle (0 = midnight, 0.5 = noon)
        float sunAngle = _timeOfDay * Mathf.Tau; // Full rotation
        
        // Position sun/moon
        Vector3 sunDirection = new Vector3(
            Mathf.Cos(sunAngle),
            Mathf.Sin(sunAngle),
            0.0f
        );
        
        // Rotate the sun around the world
        if (_sun != null)
        {
            // Make sun look at the origin from its position
            Vector3 sunPosition = -sunDirection * 100.0f;
            _sun.LookAtFromPosition(sunPosition, Vector3.Zero, Vector3.Back);
        }
        
        // Determine if it's day or night
        // Simplified: 0.0 = sunrise, 0.5 = sunset, 1.0 = sunrise again
        
        // TransitionDuration is now a fraction (0.0 to 0.5)
        float transitionTime = Mathf.Clamp(TransitionDuration, 0.0f, 0.5f);
        float dayBlend = 0.0f;
        
        // Define key time points
        float sunriseEnd = transitionTime;                    // End of sunrise
        float sunsetStart = 0.5f - transitionTime;             // Start of sunset
        
        // 0.0 to 0.5 = day period
        if (_timeOfDay < 0.5f)
        {
            // Sunrise transition
            if (_timeOfDay < sunriseEnd)
            {
                float t = _timeOfDay / transitionTime;
                dayBlend = Mathf.SmoothStep(0.0f, 1.0f, t);
            }
            // Full day
            else if (_timeOfDay < sunsetStart)
            {
                dayBlend = 1.0f;
            }
            // Sunset transition
            else
            {
                float t = (_timeOfDay - sunsetStart) / transitionTime;
                dayBlend = Mathf.SmoothStep(1.0f, 0.0f, t);
            }
        }
        // 0.5 to 1.0 = night period
        else
        {
            dayBlend = 0.0f;
        }
        
        _isDay = _timeOfDay < 0.5f;
        
        // Always keep the shader-based sky assigned (it will blend day texture internally)
        if (_sky != null && _nightSkyMaterial != null)
        {
            _sky.SkyMaterial = _nightSkyMaterial;
        }

        // Push day_blend to shader
        if (_nightSkyMaterial != null)
        {
            _nightSkyMaterial.SetShaderParameter("day_blend", dayBlend);
        }
        
        // Use single light source - always at full energy, positioned at whichever body is above horizon
        // During day: sun position, during night: moon position (opposite side)
        Vector3 primaryLightDir = sunDirection;
        Color primaryLightColor = new Color(1.0f, 0.98f, 0.95f); // Warm sunlight
        
        // During night, use moon direction (opposite of sun) and cool color
        if (dayBlend < 0.5f)
        {
            primaryLightDir = -sunDirection; // Moon is opposite
            primaryLightColor = new Color(0.9f, 0.95f, 1.0f); // Cool moonlight
        }
        
        // Update primary light (sun)
        if (_sun != null)
        {
            Vector3 lightPosition = -primaryLightDir * 100.0f;
            _sun.LookAtFromPosition(lightPosition, Vector3.Zero, Vector3.Back);
            _sun.LightEnergy = 1.0f; // Always full energy
            _sun.LightColor = primaryLightColor;
        }

        // Disable moon light completely - we only use one light now
        if (_moon != null)
        {
            _moon.LightEnergy = 0.0f;
        }
        
        // Update ambient lighting
        if (_environment != null)
        {
            float ambientBlend = dayBlend; // use same blend
            _environment.AmbientLightColor = DayAmbientColor.Lerp(NightAmbientColor, 1.0f - ambientBlend);
            // Keep night much brighter for gameplay - actually make it brighter than day!
            _environment.AmbientLightEnergy = Mathf.Lerp(1.2f, 0.5f, ambientBlend); // Night = 1.2, Day = 0.5
        }
        
        // Update moon direction in shader if it's night
        if (_nightSkyMaterial != null)
        {
            // Provide moon direction to shader (moon is opposite the sun)
            _nightSkyMaterial.SetShaderParameter("moon_direction", -sunDirection);
        }
        
        // Debug output
        if (Engine.GetProcessFrames() % 60 == 0)
        {
            GD.Print($"Time: {_timeOfDay:F2}, DayBlend: {dayBlend:F2}, LightEnergy: {_sun?.LightEnergy:F2}, AmbientEnergy: {_environment?.AmbientLightEnergy:F2}");
        }
    }
    
    // Helper method to set time of day (0 = midnight, 0.5 = noon)
    public void SetTimeOfDay(float time)
    {
        _timeOfDay = Mathf.Clamp(time, 0.0f, 1.0f);
        UpdateCycle();
    }
    
    // Get current time of day
    public float GetTimeOfDay()
    {
        return _timeOfDay;
    }
}
