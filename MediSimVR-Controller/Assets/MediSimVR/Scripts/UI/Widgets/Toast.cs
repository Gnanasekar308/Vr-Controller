using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MediSimVR
{
    public class Toast : MonoBehaviour
    {
        public GameObject root;
        public TMP_Text text;
        float hideAt;

        void Awake() { root.SetActive(false); }

        public void Show(string msg)
        {
            text.text = msg;
            root.SetActive(true);
            hideAt = Time.unscaledTime + 2.4f;
        }

        void Update() { if (root.activeSelf && Time.unscaledTime > hideAt) root.SetActive(false); }
    }
}
