shader_type particles;
render_mode disable_velocity, disable_force;

uniform sampler2D particleData;

void start() {
	// Called when a particle is spawned.
}

void process() {
	vec2 imageSize = vec2(textureSize(particleData, 0));
	float idx = float(INDEX);
	ivec2 pixel = ivec2(
		int(mod(idx, imageSize.x)),
		int(idx / imageSize.x));
	vec4 data = texelFetch(particleData, pixel, 0);

	TRANSFORM[3].xy = data.xy;
	//COLOR = vec4(data.z, 0., data.z, 1.);
	CUSTOM.xy = data.zw;
}
