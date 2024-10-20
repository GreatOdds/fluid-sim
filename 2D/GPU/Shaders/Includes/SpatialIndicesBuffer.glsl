struct Entry {
    uint index;
    uint hash;
    uint key;
};

layout(set = 0, binding = 5, std430) restrict buffer SpatialIndicesBuffer {
    Entry[] SpatialIndices;
};