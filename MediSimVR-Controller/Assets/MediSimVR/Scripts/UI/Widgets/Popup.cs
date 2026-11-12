using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MediSimVR
{
    // Simple dialog: title, message, optional text input, one or two buttons.
    public class Popup : MonoBehaviour
    {
        public GameObject root;
        public TMP_Text title, message, okLabel, cancelLabel;
        public TMP_InputField input;
        public Button okButton, cancelButton, dimButton;
        public ThemedColor okColor;       // OK background: Primary (gradient) or Danger (destructive)
        public UIGradient okGradient;

        Action onOk;
        Action<string> onText;

        void Awake()
        {
            okButton.onClick.AddListener(() =>
            {
                string v = input != null ? input.text : "";
                Hide();
                if (onText != null) onText(v);
                if (onOk != null) onOk();
            });
            cancelButton.onClick.AddListener(Hide);
            if (dimButton != null) dimButton.onClick.AddListener(Hide);
            root.SetActive(false);
        }

        // destructive = the OK button is solid red (End session, Leave, Switch role); otherwise the primary gradient
        public void Show(string t, string msg, string ok = "OK", Action okAct = null, string cancel = null,
            string inputHint = null, Action<string> textAct = null, bool destructive = false)
        {
            if (okColor != null) { okColor.role = destructive ? ColorRole.Danger : ColorRole.Primary; okColor.Apply(); }
            if (okGradient != null) okGradient.enabled = !destructive;
            title.text = t;
            message.text = msg;
            okLabel.text = ok;
            onOk = okAct;
            onText = textAct;
            cancelButton.gameObject.SetActive(cancel != null);
            if (cancel != null) cancelLabel.text = cancel;
            input.gameObject.SetActive(inputHint != null);
            if (inputHint != null)
            {
                input.text = "";
                ((TMP_Text)input.placeholder).text = inputHint;
            }
            root.SetActive(true);
        }

        public void Hide() { root.SetActive(false); }
    }
}
