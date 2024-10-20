shader_type canvas_item;

uniform float particleSize = 1.;
uniform vec3 particleColor = vec3(1.);
uniform float velocityMax = 10.;
uniform sampler2D velocityGradient: source_color;

void vertex() {
	// Scale the particle to size
	VERTEX = particleSize * VERTEX;

	// Calculate color from velocity (in INSTANCE_CUSTOM)
	float speed = length(INSTANCE_CUSTOM.xy);
	float speedT = clamp(speed / velocityMax, 0., 1.);
	COLOR = texture(velocityGradient, vec2(speedT, 0.));
}

void fragment() {
	vec2 centerOffset = (UV - .5) * 2.;
	float sqrDstFromCenter = dot(centerOffset, centerOffset);
	float delta = fwidth(sqrt(sqrDstFromCenter));
	float circleAlpha = 1. - smoothstep(1. - delta, 1. + delta, sqrDstFromCenter);
	COLOR = vec4(COLOR.rgb, circleAlpha);
}
