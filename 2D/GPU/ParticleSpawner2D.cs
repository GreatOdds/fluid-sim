using Godot;

[Tool]
[GlobalClass]
public partial class ParticleSpawner2D : Node
{
    [Export(PropertyHint.Range, "0, 100, 1, or_greater")]
    public int particleCount;

    [Export]
    public Vector2 initialVelocity;
    [Export]
    public Vector2 SpawnCenter
    {
        get => spawnCenter;
        set
        {
            spawnCenter = value;
            QueueRedraw();
        }
    }
    [Export]
    public Vector2 SpawnSize
    {
        get => spawnSize;
        set
        {
            spawnSize = value;
            QueueRedraw();
        }
    }
    [Export]
    public float jitterStrength;
    [Export]
    public bool ShowSpawnBoundsGizmo
    {
        get => showSpawnBoundsGizmo;
        set
        {
            showSpawnBoundsGizmo = value & Engine.IsEditorHint();
            if (showSpawnBoundsGizmo && boundsGizmo == null)
            {
                boundsGizmo = new();
                AddChild(boundsGizmo);
                boundsGizmo.Draw += OnBoundsGizmoDraw;
            }
            else if (!showSpawnBoundsGizmo && boundsGizmo != null)
            {
                boundsGizmo.Draw -= OnBoundsGizmoDraw;
                boundsGizmo.QueueFree();
                boundsGizmo = null;
            }
        }
    }

    Vector2 spawnCenter;
    Vector2 spawnSize;
    bool showSpawnBoundsGizmo = false;

    Node2D boundsGizmo;

    public struct ParticleSpawnData
    {
        public Vector2[] positions;
        public Vector2[] velocities;

        public ParticleSpawnData(int num)
        {
            positions = new Vector2[num];
            velocities = new Vector2[num];
        }
    }

    public ParticleSpawnData GetSpawnData()
    {
        var data = new ParticleSpawnData(particleCount);

        Vector2 s = spawnSize;
        int numX = Mathf.CeilToInt(Mathf.Sqrt(s.X / s.Y * particleCount + (s.X - s.Y) * (s.X - s.Y) / (4 * s.Y * s.Y)) - (s.X - s.Y) / (2 * s.Y));
        int numY = Mathf.CeilToInt(particleCount / (float)numX);
        int i = 0;

        for (int y = 0; y < numY; y++)
        {
            for (int x = 0; x < numX; x++)
            {
                if (i >= particleCount) break;

                float tx = numX <= 1 ? 0.5f : x / (numX - 1f);
                float ty = numY <= 1 ? 0.5f : y / (numY - 1f);

                float angle = GD.Randf() * Mathf.Tau;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 jitter = dir * jitterStrength * (GD.Randf() - 0.5f);
                data.positions[i] = new Vector2((tx - 0.5f) * spawnSize.X, (ty - 0.5f) * spawnSize.Y) + jitter + spawnCenter;
                data.velocities[i] = initialVelocity;
                i++;
            }
        }

        return data;
    }

    void QueueRedraw()
    {
        if (boundsGizmo != null)
        {
            boundsGizmo.QueueRedraw();
        }
    }

    void OnBoundsGizmoDraw()
    {
        if (!Engine.IsEditorHint()) return;
        boundsGizmo?.DrawRect(
            new Rect2(spawnCenter - 0.5f * spawnSize, spawnSize),
            new Color(1f, 1f, 0f, 0.5f),
            false
        );
    }
}