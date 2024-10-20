using System;
using System.Runtime.InteropServices;
using Godot;

[Tool]
[GlobalClass]
public partial class GPUFluid2D : Node2D
{
    #region Exported Variables
    [ExportGroup("Simulation Settings")]
    [Export(PropertyHint.Range, "0, 1, 0.001, or_greater")]
    public float timeScale = 1f;
    [Export]
    public bool usePhysicsProcess = true;
    [Export(PropertyHint.Range, "0, 5, 1, or_greater")]
    public uint iterationsPerFrame = 1;
    [Export]
    public Vector2 gravity = new Vector2(0f, 9.8f);
    [Export(PropertyHint.Range, "0, 1, 0.001")]
    public float collisionDamping = 0.95f;
    [Export(PropertyHint.Range, "0.001, 1, 0.001, or_greater")]
    public float smoothingRadius = 0.35f;
    [Export]
    public float targetDensity = 55f;
    [Export]
    public float pressureMultiplier = 200f;
    [Export]
    public float nearPressureMultiplier = 18f;
    [Export]
    public float viscosityStrength = 0.2f;
    [Export]
    public Vector2 BoundsSize
    {
        get => boundsSize;
        set
        {
            boundsSize = value;
            QueueRedraw();
        }
    }
    [Export]
    public Vector2 obstacleSize;
    [Export]
    public Vector2 obstacleCenter;
    [Export]
    public float ParticleSize
    {
        get => particleSize;
        set
        {
            particleSize = value;
            if (Engine.IsEditorHint()) return;
            cpuFluidMaterial?.SetShaderParameter("particleSize", particleSize);
        }
    }

    [ExportGroup("Interaction Settings")]
    [Export(PropertyHint.Range, "0, 5, 0.001, or_greater")]
    public float interactionRadius = 1.25f;
    [Export]
    public float interactionStrength = 200f;

    [ExportGroup("Required Nodes")]
    [Export]
    public ParticleSpawner2D spawner;
    [Export]
    public ShaderMaterial gpuFluidMaterial;
    [Export]
    public ShaderMaterial cpuFluidMaterial;
    [Export]
    public GpuParticles2D gpuParticles2D;
    #endregion Exported Variables

    public uint numParticles { get; private set; }
    Vector2 boundsSize = new Vector2(16, 9);
    float particleSize = 0.1f;

    bool isPaused;
    bool pauseNextFrame;
    bool mousePressed;
    Vector2 mousePos;
    ParticleSpawner2D.ParticleSpawnData spawnData;
    GPUSort gpuSort;

    RenderingDevice rd;
    RenderingDevice globalRd;

    #region Buffers
    enum Buffers
    {
        Parameters,
        Positions,
        PredictedPositions,
        Velocities,
        Densities,
        SpatialIndices,
        SpatialOffsets,
        ScalingFactors,
        ParticleData,
    }

    public struct SimulationParameters
    {
        public uint numParticles;
        public float deltaTime;
        public float collisionDamping;
        public float smoothingRadius;
        public float targetDensity;
        public float pressureMultiplier;
        public float nearPressureMultiplier;
        public float viscosityStrength;
        public Vector2 gravity;
        public Vector2 boundsSize;
        public Vector2 interactionInputPoint;
        public float interactionInputStrength;
        public float interactionInputRadius;
        public Vector2 obstacleSize;
        public Vector2 obstacleCenter;
    }

    public struct ScalingFactors
    {
        public float SmoothPoly6ScalingFactor;
        public float SpikyPow2ScalingFactor;
        public float SpikyPow3ScalingFactor;
        public float SpikyPow2DerivativeScalingFactor;
        public float SpikyPow3DerivativeScalingFactor;
    }

    Rid parametersBuffer;
    Rid positionsBuffer;
    Rid predictedPositionsBuffer;
    Rid velocitiesBuffer;
    Rid densitiesBuffer;
    Rid spatialIndicesBuffer;
    Rid spatialOffsetsBuffer;
    Rid scalingFactorsBuffer;
    #endregion Buffers

    #region Pipelines
    ComputePipeline externalForces;
    ComputePipeline updateSpatialHash;
    ComputePipeline calculateDensities;
    ComputePipeline calculatePressure;
    ComputePipeline calculateViscosity;
    ComputePipeline updatePositions;
    #endregion Pipelines

    #region Output
    Rid localParticleData;
    Rid globalParticleData;
    uint imageSize;
    #endregion Output Images

    ~GPUFluid2D()
    {
        Destroy();
    }

    public override async void _Ready()
    {
        SetPhysicsProcess(false);
        if (Engine.IsEditorHint())
        {
            QueueRedraw();
            return;
        }
        GD.Print("Controls: Space = Play/Pause, R = Reset, Right Arrow = Step");

        Start();

        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        SetPhysicsProcess(true);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed)
        {
            switch (k.Keycode)
            {
                case Key.Space:
                    isPaused = !isPaused;
                    break;
                case Key.Right:
                    isPaused = false;
                    pauseNextFrame = true;
                    break;
                case Key.R:
                    isPaused = true;
                    InitializeBuffers(spawnData);
                    RunSimulationStep();
                    InitializeBuffers(spawnData);
                    break;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (usePhysicsProcess)
        {
            RunSimulationFrame((float)delta);
            UpdateVisuals();
        }
    }

    public override void _Process(double delta)
    {
        if (!usePhysicsProcess)
        {
            RunSimulationFrame((float)delta);
            UpdateVisuals();
        }

        if (pauseNextFrame)
        {
            isPaused = true;
            pauseNextFrame = false;
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(-0.5f * boundsSize, boundsSize),
            new Color(0f, 1f, 1f, 0.5f), false);
        DrawRect(new Rect2(obstacleCenter - 0.5f * obstacleSize, obstacleSize),
            new Color(1f, 0.5f, 0.5f, 0.5f), false);
        if (mousePressed)
        {
            DrawCircle(mousePos, interactionRadius, new Color(1f, 1f, 0f, 0.5f), false);
        }
    }

    void Start()
    {
        spawnData = spawner.GetSpawnData();
        numParticles = (uint)spawnData.positions.Length;

        rd = RenderingServer.CreateLocalRenderingDevice();
        globalRd = RenderingServer.GetRenderingDevice();

        CreateBuffers();

        InitializeBuffers(spawnData);

        CreatePipelines();
        CreateVisuals();

        gpuSort = new(rd);
        gpuSort.SetBuffers((int)numParticles, spatialIndicesBuffer, spatialOffsetsBuffer);

        UpdateParameters(0f);
        RunSimulationStep();
        UpdateVisuals();
    }

    void Destroy()
    {
        gpuSort.Destroy();
        gpuSort = null;

        externalForces?.Destroy();
        updateSpatialHash?.Destroy();
        calculateDensities?.Destroy();
        calculatePressure?.Destroy();
        calculateViscosity?.Destroy();
        updatePositions?.Destroy();

        rd.FreeRid(parametersBuffer);
        rd.FreeRid(positionsBuffer);
        rd.FreeRid(predictedPositionsBuffer);
        rd.FreeRid(velocitiesBuffer);
        rd.FreeRid(densitiesBuffer);
        rd.FreeRid(spatialIndicesBuffer);
        rd.FreeRid(spatialOffsetsBuffer);
        rd.FreeRid(scalingFactorsBuffer);

        rd.FreeRid(localParticleData);
        globalRd.FreeRid(globalParticleData);

        rd.Free();
        rd = null;

        globalRd = null;
    }

    void UpdateParameters(float deltaTime)
    {
        mousePos = GetLocalMousePosition();
        bool isPullInteraction = Input.IsMouseButtonPressed(MouseButton.Left);
        bool isPushInteraction = Input.IsMouseButtonPressed(MouseButton.Right);
        float currInteractStrength = 0;
        mousePressed = isPushInteraction || isPullInteraction;
        if (mousePressed)
        {
            currInteractStrength = isPushInteraction ? -interactionStrength : interactionStrength;
        }

        rd.BufferUpdate(parametersBuffer,
            0, (uint)Marshal.SizeOf<SimulationParameters>(),
            ByteConverter.ConvertToBytes(new SimulationParameters()
            {
                numParticles = numParticles,
                deltaTime = deltaTime,
                collisionDamping = collisionDamping,
                smoothingRadius = smoothingRadius,
                targetDensity = targetDensity,
                pressureMultiplier = pressureMultiplier,
                nearPressureMultiplier = nearPressureMultiplier,
                viscosityStrength = viscosityStrength,
                gravity = gravity.Rotated(-Rotation),
                boundsSize = boundsSize,
                interactionInputPoint = mousePos,
                interactionInputStrength = currInteractStrength,
                interactionInputRadius = interactionRadius,
                obstacleSize = obstacleSize,
                obstacleCenter = obstacleCenter,
            }));

        rd.BufferUpdate(scalingFactorsBuffer,
            0, (uint)Marshal.SizeOf<ScalingFactors>(),
            ByteConverter.ConvertToBytes(new ScalingFactors()
            {
                SmoothPoly6ScalingFactor = 4 / (Mathf.Pi * Mathf.Pow(smoothingRadius, 8)),
                SpikyPow2ScalingFactor = 6 / (Mathf.Pi * Mathf.Pow(smoothingRadius, 4)),
                SpikyPow3ScalingFactor = 10 / (Mathf.Pi * Mathf.Pow(smoothingRadius, 5)),
                SpikyPow2DerivativeScalingFactor = 12 / (Mathf.Pi * Mathf.Pow(smoothingRadius, 4)),
                SpikyPow3DerivativeScalingFactor = 30 / (Mathf.Pi * Mathf.Pow(smoothingRadius, 5)),
            }));
    }

    void UpdateVisuals()
    {
        globalRd.TextureUpdate(globalParticleData, 0, rd.TextureGetData(localParticleData, 0));
        QueueRedraw();
    }

    void RunSimulationFrame(float frameTime)
    {
        if (!isPaused)
        {
            var timeStep = frameTime / iterationsPerFrame * timeScale;

            UpdateParameters(timeStep);

            for (int i = 0; i < iterationsPerFrame; i++)
            {
                RunSimulationStep();
            }
        }
    }

    void RunSimulationStep()
    {
        uint xGroups = (uint)Mathf.CeilToInt(numParticles / 64f);

        externalForces.Dispatch(xGroups, 1, 1);
        updateSpatialHash.Dispatch(xGroups, 1, 1);

        gpuSort.Sort();
        gpuSort.CalculateOffsets();

        calculateDensities.Dispatch(xGroups, 1, 1);
        calculatePressure.Dispatch(xGroups, 1, 1);
        calculateViscosity.Dispatch(xGroups, 1, 1);

        updatePositions.Dispatch(xGroups, 1, 1);

        rd.Submit();
        rd.Sync();
    }

    void InitializeBuffers(ParticleSpawner2D.ParticleSpawnData spawnData)
    {
        rd.BufferUpdate(
            positionsBuffer, 0, (uint)Marshal.SizeOf<Vector2>() * numParticles,
            ByteConverter.ConvertArrayToBytes(spawnData.positions));
        rd.BufferCopy(
            positionsBuffer, predictedPositionsBuffer, 0, 0,
            (uint)Marshal.SizeOf<Vector2>() * numParticles);
        rd.BufferUpdate(
            velocitiesBuffer, 0, (uint)Marshal.SizeOf<Vector2>() * numParticles,
            ByteConverter.ConvertArrayToBytes(spawnData.velocities));
    }

    void CreateBuffers()
    {
        imageSize = (uint)Mathf.NearestPo2(
            (int)Mathf.Sqrt(numParticles));

        parametersBuffer = rd.StorageBufferCreate(
            (uint)Marshal.SizeOf<SimulationParameters>());
        positionsBuffer = rd.StorageBufferCreate(
            (uint)Marshal.SizeOf<Vector2>() * numParticles);
        predictedPositionsBuffer = rd.StorageBufferCreate(
            (uint)Marshal.SizeOf<Vector2>() * numParticles);
        velocitiesBuffer = rd.StorageBufferCreate(
            (uint)Marshal.SizeOf<Vector2>() * numParticles);
        densitiesBuffer = rd.StorageBufferCreate(
            (uint)Marshal.SizeOf<Vector2>() * numParticles);
        spatialIndicesBuffer = rd.StorageBufferCreate(
            (uint)Marshal.SizeOf<uint>() * 3 * numParticles);
        spatialOffsetsBuffer = rd.StorageBufferCreate(
            (uint)Marshal.SizeOf<uint>() * numParticles);
        scalingFactorsBuffer = rd.StorageBufferCreate(
            (uint)Marshal.SizeOf<ScalingFactors>());

        var fmt = new RDTextureFormat();
        fmt.Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat;
        fmt.Width = imageSize;
        fmt.Height = imageSize;
        fmt.UsageBits =
            RenderingDevice.TextureUsageBits.SamplingBit |
            RenderingDevice.TextureUsageBits.StorageBit |
            RenderingDevice.TextureUsageBits.CanUpdateBit |
            RenderingDevice.TextureUsageBits.CanCopyFromBit;
        var view = new RDTextureView();
        localParticleData = rd.TextureCreate(fmt, view);
        globalParticleData = globalRd.TextureCreate(fmt, view);
    }

    void CreatePipelines()
    {
        var parametersUniform = CreateUniform(
            RenderingDevice.UniformType.StorageBuffer,
            (int)Buffers.Parameters, parametersBuffer);

        var positionsUniform = CreateUniform(
            RenderingDevice.UniformType.StorageBuffer,
            (int)Buffers.Positions, positionsBuffer);

        var predictedPositionsUniform = CreateUniform(
            RenderingDevice.UniformType.StorageBuffer,
            (int)Buffers.PredictedPositions, predictedPositionsBuffer);

        var velocitiesUniform = CreateUniform(
            RenderingDevice.UniformType.StorageBuffer,
            (int)Buffers.Velocities, velocitiesBuffer);

        var densitiesUniform = CreateUniform(
            RenderingDevice.UniformType.StorageBuffer,
            (int)Buffers.Densities, densitiesBuffer);

        var spatialIndicesUniform = CreateUniform(
            RenderingDevice.UniformType.StorageBuffer,
            (int)Buffers.SpatialIndices, spatialIndicesBuffer);

        var spatialOffsetsUniform = CreateUniform(
            RenderingDevice.UniformType.StorageBuffer,
            (int)Buffers.SpatialOffsets, spatialOffsetsBuffer);

        var scalingFactorsUniform = CreateUniform(
            RenderingDevice.UniformType.StorageBuffer,
            (int)Buffers.ScalingFactors, scalingFactorsBuffer);

        var particleDataUniform = CreateUniform(
            RenderingDevice.UniformType.Image,
            (int)Buffers.ParticleData, localParticleData);

        externalForces = new ComputePipeline(rd, "res://2D/GPU/Shaders/ExternalForces.glsl");
        externalForces.AddUniforms(new() {
            parametersUniform,
            positionsUniform,
            predictedPositionsUniform,
            velocitiesUniform,
        });

        updateSpatialHash = new ComputePipeline(rd, "res://2D/GPU/Shaders/UpdateSpatialHash.glsl");
        updateSpatialHash.AddUniforms(new() {
            parametersUniform,
            predictedPositionsUniform,
            spatialIndicesUniform,
            spatialOffsetsUniform,
        });

        calculateDensities = new ComputePipeline(rd, "res://2D/GPU/Shaders/CalculateDensities.glsl");
        calculateDensities.AddUniforms(new() {
            parametersUniform,
            predictedPositionsUniform,
            densitiesUniform,
            spatialIndicesUniform,
            spatialOffsetsUniform,
            scalingFactorsUniform,
        });

        calculatePressure = new ComputePipeline(rd, "res://2D/GPU/Shaders/CalculatePressure.glsl");
        calculatePressure.AddUniforms(new() {
            parametersUniform,
            predictedPositionsUniform,
            velocitiesUniform,
            densitiesUniform,
            spatialIndicesUniform,
            spatialOffsetsUniform,
            scalingFactorsUniform,
        });

        calculateViscosity = new ComputePipeline(rd, "res://2D/GPU/Shaders/CalculateViscosity.glsl");
        calculateViscosity.AddUniforms(new() {
            parametersUniform,
            predictedPositionsUniform,
            velocitiesUniform,
            spatialIndicesUniform,
            spatialOffsetsUniform,
            scalingFactorsUniform,
        });

        updatePositions = new ComputePipeline(rd, "res://2D/GPU/Shaders/UpdatePositions.glsl");
        updatePositions.AddUniforms(new() {
            parametersUniform,
            positionsUniform,
            velocitiesUniform,
            densitiesUniform,
            particleDataUniform,
            spatialIndicesUniform,
            spatialOffsetsUniform,
        });
    }

    void CreateVisuals()
    {
        var particleDataTexture = new Texture2Drd() { TextureRdRid = globalParticleData };
        if (gpuFluidMaterial != null)
        {
            gpuFluidMaterial.SetShaderParameter("particleData",
                particleDataTexture);
        }

        cpuFluidMaterial?.SetShaderParameter("particleSize", particleSize);

        gpuParticles2D.Amount = (int)numParticles;

        GetNode<TextureRect>("../CanvasLayer/TextureRect").Texture = particleDataTexture;
    }

    RDUniform CreateUniform(
        RenderingDevice.UniformType uniformType, int binding, Rid id)
    {
        var uniform = new RDUniform();
        uniform.UniformType = uniformType;
        uniform.Binding = binding;
        uniform.AddId(id);
        return uniform;
    }
}