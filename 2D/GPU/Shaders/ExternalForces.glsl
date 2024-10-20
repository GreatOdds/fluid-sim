#[compute]
#version 450

#include "Includes/Common.glsl"
#include "Includes/PositionsBuffer.glsl"
#include "Includes/PredictedPositionsBuffer.glsl"
#include "Includes/VelocitiesBuffer.glsl"

vec2 ExternalForces(vec2 pos, vec2 vel) {
    vec2 gravityAccel = vec2(gravityX, gravityY);
    vec2 interactionInputPoint = vec2(interactionInputPointX, interactionInputPointY);

    if (interactionInputStrength != 0) {
        vec2 inputPointOffset = interactionInputPoint - pos;
        float sqrDst = dot(inputPointOffset, inputPointOffset);
        if (sqrDst < interactionInputRadius * interactionInputRadius) {
            float dst = sqrt(sqrDst);
            float edgeT = (dst / interactionInputRadius);
			float centreT = 1 - edgeT;
			vec2 dirToCentre = inputPointOffset / dst;

			float gravityWeight = 1 - (centreT * clamp(interactionInputStrength / 10, 0, 1));
			vec2 accel = gravityAccel * gravityWeight + dirToCentre * centreT * interactionInputStrength;
			accel -= vel * centreT;
			return accel;
        }
    }

    return gravityAccel;
}

void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= numParticles) return;

    // External forces (gravity and input interaction)
    Velocities[idx] += ExternalForces(Positions[idx], Velocities[idx]) * deltaTime;

    // Predict
	const float predictionFactor = 1.0 / 120.0;
	PredictedPositions[idx] = Positions[idx] + Velocities[idx] * predictionFactor;
}