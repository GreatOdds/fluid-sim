layout(local_size_x = 128, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) readonly restrict buffer SettingsBuffer {
    uint numEntries;
    uint groupWidth;
    uint groupHeight;
    uint stepIndex;
};