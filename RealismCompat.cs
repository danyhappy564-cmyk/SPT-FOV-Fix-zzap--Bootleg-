using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace FOVFix
{
    /// <summary>
    /// Reads the handful of Realism Mod statics this mod reacts to.
    ///
    /// These used to be direct references, which made Realism Mod a compile-time dependency:
    /// without RealismMod.dll on the build machine the project would not build at all, even
    /// though the plugin is designed to run perfectly well without it - Plugin.Awake only
    /// constructs this class when Chainloader reports Realism is loaded. Reading the values
    /// reflectively removes the build dependency without changing what happens at runtime: one
    /// build works with or without Realism, and the integration turns itself on the moment
    /// Realism is installed.
    ///
    /// Every member is resolved once on construction. If any of them is missing - a Realism
    /// version that moved or renamed something - the whole compat layer reports itself
    /// unresolved rather than half-reading a stance state, and Plugin treats that as
    /// "Realism not present", which is the behaviour this mod already has when it is not.
    /// </summary>
    public class RealismCompat
    {
        private const string PluginGuid = "RealismMod";

        public bool HasShoulderContact { get; private set; } = false;
        public bool IsMachinePistol { get; private set; } = false;
        public bool DoAltPistol { get; private set; } = false;
        public float StanceBlenderValue { get; private set; } = 0f;
        public float StanceBlenderTarget { get; private set; } = 0f;
        public bool StancesAreEnabled { get; private set; } = false;
        public bool DoPatrolStanceAdsSmoothing { get; private set; } = false;
        public bool StopCameraMovmentForCollision { get; private set; } = false;
        public float CameraMovmentForCollisionSpeed { get; private set; } = 1f;
        public bool IsColliding { get; private set; } = false;
        public bool IsLeftShoulder { get; private set; } = false;
        public bool IsResettingShoulder { get; private set; } = false;
        public bool IsFiringMovement { get; private set; } = false;
        public bool DoAltRifle { get; private set; } = false;

        /// <summary>False when Realism's API could not be read; the caller should then behave as if Realism were absent.</summary>
        public bool IsResolved { get; private set; }

        private Func<object> _hasShoulderContact;
        private Func<object> _isMachinePistol;
        private Func<object> _enableAltPistol;
        private Func<object> _enableAltRifle;
        private Func<object> _stanceBlender;
        private Func<object> _finishedUnPatrolStancing;
        private Func<object> _stopCameraMovement;
        private Func<object> _isColliding;
        private Func<object> _cameraMovmentForCollisionSpeed;
        private Func<object> _isLeftShoulder;
        private Func<object> _isLeftStanceResetState;
        private Func<object> _serverConfig;

        public RealismCompat()
        {
            Assembly realism = FindAssembly();
            if (realism == null)
            {
                Utils.Logger.LogWarning("FOVFix: Realism Mod is loaded but its assembly could not be found; stance integration is off.");
                return;
            }

            Type weaponStats = realism.GetType("RealismMod.WeaponStats");
            Type pluginConfig = realism.GetType("RealismMod.PluginConfig");
            Type stanceController = realism.GetType("RealismMod.StanceController");
            Type realismPlugin = realism.GetType("RealismMod.Plugin");

            _hasShoulderContact = StaticGetter(weaponStats, "HasShoulderContact");
            _isMachinePistol = StaticGetter(weaponStats, "IsMachinePistol");
            _enableAltPistol = StaticGetter(pluginConfig, "EnableAltPistol");
            _enableAltRifle = StaticGetter(pluginConfig, "EnableAltRifle");
            _stanceBlender = StaticGetter(stanceController, "StanceBlender");
            _finishedUnPatrolStancing = StaticGetter(stanceController, "FinishedUnPatrolStancing");
            _stopCameraMovement = StaticGetter(stanceController, "StopCameraMovement");
            _isColliding = StaticGetter(stanceController, "IsColliding");
            _cameraMovmentForCollisionSpeed = StaticGetter(stanceController, "CameraMovmentForCollisionSpeed");
            _isLeftShoulder = StaticGetter(stanceController, "IsLeftShoulder");
            _isLeftStanceResetState = StaticGetter(stanceController, "IsLeftStanceResetState");
            _serverConfig = StaticGetter(realismPlugin, "ServerConfig");

            IsResolved = _hasShoulderContact != null && _isMachinePistol != null
                      && _enableAltPistol != null && _enableAltRifle != null
                      && _stanceBlender != null && _finishedUnPatrolStancing != null
                      && _stopCameraMovement != null && _isColliding != null
                      && _cameraMovmentForCollisionSpeed != null && _isLeftShoulder != null
                      && _isLeftStanceResetState != null && _serverConfig != null;

            if (IsResolved)
                Utils.Logger.LogInfo("FOVFix: Realism Mod stance integration active.");
            else
                Utils.Logger.LogWarning(
                    "FOVFix: Realism Mod is loaded but its stance API does not look the way this mod expects, " +
                    "so the integration is off. FOV Fix still works, it just will not follow Realism's stances.");
        }

        public void Update()
        {
            if (!IsResolved) return;

            HasShoulderContact = Read(_hasShoulderContact, false);
            IsMachinePistol = Read(_isMachinePistol, false);
            DoAltPistol = ReadConfigEntry(_enableAltPistol, false);
            DoAltRifle = ReadConfigEntry(_enableAltRifle, false);

            object blender = _stanceBlender();
            StanceBlenderTarget = ReadMember(blender, "Target", 0f);
            StanceBlenderValue = ReadMember(blender, "Value", 0f);

            StancesAreEnabled = ReadMember(_serverConfig(), "enable_stances", false);
            DoPatrolStanceAdsSmoothing = !Read(_finishedUnPatrolStancing, true);
            StopCameraMovmentForCollision = Read(_stopCameraMovement, false);
            IsColliding = Read(_isColliding, false);
            CameraMovmentForCollisionSpeed = Read(_cameraMovmentForCollisionSpeed, 1f);
            IsLeftShoulder = Read(_isLeftShoulder, false);
            IsResettingShoulder = Read(_isLeftStanceResetState, false);
        }

        private static Assembly FindAssembly()
        {
            BepInEx.PluginInfo info;
            if (Chainloader.PluginInfos.TryGetValue(PluginGuid, out info) && info.Instance != null)
                return info.Instance.GetType().Assembly;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == PluginGuid) return assembly;

            return null;
        }

        /// <summary>A getter for a public static field or property, whichever the member turns out to be.</summary>
        private static Func<object> StaticGetter(Type type, string name)
        {
            if (type == null) return null;

            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo field = type.GetField(name, flags);
            if (field != null) return () => field.GetValue(null);

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.GetGetMethod(true) != null) return () => property.GetValue(null, null);

            return null;
        }

        private static T Read<T>(Func<object> getter, T fallback)
        {
            try
            {
                object value = getter();
                return value is T typed ? typed : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>Realism's config entries are BepInEx ConfigEntry&lt;T&gt;, so the value is one hop further in.</summary>
        private static T ReadConfigEntry<T>(Func<object> getter, T fallback)
        {
            try
            {
                return ReadMember(getter(), "Value", fallback);
            }
            catch
            {
                return fallback;
            }
        }

        private static T ReadMember<T>(object instance, string name, T fallback)
        {
            if (instance == null) return fallback;

            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                Type type = instance.GetType();

                FieldInfo field = type.GetField(name, flags);
                if (field != null) return field.GetValue(instance) is T fieldValue ? fieldValue : fallback;

                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null) return property.GetValue(instance, null) is T propertyValue ? propertyValue : fallback;
            }
            catch
            {
                // fall through
            }

            return fallback;
        }
    }
}
