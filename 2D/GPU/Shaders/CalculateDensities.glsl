#[compute]
#version 450

#include "Includes/SpatialHash.glsl"
#include "Includes/Common.glsl"
#include "Includes/PredictedPositionsBuffer.glsl"
#include "Includes/DensitiesBuffer.glsl"
#include "Includes/SpatialIndicesBuffer.glsl"
#include "Includes/SpatialOffsetsBuffer.glsl"
#include "Includes/FluidMath2D.glsl"

vec2 CalculateDensity(vec2 pos) {
    ivec2 originCell = GetCell2D(pos, smoothingRadius);
    float sqrRadius = smoothingRadius * smoothingRadius;
    float density = 1;
    float nearDensity = 1;

    // Neighbour search
    for (int i = 0; i < 9; i++) {
        uint hash = HashCell2D(originCell + offsets2D[i]);
        uint key = KeyFromHash(hash, numParticles);
        uint currIndex = SpatialOffsets[key];

        while (currIndex < numParticles) {
            Entry indexData = SpatialIndices[currIndex];
            currIndex++;
            // Exit if no longer looking at correct bin
            if (indexData.key != key) break;
            // Skip if hash does not match
            if (indexData.hash != hash) continue;

            uint neighbourIndex = indexData.index;
            vec2 neighbourPos = PredictedPositions[neighbourIndex];
            vec2 offsetToNeighbour = neighbourPos - pos;
            float sqrDstToNeighbour = dot(offsetToNeighbour, offsetToNeighbour);

            // Skip if not within radius
            if (sqrDstToNeighbour > sqrRadius) continue;

            // Calculate density and near density
            float dst = sqrt(sqrDstToNeighbour);
            density += DensityKernel(dst, smoothingRadius);
            nearDensity += NearDensityKernel(dst, smoothingRadius);
        }
    }

    return vec2(density, nearDensity);
}

void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= numParticles) return;

    vec2 pos = PredictedPositions[idx];
    Densities[idx] = CalculateDensity(pos);
}