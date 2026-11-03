using System;
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Extensions;
using AppleAuth.Interfaces;
using AppleAuth.Native;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// Optional Sign in with Apple. The game has no server: signing in ties purchases (RevenueCat) to the
    /// player's Apple ID, so they come back on any device, and gives the HUD the player's name.
    /// </summary>
    public static class AppleAccount
    {
        const string UserKey = "apple.user", NameKey = "apple.name";
        static IAppleAuthManager _auth;

        public static bool Supported { get { return Application.platform == RuntimePlatform.IPhonePlayer && AppleAuthManager.IsCurrentPlatformSupported; } }
        public static string UserId { get { return PlayerPrefs.GetString(UserKey, ""); } }
        public static bool SignedIn { get { return UserId.Length > 0; } }
        public static string DisplayName { get { return PlayerPrefs.GetString(NameKey, ""); } }
        public static event Action Changed;

        /// <summary>Call once at startup: checks a saved sign-in is still valid and links purchases to it.</summary>
        public static void Start()
        {
            if (!Supported) return;
            _auth = new AppleAuthManager(new PayloadDeserializer());
            if (!SignedIn) return;
            _auth.GetCredentialState(UserId, state =>
            {
                if (state == CredentialState.Authorized) Store.Current.Identify(UserId);
                else SignOut(); // revoked in Settings, or a different Apple ID
            }, error => Store.Current.Identify(UserId)); // offline: keep the saved sign-in
        }

        /// <summary>Pumps the native callbacks (call every frame).</summary>
        public static void Update() { if (_auth != null) _auth.Update(); }

        public static void SignIn(Action<bool, string> done)
        {
            if (_auth == null) { done(false, "Sign in with Apple isn't available on this device."); return; }
            _auth.LoginWithAppleId(new AppleAuthLoginArgs(LoginOptions.IncludeFullName), credential =>
            {
                var apple = credential as IAppleIDCredential;
                PlayerPrefs.SetString(UserKey, credential.User);
                // Apple sends the name only on the very first sign-in; keep what we had otherwise.
                if (apple != null && apple.FullName != null && !string.IsNullOrEmpty(apple.FullName.GivenName))
                    PlayerPrefs.SetString(NameKey, apple.FullName.GivenName);
                PlayerPrefs.Save();
                Store.Current.Identify(credential.User);
                Changed?.Invoke();
                done(true, null);
            }, error =>
            {
                var cancelled = error.GetAuthorizationErrorCode() == AuthorizationErrorCode.Canceled;
                done(false, cancelled ? null : "Sign in with Apple failed. Please try again.");
            });
        }

        public static void SignOut()
        {
            PlayerPrefs.DeleteKey(UserKey);
            PlayerPrefs.DeleteKey(NameKey);
            PlayerPrefs.Save();
            Store.Current.Identify(null);
            Changed?.Invoke();
        }
    }
}
