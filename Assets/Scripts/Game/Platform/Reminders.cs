using System;
using System.Collections.Generic;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace Ironhorse.Game
{
    /// <summary>
    /// Local reminder notifications, scheduled when the game goes to the background and cleared when it
    /// comes back: trains arriving, fuel topped up, production done, and a nudge after a day away.
    /// Permission is asked the first time the player dispatches a train (when a reminder makes sense).
    /// </summary>
    public static class Reminders
    {
        const string AskedKey = "notif.asked";
        const int MaxTrainAlerts = 6;

        public static bool Enabled { get { return G.Session != null && G.Session.State.settings.notifications; } }

        /// <summary>Asks iOS for permission once (after the first dispatch).</summary>
        public static void AskOnce()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (PlayerPrefs.GetInt(AskedKey, 0) == 1) return;
            PlayerPrefs.SetInt(AskedKey, 1);
            GameRoot.Instance.StartCoroutine(Ask());
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        static System.Collections.IEnumerator Ask()
        {
            using (var req = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, false))
                while (!req.IsFinished) yield return null;
        }
#endif

        /// <summary>Clears pending and delivered reminders and the app badge (on launch and resume).</summary>
        public static void Clear()
        {
#if UNITY_IOS && !UNITY_EDITOR
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
            iOSNotificationCenter.ApplicationBadge = 0;
#endif
        }

        /// <summary>Everything worth a reminder from now, as (seconds from now, title, body).</summary>
        public static List<(double secs, string title, string body)> Plan(GameSession s)
        {
            var list = new List<(double, string, string)>();
            double now = s.Now;
            foreach (var t in s.State.trains.Where(t => !t.IsIdle && t.arrivesAt > now + 30).OrderBy(t => t.arrivesAt).Take(MaxTrainAlerts))
            {
                var loco = GameDatabase.Locomotive(t.defId);
                var dest = GameDatabase.Destination(t.destinationId);
                string name = !string.IsNullOrEmpty(t.nickname) ? t.nickname : loco != null ? loco.Name : "Your train";
                list.Add((t.arrivesAt - now, name + " is back", dest != null ? "Home from " + dest.Name + " with a full load. Unload it at the station!" : "Unload its cargo at the station!"));
            }
            // Haulers: one alert per project, when the last load on the way gets there.
            foreach (var g in s.State.haulers.Where(h => !h.IsIdle && h.arrivesAt > now + 30).GroupBy(h => h.projectId)
                         .Select(g => g.OrderBy(h => h.arrivesAt).Last()).OrderBy(h => h.arrivesAt).Take(2))
            {
                var p = DepthContent.Project(g.projectId);
                if (p == null) continue;
                list.Add((g.arrivesAt - now, "Delivered to " + p.Name, "Your haulers dropped off their loads. Send the next batch to keep the crew building!"));
            }
            double fuel = s.FullFuelIn;
            if (fuel > 600) list.Add((fuel, "Fuel's topped up", "Your coal bunkers are full. Time to send the trains out!"));
            // The factory whose conveyor empties first (the legacy productionEndsAt field is always 0 now).
            var done = s.State.buildings.Where(b => b.queue.Count > 0 && b.queue[b.queue.Count - 1].endsAt > now + 60)
                .OrderBy(b => b.queue[b.queue.Count - 1].endsAt).FirstOrDefault();
            if (done != null)
            {
                var def = GameDatabase.Building(done.defId);
                list.Add((done.queue[done.queue.Count - 1].endsAt - now, (def != null ? def.Name : "Your workshop") + " is finished", "Fresh goods are ready to collect."));
            }
            // Payday: a nudge later today if it's unclaimed, otherwise tomorrow morning at 10.
            if (s.State.settings.tutorialDone)
            {
                double pay = s.CanClaimPayday ? Math.Min(3 * 3600, s.NextPaydayIn - 1800) : s.NextPaydayIn + 10 * 3600;
                int streak = s.PaydayStreak;
                if (pay > 600) list.Add((pay, "Payday in " + s.State.townName, streak > 1 ? "Your " + streak + "-day streak is on the line. Collect today's pay!" : "Your daily pay is waiting at the station."));
            }
            var tg = s.Telegram;
            if (tg != null && s.TelegramLeft(tg) > 45 * 60)
                list.Add((s.TelegramLeft(tg) - 30 * 60, "Telegram expiring!", GameSession.TelegramText(tg).Replace("in 2 hours", "in 30 minutes")));
            list.Add((24 * 3600, "The railhead is quiet", "Contracts are piling up on the board in " + s.State.townName + "."));
            return list;
        }

        /// <summary>Schedules the plan (on going to the background).</summary>
        public static void Schedule()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Clear();
            if (!Enabled || G.Session == null) return;
            if (iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus != AuthorizationStatus.Authorized) return;
            int i = 0;
            foreach (var (secs, title, body) in Plan(G.Session))
            {
                iOSNotificationCenter.ScheduleNotification(new iOSNotification
                {
                    Identifier = "ironhorse." + (i++),
                    Title = title,
                    Body = body,
                    ShowInForeground = false,
                    ForegroundPresentationOption = PresentationOption.Alert | PresentationOption.Sound,
                    Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = TimeSpan.FromSeconds(Math.Max(1, secs)), Repeats = false },
                });
            }
#endif
        }
    }
}
