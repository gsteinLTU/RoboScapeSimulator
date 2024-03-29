using System.Numerics;

namespace RoboScapeSimulator.Entities.Drones;

class Drone : DynamicEntity, IResettable
{
    /// <summary>
    /// Reference to the room this drone is inside of
    /// </summary>
    internal Room room;

    /// <summary>
    /// Position where drone was created
    /// </summary>
    internal Vector3 _initialPosition;

    /// <summary>
    /// Orientation where drone was created
    /// </summary>
    internal Quaternion _initialOrientation;

    static internal int id = 0;

    public Drone(Room room, in Vector3? position = null, in Quaternion? rotation = null, in VisualInfo? visualInfo = null, float spawnHeight = 0.2f, float radius = 0.175f, float height = 0.025f)
    {
        this.room = room;
        var rng = new Random();

        VisualInfo = visualInfo ?? new VisualInfo() { ModelName = "quadcopter.glb", ModelScale = 2f * radius / 0.175f };

        Name = "drone" + id++;

        BodyReference = room.SimInstance.CreateBox(Name,
            position ?? new Vector3(rng.Next(-5, 5), spawnHeight, rng.Next(-5, 5)),
            rotation ?? Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), (float)rng.NextDouble() * MathF.PI),
            radius * 2, height, radius * 2, 0.5f * MathF.Pow(radius / 0.175f, 3));

        // Calculate moment of inertia
        I.M11 = (BodyReference.Mass * 0.3f) / 2f * radius * radius + 2f / 0.5f * (BodyReference.Mass * 0.7f) * MathF.Pow(height / 2f, 2);
        I.M33 = I.M11;
        I.M22 = (BodyReference.Mass * 0.3f) * radius * radius + 2f / 0.5f * (BodyReference.Mass * 0.7f) * MathF.Pow(height / 2f, 2);

        L = radius;

        D = k_D * new Vector3(
            L * height,
            L * L,
            L * height
        );

        _initialPosition = Position;
        _initialOrientation = Orientation;

        room.SimInstance.Entities.Add(this);
        DroneService droneService = new(this);
        droneService.Setup(room);
    }

    public readonly int NumMotors = 4;

    public readonly float MaxMotorSpeed = 6000;

    public float[] MotorSpeeds = { 0, 0, 0, 0 };
    public float[] MotorSpeedTargets = { 0, 0, 0, 0 };

    public event EventHandler? OnReset;

    public void Reset()
    {
        // Reset position and speed
        BodyReference.Position = _initialPosition;
        BodyReference.Orientation = _initialOrientation;
        BodyReference.AngularVelocity = new Vector3();
        BodyReference.LinearVelocity = new Vector3();

        // Set all speeds to 0
        Array.Fill(MotorSpeeds, 0);
        Array.Fill(MotorSpeedTargets, 0);
    }

    readonly float k_M = 1.5e-9f;
    readonly float k_F = 6.11e-8f;
    readonly float k_m = 20;

    /// <summary>
    /// Drag coefficient
    /// </summary>
    readonly float k_D = 0.5f;

    /// <summary>
    /// Arm length
    /// </summary>
    readonly float L = 0.175f;

    /// <summary>
    /// Moment of Inertia
    /// </summary>
    /// <returns></returns>
    Matrix4x4 I = Utils.MakeMatrix3x3(2.32e-3f, 0, 0,
                                        0, 4e-3f, 0,
                                        0, 0, 2.32e-3f);

    /// <summary>
    /// Drag coefficent
    /// </summary>
    /// <returns></returns>
    Vector3 D = new();

    public override void Update(float dt)
    {
        base.Update(dt);

        // Update motor speeds
        for (int i = 0; i < MotorSpeeds.Length; i++)
        {
            MotorSpeeds[i] = MathF.Min(MaxMotorSpeed, MotorSpeedTargets[i]);
            //MotorSpeeds[i] += dt * k_m * (MathF.Min(MaxMotorSpeed, MotorSpeedTargets[i]) - MotorSpeeds[i]);
        }

        var motorForces = MotorSpeeds.Select(speed => speed * speed * k_F).ToArray();
        Vector3 updateLinearAcc = (1.0f / BodyReference.Mass) * Vector3.Transform(new Vector3(0, motorForces.Sum(), 0), BodyReference.Orientation);

        Vector3 updateAngularAcc = new();

        Vector3 tempCross = Vector3.Cross(BodyReference.AngularVelocity, new Vector3(
            I.M11 * BodyReference.AngularVelocity.X, I.M22 * BodyReference.AngularVelocity.Y, I.M33 * BodyReference.AngularVelocity.Z)
        );

        float gamma = k_M / k_F;

        updateAngularAcc += Vector3.Transform(Vector3.UnitX, BodyReference.Orientation) *
            1f / I.M11 * (L * (motorForces[1] - motorForces[3]) - tempCross.X);

        updateAngularAcc += Vector3.Transform(Vector3.UnitY, BodyReference.Orientation) *
            1f / I.M22 * (gamma * (motorForces[0] - motorForces[1] + motorForces[2] - motorForces[3]) - tempCross.Y);

        updateAngularAcc += Vector3.Transform(Vector3.UnitZ, BodyReference.Orientation) *
            1f / I.M33 * (L * (motorForces[2] - motorForces[0]) - tempCross.Z);

        updateAngularAcc = Vector3.Transform(updateAngularAcc, BodyReference.Orientation);

        var linearDrag = -D * (BodyReference.LinearVelocity * BodyReference.LinearVelocity) / BodyReference.Mass;

        updateLinearAcc -= linearDrag;

        BodyReference.LinearVelocity += dt * updateLinearAcc;
        BodyReference.AngularVelocity += dt * updateAngularAcc;
    }

    /// <summary>
    /// State of the Drone's controller
    /// SetMotorSpeed - Manually set speed of motors (hardest)
    /// SetAnglesAndSpeed - Set target pitch/yaw/roll and speed
    /// SetTargetVelocityXZ - Set target for X/Z axes, target height on Y axis
    /// SetTargetVelocity - Set requested speeds on X/Y/Z axes
    /// GoToCoords - Set target X/Y/Z coordinates (easiest)
    /// </summary>
    internal enum DroneDriveState
    {
        SetMotorSpeed, SetAnglesAndSpeed, SetTargetVelocityXZ, SetTargetVelocity, GoToCoords
    }

    internal DroneDriveState DriveState = DroneDriveState.SetMotorSpeed;  
}