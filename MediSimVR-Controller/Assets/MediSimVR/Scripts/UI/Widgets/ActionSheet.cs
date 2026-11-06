using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Bottom sheet with up to 4 choices + Cancel (file type, text source, file actions).
    public class ActionSheet : MonoBehaviour
    {
        public GameObject root;
        public TMP_Text title;
        public List<Button> options = new List<Button>();
        public List<TMP_Text> labels = new List<TMP_Text>();
        public List<Image> icons = new List<Image>();
        public Button cancelButton, dimButton;
        public List<Sprite> iconSprites = new List<Sprite>();   // looked up by name

        Action<int> picked;

        void Awake()
        {
            for (int i = 0; i < options.Count; i++)
            {
                int k = i;
                options[i].onClick.AddListener(() => { Hide(); if (picked != null) picked(k); });
            }
            cancelButton.onClick.AddListener(Hide);
            dimButton.onClick.AddListener(Hide);
            root.SetActive(false);
        }

        public void Show(string t, string[] texts, string[] iconNames, Action<int> onPick)
        {
            title.text = t;
            picked = onPick;
            for (int i = 0; i < options.Count; i++)
            {
                bool on = i < texts.Length;
                options[i].gameObject.SetActive(on);
                if (!on) continue;
                labels[i].text = texts[i];
                Sprite sp = null;
                if (iconNames != null && i < iconNames.Length)
                    foreach (var s in iconSprites) if (s != null && s.name == iconNames[i]) sp = s;
                icons[i].sprite = sp;
                icons[i].gameObject.SetActive(sp != null);
            }
            root.SetActive(true);
        }

        public void Hide() { root.SetActive(false); }
    }
}
