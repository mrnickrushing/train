using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>Starts the Apple services once the world is up and drives them through the app lifecycle.</summary>
    public sealed class PlatformServices : MonoBehaviour
    {
        float _syncTimer = 5f;

        void Start()
        {
            Reminders.Clear();
            AppleAccount.Start();
            GameCenter.Start();
            G.Session.TrainDeparted += _ => Reminders.AskOnce();
            G.Session.LeveledUp += _ => GameCenter.Sync();
            G.Session.ContractCompleted += _ => GameCenter.Sync();
        }

        void Update()
        {
            AppleAccount.Update();
            _syncTimer -= Time.unscaledDeltaTime;
            if (_syncTimer <= 0) { _syncTimer = 60f; GameCenter.Sync(); } // dispatches, coins, chapters
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) { GameCenter.Sync(); Reminders.Schedule(); }
            else Reminders.Clear();
        }

        void OnApplicationQuit() { Reminders.Schedule(); }
    }
}
