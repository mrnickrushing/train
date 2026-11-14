using System;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>
    /// The durability rating of an engine or a truck on its card: a bar out of 100, and the button that fits (Repair with a wrench in hand;
    /// with none, a wrench for a watched advert or for nuggets). Nothing is shown before wear begins.
    /// </summary>
    public sealed partial class UIRoot
    {
        void ConditionRow(Transform p, float wear, int durability, Func<ActionResult> repair, Func<ActionResult> toughen, float buttonW = 230, int px = 30)
        {
            if (!S.WearOn) return;
            int rating = GameSession.Rating(wear), cost = GameSession.TripCost(durability);
            bool out_ = S.TripsLeft(wear, durability) <= 0;
            var row = HBox(p, 10, 0, TextAnchor.MiddleLeft, -1, 88);
            var col = VBox(row.transform, 4, 0, TextAnchor.MiddleLeft, -1, -1, 1);
            T(col.transform, "Durability " + rating + " / 100", 26, out_ ? BBad : BLabel, TextAnchor.MiddleLeft, true, UIKit.Heading);
            var bar = Track(col.transform, 14, out_ ? BBad : rating < cost * 2 ? BGold : BGood);
            UIKit.SetBar(bar, Mathf.Max(0.04f, 1 - wear));
            T(col.transform, out_ ? "Needs " + cost + ": rested in " + Fmt.Time(S.RestNeeded(wear, durability)) : "A trip takes " + cost, 22, out_ ? BBad : BLabel, TextAnchor.MiddleLeft, true, UIKit.Heading);
            if (S.Wrenches > 0 || wear < 0.02f)
                Big(row.transform, wear < 0.02f ? "Fine" : "Repair (" + S.Wrenches + ")", UIKit.Green, () => Result(repair(), Sfx.Build), buttonW, 66, wear >= 0.02f, px);
            else if (S.AdsLeft(AdReward.Wrench) > 0 && Ads.Current.Available)
                Big(row.transform, "Wrench: Ad", UIKit.Green, () => Ads.Watch(AdReward.Wrench, 0, r => Result(r, Sfx.Coin)), buttonW, 66, true, px);
            else
                PriceBtn(row.transform, "Wrench", Premium, tr => UIKit.NuggetIcon(tr, 36), Balance.WrenchNuggets.ToString(), () => Result(S.BuyWrench(), Sfx.Coin), buttonW, 66, S.Nuggets >= Balance.WrenchNuggets, px);
            // Durability is its own upgrade: one more trip before it is worn out, for coins.
            if (durability < Balance.MaxDurability)
            {
                int coins = S.DurabilityCost(durability);
                PriceBtn(p, "Tougher: " + GameSession.TripCost(durability + 1) + " a trip", UIKit.Green, tr => UIKit.CoinIcon(tr, 36), Fmt.Num(coins), () => Result(toughen(), Sfx.Build), -1, 62, S.Coins >= coins, px - 4);
            }
        }
    }
}
