using System;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>
    /// Trips left before an engine or a truck is worn out, on its card: a bar, and the button that fits (Repair with a wrench in hand;
    /// with none, a wrench for a watched advert or for nuggets). Nothing is shown before wear begins.
    /// </summary>
    public sealed partial class UIRoot
    {
        void ConditionRow(Transform p, float wear, int level, Func<ActionResult> repair, float buttonW = 230, int px = 30)
        {
            if (!S.WearOn) return;
            int left = S.TripsLeft(wear, level), fresh = GameSession.TripsWhenFresh(level);
            bool out_ = left <= 0;
            var row = HBox(p, 10, 0, TextAnchor.MiddleLeft, -1, out_ ? 88 : 70);
            var col = VBox(row.transform, 4, 0, TextAnchor.MiddleLeft, -1, -1, 1);
            T(col.transform, out_ ? "Worn out" : "Trips left " + left + " / " + fresh, 26, out_ ? BBad : BLabel, TextAnchor.MiddleLeft, true, UIKit.Heading);
            var bar = Track(col.transform, 14, out_ ? BBad : left <= 2 ? BGold : BGood);
            UIKit.SetBar(bar, Mathf.Max(0.04f, 1 - wear));
            if (out_) T(col.transform, "Rested in " + Fmt.Time(S.RestNeeded(wear, level)), 22, BBad, TextAnchor.MiddleLeft, true, UIKit.Heading);
            if (S.Wrenches > 0 || wear < 0.02f)
                Big(row.transform, wear < 0.02f ? "Fine" : "Repair (" + S.Wrenches + ")", UIKit.Green, () => Result(repair(), Sfx.Build), buttonW, 66, wear >= 0.02f, px);
            else if (S.AdsLeft(AdReward.Wrench) > 0 && Ads.Current.Available)
                Big(row.transform, "Wrench: Ad", UIKit.Green, () => Ads.Watch(AdReward.Wrench, 0, r => Result(r, Sfx.Coin)), buttonW, 66, true, px);
            else
                PriceBtn(row.transform, "Wrench", Premium, tr => UIKit.NuggetIcon(tr, 36), Balance.WrenchNuggets.ToString(), () => Result(S.BuyWrench(), Sfx.Coin), buttonW, 66, S.Nuggets >= Balance.WrenchNuggets, px);
        }
    }
}
