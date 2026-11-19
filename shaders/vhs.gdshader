shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform float intensity : hint_range(0.0, 1.0) = 0.55;
uniform float time_scale = 1.0;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

void fragment() {
    vec2 uv = SCREEN_UV;
    float t = TIME * time_scale;
    float line = sin((uv.y * 720.0 + t * 18.0) * 3.14159265) * 0.0035 * intensity;
    float tracking = smoothstep(0.985, 1.0, sin(uv.y * 18.0 + t * 1.7) * 0.5 + 0.5) * 0.009 * intensity;
    uv.x += line + tracking;

    float chroma = 0.0018 * intensity;
    vec3 c;
    c.r = texture(screen_tex, uv + vec2(chroma, 0.0)).r;
    c.g = texture(screen_tex, uv).g;
    c.b = texture(screen_tex, uv - vec2(chroma, 0.0)).b;

    float scan = 0.90 + 0.10 * sin(uv.y * 900.0);
    float grain = (hash(uv * vec2(1920.0, 1080.0) + t) - 0.5) * 0.055 * intensity;
    float vignette = smoothstep(0.78, 0.20, distance(uv, vec2(0.5)));
    c = c * mix(1.0, scan, intensity * 0.6) + grain;
    c *= mix(1.0, vignette, intensity * 0.32);
    COLOR = vec4(c, 1.0);
}
