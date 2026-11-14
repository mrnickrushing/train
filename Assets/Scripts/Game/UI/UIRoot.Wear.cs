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
            // One compact block: the rating and what a trip takes, the bar, then the two buttons side by side.
            var box = VBox(p, 5, 0, TextAnchor.UpperLeft, -1, 110);
            var head = HBox(box.transform, 8, 0, TextAnchor.MiddleLeft, -1, 28);
            UIKit.Size(T(head.transform, "Durability " + rating + " / 100", 26, out_ ? BBad : BLabel, TextAnchor.MiddleLeft, true, UIKit.Heading).gameObject, -1, -1, 1);
            T(head.transform, out_ ? "rested in " + Fmt.Time(S.RestNeeded(wear, durability)) : "a trip takes " + cost, 22, out_ ? BBad : BLabel, TextAnchor.MiddleRight, true, UIKit.Heading);
            var bar = Track(box.transform, 12, out_ ? BBad : rating < cost * 2 ? BGold : BGood);
            UIKit.SetBar(bar, Mathf.Max(0.04f, 1 - wear));
            var row = HBox(box.transform, 8, 0, TextAnchor.MiddleLeft, -1, 58);
            bool tough = durability < Balance.MaxDurability;
            float w = tough ? -1 : buttonW;
            Button fix;
            if (S.Wrenches > 0 || wear < 0.02f)
                fix = Big(row.transform, wear < 0.02f ? "Fine" : "Repair (" + S.Wrenches + ")", UIKit.Green, () => Result(repair(), Sfx.Build), w, 58, wear >= 0.02f, px - 4);
            else if (S.AdsLeft(AdReward.Wrench) > 0 && Ads.Current.Available)
                fix = Big(row.transform, "Wrench: Ad", UIKit.Green, () => Ads.Watch(AdReward.Wrench, 0, r => Result(r, Sfx.Coin)), w, 58, true, px - 4);
            else
                fix = PriceBtn(row.transform, "Wrench", Premium, tr => UIKit.NuggetIcon(tr, 32), Balance.WrenchNuggets.ToString(), () => Result(S.BuyWrench(), Sfx.Coin), w, 58, S.Nuggets >= Balance.WrenchNuggets, px - 4);
            // Durability is its own upgrade: a trip takes less of the hundred, for coins.
            if (tough)
            {
                UIKit.Size(fix.gameObject, -1, 58, 1);
                int coins = S.DurabilityCost(durability);
                var up = PriceBtn(row.transform, "Tougher", UIKit.Blue, tr => UIKit.CoinIcon(tr, 32), Fmt.Num(coins), () => Result(toughen(), Sfx.Build), -1, 58, S.Coins >= coins, px - 4);
                UIKit.Size(up.gameObject, -1, 58, 1);
            }
        }
    }
}
