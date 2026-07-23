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
// ripple_scale: controls UV tiling frequency of the flowing ripple pattern
uniform float ripple_scale = 12.5;
// ripple_strength: controls how strongly ripple normals perturb the surface
uniform float ripple_strength = 0.15;
// shine_strength: extra specular highlight intensity on ripple crests
uniform float shine_strength = 0.20;
uniform float wave_height = 0.0;   // Keep 0 to prevent terrain clipping / Z-fighting

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

// Fractional Brownian Motion (FBM) – multi-frequency wave texture
float fbm(vec2 p, float time) {
    vec2 flow = vec2(0.0, time * flow_speed);
    float value = 0.0;
    float amplitude = 0.5;
    mat2 rot = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));

    for (int i = 0; i < 4; i++) {
        value += amplitude * noise(p + flow);
        p = rot * p * 2.0 - flow * 0.4;
        amplitude *= 0.5;
    }
    return value;
}

// ──────────────────────────────────────────────────────────────────────────
// Flowing ripple pattern: two overlapping scrolling sine waves oriented
// along the downstream direction (world Z axis), shifted by 90° so that
// the combination creates a convincing "wind-blown / current-driven" look.
// Both layers scroll with flow_speed so they always move together.
// ──────────────────────────────────────────────────────────────────────────
float ripple(vec2 pos, float time) {
    float t = time * flow_speed;
    float freq = ripple_scale;

    // Layer A – primary downstream ripples (along Z)
    float layerA = sin(pos.y * freq       - t * 3.8 + pos.x * freq * 0.18) * 0.55
                 + sin(pos.y * freq * 1.7 - t * 5.1 - pos.x * freq * 0.26) * 0.30
                 + sin(pos.y * freq * 2.8 - t * 2.4 + pos.x * freq * 0.09) * 0.15;

    // Layer B – diagonal cross-ripples for natural "breeze" effect
    float layerB = sin((pos.x * 0.6 + pos.y) * freq * 0.9 - t * 4.2) * 0.40
                 + sin((pos.x * 0.4 - pos.y) * freq * 1.3 + t * 3.0) * 0.25
                 + sin( pos.x       * freq * 2.2            - t * 6.5) * 0.12;

    return (layerA * 0.65 + layerB * 0.35) * 0.5 + 0.5; // remap to [0,1]
}

void vertex() {
    vec3 world_vertex = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_pos = world_vertex;
    VERTEX = (VIEW_MATRIX * vec4(world_vertex, 1.0)).xyz;
}

void fragment() {
    float flow = TIME * flow_speed;
    vec2 pos = world_pos.xz;

    // ── Base FBM wave normals (low-frequency chop) ──
    float eps = 0.08;
    float h_l = fbm(pos - vec2(eps, 0.0), flow);
    float h_r = fbm(pos + vec2(eps, 0.0), flow);
    float h_d = fbm(pos - vec2(0.0, eps), flow);
    float h_u = fbm(pos + vec2(0.0, eps), flow);
    vec3 base_normal = normalize(vec3((h_l - h_r) * wave_strength, 1.0, (h_d - h_u) * wave_strength));

    // ── Flowing ripple normal perturbation (high-frequency direction-aware) ──
    float reps = 0.04;
    float r_l = ripple(pos - vec2(reps, 0.0), TIME);
    float r_r = ripple(pos + vec2(reps, 0.0), TIME);
    float r_d = ripple(pos - vec2(0.0, reps), TIME);
    float r_u = ripple(pos + vec2(0.0, reps), TIME);
    vec3 ripple_normal = normalize(vec3((r_l - r_r) * ripple_strength * 2.0,
                                         1.0,
                                        (r_d - r_u) * ripple_strength * 2.0));

    // Blend the two normal layers
    vec3 world_normal = normalize(base_normal * 0.55 + ripple_normal * 0.45);
    NORMAL = normalize(mat3(VIEW_MATRIX) * world_normal);

    // ── Ripple crest highlights ──
    float ripple_val = ripple(pos, TIME);
    float crest = smoothstep(0.60, 0.90, ripple_val);

    // ── Foam on wave crests (FBM) ──
    float wave_val = fbm(pos * 0.8, flow);
    float foam = smoothstep(0.55, 0.85, wave_val);

    // ── Shore-line transition ──
    float edge_x = min(UV.x, 1.0 - UV.x);
    float edge_y = min(UV.y, 1.0 - UV.y);
    float edge = 1.0 - smoothstep(0.0, edge_fade, min(edge_x, edge_y));

    // ── Fresnel Reflection ──
    float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);

    // ── Color composition ──
    vec3 deep_color    = water_color.rgb;
    vec3 shallow_color = mix(deep_color, vec3(0.05, 0.38, 0.58), wave_val * 0.3 + 0.3);

    vec3 final_color = mix(shallow_color, foam_color.rgb, foam * foam_scale + edge * 0.45);
    // Ripple-crest brightening (simulates sunlight catching moving water)
    final_color += foam_color.rgb * crest * shine_strength;
    final_color = mix(final_color, foam_color.rgb * 1.12, fresnel * 0.36);

    ALBEDO    = final_color;
    ALPHA     = mix(water_color.a, 1.0, edge * 0.5);
    ROUGHNESS = clamp(roughness + (1.0 - foam) * 0.03 - crest * 0.02, 0.01, 0.10);
    METALLIC  = metallic;
    SPECULAR  = 0.75 + foam * 0.20 + crest * shine_strength * 0.4;
}
