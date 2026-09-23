shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, diffuse_burley, specular_schlick_ggx;

// Core Material Uniforms
uniform vec4 water_color : source_color = vec4(0.06, 0.28, 0.48, 0.58);
uniform vec4 foam_color : source_color = vec4(0.88, 0.96, 1.0, 0.92);
uniform float flow_speed = 0.46;
uniform float wave_scale = 0.55;
uniform float wave_strength = 0.42;
uniform float edge_fade = 0.14;
uniform float foam_scale = 0.38;
uniform float metallic = 0.15;
uniform float roughness = 0.08;
uniform float ripple_scale = 12.5;
uniform float ripple_strength = 0.15;
uniform float shine_strength = 0.20;
uniform float wave_height = 0.25;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform sampler2D depth_texture : hint_depth_texture, repeat_disable, filter_nearest;

varying vec3 world_pos;
varying float wave_elevation;

// 3D Gerstner Wave Component
vec3 gerstner_wave(vec4 wave, vec3 p, inout vec3 tangent, inout vec3 binormal, float time) {
	float steepness = wave.z;
	float wavelength = wave.w;
	float k = 2.0 * 3.14159265 / wavelength;
	float c = sqrt(9.8 / k);
	vec2 d = normalize(wave.xy);
	float f = k * (dot(d, p.xz) - c * time * flow_speed * 1.5);
	float a = steepness / k;

	tangent += vec3(
		-d.x * d.x * (steepness * sin(f)),
		d.x * (steepness * cos(f)),
		-d.x * d.y * (steepness * sin(f))
	);
	binormal += vec3(
		-d.x * d.y * (steepness * sin(f)),
		d.y * (steepness * cos(f)),
		-d.y * d.y * (steepness * sin(f))
	);

	return vec3(
		d.x * (a * cos(f)),
		a * sin(f) * wave_height,
		d.y * (a * cos(f))
	);
}

void vertex() {
	vec3 world_vertex = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 grid_point = world_vertex;
	vec3 tangent = vec3(1.0, 0.0, 0.0);
	vec3 binormal = vec3(0.0, 0.0, 1.0);
	vec3 p = grid_point;

	// Superimpose 4 Gerstner wave vectors for dynamic 3D surface movement
	vec4 w1 = vec4(1.0, 0.3, 0.15, 12.0 * wave_scale);
	vec4 w2 = vec4(0.3, 1.0, 0.2, 7.0 * wave_scale);
	vec4 w3 = vec4(-0.7, 0.5, 0.1, 4.0 * wave_scale);
	vec4 w4 = vec4(0.5, -0.8, 0.08, 2.5 * wave_scale);

	p += gerstner_wave(w1, grid_point, tangent, binormal, TIME);
	p += gerstner_wave(w2, grid_point, tangent, binormal, TIME);
	p += gerstner_wave(w3, grid_point, tangent, binormal, TIME);
	p += gerstner_wave(w4, grid_point, tangent, binormal, TIME);

	wave_elevation = p.y - grid_point.y;
	world_pos = p;
	VERTEX = (VIEW_MATRIX * vec4(p, 1.0)).xyz;
	NORMAL = normalize(mat3(VIEW_MATRIX) * normalize(cross(binormal, tangent)));
}

// Procedural ripple noise
float ripple(vec2 pos, float time) {
	float t = time * flow_speed;
	float freq = ripple_scale;

	float layerA = sin(pos.y * freq - t * 3.8 + pos.x * freq * 0.18) * 0.55
				 + sin(pos.y * freq * 1.7 - t * 5.1 - pos.x * freq * 0.26) * 0.30;

	float layerB = sin((pos.x * 0.6 + pos.y) * freq * 0.9 - t * 4.2) * 0.40
				 + sin((pos.x * 0.4 - pos.y) * freq * 1.3 + t * 3.0) * 0.25;

	return (layerA * 0.65 + layerB * 0.35) * 0.5 + 0.5;
}

void fragment() {
	NORMAL = vec3(0.0, 1.0, 0.0);

	// 1. Fetch underwater background scene (pillars, sea bed, docks)
	vec2 offset = NORMAL.xz * 0.03;
	vec3 underwater_scene = texture(screen_texture, SCREEN_UV + offset).rgb;

	// 2. Blend crystal water tint over submerged objects
	vec3 sea_base = mix(underwater_scene, water_color.rgb, 0.10);

	// 3. Subtle Fresnel Sky Reflection
	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
	sea_base = mix(sea_base, vec3(0.50, 0.85, 0.95), fresnel * 0.10);

	ALBEDO = sea_base;
	ALPHA = clamp(water_color.a + fresnel * 0.06, 0.05, 0.60);
	ROUGHNESS = 0.02;
	METALLIC = 0.05;
	SPECULAR = 0.50;
}
