shader_type spatial;
render_mode unshaded, depth_draw_always, cull_disabled;

// Depth texture for occlusion detection
uniform sampler2D DEPTH_TEXTURE : hint_depth_texture, filter_linear_mipmap;

// Uniforms passed from C#
uniform vec3 start_pos;
uniform vec3 end_pos;
uniform float slack_amount = 0.0;

void vertex() {
    // UV.y goes from 0 (bottom) to 1 (top) on a cylinder
    float t = UV.y;
    
    // Interpolate between start and end positions (in world space)
    vec3 world_pos = mix(start_pos, end_pos, t);
    
    // Transform from world space to view space for rendering
    // MODEL_MATRIX transforms from local to world, so inverse transforms world to local
    vec3 local_pos = (inverse(MODEL_MATRIX) * vec4(world_pos, 1.0)).xyz;
    
    VERTEX = local_pos;
}

void fragment() {
    // Temporarily just make it bright green to see if it renders at all
    ALBEDO = vec3(0.0, 1.0, 0.0); // Bright green
    ALPHA = 1.0;
}

