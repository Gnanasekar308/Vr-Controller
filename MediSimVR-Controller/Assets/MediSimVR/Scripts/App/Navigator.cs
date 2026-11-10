using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    public enum ScreenId { Home, Devices, Send, Control, Settings, Notifications, EditApp, Scene } // new ids go at the end

    [Serializable]
    public class NavItem
    {
        public ScreenId target;
        public Button button;
        public Image icon;
        public TMP_Text label;
        public Image highlight;   // soft pill behind the selected tab
    }

    // Shows one screen at a time + persistent bottom navigation + back stack.
    public class Navigator : MonoBehaviour
    {
        public List<AppScreen> screens = new List<AppScreen>();
        public List<NavItem> navItems = new List<NavItem>();
        public Popup popup;
        public Toast toast;
        public ColorPickerPopup colorPicker;
        public ActionSheet actionSheet;
        public TextEditorPopup textEditor;

        readonly Stack<ScreenId> back = new Stack<ScreenId>();
        ScreenId current = ScreenId.Home;

        public ScreenId Current { get { return current; } }

        public void Init()
        {
            foreach (var n in navItems)
            {
                var item = n;
                item.button.onClick.AddListener(() => Tab(item.target));
            }
            foreach (var s in screens)
            {
                s.Init(this);
                s.gameObject.SetActive(false); // Show() re-enables one, so screens subscribe to data events
            }
            ThemeManager.Changed += Highlight;
            Show(ScreenId.Home);
        }

        void OnDestroy() { ThemeManager.Changed -= Highlight; }

        public void Tab(ScreenId id)
        {
            back.Clear();
            Show(id);
        }

        public void Push(ScreenId id)
        {
            back.Push(current);
            Show(id);
        }

        public void Back()
        {
            if (textEditor != null && textEditor.root.activeSelf) { textEditor.root.SetActive(false); return; }
            if (actionSheet != null && actionSheet.root.activeSelf) { actionSheet.Hide(); return; }
            if (popup != null && popup.root.activeSelf) { popup.Hide(); return; }
            if (colorPicker != null && colorPicker.root.activeSelf) { colorPicker.root.SetActive(false); return; }
            if (back.Count > 0) { Show(back.Pop()); return; }
            if (current != ScreenId.Home) { Show(ScreenId.Home); return; }
            Application.Quit();
        }

        void Show(ScreenId id)
        {
            current = id;
            foreach (var s in screens)
            {
                bool on = s.id == id;
                if (s.gameObject.activeSelf != on) s.gameObject.SetActive(on);
                if (on) s.OnShow();
            }
            Highlight();
        }

        void Highlight()
        {
            ScreenId tab = current == ScreenId.Notifications ? ScreenId.Home : current == ScreenId.EditApp ? ScreenId.Settings : current;
            foreach (var n in navItems)
            {
                Color c = n.target == tab ? ThemeManager.Get(ColorRole.Primary) : ThemeManager.Get(ColorRole.SubText);
                n.icon.color = c;
                n.label.color = c;
                if (n.highlight != null)
                {
                    n.highlight.gameObject.SetActive(n.target == tab);
                    n.highlight.color = ThemeManager.Get(ColorRole.PrimarySoft);
                }
            }
        }

        public void Say(string msg) { if (toast != null) toast.Show(msg); }
    }
}
