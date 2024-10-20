#[compute]
#version 450

#include "Includes/SpatialHash.glsl"
#include "Includes/Common.glsl"
#include "Includes/PredictedPositionsBuffer.glsl"
#include "Includes/SpatialIndicesBuffer.glsl"
#include "Includes/SpatialOffsetsBuffer.glsl"

void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= numParticles) return;

    // Reset offsets
	SpatialOffsets[idx] = numParticles;
	// Update index buffer
	ivec2 cell = GetCell2D(PredictedPositions[idx], smoothingRadius);
	SpatialIndices[idx].index = idx;
	SpatialIndices[idx].hash = HashCell2D(cell);
	SpatialIndices[idx].key = KeyFromHash(SpatialIndices[idx].hash, numParticles);
}