using System;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    public sealed partial class UIRoot
    {
        string _depotTab = "locos";
        BuildingCategory _buildTab = BuildingCategory.Residential;
        int _selectedTrain;

        // ---------------------------------------------------------------- shared bits

        static GameObject Card(Transform parent, float height, Color? color = null)
        {
            var c = color.HasValue ? UIKit.Panel(parent, color.Value, "Card") : UIKit.KitImage(parent, "card_light", "Card", height);
            UIKit.Size(c.gameObject, -1, height);
            UIKit.HStack(c.gameObject, 18, 14, TextAnchor.MiddleLeft);
            return c.gameObject;
        }

        static GameObject Col(Transform parent, float flex = 1, float width = -1)
        {
            var v = UIKit.Node("Col", parent);
            UIKit.VStack(v, 4, 0, TextAnchor.MiddleLeft);
            UIKit.Size(v, width, -1, width < 0 ? flex : -1);
            return v;
        }

        static Text Line(Transform parent, string text, int size = 22, Color? c = null, bool heading = false)
        {
            var t = UIKit.Label(parent, text, size, c ?? UIKit.Ink, TextAnchor.MiddleLeft, heading);
            UIKit.Size(t.gameObject, -1, size + 10);
            return t;
        }

        static string Plural(string noun) { return noun.EndsWith("ch") ? noun + "es" : noun + "s"; }

        static void Heading(Transform parent, string text)
        {
            var t = UIKit.Label(parent, text.ToUpperInvariant(), 26, Palette.Hex(0x2B3D5C), TextAnchor.MiddleLeft, false, FontStyle.Bold);
            t.font = UIKit.Display;
            t.fontStyle = FontStyle.Normal;
            UIKit.Size(t.gameObject, -1, 42);
            t.gameObject.AddComponent<Shadow>().effectColor = new Color(1, 1, 1, 0.5f);
        }

        void Tabs(Transform parent, (string label, bool active, Action onClick)[] tabs)
        {
            var row = UIKit.Row(parent, 60, 10, TextAnchor.MiddleCenter);
            foreach (var t in tabs)
                UIKit.Btn(row.transform, t.label, t.active ? UIKit.Brass : UIKit.WoodLight, () => { t.onClick(); RebuildWindow(); }, 24, 220, 62);
        }

        /// <summary>The locomotive's rendered 3D model on a rarity-colored tile (1.5:1).</summary>
        static GameObject LocoSwatch(Transform parent, LocomotiveDef d, float size = 90)
        {
            var bg = UIKit.Panel(parent, UIKit.RarityColor(d.Rarity), "Swatch", UIKit.RoundedGrad);
            UIKit.Size(bg.gameObject, size * 1.5f, size);
            var img = ModelIcons.Image(bg.transform, ModelIcons.Locomotive(d), size * 1.5f, size * 0.94f);
            UIKit.Place(img.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -size * 0.02f), new Vector2(size * 1.5f, size * 0.94f));
            return bg.gameObject;
        }

        /// <summary>Crew gauge, conveyor belt of job slots, and the controls to queue, collect, rush and extend.</summary>
        static void CrewIcon(Transform t)
        {
            var h = UIKit.Node("Crew", t);
            UIKit.Size(h, 56, 56);
            var helmet = UIKit.Panel(h.transform, UIKit.Gold, "Hat", UIKit.Circle);
            UIKit.Place(helmet.gameObject, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 18));
            var head = UIKit.Panel(h.transform, Palette.Hex(0xE0B48A), "Head", UIKit.Circle);
            UIKit.Place(head.gameObject, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));
            var body = UIKit.Panel(h.transform, UIKit.Blue, "Body", UIKit.RoundedSmall);
            UIKit.Place(body.gameObject, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38, 24));
        }

        static GameObject WagonSwatch(Transform parent, WagonDef wd)
        {
            var bg = UIKit.Panel(parent, UIKit.ParchmentDark * 0.92f, "Wagon", UIKit.RoundedSmall);
            UIKit.Size(bg.gameObject, 120, 76);
            var img = ModelIcons.Image(bg.transform, ModelIcons.Wagon(wd), 120, 75);
            UIKit.Stretch(img.gameObject);
            return bg.gameObject;
        }

        static GameObject BuildingSwatch(Transform parent, BuildingDef d)
        {
            Color c;
            switch (d.Category)
            {
                case BuildingCategory.Residential: c = Palette.Hex(0xC8A27A); break;
                case BuildingCategory.Production: c = Palette.Brick; break;
                case BuildingCategory.Civic: c = Palette.PaintBlue; break;
                default: c = Palette.PaintGreen; break;
            }
            var bg = UIKit.Panel(parent, c, "Swatch", UIKit.Rounded);
            UIKit.Size(bg.gameObject, 96, 96);
            var house = UIKit.Panel(bg.transform, UIKit.Cream, "House", UIKit.RoundedSmall);
            UIKit.Place(house.gameObject, new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52, 38));
            var roof = UIKit.Panel(bg.transform, UIKit.Wood, "Roof", UIKit.RoundedSmall);
            UIKit.Place(roof.gameObject, new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42, 42)).localRotation = Quaternion.Euler(0, 0, 45);
            var door = UIKit.Panel(bg.transform, UIKit.Wood, "Door", UIKit.RoundedSmall);
            UIKit.Place(door.gameObject, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 20));
            return bg.gameObject;
        }

        // ================================================================ settings helpers

        static Font _systemFont;

        /// <summary>
        /// Black "Sign in with Apple" button per Apple's guidelines: mixed case and the Apple logo, drawn in the
        /// iOS system font (which carries the logo glyph); elsewhere the game font without the logo.
        /// </summary>
        void AppleSignInButton(Transform parent, Action onClick)
        {
            var b = UIKit.Btn(parent, "Sign in with Apple", Color.black, onClick, 26, -1, 72);
            var t = b.GetComponentInChildren<Text>();
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                if (_systemFont == null) _systemFont = Font.CreateDynamicFontFromOSFont(new[] { "Helvetica Neue", "Helvetica" }, 30);
                t.font = _systemFont;
                t.text = "\uF8FF  Sign in with Apple";
            }
            else t.text = "Sign in with Apple";
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
        }

        void ConfirmReset()
        {
            ShowBoard("Are you sure?", (body, right) =>
            {
                var panel = Box(body, BCardDeep, -1, -1, "Panel");
                UIKit.Stretch(panel.gameObject);
                Stack(panel.gameObject, 18, new RectOffset(30, 30, 30, 30), TextAnchor.MiddleCenter);
                T(panel.transform, "Erase this railroad?", 56, BBad, TextAnchor.MiddleCenter, true);
                T(panel.transform, "All progress, coins and nuggets are lost and the game starts over. One-time purchases such as the Big Tender come back with Restore purchases. This cannot be undone.", 34, Color.white, TextAnchor.MiddleCenter, false, UIKit.Heading);
                Flex(panel.transform);
                var row = HBox(panel.transform, 24, 0, TextAnchor.MiddleCenter, -1, 100);
                Big(row.transform, "Keep playing", UIKit.Green, () => CloseWindow(), 380, 96, true, 40);
                Big(row.transform, "Erase everything", UIKit.Red, () => { CloseWindow(); GameRoot.Instance.ResetGame(); }, 380, 96, true, 40);
            }, 1300, 640);
        }
    }
}
