using System;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>
    /// Condition of an engine or a truck on its card: a bar, and the button that fits (Repair with a wrench in hand;
    /// with none, a wrench for a watched advert or for nuggets). Nothing is shown before wear begins.
    /// </summary>
    public sealed partial class UIRoot
    {
        void ConditionRow(Transform p, float wear, Func<ActionResult> repair, float buttonW = 230, int px = 30)
        {
            if (!S.WearOn) return;
            int pct = GameSession.Condition(wear);
            bool worn = GameSession.Worn(wear);
            var row = HBox(p, 10, 0, TextAnchor.MiddleLeft, -1, worn ? 88 : 70);
            var col = VBox(row.transform, 4, 0, TextAnchor.MiddleLeft, -1, -1, 1);
            int slower = Mathf.RoundToInt((float)(GameSession.WearSlow(wear) - 1) * 100);
            T(col.transform, "Condition " + pct + "%", 26, worn ? BBad : BLabel, TextAnchor.MiddleLeft, true, UIKit.Heading);
            var bar = Track(col.transform, 14, worn ? BBad : pct < 75 ? BGold : BGood);
            UIKit.SetBar(bar, Mathf.Max(0.04f, 1 - wear));
            if (worn) T(col.transform, "Worn: " + slower + "% slower", 22, BBad, TextAnchor.MiddleLeft, true, UIKit.Heading);
            if (S.Wrenches > 0 || wear < 0.02f)
                Big(row.transform, wear < 0.02f ? "Fine" : "Repair (" + S.Wrenches + ")", UIKit.Green, () => Result(repair(), Sfx.Build), buttonW, 66, wear >= 0.02f, px);
            else if (S.AdsLeft(AdReward.Wrench) > 0 && Ads.Current.Available)
                Big(row.transform, "Wrench: Ad", UIKit.Green, () => Ads.Watch(AdReward.Wrench, 0, r => Result(r, Sfx.Coin)), buttonW, 66, true, px);
            else
                PriceBtn(row.transform, "Wrench", Premium, tr => UIKit.NuggetIcon(tr, 36), Balance.WrenchNuggets.ToString(), () => Result(S.BuyWrench(), Sfx.Coin), buttonW, 66, S.Nuggets >= Balance.WrenchNuggets, px);
        }
    }
}
