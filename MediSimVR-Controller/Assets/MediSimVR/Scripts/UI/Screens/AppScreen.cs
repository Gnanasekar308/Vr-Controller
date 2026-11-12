using System.Collections.Generic;
using UnityEngine;

namespace MediSimVR
{
    // Base for every screen. Refresh() is called when shown and whenever the service data changes.
    public abstract class AppScreen : MonoBehaviour
    {
        public ScreenId id;
        protected Navigator nav;
        protected IControllerService Svc { get { return AppRoot.Service; } }

        public virtual void Init(Navigator n) { nav = n; }

        public virtual void OnShow() { Refresh(); }

        protected virtual void OnEnable()
        {
            if (AppRoot.Service != null) AppRoot.Service.Changed += OnData;
            ThemeManager.Changed += OnData;
        }

        protected virtual void OnDisable()
        {
            if (AppRoot.Service != null) AppRoot.Service.Changed -= OnData;
            ThemeManager.Changed -= OnData;
        }

        void OnData() { if (nav != null && isActiveAndEnabled) Refresh(); }

        public abstract void Refresh();

        // like Fill, but keeps the existing clones (no flicker / garbage when a list refreshes often); the caller rebinds them
        public static List<T> Keep<T>(T template, List<T> cache, int count) where T : Component
        {
            cache.RemoveAll(x => x == null);
            while (cache.Count > count)
            {
                Destroy(cache[cache.Count - 1].gameObject);
                cache.RemoveAt(cache.Count - 1);
            }
            while (cache.Count < count)
            {
                var r = Instantiate(template, template.transform.parent);
                r.name = template.name + "_" + cache.Count;
                r.gameObject.SetActive(true);
                cache.Add(r);
            }
            return cache;
        }

        // Android back: true = handled here (e.g. a sheet was closed), false = go back a screen
        public virtual bool HandleBack() { return false; }

        // fixed "up" screen of the Control Mode flow (Play page -> Quest list / Session); false = use the back stack
        public virtual bool TryGetParent(out ScreenId parent) { parent = ScreenId.Home; return false; }

        // clone list rows from an inactive template; keeps the template itself
        protected static List<T> Fill<T>(T template, int count) where T : Component
        {
            var parent = template.transform.parent;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var c = parent.GetChild(i);
                if (c != template.transform && c.name.StartsWith(template.name)) Destroy(c.gameObject);
            }
            var list = new List<T>();
            for (int i = 0; i < count; i++)
            {
                var r = Instantiate(template, parent);
                r.name = template.name + "_" + i;
                r.gameObject.SetActive(true);
                list.Add(r);
            }
            return list;
        }
    }
}
