package com.highbrow.mq;

import android.app.Activity;
import android.app.Fragment;
import android.content.ClipData;
import android.content.ContentResolver;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;

import com.unity3d.player.UnityPlayer;

// Phone side only. Opens the system picker for videos and gives Unity readable file descriptors.
// Place at Assets/Plugins/Android/MQGallery.java (Unity compiles it with the Android build).
public class MQGallery {
    private static final int REQ = 0x4D51;

    public static void pick(final String gameObject, final String method, final boolean multiple) {
        final Activity a = UnityPlayer.currentActivity;
        if (a == null) { UnityPlayer.UnitySendMessage(gameObject, method, "ERR\tNo activity"); return; }
        a.runOnUiThread(new Runnable() {
            public void run() {
                try {
                    PickFragment f = new PickFragment();
                    Bundle b = new Bundle();
                    b.putString("go", gameObject);
                    b.putString("m", method);
                    b.putBoolean("multi", multiple);
                    f.setArguments(b);
                    a.getFragmentManager().beginTransaction().add(f, "mq_pick").commitAllowingStateLoss();
                } catch (Throwable t) {
                    UnityPlayer.UnitySendMessage(gameObject, method, "ERR\t" + t.getMessage());
                }
            }
        });
    }

    // Same as pick(), but with a MIME filter (comma separated), e.g. "video/mp4,video/webm".
    public static void pickTypes(final String gameObject, final String method, final boolean multiple, final String mimes) {
        final Activity a = UnityPlayer.currentActivity;
        if (a == null) { UnityPlayer.UnitySendMessage(gameObject, method, "ERR\tNo activity"); return; }
        a.runOnUiThread(new Runnable() {
            public void run() {
                try {
                    PickFragment f = new PickFragment();
                    Bundle b = new Bundle();
                    b.putString("go", gameObject);
                    b.putString("m", method);
                    b.putBoolean("multi", multiple);
                    b.putString("mimes", mimes);
                    f.setArguments(b);
                    a.getFragmentManager().beginTransaction().add(f, "mq_pick").commitAllowingStateLoss();
                } catch (Throwable t) {
                    UnityPlayer.UnitySendMessage(gameObject, method, "ERR\t" + t.getMessage());
                }
            }
        });
    }

    // Reads a picked text file (UTF-8), at most maxBytes. Returns "" on failure.
    public static String readText(String uri, int maxBytes) {
        java.io.InputStream in = null;
        try {
            in = UnityPlayer.currentActivity.getContentResolver().openInputStream(Uri.parse(uri));
            if (in == null) return "";
            byte[] buf = new byte[maxBytes];
            int total = 0, n;
            while (total < maxBytes && (n = in.read(buf, total, maxBytes - total)) > 0) total += n;
            return new String(buf, 0, total, "UTF-8");
        } catch (Throwable t) {
            return "";
        } finally {
            try { if (in != null) in.close(); } catch (Throwable ignored) { }
        }
    }

    // Reads a picked file (e.g. an image for the preview), at most maxBytes. Returns null on failure.
    public static byte[] readBytes(String uri, int maxBytes) {
        java.io.InputStream in = null;
        try {
            in = UnityPlayer.currentActivity.getContentResolver().openInputStream(Uri.parse(uri));
            if (in == null) return null;
            java.io.ByteArrayOutputStream out = new java.io.ByteArrayOutputStream();
            byte[] buf = new byte[65536];
            int n, total = 0;
            while ((n = in.read(buf)) > 0) {
                total += n;
                if (total > maxBytes) return null;
                out.write(buf, 0, n);
            }
            return out.toByteArray();
        } catch (Throwable t) {
            return null;
        } finally {
            try { if (in != null) in.close(); } catch (Throwable ignored) { }
        }
    }

    // Copies a picked file (content:// uri) to 'path' on a background thread (videos can be large) and answers
    // "OK\t<path>" or "ERR\t<path>\t<message>" to gameObject.method (the path tells which request it answers). Used for the phone's video preview: Unity's VideoPlayer
    // cannot read content:// uris. An existing complete copy is reused.
    public static void copyToFile(final String uri, final String path, final String gameObject, final String method) {
        new Thread(new Runnable() {
            public void run() {
                java.io.InputStream in = null;
                java.io.OutputStream out = null;
                try {
                    java.io.File f = new java.io.File(path);
                    if (f.exists() && f.length() > 0) { UnityPlayer.UnitySendMessage(gameObject, method, "OK\t" + path); return; }
                    java.io.File dir = f.getParentFile();
                    if (dir != null) dir.mkdirs();
                    java.io.File part = new java.io.File(path + ".part");
                    in = UnityPlayer.currentActivity.getContentResolver().openInputStream(Uri.parse(uri));
                    if (in == null) throw new java.io.IOException("cannot open " + uri);
                    out = new java.io.FileOutputStream(part);
                    byte[] buf = new byte[262144];
                    int n;
                    while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
                    out.close();
                    out = null;
                    if (!part.renameTo(f)) throw new java.io.IOException("cannot rename the copy");
                    UnityPlayer.UnitySendMessage(gameObject, method, "OK\t" + path);
                } catch (Throwable t) {
                    UnityPlayer.UnitySendMessage(gameObject, method, "ERR\t" + path + "\t" + t.getMessage());
                } finally {
                    try { if (in != null) in.close(); } catch (Throwable ignored) { }
                    try { if (out != null) out.close(); } catch (Throwable ignored) { }
                }
            }
        }).start();
    }

    // Returns a detached fd (owned by the caller) or -1.
    public static int openFd(String uri) {
        try {
            ParcelFileDescriptor p = UnityPlayer.currentActivity.getContentResolver()
                    .openFileDescriptor(Uri.parse(uri), "r");
            return p == null ? -1 : p.detachFd();
        } catch (Throwable t) {
            return -1;
        }
    }

    public static class PickFragment extends Fragment {
        private boolean launched;

        @Override
        public void onCreate(Bundle state) {
            super.onCreate(state);
            if (state != null) launched = state.getBoolean("launched");
            if (launched) return;
            launched = true;
            Intent i = new Intent(Intent.ACTION_OPEN_DOCUMENT);
            i.addCategory(Intent.CATEGORY_OPENABLE);
            String mimes = getArguments().getString("mimes");
            if (mimes != null && mimes.length() > 0) {
                i.setType("*/*");
                i.putExtra(Intent.EXTRA_MIME_TYPES, mimes.split(","));
            } else {
                i.setType("video/*");
            }
            i.putExtra(Intent.EXTRA_ALLOW_MULTIPLE, getArguments().getBoolean("multi"));
            i.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
            try {
                startActivityForResult(i, REQ);
            } catch (Throwable t) {
                send("ERR\tNo gallery or file picker available: " + t.getMessage());
                remove();
            }
        }

        @Override
        public void onSaveInstanceState(Bundle out) {
            super.onSaveInstanceState(out);
            out.putBoolean("launched", launched);
        }

        @Override
        public void onActivityResult(int req, int res, Intent data) {
            if (req != REQ) return;
            StringBuilder sb = new StringBuilder();
            try {
                if (res == Activity.RESULT_OK && data != null) {
                    ContentResolver cr = getActivity().getContentResolver();
                    ClipData clip = data.getClipData();
                    if (clip != null) {
                        for (int k = 0; k < clip.getItemCount(); k++) add(cr, clip.getItemAt(k).getUri(), sb);
                    } else if (data.getData() != null) {
                        add(cr, data.getData(), sb);
                    }
                }
                send(sb.toString());
            } catch (Throwable t) {
                send("ERR\t" + t.getMessage());
            }
            remove();
        }

        private static void add(ContentResolver cr, Uri uri, StringBuilder sb) {
            if (uri == null) return;
            try { cr.takePersistableUriPermission(uri, Intent.FLAG_GRANT_READ_URI_PERMISSION); } catch (Throwable ignored) { }
            String name = null;
            long size = -1;
            Cursor c = null;
            try {
                c = cr.query(uri, new String[]{OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE}, null, null, null);
                if (c != null && c.moveToFirst()) {
                    if (!c.isNull(0)) name = c.getString(0);
                    if (!c.isNull(1)) size = c.getLong(1);
                }
            } catch (Throwable ignored) {
            } finally {
                if (c != null) c.close();
            }
            if (name == null) name = uri.getLastPathSegment();
            if (name == null) name = "video.mp4";
            name = name.replace('\t', ' ').replace('\n', ' ');
            sb.append(uri.toString()).append('\t').append(size).append('\t').append(name).append('\n');
        }

        private void send(String s) {
            Bundle b = getArguments();
            UnityPlayer.UnitySendMessage(b.getString("go"), b.getString("m"), s);
        }

        private void remove() {
            try { getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss(); } catch (Throwable ignored) { }
        }
    }
}
