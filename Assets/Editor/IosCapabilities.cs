#if UNITY_IOS
using AppleAuth.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Ironhorse.EditorTools
{
    /// <summary>
    /// Adds the app's capabilities to the exported Xcode project: Game Center and Sign in with Apple
    /// (entitlements file + frameworks). The bundle id has both enabled in the Apple Developer portal, so
    /// the App Store profile created at signing time carries them. Reminders are local notifications and
    /// need no capability.
    /// </summary>
    public static class IosCapabilities
    {
        public const string Entitlements = "Ironhorse.entitlements";

        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            var caps = new ProjectCapabilityManager(projPath, Entitlements, null, proj.GetUnityMainTargetGuid());
            caps.AddGameCenter();
            caps.AddSignInWithAppleWithCompatibility();
            caps.WriteToFile();
        }

        /// <summary>The game asks for notification permission after the first dispatch, not at launch.</summary>
        public static void ConfigureNotifications()
        {
            Unity.Notifications.NotificationSettings.iOSSettings.RequestAuthorizationOnAppLaunch = false;
            Unity.Notifications.NotificationSettings.iOSSettings.AddRemoteNotificationCapability = false;
        }
    }
}
#endif
