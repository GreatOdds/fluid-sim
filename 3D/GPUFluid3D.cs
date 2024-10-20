using Godot;
using System;

public partial class GPUFluid3D : Node3D
{
    [ExportGroup("Settings")]
    [Export(PropertyHint.Range, "0, 1, 0.001, or_greater")]
    public float timeScale = 1f;
    [Export]
    public bool usePhysicsProcess = true;
    [Export(PropertyHint.Range, "0, 5, 1, or_greater")]
    public uint iterationsPerFrame = 1;
    [Export]
    public Vector3 gravity = new Vector3(0f, -9.8f, 0);
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

}
