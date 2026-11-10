using System;
using System.Collections.Generic;
using MediSimVR.SceneSync;

namespace MediSimVR
{
    // The phone's copy of the live scene: objects, selection, edits and Undo / Redo (one step per finished gesture
    // or Inspector edit, never per frame). The screen listens to the events to update the 3D view, the UI and the network.
    public class SceneDocument
    {
        public const int MaxUndo = 100;

        public event Action Loaded;
        public event Action SelectionChanged;
        public event Action<SceneObjectState, bool> PoseChanged;   // object, final (false = still dragging)
        public event Action<SceneObjectState> LockChanged;
        public event Action HistoryChanged;

        public readonly List<SceneObjectState> Objects = new List<SceneObjectState>();
        public SceneObjectState Selected { get; private set; }
        public bool HasClipboard { get; private set; }
        public bool Editing { get { return editObj != null; } }
        public SceneObjectState EditObject { get { return editObj; } }
        public ScenePose EditStart { get { return editStart; } }
        public int UndoCount { get { return undo.Count; } }
        public int RedoCount { get { return redo.Count; } }

        private class Change
        {
            public SceneObjectState obj;
            public ScenePose before, after;
        }

        private class Step
        {
            public string label;
            public List<Change> changes;
        }

        private readonly Dictionary<string, SceneObjectState> byId = new Dictionary<string, SceneObjectState>();
        private readonly List<Step> undo = new List<Step>();
        private readonly List<Step> redo = new List<Step>();
        private SceneObjectState editObj;
        private ScenePose editStart;
        private string editLabel;
        private ScenePose clipboard;

        public void Load(IList<SyncObjectInfo> infos)
        {
            Objects.Clear();
            byId.Clear();
            undo.Clear();
            redo.Clear();
            editObj = null;
            Selected = null;
            foreach (var info in infos)
            {
                if (info == null || !info.IsValid() || byId.ContainsKey(info.id)) continue;
                var o = SceneObjectState.From(info);
                Objects.Add(o);
                byId[o.id] = o;
            }
            if (Loaded != null) Loaded();
            if (SelectionChanged != null) SelectionChanged();
            if (HistoryChanged != null) HistoryChanged();
        }

        public SceneObjectState Get(string id)
        {
            SceneObjectState o;
            return id != null && byId.TryGetValue(id, out o) ? o : null;
        }

        public void Select(string id)
        {
            var o = Get(id);
            if (o == Selected) return;
            if (Editing) CommitEdit();
            Selected = o;
            if (SelectionChanged != null) SelectionChanged();
        }

        // ---------- edits ----------

        // start a gesture / scrub on one object. false = locked (nothing happens)
        public bool BeginEdit(SceneObjectState o, string label)
        {
            if (o == null || o.locked) return false;
            if (Editing) CommitEdit();
            editObj = o;
            editStart = o.pose;
            editLabel = label;
            return true;
        }

        // live value while the finger is still down
        public void Preview(ScenePose p)
        {
            if (editObj == null) return;
            editObj.pose = p.Sanitized();
            if (PoseChanged != null) PoseChanged(editObj, false);
        }

        // finger lifted / value confirmed: one undo step if something changed. The final pose is always announced
        // (in "Live" mode the receivers saw the drag and must end on exactly this pose).
        public bool CommitEdit()
        {
            if (editObj == null) return false;
            var o = editObj;
            editObj = null;
            bool changed = !o.pose.SameAs(editStart);
            if (changed) Push(new Step { label = editLabel, changes = new List<Change> { new Change { obj = o, before = editStart, after = o.pose } } });
            if (PoseChanged != null) PoseChanged(o, true);
            return changed;
        }

        public void CancelEdit()
        {
            if (editObj == null) return;
            var o = editObj;
            editObj = null;
            o.pose = editStart;
            if (PoseChanged != null) PoseChanged(o, true);
        }

        // one finished edit in a single call (+/- buttons, typed values, paste)
        public bool Apply(SceneObjectState o, ScenePose p, string label)
        {
            if (!BeginEdit(o, label)) return false;
            Preview(p);
            return CommitEdit();
        }

        // several objects in ONE undo step (Reset). Locked objects are skipped. Returns how many changed.
        public int ApplyMany(List<KeyValuePair<SceneObjectState, ScenePose>> list, string label)
        {
            if (Editing) CommitEdit();
            var changes = new List<Change>();
            foreach (var kv in list)
            {
                var o = kv.Key;
                if (o == null || o.locked) continue;
                var p = kv.Value.Sanitized();
                if (o.pose.SameAs(p)) continue;
                changes.Add(new Change { obj = o, before = o.pose, after = p });
                o.pose = p;
                if (PoseChanged != null) PoseChanged(o, true);
            }
            if (changes.Count > 0) Push(new Step { label = label, changes = changes });
            return changes.Count;
        }

        public int ResetAll()
        {
            var list = new List<KeyValuePair<SceneObjectState, ScenePose>>();
            foreach (var o in Objects) list.Add(new KeyValuePair<SceneObjectState, ScenePose>(o, o.initialPose));
            return ApplyMany(list, "Reset");
        }

        public void SetLocked(SceneObjectState o, bool locked)
        {
            if (o == null || o.locked == locked) return;
            if (editObj == o) CommitEdit();
            o.locked = locked;
            if (LockChanged != null) LockChanged(o);
        }

        public void Copy(SceneObjectState o)
        {
            if (o == null) return;
            clipboard = o.pose;
            HasClipboard = true;
        }

        public bool Paste(SceneObjectState o)
        {
            if (o == null || !HasClipboard) return false;
            return Apply(o, clipboard, "Paste");
        }

        // ---------- undo / redo (also used when the object is locked now: it is an explicit command) ----------

        public string Undo() { return Walk(undo, redo, true); }
        public string Redo() { return Walk(redo, undo, false); }

        private string Walk(List<Step> from, List<Step> to, bool back)
        {
            if (Editing) CommitEdit();
            if (from.Count == 0) return null;
            var step = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            to.Add(step);
            foreach (var c in step.changes)
            {
                c.obj.pose = back ? c.before : c.after;
                if (PoseChanged != null) PoseChanged(c.obj, true);
            }
            if (HistoryChanged != null) HistoryChanged();
            return step.label;
        }

        private void Push(Step s)
        {
            undo.Add(s);
            if (undo.Count > MaxUndo) undo.RemoveAt(0);
            redo.Clear();
            if (HistoryChanged != null) HistoryChanged();
        }
    }
}
