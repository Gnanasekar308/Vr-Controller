using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MediSimVR
{
    // HSV colour picker + HEX field.
    public class ColorPickerPopup : MonoBehaviour
    {
        public GameObject root;
        public TMP_Text title;
        public Image preview;
        public Slider hue, saturation, value;
        public Image satTrack, valTrack;
        public TMP_InputField hex;
        public Button okButton, cancelButton;

        Action<Color> done;
        bool busy;

        void Awake()
        {
            hue.onValueChanged.AddListener(v => FromSliders());
            saturation.onValueChanged.AddListener(v => FromSliders());
            value.onValueChanged.AddListener(v => FromSliders());
            hex.onEndEdit.AddListener(FromHex);
            okButton.onClick.AddListener(() => { root.SetActive(false); if (done != null) done(preview.color); });
            cancelButton.onClick.AddListener(() => root.SetActive(false));
            root.SetActive(false);
        }

        public void Open(string name, Color c, Action<Color> onDone)
        {
            title.text = name;
            done = onDone;
            SetColor(c);
            root.SetActive(true);
        }

        void SetColor(Color c)
        {
            busy = true;
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            hue.value = h;
            saturation.value = s;
            value.value = v;
            busy = false;
            Show(c);
        }

        void FromSliders()
        {
            if (busy) return;
            Show(Color.HSVToRGB(hue.value, saturation.value, value.value));
        }

        void FromHex(string s)
        {
            Color c;
            if (!s.StartsWith("#")) s = "#" + s;
            if (ColorUtility.TryParseHtmlString(s, out c)) SetColor(c);
        }

        void Show(Color c)
        {
            c.a = 1f;
            preview.color = c;
            if (satTrack != null) satTrack.color = Color.HSVToRGB(hue.value, 1f, 1f);
            if (valTrack != null) valTrack.color = Color.HSVToRGB(hue.value, saturation.value, 1f);
            hex.SetTextWithoutNotify("#" + ColorUtility.ToHtmlStringRGB(c));
        }
    }
}
