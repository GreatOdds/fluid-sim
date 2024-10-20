struct Entry {
    uint index;
    uint hash;
    uint key;
};

layout(set = 0, binding = 1, std430) restrict buffer EntriesBuffer {
    Entry[] Entries;
};