using System.Numerics;
using RoboScapeSimulator.Physics;
using RoboScapeSimulator.Physics.Bepu;

namespace RoboScapeSimulator.Entities;

/// <summary> 
/// A non-solid box that tracks other Entities entering its volume
/// </summary>
public class Trigger : DynamicEntity, IResettable
{
    private static uint ID = 0;

    /// <summary>
    /// Should the Trigger only fire OnTriggerEnter once
    /// </summary>    
    public bool OneTime = false;

    private bool triggered = false;

    /// <summary>
    /// Invoked when an Entity enters the Trigger
    /// </summary>
    public event EventHandler<Entity>? OnTriggerEnter;

    /// <summary>
    /// Invoked onces for each Entity in the Trigger each Update
    /// </summary>
    public event EventHandler<Entity>? OnTriggerStay;

    /// <summary>
    /// Invoked when an Entity leaves the Trigger
    /// </summary>
    public event EventHandler<Entity>? OnTriggerExit;

    /// <summary>
    /// Invoked when the last Entity in the Trigger leaves
    /// </summary>
    public event EventHandler? OnTriggerEmpty;
    public event EventHandler? OnReset;

    /// <summary>
    /// Entities currently in the Trigger
    /// </summary>
    public List<Entity> InTrigger = new();

    private readonly List<Entity> lastInTrigger = new();

    /// <summary>
    /// Create a new Trigger
    /// </summary>
    /// <param name="room">Room to add the Trigger to</param>
    /// <param name="initialPosition">Position of the Trigger at start</param>
    /// <param name="initialOrientation">Orientation of the Trigger at start</param>
    /// <param name="width">X size</param>
    /// <param name="height">Y size</param>
    /// <param name="depth">Z size</param>
    /// <param name="oneTime">Should the Trigger only track the entry of the first Entity that enters</param>
    /// <param name="debug">Sets the Trigger to be visible in the client</param>
    public Trigger(Room room, in Vector3 initialPosition, in Quaternion initialOrientation, float width = 1, float height = 1, float depth = 1, bool oneTime = false, bool debug = false)
    {
        if (!debug)
        {
            VisualInfo = VisualInfo.None;
        }
        else
        {
            VisualInfo = VisualInfo.DefaultCube;
        }

        Name = $"trigger_{ID++}";

        Width = width;
        Height = height;
        Depth = depth;

        BodyReference = room.SimInstance.CreateBox(Name, initialPosition, initialOrientation, width, height, depth, 1, true);

        if(room.SimInstance is BepuSimulationInstance bepuSim && BodyReference is SimBodyBepu bepuBody){
            ref var bodyProperties = ref bepuSim.Properties.Allocate(bepuBody.BodyReference.Handle);
            bodyProperties = new BodyCollisionProperties { Friction = 1f, Filter = new SubgroupCollisionFilter(bepuBody.BodyReference.Handle.Value, 0), IsTrigger = true };
        } else {
            // Current simulation type does not support creating triggers
            throw new SimulationTypeNotSupportedException();
        }

        OneTime = oneTime;
        room.SimInstance.Entities.Add(this);
    }

    internal void EntityInside(Entity e)
    {
        if (e is Trigger)
        {
            return;
        }

        lock (InTrigger)
        {
            if (!InTrigger.Contains(e))
            {
                InTrigger.Add(e);
            }
        }
    }

    public void Reset()
    {
        lock (InTrigger)
        {
            triggered = false;
            InTrigger.Clear();
            OnReset?.Invoke(this, EventArgs.Empty);
        }
    }

    public override void Update(float dt)
    {
        base.Update(dt);

        lock (InTrigger)
        {
            // Find entities that are newly in the trigger this Update
            if (InTrigger.Any(o => !lastInTrigger.Contains(o)))
            {
                // OnEnter Event only occurs once if OneTime is set
                if (!OneTime || !triggered)
                {
                    foreach (var e in InTrigger.Where(o => !lastInTrigger.Contains(o)))
                    {
                        if (e != null)
                        {
                            OnTriggerEnter?.Invoke(this, e);
                        }
                    }

                    triggered = true;
                }
            }

            // Find entities that left in this Update
            foreach (var e in lastInTrigger.Where(o => !InTrigger.Contains(o)))
            {
                if (e != null)
                {
                    OnTriggerExit?.Invoke(this, e);
                }
            }

            // Find entities that are currently in the trigger
            if (InTrigger.Count > 0)
            {
                InTrigger.ForEach(e =>
                {
                    if (e != null)
                    {
                        OnTriggerStay?.Invoke(this, e);
                    }
                });
            }

            if (InTrigger.Count == 0 && lastInTrigger.Count > 0)
            {
                OnTriggerEmpty?.Invoke(this, EventArgs.Empty);
            }

            // Cycle lists
            lastInTrigger.Clear();
            lastInTrigger.AddRange(InTrigger);
            InTrigger.Clear();
        }
    }
}