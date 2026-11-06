using System.Linq;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// One-time feature tips after the first-launch guide: the first time a storyline bubble, haulage, the work
    /// trains, the clouds over Eagle Lake or a windfall turns up, Hank or Clara point it out once (Got it, or doing the
    /// thing, ends the tip for good). Uses the guide's bubble, ring and arrow; never shows over a board or a site.
    /// </summary>
    public sealed partial class UIRoot
    {
        bool TipSeen(string key) { var l = S.State.settings.tipsSeen; return l != null && l.Contains(key); }

        void MarkTip(string key)
        {
            var st = S.State.settings;
            if (st.tipsSeen == null) st.tipsSeen = new System.Collections.Generic.List<string>();
            if (!st.tipsSeen.Contains(key)) st.tipsSeen.Add(key); // saved with the next autosave
        }

        CoachStep TipNow()
        {
            if (S.State.settings.tipsSeen == null) return null;
            if (WindowOpen && _windowTitle == "Depot" && _depotTab == "haulers") MarkTip("tip.rail"); // found it
            if (SiteOpen) MarkTip("tip.haul");
            if (WindowOpen || SiteOpen) return null;
            var sites = ProjectSites.Instance;

            // A storyline is waiting: tap its character's bubble to hear what's being built.
            var fresh = DepthContent.Projects.FirstOrDefault(p => S.IsProjectUnlocked(p) && !S.Progress(p.Id).done
                && StoryDirector.Instance != null && StoryDirector.Instance.IsNew(p.Id));
            if (!TipSeen("tip.story") && fresh != null && sites != null)
            {
                var at = sites.PinAt(fresh.Id);
                if (at.HasValue)
                {
                    var r = Haulage.Route(fresh.Id);
                    string who = r != null ? r.CharacterName : "Someone";
                    return World("tip.story", who + " has a story for you! Tap the bubble with the <b>NEW</b> flag to hear what we're building at <b>" + fresh.Name + "</b>.", at.Value + Vector3.up * 4, r != null && r.Character == "engineer");
                }
            }
            else if (fresh == null && !TipSeen("tip.story") && S.State.projects.Any(p => p.delivered > 0 || p.done)) MarkTip("tip.story");

            // Haulage: projects are supplied by road and river, not by train.
            if (!TipSeen("tip.haul") && S.HaulageUnlocked && S.State.haulers.Count > 0 && sites != null)
            {
                if (S.State.haulers.Any(h => !h.IsIdle)) MarkTip("tip.haul");
                else
                {
                    var need = DepthContent.Projects.FirstOrDefault(p => S.IsProjectUnlocked(p) && S.StillToSend(p.Id) > 0
                        && (StoryDirector.Instance == null || !StoryDirector.Instance.IsNew(p.Id)));
                    var at = need != null ? sites.PinAt(need.Id) : null;
                    if (at.HasValue)
                        return World("tip.haul", "Projects get their goods by road and river. Tap <b>" + need.Name + "</b>, pick your <b>Freight Wagon</b> and send it a load from the warehouse.", at.Value + Vector3.up * 4);
                }
            }

            // Work trains: the trestle can be supplied by rail.
            var wt = Haulage.Hauler("worktrain");
            var trestle = DepthContent.Project("trestle");
            if (!TipSeen("tip.rail") && wt != null && trestle != null && S.IsHaulerUnlocked(wt) && S.IsProjectUnlocked(trestle) && !S.Progress("trestle").done
                && TipSeen("tip.haul"))
            {
                if (S.State.haulers.Any(h => Haulage.Hauler(h.defId).OnRail)) MarkTip("tip.rail");
                else return Point("tip.rail", "The trestle's a rail job: a <b>Work Train</b> hauls a big load there along the scenic loop. You'll find one in the <b>Depot</b> under <b>Haulers</b>.", Nav("Depot"));
            }

            // The clouds over Eagle Lake country.
            if (!TipSeen("tip.north") && !S.NorthOpen && S.Level >= 9 && CameraRig.Instance != null && CameraRig.Instance.Focus.z > 300)
                return World("tip.north", "Clouds hide <b>Eagle Lake</b> country up north. Finish the <b>Fort Resupply</b> storyline and they'll lift, with new storylines waiting.", new Vector3(-20, 30, RegionFog.EdgeZ + 30), true);

            // A windfall: free goods at an outpost.
            var wf = S.State.events.FirstOrDefault(e => e.kind == MapEventKind.Windfall && S.Now < e.expiresAt);
            if (!TipSeen("tip.windfall") && wf != null)
            {
                var at = DestinationSites.PinAt(wf.destinationId);
                if (at.HasValue) return World("tip.windfall", "A windfall! Goods are piled up at the outpost, free for the taking. Tap them to haul them into your warehouse.", at.Value + Vector3.up * 20);
            }
            return null;
        }
    }
}
