using UnityEngine;

namespace MediSimVR
{
    // Text-to-speech on the phone (Android TextToSpeech through Plugins/Android/MQSpeech.java). Used in demo mode to let
    // the presenter hear what a Quest would read aloud (lesson notes, important instructions); the real Quest reads the
    // text itself (receiver side). In the Editor it only logs.
    public static class PhoneSpeech
    {
        const string JavaClass = "com.highbrow.mq.MQSpeech";

        public static void Say(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using (var c = new AndroidJavaClass(JavaClass)) c.CallStatic("speak", text); }
            catch (System.Exception e) { Debug.LogWarning("[MediSimVR] phone speech: " + e.Message); }
#else
            Debug.Log("[MediSimVR] phone speech (Editor, not spoken): " + text);
#endif
        }

        public static void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using (var c = new AndroidJavaClass(JavaClass)) c.CallStatic("stop"); }
            catch (System.Exception e) { Debug.LogWarning("[MediSimVR] phone speech stop: " + e.Message); }
#endif
        }
    }
}
