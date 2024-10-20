layout(set = 0, binding = 7, std430) readonly restrict buffer ScalingFactorsBuffer {
    float SmoothPoly6ScalingFactor;
    float SpikyPow2ScalingFactor;
    float SpikyPow3ScalingFactor;
    float SpikyPow2DerivativeScalingFactor;
    float SpikyPow3DerivativeScalingFactor;
};

float SmoothPoly6Kernel(float dst, float radius) {
	if (dst < radius) {
		float v = radius * radius - dst * dst;
		return v * v * v * SmoothPoly6ScalingFactor;
	}
	return 0;
}

float SpikyPow2Kernel(float dst, float radius) {
	if (dst < radius) {
		float v = radius - dst;
		return v * v * SpikyPow2ScalingFactor;
	}
	return 0;
}

float SpikyPow3Kernel(float dst, float radius) {
	if (dst < radius) {
		float v = radius - dst;
		return v * v * v * SpikyPow3ScalingFactor;
	}
	return 0;
}

float SpikyPow2Derivative(float dst, float radius) {
	if (dst <= radius) {
		float v = radius - dst;
		return -v * SpikyPow2DerivativeScalingFactor;
	}
	return 0;
}

float SpikyPow3Derivative(float dst, float radius) {
	if (dst <= radius) {
		float v = radius - dst;
		return -v * v * SpikyPow3DerivativeScalingFactor;
	}
	return 0;
}

float DensityKernel(float dst, float radius) {
	return SpikyPow2Kernel(dst, radius);
}

float NearDensityKernel(float dst, float radius) {
	return SpikyPow3Kernel(dst, radius);
}

float DensityDerivative(float dst, float radius) {
	return SpikyPow2Derivative(dst, radius);
}

float NearDensityDerivative(float dst, float radius) {
	return SpikyPow3Derivative(dst, radius);
}

float ViscosityKernel(float dst, float radius) {
	return SmoothPoly6Kernel(dst, radius);
}
