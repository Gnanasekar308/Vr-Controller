using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Write a new text or edit a .txt file (title + content).
    public class TextEditorPopup : MonoBehaviour
    {
        public GameObject root;
        public TMP_Text header;
        public TMP_InputField titleInput, contentInput;
        public Button saveButton, cancelButton;

        Action<string, string> saved;

        void Awake()
        {
            saveButton.onClick.AddListener(() =>
            {
                string t = titleInput.text.Trim();
                if (t.Length == 0) t = "Note_" + DateTime.Now.ToString("HHmm");
                root.SetActive(false);
                if (saved != null) saved(t, contentInput.text);
            });
            cancelButton.onClick.AddListener(() => root.SetActive(false));
            root.SetActive(false);
        }

        public void Open(string headerText, string title, string content, Action<string, string> onSave)
        {
            header.text = headerText;
            titleInput.text = title ?? "";
            contentInput.text = content ?? "";
            saved = onSave;
            root.SetActive(true);
        }
    }
}
