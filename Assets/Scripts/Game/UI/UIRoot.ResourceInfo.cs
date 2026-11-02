using System.Linq;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>
    /// "How to get" card: tap any resource icon (costs, contract orders, rewards, the warehouse) to see how
    /// much you have and where it comes from: the route that hauls it, the factory that makes it (with its
    /// inputs), or the contracts/projects that award it. Each source has a button that takes you there.
    /// </summary>
    public sealed partial class UIRoot
    {
        GameObject _resInfo;

        public void ShowResourceInfo(string resId)
        {
            var res = GameDatabase.Resource(resId);
            if (res == null) return;
            CloseResourceInfo();
            AudioManager.Play(Sfx.Open);

            var dim = UIKit.Panel(_popupLayer, UIKit.Dim, "ResourceInfo");
            dim.sprite = null;
            UIKit.Stretch(dim.gameObject);
            _resInfo = dim.gameObject;
            Clickable(_resInfo, CloseResourceInfo);
            _resInfo.GetComponent<ButtonFx>().enabled = false;

            var card = UIKit.Panel(dim.transform, UIKit.Parchment, "Card", UIKit.Rounded, true);
            UIKit.Place(card.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 0));
            card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // swallow taps
            card.gameObject.AddComponent<TapSwallow>();
            card.gameObject.AddComponent<WindowIntro>();
            var edge = card.gameObject.AddComponent<Outline>();
            edge.effectColor = UIKit.Brass;
            edge.effectDistance = new Vector2(3, -3);
            UIKit.VStack(card.gameObject, 14, 26, TextAnchor.UpperCenter, true);
            card.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Header: warehouse fill, close button
            var head = UIKit.Row(card.transform, 70, 16, TextAnchor.MiddleLeft);
            UIKit.Icon(head.transform, "crate", 60);
            var barHolder = UIKit.Node("Fill", head.transform);
            UIKit.Size(barHolder, -1, 44, 1);
            var bar = UIKit.Bar(barHolder.transform, UIKit.Green, 44, new Color(0.2f, 0.14f, 0.09f, 0.85f));
            UIKit.Stretch(bar.transform.parent.gameObject);
            UIKit.SetBar(bar, S.WarehouseUsed / (float)Mathf.Max(1, S.WarehouseCapacity));
            var used = UIKit.Label(bar.transform.parent, S.WarehouseUsed + "/" + S.WarehouseCapacity, 30, Color.white, TextAnchor.MiddleCenter);
            used.font = UIKit.Display;
            UIKit.Stretch(used.gameObject);
            used.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.5f);
            UIKit.Btn(head.transform, "X", Palette.Hex(0x2A1C12), CloseResourceInfo, 34, 76, 70);

            // Item row
            var item = UIKit.Panel(card.transform, UIKit.ParchmentDark, "Item", UIKit.Rounded);
            UIKit.Size(item.gameObject, -1, 130);
            UIKit.HStack(item.gameObject, 22, 18, TextAnchor.MiddleLeft);
            UIKit.ResIcon(item.transform, res.Id, 96);
            var col = UIKit.Node("Col", item.transform);
            UIKit.VStack(col, 2, 0, TextAnchor.MiddleLeft);
            UIKit.Size(col, -1, -1, 1);
            var stored = UIKit.Label(col.transform, res.IsPart ? "In the workshop" : "In storage", 24, UIKit.InkSoft, TextAnchor.MiddleLeft);
            UIKit.Size(stored.gameObject, -1, 32);
            var amount = UIKit.Label(col.transform, res.Name + ": " + Fmt.Num(S.Amount(res.Id)), 48, UIKit.Ink, TextAnchor.MiddleLeft);
            amount.font = UIKit.Display;
            UIKit.Size(amount.gameObject, -1, 58);

            UIDeco.Divider(card.transform, "How to get", UIKit.InkSoft);
            var how = UIKit.Panel(card.transform, UIKit.ParchmentDark, "How", UIKit.Rounded);
            UIKit.VStack(how.gameObject, 12, 20, TextAnchor.UpperCenter, true);
            how.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            int sources = 0;

            // Routes that haul it
            foreach (var d in GameDatabase.Destinations.Where(d => d.ResourceId == res.Id))
            {
                bool open = S.IsDestinationUnlocked(d);
                var dest = d;
                Source(how.transform, "OBTAIN FROM LOCATION", d.Name, open ? "Haul it by train" : "Unlocks at level " + d.UnlockLevel,
                    open ? "Send a train" : null, () => { CloseResourceInfo(); SendTrainTo(dest); });
                sources++;
            }

            // Factories that make it
            foreach (var b in GameDatabase.Buildings.Where(b => b.Recipe != null && b.Recipe.Output.id == res.Id))
            {
                var bd = b;
                var inputs = string.Join(" + ", b.Recipe.Inputs.Select(i => i.amount + " " + GameDatabase.Resource(i.id).Name));
                var owned = S.State.buildings.FirstOrDefault(x => x.defId == b.Id);
                bool open = S.IsBuildingUnlocked(b);
                Source(how.transform, "PRODUCED AT", b.Name, "From " + inputs + (open ? "" : " · unlocks at level " + b.UnlockLevel),
                    !open ? null : owned != null ? "Open" : "Build one",
                    () =>
                    {
                        CloseResourceInfo();
                        if (owned != null) OpenBuilding(owned.uid);
                        else { _buildTab = bd.Category; OpenBuildMenu(); }
                    },
                    inputs: b.Recipe.Inputs.Select(i => i.id).ToArray());
                sources++;
            }

            if (res.IsPart)
            {
                Source(how.transform, "EARNED FROM", "Contracts & Projects", "Workshop parts come as contract rewards and from territory projects",
                    "Contract board", () => { CloseResourceInfo(); OpenContracts(); });
                sources++;
            }
            if (sources == 0)
            {
                var l = UIKit.Label(how.transform, "Earned from contracts and the Foreman's tasks.", 26, UIKit.Ink, TextAnchor.MiddleCenter);
                UIKit.Size(l.gameObject, -1, 60);
            }

            // Fit the card to the screen height.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)card.transform);
        }

        void Source(Transform parent, string kind, string place, string detail, string action, System.Action onClick, string[] inputs = null)
        {
            var row = UIKit.Node("Source", parent);
            UIKit.VStack(row, 6, 0, TextAnchor.MiddleCenter);
            UIKit.Size(row, -1, -1);
            var title = UIKit.Label(row.transform, "<color=#C27A18>" + kind + "</color>  " + place.ToUpperInvariant(), 38, UIKit.Ink, TextAnchor.MiddleCenter);
            title.font = UIKit.Display;
            UIKit.Size(title.gameObject, -1, 50);
            if (inputs != null && inputs.Length > 0)
            {
                var icons = UIKit.Row(row.transform, 60, 10, TextAnchor.MiddleCenter);
                foreach (var id in inputs) UIKit.ResIcon(icons.transform, id, 50);
            }
            var d = UIKit.Label(row.transform, detail, 24, UIKit.InkSoft, TextAnchor.MiddleCenter);
            UIKit.Size(d.gameObject, -1, 34);
            if (action != null)
            {
                var r = UIKit.Row(row.transform, 76, 0, TextAnchor.MiddleCenter);
                UIKit.Btn(r.transform, action, UIKit.Blue, onClick, 28, 320, 70);
            }
        }

        /// <summary>Opens dispatch for the first idle train (the destination list is right there), else the fleet.</summary>
        void SendTrainTo(DestinationDef d)
        {
            var idle = S.State.trains.FirstOrDefault(t => t.IsIdle && !S.HasArrived(t));
            if (idle != null) OpenTrain(idle.uid);
            else OpenFleet();
            Toast("Send a train to " + d.Name, true);
        }

        public void CloseResourceInfo()
        {
            if (_resInfo != null) Destroy(_resInfo);
            _resInfo = null;
        }
    }

    /// <summary>Marks a Button that only exists to stop taps falling through a panel.</summary>
    public sealed class TapSwallow : MonoBehaviour { }

    /// <summary>Makes a resource icon tappable for its "How to get" card (skipped inside buttons).</summary>
    public sealed class ResourceInfoTap : MonoBehaviour, IPointerClickHandler
    {
        public string ResourceId;

        void Start()
        {
            // Inside a real button (a cost on a Buy button, a world marker) the button's own tap must win.
            // Windows and cards wrap content in tap-swallowing buttons marked TapSwallow; those don't count.
            var host = transform.parent != null ? transform.parent.GetComponentInParent<Button>() : null;
            if (host != null && host.GetComponent<TapSwallow>() == null) { enabled = false; return; }
            foreach (var g in GetComponentsInChildren<Graphic>()) g.raycastTarget = g.gameObject == gameObject;
            var own = GetComponent<Graphic>();
            if (own != null) own.raycastTarget = true;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!enabled) return;
            AudioManager.Play(Sfx.Click);
            UIRoot.Instance.ShowResourceInfo(ResourceId);
        }
    }
}
