shader_type spatial;
render_mode blend_mix, depth_prepass_alpha, cull_back;

uniform vec4 water_color : source_color = vec4(0.04, 0.28, 0.52, 0.80);
uniform vec4 foam_color : source_color = vec4(0.86, 0.96, 1.0, 1.0);
uniform float flow_speed = 0.24;
uniform float ripple_scale = 8.5;
uniform float ripple_strength = 0.028;
uniform float edge_fade = 0.08;
uniform float shine_strength = 0.05;

void fragment() {
    vec2 uv = UV;
    float flow = TIME * flow_speed;
    vec2 flow_uv = vec2(uv.x * ripple_scale * 0.55, uv.y * ripple_scale);
    float wave_a = sin((flow_uv.y + flow) * 6.2831853);
    float wave_b = cos((flow_uv.y * 1.42 - flow * 1.35) * 6.2831853);
    float wave_c = sin(((flow_uv.y + flow_uv.x * 0.22) + flow * 0.85) * 6.2831853);
    float ripple = (wave_a * 0.5 + wave_b * 0.35 + wave_c * 0.15);
    float ripple01 = ripple * 0.5 + 0.5;
    float foam = smoothstep(0.44, 0.82, ripple01);

    float edge_x = min(uv.x, 1.0 - uv.x);
    float edge_y = min(uv.y, 1.0 - uv.y);
    float edge = 1.0 - smoothstep(0.0, edge_fade, min(edge_x, edge_y));
    float center_band = 1.0 - smoothstep(0.18, 0.46, abs(uv.x - 0.5));

    vec3 base = water_color.rgb;
    vec3 color = mix(base, foam_color.rgb, foam * 0.28 + edge * 0.34 + center_band * 0.18);

    ALBEDO = color;
    ALPHA = water_color.a;
    ROUGHNESS = clamp(0.18 - (foam + center_band * 0.8) * shine_strength, 0.03, 0.28);
    METALLIC = 0.0;
    SPECULAR = 0.58 + foam * 0.24 + center_band * 0.14;
    EMISSION = foam_color.rgb * (foam * 0.022 + edge * 0.012 + center_band * 0.018);
}
