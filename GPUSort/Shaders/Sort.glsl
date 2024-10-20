#[compute]
#version 450

#include "Includes/Common.glsl"
#include "Includes/EntriesBuffer.glsl"

void main() {
    uint i = gl_GlobalInvocationID.x;
    
    uint hIndex = i & (groupWidth - 1);
	uint indexLeft = hIndex + (groupHeight + 1) * (i / groupWidth);
	uint rightStepSize = stepIndex == 0 ? groupHeight - 2 * hIndex : (groupHeight + 1) / 2;
	uint indexRight = indexLeft + rightStepSize;

	// Exit if out of bounds (for non-power of 2 input sizes)
	if (indexRight >= numEntries) return;

	uint valueLeft = Entries[indexLeft].key;
	uint valueRight = Entries[indexRight].key;

	// Swap entries if value is descending
	if (valueLeft > valueRight)
	{
		Entry temp = Entries[indexLeft];
		Entries[indexLeft] = Entries[indexRight];
		Entries[indexRight] = temp;
	}
}