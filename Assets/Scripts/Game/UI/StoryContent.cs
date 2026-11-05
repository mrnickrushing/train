using System;
using System.Collections.Generic;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// The territory's story: Silas Pemberton bankrolls your railroad to grow the town before rival baron
    /// Cyrus Blackwell can claim the territory. Clara Whitlock plans the six territory projects (the
    /// chapters); Hank runs the yard. Each beat plays once per save (GameState.storySeen).
    /// </summary>
    public sealed class StoryDirector : MonoBehaviour
    {
        static StoryLine L(string who, string text) { return new StoryLine(who, text); }

        static readonly Dictionary<string, (string backdrop, StoryLine[] lines)> Beats = new Dictionary<string, (string, StoryLine[])>
        {
            ["intro"] = ("LoadingArt", new[]
            {
                L("silas", "So this is {town}. One water tower, one depot, and a great deal more dust than I was promised."),
                L("hank", "Give her time, Mr. Pemberton. Timber in the hills, coal in the hollow, and the rails run right through."),
                L("silas", "Then haul it. Every load builds this town, and every town we build is ground Cyrus Blackwell doesn't own."),
                L("hank", "Leave it to us. First job: send Dusty up to the logging camp for timber."),
            }),
            ["first_contract"] = ("contract", new[]
            {
                L("hank", "First order filled! Folks around here remember who delivers."),
                L("clara", "Clara Whitlock, territory engineer. Mr. Pemberton sent me. Keep that contract board busy and the town can pay for real works."),
                L("hank", "Real works?"),
                L("clara", "Telegraph lines. Trestles. A dam, one day. Grow the town and I'll bring you the first plans."),
            }),
            ["rival_arrives"] = ("levelup", new[]
            {
                L("cyrus", "Pemberton. I heard you'd sunk your fortune into a dust bowl with a water tower."),
                L("silas", "Blackwell. Come to admire the view?"),
                L("cyrus", "Come to buy it. The Iron Range Syndicate will own every mile of track in this territory. Sell now, while it's worth something."),
                L("silas", "Dry Creek isn't for sale. Good day, sir."),
                L("cyrus", "We'll see how long your little railroad keeps its steam."),
            }),
            ["rival_offer"] = ("newloco", new[]
            {
                L("cyrus", "Your foreman, isn't it? Hank. I pay my crews double what Pemberton does."),
                L("hank", "And you work 'em twice as hard on half the safety. I've seen your camps, Blackwell."),
                L("cyrus", "Loyalty to a losing line. Touching."),
                L("hank", "Funny. Our trains keep arriving on time. Yours keep arriving late. Good day."),
            }),
            ["golden_age"] = ("goldenage", new[]
            {
                L("silas", "Look at it, Hank. Streamliners. Steel. Electric light down Main Street."),
                L("hank", "Never thought I'd see a train that doesn't even look like a train."),
                L("silas", "The Golden Age, my friend. And it's ours, not Blackwell's."),
            }),
        };

        // Territory projects: Clara pitches each one when it unlocks; the finale plays when it's done.
        static readonly Dictionary<string, (StoryLine[] pitch, StoryLine[] finale)> Projects = new Dictionary<string, (StoryLine[], StoryLine[])>
        {
            ["telegraph"] = (new[]
            {
                L("clara", "Fort Calder is two days' ride away. Orders come late, payment comes later."),
                L("clara", "String a telegraph wire across the badlands and the whole territory talks to {town} in minutes."),
                L("hank", "Poles, wire and a lot of timber. We'll haul it."),
            }, new[]
            {
                L("clara", "The line's live! Fort Calder just wired its first order."),
                L("silas", "Splendid. And I hear Blackwell's men tried to cut it twice. Twice they failed."),
            }),
            ["trestle"] = (new[]
            {
                L("clara", "The western run loops forty miles around Crooked River. A trestle cuts straight across."),
                L("hank", "That's a lot of planks and a lot of nerve."),
                L("clara", "Bring me the planks. I'll supply the nerve."),
            }, new[]
            {
                L("clara", "Forty miles shorter. Every train west just got faster."),
                L("silas", "Blackwell bet his partners it would wash out by spring. I do enjoy collecting on a wager."),
            }),
            ["mineshaft"] = (new[]
            {
                L("hank", "Black Hollow's old shaft caved in back in '71. Nobody's been down since."),
                L("clara", "Then we sink a new one, timbered properly this time. There's a seam down there worth a fortune."),
            }, new[]
            {
                L("hank", "New shaft's open and the timbers are holding. The men call her the Deep Hollow."),
                L("clara", "Coal and iron for years. Blackwell's syndicate wanted this claim badly."),
                L("silas", "Then let him want."),
            }),
            ["resupply"] = (new[]
            {
                L("clara", "Winter's coming early. Fort Calder will be snowed in by month's end."),
                L("hank", "Provisions, flour, coal. Whatever they need, we'll roll it up there before the passes close."),
            }, new[]
            {
                L("clara", "The fort's stocked through spring. The colonel sent his thanks, and a promise."),
                L("silas", "The army ships with us from now on. Blackwell will be livid."),
            }),
            ["dam"] = (new[]
            {
                L("clara", "Silver Lake floods the lower valley every spring. A dam would hold it back and light the whole town."),
                L("silas", "Electric light? In {town}? Now that would make the eastern papers."),
                L("clara", "It will take steel, copper and a great deal of patience."),
            }, new[]
            {
                L("clara", "All finished!"),
                L("silas", "I had my reservations, given the cost. But look at it. The valley's lit like a city."),
                L("hank", "Blackwell rode by this morning. Didn't say a word. Just stared."),
            }),
            ["lighthouse"] = (new[]
            {
                L("clara", "Up the river there's a lake so big the steamers lose their way in a fog. Eagle Lake."),
                L("clara", "A lighthouse on Eagle Point would guide every boat home. But there's no road to an island."),
                L("hank", "Then the boats carry it out, plank by plank. I'll get the landing ready."),
            }, new[]
            {
                L("clara", "The light on Eagle Point is burning! You can see it from the lodge road."),
                L("hank", "First night it shone, three steamers came home through the fog. Not a scrape on any of 'em."),
            }),
            ["lodge"] = (new[]
            {
                L("silas", "City folk will pay a fortune for clean air and a view. Eagle Lake has both."),
                L("silas", "A grand log lodge on the shore, and every one of them rides our trains to reach it."),
                L("hank", "Logs, provisions, and a lot of fancy fixings. We'll haul it up the valley."),
            }, new[]
            {
                L("silas", "The Eagle Lake Lodge is booked through the summer. Booked!"),
                L("clara", "And every guest bought a ticket on our line to get here."),
                L("hank", "Blackwell tried to book a room. Under a false name. They turned him away."),
            }),
            ["goldenspike"] = (new[]
            {
                L("silas", "The eastern line is three hundred miles out. Meet it, and the territory becomes a state."),
                L("clara", "One final push. Steel, gold and every train we have."),
                L("hank", "Then let's drive that spike."),
            }, new[]
            {
                L("silas", "East and west, joined. The territory is a state, and {town} is its railroad capital."),
                L("clara", "Not bad for a dust bowl with a water tower."),
                L("hank", "Blackwell sold his syndicate shares last week. Moved back east, I hear."),
                L("silas", "Then the line is ours. Thank you, all of you."),
            }),
        };

        /// <summary>A line of thanks from a contract client, shown with the delivery celebration.</summary>
        public static string ClientThanks(string client)
        {
            switch (client)
            {
                case "Reverend Ashby": return "\"Bless you. The chapel roof will be done before the rains.\"";
                case "Doc Pettibone": return "\"Just in time. Half the town's got the grippe.\"";
                case "Golden Spur Saloon": return "\"Drinks are on the house tonight, partner!\"";
                case "Cavalry Quartermaster": return "\"The colonel sends his regards. Fine work.\"";
                case "Sheriff Barlow": return "\"Keeps the peace when folks are fed and working. Obliged.\"";
                case "Widow McAllister": return "\"You're a godsend. My boys can finish the barn now.\"";
                case "Mayor Quill": return "\"Splendid! I'll mention you in my next speech. Twice.\"";
                case "Prospector Gus": return "\"Hot dang! Now I can dig all winter!\"";
                case "Miss Adelaide's Hotel": return "\"Our guests will want for nothing. Thank you, dear.\"";
                case "The Iron Range Syndicate": return "\"Mr. Blackwell will... begrudgingly acknowledge receipt.\"";
                default: return "\"Right on time. We'll be ordering again.\"";
            }
        }

        public static StoryDirector Instance { get; private set; }
        GameSession S { get { return G.Session; } }

        void Awake() { Instance = this; }

        /// <summary>Called by UIRoot after the matching cinematic is queued, so scenes follow it.</summary>
        public void OnContract() { Beat("first_contract"); }
        public void OnProject(ProjectDef p) { Finale(p.Id); }

        /// <summary>True until the player has heard a project's story: its map pin wears a NEW bubble.</summary>
        public bool IsNew(string projectId) { return Projects.ContainsKey(projectId) && !Seen("pitch_" + projectId); }

        /// <summary>Plays a project's story (when it hasn't been heard), then runs <paramref name="after"/>.</summary>
        public void Open(string projectId, Action after)
        {
            if (!IsNew(projectId) || Celebrations.Instance == null) { after(); return; }
            Pitch(projectId);
            Celebrations.Instance.Enqueue(() => Then(after));
        }

        static System.Collections.IEnumerator Then(Action a) { a(); yield break; }

        void Start()
        {
            if (S.State.level <= 1 && S.State.storySeen.Count == 0) Invoke(nameof(Intro), 2.4f);
            // Catch up on pitches for projects already unlocked in older saves.
            foreach (var p in DepthContent.Projects)
                if (S.State.level >= p.UnlockLevel && !Seen("pitch_" + p.Id) && !S.State.storySeen.Contains("finale_" + p.Id)) MarkSeen("pitch_" + p.Id);
        }

        void Intro() { Beat("intro"); }

        public void OnLevel(int level)
        {
            if (level == 4) Beat("rival_arrives");
            if (level == 10) Beat("rival_offer");
            if (level == Balance.GoldenAgeLevel) Beat("golden_age");
            // New projects aren't told here: their map pin shows a NEW bubble, and the story plays when it's tapped.
        }

        bool Seen(string id) { return S.State.storySeen.Contains(id); }
        void MarkSeen(string id) { if (!Seen(id)) S.State.storySeen.Add(id); }

        void Beat(string id)
        {
            if (Seen(id) || !Beats.ContainsKey(id)) return;
            MarkSeen(id);
            var b = Beats[id];
            StoryScene.Play(b.backdrop, Fill(b.lines));
        }

        void Pitch(string projectId)
        {
            string id = "pitch_" + projectId;
            if (Seen(id) || !Projects.ContainsKey(projectId)) return;
            MarkSeen(id);
            StoryScene.Play("scene_" + projectId, Fill(Projects[projectId].pitch));
        }

        void Finale(string projectId)
        {
            string id = "finale_" + projectId;
            if (Seen(id) || !Projects.ContainsKey(projectId)) return;
            MarkSeen(id);
            StoryScene.Play("scene_" + projectId, Fill(Projects[projectId].finale));
        }

        StoryLine[] Fill(StoryLine[] lines)
        {
            var o = new StoryLine[lines.Length];
            for (int i = 0; i < lines.Length; i++) o[i] = new StoryLine(lines[i].Speaker, lines[i].Text.Replace("{town}", S.State.townName));
            return o;
        }
    }
}
