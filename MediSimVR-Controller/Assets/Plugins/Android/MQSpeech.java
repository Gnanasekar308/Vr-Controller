package com.highbrow.mq;

import android.os.Bundle;
import android.speech.tts.TextToSpeech;

import com.unity3d.player.UnityPlayer;

import java.util.Locale;

// Phone side only. Android text-to-speech for the demo: "read aloud on the Quest" is played on the phone when no Quest
// receiver is connected, so the presenter hears it. The real Quest reads the text itself (receiver side).
public class MQSpeech {
    private static TextToSpeech tts;
    private static boolean ready;
    private static String pending;

    public static void speak(final String text) {
        if (text == null || text.length() == 0) return;
        if (ready && tts != null) { say(text); return; }
        pending = text;
        if (tts != null) return;   // still starting
        try {
            tts = new TextToSpeech(UnityPlayer.currentActivity, new TextToSpeech.OnInitListener() {
                public void onInit(int status) {
                    ready = status == TextToSpeech.SUCCESS;
                    if (ready) {
                        try { tts.setLanguage(Locale.US); } catch (Throwable ignored) { }
                        if (pending != null) { say(pending); pending = null; }
                    }
                }
            });
        } catch (Throwable t) {
            tts = null;
        }
    }

    public static void stop() {
        try { if (tts != null) tts.stop(); } catch (Throwable ignored) { }
    }

    public static boolean isReady() { return ready; }

    private static void say(String text) {
        try { tts.speak(text, TextToSpeech.QUEUE_FLUSH, new Bundle(), "msvr"); } catch (Throwable ignored) { }
    }
}
