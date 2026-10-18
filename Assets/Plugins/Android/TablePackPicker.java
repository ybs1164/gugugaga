package com.ybs1164.gugugaga;

import android.app.Activity;
import android.app.Fragment;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;

import com.unity3d.player.UnityPlayer;

import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;

/**
 * 안드로이드 파일 고르기(Storage Access Framework)로 표 팩 zip 하나를 골라 destPath로 복사한 뒤
 * UnitySendMessage(gameObject, method, 복사한 경로 — 취소·실패면 빈 문자열)로 알린다. C# 쪽: View/TablePackPicker.
 * 결과를 받으려고 화면 없는 Fragment를 잠깐 붙인다. 고르는 동안 화면을 돌려도 다시 열지 않도록 인자는 Bundle에 둔다.
 */
public class TablePackPicker extends Fragment {
    private static final int REQUEST = 7311;
    private static final String TAG = "TablePackPicker";

    public static void open(final String gameObject, final String method, final String destPath) {
        final Activity activity = UnityPlayer.currentActivity;
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                TablePackPicker picker = new TablePackPicker();
                Bundle args = new Bundle();
                args.putString("gameObject", gameObject);
                args.putString("method", method);
                args.putString("destPath", destPath);
                picker.setArguments(args);
                activity.getFragmentManager().beginTransaction().add(picker, TAG).commitAllowingStateLoss();
            }
        });
    }

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (savedInstanceState != null) return;
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("*/*");
        intent.putExtra(Intent.EXTRA_MIME_TYPES, new String[] { "application/zip", "application/x-zip-compressed", "application/octet-stream" });
        try {
            startActivityForResult(intent, REQUEST);
        } catch (Exception e) {
            finish("");
        }
    }

    @Override
    public void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode != REQUEST) return;
        if (resultCode != Activity.RESULT_OK || data == null || data.getData() == null) {
            finish("");
            return;
        }
        final Uri uri = data.getData();
        final String dest = getArguments().getString("destPath");
        final Activity activity = getActivity();
        new Thread(new Runnable() {
            @Override
            public void run() {
                String result = "";
                try (InputStream in = activity.getContentResolver().openInputStream(uri);
                     OutputStream out = new FileOutputStream(dest)) {
                    byte[] buffer = new byte[16384];
                    int n;
                    while ((n = in.read(buffer)) > 0) out.write(buffer, 0, n);
                    result = dest;
                } catch (Exception e) {
                    result = "";
                }
                final String path = result;
                activity.runOnUiThread(new Runnable() {
                    @Override
                    public void run() { finish(path); }
                });
            }
        }).start();
    }

    private void finish(String path) {
        Bundle args = getArguments();
        UnityPlayer.UnitySendMessage(args.getString("gameObject"), args.getString("method"), path);
        if (getFragmentManager() != null) getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();
    }
}
