using UnityEngine;

namespace RewindNotary
{
    public sealed partial class RepresentativeArea
    {
        private const string ForgeRoomId = "hub_forge";
        private SpriteRenderer ethan;
        private bool CanTalkToEthan => Room.id == ForgeRoomId && Has("intro_complete") && session.AcceptsSupplies
            && ethan != null && Mathf.Abs(player.Body.position.x - ethan.transform.position.x) <= 1.85f
            && Mathf.Abs(player.Body.position.y - ethan.transform.position.y) <= .55f;

        private void BuildForgeMerchant()
        {
            ethan = null;
            if (Room.id != ForgeRoomId) return;
            var mark = Room.Point("ethan_grindstone");
            ethan = Place("Ethan", mark.x - 1.55f, Room.SurfaceTop(mark.surface)).AddComponent<SpriteRenderer>();
            ethan.sortingOrder = 5;
            ethan.transform.localScale = Vector3.one * 2.1f;
            ethan.sprite = ForgeSupplyArt.Ethan(false);
        }

        private void UpdateForgeMerchant()
        {
            if (ethan == null) return;
            ethan.sprite = ForgeSupplyArt.Ethan(shell.ForgeInteractionOpen);
            ethan.flipX = player.Body.position.x > ethan.transform.position.x;
        }

        private bool TryTalkToEthan()
        {
            if (!CanTalkToEthan) return false;
            if (!InteractAt(ethan)) return true;
            shell.OpenForgeDialogue();
            return true;
        }

        private string EthanPrompt()
        {
            string interact = GameInput.UsingGamepad ? "[L3]" : GameInput.KeyLabel(GameInput.InteractKey);
            return Loc.Get("shop.talk_prompt", interact);
        }
    }
}
