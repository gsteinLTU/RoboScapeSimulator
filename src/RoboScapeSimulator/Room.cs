using System.Diagnostics;
using System.Text.Json.Nodes;
using RoboScapeSimulator.Entities;
using RoboScapeSimulator.Entities.Robots;
using RoboScapeSimulator.Environments;

namespace RoboScapeSimulator
{
    /// <summary>
    /// A room shared between a set of users
    /// </summary>
    public class Room : IDisposable
    {
        /// <summary>
        /// List of sockets for users connected to this room
        /// </summary>
        public List<Node.Socket> activeSockets = new();

        /// <summary>
        /// Visible string used to identify this room
        /// </summary>
        public string Name;

        /// <summary>
        /// Extra string required to join the room, not intended for cryptographic security.
        /// </summary>
        public string Password;

        /// <summary>
        /// The simulated environment this Room represents
        /// </summary>
        public SimulationInstance SimInstance;

        /// <summary>
        /// Username/ID of this Room's creator
        /// </summary>
        public string? Creator;

        /// <summary>
        /// Time (in seconds) without interaction this room will stay alive for, default 15 minutes
        /// </summary>
        public float Timeout = 60 * 15;

        /// <summary>
        /// Previous time this Room was interacted with
        /// </summary>
        private DateTime lastInteractionTime;

        /// <summary>
        /// When going inactive, how long should this room be kept in "suspended animation" until deletion
        /// </summary>
        public float MaxHibernateTime = 60 * 60 * 24;

        /// <summary>
        /// Event called when this Room enters the hibernating state
        /// </summary>
        public event EventHandler? OnHibernateStart;

        /// <summary>
        /// Event called when this Room resumes if currently hibernating
        /// </summary>
        public event EventHandler? OnHibernateEnd;

        /// <summary>
        /// Event called when this room is destroyed
        /// </summary>
        public event EventHandler? OnRoomClose;

        /// <summary>
        /// ID of the environment used to launch this Room
        /// </summary>
        public string EnvironmentID;

        /// <summary>
        /// Time elapsed in the simulation instance
        /// </summary>
        public float Time { get => SimInstance.Time; }

        private bool hibernating = false;

        /// <summary>
        /// Is the Room currently suspended
        /// </summary>
        public bool Hibernating
        {
            get => hibernating; set
            {
                if (hibernating != value)
                {
                    // Fire appropriate event
                    if (hibernating)
                    {
                        OnHibernateEnd?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        OnHibernateStart?.Invoke(this, EventArgs.Empty);
                    }

                    hibernating = value;
                }
            }
        }

        /// <summary>
        /// The EnvironmentConfiguration used to setup this Room
        /// </summary>
        readonly EnvironmentConfiguration? environmentConfiguration;

        /// <summary>
        /// Instantiate a Room
        /// </summary>
        /// <param name="name">Name of this Room, leave empty to be assigned a random name</param>
        /// <param name="password">Password to restrict entry to this Room with</param>
        /// <param name="environment">ID of EnvironmentConfiguration to setup this Room with</param>
        public Room(string name = "", string password = "", string environment = "default")
        {
            Trace.WriteLine($"Setting up room {name} with environment {environment}");
            SimInstance = new SimulationInstance();

            // Find requested environment (or use default)
            if (!Environments.Any((env) => env.ID == environment))
            {
                Trace.WriteLine($"Environment {environment} not found");
            }
            var env = Environments.Find((env) => env.ID == environment) ?? Environments[0];

            EnvironmentID = env.ID;

            // Create instance of requested environment
            environmentConfiguration = (EnvironmentConfiguration?)env.Clone();

            if (environmentConfiguration == null)
            {
                environmentConfiguration = new DefaultEnvironment();
            }

            environmentConfiguration.Setup(this);

            // Give randomized default name
            if (string.IsNullOrWhiteSpace(name))
            {
                Random random = new();
                Name = "Room" + random.Next(0, 1000000).ToString("X4");
            }
            else
            {
                Name = name;
            }

            Password = password;

            LastInteractionTime = DateTime.Now;

            OnHibernateStart += (o, e) =>
            {
                Trace.WriteLine($"Room {Name} is now hibernating");
            };

            OnHibernateEnd += (o, e) =>
            {
                Trace.WriteLine($"Room {Name} is no longer hibernating");
            };

            Trace.WriteLine("Room " + Name + " created.");
        }

        public void Dispose()
        {
            OnRoomClose?.Invoke(this, EventArgs.Empty);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Collect information about the room's metadata in a format sendable to the client as JSON
        /// </summary>
        /// <returns>Structure containing room metadata</returns>
        public Dictionary<string, object> GetInfo()
        {
            return new Dictionary<string, object>()
        {
            {"background", ""},
            {"time", Time}
        };
        }


        /// <summary>
        /// Do not send the next update for this room (if needed for optimization purposes)
        /// </summary>
        public bool SkipNextUpdate = false;

        public void SendToClients(string eventName, params object[] args)
        {
            lock (activeSockets)
            {
                foreach (var socket in activeSockets)
                {
                    Utils.SendAsJSON(socket, eventName, args);
                }
            }
        }

        /// <summary>
        /// Adds a socket to active list and sets up message listeners
        /// </summary>
        /// <param name="socket">Socket to add to active sockets list</param>
        internal void AddSocket(Node.Socket socket)
        {
            LastInteractionTime = DateTime.Now;
            lock (activeSockets)
            {
                activeSockets.Add(socket);
            }
            socket.On("resetRobot", HandleResetRobot);
            socket.On("resetAll", HandleResetAll);
            socket.On("robotButton", HandleRobotButton);
        }

        private void HandleResetRobot(JsonNode[] args)
        {
            string robotID = args[0].ToString();
            ResetRobot(robotID);
        }

        private void HandleResetAll(JsonNode[] args)
        {
            foreach (var entity in SimInstance.Entities)
            {
                if (entity is IResettable resettable)
                {
                    resettable.Reset();
                }
            }
        }

        private void HandleRobotButton(JsonNode[] args)
        {
            string robotID = args[0].ToString();

            Robot? robot = SimInstance.Robots.FirstOrDefault(r => r?.ID == robotID, null);
            if (robot is ParallaxRobot parallaxRobot)
            {
                parallaxRobot.OnButtonPress((bool)args[1]);
            }
        }

        /// <summary>
        /// Reset a robot based on its ID
        /// </summary>
        /// <param name="robotID">ID of robot to reset</param>
        public void ResetRobot(string robotID)
        {
            Robot? robot = SimInstance.Robots.FirstOrDefault(r => r?.ID == robotID, null);
            if (robot != null)
            {
                robot.Reset();
            }
            else
            {
                Trace.WriteLine("Attempt to reset unknown robot " + robot);
            }
        }

        /// <summary>
        /// Removes a socket from active list and removes message listeners
        /// </summary>
        /// <param name="socket">Socket to remove from active sockets list</param>
        internal void RemoveSocket(Node.Socket socket)
        {
            socket.Off("resetRobot", HandleResetRobot);
            socket.Off("resetAll", HandleResetAll);
            socket.Off("robotButton", HandleRobotButton);
            socket.Emit("roomLeft");
            lock (activeSockets)
            {
                activeSockets.Remove(socket);

                // Stop running room when no users
                if (activeSockets.Count == 0)
                {
                    Hibernating = true;
                }
            }
        }

        /// <summary>
        /// Lists the available environment types in a format sendable to the client as JSON
        /// </summary>
        /// <returns>Structure representing information about avaialble environment types</returns>
        public static List<Dictionary<string, object>> ListEnvironments()
        {
            return Environments.Select(
                (environmentType) => new Dictionary<string, object> {
                { "Name", environmentType.Name },
                { "ID", environmentType.ID }
            }).ToList();
        }

        /// <summary>
        /// Available environment types
        /// </summary>
        internal static List<EnvironmentConfiguration> Environments = new()
        {
            new DefaultEnvironment(),
            new WallEnvironment(),
            new SquareDrivingEnvironment(),
            new FourColorRobotsEnvironment(),
            new ObstacleCourseEnvironment(),
            // new LIDARTestEnvironment(),
            new TableEnvironment(2, 1, true),
            new TableEnvironment(2, 2, true),
            new LIDARRoadEnvironment(),
            new LIDARRoadEnvironment(LIDARRoadEnvironment.Courses.Hard),
            new LIDARRoadEnvironment(LIDARRoadEnvironment.Courses.VeryHard),
            new WaypointNavigationEnvironment()
        };

        public DateTime LastInteractionTime
        {
            get => lastInteractionTime;
            set
            {
                lastInteractionTime = value;

                // Wake up if sleeping
                Hibernating = false;
            }
        }

        /// <summary>
        /// Client-relevant information about a Room
        /// </summary>
        [Serializable]
        public struct RoomInfo
        {
            public string name;

            public bool hasPassword;

            public string environment;

            public DateTime lastInteractionTime;

            public bool isHibernating;

            public string creator;
        }

        /// <summary>
        /// Get the client-relevant information about this room
        /// </summary>
        /// <returns>Struct of information about this room</returns>
        public RoomInfo GetRoomInfo()
        {
            return new RoomInfo()
            {
                name = Name,
                creator = Creator ?? "",
                environment = EnvironmentID,
                hasPassword = !string.IsNullOrEmpty(Password),
                isHibernating = Hibernating,
                lastInteractionTime = LastInteractionTime
            };
        }

        /// <summary>
        /// Update the state of the Room and its simulation
        /// </summary>
        /// <param name="dt">Delta time between updates</param>
        public void Update(float dt)
        {
            if (Hibernating) return;

            // Check if too much time has passed
            if ((DateTime.Now - LastInteractionTime).TotalSeconds > Timeout)
            {
                // Go to sleep
                Hibernating = true;

                // Kick all users
                while (activeSockets.Count > 0)
                {
                    RemoveSocket(activeSockets[0]);
                }

                return;
            }

            SimInstance.Update(dt);
        }
    }
}