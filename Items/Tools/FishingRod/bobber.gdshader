shader_type spatial;

void fragment() {
    // UV.y goes from 0 (bottom) to 1 (top)
    if (UV.y > 0.5) {
        ALBEDO = vec3(1.0, 0.0, 0.0); // red top
    } else {
        ALBEDO = vec3(1.0, 1.0, 1.0); // white bottom
    }
}
