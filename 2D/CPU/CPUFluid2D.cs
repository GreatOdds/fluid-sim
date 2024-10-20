using Godot;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public partial class CPUFluid2D : Node2D
{
    [ExportGroup("Simulation Settings")]
    [Export(PropertyHint.Range, "1,1000,1,or_greater")]
    public int NumParticles
    {
        get => numParticles;
        set
        {
            numParticles = value;
            InitializeData();
        }
    }
    [Export]
    public float timeScale = 1f;
    [Export]
    public int iterationsPerFrame = 1;
    [Export]
    public float gravity = 9.8f;
    [Export(PropertyHint.Range, "0, 1")]
    public float collisionDamping = 0.95f;
    [Export]
    public float SmoothingRadius
    {
        get => smoothingRadius;
        set
        {
            smoothingRadius = value;

            SmoothPoly6ScalingFactor = 4f / (Mathf.Pi * Mathf.Pow(smoothingRadius, 8));
            SpikyPow2ScalingFactor = 6f / (Mathf.Pi * Mathf.Pow(smoothingRadius, 4));
            SpikyPow3ScalingFactor = 10f / (Mathf.Pi * Mathf.Pow(smoothingRadius, 5));
            SpikyPow2DerivativeScalingFactor = 12f / (Mathf.Pi * Mathf.Pow(smoothingRadius, 4));
            SpikyPow3DerivativeScalingFactor = 30f / (Mathf.Pi * Mathf.Pow(smoothingRadius, 5));
        }
    }
    [Export]
    public float targetDensity = 55f;
    [Export]
    public float pressureMultiplier = 200f;
    [Export]
    public float nearPressureMultiplier = 18f;
    [Export]
    public float viscosityStrength = 0.06f;
    [Export]
    public Vector2 boundsSize = new Vector2(16, 9);
    // [Export]
    // public Vector2 obstacleSize;
    // [Export]
    // public Vector2 obstacleCenter;

    [ExportGroup("Interaction Settings")]
    [Export]
    public float interactionRadius = 2f;
    [Export]
    public float interactionStrength = 90f;

    [ExportGroup("References")]
    [Export]
    public ShaderMaterial particleMaterial;
    [Export]
    public MultiMesh particleMultiMesh;

    bool isPaused;
    bool pauseNextFrame;

    bool hasInteraction;
    Vector2 interactionInputPoint;
    float currInteractionStrength;

    Vector2[] positions;
    Vector2[] predictedPositions;
    Vector2[] velocities;
    Vector2[] densities; // Density, NearDensity
    SpatialLookup2D spatialLookup;

    int numParticles;
    float smoothingRadius = 0.35f;

    public override async void _Ready()
    {
        GD.Print("<=====Controls=====>\nPause\t[Space]\nStep\t[Right Arrow]\nReset\t[R]\n");
        InitializeData();
        UpdateVisuals();
        QueueRedraw();
        SetPhysicsProcess(false);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        SetPhysicsProcess(!Engine.IsEditorHint());
    }

    static Color GREEN = new Color(0f, 1f, 0f);
    public override void _Draw()
    {
        DrawRect(new Rect2(-0.5f * boundsSize, boundsSize), GREEN, false);
        if (hasInteraction)
        {
            DrawCircle(interactionInputPoint, interactionRadius, GREEN, false);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        RunSimulationFrame((float)delta);

        if (pauseNextFrame)
        {
            isPaused = true;
            pauseNextFrame = false;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed)
        {
            if (k.Keycode == Key.Space)
            {
                isPaused = !isPaused;
            }
            else if (k.Keycode == Key.Right)
            {
                isPaused = false;
                pauseNextFrame = true;
            }
            else if (k.Keycode == Key.R)
            {
                isPaused = true;
                InitializeData();
                RunSimulationStep((float)GetPhysicsProcessDeltaTime());
                UpdateVisuals();
            }
        }

    }

    void InitializeData()
    {
        positions = new Vector2[numParticles];
        predictedPositions = new Vector2[numParticles];
        velocities = new Vector2[numParticles];
        densities = new Vector2[numParticles];

        spatialLookup = new SpatialLookup2D(numParticles);

        SmoothingRadius = smoothingRadius;

        Vector2 s = boundsSize * 0.9f;
        Vector2 halfS = s * 0.5f;
        int numX = Mathf.CeilToInt(
            Mathf.Sqrt(s.X / s.Y * numParticles + (s.X - s.Y) * (s.X - s.Y) / (4 * s.Y * s.Y)) - (s.X - s.Y) / (2 * s.Y));
        int numY = Mathf.CeilToInt(numParticles / (float)numX);
        int i = 0;
        for (int y = 0; y < numY; y++)
        {
            for (int x = 0; x < numX; x++)
            {
                if (i >= numParticles) break;

                float tx = numX <= 1 ? 0.5f : x / (numX - 1f);
                float ty = numY <= 1 ? 0.5f : y / (numY - 1f);

                positions[i] = s * new Vector2(tx, ty) - halfS;

                i++;
            }
        }

        if (particleMultiMesh != null)
        {
            particleMultiMesh.InstanceCount = numParticles;
        }
    }

    void GatherInput()
    {
        interactionInputPoint = GetLocalMousePosition();
        bool isPullInteraction = Input.IsMouseButtonPressed(MouseButton.Left);
        bool isPushInteraction = Input.IsMouseButtonPressed(MouseButton.Right);
        currInteractionStrength = 0;
        hasInteraction = false;
        if (isPullInteraction || isPushInteraction)
        {
            currInteractionStrength = isPushInteraction ? -interactionStrength : interactionStrength;
            hasInteraction = true;
        }
        QueueRedraw();
    }

    void RunSimulationFrame(float delta)
    {
        if (!isPaused)
        {
            float timeStep = delta / iterationsPerFrame * timeScale;

            GatherInput();

            for (int i = 0; i < iterationsPerFrame; i++)
            {
                RunSimulationStep(timeStep);
            }

            CallDeferred("UpdateVisuals");
        }
    }

    void RunSimulationStep(float delta)
    {
        // External
        const float predictionFactor = 1f / 120f;
        Parallel.For(0, numParticles, i =>
        {
            velocities[i] += ExternalForces(i) * delta;
            // velocities[i] += Vector2.Down * gravity * delta;
            predictedPositions[i] = positions[i] + velocities[i] * predictionFactor;
        });

        // Spatial Hash
        spatialLookup.Update(predictedPositions, smoothingRadius);

        // Density
        Parallel.For(0, numParticles, i =>
        {
            densities[i] = CalculateDensity(predictedPositions[i]);
        });

        // Pressure
        Parallel.For(0, numParticles, i =>
        {
            Vector2 pressureForce = CalculatePressureForce(i);
            velocities[i] += (pressureForce / densities[i].X) * delta;
        });

        // Viscosity
        Parallel.For(0, numParticles, i =>
        {
            Vector2 viscosityForce = CalculateViscosityForce(i);
            velocities[i] += viscosityForce * delta;
        });

        // Position
        Parallel.For(0, numParticles, i =>
        {
            positions[i] += velocities[i] * delta;
            HandleCollisions(i);
        });
    }

    void UpdateVisuals()
    {
        if (particleMultiMesh != null)
        {
            Transform2D tf = Transform2D.Identity;
            Color customData = new Color(0f, 0f, 0f, 0f);
            for (int i = 0; i < numParticles; i++)
            {
                tf.Origin = positions[i];
                customData.R = velocities[i].X;
                customData.G = velocities[i].Y;
                particleMultiMesh.SetInstanceTransform2D(i, tf);
                particleMultiMesh.SetInstanceCustomData(i, customData);
            }
        }
    }

    Vector2 ExternalForces(int particleIndex)
    {
        Vector2 gravityAccel = new Vector2(0, gravity);
        Vector2 position = positions[particleIndex];
        Vector2 velocity = velocities[particleIndex];

        if (currInteractionStrength != 0)
        {
            Vector2 inputPointOffset = interactionInputPoint - position;
            float sqrDst = inputPointOffset.LengthSquared();
            if (sqrDst < interactionRadius * interactionRadius)
            {
                float dst = Mathf.Sqrt(sqrDst);
                float edgeT = dst / interactionRadius;
                float centerT = 1f - edgeT;
                Vector2 dirToCenter = inputPointOffset / dst;

                float gravityWeight = 1f - (centerT * Mathf.Clamp(currInteractionStrength / 10, 0f, 1f));
                Vector2 accel = gravityAccel * gravityWeight + dirToCenter * centerT * currInteractionStrength;
                accel -= velocity * centerT;
                return accel;
            }
        }

        return gravityAccel;
    }

    Vector2 CalculateDensity(Vector2 samplePosition)
    {
        Vector2 density = Vector2.Zero;

        spatialLookup.ForeachPointWithinRadius(samplePosition, particleIndex =>
        {
            float dst = predictedPositions[particleIndex].DistanceTo(samplePosition);
            density.X += DensityKernel(dst, smoothingRadius);
            density.Y += NearDensityKernel(dst, smoothingRadius);
        });

        return density;
    }

    Vector2 CalculatePressureForce(int particleIndex)
    {
        Vector2 pressureForce = Vector2.Zero;
        Vector2 position = predictedPositions[particleIndex];

        float pressure = PressureFromDensity(densities[particleIndex].X);
        float nearPressure = NearPressureFromDensity(densities[particleIndex].Y);

        spatialLookup.ForeachPointWithinRadius(position, otherParticleIndex =>
        {
            if (particleIndex == otherParticleIndex) return;

            Vector2 offsetToNeighbor = predictedPositions[otherParticleIndex] - position;
            float dst = offsetToNeighbor.Length();
            Vector2 dirToNeighbor = dst > 0 ? offsetToNeighbor / dst : Vector2.Right.Rotated(GD.Randf() * Mathf.Tau);

            float neighborDensity = densities[otherParticleIndex].X;
            float neighborNearDensity = densities[otherParticleIndex].Y;

            float sharedPressure = (pressure + PressureFromDensity(neighborDensity)) * 0.5f;
            float sharedNearPressure = (nearPressure + NearPressureFromDensity(neighborNearDensity)) * 0.5f;

            pressureForce += dirToNeighbor * DensityDerivative(dst, smoothingRadius) * sharedPressure / neighborDensity;
            pressureForce += dirToNeighbor * NearDensityDerivative(dst, smoothingRadius) * sharedNearPressure / neighborNearDensity;
        });

        return pressureForce;
    }

    Vector2 CalculateViscosityForce(int particleIndex)
    {
        Vector2 viscosityForce = Vector2.Zero;
        Vector2 position = predictedPositions[particleIndex];
        Vector2 velocity = velocities[particleIndex];

        spatialLookup.ForeachPointWithinRadius(position, otherParticleIndex =>
        {
            if (particleIndex == otherParticleIndex) return;

            float dst = predictedPositions[otherParticleIndex].DistanceTo(position);
            viscosityForce += (velocities[otherParticleIndex] - velocity) * ViscosityKernel(dst, smoothingRadius);
        });

        return viscosityForce * viscosityStrength;
    }

    void HandleCollisions(int particleIndex)
    {
        Vector2 position = positions[particleIndex];
        Vector2 velocity = velocities[particleIndex];

        Vector2 halfSize = boundsSize * 0.5f;

        if (Mathf.Abs(position.X) > halfSize.X)
        {
            position.X = halfSize.X * Mathf.Sign(position.X);
            velocity.X *= -1 * collisionDamping;
        }

        if (Mathf.Abs(position.Y) > halfSize.Y)
        {
            position.Y = halfSize.Y * Mathf.Sign(position.Y);
            velocity.Y *= -1 * collisionDamping;
        }

        positions[particleIndex] = position;
        velocities[particleIndex] = velocity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float PressureFromDensity(float density)
    {
        return (density - targetDensity) * pressureMultiplier;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float NearPressureFromDensity(float nearDensity)
    {
        return nearPressureMultiplier * nearDensity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float DensityKernel(float dst, float radius)
    {
        return SpikyPow2Kernel(dst, radius);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float NearDensityKernel(float dst, float radius)
    {
        return SpikyPow3Kernel(dst, radius);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float ViscosityKernel(float dst, float radius)
    {
        return SmoothPoly6Kernel(dst, radius);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float DensityDerivative(float dst, float radius)
    {
        return SpikyPow2Derivative(dst, radius);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float NearDensityDerivative(float dst, float radius)
    {
        return SpikyPow3Derivative(dst, radius);
    }

    float SmoothPoly6ScalingFactor;
    float SpikyPow2ScalingFactor;
    float SpikyPow3ScalingFactor;
    float SpikyPow2DerivativeScalingFactor;
    float SpikyPow3DerivativeScalingFactor;

    float SmoothPoly6Kernel(float dst, float radius)
    {
        if (dst < radius)
        {
            float v = radius * radius - dst * dst;
            return v * v * v * SmoothPoly6ScalingFactor;
        }
        return 0f;
    }

    float SpikyPow2Kernel(float dst, float radius)
    {
        if (dst < radius)
        {
            float v = radius - dst;
            return v * v * SpikyPow2ScalingFactor;
        }
        return 0f;
    }

    float SpikyPow3Kernel(float dst, float radius)
    {
        if (dst < radius)
        {
            float v = radius - dst;
            return v * v * v * SpikyPow3ScalingFactor;
        }
        return 0f;
    }

    float SpikyPow2Derivative(float dst, float radius)
    {
        if (dst <= radius)
        {
            float v = radius - dst;
            return -v * SpikyPow2DerivativeScalingFactor;
        }
        return 0f;
    }

    float SpikyPow3Derivative(float dst, float radius)
    {
        if (dst <= radius)
        {
            float v = radius - dst;
            return -v * v * SpikyPow3DerivativeScalingFactor;
        }
        return 0f;
    }
}