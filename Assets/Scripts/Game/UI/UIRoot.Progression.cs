using System.Linq;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>
    /// Progression you can see: a chapter card on the HUD (current chapter, progress, the next unlock)
    /// and the Railroad Timeline, which lays out every chapter with its story art, requirements, unlocks
    /// and reward.
    /// </summary>
    public sealed partial class UIRoot
    {
        GameObject _chapterCard;
        Text _chapterTitle, _chapterNext;
        Image _chapterFill;
        Button _chapterClaim;

        void BuildChapterHud()
        {
            var card = UIKit.KitImage(_safe, "tile_dark", "Chapter");
            _chapterCard = card.gameObject;
            UIKit.Place(_chapterCard, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -300), new Vector2(430, 118));
            HudShadow(card.gameObject);
            Clickable(_chapterCard, OpenTimeline);
            var v = UIKit.Node("V", card.transform);
            UIKit.Stretch(v, 12);
            UIKit.VStack(v, 4, 0, TextAnchor.UpperLeft);
            _chapterTitle = UIKit.Label(v.transform, "", 20, UIKit.Gold, TextAnchor.MiddleLeft);
            _chapterTitle.font = UIKit.Display;
            UIKit.OneLine(_chapterTitle, 16);
            var row = UIKit.Row(v.transform, 22, 8);
            _chapterFill = UIKit.Bar(row.transform, UIKit.Brass, 16, new Color(0, 0, 0, 0.4f));
            _chapterNext = UIKit.Label(v.transform, "", 19, UIKit.Cream, TextAnchor.MiddleLeft);
            UIKit.OneLine(_chapterNext, 12); // long unlock names shrink instead of spilling out of the card
            _chapterClaim = UIKit.Btn(card.transform, "Claim!", UIKit.Green, OpenTimeline, 22, 120, 50);
            UIKit.Place(_chapterClaim.gameObject, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-8, -4), new Vector2(120, 50));
        }

        void RefreshChapterHud()
        {
            if (_chapterCard == null) return;
            var c = S.CurrentChapter;
            _chapterCard.SetActive(c != null);
            if (c == null) return;
            _chapterTitle.text = "CHAPTER " + c.Number + " · " + c.Title.ToUpperInvariant();
            UIKit.SetBar(_chapterFill, S.ChapterProgress(c));
            bool done = S.IsChapterComplete(c);
            _chapterClaim.gameObject.SetActive(done);
            if (done) _chapterClaim.transform.localScale = Vector3.one * (1 + Mathf.Sin(Time.unscaledTime * 6) * 0.05f);
            _chapterNext.text = done ? "Chapter complete! Claim your reward." : NextUnlockText();
        }

        string NextUnlockText()
        {
            var next = S.NextUnlocks();
            if (next.Count == 0) return "Every locomotive and building unlocked.";
            var best = next.FirstOrDefault(u => u.Kind == "locomotive");
            if (best.Name == null) best = next.FirstOrDefault(u => u.Kind == "project");
            if (best.Name == null) best = next[0];
            string more = next.Count > 1 ? " +" + (next.Count - 1) + " more" : "";
            return "Next at Lv " + best.Level + ": " + best.Name + more;
        }

        // ================================================================ timeline

        public void OpenTimeline() { ShowBoard("Railroad Timeline", TimelineBoard); }

        void TimelineBoard(Transform body, Transform right)
        {
            var cur = S.CurrentChapter;
            _boardSub = cur != null ? "Chapter " + cur.Number + " of " + ChapterContent.Chapters.Count : "Every chapter told";
            Pill(right, null, "Reach the level and finish the project", 30);
            var panel = Box(body, BTile, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Stack(panel.gameObject, 0, new RectOffset(14, 14, 14, 14));
            // Open on the current chapter (cards are 560 wide; 0 = first).
            if (cur != null && !_scrollMemo.ContainsKey("timeline"))
                _scrollMemo["timeline"] = new Vector2(Mathf.Clamp01((ChapterContent.Chapters.IndexOf(cur) - 0.5f) / Mathf.Max(1, ChapterContent.Chapters.Count - 3)), 0);
            var row = Scroller(panel.transform, "timeline", true, 16);
            foreach (var c in ChapterContent.Chapters) ChapterCard(row, c, cur);
        }

        void ChapterCard(Transform parent, ChapterDef c, ChapterDef current)
        {
            bool claimed = S.IsChapterClaimed(c), isCurrent = c == current, locked = !claimed && !isCurrent;
            bool complete = S.IsChapterComplete(c);
            var card = Box(parent, isCurrent ? Palette.Hex(0x2C3C5E) : BCardDeep, 560, -1, "Chapter " + c.Number);
            Stack(card.gameObject, 8, new RectOffset(14, 14, 12, 12), TextAnchor.UpperLeft);
            if (isCurrent)
            {
                var o = card.gameObject.AddComponent<Outline>();
                o.effectColor = BGold;
                o.effectDistance = new Vector2(4, -4);
            }

            var photo = Photo(card.transform, LoadArt(c.Art), -1, 196, locked ? new Color(0.38f, 0.37f, 0.38f) : (Color?)null);
            Overlay(photo.transform, Fade(true), new Color(0.06f, 0.08f, 0.12f, 0.85f));
            var num = UIKit.KitImage(photo.transform, isCurrent ? "round_gold" : claimed ? "round_green" : "round_brown", "Num");
            UIKit.Place(num.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -12), new Vector2(80, 80));
            if (claimed)
            {
                var chk = CheckBadge(num.transform, 60);
                UIKit.Place(chk, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
            }
            else
            {
                var nt = T(num.transform, c.Number.ToString(), 46, Color.white, TextAnchor.MiddleCenter, true);
                UIKit.Stretch(nt.gameObject);
            }
            var title = T1(photo.transform, c.Title, 40, isCurrent ? BGold : Color.white, TextAnchor.LowerLeft, true);
            UIKit.Anchor(title.gameObject, Vector2.zero, new Vector2(1, 0), new Vector2(16, 10), new Vector2(-16, 60));
            string stamp = claimed ? "COMPLETE" : locked ? (S.State.level >= c.StartLevel ? "UP NEXT" : "LEVEL " + c.StartLevel) : null;
            if (stamp != null)
            {
                var st = T(photo.transform, stamp, 56, claimed ? BGood : Color.white, TextAnchor.MiddleCenter, true);
                UIKit.Anchor(st.gameObject, new Vector2(0, 0.3f), Vector2.one, Vector2.zero, Vector2.zero);
                st.rectTransform.localRotation = Quaternion.Euler(0, 0, claimed ? 10 : 0);
            }

            T1(card.transform, "Levels " + c.StartLevel + "–" + c.EndLevel, 28, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var blurb = T(card.transform, c.Blurb, 28, Color.white, TextAnchor.UpperLeft, false, UIKit.Heading);
            UIKit.Size(blurb.gameObject, -1, 64);
            Requirement(card.transform, "Reach level " + c.EndLevel, S.State.level >= c.EndLevel, Mathf.Min(S.State.level, c.EndLevel) + " / " + c.EndLevel);
            if (c.ProjectId != null)
            {
                var p = DepthContent.Project(c.ProjectId);
                var pp = S.State.projects.FirstOrDefault(x => x.id == c.ProjectId);
                string st = pp == null ? "" : pp.done ? "Done" : "Stage " + Mathf.Min(pp.stage + 1, p.Stages.Length) + " / " + p.Stages.Length;
                Requirement(card.transform, p.Name, S.IsChapterProjectDone(c), st);
            }

            var unl = GameSession.UnlocksBetween(c.StartLevel, c.EndLevel);
            var locos = unl.Where(u => u.Kind == "locomotive").Take(2).ToList();
            var ur = HBox(card.transform, 8, 0, TextAnchor.MiddleLeft, -1, 64);
            foreach (var u in locos) { var d = GameDatabase.Locomotive(u.Id); Well(ur.transform, 64, t => LocoArt(t, d, 58, 38)); }
            int others = unl.Count - locos.Count;
            if (others > 0) T(ur.transform, "+" + others + " unlocks", 28, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading).horizontalOverflow = HorizontalWrapMode.Overflow;
            Flex(ur.transform);
            GrantPills(ur.transform, c.Reward, 0);
            Flex(card.transform);

            if (isCurrent)
            {
                string id = c.Id;
                Big(card.transform, complete ? "Claim" : "In progress", complete ? UIKit.Green : UIKit.Disabled, () =>
                {
                    var r = S.ClaimChapter(id);
                    Result(r, Sfx.LevelUp);
                    if (r.Ok) RebuildWindow();
                }, -1, 80, complete, 38);
            }
            else Big(card.transform, claimed ? "Complete" : "Locked", UIKit.Disabled, null, -1, 80, false, 36);
        }

        static void Requirement(Transform parent, string text, bool done, string progress)
        {
            var row = Box(parent, done ? Palette.Hex(0x3E5A33) : BChip, -1, 50, "Req");
            Row(row.gameObject, 10, new RectOffset(10, 14, 0, 0));
            if (done) CheckBadge(row.transform, 40);
            else { var ring = Box(row.transform, BTile, 40, 40, "Open"); ring.sprite = UIKit.Circle; }
            UIKit.Size(T1(row.transform, text, 30, Color.white, TextAnchor.MiddleLeft, true).gameObject, -1, -1, 1);
            T(row.transform, progress, 28, done ? BGood : BLabel, TextAnchor.MiddleRight, true).horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        static Texture2D LoadArt(string id)
        {
            foreach (var folder in new[] { "UI/Story/", "UI/Cinematics/", "UI/" })
            {
                var t = Resources.Load<Texture2D>(folder + id);
                if (t != null) return t;
            }
            return null;
        }

        void OnChapterCompleted(ChapterDef c)
        {
            Celebrations.Instance.Confetti();
            Celebrations.Instance.Play("levelup", "Chapter " + c.Number + " Complete!", c.Title + ". The territory remembers.", force: true);
            var next = ChapterContent.Chapters.FirstOrDefault(x => x.Number == c.Number + 1);
            if (next != null) Toast("Chapter " + next.Number + ": " + next.Title + " begins", true);
        }
    }
}
