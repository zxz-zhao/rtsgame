shader_type spatial;

uniform vec4 bg_color : source_color = vec4(0.07, 0.10, 0.14, 1.0);
uniform vec4 grid_color : source_color = vec4(0.14, 0.36, 0.52, 0.65);
uniform vec4 border_color : source_color = vec4(0.0, 0.85, 1.0, 0.85);
uniform float grid_scale = 4.0;

void fragment() {
    // 战术地面网格计算 (4 米间距)
    vec2 uv = UV * 160.0 / grid_scale;
    vec2 grid = abs(fract(uv - 0.5) - 0.5) / fwidth(uv);
    float line = min(grid.x, grid.y);
    float grid_mask = 1.0 - clamp(line, 0.0, 1.0);
    
    // 战场外围战术边界辉光 (160x160 边框)
    vec2 norm_uv = abs(UV - 0.5) * 2.0;
    float border_dist = max(norm_uv.x, norm_uv.y);
    float border_mask = smoothstep(0.92, 0.98, border_dist);
    
    vec4 col = mix(bg_color, grid_color, grid_mask);
    col = mix(col, border_color, border_mask * 0.7);
    
    ALBEDO = col.rgb;
    ROUGHNESS = 0.55;
    METALLIC = 0.35;
    EMISSION = (grid_color.rgb * grid_mask * 0.2) + (border_color.rgb * border_mask * 0.5);
}
