using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

namespace MediSimVR
{
    // PLAYER: pick the one Quest this phone works with (offline Quests cannot be picked).
    public class SessionPlayerPickScreen : AppScreen
    {
        public Button backButton;
        public QuestCardView cardTemplate;
        public TMP_Text hintText;

        private readonly List<QuestCardView> cache = new List<QuestCardView>();

        public override void Init(Navigator n)
        {
            base.Init(n);
            backButton.onClick.AddListener(() => nav.Back());
            cardTemplate.gameObject.SetActive(false);
        }

        public override void Refresh()
        {
            var cards = Keep(cardTemplate, cache, Svc.Devices.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                var d = Svc.Devices[i];
                cards[i].Bind(d, QuestCardView.Content(d, Svc), false, false, () => Pick(d));
                cards[i].button.interactable = d.Online; // offline Quests are shown (dimmed) but cannot be picked
            }
            hintText.text = "Tap your online Quest to join as a player";
        }

        private void Pick(QuestDevice d)
        {
            if (!d.Online) { nav.Say(d.Name + " is offline"); return; }
            if (AppRoot.Session == null) { nav.Say("Session backend not connected yet"); return; }
            foreach (var x in Svc.Devices) x.Selected = x == d; // the controls on the player screen act on this Quest only
            AppRoot.Session.StartPlayer(d.Id);
            nav.Push(ScreenId.SessionPlayer);
        }
    }
}
