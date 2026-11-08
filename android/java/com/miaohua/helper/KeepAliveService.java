package com.miaohua.helper;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Build;
import android.os.IBinder;

/**
 * 常驻前台服务 —— 让喵化助手不容易被系统杀掉。
 *
 * 为什么需要：无障碍服务跑在应用进程里。手机应用开多了、或者系统省电策略激进的时候，
 * 整个进程会被回收，无障碍跟着一起没。用户看到的症状就是"用着用着突然不生效了"。
 *
 * 前台服务是安卓官方给的正规做法：带一条常驻通知，进程优先级提到"前台"，
 * 系统基本不会回收；被杀之后 START_STICKY 还会让系统把它拉回来。
 *
 * ★ 明确不做的事：不搞双进程互拉、不改签名、不用任何"杀不死"的黑科技。
 *   那些要么不可审计、要么违反商店规则，而且国产 ROM 照样能杀。
 *   官方这条路覆盖绝大多数情况，剩下的用户可以手动把应用加进系统白名单。
 */
public class KeepAliveService extends Service {

    /** 和界面共用一份偏好设置。 */
    public static final String PREF = "miaohua_style";
    public static final String KEY = "keep_alive";
    static final String CHANNEL = "miaohua_alive";
    static final int NOTI_ID = 1001;

    public static boolean enabled(Context c) {
        try {
            return c.getSharedPreferences(PREF, Context.MODE_PRIVATE).getBoolean(KEY, false);
        } catch (Exception e) { return false; }
    }

    /** 开关的唯一入口：写偏好 + 起停服务。界面和开机自启都走这里。 */
    public static void setEnabled(Context c, boolean on) {
        try {
            c.getSharedPreferences(PREF, Context.MODE_PRIVATE).edit().putBoolean(KEY, on).apply();
        } catch (Exception e) { }
        Intent it = new Intent(c, KeepAliveService.class);
        if (on) {
            try {
                if (Build.VERSION.SDK_INT >= 26) c.startForegroundService(it);
                else c.startService(it);
            } catch (Exception e) { }
        } else {
            try { c.stopService(it); } catch (Exception e) { }
        }
    }

    @Override
    public IBinder onBind(Intent i) { return null; }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        // 开机自启也会走到这里，所以再确认一次开关 —— 关着就不要再赖着。
        if (!enabled(this)) { stopSelf(); return START_NOT_STICKY; }
        try {
            startForeground(NOTI_ID, buildNotification());
        } catch (Exception e) { stopSelf(); return START_NOT_STICKY; }
        // START_STICKY：被系统回收之后自动拉回来。
        return START_STICKY;
    }

    Notification buildNotification() {
        NotificationManager nm = (NotificationManager) getSystemService(NOTIFICATION_SERVICE);
        if (Build.VERSION.SDK_INT >= 26 && nm != null) {
            NotificationChannel ch = new NotificationChannel(
                    CHANNEL, "常驻运行", NotificationManager.IMPORTANCE_MIN);
            ch.setDescription("让喵化助手一直待命，不至于被系统清掉");
            ch.setShowBadge(false);
            try { nm.createNotificationChannel(ch); } catch (Exception e) { }
        }

        Intent open = new Intent(this, MainActivity.class);
        open.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP);
        int fl = PendingIntent.FLAG_UPDATE_CURRENT;
        if (Build.VERSION.SDK_INT >= 23) fl |= PendingIntent.FLAG_IMMUTABLE;
        PendingIntent pi = PendingIntent.getActivity(this, 0, open, fl);

        Notification.Builder b = (Build.VERSION.SDK_INT >= 26)
                ? new Notification.Builder(this, CHANNEL)
                : new Notification.Builder(this);
        b.setSmallIcon(android.R.drawable.ic_menu_edit)
         .setContentTitle("喵化助手待命中")
         .setContentText("点一下打开。不想要这个通知就在「设置」里关掉常驻。")
         .setContentIntent(pi)
         .setOngoing(true)
         .setShowWhen(false);
        if (Build.VERSION.SDK_INT >= 16) return b.build();
        return b.getNotification();
    }
}