using System;
using System.Runtime.InteropServices;
using Godot;

public class GPUSort
{
    enum Buffers
    {
        Settings,
        Indices,
        Offsets,
    }

    public struct SortSettings
    {
        public uint numEntries;
        public uint groupWidth;
        public uint groupHeight;
        public uint stepIndex;
    }

    int numEntries;

    RenderingDevice rd;

    Rid settingsBuffer;
    RDUniform settingsUniform;
    RDUniform indicesUniform;
    RDUniform offsetsUniform;

    Rid sortShader;
    Rid sortPipeline;
    Rid sortUniformSet;
    Rid calculateOffsetsShader;
    Rid calculateOffsetsPipeline;
    Rid calculateOffsetsUniformSet;

    public GPUSort(RenderingDevice rd)
    {
        this.rd = rd;

        settingsBuffer = rd.StorageBufferCreate(
            (uint)Marshal.SizeOf<SortSettings>());

        settingsUniform = new()
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = (int)Buffers.Settings
        };
        settingsUniform.AddId(settingsBuffer);

        indicesUniform = new()
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = (int)Buffers.Indices
        };

        offsetsUniform = new()
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = (int)Buffers.Offsets
        };

        sortShader = rd.ShaderCreateFromSpirV(
            GD.Load<RDShaderFile>("res://GPUSort/Shaders/Sort.glsl")
            ?.GetSpirV());
        sortPipeline = rd.ComputePipelineCreate(sortShader);

        calculateOffsetsShader = rd.ShaderCreateFromSpirV(
            GD.Load<RDShaderFile>("res://GPUSort/Shaders/CalculateOffsets.glsl")
            ?.GetSpirV());
        calculateOffsetsPipeline = rd.ComputePipelineCreate(calculateOffsetsShader);
    }

    public void Destroy()
    {
        rd.FreeRid(settingsBuffer);

        rd.FreeRid(sortUniformSet);
        rd.FreeRid(sortPipeline);
        rd.FreeRid(sortShader);

        rd.FreeRid(calculateOffsetsUniformSet);
        rd.FreeRid(calculateOffsetsPipeline);
        rd.FreeRid(calculateOffsetsShader);
    }

    public void SetBuffers(int numEntries, Rid indexBuffer, Rid offsetBuffer)
    {
        this.numEntries = numEntries;

        indicesUniform.ClearIds();
        indicesUniform.AddId(indexBuffer);

        offsetsUniform.ClearIds();
        offsetsUniform.AddId(offsetBuffer);

        if (rd.UniformSetIsValid(sortUniformSet))
        {
            rd.FreeRid(sortUniformSet);
        }
        sortUniformSet = rd.UniformSetCreate(
            new() {
                settingsUniform,
                indicesUniform,
            },
            sortShader, 0);

        if (rd.UniformSetIsValid(calculateOffsetsUniformSet))
        {
            rd.FreeRid(calculateOffsetsUniformSet);
        }
        calculateOffsetsUniformSet = rd.UniformSetCreate(
            new() {
                settingsUniform,
                indicesUniform,
                offsetsUniform,
            },
            calculateOffsetsShader, 0);
    }

    public void Sort()
    {
        var xGroups = (uint)(Mathf.NearestPo2(numEntries) / 256f);
        if (xGroups == 0) xGroups = 1;
        var numStages = (int)MathF.Log(Mathf.NearestPo2(numEntries), 2f);

        for (int stageIndex = 0; stageIndex < numStages; stageIndex++)
        {
            for (int stepIndex = 0; stepIndex < stageIndex + 1; stepIndex++)
            {
                // Calculate some pattern stuff
                int groupWidth = 1 << (stageIndex - stepIndex);
                int groupHeight = 2 * groupWidth - 1;

                rd.BufferUpdate(
                    settingsBuffer, 0,
                    (uint)Marshal.SizeOf<SortSettings>(),
                    ByteConverter.ConvertToBytes(new SortSettings()
                    {
                        numEntries = (uint)numEntries,
                        groupWidth = (uint)groupWidth,
                        groupHeight = (uint)groupHeight,
                        stepIndex = (uint)stepIndex,
                    }));
                var computeList = rd.ComputeListBegin();
                rd.ComputeListBindComputePipeline(computeList, sortPipeline);
                rd.ComputeListBindUniformSet(computeList, sortUniformSet, 0);
                rd.ComputeListDispatch(computeList, xGroups, 1, 1);
                rd.ComputeListEnd();
            }
        }
    }

    public void CalculateOffsets()
    {
        uint xGroups = (uint)(numEntries / 128f);
        if (xGroups == 0) xGroups = 1;
        var computeList = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(computeList, calculateOffsetsPipeline);
        rd.ComputeListBindUniformSet(computeList, calculateOffsetsUniformSet, 0);
        rd.ComputeListDispatch(computeList, xGroups, 1, 1);
        rd.ComputeListEnd();
    }
}