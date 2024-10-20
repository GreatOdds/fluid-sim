#[compute]
#version 450

#include "Includes/SpatialHash.glsl"
#include "Includes/Common.glsl"
#include "Includes/PredictedPositionsBuffer.glsl"
#include "Includes/VelocitiesBuffer.glsl"
#include "Includes/SpatialIndicesBuffer.glsl"
#include "Includes/SpatialOffsetsBuffer.glsl"
#include "Includes/FluidMath2D.glsl"

void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= numParticles) return;

    vec2 pos = PredictedPositions[idx];
	ivec2 originCell = GetCell2D(pos, smoothingRadius);
	float sqrRadius = smoothingRadius * smoothingRadius;

	vec2 viscosityForce = vec2(0);
	vec2 velocity = Velocities[idx];

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

			float dst = sqrt(sqrDstToNeighbour);
			vec2 neighbourVelocity = Velocities[neighbourIndex];
			viscosityForce += (neighbourVelocity - velocity) * ViscosityKernel(dst, smoothingRadius);
		}

	}
	Velocities[idx] += viscosityForce * viscosityStrength * deltaTime;
}