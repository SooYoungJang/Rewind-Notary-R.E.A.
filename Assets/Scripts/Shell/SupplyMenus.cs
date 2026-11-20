using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class GameShell
    {
        internal bool ForgeInteractionOpen => Page == ShellPage.ForgeDialogue || Page == ShellPage.ForgeShop;
        private bool InSupplyMenu => Page == ShellPage.ForgeShop || Page == ShellPage.SupplyBag;
        private bool CanManageSupplies => game.ActiveSkills?.AcceptsSupplies == true && game.ActivePlayerHealth != null;
        private SupplyPouch Supplies => game.ActiveSkills.Progression.Record.supplies;

        internal void OpenForgeDialogue()
        {
            if (!Playing || !CanManageSupplies) return;
            message = null;
            game.SuspendInput();
            ImpactFeedback.ResetEffects();
            Time.timeScale = 0f;
            Go(ShellPage.ForgeDialogue);
        }

        private void ActivateSupplyRow(int index)
        {
            if (index == SupplyProduct.Stock.Length) { Back(); return; }
            var session = game.ActiveSkills;
            if (!session.LastSaveSucceeded) { message = session.StorageError; return; }
            var product = SupplyProduct.Stock[index];
            if (Page == ShellPage.ForgeShop) BuySupply(product);
            else UseSupply(product);
        }

        private void BuySupply(SupplyProduct product)
        {
            var purchase = Supplies.Purchased(product);
            if (purchase == null) { message = Loc.Get("shop.insufficient"); return; }
            var session = game.ActiveSkills;
            message = session.TrySaveSupplies(purchase) ? Loc.Get("shop.purchased", product.Name) : session.StorageError;
        }

        private void UseSupply(SupplyProduct product)
        {
            var remaining = Supplies.Consumed(product.Item);
            if (remaining == null) { message = Loc.Get("shop.none_held", product.Name); return; }
            var health = game.ActivePlayerHealth;
            if (health.Dead) { message = Loc.Get("shop.use_unavailable"); return; }
            if (health.Current >= health.Maximum) { message = Loc.Get("shop.full_health"); return; }
            var session = game.ActiveSkills;
            if (!session.TrySaveSupplies(remaining)) { message = session.StorageError; return; }
            int before = health.Current;
            health.Heal(product.Recovery);
            message = Loc.Get("shop.used", product.Name, health.Current - before);
        }

        private string[] PauseLabels()
        {
            var labels = new List<string>
            {
                Loc.Get("ui.resume"), Loc.Get("ui.skills"), Loc.Get("ui.journal") + " [N]", Loc.Get("ui.settings")
            };
            if (CanManageSupplies) labels.Add(Loc.Get("shop.bag_title"));
            labels.Add(Loc.Get("ui.main_menu"));
            labels.Add(Loc.Get("ui.quit"));
            return labels.ToArray();
        }

        private GameShellCanvas.DialogueView ForgeDialogueSnapshot() => new GameShellCanvas.DialogueView
        {
            RoomLabel = Loc.Get("area.room.hub_forge"),
            SpeakerId = "ethan",
            SpeakerName = Loc.Get("area.speaker.ethan"),
            BodyText = Loc.Get(game.ActiveSkills.Progression.Record.storyFlags.Contains("velvet_entry_wave_cleared")
                ? "shop.ethan.return" : "shop.ethan.greeting"),
            SelectedIndex = selected,
            ContinueLabel = Loc.Get("shop.browse"),
            SecondaryLabel = Loc.Get("shop.end_talk")
        };

        private void PopulateSupplies(ref GameShellCanvas.ShellSnapshot snap)
        {
            bool shopping = Page == ShellPage.ForgeShop;
            var health = game.ActivePlayerHealth;
            snap.BodyHeader = shopping
                ? Loc.Get("shop.balance", Supplies.silver)
                : Loc.Get("shop.bag_status", health.Current, health.Maximum, Supplies.silver);
            snap.FooterText = Loc.Get(shopping ? "shop.buy_keys" : "shop.use_keys");
            for (int i = 0; i < SupplyProduct.Stock.Length; i++)
            {
                var item = SupplyProduct.Stock[i];
                snap.Buttons.Add(new GameShellCanvas.ButtonItem
                {
                    Index = i,
                    Label = item.Name,
                    Selected = selected == i,
                    Icon = ForgeSupplyArt.Icon(item.Item),
                    Detail = Loc.Get("shop.item_detail", item.Recovery, Supplies.Count(item.Item)),
                    Price = shopping ? Loc.Get("shop.price", item.Price) : null
                });
            }
            int back = SupplyProduct.Stock.Length;
            snap.Buttons.Add(new GameShellCanvas.ButtonItem { Index = back, Label = Loc.Get("ui.back"), Selected = selected == back });
        }
    }
}
