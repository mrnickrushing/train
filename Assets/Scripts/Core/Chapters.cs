using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    /// <summary>
    /// The campaign's spine: chapters run from one level to the next milestone and end with a territory
    /// project. A chapter is complete when you reach its end level and finish its project; then its
    /// reward can be claimed. Unlocks shown for a chapter are everything that opens in its level range.
    /// </summary>
    public sealed class ChapterDef
    {
        public int Number;
        public string Id;
        public string Title;
        public string Blurb;
        public int StartLevel;
        public int EndLevel;
        public string ProjectId;   // null: no project in this chapter
        public string Art;         // backdrop id (Resources/UI/Story, Cinematics or UI)
        public Grant Reward;
    }

    public static class ChapterContent
    {
        public static readonly List<ChapterDef> Chapters = new List<ChapterDef>
        {
            C(1, "railhead", "The Railhead", "A water tower, a depot and a lot of dust. Haul timber, open the sawmill, fill your first orders.", 1, 3, null, "LoadingArt", 600, 5),
            C(2, "wires", "Wires Across the Badlands", "Fort Calder is two days' ride away. String a telegraph line and the territory starts to talk.", 3, 5, "telegraph", "scene_telegraph", 1500, 8),
            C(3, "crookedriver", "Crooked River", "Shave forty miles off the western run with a timber trestle over the gorge.", 5, 7, "trestle", "scene_trestle", 3000, 10),
            C(4, "deephollow", "The Deep Hollow", "Sink a new shaft at Black Hollow before Blackwell's syndicate claims the seam.", 7, 9, "mineshaft", "scene_mineshaft", 5000, 12),
            C(5, "fortcalder", "Winter at Fort Calder", "Keep the cavalry supplied before the passes close, and win the army's freight.", 9, 12, "resupply", "scene_resupply", 8000, 15),
            C(6, "silverlake", "Silver Lake", "Tame the river with a great dam and light the whole valley.", 12, 15, "dam", "scene_dam", 12000, 20),
            C(7, "goldenspike", "The Golden Spike", "Streamliners, steel and silver. Meet the eastern line and make the territory a state.", 15, 20, "goldenspike", "scene_goldenspike", 25000, 40),
        };

        static ChapterDef C(int n, string id, string title, string blurb, int from, int to, string project, string art, int coins, int nuggets)
        {
            return new ChapterDef { Number = n, Id = id, Title = title, Blurb = blurb, StartLevel = from, EndLevel = to, ProjectId = project, Art = art,
                                    Reward = new Grant { Coins = coins, Nuggets = nuggets } };
        }

        public static ChapterDef Get(string id) { return Chapters.FirstOrDefault(c => c.Id == id); }
    }

    /// <summary>Something that opens up at a given level (for timelines and "next unlock" teasers).</summary>
    public struct Unlock
    {
        public int Level;
        public string Kind;   // "locomotive", "wagon", "building", "route", "project"
        public string Id;
        public string Name;
    }

    public sealed partial class GameSession
    {
        /// <summary>Every unlock in the game, ordered by level.</summary>
        public static List<Unlock> AllUnlocks()
        {
            var u = new List<Unlock>();
            u.AddRange(GameDatabase.Locomotives.Where(d => d.UnlockLevel > 1).Select(d => new Unlock { Level = d.UnlockLevel, Kind = "locomotive", Id = d.Id, Name = d.Name }));
            u.AddRange(GameDatabase.Wagons.Where(d => d.UnlockLevel > 1).Select(d => new Unlock { Level = d.UnlockLevel, Kind = "wagon", Id = d.Id, Name = d.Name }));
            u.AddRange(GameDatabase.Buildings.Where(d => d.UnlockLevel > 1).Select(d => new Unlock { Level = d.UnlockLevel, Kind = "building", Id = d.Id, Name = d.Name }));
            u.AddRange(GameDatabase.Destinations.Where(d => d.UnlockLevel > 1).Select(d => new Unlock { Level = d.UnlockLevel, Kind = "route", Id = d.Id, Name = d.Name }));
            u.AddRange(DepthContent.Projects.Select(p => new Unlock { Level = p.UnlockLevel, Kind = "project", Id = p.Id, Name = p.Name }));
            return u.OrderBy(x => x.Level).ThenBy(x => x.Kind).ToList();
        }

        public static List<Unlock> UnlocksBetween(int fromLevel, int toLevelExclusive)
        {
            return AllUnlocks().Where(x => x.Level >= fromLevel && x.Level < toLevelExclusive).ToList();
        }

        /// <summary>The soonest things above the current level (all at the same next level).</summary>
        public List<Unlock> NextUnlocks()
        {
            var later = AllUnlocks().Where(x => x.Level > State.level).ToList();
            if (later.Count == 0) return later;
            int lvl = later[0].Level;
            return later.Where(x => x.Level == lvl).ToList();
        }

        public bool IsChapterClaimed(ChapterDef c) { return State.chaptersClaimed.Contains(c.Id); }

        public bool IsChapterProjectDone(ChapterDef c)
        {
            if (c.ProjectId == null) return true;
            var p = State.projects.FirstOrDefault(x => x.id == c.ProjectId);
            return p != null && p.done;
        }

        public bool IsChapterComplete(ChapterDef c) { return State.level >= c.EndLevel && IsChapterProjectDone(c); }

        /// <summary>The first chapter whose reward hasn't been claimed (null once the campaign is done).</summary>
        public ChapterDef CurrentChapter { get { return ChapterContent.Chapters.FirstOrDefault(c => !IsChapterClaimed(c)); } }

        /// <summary>0..1 progress through a chapter: levels count for most of it, the project for the rest.</summary>
        public float ChapterProgress(ChapterDef c)
        {
            float lv = c.EndLevel <= c.StartLevel ? 1 : System.Math.Min(1f, System.Math.Max(0f, (State.level - c.StartLevel + XpFraction) / (float)(c.EndLevel - c.StartLevel)));
            if (c.ProjectId == null) return lv;
            var def = DepthContent.Project(c.ProjectId);
            var p = State.projects.FirstOrDefault(x => x.id == c.ProjectId);
            float pj = p == null || def == null ? 0 : p.done ? 1 : p.stage / (float)def.Stages.Length;
            return lv * 0.6f + pj * 0.4f;
        }

        float XpFraction { get { return System.Math.Min(1f, State.xp / (float)System.Math.Max(1, Balance.XpForNextLevel(State.level))); } }

        public ActionResult ClaimChapter(string chapterId)
        {
            var c = ChapterContent.Get(chapterId);
            if (c == null) return ActionResult.Fail("Unknown chapter");
            if (IsChapterClaimed(c)) return ActionResult.Fail("Already claimed");
            if (c != CurrentChapter) return ActionResult.Fail("Finish the earlier chapters first");
            if (!IsChapterComplete(c)) return ActionResult.Fail("Chapter not finished yet");
            State.chaptersClaimed.Add(c.Id);
            Apply(c.Reward);
            RaiseChanged();
            ChapterCompleted?.Invoke(c);
            return ActionResult.Success("Chapter " + c.Number + " complete: " + c.Title + "!");
        }

        public event System.Action<ChapterDef> ChapterCompleted;
    }
}
