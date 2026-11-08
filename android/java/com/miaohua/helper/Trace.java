package com.miaohua.helper;

import java.io.File;
import java.io.FileOutputStream;
import java.util.ArrayList;

/**
 * A tiny record of what the service actually did, shown in the app AND written to a file.
 *
 * Added after two rounds of guessing wrong about a duplicated-text report. The engine was
 * tested against every shape that could be imagined and behaved correctly in all of them, so
 * the fault is somewhere in the service <-> editor conversation - which the engine tests
 * cannot reach. This records the exact read and write strings so the next report says what
 * happened instead of what might have happened.
 *
 * 同时落一份到应用自己的 files 目录。
 *
 * 原来这里写着"写文件需要存储权限，所以只存内存" —— 那句话是错的：应用写【自己的私有
 * 目录】(getFilesDir) 不需要任何权限。只存内存的代价很大：重启应用记录就没了，而排查
 * 问题时最想看的那一段往往正好在重启之前。落盘之后用 adb 也能直接读出来，不用再让人
 * 一边操作一边截图。
 */
public class Trace {

    static final int MAX = 400;
    static final ArrayList<String> LINES = new ArrayList<String>();
    /** 落在应用私有目录里的那份；由服务或界面启动时设一次。 */
    static File out = null;

    /** 由服务或界面在启动时调一次，传 getFilesDir()。 */
    public static void init(File dir) {
        if (dir == null) return;
        try {
            if (!dir.exists()) dir.mkdirs();
            out = new File(dir, "trace.txt");
        } catch (Exception e) { }
    }

    /// ★ 降噪开关 ★
    /// 平时 true：只留关键信息（服务启动、找到输入框、写回结果、失败原因）。
    /// 排查问题时改成 false，所有中间步骤都会回来。
    public static boolean QUIET = true;

    /// 判断一行是不是"调试期的中间步骤"。
    /// 这些行在排查时有用，正常使用时只会把关键信息冲掉。
    static boolean isNoisy(String s) {
        if (s == null) return false;
        if (s.contains("继续走别的路")) return true;
        if (s.contains("换别的路试")) return true;
        if (s.contains("事件没带节点")) return true;
        if (s.contains("窗口清单变成")) return true;
        if (s.contains("次重试（最多")) return true;
        if (s.contains("的根节点重试")) return true;
        if (s.contains("活动窗口的结构")) return true;
        if (s.contains("活动窗口: 根节点=")) return true;
        if (s.contains("窗口数量：")) return true;
        if (s.contains("按文本搜")) return true;
        if (s.contains("返回 null")) return true;
        if (s.contains("都试过了")) return true;
        if (s.contains("窗口 ") && s.contains("的树")) return true;
        if (s.contains("事件瞬间也没抓到输入框")) return true;
        if (s.contains("事件带来了节点")) return true;
        if (s.contains("事件（无节点）")) return true;
        if (s.contains("getRootInActiveWindow() 返回 null")) return true;
        if (s.contains("prefetch")) return true;
        if (s.contains("→ 窗口 ")) return true;
        if (s.contains("打结构时出错")) return true;
        if (s.contains("打各窗口结构时出错")) return true;
        if (s.contains("列窗口时出错")) return true;
        return false;
    }


    public static void add(String s) {
        if (QUIET && isNoisy(s)) return;
        synchronized (LINES) {
            LINES.add(s);
            while (LINES.size() > MAX) LINES.remove(0);
            flush();
        }
    }

    static void flush() {
        if (out == null) return;
        try {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < LINES.size(); i++) sb.append(LINES.get(i)).append("\n");
            FileOutputStream fo = new FileOutputStream(out, false);
            fo.write(sb.toString().getBytes("UTF-8"));
            fo.close();
        } catch (Exception e) { }
    }

    public static String dump() {
        synchronized (LINES) {
            if (LINES.isEmpty()) return "(还没有记录。去别的应用里打几个字，再回来看。)";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < LINES.size(); i++) sb.append(LINES.get(i)).append("\n");
            return sb.toString();
        }
    }

    public static void clear() {
        synchronized (LINES) {
            LINES.clear();
            flush();
        }
    }

    public static String now() {
        java.text.SimpleDateFormat f = new java.text.SimpleDateFormat("MM-dd HH:mm:ss");
        return f.format(new java.util.Date());
    }

    /** Shortens a string for the log without hiding its shape: quotes kept, middle elided. */
    public static String brief(String s) {
        if (s == null) return "null";
        s = s.replace("\n", "\u21B5");
        if (s.length() <= 60) return s;
        return s.substring(0, 30) + "\u2026(" + s.length() + ")" + s.substring(s.length() - 20);
    }
}