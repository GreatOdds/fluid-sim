#[compute]
#version 450

#include "Includes/SpatialHash.glsl"
#include "Includes/Common.glsl"
#include "Includes/PredictedPositionsBuffer.glsl"
#include "Includes/VelocitiesBuffer.glsl"
#include "Includes/DensitiesBuffer.glsl"
#include "Includes/SpatialIndicesBuffer.glsl"
#include "Includes/SpatialOffsetsBuffer.glsl"
#include "Includes/FluidMath2D.glsl"

float PressureFromDensity(float density) {
	return (density - targetDensity) * pressureMultiplier;
}

float NearPressureFromDensity(float nearDensity) {
	return nearPressureMultiplier * nearDensity;
}

void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= numParticles) return;

    float density = Densities[idx][0];
	float densityNear = Densities[idx][1];
	float pressure = PressureFromDensity(density);
	float nearPressure = NearPressureFromDensity(densityNear);
	vec2 pressureForce = vec2(0);
	
	vec2 pos = PredictedPositions[idx];
	ivec2 originCell = GetCell2D(pos, smoothingRadius);
	float sqrRadius = smoothingRadius * smoothingRadius;

	// Neighbour search
	for (int i = 0; i < 9; i ++)
	{
		uint hash = HashCell2D(originCell + offsets2D[i]);
		uint key = KeyFromHash(hash, numParticles);
		uint currIndex = SpatialOffsets[key];

		while (currIndex < numParticles)
		{
			Entry indexData = SpatialIndices[currIndex];
			currIndex ++;
			// Exit if no longer looking at correct bin
			if (indexData.key != key) break;
			// Skip if hash does not match
			if (indexData.hash != hash) continue;

			uint neighbourIndex = indexData.index;
			// Skip if looking at self
			if (neighbourIndex == idx) continue;

			vec2 neighbourPos = PredictedPositions[neighbourIndex];
			vec2 offsetToNeighbour = neighbourPos - pos;
			float sqrDstToNeighbour = dot(offsetToNeighbour, offsetToNeighbour);

			// Skip if not within radius
			if (sqrDstToNeighbour > sqrRadius) continue;

			// Calculate pressure force
			float dst = sqrt(sqrDstToNeighbour);
			vec2 dirToNeighbour = dst > 0 ? offsetToNeighbour / dst : vec2(0, 1);

			float neighbourDensity = Densities[neighbourIndex][0];
			float neighbourNearDensity = Densities[neighbourIndex][1];
			float neighbourPressure = PressureFromDensity(neighbourDensity);
			float neighbourNearPressure = NearPressureFromDensity(neighbourNearDensity);

			float sharedPressure = (pressure + neighbourPressure) * 0.5;
			float sharedNearPressure = (nearPressure + neighbourNearPressure) * 0.5;

			pressureForce += dirToNeighbour * DensityDerivative(dst, smoothingRadius) * sharedPressure / neighbourDensity;
			pressureForce += dirToNeighbour * NearDensityDerivative(dst, smoothingRadius) * sharedNearPressure / neighbourNearDensity;
		}
	}

	vec2 acceleration = pressureForce / density;
	Velocities[idx] += acceleration * deltaTime;
}