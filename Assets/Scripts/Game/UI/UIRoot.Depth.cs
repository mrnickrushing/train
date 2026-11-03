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

        // ================================================================ Territory projects (Orders window tab)

        void ProjectCard(Transform p, ProjectDef def)
        {
            var pr = S.Progress(def.Id);
            bool locked = !S.IsProjectUnlocked(def);
            var card = Box(p, BCardDeep, 560, -1, "Project " + def.Id);
            Stack(card.gameObject, 10, new RectOffset(14, 14, 14, 16), TextAnchor.UpperLeft);

            var photo = Photo(card.transform, Resources.Load<Texture2D>("UI/Story/scene_" + def.Id), -1, 220, locked ? new Color(0.4f, 0.4f, 0.44f) : (Color?)null);
            Overlay(photo.transform, Fade(true), new Color(0.06f, 0.08f, 0.12f, 0.9f));
            var name = T(photo.transform, def.Name, 40, Color.white, TextAnchor.LowerLeft, true);
            UIKit.Anchor(name.gameObject, Vector2.zero, Vector2.one, new Vector2(16, 10), new Vector2(-16, -10));
            var tag = Pill(photo.transform, null, locked ? "Level " + def.UnlockLevel : pr.done ? "Complete" : "Stage " + Mathf.Min(pr.stage + 1, def.Stages.Length) + " / " + def.Stages.Length, 28, pr.done ? BGood : Color.white);
            var tle = tag.GetComponent<LayoutElement>();
            if (tle == null) tle = tag.AddComponent<LayoutElement>();
            tle.ignoreLayout = true;
            UIKit.Place(tag, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -12), new Vector2(200, 44));
            tag.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (locked)
            {
                var lk = UIKit.Icon(photo.transform, "lock", 90);
                if (lk != null) UIKit.Place(lk, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(90, 90));
            }
            else if (pr.done)
            {
                var chk = CheckBadge(photo.transform, 64);
                var le = chk.GetComponent<LayoutElement>();
                if (le == null) le = chk.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
                UIKit.Place(chk, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(64, 64));
            }

            T1(card.transform, def.Description, 28, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            Pill(card.transform, null, "Bonus: " + def.PerkText, 28, BGold);
            Segs(card.transform, def.Stages.Length, pr.done ? def.Stages.Length : pr.stage, 0, 16);

            if (!locked && !pr.done)
            {
                var stage = def.Stages[pr.stage];
                var res = GameDatabase.Resource(stage.ResourceId);
                int have = S.Amount(stage.ResourceId);
                var row = HBox(card.transform, 12, 0, TextAnchor.MiddleLeft, -1, 76);
                Well(row.transform, 76, tr => UIKit.ResIcon(tr, stage.ResourceId, 52));
                var col = VBox(row.transform, 4, 0, TextAnchor.MiddleLeft, -1, -1, 1);
                var top = HBox(col.transform, 6, 0, TextAnchor.MiddleLeft, -1, 34);
                UIKit.Size(T1(top.transform, "Deliver " + res.Name, 30, Color.white, TextAnchor.MiddleLeft, true).gameObject, -1, -1, 1);
                T(top.transform, pr.delivered + " / " + stage.Amount, 30, Color.white, TextAnchor.MiddleRight, true).horizontalOverflow = HorizontalWrapMode.Overflow;
                var bar = Track(col.transform, 18, BGreen);
                UIKit.SetBar(bar, pr.delivered / (float)stage.Amount);
                T(col.transform, "You have " + Fmt.Num(have), 24, have > 0 ? BGood : BBad, TextAnchor.MiddleLeft, false, UIKit.Heading);

                var rw = HBox(card.transform, 8, 0, TextAnchor.MiddleLeft, -1, 48);
                T(rw.transform, "This stage", 26, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading).horizontalOverflow = HorizontalWrapMode.Overflow;
                if (stage.Coins > 0) Pill(rw.transform, t => UIKit.CoinIcon(t, 38), Fmt.Num(stage.Coins), 28, null, BTile);
                if (stage.Xp > 0) Pill(rw.transform, t => UIDeco.XpIcon(t, 38), stage.Xp.ToString(), 28, null, BTile);
            }
            var fin = HBox(card.transform, 8, 0, TextAnchor.MiddleLeft, -1, 48);
            T(fin.transform, "Finish", 26, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading).horizontalOverflow = HorizontalWrapMode.Overflow;
            GrantPills(fin.transform, def.Reward, 0);
            Flex(card.transform);

            if (locked) { Big(card.transform, "Unlocks at level " + def.UnlockLevel, UIKit.Disabled, null, -1, 88, false, 34); return; }
            if (pr.done) { Big(card.transform, "Bonus active", UIKit.Disabled, null, -1, 88, false, 34); return; }
            var st = def.Stages[pr.stage];
            string id = def.Id, rid = st.ResourceId;
            var btns = HBox(card.transform, 12, 0, TextAnchor.MiddleCenter, -1, 92);
            bool any = S.Amount(rid) > 0;
            Big(btns.transform, "Deliver", any ? UIKit.Green : UIKit.Disabled, () => Result(S.DeliverToProject(id), Sfx.Deliver), any ? 528 : 380, 88, any, 40);
            if (!any) Big(btns.transform, "Go", BBlue, () => GoGet(rid), 136, 88, true, 40);
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
