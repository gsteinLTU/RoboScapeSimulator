using Microsoft.Extensions.Configuration;

namespace RoboScapeSimulator
{
    /// <summary>
    /// Stores settings for the program in general
    /// </summary>
    class SettingsManager
    {
        /// <summary>
        /// The NetsBlox server to connect to
        /// </summary>
        public static string RoboScapeHostWithoutPort
        {
            get
            {
                if (loadedSettings == null)
                {
                    LoadSettings();
                }

                return (loadedSettings?.RoboScapeHost) ?? DefaultSettings.RoboScapeHost ?? "";
            }
        }

        /// <summary>
        /// The RoboScape port for the given server
        /// </summary>
        public static int RoboScapePort
        {
            get
            {
                if (loadedSettings == null)
                {
                    LoadSettings();
                }

                return loadedSettings?.RoboScapePort ?? DefaultSettings.RoboScapePort ?? 0;
            }
        }


        /// <summary>
        /// The IoTScape port for the given server
        /// </summary>
        public static int IoTScapePort
        {
            get
            {
                if (loadedSettings == null)
                {
                    LoadSettings();
                }

                return loadedSettings?.IoTScapePort ?? DefaultSettings.IoTScapePort ?? 0;
            }
        }

        /// <summary>
        /// The maximum number of rooms allowed for the server instance
        /// </summary>
        public static int MaxRooms
        {
            get
            {
                if (loadedSettings == null)
                {
                    LoadSettings();
                }

                return loadedSettings?.MaxRooms ?? DefaultSettings.MaxRooms ?? 0;
            }
        }

        /// <summary>
        /// The port for the API server
        /// </summary>
        public static int APIPort
        {
            get
            {
                if (loadedSettings == null)
                {
                    LoadSettings();
                }

                return loadedSettings?.APIPort ?? DefaultSettings.APIPort ?? 0;
            }
        }

        /// <summary>
        /// The Main API server to connect to
        /// </summary>
        public static string MainAPIServer
        {
            get
            {
                if (loadedSettings == null)
                {
                    LoadSettings();
                }

                return (loadedSettings?.MainAPIServer) ?? DefaultSettings.MainAPIServer ?? "";
            }
        }

        static RoboScapeSimSettings? loadedSettings;

        /// <summary>
        /// Loads the configuration in appsettings.json
        /// </summary>
        private static void LoadSettings()
        {
            var config = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json").AddEnvironmentVariables().Build();

            var section = config.GetSection(nameof(RoboScapeSimSettings));
            loadedSettings = section.Get<RoboScapeSimSettings>();

            // Validate
            loadedSettings ??= DefaultSettings;

            if (string.IsNullOrWhiteSpace(loadedSettings.RoboScapeHost))
            {
                loadedSettings.RoboScapeHost = DefaultSettings.RoboScapeHost;
            }

            if (loadedSettings.RoboScapePort == null || loadedSettings.RoboScapePort <= 0)
            {
                loadedSettings.RoboScapePort = DefaultSettings.RoboScapePort;
            }

            if (loadedSettings.MaxRooms == null || loadedSettings.MaxRooms <= 0)
            {
                loadedSettings.MaxRooms = DefaultSettings.MaxRooms;
            }

            if (loadedSettings.APIPort == null || loadedSettings.APIPort <= 0)
            {
                loadedSettings.APIPort = DefaultSettings.APIPort;
            }

            if (string.IsNullOrWhiteSpace(loadedSettings.MainAPIServer))
            {
                loadedSettings.MainAPIServer = DefaultSettings.MainAPIServer;
            }
        }

        /// <summary>
        /// Default RoboScapeSimSettings to use when none is loaded externally
        /// </summary>
        readonly static RoboScapeSimSettings DefaultSettings = new()
        {
            RoboScapeHost = "editor.netsblox.org",
            RoboScapePort = 1973,
            IoTScapePort = 1978,
            MaxRooms = 64,
            APIPort = 8000,
            MainAPIServer = "https://roboscapeonlineapi.netsblox.org"
        };
    }

    /// <summary>
    /// Settings data for the program, as stored in appsettings.json
    /// </summary>
    public class RoboScapeSimSettings
    {
        public string? RoboScapeHost { get; set; }
        public int? RoboScapePort { get; set; }
        public int? IoTScapePort { get; set; }
        public int? MaxRooms { get; set; }
        public int? APIPort { get; set; }
        public string? MainAPIServer { get; set; }
    }
}