using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MediSimVR
{
    // The Quest list of the Devices tab, reused by the Session Quest list: one DeviceRowView per Quest (rows are kept
    // between refreshes), an empty text, and the shared "tap = select / unselect, offline cannot be selected" rule.
    public class DeviceListView : MonoBehaviour
    {
        public DeviceRowView rowTemplate;
        public TMP_Text emptyText;

        private readonly List<DeviceRowView> rows = new List<DeviceRowView>();

        public IReadOnlyList<DeviceRowView> Rows { get { return rows; } }

        public void Show(IReadOnlyList<QuestDevice> devices, IControllerService svc, Action<QuestDevice> onTap, string empty)
        {
            rowTemplate.gameObject.SetActive(false);
            AppScreen.Keep(rowTemplate, rows, devices.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var d = devices[i];
                rows[i].Bind(d, DeviceStatus.Content(d, svc), () => { if (onTap != null) onTap(d); });
            }
            if (emptyText != null)
            {
                emptyText.text = empty;
                emptyText.gameObject.SetActive(devices.Count == 0);
            }
        }

        // tap on a Quest: select / unselect; an offline Quest cannot be selected. true = the selection changed.
        public static bool ToggleSelect(QuestDevice d, Navigator nav)
        {
            if (!d.Online)
            {
                if (nav != null) nav.Say(d.Name + " is offline: it cannot be selected");
                return false;
            }
            d.Selected = !d.Selected;
            return true;
        }
    }
}
