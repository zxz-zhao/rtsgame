shader_type spatial;
render_mode blend_mix, cull_back, diffuse_burley, specular_schlick_ggx;

// Core Material Uniforms
uniform vec4 water_color : source_color = vec4(0.06, 0.28, 0.48, 0.58);
uniform vec4 shallow_color : source_color = vec4(0.08, 0.68, 0.46, 0.85);
uniform vec4 foam_color : source_color = vec4(0.88, 0.96, 1.0, 0.92);
uniform float flow_speed = 0.46;
uniform float wave_scale = 0.55;
uniform float wave_strength = 0.42;
uniform float edge_fade = 0.14;
uniform float foam_scale = 0.38;
uniform float metallic = 0.0;
uniform float roughness = 0.12;
uniform float specular = 0.5;
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

	// 在模型局部空间内实施顶点位移与法线计算，避免由 VIEW_MATRIX 二次叠加引起的镜头低角异常倾斜
	vec3 model_disp = (inverse(MODEL_MATRIX) * vec4(p - grid_point, 0.0)).xyz;
	VERTEX += model_disp;
	NORMAL = normalize((inverse(MODEL_MATRIX) * vec4(cross(binormal, tangent), 0.0)).xyz);
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
	// 动态波纹与扰动法线：在世界空间生成表面扰动向量，然后乘以 VIEW_MATRIX 转换为 Godot 期望的视图空间法线
	float rip = ripple(world_pos.xz, TIME);
	vec3 wave_norm_world = normalize(vec3((rip - 0.5) * ripple_strength, 1.0, (rip - 0.5) * ripple_strength));
	NORMAL = normalize((VIEW_MATRIX * vec4(wave_norm_world, 0.0)).xyz);

	// 1. 水下透视折射 (Screen Refraction)
	vec2 offset = wave_norm_world.xz * 0.015;
	vec3 underwater_scene = textureLod(screen_texture, SCREEN_UV + offset, 0.0).rgb;

	// 2. 真实深度计算 (Depth Buffer Linearization & Cross-Renderer Fallback)
	float edge_dist = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
	float uv_depth = clamp(edge_dist / max(0.01, edge_fade), 0.0, 1.0);

	float depth_raw = textureLod(depth_texture, SCREEN_UV, 0.0).r;
	vec3 ndc;
	#if CURRENT_RENDERER == RENDERER_COMPATIBILITY
		ndc = vec3(SCREEN_UV, depth_raw) * 2.0 - 1.0;
	#else
		ndc = vec3(SCREEN_UV * 2.0 - 1.0, depth_raw);
	#endif
	vec4 view_pos = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
	view_pos.xyz /= view_pos.w;
	float linear_depth = -view_pos.z;
	float diff = linear_depth + VERTEX.z;

	float water_depth;
	if (depth_raw > 0.0005 && depth_raw < 0.9995 && diff > 0.0) {
		float depth_based = clamp(diff / max(0.01, edge_fade * 4.0), 0.0, 1.0);
		water_depth = depth_based;
	} else {
		water_depth = uv_depth;
	}

	// 3. 浅水碧绿到深水深蓝过渡 (Commercial Shallow-to-Deep Oceanic Gradient)
	vec3 deep_color = water_color.rgb;
	vec3 shallow = shallow_color.a > 0.01 ? shallow_color.rgb : mix(deep_color, vec3(0.12, 0.78, 0.52), 0.35);
	vec3 water_body = mix(shallow, deep_color, smoothstep(0.05, 0.75, water_depth));
	// 吸收与透射混合：保持浓郁水体色彩主导，防止浅滩砂石反白冲淡水色
	vec3 sea_base = mix(underwater_scene, water_body, clamp(0.70 + water_depth * 0.28, 0.70, 0.98));

	// 4. 商用发布级近岸、岛礁与舰船吃水深度浪花泡沫 (Multi-Band Shoreline Depth Foam)
	float shore_contact = 1.0 - smoothstep(0.0, 0.05, water_depth);
	float surf_band = (1.0 - smoothstep(0.02, 0.16, water_depth)) * clamp(sin(water_depth * 38.0 - TIME * 3.4), 0.0, 1.0);
	float foam_noise_a = sin(world_pos.x * 6.2 + TIME * 1.9) * cos(world_pos.z * 6.2 - TIME * 1.6) * 0.5 + 0.5;
	float foam_noise_b = sin(world_pos.x * 14.5 - TIME * 2.8) * cos(world_pos.z * 14.5 + TIME * 2.4) * 0.5 + 0.5;
	float combined_noise = mix(foam_noise_a, foam_noise_b, 0.35);
	float foam = clamp(shore_contact * 0.85 + surf_band * 0.35 * combined_noise, 0.0, 1.0);
	sea_base = mix(sea_base, foam_color.rgb, foam * foam_color.a * 0.75);

	// 5. 菲涅尔天光镜面反射 (Physical Fresnel Glance - only at grazing angles)
	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.5);
	sea_base = mix(sea_base, vec3(0.65, 0.88, 0.85), fresnel * 0.20);

	ALBEDO = sea_base;
	ALPHA = clamp(water_color.a + fresnel * 0.20 + foam * 0.25, 0.85, 0.99);
	ROUGHNESS = roughness;
	METALLIC = metallic;
	SPECULAR = specular;
}
