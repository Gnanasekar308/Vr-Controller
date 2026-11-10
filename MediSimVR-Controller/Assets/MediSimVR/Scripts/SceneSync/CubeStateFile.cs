using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace MediSimVR
{
    // cube_state.json in Application.persistentDataPath (pretty JSON):
    // {"app":"MediSimVR","version":1,"savedAt":"...","position":[x,y,z],"rotationEuler":[x,y,z],"scale":[x,y,z]}
    [Serializable]
    public class CubeState
    {
        public string app = "MediSimVR";
        public int version = 1;
        public string savedAt = "";
        public float[] position = new float[3];
        public float[] rotationEuler = new float[3];
        public float[] scale = new float[3];
    }

    public static class CubeStateFile
    {
        public const string FileName = "cube_state.json";
        public const int Version = 1;

        public static string FilePath { get { return Path.Combine(Application.persistentDataPath, FileName); } }

        public static bool Save(Vector3 pos, Quaternion rot, Vector3 scale, out string error)
        {
            error = null;
            Vector3 e = rot.eulerAngles;
            var st = new CubeState
            {
                savedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                position = new[] { pos.x, pos.y, pos.z },
                rotationEuler = new[] { e.x, e.y, e.z },
                scale = new[] { scale.x, scale.y, scale.z }
            };
            string path = FilePath;
            string tmp = path + ".tmp";
            string bak = path + ".bak";
            try
            {
                // 1. new content in a temp file (a crash here never touches the old save)
                File.WriteAllText(tmp, JsonUtility.ToJson(st, true));
                // 2. old save -> .bak (renamed, never deleted before the new file is in place)
                bool hadOld = File.Exists(path);
                if (hadOld)
                {
                    if (File.Exists(bak)) File.Delete(bak); // stale backup from an earlier failed save
                    File.Move(path, bak);
                }
                // 3. temp -> cube_state.json; if that fails, the old save is put back
                try { File.Move(tmp, path); }
                catch (Exception)
                {
                    if (hadOld && !File.Exists(path) && File.Exists(bak)) File.Move(bak, path);
                    throw;
                }
                // 4. new save is in place: the backup is no longer needed
                if (hadOld)
                {
                    try { File.Delete(bak); }
                    catch (Exception delErr) { Debug.LogWarning("[MediSimVR] Could not delete " + bak + ": " + delErr.Message); }
                }
                Debug.Log("[MediSimVR] Saved " + path);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.LogError("[MediSimVR] Could not save " + path + ": " + ex);
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
                return false;
            }
        }

        public static bool TryLoad(out Vector3 pos, out Quaternion rot, out Vector3 scale, out string error)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            scale = Vector3.one;
            string path = FilePath;
            if (!File.Exists(path) && File.Exists(path + ".bak"))
            {
                // an earlier save stopped between steps 2 and 3: the previous state is still in the backup
                Debug.LogWarning("[MediSimVR] cube_state.json missing, loading the backup " + path + ".bak");
                path += ".bak";
            }
            if (!File.Exists(path)) { error = "No saved state yet (tap Save first)"; return false; }

            CubeState st;
            try { st = JsonUtility.FromJson<CubeState>(File.ReadAllText(path)); }
            catch (Exception ex) { error = "cube_state.json is not valid JSON"; Debug.LogWarning("[MediSimVR] " + path + ": " + ex.Message); return false; }

            if (st == null || st.app != "MediSimVR") { error = "cube_state.json is not a MediSimVR file"; return false; }
            if (st.version != Version) { error = "cube_state.json version " + st.version + " is not supported"; return false; }
            if (!Valid(st.position) || !Valid(st.rotationEuler) || !Valid(st.scale)) { error = "cube_state.json has missing or bad numbers"; return false; }
            if (st.scale[0] <= 0f || st.scale[1] <= 0f || st.scale[2] <= 0f) { error = "cube_state.json has a scale <= 0"; return false; }

            pos = new Vector3(st.position[0], st.position[1], st.position[2]);
            rot = Quaternion.Euler(st.rotationEuler[0], st.rotationEuler[1], st.rotationEuler[2]);
            scale = new Vector3(st.scale[0], st.scale[1], st.scale[2]);
            error = null;
            Debug.Log("[MediSimVR] Loaded " + path + " (saved " + st.savedAt + ")");
            return true;
        }

        private static bool Valid(float[] a)
        {
            if (a == null || a.Length != 3) return false;
            foreach (float v in a) if (float.IsNaN(v) || float.IsInfinity(v)) return false;
            return true;
        }
    }
}
