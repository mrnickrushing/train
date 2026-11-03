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
            var card = UIKit.KitImage(_safe, "card_wood", "Chapter");
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
            UIKit.Size(_chapterNext.gameObject, -1, 40);
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

        public void OpenTimeline()
        {
            ShowWindow("Railroad Timeline", content =>
            {
                var intro = UIKit.Label(content, "Each chapter ends when you reach its level and finish its territory project. Claim the reward to move on.", 22, UIKit.InkSoft, TextAnchor.MiddleCenter);
                UIKit.Size(intro.gameObject, -1, 34);
                var cur = S.CurrentChapter;
                foreach (var c in ChapterContent.Chapters) ChapterCard(content, c, cur);
                ScrollToCurrent(cur);
            }, 1220, 860);
        }

        void ScrollToCurrent(ChapterDef cur)
        {
            if (cur == null || _windowScroll == null) return;
            int i = ChapterContent.Chapters.IndexOf(cur);
            Canvas.ForceUpdateCanvases();
            _windowScroll.verticalNormalizedPosition = 1 - Mathf.Clamp01(i / (float)Mathf.Max(1, ChapterContent.Chapters.Count - 1));
        }

        void ChapterCard(Transform parent, ChapterDef c, ChapterDef current)
        {
            bool claimed = S.IsChapterClaimed(c), isCurrent = c == current, locked = !claimed && !isCurrent;
            bool complete = S.IsChapterComplete(c);

            var card = UIKit.Panel(parent, isCurrent ? Palette.Hex(0x3E2A1A) : UIKit.Wood, "Chapter " + c.Number, UIKit.Rounded, true);
            UIKit.Size(card.gameObject, -1, 330);
            if (isCurrent)
            {
                var o = card.gameObject.AddComponent<Outline>();
                o.effectColor = UIKit.Gold;
                o.effectDistance = new Vector2(4, -4);
            }
            UIKit.HStack(card.gameObject, 22, 16, TextAnchor.MiddleLeft);

            // Story art, desaturated while locked, with the chapter number on it.
            var artHolder = UIKit.Node("Art", card.transform);
            UIKit.Size(artHolder, 470, 298);
            var mask = artHolder.AddComponent<Image>();
            mask.sprite = UIKit.Rounded;
            mask.type = Image.Type.Sliced;
            artHolder.AddComponent<Mask>();
            var tex = LoadArt(c.Art);
            if (tex != null)
            {
                var raw = UIKit.Node("Pic", artHolder.transform).AddComponent<RawImage>();
                raw.texture = tex;
                raw.raycastTarget = false;
                UIKit.Stretch(raw.gameObject);
                var fit = raw.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = tex.width / (float)tex.height;
                if (locked) raw.color = new Color(0.35f, 0.33f, 0.32f);
            }
            var num = UIKit.Panel(artHolder.transform, isCurrent ? UIKit.Gold : claimed ? UIKit.Green : Palette.Hex(0x4A4440), "Num", UIKit.Circle, true);
            UIKit.Place(num.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -14), new Vector2(76, 76));
            var nt = UIKit.Label(num.transform, claimed ? "✓" : c.Number.ToString(), 44, isCurrent ? UIKit.Ink : Color.white, TextAnchor.MiddleCenter);
            nt.font = UIKit.Display;
            UIKit.Stretch(nt.gameObject);
            if (claimed)
            {
                var stamp = UIKit.Label(artHolder.transform, "COMPLETE", 54, new Color(0.3f, 0.75f, 0.35f, 0.95f), TextAnchor.MiddleCenter);
                stamp.font = UIKit.Display;
                UIKit.Stretch(stamp.gameObject);
                stamp.rectTransform.localRotation = Quaternion.Euler(0, 0, 12);
                stamp.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
            }
            else if (locked)
            {
                var lk = UIKit.Label(artHolder.transform, S.State.level >= c.StartLevel ? "UP NEXT" : "LEVEL " + c.StartLevel, 46, new Color(1, 1, 1, 0.9f), TextAnchor.MiddleCenter);
                lk.font = UIKit.Display;
                UIKit.Stretch(lk.gameObject);
                lk.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
            }

            // Text column
            var col = UIKit.Node("Col", card.transform);
            UIKit.VStack(col, 6, 0, TextAnchor.UpperLeft);
            UIKit.Size(col, -1, 298, 1);
            var title = UIKit.Label(col.transform, c.Title.ToUpperInvariant(), 36, isCurrent ? UIKit.Gold : UIKit.Cream, TextAnchor.MiddleLeft);
            title.font = UIKit.Display;
            UIKit.Size(title.gameObject, -1, 44);
            var levels = UIKit.Label(col.transform, "Levels " + c.StartLevel + "–" + c.EndLevel, 20, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft);
            UIKit.Size(levels.gameObject, -1, 24);
            var blurb = UIKit.Label(col.transform, c.Blurb, 20, UIKit.Cream, TextAnchor.UpperLeft);
            UIKit.Size(blurb.gameObject, -1, 52);

            // Requirements
            Requirement(col.transform, "Reach level " + c.EndLevel, S.State.level >= c.EndLevel, Mathf.Min(S.State.level, c.EndLevel) + "/" + c.EndLevel);
            if (c.ProjectId != null)
            {
                var p = DepthContent.Project(c.ProjectId);
                var pp = S.State.projects.FirstOrDefault(x => x.id == c.ProjectId);
                string st = pp == null ? "" : pp.done ? "done" : "stage " + Mathf.Min(pp.stage + 1, p.Stages.Length) + "/" + p.Stages.Length;
                Requirement(col.transform, "Build " + p.Name, S.IsChapterProjectDone(c), st);
            }

            // Unlocks + reward row
            var bottom = UIKit.Row(col.transform, 74, 10, TextAnchor.MiddleLeft);
            var unl = GameSession.UnlocksBetween(c.StartLevel, c.EndLevel);
            var locos = unl.Where(u => u.Kind == "locomotive").ToList();
            foreach (var u in locos.Take(2))
            {
                var tile = UIKit.Panel(bottom.transform, new Color(0, 0, 0, 0.3f), "Loco", UIKit.RoundedSmall);
                UIKit.Size(tile.gameObject, 110, 70);
                var img = ModelIcons.Image(tile.transform, ModelIcons.Locomotive(GameDatabase.Locomotive(u.Id)), 110, 70);
                UIKit.Stretch(img.gameObject);
            }
            int others = unl.Count - locos.Take(2).Count();
            if (others > 0)
            {
                var more = UIKit.Label(bottom.transform, "+" + others + " unlocks", 20, UIKit.Cream, TextAnchor.MiddleLeft);
                UIKit.Size(more.gameObject, 170, 70);
            }
            UIKit.Spacer(bottom.transform);
            var rew = UIKit.Node("Reward", bottom.transform);
            UIKit.HStack(rew, 4, 0, TextAnchor.MiddleRight);
            UIKit.Size(rew, 190, 70);
            UIKit.CoinIcon(rew.transform, 34);
            var rc = UIKit.Label(rew.transform, Fmt.Num(c.Reward.Coins), 24, UIKit.Cream, TextAnchor.MiddleLeft);
            rc.font = UIKit.Display;
            UIKit.Size(rc.gameObject, 70, 40);
            UIKit.NuggetIcon(rew.transform, 30);
            var rn = UIKit.Label(rew.transform, c.Reward.Nuggets.ToString(), 24, UIKit.Cream, TextAnchor.MiddleLeft);
            rn.font = UIKit.Display;
            UIKit.Size(rn.gameObject, 40, 40);
            if (isCurrent)
            {
                string id = c.Id;
                UIKit.Btn(bottom.transform, complete ? "Claim" : "In progress", complete ? UIKit.Green : UIKit.Disabled, () =>
                {
                    var r = S.ClaimChapter(id);
                    Result(r, Sfx.LevelUp);
                    if (r.Ok) RebuildWindow();
                }, 24, 170, 62, complete);
            }
        }

        static void Requirement(Transform parent, string text, bool done, string progress)
        {
            var row = UIKit.Row(parent, 30, 8, TextAnchor.MiddleLeft);
            var dot = UIKit.Panel(row.transform, done ? UIKit.Green : new Color(1, 1, 1, 0.2f), "Dot", UIKit.Circle);
            UIKit.Size(dot.gameObject, 22, 22);
            var t = UIKit.Label(row.transform, text, 22, done ? new Color(0.7f, 0.95f, 0.7f) : UIKit.Cream, TextAnchor.MiddleLeft);
            UIKit.Size(t.gameObject, -1, 30, 1);
            var p = UIKit.Label(row.transform, progress, 20, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleRight);
            UIKit.Size(p.gameObject, 110, 30);
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
