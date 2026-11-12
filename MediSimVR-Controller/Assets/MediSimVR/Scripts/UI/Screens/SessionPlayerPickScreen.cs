using TMPro;
using UnityEngine.UI;

namespace MediSimVR
{
    // PLAYER: "Change" on the Play page: pick My Quest from the Devices list (ticked = current; offline cannot be picked),
    // then back to the Play page.
    public class SessionPlayerPickScreen : AppScreen
    {
        public Button backButton;
        public DeviceListView list;
        public TMP_Text hintText;

        public override void Init(Navigator n)
        {
            base.Init(n);
            backButton.onClick.AddListener(() => nav.Back());
        }

        public override void Refresh()
        {
            list.Show(Svc.Devices, Svc, Pick, "No Quests found");
            hintText.text = "Tap your online Quest to play on it";
        }

        private void Pick(QuestDevice d)
        {
            if (!d.Online) { nav.Say(d.Name + " is offline: it cannot be picked"); return; }
            ControlFlow.UsePlayerQuest(Svc, d);
            nav.Say("My Quest: " + d.Name);
            nav.Back();
        }
    }
}
