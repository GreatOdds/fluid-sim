using Godot;

public class ComputePipeline
{
    private RenderingDevice rd;
    public Rid shader;
    public Rid pipeline;
    public Rid uniformSet;

    public ComputePipeline(RenderingDevice rd, string shaderPath)
    {
        this.rd = rd;
        shader = this.rd.ShaderCreateFromSpirV(
            GD.Load<RDShaderFile>(shaderPath).GetSpirV());
        pipeline = this.rd.ComputePipelineCreate(shader);
    }

    public void AddUniforms(Godot.Collections.Array<RDUniform> uniforms)
    {
        if (rd.UniformSetIsValid(uniformSet))
        {
            rd.FreeRid(uniformSet);
        }
        uniformSet = rd.UniformSetCreate(uniforms, shader, 0);
    }

    public void Dispatch(uint xGroups, uint yGroups, uint zGroups)
    {
        var computeList = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(computeList, pipeline);
        rd.ComputeListBindUniformSet(computeList, uniformSet, 0);
        rd.ComputeListDispatch(computeList, xGroups, yGroups, zGroups);
        rd.ComputeListEnd();
    }

    public void Destroy()
    {
        if (shader.IsValid)
        {
            rd.FreeRid(shader);
        }
        if (rd.ComputePipelineIsValid(pipeline))
        {
            rd.FreeRid(pipeline);
        }
        if (rd.UniformSetIsValid(uniformSet))
        {
            rd.FreeRid(uniformSet);
        }
    }
}