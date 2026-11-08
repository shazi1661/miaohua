package com.miaohua.helper;

import android.content.Context;
import android.content.SharedPreferences;

/// 喵化统计。对照电脑版的 Stats 类写的。
///
/// ★ 规则必须和电脑版【一模一样】，不然两边显示同一个账号却给出不同等级：
///     每 40 次升一级      Level = 1 + Total / 40     （电脑版 Stats.PerLevel = 40）
///     换一天就把今日清零   电脑版 Bump() 里的 if (Day != today) { Day = today; Today = 0; }
///
/// 存储方式不一样（电脑版写 stats.txt，安卓写 SharedPreferences）——
/// 这个差别没关系，两边各自记各自的；要紧的是【算法】和【键名】对齐。
///
/// 电脑版还额外统计了每种颜文字用了多少次（用来显示"最常用"），
/// 首页这一项不需要，所以没搬。
public class Stats {

    static final String PREF = "miaohua_stats";

    /// ★ 和电脑版 Stats.PerLevel 保持一致。改这里必须两边一起改。
    public static final int PER_LEVEL = 40;

    /** ★ 2026-10-18：最近喵化的历史 ✧ 只在内存 ✗ 不落盘 ✓
     *  和电脑版一致：最多 20 条，每条｛原文, 结果ｽ */
    public static final java.util.List<String[]> recent = new java.util.ArrayList<String[]>();

    /** ★ 连续喵化多少次（猫猫冷却彩蛋用）✓ */
    public static int streak = 0;

    /** ★ 记一条历史 + 累计连击 ✓ */
    public static void addRecent(String src, String out) {
        try {
            recent.add(new String[] { src, out });
            while (recent.size() > 20) recent.remove(0);
            streak++;
        } catch (Throwable t) { }
    }

    public String day = "";
    public int today = 0;
    public int total = 0;

    public int level() { return 1 + total / PER_LEVEL; }
    public int levelFloor() { return (level() - 1) * PER_LEVEL; }
    public int levelCeil() { return level() * PER_LEVEL; }

    /// 0..1，当前等级内的进度。电脑版的 LevelProgress 是同一个算法。
    public float levelProgress() {
        int span = levelCeil() - levelFloor();
        if (span <= 0) return 0f;
        return (float) (total - levelFloor()) / span;
    }

    /// 距离下一级还差几次。
    public int toNextLevel() { return levelCeil() - total; }

    static SharedPreferences prefs(Context ctx) {
        return ctx.getSharedPreferences(PREF, Context.MODE_PRIVATE);
    }

    public static Stats load(Context ctx) {
        Stats s = new Stats();
        SharedPreferences sp = prefs(ctx);
        s.day = sp.getString("day", "");
        s.today = sp.getInt("today", 0);
        s.total = sp.getInt("total", 0);
        return s;
    }

    /// 记一次转化。
    ///
    /// 和电脑版 Bump() 一样：先看是不是换了天，换了就把今日清零。
    /// 累计数（total）永远不清。
    public void bump(Context ctx) {
        String d = todayString();
        if (!d.equals(day)) {
            day = d;
            today = 0;
        }
        today++;
        total++;
        save(ctx);
    }

    /// 只读地过一遍"今天"——界面刷新时用，不改变计数。
    /// 如果发现换天了，返回的是清零之后的视图（但不落盘，落盘留给下一次 bump）。
    public void rolloverIfNewDay() {
        String d = todayString();
        if (!d.equals(day)) {
            day = d;
            today = 0;
        }
    }

    public void save(Context ctx) {
        prefs(ctx).edit()
                .putString("day", day)
                .putInt("today", today)
                .putInt("total", total)
                .apply();
    }

    /// 和电脑版用同一个日期格式（yyyy-MM-dd），这样"换天"的判定时刻一致。
    static String todayString() {
        return new java.text.SimpleDateFormat("yyyy-MM-dd", java.util.Locale.US)
                .format(new java.util.Date());
    }
}
