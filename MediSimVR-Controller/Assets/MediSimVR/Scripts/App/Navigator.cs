using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    public enum ScreenId { Splash, RoleSelect, Login, Dashboard, Meetings, MeetingCreate, MeetingDetails, Live, Report,
        Devices, Send, Control, Scene, Settings, Notifications, EditApp }

    [Serializable]
    public class NavItem
    {
        public ScreenId target;
        public Button button;
        public Image icon;
        public TMP_Text label;
        public Image highlight;   // soft pill behind the selected tab
        public bool adminOnly;    // Live and Devices: hidden for a student
    }

    // Shows one screen at a time + the role-based bottom navigation + back stack.
    //   before login (Splash, Role selection, Login): no bottom bar
    //   Admin: Dashboard, Meetings, Live, Devices, Settings      Student: Dashboard, Meetings, Settings
    public class Navigator : MonoBehaviour
    {
        public List<AppScreen> screens = new List<AppScreen>();
        public List<NavItem> navItems = new List<NavItem>();
        public GameObject navBar;
        public RectTransform screensRoot;
        public float navHeight = 200f;
        public Popup popup;
        public Toast toast;
        public ColorPickerPopup colorPicker;
        public ActionSheet actionSheet;
        public TextEditorPopup textEditor;

        readonly Stack<ScreenId> back = new Stack<ScreenId>();
        ScreenId current = ScreenId.Splash;

        public ScreenId Current { get { return current; } }
        // the screen Back returns to (null = none): e.g. Send knows it was opened from the Live session
        public ScreenId? Previous { get { return back.Count > 0 ? back.Peek() : (ScreenId?)null; } }

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
            Classroom.Changed += Chrome;
            Show(ScreenId.Splash);
        }

        void OnDestroy()
        {
            ThemeManager.Changed -= Highlight;
            Classroom.Changed -= Chrome;
        }

        // the first screen of the logged-in role (Role selection before login)
        public ScreenId HomeScreen { get { return Classroom.Role == UserRole.None ? ScreenId.RoleSelect : ScreenId.Dashboard; } }

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
            var cur = Find(current);
            if (cur != null && cur.HandleBack()) return; // e.g. a sheet open on the screen
            ScreenId parent;
            if (cur != null && cur.TryGetParent(out parent)) { back.Clear(); Show(parent); return; }
            if (back.Count > 0) { Show(back.Pop()); return; }
            if (current != HomeScreen && current != ScreenId.Splash) { Show(HomeScreen); return; }
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
            Debug.Log("[MediSimVR] screen " + id);
            Chrome();
        }

        // bottom bar: visible after login (and not while a screen asks to hide it, e.g. the Live session in landscape);
        // a student does not see Live and Devices
        public void Chrome()
        {
            var cur = Find(current);
            bool show = Classroom.Role != UserRole.None && (cur == null || cur.ShowsNav);
            if (navBar != null && navBar.activeSelf != show) navBar.SetActive(show);
            if (screensRoot != null) screensRoot.offsetMin = new Vector2(screensRoot.offsetMin.x, show ? navHeight : 0f);
            foreach (var n in navItems)
            {
                bool visible = !n.adminOnly || Classroom.IsAdmin;
                if (n.button.gameObject.activeSelf != visible) n.button.gameObject.SetActive(visible);
            }
            Highlight();
        }

        // which tab a screen belongs to
        public ScreenId TabOf(ScreenId s)
        {
            switch (s)
            {
                case ScreenId.Notifications: return ScreenId.Dashboard;
                case ScreenId.MeetingCreate:
                case ScreenId.MeetingDetails: return ScreenId.Meetings;
                case ScreenId.Report:
                case ScreenId.Scene: return ScreenId.Live;
                case ScreenId.Send:
                case ScreenId.Control: return Classroom.LiveActive ? ScreenId.Live : ScreenId.Devices;
                case ScreenId.EditApp: return ScreenId.Settings;
                default: return s;
            }
        }

        void Highlight()
        {
            ScreenId tab = TabOf(current);
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

        public T Get<T>() where T : AppScreen
        {
            foreach (var s in screens) if (s is T) return (T)s;
            return null;
        }

        AppScreen Find(ScreenId id)
        {
            foreach (var s in screens) if (s.id == id) return s;
            return null;
        }
    }
}
