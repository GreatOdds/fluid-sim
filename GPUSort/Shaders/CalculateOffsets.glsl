#[compute]
#version 450

#include "Includes/Common.glsl"
#include "Includes/EntriesBuffer.glsl"
#include "Includes/OffsetsBuffer.glsl"

void main() {
    if (gl_GlobalInvocationID.x >= numEntries) { return; }

	uint i = gl_GlobalInvocationID.x;
	uint null = numEntries;

	uint key = Entries[i].key;
	uint keyPrev = i == 0 ? null : Entries[i - 1].key;

	if (key != keyPrev)
	{
		Offsets[key] = i;
	}
}