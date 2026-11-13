package com.warehouseduel.profile;

import android.app.Activity;
import android.app.Fragment;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Matrix;
import android.media.ExifInterface;
import android.net.Uri;
import android.os.Bundle;

import com.unity3d.player.UnityPlayer;

import java.io.FileOutputStream;
import java.io.InputStream;

/**
 * Headless fragment that opens the system image picker (ACTION_GET_CONTENT: no storage permission needed),
 * then decodes the chosen picture, applies its EXIF rotation, centre-crops it to a square, scales it and
 * writes a PNG. The result path ("" when cancelled, "error:..." on failure) goes back to Unity through
 * UnitySendMessage. A fragment is used so no extra activity has to be declared in the manifest.
 */
@SuppressWarnings("deprecation")
public class WDAvatarPicker extends Fragment {
    private static final int REQUEST_CODE = 0x5744;
    private static final String TAG = "WDAvatarPicker";

    private String receiver;
    private String method;
    private String outputPath;
    private int size;
    private boolean finished;

    public static void pick(final Activity activity, final String receiver, final String method, final String outputPath, final int size) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    WDAvatarPicker fragment = new WDAvatarPicker();
                    Bundle args = new Bundle();
                    args.putString("receiver", receiver);
                    args.putString("method", method);
                    args.putString("output", outputPath);
                    args.putInt("size", size);
                    fragment.setArguments(args);
                    activity.getFragmentManager().beginTransaction().add(fragment, TAG).commitAllowingStateLoss();
                } catch (Throwable t) {
                    UnityPlayer.UnitySendMessage(receiver, method, "error:" + t.getMessage());
                }
            }
        });
    }

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        Bundle args = getArguments();
        receiver = args.getString("receiver");
        method = args.getString("method");
        outputPath = args.getString("output");
        size = args.getInt("size", 256);

        if (savedInstanceState != null) return; // re-created after a configuration change: the picker is already open
        Intent intent = new Intent(Intent.ACTION_GET_CONTENT);
        intent.setType("image/*");
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        try {
            startActivityForResult(Intent.createChooser(intent, "Choose a profile picture"), REQUEST_CODE);
        } catch (Throwable t) {
            finish("error:no picker");
        }
    }

    @Override
    public void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode != REQUEST_CODE) return;
        if (resultCode != Activity.RESULT_OK || data == null || data.getData() == null) {
            finish("");
            return;
        }
        final Uri uri = data.getData();
        final Activity activity = getActivity();
        new Thread(new Runnable() {
            @Override
            public void run() {
                String result;
                try {
                    result = process(activity, uri) ? outputPath : "error:decode";
                } catch (Throwable t) {
                    result = "error:" + t.getMessage();
                }
                finish(result);
            }
        }).start();
    }

    private boolean process(Activity activity, Uri uri) throws Exception {
        // Pass 1: dimensions only, to pick a memory-friendly sample size.
        BitmapFactory.Options bounds = new BitmapFactory.Options();
        bounds.inJustDecodeBounds = true;
        try (InputStream in = activity.getContentResolver().openInputStream(uri)) {
            BitmapFactory.decodeStream(in, null, bounds);
        }
        if (bounds.outWidth <= 0 || bounds.outHeight <= 0) return false;
        int sample = 1;
        while (Math.min(bounds.outWidth, bounds.outHeight) / (sample * 2) >= size * 2) sample *= 2;

        BitmapFactory.Options options = new BitmapFactory.Options();
        options.inSampleSize = sample;
        Bitmap bitmap;
        try (InputStream in = activity.getContentResolver().openInputStream(uri)) {
            bitmap = BitmapFactory.decodeStream(in, null, options);
        }
        if (bitmap == null) return false;

        int rotation = 0;
        try (InputStream in = activity.getContentResolver().openInputStream(uri)) {
            ExifInterface exif = new ExifInterface(in);
            int orientation = exif.getAttributeInt(ExifInterface.TAG_ORIENTATION, ExifInterface.ORIENTATION_NORMAL);
            if (orientation == ExifInterface.ORIENTATION_ROTATE_90) rotation = 90;
            else if (orientation == ExifInterface.ORIENTATION_ROTATE_180) rotation = 180;
            else if (orientation == ExifInterface.ORIENTATION_ROTATE_270) rotation = 270;
        } catch (Throwable ignored) {
            // No EXIF (e.g. PNG): keep as is.
        }

        int side = Math.min(bitmap.getWidth(), bitmap.getHeight());
        int x = (bitmap.getWidth() - side) / 2;
        int y = (bitmap.getHeight() - side) / 2;
        Matrix matrix = new Matrix();
        float scale = size / (float) side;
        matrix.postScale(scale, scale);
        if (rotation != 0) matrix.postRotate(rotation);
        Bitmap square = Bitmap.createBitmap(bitmap, x, y, side, side, matrix, true);

        try (FileOutputStream out = new FileOutputStream(outputPath)) {
            square.compress(Bitmap.CompressFormat.PNG, 100, out);
        }
        if (square != bitmap) square.recycle();
        bitmap.recycle();
        return true;
    }

    private synchronized void finish(final String result) {
        if (finished) return;
        finished = true;
        UnityPlayer.UnitySendMessage(receiver, method, result);
        final Activity activity = getActivity();
        if (activity != null) {
            activity.runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    try {
                        activity.getFragmentManager().beginTransaction().remove(WDAvatarPicker.this).commitAllowingStateLoss();
                    } catch (Throwable ignored) {
                    }
                }
            });
        }
    }
}
