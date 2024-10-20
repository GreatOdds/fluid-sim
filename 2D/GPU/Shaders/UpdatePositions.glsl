#[compute]
#version 450

#include "Includes/Common.glsl"
#include "Includes/PositionsBuffer.glsl"
#include "Includes/VelocitiesBuffer.glsl"
#include "Includes/DensitiesBuffer.glsl"
#include "Includes/SpatialIndicesBuffer.glsl"
#include "Includes/SpatialOffsetsBuffer.glsl"
#include "Includes/ParticleData.glsl"

void HandleCollisions(uint particleIndex)
{
	vec2 pos = Positions[particleIndex];
	vec2 vel = Velocities[particleIndex];

	// Keep particle inside bounds
	const vec2 halfSize = vec2(boundsSizeX, boundsSizeY) * 0.5;
	vec2 edgeDst = halfSize - abs(pos);

	if (edgeDst.x <= 0)
	{
		pos.x = halfSize.x * sign(pos.x);
		vel.x *= -1. * collisionDamping;
	}
	if (edgeDst.y <= 0)
	{
		pos.y = halfSize.y * sign(pos.y);
		vel.y *= -1. * collisionDamping;
	}

	// // Collide particle against the test obstacle
	const vec2 obstacleCenter = vec2(obstacleCenterX, obstacleCenterY);
	const vec2 obstacleHalfSize = vec2(obstacleSizeX, obstacleSizeY) * 0.5;
	vec2 obstacleEdgeDst = obstacleHalfSize - abs(pos - obstacleCenter);

	if (obstacleEdgeDst.x >= 0 && obstacleEdgeDst.y >= 0)
	{
		if (obstacleEdgeDst.x < obstacleEdgeDst.y) {
			pos.x = obstacleHalfSize.x * sign(pos.x - obstacleCenter.x) + obstacleCenter.x;
			vel.x *= -1 * collisionDamping;
		}
		else {
			pos.y = obstacleHalfSize.y * sign(pos.y - obstacleCenter.y) + obstacleCenter.y;
			vel.y *= -1 * collisionDamping;
		}
	}

	// Update position and velocity
	Positions[particleIndex] = pos;
	Velocities[particleIndex] = vel;
}

vec2 IndexHash(uint hash) {
	float s = sin(hash * 100);
	float c = cos(hash * 100);
	return mat2(c, -s, s, c) * vec2(1, 0);
}

void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= numParticles) return;

    Positions[idx] += Velocities[idx] * deltaTime;
    HandleCollisions(idx);

	ivec2 imageSize = imageSize(ParticleData);
	ivec2 pixel = ivec2(
		idx % imageSize.x,
		idx / float(imageSize.x));
	imageStore(ParticleData, pixel, vec4(
		Positions[idx],	Velocities[idx]));
	
	// imageStore(ParticleData, pixel, vec4(
	// 	Positions[idx], IndexHash(SpatialIndices[idx].key)));

	// imageStore(ParticleData, pixel, vec4(
	// 	Positions[idx], IndexHash(idx)));

	// imageStore(ParticleData, pixel, vec4(
	// 	Positions[idx], IndexHash(SpatialOffsets[idx] * 3)));
	
	// imageStore(ParticleData, pixel, vec4(
	// 	Positions[idx], Densities[idx]));
}