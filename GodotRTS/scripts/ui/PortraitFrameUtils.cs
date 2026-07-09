using Godot;

public static class PortraitFrameUtils
{
    static Shader? circularPortraitShader;
    static Shader? portraitWrapFrameShader;

    public static void ApplyCircularPortrait(
        TextureRect rect,
        Color borderColor,
        Color rimColor,
        float radius = 0.47f,
        float borderWidth = 0.075f,
        float feather = 0.02f,
        Vector2? uvScale = null,
        Vector2? uvOffset = null)
    {
        var material = new ShaderMaterial
        {
            Shader = CircularPortraitShader
        };
        var scale = uvScale ?? new Vector2(1.75f, 1.75f);
        var offset = uvOffset ?? new Vector2(0.01f, -0.055f);
        material.SetShaderParameter("border_color", borderColor);
        material.SetShaderParameter("rim_color", rimColor);
        material.SetShaderParameter("radius", radius);
        material.SetShaderParameter("border_width", borderWidth);
        material.SetShaderParameter("feather", feather);
        material.SetShaderParameter("uv_scale", scale);
        material.SetShaderParameter("uv_offset", offset);
        rect.Material = material;
    }

    static Shader CircularPortraitShader => circularPortraitShader ??= new Shader
    {
        Code = """
shader_type canvas_item;

uniform vec4 border_color : source_color = vec4(0.94, 0.76, 0.26, 1.0);
uniform vec4 rim_color : source_color = vec4(1.0, 0.95, 0.76, 1.0);
uniform float radius = 0.47;
uniform float border_width = 0.075;
uniform float feather = 0.02;
uniform vec2 uv_scale = vec2(1.75, 1.75);
uniform vec2 uv_offset = vec2(0.01, -0.055);

void fragment() {
    vec2 centered = UV - vec2(0.5);
    float dist = length(centered);
    float visible = 1.0 - smoothstep(radius, radius + feather, dist);
    float ring = smoothstep(radius - border_width - feather, radius - border_width, dist) * visible;
    float rim = smoothstep(radius - border_width * 0.55 - feather, radius - border_width * 0.55, dist) * visible;
    float shade = smoothstep(radius - border_width * 2.0, radius - border_width * 0.95, dist) * (1.0 - ring);

    vec2 sample_uv = (UV - vec2(0.5)) / uv_scale + vec2(0.5) + uv_offset;
    vec4 tex = texture(TEXTURE, sample_uv) * COLOR;
    vec3 color = tex.rgb;
    color *= 1.0 - shade * 0.10;
    color = mix(color, border_color.rgb, ring * 0.90);
    color = mix(color, rim_color.rgb, rim * 0.42);

    COLOR = vec4(color, tex.a * visible);
}
"""
    };

    public static void ApplyWrapFrame(TextureRect rect, Color outerColor, Color innerColor, float radius = 0.49f, float outerWidth = 0.11f, float innerWidth = 0.045f, float feather = 0.02f)
    {
        var material = new ShaderMaterial
        {
            Shader = PortraitWrapFrameShader
        };
        material.SetShaderParameter("outer_color", outerColor);
        material.SetShaderParameter("inner_color", innerColor);
        material.SetShaderParameter("radius", radius);
        material.SetShaderParameter("outer_width", outerWidth);
        material.SetShaderParameter("inner_width", innerWidth);
        material.SetShaderParameter("feather", feather);
        rect.Material = material;
    }

    static Shader PortraitWrapFrameShader => portraitWrapFrameShader ??= new Shader
    {
        Code = """
shader_type canvas_item;

uniform vec4 outer_color : source_color = vec4(0.85, 0.64, 0.18, 0.96);
uniform vec4 inner_color : source_color = vec4(1.0, 0.94, 0.72, 0.92);
uniform float radius = 0.49;
uniform float outer_width = 0.11;
uniform float inner_width = 0.045;
uniform float feather = 0.02;

void fragment() {
    vec2 centered = UV - vec2(0.5);
    float dist = length(centered);
    float visible = 1.0 - smoothstep(radius, radius + feather, dist);
    float outer_ring = smoothstep(radius - outer_width - feather, radius - outer_width, dist) * visible;
    float inner_ring = smoothstep(radius - outer_width - inner_width - feather, radius - outer_width - inner_width, dist) * (1.0 - outer_ring) * visible;
    float glow = smoothstep(radius - outer_width * 1.45 - feather, radius - outer_width * 0.92, dist) * visible;

    vec3 color = mix(inner_color.rgb, outer_color.rgb, outer_ring);
    color = mix(color, inner_color.rgb, inner_ring * 0.92);
    color += inner_color.rgb * glow * 0.16;

    float alpha = max(outer_ring * outer_color.a, inner_ring * inner_color.a);
    alpha = max(alpha, glow * 0.28);
    COLOR = vec4(color, alpha);
}
"""
    };
}
