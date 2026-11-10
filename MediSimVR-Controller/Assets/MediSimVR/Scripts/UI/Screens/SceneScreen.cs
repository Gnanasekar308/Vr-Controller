using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // "Live Scene": a cube in a small 3D world (SyncWorld, rendered to a RenderTexture) that moves on every
    // connected receiver (PC Play mode, Quests) through SceneSyncClient. Save / Load / Reset use cube_state.json.
    public class SceneScreen : AppScreen
    {
        public TMP_Text statusText, readoutText, hintText;
        public Image statusDot;
        public Button linkButton, saveButton, loadButton, resetButton;
        public Segmented modeSeg;
        public RawImage view;
        public CubeGestures gestures;
        public Camera worldCamera;
        public Transform cube;

        public static readonly Vector3 DefaultPos = new Vector3(0f, 0.5f, 0f);
        public const string IpsKey = "msvr_scene_ips";

        private SceneSyncClient client;
        private RenderTexture rt;
        private readonly StringBuilder sb = new StringBuilder();
        private float nextReadout;

        public override void Init(Navigator n)
        {
            base.Init(n);
            client = new SceneSyncClient();
            client.SetManualIps(PlayerPrefs.GetString(IpsKey, ""));
            client.SetPose(cube.localPosition, cube.localRotation, cube.localScale, false);
            client.StateAdopted += OnAdopted;
            client.TargetsChanged += RefreshStatus;
            gestures.Changed += () => Push(true);
            modeSeg.Changed += i => { gestures.mode = (GestureMode)i; UpdateHint(); };
            linkButton.onClick.AddListener(AskIps);
            saveButton.onClick.AddListener(Save);
            loadButton.onClick.AddListener(Load);
            resetButton.onClick.AddListener(ResetCube);
            if (worldCamera != null) worldCamera.enabled = false; // only renders while this tab is visible
        }

        public override void OnShow()
        {
            EnsureTexture();
            if (worldCamera != null) worldCamera.enabled = true;
            client.Start();
            base.OnShow();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (worldCamera != null) worldCamera.enabled = false; // battery: no 3D rendering on other tabs
        }

        private void OnDestroy()
        {
            if (client != null) client.Dispose();
            if (rt != null) { rt.Release(); Destroy(rt); }
        }

        private void Update()
        {
            if (client == null) return;
            EnsureTexture(); // cheap when the size is unchanged; recreates the texture if the view was resized
            client.Tick(Time.unscaledTime);
            if (Time.unscaledTime >= nextReadout)
            {
                nextReadout = Time.unscaledTime + 0.1f;
                UpdateReadout();
            }
        }

        public override void Refresh()
        {
            RefreshStatus();
            UpdateReadout();
            UpdateHint();
        }

        private void UpdateHint()
        {
            if (hintText == null) return;
            switch (gestures.mode)
            {
                case GestureMode.Move: hintText.text = "1 finger: slide  •  2 fingers up/down: height  •  pinch: size"; break;
                case GestureMode.Rotate: hintText.text = "1 finger: turn  •  2-finger twist: spin  •  pinch: size"; break;
                default: hintText.text = "1 finger up/down: size  •  pinch: size"; break;
            }
        }

        // ---------- view ----------

        private void EnsureTexture()
        {
            if (worldCamera == null || view == null) return;
            Canvas c = view.canvas != null ? view.canvas.rootCanvas : null;
            float f = c != null ? c.scaleFactor : 1f;
            Rect r = view.rectTransform.rect;
            if (r.width < 1f || r.height < 1f) return; // not laid out yet: Update() tries again next frame
            int w = Mathf.Clamp(Mathf.RoundToInt(r.width * f), 64, 2048);
            int h = Mathf.Clamp(Mathf.RoundToInt(r.height * f), 64, 2048);
            if (rt != null && rt.width == w && rt.height == h) return;
            if (rt != null) { worldCamera.targetTexture = null; rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "SceneView", antiAliasing = 2 };
            rt.Create();
            worldCamera.targetTexture = rt;
            view.texture = rt;
            Debug.Log("[MediSimVR] Scene view RenderTexture " + w + "x" + h);
        }

        // ---------- sync ----------

        private void Push(bool userChange)
        {
            client.SetPose(cube.localPosition, cube.localRotation, cube.localScale, userChange);
            nextReadout = 0f;
        }

        private void OnAdopted(Vector3 p, Quaternion r, Vector3 s, string from)
        {
            cube.localPosition = p;
            cube.localRotation = r;
            cube.localScale = s;
            nextReadout = 0f;
            nav.Say("Cube state taken from " + from);
        }

        private void RefreshStatus()
        {
            if (statusText == null) return;
            var list = client.Targets;
            if (list.Count == 0)
            {
                statusText.text = client.Error != null ? "Network error: " + client.Error : "Searching PC / Quest on this Wi-Fi...";
                statusDot.color = ThemeManager.Get(ColorRole.Warning);
                return;
            }
            sb.Length = 0;
            sb.Append("Connected: ");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(list[i].Name);
            }
            statusText.text = sb.ToString();
            statusDot.color = ThemeManager.Get(ColorRole.Accent);
        }

        private void UpdateReadout()
        {
            if (readoutText == null || cube == null) return;
            Vector3 p = cube.localPosition, e = cube.localRotation.eulerAngles, s = cube.localScale;
            readoutText.text = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Position  {0:0.00}, {1:0.00}, {2:0.00} m\nRotation  {3:0}, {4:0}, {5:0} deg\nScale  {6:0.00}, {7:0.00}, {8:0.00}",
                p.x, p.y, p.z, e.x, e.y, e.z, s.x, s.y, s.z);
        }

        // ---------- buttons ----------

        private void AskIps()
        {
            string current = PlayerPrefs.GetString(IpsKey, "");
            nav.popup.Show("Receiver IPs",
                "Type the IP of each PC / Quest, separated by commas. Leave empty to use Wi-Fi discovery only.",
                "Save", null, "Cancel", "192.168.1.20, 192.168.1.31", text =>
                {
                    List<string> bad;
                    var ok = SceneSyncClient.ParseIps(text, out bad);
                    client.SetManualIps(text);
                    sb.Length = 0;
                    foreach (var a in ok) { if (sb.Length > 0) sb.Append(", "); sb.Append(a); }
                    PlayerPrefs.SetString(IpsKey, sb.ToString());
                    PlayerPrefs.Save();
                    nav.Say(bad.Count > 0 ? "Not an IP: " + string.Join(", ", bad.ToArray())
                        : ok.Count == 0 ? "Manual IPs cleared" : "Saved " + ok.Count + " IP(s)");
                    RefreshStatus();
                });
            nav.popup.input.text = current;
        }

        private void Save()
        {
            string err;
            nav.Say(CubeStateFile.Save(cube.localPosition, cube.localRotation, cube.localScale, out err)
                ? "Saved to " + CubeStateFile.FileName : "Save failed: " + err);
        }

        private void Load()
        {
            Vector3 p, s;
            Quaternion r;
            string err;
            if (!CubeStateFile.TryLoad(out p, out r, out s, out err)) { nav.Say(err); return; }
            cube.localPosition = p;
            cube.localRotation = r;
            cube.localScale = s;
            Push(true);
            nav.Say("Loaded " + CubeStateFile.FileName + ", sending to " + client.Targets.Count + " device(s)");
        }

        private void ResetCube()
        {
            cube.localPosition = DefaultPos;
            cube.localRotation = Quaternion.identity;
            cube.localScale = Vector3.one;
            Push(true);
            nav.Say("Cube reset");
        }
    }
}
