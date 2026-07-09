shader_type spatial;
render_mode blend_mix, depth_prepass_alpha, cull_back;

uniform vec4 water_color : source_color = vec4(0.08, 0.28, 0.46, 0.88);
uniform vec4 foam_color : source_color = vec4(0.85, 0.94, 1.0, 0.95);
uniform float flow_speed = 0.46;
uniform float wave_scale = 0.55;
uniform float wave_strength = 0.42;
uniform float edge_fade = 0.14;
uniform float foam_scale = 0.38;
uniform float metallic = 0.15;
uniform float roughness = 0.08;
uniform float wave_height = 0.15;        // Physical displacement height

varying vec3 world_pos;

// Hash function for procedural noise
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

// Procedural Value Noise
float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i + vec2(0.0,0.0)), hash(i + vec2(1.0,0.0)), u.x),
               mix(hash(i + vec2(0.0,1.0)), hash(i + vec2(1.0,1.0)), u.x), u.y);
}

// Fractional Brownian Motion (FBM) representing multi-frequency waves
float fbm(vec2 p, float time) {
    vec2 flow = vec2(0.0, time * flow_speed);
    float value = 0.0;
    float amplitude = 0.5;
    mat2 rot = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8)); // Rotation to prevent grid alignment
    
    // 4 Octaves of noise for detailed ripples
    for (int i = 0; i < 4; i++) {
        value += amplitude * noise(p + flow);
        p = rot * p * 2.0 - flow * 0.4;
        amplitude *= 0.5;
    }
    return value;
}

// Primary wave function for vertex displacement and low frequency motion
float get_primary_wave(vec2 pos, float time) {
    vec2 p = pos * wave_scale;
    float w1 = sin(p.y - time * 2.6 + p.x * 0.5) * 0.45;
    float w2 = cos(p.y * 1.3 + time * 3.2 - p.x * 0.7) * 0.35;
    float w3 = sin((p.x * 1.2 + p.y * 0.9) - time * 1.8) * 0.20;
    return w1 + w2 + w3;
}

void vertex() {
    // 1. Transform vertex from View space to World space
    vec3 world_vertex = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    
    // 2. Compute wave displacement based on world xz coordinates
    float flow = TIME * flow_speed;
    float wave = get_primary_wave(world_vertex.xz, flow);
    
    // 3. Apply displacement along the world Y axis
    world_vertex.y += wave * wave_height;
    
    // 4. Set world_pos for fragment shader
    world_pos = world_vertex;
    
    // 5. Transform back to View space and write to VERTEX
    VERTEX = (VIEW_MATRIX * vec4(world_vertex, 1.0)).xyz;
}

void fragment() {
    float flow = TIME * flow_speed;
    vec2 pos = world_pos.xz;
    
    // Wave slope perturbation via FBM noise finite difference
    float eps = 0.08;
    float h_l = fbm(pos - vec2(eps, 0.0), flow);
    float h_r = fbm(pos + vec2(eps, 0.0), flow);
    float h_d = fbm(pos - vec2(0.0, eps), flow);
    float h_u = fbm(pos + vec2(0.0, eps), flow);
    
    vec3 world_normal = normalize(vec3((h_l - h_r) * wave_strength, 1.0, (h_d - h_u) * wave_strength));
    
    // View space normal for rendering
    NORMAL = normalize(mat3(VIEW_MATRIX) * world_normal);
    
    // Detailed procedural foam on wave crests
    float wave_val = fbm(pos * 0.8, flow);
    float wave_01 = wave_val;
    float foam = smoothstep(0.55, 0.85, wave_01);
    
    // Shore line transition
    float edge_x = min(UV.x, 1.0 - UV.x);
    float edge_y = min(UV.y, 1.0 - UV.y);
    float edge = 1.0 - smoothstep(0.0, edge_fade, min(edge_x, edge_y));
    
    // Fresnel Reflection
    float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
    
    // Shallow/deep water color variation
    vec3 deep_color = water_color.rgb;
    vec3 shallow_color = mix(deep_color, vec3(0.05, 0.38, 0.58), wave_val * 0.3 + 0.3);
    
    // Blend components
    vec3 final_color = mix(shallow_color, foam_color.rgb, foam * foam_scale + edge * 0.45);
    final_color = mix(final_color, foam_color.rgb * 1.12, fresnel * 0.36);
    
    ALBEDO = final_color;
    ALPHA = mix(water_color.a, 1.0, edge * 0.5);
    ROUGHNESS = roughness + (1.0 - foam) * 0.14;
    METALLIC = metallic;
    SPECULAR = 0.55 + foam * 0.25;
}
