layout(local_size_x = 64, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) readonly restrict buffer ParametersBuffer {
    uint numParticles;
    float deltaTime;
    float collisionDamping;
    float smoothingRadius;
    float targetDensity;
    float pressureMultiplier;
    float nearPressureMultiplier;
    float viscosityStrength;
    float gravityX; // Solved - I don't know why making it vec2 messes with padding
    float gravityY;
    float boundsSizeX;
    float boundsSizeY;
    float interactionInputPointX;
    float interactionInputPointY;
    float interactionInputStrength;
    float interactionInputRadius;
    float obstacleSizeX;
    float obstacleSizeY;
    float obstacleCenterX;
    float obstacleCenterY;
};
// vecN cannot start on a 16 byte boundary.
// vec4           |
// vec3 | 4 bytes | vec3 | float | is OK
