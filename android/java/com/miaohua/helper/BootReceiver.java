package com.miaohua.helper;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.os.Build;

/** 开机之后把常驻服务拉起来 —— 只在用户打开过那个开关时才做。 */
public class BootReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(Context c, Intent i) {
        try {
            if (c == null || !KeepAliveService.enabled(c)) return;
            Intent it = new Intent(c, KeepAliveService.class);
            if (Build.VERSION.SDK_INT >= 26) c.startForegroundService(it);
            else c.startService(it);
        } catch (Exception e) { }
    }
}