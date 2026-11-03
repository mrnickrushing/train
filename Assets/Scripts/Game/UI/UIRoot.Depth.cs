using System;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>Territory projects, the Foreman's orders and blueprint UI.</summary>
    public sealed partial class UIRoot
    {
        Text _foremanBadge;
        string _ordersTab = "orders";

        // ================================================================ Foreman button (left side)

        void BuildDepthHud()
        {
            var holder = UIKit.Node("Foreman", _safe);
            UIKit.Place(holder, new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 190), new Vector2(150, 150));
            var circle = HudTile(holder.transform, "Foreman", t => ForemanPortrait(t, 96), 140);
            Clickable(holder, OpenForeman);
            _foremanBadge = UIKit.Badge(circle.transform, "", UIKit.Green);
            _foremanBadge.transform.parent.gameObject.SetActive(false);
        }

        void RefreshDepthHud()
        {
            if (_foremanBadge == null) return;
            bool claim = S.CanClaimTaskSet;
            _foremanBadge.text = claim ? "!" : S.TasksDone + "/3";
            _foremanBadge.transform.parent.GetComponent<Image>().color = claim ? UIKit.Green : UIKit.BrassDark;
            _foremanBadge.transform.parent.gameObject.SetActive(true);
        }

        /// <summary>Old Hank, the Dry Creek stationmaster, drawn from UI shapes until real art arrives.</summary>
        static void ForemanPortrait(Transform parent, float size)
        {
            var sp = UIKit.IconSprite("foreman");
            if (sp != null)
            {
                // Painted portrait, cropped into the round badge.
                var mask = UIKit.Panel(parent, Palette.Hex(0x3C6E91), "Hank", UIKit.Circle);
                UIKit.Size(mask.gameObject, size, size);
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                var pic = UIKit.Node("Face", mask.transform).AddComponent<Image>();
                pic.sprite = sp;
                pic.preserveAspect = true;
                pic.raycastTarget = false;
                UIKit.Stretch(pic.gameObject, -size * 0.04f);
                return;
            }
            var h = UIKit.Node("Hank", parent);
            UIKit.Size(h, size, size);
            Color skin = Palette.Hex(0xE0B48A), coat = Palette.Hex(0x2F4A6B), hat = Palette.Hex(0x3A2A20), mustache = Palette.Hex(0xD8D2C8);
            Shape(h, coat, new Vector2(0.5f, 0.16f), new Vector2(size * 0.8f, size * 0.34f));
            Shape(h, skin, new Vector2(0.5f, 0.5f), new Vector2(size * 0.44f, size * 0.46f), UIKit.Circle);
            Shape(h, mustache, new Vector2(0.5f, 0.42f), new Vector2(size * 0.36f, size * 0.09f));
            Shape(h, UIKit.Wood, new Vector2(0.4f, 0.55f), new Vector2(size * 0.06f, size * 0.06f), UIKit.Circle);
            Shape(h, UIKit.Wood, new Vector2(0.6f, 0.55f), new Vector2(size * 0.06f, size * 0.06f), UIKit.Circle);
            Shape(h, hat, new Vector2(0.5f, 0.75f), new Vector2(size * 0.62f, size * 0.08f));
            Shape(h, hat, new Vector2(0.5f, 0.84f), new Vector2(size * 0.4f, size * 0.2f));
            Shape(h, UIKit.Brass, new Vector2(0.5f, 0.79f), new Vector2(size * 0.4f, size * 0.04f));
        }

        // ================================================================ Foreman's orders

        public void OpenForeman()
        {
            ShowWindow("Foreman's Orders", content =>
            {
                var set = S.CurrentTaskSet;
                var head = Card(content, 170, Palette.Hex(0xC9D6E3));
                ForemanPortrait(head.transform, 140);
                var hc = Col(head.transform);
                Line(hc.transform, set.Title + "   (set " + (set.Index + 1) + ")", 30, UIKit.Ink, true);
                var bubble = UIKit.Panel(hc.transform, Color.white, "Bubble", UIKit.Rounded, true);
                UIKit.Size(bubble.gameObject, -1, 64);
                var q = UIKit.Label(bubble.transform, set.Quote, 22, UIKit.Ink, TextAnchor.MiddleLeft);
                UIKit.Stretch(q.gameObject, 14);
                Line(hc.transform, "— Hank, Dry Creek stationmaster", 18, UIKit.InkSoft);

                for (int i = 0; i < set.Tasks.Length; i++)
                {
                    var task = set.Tasks[i];
                    bool done = S.IsTaskDone(i);
                    var row = Card(content, 96, done ? Palette.Hex(0xBFDDB0) : (Color?)null);
                    var dot = UIKit.Panel(row.transform, done ? UIKit.Green : UIKit.Disabled, "Dot", UIKit.Circle);
                    UIKit.Size(dot.gameObject, 30, 30);
                    var label = Line(row.transform, DepthContent.Describe(task), 24, UIKit.Ink, true);
                    label.GetComponent<LayoutElement>().flexibleWidth = 1;
                    var barHolder = UIKit.Node("Bar", row.transform);
                    UIKit.Size(barHolder, 280, 40);
                    UIKit.HStack(barHolder, 0);
                    var bar = UIKit.Bar(barHolder.transform, UIKit.Green, 34);
                    var bt = UIKit.Label(bar.transform.parent, "", 20, UIKit.Cream, TextAnchor.MiddleCenter, true, FontStyle.Bold);
                    UIKit.Stretch(bt.gameObject);
                    bt.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.5f);
                    int idx = i;
                    Action live = () =>
                    {
                        int p = S.TaskProgress(idx), c = S.CurrentTaskSet.Tasks[idx].Count;
                        UIKit.SetBar(bar, p / (float)c);
                        bt.text = Fmt.Num(p) + " / " + Fmt.Num(c);
                    };
                    live(); Live(live);
                }

                Heading(content, "Reward for all three");
                var rew = Card(content, 90);
                Line(rew.transform, set.Reward + " · +" + (60 + 30 * set.Index) + " XP", 22).GetComponent<LayoutElement>().flexibleWidth = 1;
                UIKit.Btn(rew.transform, "Claim", UIKit.Green, () => Result(S.ClaimTaskSet(), Sfx.LevelUp), 26, 200, 66, S.CanClaimTaskSet);
            }, 1060, 820);
            WindowFigure("char_foreman");
        }

        // ================================================================ Territory projects (Orders window tab)

        void ProjectsTab(Transform content)
        {
            Line(content, "Big builds across the territory. Deliver goods in any amount. Each stage pays out, and finishing a project grants a permanent bonus.", 20, UIKit.InkSoft);
            foreach (var def in DepthContent.Projects)
            {
                var pr = S.Progress(def.Id);
                bool locked = !S.IsProjectUnlocked(def);
                var card = UIKit.Panel(content, locked ? new Color(0.74f, 0.77f, 0.82f) : pr.done ? Palette.Hex(0xBFDDB0) : Palette.Hex(0xD9C6E8), "Project", null, true);
                UIKit.Size(card.gameObject, -1, 290);
                UIKit.HStack(card.gameObject, 18, 16, TextAnchor.MiddleLeft);

                var col = Col(card.transform);
                var title = UIKit.Row(col.transform, 40, 12);
                UIKit.Label(title.transform, def.Name, 28, UIKit.Ink, TextAnchor.MiddleLeft, true);
                UIKit.Label(title.transform, locked ? "Level " + def.UnlockLevel : pr.done ? "COMPLETE" : Mathf.Min(pr.stage + 1, def.Stages.Length) + "/" + def.Stages.Length, 24, locked ? UIKit.InkSoft : Purple, TextAnchor.MiddleRight, true);
                Line(col.transform, def.Description, 20, UIKit.InkSoft);
                Line(col.transform, "Bonus: " + def.PerkText + " · Reward: " + def.Reward, 18, UIKit.Ink);

                if (locked || pr.done) continue;
                var stage = def.Stages[pr.stage];
                var res = GameDatabase.Resource(stage.ResourceId);
                // Notepad slip for the current stage, like a job card.
                var slip = UIDeco.Slip(card.transform, 260);
                UIKit.Size(slip.parent.gameObject, 400, 260);
                var top = UIKit.Row(slip, 50, 10, TextAnchor.MiddleCenter);
                UIKit.ResIcon(top.transform, stage.ResourceId, 46);
                var dl = UIKit.Label(top.transform, "DELIVER " + stage.Amount, 30, UIKit.Ink, TextAnchor.MiddleLeft, false, FontStyle.Bold);
                dl.font = UIKit.Display;
                dl.fontStyle = FontStyle.Normal; // the face is already bold
                var barRow = UIKit.Node("Progress", slip);
                UIKit.Size(barRow, -1, 26);
                UIKit.HStack(barRow, 0);
                var bar = UIKit.Bar(barRow.transform, UIKit.Green, 24, new Color(0.25f, 0.18f, 0.12f, 0.55f));
                UIKit.SetBar(bar, pr.delivered / (float)stage.Amount);
                var bt = UIKit.Label(bar.transform.parent, pr.delivered + " / " + stage.Amount + "   (have " + S.Amount(stage.ResourceId) + ")", 18, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                UIKit.Stretch(bt.gameObject);
                bt.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.5f);
                UIDeco.Rewards(slip, S, stage.Coins, stage.Xp, 0);
                string id = def.Id;
                UIKit.Btn(slip, "Deliver " + res.Name, UIKit.Green, () => Result(S.DeliverToProject(id), Sfx.Deliver), 20, -1, 52, S.Amount(stage.ResourceId) > 0);
            }
        }

        // ================================================================ Blueprints (depot)

        void BlueprintRow(Transform col, LocomotiveDef d)
        {
            if (d.Blueprints <= 0) return;
            int have = S.BlueprintsOf(d.Id);
            var row = UIKit.Row(col, 40, 0, TextAnchor.MiddleCenter);
            if (S.CanAssemble(d))
            {
                // Ready: the whole row becomes the Assemble button, so it fits narrow depot cards.
                string id = d.Id;
                var b = UIKit.Btn(row.transform, "Assemble (" + have + "/" + d.Blueprints + ")", UIKit.Green, () => Result(S.AssembleLocomotive(id), Sfx.Bell), 20, -1, 40);
                UIKit.Size(b.gameObject, -1, 40, 1);
                return;
            }
            var tag = UIKit.Panel(row.transform, UIKit.Blue, "Blueprints", UIKit.RoundedSmall);
            UIKit.Size(tag.gameObject, -1, 32, 1);
            var bar = UIKit.Panel(tag.transform, Palette.Hex(0x7FB0D8), "Fill", UIKit.RoundedSmall);
            UIKit.Anchor(bar.gameObject, Vector2.zero, new Vector2(Mathf.Clamp01(have / (float)d.Blueprints), 1), Vector2.zero, Vector2.zero);
            var l = UIKit.Label(tag.transform, "Blueprints " + have + "/" + d.Blueprints, 18, UIKit.Cream, TextAnchor.MiddleCenter, true, FontStyle.Bold);
            UIKit.Stretch(l.gameObject);
        }
    }
}
