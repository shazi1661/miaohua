package com.miaohua.helper;

import android.content.Context;
import android.content.SharedPreferences;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.List;
import java.util.Random;

/**
 * Rule set + text transform engine.
 * Same file format as the desktop version, and the same idempotency rules.
 */
public class Rules {

    /** 一个条件 ✧ 完全本地判断 ✧ 不联网不花钱 ✓ */
    public static class Cond {
        public String kind = "";   // contains/notcontains/prev/next/start/end/whole/len/mood
        public String arg = "";
        public int num = 0;
    }

    public static class Pair {
        public final String from;
        public final String to;
        public final java.util.List<Cond> conds;   // null = 无条件（老写法 ✓）
        public Pair(String f, String t) { from = f; to = t; conds = null; }
        public Pair(String f, String t, java.util.List<Cond> c) { from = f; to = t; conds = c; }
    }

    static final char FULL_STOP = '\u3002';   // ideographic full stop
    static final char EXCL      = '\uFF01';   // fullwidth exclamation
    static final char QUEST     = '\uFF1F';   // fullwidth question
    static final char SEMI      = '\uFF1B';   // fullwidth semicolon
    static final char ELLIPSIS  = '\u2026';   // horizontal ellipsis
    static final String MAO     = "\u55B5";   // the cat sound
    static final String TILDE   = "\uFF5E";   // fullwidth tilde

    public final List<Pair> rules = new ArrayList<Pair>();
    public final List<String> kaomoji = new ArrayList<String>();

    /** ★ 2026-10-18：收藏的颜文字 ✧ 非空时只从这里抽 ✓ */
    public final List<String> favKao = new ArrayList<String>();

    /** \u2605 2026-10-18\uff1a\u989c\u6587\u5b57\u51b7\u5374 \u2727 \u6700\u8fd1 N \u6b21\u7528\u8fc7\u7684\u4e0d\u518d\u62bd \u2717 0 = \u4e0d\u9650\u5236 \u2713 */
    public int kaoCool = 10;

    /** \u6700\u8fd1\u7528\u8fc7\u7684\u989c\u6587\u5b57\uff08\u73af\u5f62\u7f13\u51b2\uff09\u2713 */
    public final List<String> kaoRecent = new ArrayList<String>();

    /** \u2605 2026-10-18\uff1a\u5305\u540d \u2192 \u8bed\u6c14\u540d \u2727 \u6ca1\u5199\u7684\u5305\u7528\u5168\u5c40\u98ce\u683c \u2713 */
    public final java.util.Map<String, String> pkgStyles = new java.util.HashMap<String, String>();

    /** ★ 2026-10-18：解析时的原始文本 ✧ 按包名切风格要用 ✓ */
    public String sourceText = "";

    /** \u2605 2026-10-18\uff1a\u81ea\u5b9a\u4e49\u8bed\u6c14 \u2727 \u6bcf\u4e2a\u5143\u7d20\u662f
     *   { \u540d\u5b57, \u540e\u7F00, \u989C\u6587\u5B57(\u9017\u53F7\u5206), \u5F00\u5934(\u9017\u53F7\u5206), \u7ED3\u5C3E(\u9017\u53F7\u5206) } \u2713 */
    public final List<String[]> customTones = new ArrayList<String[]>();

    /** \u2605 2026-10-18\uff1a\u81ea\u5b9a\u4e49\u5916\u89c2 \u2727 \u6bcf\u4e2a\u5143\u7d20\u662f { \u540d\u5b57, \u540e\u7F00, \u989C\u6587\u5B57 } \u2713 */
    public final List<String[]> customLooks = new ArrayList<String[]>();
    public final List<String> skipEnds = new ArrayList<String>();
    public final List<String> packages = new ArrayList<String>();

    public String suffix = "";
    public String template = "{kaomoji}{suffix}";
    public boolean eachSentence = true;
    public boolean autoMode = true;
    public int debounceMs = 700;

    /** ★ 2026-10-18：智能延迟 ✧ 短句快转 ✓ */
    public boolean smartDelay = true;

    /** \u2605 2026-10-18\uff1a\u6211\u7684\u7ec4\u5408 \u2727 \u6bcf\u4e2a\u5143\u7d20\u662f \u300c\u540d\u5b57|\u98ce\u683c\u540d\u300d \u2713 */
    public final java.util.List<String[]> combos = new java.util.ArrayList<String[]>();

    /** ★ 2026-10-18：按包名关自动 ✧ 包名 → 要不要自动 ✓ */
    public final java.util.Map<String, Boolean> pkgAuto = new java.util.HashMap<String, Boolean>();

    /** \u2605 \u5c0f\u6a21\u578b\u8bed\u6c14\uff08\u84b8\u998f\u51fa\u6765\u7684 7MB \u5206\u7c7b\u5668 \u2727 \u9ed8\u8ba4\u5173 \u2713\uff09
     *
     * \u5f00\u4e86\u4ee5\u540e\uff1a\u5173\u952e\u8bcd\u5224\u4e0d\u51fa\u6765\uff08plain\uff09\u65f6 \u2727 \u95ee\u4e00\u4e0b\u5c0f\u6a21\u578b \u2713
     * \u2014\u2014 \u5173\u952e\u8bcd\u80fd\u5224\u7684\u4e00\u5f8b\u4e0d\u95ee \u2727 \u6240\u4ee5\u6700\u574f\u5c31\u662f\u56de\u5230\u73b0\u5728\u7684\u884c\u4e3a \u2713
     */
    public boolean tinyMood = false;

    /** \u2605 \u60ac\u6d6e\u7a97\uff08\u624b\u52a8\u8f6c\u6362\u6a21\u5f0f \u2727 \u9ed8\u8ba4\u5173 \u2713\uff09
     *
     * \u5f00\u4e86\u4ee5\u540e\uff1a\u4e0d\u81ea\u52a8\u8f6c\u6362 \u2727 \u6539\u6210\u8f93\u5165\u6846\u6709\u5b57\u65f6\u5192\u4e00\u4e2a\u5c0f\u7403 \u2713
     * \u70b9\u5c0f\u7403\u770b\u9884\u89c8 \u2727 \u518d\u70b9\u3010\u8f6c\u6362\u3011\u624d\u5199\u8fdb\u53bb \u2713
     * \u2014\u2014 \u2605 \u548c\u7535\u8111\u7248\u7684\u70ed\u952e\u8f6c\u6362\u5bf9\u5e94 \u2713
     */
    public boolean floatBar = false;

    /** \u804a\u5929\u4e0a\u4e0b\u6587 \u2727 \u7531 Service \u8bfb\u597d\u4e86\u653e\u8fdb\u6765\uff08\u8bfb\u4e0d\u5230\u5c31\u662f\u7a7a\u4e32 \u2713\uff09*/
    public static String chatCtx = "";

    // 语气 styles wrap the whole message. There is no language understanding here: the
    // sentence TYPE picks which pool, and one variant is picked at random inside it.
    public List<String> openers = new ArrayList<String>();
    public List<String> closers = new ArrayList<String>();
    public List<String> questionClosers = new ArrayList<String>();
    public List<String> exclaimClosers = new ArrayList<String>();
    public List<String> happyClosers = new ArrayList<String>();
    public List<String> sadClosers = new ArrayList<String>();
    public List<String> angryClosers = new ArrayList<String>();
    // 打招呼 / 分别 / 喜爱 / 夸奖 - checked before the emotional moods because they overlap:
    // 你好棒 contains 你好, and 我喜欢你 contains 喜欢 (also a happy word).
    public List<String> greetingClosers = new ArrayList<String>();
    public List<String> farewellClosers = new ArrayList<String>();
    public List<String> loveClosers = new ArrayList<String>();
    public List<String> praiseClosers = new ArrayList<String>();
    // Second round, mapped from the DUT emotion ontology (乐/好/怒/哀/惧/恶/惊, 21
    // subcategories) cut down to the ones that need a DIFFERENT reply.
    public List<String> thanksClosers = new ArrayList<String>();
    public List<String> apologyClosers = new ArrayList<String>();
    public List<String> congratClosers = new ArrayList<String>();
    public List<String> inviteClosers = new ArrayList<String>();
    public List<String> surpriseClosers = new ArrayList<String>();
    public List<String> fearClosers = new ArrayList<String>();
    public List<String> tiredClosers = new ArrayList<String>();
    public List<String> boredClosers = new ArrayList<String>();
    public List<String> disappointedClosers = new ArrayList<String>();
    // 安慰 = the USER is comforting the OTHER person ("别难过了"), as opposed to 难过 =
    // the user is sad themselves ("我好难过"). They used to be one pool, and every line in
    // it was written from the comforting side - typing 不开心 produced "过来，给你靠一下",
    // i.e. the user offering a shoulder while being the sad one.
    public List<String> comfortClosers = new ArrayList<String>();
    // 撒娇 = the user is ASKING for affection ("抱抱我"), not declaring it ("我喜欢你").
    public List<String> charmClosers = new ArrayList<String>();
    /** false = 只加末尾: the word rules and the end decoration still run, but nothing is
     *  wrapped around the sentence. */
    public boolean tone = true;

    /** ★ 2026-10-31：悬浮窗显示时机 ✧ qq = 只在生效应用里 / always = 一直显示 ✓ */
    public String floatMode = "qq";

    /** ★ 2026-10-31：球旁边显示转换结果 ✓ */
    public boolean resultTail = true;

    /** ★ 2026-11-01：当前风格名 ✗ 给大模型写开头结尾当参考 ✓ */
    public String styleName = "";

    /** ★ 2026-11-01：上一次判出的情绪 ✗ 大模型写包装时要用 ✓ */
    public static String lastMood = "";

    /** ★ 2026-10-31：大模型改写 ✗ off / manual / auto / wrap ✓ */
    public String rewriteMode = "off";
    public String rewriteEndpoint = "";
    public String rewriteModel = "";
    public String rewriteKey = "";
    public int rewriteTimeoutMs = 5000;
    // 重度: break after commas too
    public boolean commaDecorate = false;

    private static Random RND = new Random();

    /** \u2605 2026-10-18\uff1a\u8bed\u6c14\u7a33\u5b9a \u2727 \u540c\u4e00\u53e5\u8bdd\u7ed3\u679c\u56fa\u5b9a \u2713 */
    public boolean stableMood = true;

    /** \u6309\u300c\u65e5\u671f + \u53e5\u5b50\u300d\u91cd\u7f6e\u79cd\u5b50 \u2727 salt \u975e 0 \u53ef\u4ee5\u300c\u6362\u4e00\u4e2a\u300d\u2713 */
    static int retrySalt = 0;

    void reseed(String text) {
        if (!stableMood) { RND = new Random(); return; }
        java.util.Calendar c = java.util.Calendar.getInstance();
        int day = c.get(java.util.Calendar.YEAR) * 1000 + c.get(java.util.Calendar.DAY_OF_YEAR);
        int h = 0;
        if (text != null) for (int i = 0; i < text.length(); i++) h = h * 31 + text.charAt(i);
        RND = new Random(day ^ h ^ (retrySalt * 7919));
    }

    /** \u2605 \u63a5\u4e0b\u6765\u8981\u6362\u4e00\u4e2a\u7ed3\u679c \u2713 */
    public static void bumpSalt() { retrySalt = (retrySalt + 1) % 1000; }
    public static void resetSalt() { retrySalt = 0; }

    /** ★ 2026-10-23：直接指定种子序号 ✗ 「换一个」用递增序号 ✓ */
    public static void setSalt(int v) { retrySalt = ((v % 1000) + 1000) % 1000; }

    public static final String PREFS = "miaohua";
    public static final String KEY_RULES = "rules_text";
    public static final String KEY_REV = "rules_rev";

    // ---------------------------------------------------------- storage

    public static String defaultRulesText(Context ctx) {
        InputStream in = null;
        try {
            in = ctx.getResources().openRawResource(R.raw.default_rules);
            ByteArrayOutputStream bos = new ByteArrayOutputStream();
            byte[] buf = new byte[8192];
            int n;
            while ((n = in.read(buf)) > 0) bos.write(buf, 0, n);
            return new String(bos.toByteArray(), "UTF-8");
        } catch (Exception e) {
            return "";
        } finally {
            try { if (in != null) in.close(); } catch (Exception e) { }
        }
    }

    public static String storedText(Context ctx) {
        SharedPreferences sp = ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        String s = sp.getString(KEY_RULES, null);
        if (s == null || s.length() == 0) s = defaultRulesText(ctx);
        return s;
    }

    public static void saveText(Context ctx, String text) {
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
                .edit().putString(KEY_RULES, text)
                .putLong(KEY_REV, System.currentTimeMillis())
                .apply();
    }

    /// Bumped by every save. SharedPreferences has no modification time, and the accessibility
    /// service needs a cheap way to notice that the 风格 page changed the rules - otherwise it
    /// keeps the copy it parsed at startup and a style change appears to do nothing.
    public static long revision(Context ctx) {
        return ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getLong(KEY_REV, 0L);
    }

    public static Rules load(Context ctx) {
        return parse(storedText(ctx));
    }

    // ---------------------------------------------------------- parsing

    public static Rules parse(String text) {
        Rules r = new Rules();
        r.sourceText = text == null ? "" : text;
        // ★ 2026-10-21：不再默认跳过任何结尾 ✗ 和电脑版一致 ✓
        //   句尾碰巧带 "~" 或 "喵" 就不转换 ✗ 用户报过 ✓
        //   真正重复装饰的判断在下面那几个函数里 ✓
        if (text == null) return r;

        String[] lines = text.split("\r\n|\n|\r");
        for (int i = 0; i < lines.length; i++) {
            String line = lines[i].trim();
            if (line.length() == 0) continue;
            if (line.startsWith("#")) continue;
            if (line.startsWith("//")) continue;

            // A word rule ("再见 => 拜拜") also contains an '='. It MUST be recognised before
            // the settings are parsed: the left side is looked up as a setting name, and
            // 再见 is an alias for the 分别结尾 pool - so that line used to append the
            // literal "> 拜拜" to the pool instead of defining a replacement.
            // ★ 条件替换（满足条件才换 ✧ 完全本地 ✓）
            if (line.startsWith("如果")) {
                Pair cr = parseCondRule(line);
                if (cr != null && cr.from.length() > 0) r.rules.add(cr);
                continue;
            }

            int arrowPos = line.indexOf("=>");
            if (arrowPos > 0) {
                String f = line.substring(0, arrowPos).trim();
                String t = line.substring(arrowPos + 2).trim();
                if (f.length() > 0) r.rules.add(new Pair(f, t));
                continue;
            }

            int eq = line.indexOf('=');
            String key = eq > 0 ? canonicalKey(line.substring(0, eq)) : "";
            String val = eq > 0 ? line.substring(eq + 1).trim() : "";

            if (key.equals("kaomoji")) { r.kaomoji.add(val); continue; }
        if (key.equals("favkao")) { if (!r.favKao.contains(val)) r.favKao.add(val); continue; }
        if (key.equals("stablemood") || key.equals("\u8BED\u6C14\u7A33\u5B9A")) {
            r.stableMood = (val.trim().equals("true") || val.trim().equals("1") || val.trim().equals("\u5F00"));
            continue;
        }
        if (key.equals("combo") || key.equals("\u6211\u7684\u7EC4\u5408")) {
            int eq4 = val.indexOf('|');
            if (eq4 > 0) {
                String nm = val.substring(0, eq4).trim();
                String st = val.substring(eq4 + 1).trim();
                if (nm.length() > 0 && st.length() > 0) r.combos.add(new String[] { nm, st });
            }
            continue;
        }
        if (key.equals("smartdelay") || key.equals("\u667A\u80FD\u5EF6\u8FDF")) {
            r.smartDelay = (val.trim().equals("true") || val.trim().equals("1") || val.trim().equals("\u5F00"));
            continue;
        }
        if (key.equals("pkgauto") || key.equals("\u5305\u540D\u81EA\u52A8")) {
            int eq3 = val.indexOf('=');
            if (eq3 > 0) {
                String pk = val.substring(0, eq3).trim();
                String vv = val.substring(eq3 + 1).trim();
                if (pk.length() > 0) r.pkgAuto.put(pk, Boolean.valueOf(vv.equals("true") || vv.equals("1") || vv.equals("\u5F00")));
            }
            continue;
        }
        if (key.equals("kaocool") || key.equals("\u989C\u6587\u5B57\u51B7\u5374")) {
            try { int kv = Integer.parseInt(val.trim()); if (kv >= 0 && kv <= 90) r.kaoCool = kv; } catch (Exception e) { }
            continue;
        }
        if (key.equals("lookstyle")) {
            String[] parts = val.split("\\|", -1);
            if (parts.length >= 3 && parts[0].trim().length() > 0) {
                String[] row = new String[3];
                row[0] = parts[0].trim();
                row[1] = parts[1].length() > 0 ? parts[1] : "\u55B5~";
                row[2] = parts[2];
                r.customLooks.add(row);
            }
            continue;
        }
        if (key.equals("tonestyle")) {
            String[] parts = val.split("\\|", -1);
            if (parts.length >= 3 && parts[0].trim().length() > 0) {
                String[] row = new String[5];
                row[0] = parts[0].trim();
                row[1] = parts.length > 1 ? parts[1] : "\u55B5~";
                row[2] = parts.length > 2 ? parts[2] : "";
                row[3] = parts.length > 3 ? parts[3] : "";
                row[4] = parts.length > 4 ? parts[4] : "";
                r.customTones.add(row);
            }
            continue;
        }
        if (key.equals("pkgstyle")) {
            int eq1 = val.indexOf('=');
            if (eq1 > 0) {
                String pn = val.substring(0, eq1).trim();
                String sn = val.substring(eq1 + 1).trim();
                if (pn.length() > 0 && sn.length() > 0) r.pkgStyles.put(pn, sn);
            }
            continue;
        }
            if (key.equals("skipend")) { r.skipEnds.add(val); continue; }
        if (key.equals("rewritemode")) { r.rewriteMode = val.trim().toLowerCase(); continue; }
        if (key.equals("rewriteendpoint")) { r.rewriteEndpoint = val.trim(); continue; }
        if (key.equals("rewritemodel")) { r.rewriteModel = val.trim(); continue; }
        if (key.equals("rewritekey")) { r.rewriteKey = val.trim(); continue; }
        if (key.equals("rewritetimeout")) {
            try { int v = Integer.parseInt(val.trim()); if (v >= 300) r.rewriteTimeoutMs = v; } catch (Exception e) { }
            continue;
        }
        if (key.equals("floatmode") || key.equals("\u60AC\u6D6E\u7A97\u6A21\u5F0F")) {
            r.floatMode = val.trim().equals("always") ? "always" : "qq";
            continue;
        }
        if (key.equals("tail") || key.equals("\u7ED3\u679C\u5C3E\u5DF4")) {
            r.resultTail = (val.trim().equals("true") || val.trim().equals("1") || val.trim().equals("\u5F00"));
            continue;
        }
            if (key.equals("package") || key.equals("whitelist")) { r.packages.add(val); continue; }
            if (key.equals("suffix")) { r.suffix = val; continue; }
            if (key.equals("template")) { r.template = val; continue; }
            if (key.equals("eachsentence")) { r.eachSentence = parseBool(val); continue; }
            if (key.equals("opener")) { r.openers.add(val); continue; }
            if (key.equals("closer")) { r.closers.add(val); continue; }
            if (key.equals("questioncloser")) { r.questionClosers.add(val); continue; }
            if (key.equals("exclaimcloser")) { r.exclaimClosers.add(val); continue; }
            if (key.equals("happycloser")) { r.happyClosers.add(val); continue; }
            if (key.equals("sadcloser")) { r.sadClosers.add(val); continue; }
            if (key.equals("angrycloser")) { r.angryClosers.add(val); continue; }
            if (key.equals("greetingcloser")) { r.greetingClosers.add(val); continue; }
            if (key.equals("farewellcloser")) { r.farewellClosers.add(val); continue; }
            if (key.equals("lovecloser")) { r.loveClosers.add(val); continue; }
            if (key.equals("praisecloser")) { r.praiseClosers.add(val); continue; }
            if (key.equals("thankscloser")) { r.thanksClosers.add(val); continue; }
            if (key.equals("apologycloser")) { r.apologyClosers.add(val); continue; }
            if (key.equals("congratcloser")) { r.congratClosers.add(val); continue; }
            if (key.equals("invitecloser")) { r.inviteClosers.add(val); continue; }
            if (key.equals("surprisecloser")) { r.surpriseClosers.add(val); continue; }
            if (key.equals("fearcloser")) { r.fearClosers.add(val); continue; }
            if (key.equals("tiredcloser")) { r.tiredClosers.add(val); continue; }
            if (key.equals("boredcloser")) { r.boredClosers.add(val); continue; }
            if (key.equals("disappointedcloser")) { r.disappointedClosers.add(val); continue; }
            if (key.equals("comfortcloser")) { r.comfortClosers.add(val); continue; }
            if (key.equals("charmcloser")) { r.charmClosers.add(val); continue; }
            if (key.equals("tone")) { r.tone = parseBool(val); continue; }
            if (key.equals("commadecorate")) { r.commaDecorate = parseBool(val); continue; }
            if (key.equals("level")) {
                // 轻度 = decorate once at the very end, 普通 = per sentence,
                // 重度 = per sentence and after every comma too
                String v = val.toLowerCase();
                if (v.contains("\u8F7B") || v.contains("light")) { r.eachSentence = false; r.commaDecorate = false; }
                else if (v.contains("\u91CD") || v.contains("heavy")) { r.eachSentence = true; r.commaDecorate = true; }
                else { r.eachSentence = true; r.commaDecorate = false; }
                continue;
            }
            if (key.equals("automode")) { r.autoMode = parseBool(val); continue; }
            if (key.equals("tinymood")) { r.tinyMood = parseBool(val); continue; }
            if (key.equals("floatbar")) { r.floatBar = parseBool(val); continue; }
            if (key.equals("debounce")) {
                try {
                    int v = Integer.parseInt(val);
                    if (v >= 100) r.debounceMs = v;
                } catch (Exception e) { }
                continue;
            }
        }
        return r;
    }

    /**
     * Setting names accept English or Chinese, with or without separators, so
     * "hotkey_transform", "HotkeyTransform" and "热键转换" all land on the same
     * setting. Anything unrecognised returns "" and the line is then tried as a
     * "from => to" replacement rule instead.
     */
    static String canonicalKey(String raw) {
        if (raw == null) return "";
        String k = raw.trim().toLowerCase()
                .replace("_", "").replace("-", "").replace(" ", "").replace(".", "");

        if (k.equals("lookstyle") || k.equals("\u81EA\u5B9A\u4E49\u5916\u89C2"))
            return "lookstyle";
        if (k.equals("tonestyle") || k.equals("\u81EA\u5B9A\u4E49\u8BED\u6C14") || k.equals("\u81EA\u5B9A\u4E49\u98CE\u683C"))
            return "tonestyle";
        if (k.equals("pkgstyle") || k.equals("\u5305\u540D\u98CE\u683C") || k.equals("\u8FDB\u7A0B\u98CE\u683C"))
            return "pkgstyle";
        if (k.equals("stablemood") || k.equals("\u8BED\u6C14\u7A33\u5B9A"))
            return "stablemood";
        if (k.equals("combo") || k.equals("\u6211\u7684\u7EC4\u5408") || k.equals("\u7EC4\u5408"))
            return "combo";
        if (k.equals("smartdelay") || k.equals("\u667A\u80FD\u5EF6\u8FDF"))
            return "smartdelay";
        if (k.equals("pkgauto") || k.equals("\u5305\u540D\u81EA\u52A8"))
            return "pkgauto";
        if (k.equals("kaocool") || k.equals("\u989C\u6587\u5B57\u51B7\u5374") || k.equals("\u51B7\u5374"))
            return "kaocool";
        if (k.equals("favkao") || k.equals("\u6536\u85CF\u989C\u6587\u5B57") || k.equals("\u6536\u85CF"))
            return "favkao";
        if (k.equals("kaomoji") || k.equals("\u989C\u6587\u5B57") || k.equals("\u8868\u60C5"))
            return "kaomoji";
        if (k.equals("suffix") || k.equals("\u540E\u7F00") || k.equals("\u5C3E\u5DF4"))
            return "suffix";
        if (k.equals("template") || k.equals("\u6A21\u677F"))
            return "template";
        if (k.equals("rewritemode") || k.equals("\u6539\u5199\u6A21\u5F0F")) return "rewritemode";
        if (k.equals("rewriteendpoint") || k.equals("\u6539\u5199\u5730\u5740")) return "rewriteendpoint";
        if (k.equals("rewritemodel") || k.equals("\u6539\u5199\u6A21\u578B")) return "rewritemodel";
        if (k.equals("rewritekey") || k.equals("\u6539\u5199\u5BC6\u94A5")) return "rewritekey";
        if (k.equals("rewritetimeout") || k.equals("\u6539\u5199\u8D85\u65F6")) return "rewritetimeout";
        if (k.equals("floatmode") || k.equals("\u60AC\u6D6E\u7A97\u6A21\u5F0F") || k.equals("\u663E\u793A\u65F6\u673A"))
            return "floatmode";
        if (k.equals("tail") || k.equals("\u7ED3\u679C\u5C3E\u5DF4") || k.equals("\u5C0F\u5C3E\u5DF4"))
            return "tail";
        if (k.equals("skipend") || k.equals("\u8DF3\u8FC7") || k.equals("\u8DF3\u8FC7\u7ED3\u5C3E")
                || k.equals("\u7ED3\u5C3E\u8DF3\u8FC7"))
            return "skipend";
        if (k.equals("eachsentence") || k.equals("\u6BCF\u53E5") || k.equals("\u9010\u53E5"))
            return "eachsentence";
        if (k.equals("opener") || k.equals("\u5F00\u5934") || k.equals("\u5F00\u573A"))
            return "opener";
        if (k.equals("closer") || k.equals("\u7ED3\u5C3E") || k.equals("\u6536\u5C3E"))
            return "closer";
        if (k.equals("questioncloser") || k.equals("\u95EE\u53E5\u7ED3\u5C3E")
                || k.equals("\u7591\u95EE\u7ED3\u5C3E") || k.equals("\u95EE\u53F7\u7ED3\u5C3E"))
            return "questioncloser";
        if (k.equals("exclaimcloser") || k.equals("\u53F9\u53E5\u7ED3\u5C3E")
                || k.equals("\u611F\u53F9\u7ED3\u5C3E") || k.equals("\u611F\u53F9\u53E5\u7ED3\u5C3E"))
            return "exclaimcloser";
        if (k.equals("happycloser") || k.equals("\u5F00\u5FC3\u7ED3\u5C3E")
                || k.equals("\u9AD8\u5174\u7ED3\u5C3E") || k.equals("\u5F00\u5FC3"))
            return "happycloser";
        if (k.equals("sadcloser") || k.equals("\u96BE\u8FC7\u7ED3\u5C3E")
                || k.equals("\u4F24\u5FC3\u7ED3\u5C3E") || k.equals("\u96BE\u8FC7"))
            return "sadcloser";
        if (k.equals("angrycloser") || k.equals("\u751F\u6C14\u7ED3\u5C3E")
                || k.equals("\u751F\u6C14") || k.equals("\u70E6\u8E81\u7ED3\u5C3E"))
            return "angrycloser";
        // NOTE: every alias has to include the exact spelling the writer uses. An earlier
        // version had \u544A\u522B\u7ED3\u5C3E (告别结尾) but not \u5206\u522B\u7ED3\u5C3E
        // (分别结尾), so that pool silently stayed empty and fell back to the plain one.
        if (k.equals("greetingcloser") || k.equals("\u6253\u62DB\u547C")
                || k.equals("\u95EE\u5019") || k.equals("\u95EE\u597D")
                || k.equals("\u6253\u62DB\u547C\u7ED3\u5C3E"))
            return "greetingcloser";
        if (k.equals("farewellcloser") || k.equals("\u5206\u522B") || k.equals("\u544A\u522B")
                || k.equals("\u518D\u89C1") || k.equals("\u544A\u522B\u7ED3\u5C3E")
                || k.equals("\u5206\u522B\u7ED3\u5C3E"))
            return "farewellcloser";
        if (k.equals("lovecloser") || k.equals("\u559C\u7231") || k.equals("\u559C\u6B22")
                || k.equals("\u8868\u767D") || k.equals("\u559C\u7231\u7ED3\u5C3E"))
            return "lovecloser";
        if (k.equals("praisecloser") || k.equals("\u5938\u5956") || k.equals("\u8868\u626C")
                || k.equals("\u79F0\u8D5E") || k.equals("\u5938\u5956\u7ED3\u5C3E"))
            return "praisecloser";
        if (k.equals("thankscloser") || k.equals("\u611F\u8C22") || k.equals("\u591A\u8C22")
                || k.equals("\u81F4\u8C22") || k.equals("\u611F\u8C22\u7ED3\u5C3E"))
            return "thankscloser";
        if (k.equals("apologycloser") || k.equals("\u9053\u6B49") || k.equals("\u62B1\u6B49")
                || k.equals("\u5BF9\u4E0D\u8D77") || k.equals("\u9053\u6B49\u7ED3\u5C3E"))
            return "apologycloser";
        if (k.equals("congratcloser") || k.equals("\u795D\u8D3A") || k.equals("\u606D\u559C")
                || k.equals("\u606D\u8D3A") || k.equals("\u795D\u8D3A\u7ED3\u5C3E"))
            return "congratcloser";
        if (k.equals("invitecloser") || k.equals("\u9080\u8BF7") || k.equals("\u7EA6")
                || k.equals("\u9080\u8BF7\u7ED3\u5C3E"))
            return "invitecloser";
        if (k.equals("surprisecloser") || k.equals("\u60CA\u8BB6") || k.equals("\u60CA\u5947")
                || k.equals("\u5403\u60CA") || k.equals("\u60CA\u8BB6\u7ED3\u5C3E"))
            return "surprisecloser";
        if (k.equals("fearcloser") || k.equals("\u5BB3\u6015") || k.equals("\u62C5\u5FC3")
                || k.equals("\u6050\u60E7") || k.equals("\u5BB3\u6015\u7ED3\u5C3E"))
            return "fearcloser";
        if (k.equals("tiredcloser") || k.equals("\u75B2\u60EB") || k.equals("\u7D2F")
                || k.equals("\u56F0") || k.equals("\u75B2\u60EB\u7ED3\u5C3E")
                || k.equals("\u7D2F\u4E86\u7ED3\u5C3E"))   // \u2605 \u8865\u4e86\u300c\u7d2f\u4e86\u7ed3\u5c3e\u300d\u2713
            return "tiredcloser";
        if (k.equals("boredcloser") || k.equals("\u65E0\u804A")
                || k.equals("\u65E0\u804A\u7ED3\u5C3E"))
            return "boredcloser";
        if (k.equals("disappointedcloser") || k.equals("\u5931\u671B")
                || k.equals("\u5931\u671B\u7ED3\u5C3E"))
            return "disappointedcloser";
        if (k.equals("comfortcloser") || k.equals("\u5B89\u6170")
                || k.equals("\u5B89\u6170\u7ED3\u5C3E"))
            return "comfortcloser";
        if (k.equals("charmcloser") || k.equals("\u6492\u5A07")
                || k.equals("\u6492\u5A07\u7ED3\u5C3E"))
            return "charmcloser";
        if (k.equals("tone") || k.equals("\u8BED\u6C14") || k.equals("\u52A0\u8BED\u6C14"))
            return "tone";
        if (k.equals("level") || k.equals("\u7A0B\u5EA6") || k.equals("\u5F3A\u5EA6"))
            return "level";
        if (k.equals("commadecorate") || k.equals("\u9017\u53F7\u4E5F\u52A0") || k.equals("\u9017\u53F7"))
            return "commadecorate";
        if (k.equals("automode") || k.equals("\u81EA\u52A8\u6A21\u5F0F") || k.equals("\u81EA\u52A8"))
            return "automode";
        if (k.equals("tinymood") || k.equals("\u5C0F\u6A21\u578B\u8BED\u6C14") || k.equals("\u5C0F\u6A21\u578B"))
            return "tinymood";
        if (k.equals("floatbar") || k.equals("\u60AC\u6D6E\u7A97") || k.equals("\u60AC\u6D6E"))
            return "floatbar";
        if (k.equals("debounce") || k.equals("\u5EF6\u8FDF") || k.equals("\u505C\u624B")
                || k.equals("\u95F4\u9694"))
            return "debounce";
        if (k.equals("whitelist") || k.equals("\u7A0B\u5E8F") || k.equals("\u767D\u540D\u5355")
                || k.equals("\u751F\u6548\u7A0B\u5E8F"))
            return "whitelist";
        if (k.equals("package") || k.equals("\u5305\u540D"))
            return "package";
        return "";
    }

    /** {kaomoji}/{suffix} and {颜文字}/{后缀} are interchangeable in the template */
    static String applyTemplate(String t, String kao, String suffix) {
        if (t == null) return "";
        t = t.replace("{kaomoji}", kao).replace("{\u989C\u6587\u5B57}", kao);
        t = t.replace("{suffix}", suffix).replace("{\u540E\u7F00}", suffix);
        return t;
    }

    /** parseBool for callers that may hold a null (a key that is not in the file yet). */
    public static boolean parseBoolSafe(String s) {
        return s != null && parseBool(s);
    }

    static boolean parseBool(String s) {
        s = s.trim().toLowerCase();
        return s.equals("1") || s.equals("true") || s.equals("yes") || s.equals("on");
    }

    /** \u2605 2026-10-18\uff1a\u6309\u53e5\u5b50\u957f\u5ea6\u7b97\u5ef6\u8fdf \u2727 \u77ed\u53e5\u5feb\u8f6c \u2713 */
    public int effectiveDelay(String text) {
        int base = debounceMs;
        if (!smartDelay || text == null) return base;
        int len = text.length();
        int eff = base;
        if (len <= 4) eff = base * 60 / 100;
        else if (len >= 30) eff = base * 130 / 100;
        else if (len >= 16) eff = base * 105 / 100;
        if (eff < 200) eff = 200;
        if (eff > 2000) eff = 2000;
        return eff;
    }

    /** \u2605 \u8fd9\u4e2a\u5305\u91cc\u8981\u4e0d\u8981\u81ea\u52a8\u8f6c\u6362 \u2713 */
    public boolean isPackageAuto(String pkg) {
        if (pkg == null || pkgAuto.isEmpty()) return true;
        Boolean v = pkgAuto.get(pkg);
        return v == null ? true : v.booleanValue();
    }

    public boolean isPackageAllowed(String pkg) {
        if (pkg == null) return false;
        if (packages.isEmpty()) return true;
        for (int i = 0; i < packages.size(); i++) {
            if (packages.get(i).equalsIgnoreCase(pkg)) return true;
        }
        return false;
    }

    // ---------------------------------------------------------- engine

    /** 重度 also breaks a sentence at commas */
    static boolean isComma(char c) {
        return c == '\uFF0C' || c == ',' || c == '\u3001';
    }

    boolean isBreak(char c) {
        return isTerm(c) || (commaDecorate && isComma(c));
    }

    static boolean isTerm(char c) {        return c == FULL_STOP || c == EXCL || c == '!' || c == QUEST || c == '?'
                || c == SEMI || c == ';' || c == ELLIPSIS;
    }

    // punctuation must never count as "content", so a fragment made only of
    // separators (a lone fullwidth comma, say) does not get a kaomoji bolted on
    static boolean isPunctLike(char c) {
        if (Character.isWhitespace(c)) return true;
        if (isTerm(c)) return true;
        return c == '\uFF0C' || c == ',' || c == '\u3001'
                || c == ':' || c == '\uFF1A'
                || c == '\u201C' || c == '\u201D' || c == '\u2018' || c == '\u2019'
                || c == '\u300A' || c == '\u300B' || c == '\u3008' || c == '\u3009'
                || c == '\u3010' || c == '\u3011'
                || c == '\u2014' || c == '-' || c == '\uFF0D';
    }

    static boolean hasContent(String s) {
        for (int i = 0; i < s.length(); i++)
            if (!isPunctLike(s.charAt(i))) return true;
        return false;
    }

    static boolean endsWithAnyIn(String s, List<String> list) {
        for (int i = 0; i < list.size(); i++) {
            String t = list.get(i);
            if (t == null || t.length() == 0) continue;
            if (s.endsWith(t)) return true;
        }
        return false;
    }

    boolean endsWithSkip(String s) {
        return endsWithAnyIn(s, skipEnds);
    }

    static boolean startsWithAny(String s, int pos, List<String> list) {
        for (int i = 0; i < list.size(); i++) {
            String t = list.get(i);
            if (t.length() == 0) continue;
            if (pos < 0 || pos + t.length() > s.length()) continue;
            if (s.regionMatches(pos, t, 0, t.length())) return true;
        }
        return false;
    }

    String decorate() {
        return decorate(false);
    }

    /**
     * ★ 句尾是【表达情绪的标点】吗（！……？ ✓）
     *
     * 用户要求：
     *   "当用户添加了标点符号以后如果是！，……或？这种表达情绪的
     *    喵后面就不要跟~了，如果是其他的例如。或，可以加上~"
     */
    static boolean isEmotionalPunct(String punct) {
        if (punct == null || punct.length() == 0) return false;
        for (int i = 0; i < punct.length(); i++) {
            char c = punct.charAt(i);
            if (c == '\uFF01' || c == '!'        // ！ !
                    || c == '\uFF1F' || c == '?'     // ？ ?
                    || c == '\u2026'                 // …
                    || c == '\u2757' || c == '\u2755'   // ❗ ❕
                    || c == '\u2753' || c == '\u2754')  // ❓ ❔
                return true;
        }
        return false;
    }

    /**
     * ★★★ 把【装饰后面补的标点】挪到颜文字前面 ★★★
     *
     * 用户要求："后面加的标点符号能不能挪到颜文字前面"
     *   读到  呐，你好喵~(´･ω･`)，嗨嗨！今天也一起吗？
     *   期望  呐，你好喵？(´･ω･`)，你终于来了……我好想你
     * 理由："你好喵？"才是句子结尾 ✧ 颜文字是跟在后面的表情 ✓
     *
     * 返回重拼好的 core（已含标点）✧ 拼不了就返回 null ✓
     */
    String movePunctInsideDecoration(String core, String punct, List<String> decorations) {
        if (core == null || core.length() == 0 || punct == null || punct.length() == 0) return null;
        if (suffix.length() == 0 || kaomoji.isEmpty()) return null;
        for (int i = 0; i < punct.length(); i++) if (!isPunctLike(punct.charAt(i))) return null;

        // ★★★ 通用搜索：在 core 结尾找【后缀 … 颜文字】这个骨架 ★★★
        //
        // 不能只看 decorations 列表 ✧ 那是"枚举形状"✓
        //   枚举的话每加一条规则就多一种形状 ✧ 漏一种就重装 ✓
        String sufBare = stripTrailingTilde(suffix);
        boolean emo = isEmotionalPunct(punct);

        for (int i = 0; i < kaomoji.size(); i++) {
            String k = kaomoji.get(i);
            if (k == null || k.length() == 0) continue;
            if (!core.endsWith(k)) continue;
            String kHead = core.substring(0, core.length() - k.length());

            int p = kHead.lastIndexOf(suffix);
            String sufUse = suffix;
            if (p < 0 && sufBare != null && sufBare.length() > 0 && !sufBare.equals(suffix)) {
                p = kHead.lastIndexOf(sufBare);
                sufUse = sufBare;
            }
            if (p < 0) continue;

            String mid = kHead.substring(p + sufUse.length());
            boolean allPunct = true;
            for (int q = 0; q < mid.length(); q++) if (!isPunctLike(mid.charAt(q))) { allPunct = false; break; }
            if (!allPunct) continue;

            // ★ 已经夹过同样的标点了 ✧ 不重复加 ✓
            if (mid.endsWith(punct)) return core;

            // ★ 非情绪标点（，。）✧ 挪到【整个装饰前面】✓
            //   你好喵~(...) + ，  →  你好，喵~(...) ✓
            if (!emo) return kHead.substring(0, p) + punct + sufUse + mid + k;

            String sufOut = sufBare;
            return kHead.substring(0, p) + sufOut + mid + punct + k;
        }
        return null;
    }

    /**
     * ★★★ 按标点决定装饰怎么摆 ★★★
     *
     * 用户 2026-10-06 明确要的：
     *   ?  !  ……   →  后缀（去掉 ~）+ 标点 + 颜文字
     *                   你好？ → 你好喵？(=≧∇≦=)
     *   没有标点   →  后缀 + 颜文字
     *
     * ★ 用户 2026-10-16 改要求了（✧ 这一条翻过来了 ✓）：
     *   ，  。  ；  ：   →  标点 + 后缀 + 颜文字
     *                   你好， → 你好，喵~(=≧∇≦=)
     *   —— ★ 以前是「你好喵~(=≧∇≦=)，」✧ 看着像打错了 ✗
     *   —— ★ 两种程度（轻度 / 逐句）都照这条来 ✓
     */
    static String placeDecoration(String deco, String kao, String punct, boolean emo, String suffix) {
        if (punct == null || punct.length() == 0) return deco;
        String pre = deco, k = "";
        if (kao != null && kao.length() > 0 && deco.endsWith(kao)) {
            pre = deco.substring(0, deco.length() - kao.length());
            k = kao;
        }
        if (!emo) {
            // ★ 非情绪标点（，。；：）✧ 插到【后缀前面】✓
            //   这个函数有两个调用处 ✧ 一个传纯装饰 ✧ 一个传正文+装饰 ✓
            //   所以不能直接 punct + deco ✧ 得先把后缀找出来 ✓
            String sufBare2 = stripTrailingTilde(suffix);
            int q = (suffix == null) ? -1 : pre.lastIndexOf(suffix);
            if (q < 0 && sufBare2 != null && sufBare2.length() > 0 && !sufBare2.equals(suffix))
                q = pre.lastIndexOf(sufBare2);
            if (q >= 0) return pre.substring(0, q) + punct + pre.substring(q) + k;
            return pre + k + punct;     // 找不到后缀 ✧ 退回老行为 ✓
        }
        return stripTrailingTilde(pre) + punct + k;
    }

    /** ★ 把末尾那一串标点切下来 ✓ 没有就原样返回 ✓ */
    static String splitTrailingPunct(String core) {
        if (core == null || core.length() == 0) return core;
        int t = core.length();
        while (t > 0 && isPunctLike(core.charAt(t - 1))) t--;
        if (t >= core.length() || t == 0) return core;
        return core.substring(0, t);
    }

    /** ★ body 是不是"…后缀 + 标点 + 颜文字"这个形状 ✓（已经挪好了 ✓） */
    boolean endsWithPunctInsideDecoration(String body) {
        if (body == null || body.length() == 0 || kaomoji.isEmpty()) return false;
        String suf = stripTrailingTilde(suffix);
        if (suf == null || suf.length() == 0) return false;

        for (int i = 0; i < kaomoji.size(); i++) {
            String k = kaomoji.get(i);
            if (k == null || k.length() == 0) continue;
            if (!body.endsWith(k)) continue;
            String head = body.substring(0, body.length() - k.length());
            int p = head.lastIndexOf(suf);
            if (p < 0) continue;
            String mid = head.substring(p + suf.length());
            if (mid.length() == 0) continue;
            boolean allPunct = true;
            for (int q = 0; q < mid.length(); q++) if (!isPunctLike(mid.charAt(q))) { allPunct = false; break; }
            if (allPunct) return true;
        }
        return false;
    }

    /** ★ 把末尾那一串标点取出来 ✧ 没有就返回空串 ✓ */
    static String trailingPunct(String s) {
        if (s == null || s.length() == 0) return "";
        int t = s.length();
        while (t > 0 && isPunctLike(s.charAt(t - 1))) t--;
        return s.substring(t);
    }

    /** ★ 去掉末尾标点 ✧ 用来判"整段是不是就是喵" ✓ */
    static String stripPunct(String s) {
        if (s == null) return s;
        return s.substring(0, s.length() - trailingPunct(s).length());
    }

    /** ★★★ 整段是不是全由【后缀的字】拼成的 ★★★
     *  喵 / 喵~ / 喵喵 / 喵喵~ 都算 ✓
     *  —— 用户 2026-10-07："连打两个喵他不会进行输出" ✓
     */
    boolean isAllSuffix(String s, String[] tildeOut) {
        tildeOut[0] = "";
        if (s == null || s.length() == 0 || suffix.length() == 0) return false;
        String bare = stripTrailingTilde(suffix);
        if (bare == null || bare.length() == 0) return false;
        String tilde = suffix.substring(bare.length());
        tildeOut[0] = tilde;
        int i = 0;
        while (i < s.length()) {
            if (i + bare.length() <= s.length() && s.startsWith(bare, i)) {
                i += bare.length();
                continue;
            }
            if (tilde.length() > 0 && tilde.indexOf(s.charAt(i)) >= 0) { i++; continue; }
            return false;
        }
        return true;
    }

    /** ★ 整段就是【后缀本身】吗（喵 / 喵~ ✓）*/
    boolean isJustSuffix(String s) {
        if (s == null || s.length() == 0 || suffix.length() == 0) return false;
        String t = s.trim();
        if (t.equals(suffix)) return true;
        String bare = stripTrailingTilde(suffix);
        return bare != null && bare.length() > 0 && t.equals(bare);
    }

    /** ★ 2026-10-18：带冷却的颜文字抽取 ✧ 和电脑版一致 ✓ */
    String pickKaomoji() {
        List<String> pool = favKao.isEmpty() ? kaomoji : favKao;
        if (pool.isEmpty()) return "";
        if (pool.size() == 1) return pool.get(0);

        int cool = kaoCool;
        int maxCool = pool.size() / 2;
        if (cool > maxCool) cool = maxCool;

        if (cool > 0) {
            for (int i = kaoRecent.size() - 1; i >= 0; i--)
                if (!pool.contains(kaoRecent.get(i))) kaoRecent.remove(i);
            while (kaoRecent.size() > cool) kaoRecent.remove(0);

            List<String> cand = new ArrayList<String>();
            for (int i = 0; i < pool.size(); i++)
                if (!kaoRecent.contains(pool.get(i))) cand.add(pool.get(i));

            if (!cand.isEmpty()) {
                String pick = cand.get(RND.nextInt(cand.size()));
                kaoRecent.add(pick);
                return pick;
            }
        }
        String any = pool.get(RND.nextInt(pool.size()));
        kaoRecent.add(any);
        return any;
    }


    /** ★ 只取一个颜文字 ✧ 不带后缀 ✓ */
    String kaoOnly() {
        if (kaomoji.isEmpty()) return "";
        // \u2605 2026-10-18\uff1a\u6536\u85CF\u975E\u7A7A\u65F6\u53EA\u4ECE\u6536\u85CF\u91CC\u62BD \u2713
        String k = pickKaomoji();
        lastKaomoji = k;
        return k;
    }

    /** ★ 把后缀末尾的【波浪号】去掉 ✓ 支持 ~ ～ 〜 三种 ✓ */
    static String stripTrailingTilde(String suf) {
        if (suf == null || suf.length() == 0) return suf;
        while (suf.length() > 0) {
            char c = suf.charAt(suf.length() - 1);
            if (c == '~' || c == '\uFF5E' || c == '\u301C') suf = suf.substring(0, suf.length() - 1);
            else break;
        }
        return suf;
    }

    /** ★ noTilde = 句尾是情绪标点 ✧ 后缀末尾不加 ~ ✓ */
    /** ★ 这一次选中的颜文字 ✧ 拆装饰时要用 ✓ */
    String lastKaomoji = "";

    String decorate(boolean noTilde) {
        // \u2605 \u540C\u4E0A\uff1a\u6536\u85CF\u4F18\u5148 \u2713
        String kao = pickKaomoji();
        lastKaomoji = kao;
        String suf = noTilde ? stripTrailingTilde(suffix) : suffix;
        return applyTemplate(template, kao, suf);
    }

    // every decoration string that could already be sitting in the text
    List<String> buildDecorations() {
        List<String> list = new ArrayList<String>();
        String t = template == null ? "" : template;
        for (int i = 0; i < kaomoji.size(); i++) {
            String d = applyTemplate(t, kaomoji.get(i), suffix);
            if (d.length() > 0 && !list.contains(d)) list.add(d);
        }
        // ★ 同时把【去掉波浪号】的版本也算进去 ✓
        //   否则 "你好喵(●'ω'●)！" 会被当成没装饰过 ✧ 再装一次 ✓
        String sufNoTilde = stripTrailingTilde(suffix);
        if (!sufNoTilde.equals(suffix)) {
            for (int i = 0; i < kaomoji.size(); i++) {
                String d2 = applyTemplate(t, kaomoji.get(i), sufNoTilde);
                if (d2.length() > 0 && !list.contains(d2)) list.add(d2);
            }
            String d3 = applyTemplate(t, "", sufNoTilde);
            if (d3.length() > 0 && !list.contains(d3)) list.add(d3);
        }
        String d0 = applyTemplate(t, "", suffix);
        if (d0.length() > 0 && !list.contains(d0)) list.add(d0);
        if (suffix.length() > 0 && !list.contains(suffix)) list.add(suffix);
        return list;
    }

    /**
     * Rules must be idempotent. A rule like "ha ha => ha ha ha" has a replacement
     * containing its own pattern, so running the rules again over text that was
     * already transformed keeps growing it. Fix: before applying anything, hide the
     * occurrences of each such replacement behind a placeholder, restore afterwards.
     */
    /**
     * Spans that must never be rewritten: links, e-mail addresses, Windows paths, and
     * any alphanumeric token containing a digit (order numbers, versions, IDs).
     * Mangled links and order numbers are the fastest way to make a tool like this
     * unbearable, so both the word rules and the sentence decoration leave them alone.
     */
    static boolean[] protectedMask(String s) {
        boolean[] mask = new boolean[s.length()];
        mark(mask, s, "(https?://|www\\.)[^\\s，。！？；：、）】」》]+");
        mark(mask, s, "[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}");
        mark(mask, s, "[A-Za-z]:\\\\[^\\s]*");
        mark(mask, s, "(?<![A-Za-z0-9_])[A-Za-z0-9_]*[0-9][A-Za-z0-9_]*(?![A-Za-z0-9_])");
        return mask;
    }

    static void mark(boolean[] mask, String s, String pattern) {
        try {
            java.util.regex.Matcher m = java.util.regex.Pattern.compile(pattern).matcher(s);
            while (m.find()) {
                for (int i = m.start(); i < m.end() && i < mask.length; i++) mask[i] = true;
            }
        } catch (Throwable t) {
        }
    }

    /**
     * ★★★ "用户到底打没打字" ✧ 数字也算 ★★★
     *
     * 用户要求："把标点符号和数字也算进打字里面，
     *            有的时候想加个数字或者标点符号结果他不识别"
     *
     * 原因：protectedMask 里有一条把【任何数字】整段保护起来 ✧
     * 目的是不让词库把它改掉 ✓ 但侧效是【只打一个数字】
     * 就被当成"没内容" ✧ 一个装饰都不加 ✓
     *
     * 所以单独看一遍：只要有一个数字、而且它不在
     * 【网址 / 邮箱 / 路径】里 ✧ 就算打了字 ✓
     */
    static boolean hasTypableContent(String s) {
        if (s == null || s.length() == 0) return false;
        if (hasContentOutsideProtected(s)) return true;

        boolean[] hard = new boolean[s.length()];
        mark(hard, s, "(https?://|www\\.)[^\\s，。！？；：、）】」》]+");
        mark(hard, s, "[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}");
        mark(hard, s, "[A-Za-z]:\\\\[^\\s]*");

        // ★ 整段是网址 / 邮箱 / 路径 ✧ 不算打字 ✓
        for (int i = 0; i < s.length(); i++)
            if (hard[i]) return false;

        // ★ 其余：只要有一个非空白字符就算 ✧ 标点也算 ✓
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n') continue;
            return true;
        }
        return false;
    }

    /** a sentence that is nothing but a link or a number is not worth decorating */
    static boolean hasContentOutsideProtected(String s) {
        boolean[] mask = protectedMask(s);
        for (int i = 0; i < s.length(); i++) {
            if (mask[i]) continue;
            char c = s.charAt(i);
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n') continue;
            if (isPunctLike(c)) continue;
            return true;
        }
        return false;
    }

    /** First value of a key, or null. Used by the 设置 page to show the current state. */
    public static String getKey(String text, String cn, String en) {
        for (String line : text.replace("\r\n", "\n").split("\n", -1)) {
            String t = line.trim();
            if (t.length() == 0 || t.charAt(0) == '#') continue;
            int eq = t.indexOf('=');
            if (eq <= 0) continue;
            String k = t.substring(0, eq).trim();
            String c = canonicalKey(k);
            if (c == null) continue;
            if (c.equals(canonicalKey(cn)) || c.equals(canonicalKey(en)))
                return t.substring(eq + 1).trim();
        }
        return null;
    }

    /**
     * Sets one key, leaving every other line - word rules, comments, the pools - exactly
     * where it was. Passing a null value removes the key.
     *
     * Same contract as the style swap: the 设置 page edits the rules file visibly, so the
     * user can always open the rules page and see what a tap actually did.
     */
    public static String setKey(String text, String cn, String en, String value) {
        String[] lines = text.replace("\r\n", "\n").split("\n", -1);
        StringBuilder sb = new StringBuilder();
        boolean done = false;

        for (int i = 0; i < lines.length; i++) {
            String t = lines[i].trim();
            boolean match = false;
            if (t.length() > 0 && t.charAt(0) != '#') {
                int eq = t.indexOf('=');
                if (eq > 0) {
                    String c = canonicalKey(t.substring(0, eq).trim());
                    if (c != null && (c.equals(canonicalKey(cn)) || c.equals(canonicalKey(en))))
                        match = true;
                }
            }
            if (!match) { sb.append(lines[i]).append("\n"); continue; }
            if (done) continue;                       // drop the stale duplicates
            done = true;
            if (value != null) sb.append(cn).append(" = ").append(value).append("\n");
        }
        if (!done && value != null) sb.append(cn).append(" = ").append(value).append("\n");
        return sb.toString();
    }

    /** All values of a repeatable key, in order - 包名 may appear several times. */
    public static java.util.List<String> getKeys(String text, String cn, String en) {
        java.util.List<String> out = new java.util.ArrayList<String>();
        String wantCn = canonicalKey(cn), wantEn = canonicalKey(en);
        for (String line : text.replace("\r\n", "\n").split("\n", -1)) {
            String t = line.trim();
            if (t.length() == 0 || t.charAt(0) == '#') continue;
            int eq = t.indexOf('=');
            if (eq <= 0) continue;
            String c = canonicalKey(t.substring(0, eq).trim());
            if (c == null) continue;
            if (c.equals(wantCn) || c.equals(wantEn)) out.add(t.substring(eq + 1).trim());
        }
        return out;
    }

    /** Replaces every occurrence of a repeatable key with {@code values}, in order. */
    /** \u2605 2026-10-18\uff1a\u8FFD\u52A0\u4E00\u6761\uFF08\u4E0D\u6E05\u6389\u5DF2\u6709\u7684\uFF09\u2713 */
    public static String appendKey(String text, String cn, String en, String value) {
        java.util.List<String> cur = getKeys(text, cn, en);
        cur.add(value);
        return setKeys(text, cn, en, cur);
    }

    public static String setKeys(String text, String cn, String en, java.util.List<String> values) {
        String[] lines = text.replace("\r\n", "\n").split("\n", -1);
        StringBuilder sb = new StringBuilder();
        String wantCn = canonicalKey(cn), wantEn = canonicalKey(en);
        boolean done = false;

        for (int i = 0; i < lines.length; i++) {
            String t = lines[i].trim();
            boolean match = false;
            if (t.length() > 0 && t.charAt(0) != '#') {
                int eq = t.indexOf('=');
                if (eq > 0) {
                    String c = canonicalKey(t.substring(0, eq).trim());
                    if (c != null && (c.equals(wantCn) || c.equals(wantEn))) match = true;
                }
            }
            if (!match) { sb.append(lines[i]).append("\n"); continue; }
            if (done) continue;
            done = true;
            for (int j = 0; j < values.size(); j++)
                sb.append(cn).append(" = ").append(values.get(j)).append("\n");
        }
        if (!done) for (int j = 0; j < values.size(); j++)
            sb.append(cn).append(" = ").append(values.get(j)).append("\n");
        return sb.toString();
    }

    /** Is this key owned by a style? These are exactly the lines a style swap rewrites. */
    static boolean isStyleOwnedKey(String k) {
        String c = canonicalKey(k);
        if (c == null) return false;
        return c.equals("template") || c.equals("suffix") || c.equals("kaomoji")
            || c.equals("tone") || c.equals("opener") || c.equals("closer")
            || c.endsWith("closer");
    }

    /**
     * Swaps every style-owned line (模板 / 后缀 / 颜文字 / 语气 / all 22 tone pools) in
     * {@code text} for the ones in {@code styleText}.
     *
     * Word rules, whitelist, the package list and every comment stay untouched, so a style
     * is a visible edit the user can then tweak - not hidden magic. The PC version grew the
     * same behaviour, and it is the reason a single tap can no longer wipe a hand-written
     * ruleset.
     */
    /** \u2605 2026-10-18\uff1a\u628a\u4e00\u4e2a\u81ea\u5b9a\u4e49\u5916\u89c2\u5957\u5230\u89c4\u5219\u6587\u672c\u4e0a \u2713 */
    public static String applyCustomLook(String text, String[] t) {
        try {
            if (t == null || t.length < 3) return text;
            java.util.List<String> kaos = splitList(t[2]);
            String out = setKey(text, "\u6A21\u677F", "template", "{suffix}{kaomoji}");
            out = setKey(out, "\u540E\u7F00", "suffix", t[1]);
            out = setKeys(out, "\u989C\u6587\u5B57", "kaomoji", kaos);
            // \u2605 \u5916\u89c2\u4e0d\u52a8\u8bed\u6c14\u6c60 \u2727 \u4F46\u8981\u628A\u8BED\u6C14\u5173\u6389 \u2713
            out = setKey(out, "\u8BED\u6C14", "tone", "false");
            return out;
        } catch (Throwable e) {
            return text;
        }
    }

    /** \u2605 2026-10-18\uff1a\u628a\u4e00\u4e2a\u81ea\u5b9a\u4e49\u8bed\u6c14\u5957\u5230\u89c4\u5219\u6587\u672c\u4e0a \u2713 */
    public static String applyCustomTone(String text, String[] t) {
        try {
            if (t == null || t.length < 3) return text;
            java.util.List<String> kaos = splitList(t[2]);
            java.util.List<String> opens = splitList(t[3]);
            java.util.List<String> closes = splitList(t[4]);

            String out = setKey(text, "\u6A21\u677F", "template", "{suffix}{kaomoji}");
            out = setKey(out, "\u540E\u7F00", "suffix", t[1]);
            out = setKeys(out, "\u989C\u6587\u5B57", "kaomoji", kaos);
            out = setKeys(out, "\u5F00\u5934", "opener", opens);
            out = setKeys(out, "\u7ED3\u5C3E", "closer", closes);
            out = setKey(out, "\u8BED\u6C14", "tone", "true");
            return out;
        } catch (Throwable e) {
            return text;
        }
    }

    /** \u2605 2026-10-18\uff1a\u6309\u540d\u5b57\u627e\u81ea\u5b9a\u4e49\u8bed\u6c14 \u2727 \u627e\u4e0d\u5230\u8fd4 null \u2713 */
    public String[] findCustomTone(String name) {
        if (name == null) return null;
        for (int i = 0; i < customTones.size(); i++) {
            String[] t = customTones.get(i);
            if (t != null && t.length > 0 && name.equals(t[0])) return t;
        }
        return null;
    }

    static java.util.List<String> splitList(String s) {
        java.util.List<String> out = new java.util.ArrayList<String>();
        if (s == null) return out;
        String[] parts = s.split(",");
        for (int i = 0; i < parts.length; i++) {
            String v = parts[i].trim();
            if (v.length() > 0) out.add(v);
        }
        return out;
    }

    public static String applyStyleFromAsset(String text, String styleText) {
        // key -> its value lines, in the order the style lists them
        java.util.LinkedHashMap<String, java.util.List<String>> want =
            new java.util.LinkedHashMap<String, java.util.List<String>>();
        for (String raw : styleText.replace("\r\n", "\n").split("\n", -1)) {
            String t = raw.trim();
            if (t.length() == 0 || t.charAt(0) == '#') continue;
            int eq = t.indexOf('=');
            if (eq <= 0) continue;
            String k = t.substring(0, eq).trim();
            if (!isStyleOwnedKey(k)) continue;
            java.util.List<String> v = want.get(k);
            if (v == null) { v = new java.util.ArrayList<String>(); want.put(k, v); }
            v.add(t.substring(eq + 1).trim());
        }

        java.util.HashSet<String> written = new java.util.HashSet<String>();
        StringBuilder sb = new StringBuilder();
        for (String raw : text.replace("\r\n", "\n").split("\n", -1)) {
            String t = raw.trim();
            String k = null;
            if (t.length() > 0 && t.charAt(0) != '#') {
                int eq = t.indexOf('=');
                if (eq > 0) {
                    String kk = t.substring(0, eq).trim();
                    if (isStyleOwnedKey(kk)) k = kk;
                }
            }
            if (k == null) { sb.append(raw).append("\n"); continue; }
            if (written.contains(k)) continue;            // drop the stale duplicates
            written.add(k);
            java.util.List<String> v = want.get(k);
            // A key the style does NOT declare is left exactly as it is. That matters for
            // 只加末尾, which only declares 语气 and must not touch the 22 pools - deleting
            // them there would throw away a hand-written ruleset on a single tap.
            if (v == null) { sb.append(raw).append("\n"); continue; }
            for (int i = 0; i < v.size(); i++)
                sb.append(k).append(" = ").append(v.get(i)).append("\n");
        }
        // style keys the file never had at all
        for (java.util.Map.Entry<String, java.util.List<String>> e : want.entrySet()) {
            if (written.contains(e.getKey())) continue;
            for (int i = 0; i < e.getValue().size(); i++)
                sb.append(e.getKey()).append(" = ").append(e.getValue().get(i)).append("\n");
        }
        return sb.toString();
    }

    static boolean isStyleKeyLine(String line, String cn, String en) {
        String t = line.trim();
        if (t.length() == 0 || t.charAt(0) == '#') return false;
        int eq = t.indexOf('=');
        if (eq <= 0) return false;
        String k = t.substring(0, eq).trim().toLowerCase();
        k = k.replace("_", "").replace("-", "").replace(" ", "").replace(".", "");
        return k.equals(cn) || k.equals(en);
    }

    /**
     * Swaps only the decoration lines (模板 / 后缀 / 颜文字) of a rules text. Word
     * replacements, whitelist and every comment stay untouched, so a style is a visible
     * edit the user can then tweak - not hidden magic that silently overrides settings.
     */
    public static String applyStyleText(String text, String template, String suffix,
                                        String[] kaomoji) {
        String[] lines = text.replace("\r\n", "\n").split("\n", -1);
        StringBuilder sb = new StringBuilder();
        boolean t = false, sfx = false, k = false;

        for (int i = 0; i < lines.length; i++) {
            String raw = lines[i];
            if (isStyleKeyLine(raw, "模板", "template")) {
                if (!t) { sb.append("模板 = ").append(template).append("\n"); t = true; }
                continue;
            }
            if (isStyleKeyLine(raw, "后缀", "suffix")) {
                if (!sfx) { sb.append("后缀 = ").append(suffix).append("\n"); sfx = true; }
                continue;
            }
            if (isStyleKeyLine(raw, "颜文字", "kaomoji")) {
                if (!k) {
                    for (int j = 0; j < kaomoji.length; j++)
                        sb.append("颜文字 = ").append(kaomoji[j]).append("\n");
                    k = true;
                }
                continue;
            }
            sb.append(raw).append("\n");
        }
        if (!t) sb.append("模板 = ").append(template).append("\n");
        if (!sfx) sb.append("后缀 = ").append(suffix).append("\n");
        if (!k) for (int j = 0; j < kaomoji.length; j++)
            sb.append("颜文字 = ").append(kaomoji[j]).append("\n");

        return sb.toString();
    }

    /// ★★★ 条件替换：解析 + 求值（完全本地 ✧ 不联网 ✧ 不花钱 ✓）★★★
    ///
    /// 写法：
    ///    如果 含"吗" 就将 好 替换成 好吗
    ///    如果 语气=疑问 就将 好的 替换成 好嘛？
    ///    如果 含"吗"，不含"吧" 就将 好 替换成 好吗
    /// 多个条件用【，】隔开 ✧ 全部满足才生效 ✓
    /// 条件认不出时整条不生效 ✧ 不报错 ✧ 不乱换 ✓
    public static Pair parseCondRule(String line) {
        String body = line.substring(2).trim();
        int at = indexOfAny(body, new String[] { " 就将 ", " 则将 ", "就将", "则将" });
        if (at < 0) return null;
        String condPart = body.substring(0, at).trim();
        String rest = body.substring(at).trim();
        if (rest.startsWith(" 就将 ")) rest = rest.substring(4).trim();
        else if (rest.startsWith(" 则将 ")) rest = rest.substring(4).trim();
        else rest = rest.substring(2).trim();

        int rp = indexOfAny(rest, new String[] { " 替换成 ", "替换成" });
        if (rp < 0) return null;
        String fromPart = rest.substring(0, rp).trim();
        String toPart = rest.substring(rp).trim();
        if (toPart.startsWith(" 替换成 ")) toPart = toPart.substring(5).trim();
        else if (toPart.startsWith("替换成")) toPart = toPart.substring(3).trim();

        if (fromPart.length() == 0) return null;

        java.util.ArrayList<Cond> conds = new java.util.ArrayList<Cond>();
        String[] parts = condPart.split("[，,、]");
        for (int i = 0; i < parts.length; i++) {
            String one = parts[i].trim();
            if (one.startsWith("且")) one = one.substring(1).trim();
            if (one.startsWith("并")) one = one.substring(1).trim();
            if (one.length() == 0) continue;
            Cond cd = parseOneCond(one);
            if (cd == null) return null;
            conds.add(cd);
        }
        if (conds.isEmpty()) return null;
        return new Pair(fromPart, toPart, conds);
    }

    static int indexOfAny(String s, String[] keys) {
        int best = -1;
        for (int i = 0; i < keys.length; i++) {
            int j = s.indexOf(keys[i]);
            if (j >= 0 && (best < 0 || j < best)) best = j;
        }
        return best;
    }

    /// 去掉两头引号（半角全角都认 ✓）
    static String unquote(String s) {
        s = s.trim();
        if (s.length() >= 2) {
            char a = s.charAt(0), b = s.charAt(s.length() - 1);
            if ((a == '"' && b == '"') || (a == '“' && b == '”')
                    || (a == '「' && b == '」') || (a == '\'' && b == '\''))
                return s.substring(1, s.length() - 1);
        }
        return s;
    }

    static Cond parseOneCond(String one) {
        Cond c = new Cond();
        if (one.equals("在句首") || one.equals("句首")) { c.kind = "start"; return c; }
        if (one.equals("在句尾") || one.equals("句尾")) { c.kind = "end"; return c; }
        // ★ 看上文（聊天上下文）✧ 顺序重要：先认长的 ✓
        if (one.startsWith("上文不含")) { c.kind = "ctxnotcontains"; c.arg = unquote(one.substring(4)); return c.arg.length() > 0 ? c : null; }
        if (one.startsWith("上文含"))   { c.kind = "ctxcontains";    c.arg = unquote(one.substring(3)); return c.arg.length() > 0 ? c : null; }
        if (one.startsWith("不含"))  { c.kind = "notcontains"; c.arg = unquote(one.substring(2)); return c.arg.length() > 0 ? c : null; }
        if (one.startsWith("含"))      { c.kind = "contains";    c.arg = unquote(one.substring(1)); return c.arg.length() > 0 ? c : null; }
        if (one.startsWith("前有")) { c.kind = "prev"; c.arg = unquote(one.substring(2)); return c.arg.length() > 0 ? c : null; }
        if (one.startsWith("后有")) { c.kind = "next"; c.arg = unquote(one.substring(2)); return c.arg.length() > 0 ? c : null; }
        if (one.startsWith("整句是")) { c.kind = "whole"; c.arg = unquote(one.substring(3)); return c.arg.length() > 0 ? c : null; }
        if (one.startsWith("语气=") || one.startsWith("语气＝"))
        { c.kind = "mood"; c.arg = one.substring(3).trim(); return c.arg.length() > 0 ? c : null; }
        if (one.startsWith("长度")) {
            String t = one.substring(2).trim();
            StringBuilder op = new StringBuilder();
            int k = 0;
            while (k < t.length() && (t.charAt(k) == '>' || t.charAt(k) == '<' || t.charAt(k) == '='
                    || t.charAt(k) == '＞' || t.charAt(k) == '＜' || t.charAt(k) == '＝')) {
                char ch = t.charAt(k);
                op.append(ch == '＞' ? '>' : (ch == '＜' ? '<' : (ch == '＝' ? '=' : ch)));
                k++;
            }
            if (op.length() == 0) return null;
            try {
                c.num = Integer.parseInt(t.substring(k).trim());
            } catch (Exception e) {
                return null;
            }
            c.kind = "len"; c.arg = op.toString();
            return c;
        }
        return null;
    }

    /** 规则里有没有用到「上文…」条件 ✧ 没用就别去读聊天记录 ✓ */
    /** \u2605 \u8fd9\u6b21\u8f6c\u6362\u7528\u5f97\u4e0a\u804a\u5929\u4e0a\u4e0b\u6587\u5417 \u2727 \u7528\u4e0d\u4e0a\u5c31\u522b\u53bb\u904d\u5386\u8282\u70b9\u6811\uff08\u7701\u7535 \u2713\uff09
     *
     * \u4f8b\uff1a\u89c4\u5219\u91cc\u5199\u4e86\u300c\u4e0a\u6587\u542b"\u2026"\u300d\u2727 \u6216\u8005\u5f00\u4e86\u300c\u5c0f\u6a21\u578b\u8bed\u6c14 = true\u300d\u65f6\u624d\u8fd4\u56de true \u2713
     */
    public boolean wantsContext() {
        if (tinyMood) return true;
        return needsCtx(rules);
    }

    public static boolean needsCtx(List<Pair> rules) {
        if (rules == null) return false;
        for (int i = 0; i < rules.size(); i++) {
            Pair p = rules.get(i);
            if (p.conds == null) continue;
            for (int j = 0; j < p.conds.size(); j++) {
                String k = p.conds.get(j).kind;
                if (k.equals("ctxcontains") || k.equals("ctxnotcontains")) return true;
            }
        }
        return false;
    }

    static boolean condOk(Cond c, String s, int at, int fromLen, String plain) {
        return condOk(c, s, at, fromLen, plain, "");
    }

    /** ctx = 聊天上下文 ✧ 读不到就是空串 ✓ */
    static boolean condOk(Cond c, String s, int at, int fromLen, String plain, String ctx) {
        // ★ 看上文 ✧ 读不到上下文（ctx 为空）时【两个都返回 false】✧ 不动 ✓
        if (c.kind.equals("ctxcontains"))    return ctx.length() > 0 && ctx.indexOf(c.arg) >= 0;
        if (c.kind.equals("ctxnotcontains")) return ctx.length() > 0 && ctx.indexOf(c.arg) < 0;
        if (c.kind.equals("contains"))    return plain.indexOf(c.arg) >= 0;
        if (c.kind.equals("notcontains")) return plain.indexOf(c.arg) < 0;
        if (c.kind.equals("prev"))        return at >= c.arg.length()
                && s.regionMatches(at - c.arg.length(), c.arg, 0, c.arg.length());
        if (c.kind.equals("next"))        return at + fromLen + c.arg.length() <= s.length()
                && s.regionMatches(at + fromLen, c.arg, 0, c.arg.length());
        if (c.kind.equals("start"))       return at == 0;
        if (c.kind.equals("end"))         return at + fromLen == s.length();
        if (c.kind.equals("whole"))       return s.equals(c.arg);
        if (c.kind.equals("mood"))        return mood(plain).equals(moodKey(c.arg));
        if (c.kind.equals("len")) {
            if (c.arg.equals(">"))  return plain.length() >  c.num;
            if (c.arg.equals("<"))  return plain.length() <  c.num;
            if (c.arg.equals(">=")) return plain.length() >= c.num;
            if (c.arg.equals("<=")) return plain.length() <= c.num;
            return plain.length() == c.num;
        }
        return false;
    }

    static String moodKey(String w) {
        w = w.trim();
        if (w.equals("开心") || w.equals("高兴") || w.equals("happy")) return "happy";
        if (w.equals("难过") || w.equals("伤心") || w.equals("sad"))   return "sad";
        if (w.equals("生气") || w.equals("愤怒") || w.equals("angry")) return "angry";
        if (w.equals("疑问") || w.equals("question")) return "question";
        if (w.equals("普通") || w.equals("平常") || w.equals("平淡") || w.equals("plain")) return "plain";
        if (w.equals("惊讶") || w.equals("surprise")) return "surprise";
        if (w.equals("害怕") || w.equals("恐惧") || w.equals("fear")) return "fear";
        if (w.equals("疲惫") || w.equals("累") || w.equals("tired")) return "tired";
        if (w.equals("无聊") || w.equals("bored")) return "bored";
        if (w.equals("失望") || w.equals("disappointed")) return "disappointed";
        if (w.equals("安慰") || w.equals("comfort")) return "comfort";
        if (w.equals("喜爱") || w.equals("喜欢") || w.equals("love")) return "love";
        if (w.equals("分别") || w.equals("再见") || w.equals("farewell")) return "farewell";
        if (w.equals("打招呼") || w.equals("问候") || w.equals("greeting")) return "greeting";
        if (w.equals("邀请") || w.equals("invite")) return "invite";
        if (w.equals("夸奖") || w.equals("表扬") || w.equals("praise")) return "praise";
        if (w.equals("感谢") || w.equals("谢谢") || w.equals("thanks")) return "thanks";
        if (w.equals("道歉") || w.equals("apology")) return "apology";
        if (w.equals("恭喜") || w.equals("congrat")) return "congrat";
        if (w.equals("撒娇") || w.equals("charm")) return "charm";
        if (w.equals("感叹") || w.equals("exclaim")) return "exclaim";
        return w;
    }

    static String applyRules(String s, List<Pair> rules) {
        return applyRules(s, rules, "");
    }

    static String applyRules(String s, List<Pair> rules, String ctx) {
        // Hide protected spans FIRST, so the replacements below (which run sequentially
        // and can feed into each other) simply never see them.
        boolean[] prot = protectedMask(s);
        List<String> guarded = new ArrayList<String>();
        StringBuilder tmp = new StringBuilder();
        int at = 0;
        int i2 = 0;
        while (i2 < s.length()) {
            if (!prot[i2]) { i2++; continue; }
            int j2 = i2;
            while (j2 < s.length() && prot[j2]) j2++;
            tmp.append(s, at, i2);
            guarded.add(s.substring(i2, j2));
            tmp.append("\uE100").append(guarded.size()).append("\uE101");
            at = j2;
            i2 = j2;
        }
        if (guarded.size() > 0) {
            tmp.append(s, at, s.length());
            s = tmp.toString();
        }

        List<String> protect = new ArrayList<String>();
        for (int i = 0; i < rules.size(); i++) {
            Pair p = rules.get(i);
            if (p.from.length() == 0 || p.to.length() == 0) continue;
            if (p.to.indexOf(p.from) < 0) continue;
            if (!protect.contains(p.to)) protect.add(p.to);
        }
        // longest first, so a shorter replacement cannot eat into a longer one.
        // Deliberately a hand-written sort: an anonymous Comparator<String> would make
        // javac emit a synthetic bridge method, and d8 (build-tools 34) crashes on the
        // null parameter name it puts in that method's MethodParameters attribute.
        for (int i = 1; i < protect.size(); i++) {
            String key = protect.get(i);
            int j = i - 1;
            while (j >= 0 && protect.get(j).length() < key.length()) {
                protect.set(j + 1, protect.get(j));
                j--;
            }
            protect.set(j + 1, key);
        }

        for (int i = 0; i < protect.size(); i++)
            s = s.replace(protect.get(i), "\uE000" + i + "\uE001");

        for (int i = 0; i < rules.size(); i++) {
            Pair p = rules.get(i);
            if (p.from.length() == 0) continue;
            if (p.conds == null || p.conds.isEmpty()) {
                s = s.replace(p.from, p.to);      // 无条件 ✧ 老路 ✧ 快 ✓
                continue;
            }
            if (p.to.length() == 0) continue;
            // 有条件 ✧ 要知道匹配在哪个位置 ✧ 手动扫一遍 ✓
            StringBuilder ob = new StringBuilder();
            int scan = 0;
            while (scan <= s.length()) {
                int hit = s.indexOf(p.from, scan);
                if (hit < 0) { ob.append(s, scan, s.length()); break; }
                ob.append(s, scan, hit);        // Java: start, end（不是长度 ✗ C# 才是长度）
                boolean ok = true;
                for (int ci = 0; ci < p.conds.size(); ci++)
                    if (!condOk(p.conds.get(ci), s, hit, p.from.length(), s, ctx)) { ok = false; break; }
                ob.append(ok ? p.to : p.from);
                scan = hit + p.from.length();
            }
            s = ob.toString();
        }

        for (int i = 0; i < protect.size(); i++)
            s = s.replace("\uE000" + i + "\uE001", protect.get(i));

        for (int i = 0; i < guarded.size(); i++)
            s = s.replace("\uE100" + (i + 1) + "\uE101", guarded.get(i));

        return s;
    }

    /**
     * s[from..to) is one sentence, terminator included. posAfter is the index just
     * past that terminator, or -1 if the sentence did not end with one.
     *
     * The decoration goes AFTER the terminator, so on a re-run it becomes the head of
     * the next sentence and the sentence still "ends undecorated" - which used to make
     * every pass bolt on another kaomoji. Hence the lookahead.
     */
    /**
     * Marks the spans of s that are already a known decoration. A suffix like "喵！"
     * contains a sentence terminator of its own, so without this the splitter cuts the
     * decoration in half, treats the kaomoji as a fresh sentence and decorates it again.
     */
    static boolean[] decorationMask(String s, List<String> decorations) {
        boolean[] mask = new boolean[s.length()];
        if (decorations == null) return mask;
        for (int n = 0; n < decorations.size(); n++) {
            String d = decorations.get(n);
            if (d.length() == 0) continue;
            int at = 0;
            while (at <= s.length() - d.length()) {
                int i = s.indexOf(d, at);
                if (i < 0) break;
                for (int k = i; k < i + d.length() && k < mask.length; k++) mask[k] = true;
                at = i + 1;
            }
        }
        return mask;
    }

    void emitSegment(StringBuilder outp, String s, int from, int to, int posAfter,
                     List<String> decorations, boolean[] deco) {
        int end = to;
        while (end > from && (s.charAt(end - 1) == ' ' || s.charAt(end - 1) == '\t')) end--;

        // Split the trailing punctuation off the body so the decoration lands BEFORE it.
        // Stop at a decoration boundary: a suffix like "喵！" ends in a terminator of its
        // own, and eating that would leave the body looking undecorated.
        int punctStart = end;
        // ★ 总是切 ✧ 和电脑版一致
        //   不能只在 posAfter >= 0 时切 ✧ 最后一段是 -1 ✓
        if (true)
            // ★ 逗号也算 ✧ 和电脑版一致 ✓
            //   你好， → 你好喵~(颜文字)，
            while (punctStart > from
                    && (isBreak(s.charAt(punctStart - 1)) || isComma(s.charAt(punctStart - 1)))
                    && (deco == null || !deco[punctStart - 1])) punctStart--;

        String body = s.substring(from, punctStart);
        String punct = s.substring(punctStart, end);
        String tail = s.substring(end, to);

        if (body.length() == 0 || !hasTypableContent(body)) {
            outp.append(s, from, to);
            return;
        }

        // ★ 整段就是后缀本身 ✧ 只补一个颜文字 ✓
        {
            String[] tj = new String[1];
            if (isAllSuffix(body, tj)) {
                // ★ 保留用户打的个数 ✧ 只补波浪号和颜文字 ✓
                String t1 = (tj[0].length() > 0 && body.endsWith(tj[0])) ? "" : tj[0];
                boolean emoJ = isEmotionalPunct(punct);
                String kaoJ = kaoOnly();
                outp.append(placeDecoration(body + t1 + kaoJ, kaoJ, punct, emoJ, suffix));
                outp.append(tail);
                return;
            }
        }

        int p = posAfter;
        if (p >= 0) while (p < s.length() && (s.charAt(p) == ' ' || s.charAt(p) == '\t')) p++;

        if (alreadyDecorated(s, p, body, decorations)) {
            // ★★★ 用户把标点补在颜文字后面 ✧ 挪到颜文字前面 ★★★
            String moved = movePunctInsideDecoration(body, punct, decorations);
            if (moved != null) {
                outp.append(moved);
                outp.append(tail);
                return;
            }
            outp.append(s, from, to);
            return;
        }

        // ★★★ 按标点摆位置 ★★★
        boolean emoHere = isEmotionalPunct(punct);
        String decoHere = decorate(emoHere);
        outp.append(body);
        outp.append(placeDecoration(decoHere, lastKaomoji, punct, emoHere, suffix));
        outp.append(tail);
    }

    /**
     * Prose = something a person typed, as opposed to decoration material.
     * Kaomoji are built from symbols plus stray Greek/Thai/Hangul/Arabic letters
     * (such as the ones in the default list), so only CJK and ASCII alphanumerics
     * count as prose here.
     */
    static boolean isProse(char c) {
        if (c >= 0x3400 && c <= 0x9FFF) return true;      // CJK
        if (c >= 0xF900 && c <= 0xFAFF) return true;
        if (c >= 'a' && c <= 'z') return true;
        if (c >= 'A' && c <= 'Z') return true;
        if (c >= '0' && c <= '9') return true;
        return false;
    }

    /**
     * True when the suffix appears within s[from, from+len) and NOTHING but decoration
     * material follows it - the signature of a kaomoji the user has partially deleted.
     * "hello meow~(kao" still looks like a broken decoration, whereas
     * "meow~ today was nice" is ordinary prose and must still get decorated.
     */
    boolean suffixTailIsDecoration(String s, int from, int len) {
        if (suffix.length() == 0 || len <= 0) return false;
        // EVERY occurrence inside the window is considered, not just the first. Once a text
        // carries two decorations ("你好喵~(kao)你好喵~(kao") the FIRST suffix is followed by
        // the user's own words, so checking only the first answered "not decorated" and a
        // third decoration was appended - every pass added one more.
        int at = from;
        int limit = from + len;
        while (true) {
            int idx = s.indexOf(suffix, at);
            if (idx < 0 || idx >= limit) return false;
            boolean clean = true;
            for (int i = idx + suffix.length(); i < s.length(); i++)
                if (isProse(s.charAt(i))) { clean = false; break; }
            if (clean) return true;
            at = idx + 1;
        }
    }

    /**
     * Is this sentence already decorated?
     *
     * Exact matching alone is not enough: if the user edits one character out of an
     * existing kaomoji the match breaks, the sentence looks undecorated, and a SECOND
     * decoration gets bolted on.
     */
    boolean alreadyDecorated(String s, int afterTerm, String body, List<String> decorations) {
        final int WINDOW = 24;

        // The decoration lives INSIDE the sentence, just before the punctuation, so what
        // matters is whether the body already ends with one.
        if (endsWithSkip(body)) return true;
        if (endsWithAnyIn(body, decorations)) return true;
        // ★ 已经挪好的形状（后缀 + 标点 + 颜文字 ✓）也算已装饰 ✓
        if (endsWithPunctInsideDecoration(body)) return true;
        int from = Math.max(0, body.length() - WINDOW);
        if (suffixTailIsDecoration(body, from, body.length() - from)) return true;

        // Text an older build already decorated has it AFTER the punctuation. Still
        // recognise that shape so nothing gets a second kaomoji bolted on.
        if (afterTerm >= 0) {
            if (startsWithAny(s, afterTerm, decorations)) return true;
            if (startsWithAny(s, afterTerm, skipEnds)) return true;
            if (suffixTailIsDecoration(s, afterTerm,
                    Math.min(WINDOW, s.length() - afterTerm))) return true;
        }
        return false;
    }

    String perSentence(String s, List<String> decorations) {
        // A "?" or ";" inside a URL or an e-mail is not the end of a sentence. Without
        // this a link got cut at the "?" and the tail stopped looking like a link, so it
        // was decorated.
        boolean[] guarded = protectedMask(s);
        boolean[] deco = decorationMask(s, decorations);

        StringBuilder outp = new StringBuilder();
        int segStart = 0;
        int i = 0;
        while (i < s.length()) {
            char c = s.charAt(i);
            if (c == '\n' || c == '\r') {
                emitSegment(outp, s, segStart, i, -1, decorations, deco);
                outp.append(c);
                i++;
                segStart = i;
                continue;
            }
            if (isBreak(c) && !guarded[i] && !deco[i]) {
                int j = i;
                while (j + 1 < s.length() && isBreak(s.charAt(j + 1)) && !guarded[j + 1] && !deco[j + 1]) j++;
                emitSegment(outp, s, segStart, j + 1, j + 1, decorations, deco);
                i = j + 1;
                segStart = i;
                continue;
            }
            i++;
        }
        if (segStart < s.length()) emitSegment(outp, s, segStart, s.length(), -1, decorations, deco);
        return outp.toString();
    }

    static String pick(List<String> pool) {
        if (pool == null || pool.isEmpty()) return "";
        return pool.get(RND.nextInt(pool.size()));
    }

    /** Longest first.
     *
     *  Hand-written on purpose: Collections.sort with an anonymous Comparator produced
     *  Rules$1.class, and d8 fails on it ("Compilation failed with an internal error") -
     *  the same reason there is not a single anonymous inner class anywhere in this project.
     */
    static void sortByLengthDesc(java.util.List<String> list) {
        for (int i = 1; i < list.size(); i++) {
            String v = list.get(i);
            int j = i - 1;
            while (j >= 0 && list.get(j).length() < v.length()) {
                list.set(j + 1, list.get(j));
                j--;
            }
            list.set(j + 1, v);
        }
    }

    /** Every closing line this ruleset may have written, longest first - a long closing line
     *  must not be partially eaten by a shorter one from the same table. */
    /** 引擎自己【写过】的后缀 / 颜文字 / 关闭语，跨风格累积。
     *
     * 为什么需要它：剥旧装饰、剥旧关闭语那两步原来只看【当前配置】。
     * 用户一切换风格，上一轮用旧风格写下的装饰就认不出来了 —— 会被当成他自己
     * 写的话，新一轮再包一遍，越用越长。电脑版有同样的问题，一起修的。
     *
     * 电脑版那边还能靠"启动时播种自带风格"覆盖重启后的情况，安卓这边同样做了。
     */
    public static final java.util.List<String> EVER_SUFFIXES = new java.util.ArrayList<String>();
    public static final java.util.List<String> EVER_KAOMOJI = new java.util.ArrayList<String>();
    public static final java.util.List<String> EVER_CLOSERS = new java.util.ArrayList<String>();

    /** 登记一个"软件自带的风格"用了哪些装饰素材。启动时对每个风格调一次。 */
    public static void seedKnown(String suf, String[] kao, String[] clo) {
        if (suf != null && suf.length() > 0 && !EVER_SUFFIXES.contains(suf)) EVER_SUFFIXES.add(suf);
        if (kao != null)
            for (int i = 0; i < kao.length; i++)
                if (kao[i] != null && kao[i].length() > 0 && !EVER_KAOMOJI.contains(kao[i])) EVER_KAOMOJI.add(kao[i]);
        if (clo != null)
            for (int i = 0; i < clo.length; i++)
                if (clo[i] != null && clo[i].length() > 0 && !EVER_CLOSERS.contains(clo[i])) EVER_CLOSERS.add(clo[i]);
    }

    /** 每次转换前登记当前配置用的装饰素材（自然累积，不用额外接线）。 */
    void rememberStyle() {
        if (suffix != null && suffix.length() > 0 && !EVER_SUFFIXES.contains(suffix)) EVER_SUFFIXES.add(suffix);
        for (int i = 0; i < kaomoji.size(); i++) {
            String k = kaomoji.get(i);
            if (k != null && k.length() > 0 && !EVER_KAOMOJI.contains(k)) EVER_KAOMOJI.add(k);
        }
    }

    List<String> allCloserLines() {
        java.util.ArrayList<String> all = new java.util.ArrayList<String>();
        List<String>[] pools = new List[] {
            closers, questionClosers, exclaimClosers, happyClosers, sadClosers, angryClosers,
            greetingClosers, farewellClosers, loveClosers, praiseClosers, thanksClosers,
            apologyClosers, congratClosers, inviteClosers, surpriseClosers, fearClosers,
            tiredClosers, boredClosers, disappointedClosers, comfortClosers, charmClosers
        };
        for (int i = 0; i < pools.length; i++)
            if (pools[i] != null) all.addAll(pools[i]);
            // ★ 加上"写过的所有风格"的关闭语 —— 换风格后旧关闭语也要能认出来。
            all.addAll(EVER_CLOSERS);
        sortByLengthDesc(all);
        return all;
    }

    /** Take back a closing line the user has typed past. A closer is ordinary prose, so the
     *  only evidence is position: prose straight after it means the engine did not put it
     *  there for this text. */
    String stripStaleClosers(String s) {
        if (s == null) return s;
        List<String> all = allCloserLines();

        // exact matches first
        for (int k = 0; k < all.size(); k++) {
            String c = all.get(k);
            if (c.length() == 0) continue;
            int i = 0;
            while (i < s.length() && (i = s.indexOf(c, i)) >= 0) {
                int j = i + c.length();
                while (j < s.length() && (s.charAt(j) == ' ' || s.charAt(j) == '\t')) j++;

                // ★★★ 判断这个收尾句是不是"搁浅"了 ★★★
                //
                // 用户日志：
                //   read  [你知道吗，你好123喵~(=；ω；=)，我看到你消息就马上回了哦？]
                //   write [...哦？喵(=；ω；=)，你终于来了……我好想你]   ← 又装了一次 ✓
                //
                // 原因：isProse 只认【CJK / 字母 / 数字】✧
                //   全角 ？！。， 这些【都不算】✓
                //   于是收尾句没被剥掉 ✧ 装饰被夹在中间 ✧ 下一遍就再装一次 ✓
                //
                // 修法：后面【只跟了一串标点、而且一直到结尾】也算搁浅 ✓
                boolean stranded;
                if (j >= s.length()) stranded = false;              // 到尾了 ✧ 正常装饰 ✓
                else if (isProse(s.charAt(j))) stranded = true;     // 后面是用户打的字 ✓
                else {
                    int q = j;
                    while (q < s.length() && isPunctLike(s.charAt(q))) q++;
                    stranded = (q >= s.length());                   // 后面只有标点 ✧ 也算 ✓
                }

                if (stranded) s = s.substring(0, i) + s.substring(i + c.length());
                else i += c.length();
            }
        }

        // then DAMAGED ones, which the exact pass cannot see. This is the same bug the PC
        // engine had, and it was fixed there first - the two engines are meant to behave
        // identically, so leaving Android behind meant the same duplication came back on the
        // phone. From a user's log on Android:
        //     09:05:49 写回 [笨、笨蛋！你好喵！(￣ー￣)，哼，是你啊……有事吗]
        //            -> [笨、笨蛋！你好喵！(￣ー￣)，哼，是你啊……有事吗喵！(¯ε¯)，哼，是你啊……有事吗]
        // The closer survived only as a PREFIX of itself with the text running on. Requiring a
        // long run (12 chars) keeps ordinary prose from ever looking like one.
        final int MIN_DAMAGED = 12;
        for (int k = 0; k < all.size(); k++) {
            String c = all.get(k);
            if (c.length() < MIN_DAMAGED + 1) continue;
            int i = 0;
            while (i < s.length()) {
                int n = 0;
                while (n < c.length() && i + n < s.length() && s.charAt(i + n) == c.charAt(n)) n++;
                if (n >= MIN_DAMAGED && n < c.length()) {
                    int j = i + n;
                    while (j < s.length() && (s.charAt(j) == ' ' || s.charAt(j) == '\t')) j++;
                    if (j < s.length() && isProse(s.charAt(j))) {
                        s = s.substring(0, i) + s.substring(i + n);   // drop the prefix
                        continue;                       // re-check the same index
                    }
                }
                i++;
            }
        }
        return s;
    }

    /** Every exact string template + kaomoji can produce. Whole decorations only - a bare
     *  suffix is far too easy to type by accident. */
    public List<String> decorationTokens() {
        java.util.ArrayList<String> t = new java.util.ArrayList<String>();
        // ★ 当前配置 + 这次运行里【用过的所有】后缀 / 颜文字。
        //   只算当前配置的话，换风格之后旧装饰就认不出来，会一层层叠上去。
        java.util.ArrayList<String> sufs = new java.util.ArrayList<String>();
        if (suffix != null) sufs.add(suffix);
        for (int i = 0; i < EVER_SUFFIXES.size(); i++)
            if (!sufs.contains(EVER_SUFFIXES.get(i))) sufs.add(EVER_SUFFIXES.get(i));
        java.util.ArrayList<String> kaos = new java.util.ArrayList<String>();
        for (int i = 0; i < kaomoji.size(); i++)
            if (!kaos.contains(kaomoji.get(i))) kaos.add(kaomoji.get(i));
        for (int i = 0; i < EVER_KAOMOJI.size(); i++)
            if (!kaos.contains(EVER_KAOMOJI.get(i))) kaos.add(EVER_KAOMOJI.get(i));
        for (int a = 0; a < sufs.size(); a++) {
            String suf = sufs.get(a);
            if (suf == null) continue;
            for (int b = 0; b < kaos.size(); b++) {
                String k = kaos.get(b);
                String s = template.replace("{后缀}", suf).replace("{颜文字}", k)
                                   .replace("{suffix}", suf).replace("{kaomoji}", k);
                if (s.length() > 0 && !t.contains(s)) t.add(s);
            }
        }
        sortByLengthDesc(t);
        return t;
    }

    /** Punctuation a kaomoji never contains - stops the damaged-decoration scan at the end of
     *  the kaomoji instead of running on into the next sentence. */
    static boolean isDecoPunct(char c) {
        return c == '\u3002' || c == '\uFF01' || c == '\uFF1F' || c == '\uFF0C'
            || c == '\u3001' || c == '\uFF1B' || c == '\uFF1A' || c == '\u2026'
            || c == '\u2014' || c == '.' || c == ',' || c == '!' || c == '?'
            || c == ';' || c == ':' || c == ' ' || c == '\t' || c == '\n';
    }

    /** Take back a decoration the user has typed past. The test is the FIRST character after
     *  it: a decoration is always written just before a terminator, so punctuation or the next
     *  sentence follows. Meeting prose immediately means the user typed straight on. */
    String stripStaleDecorations(String s) {
        if (s == null) return s;

        // DAMAGED decorations first. Pinned down by a user's log:
        //   引擎写入   你好喵~ฅ^•ﻌ•^ฅ
        //   用户删一个 你好喵~ฅ^•ﻌ•ฅ          <- matches no token any more
        //   用户接着打 你好喵~ฅ^•ﻌ•ฅ你好
        //   引擎追加   你好喵~ฅ^•ﻌ•ฅ你好喵~(kao)   <- looks like the text was duplicated
        // The shape to look for: a suffix followed by a run of characters that are neither
        // prose NOR punctuation, with real text after that run. That is the engine's own
        // leftover, so the suffix and the run go together.
        List<String> tokens = decorationTokens();
        if (suffix.length() > 0) {
            int i = 0;
            while (i < s.length() && (i = s.indexOf(suffix, i)) >= 0) {
                // ★★★ 先看一眼：这里是不是【一个完整的已知装饰】★★★
                //
                // 不然会误伤【本身就含 ASCII 字母】的颜文字 ✓
                //   (っ´ω`c) 里的 `c` 是字母 ✧ (TwT) 里的 T 也是 ✓
                //   而 isProse 把字母当散文 ✧
                //   于是扫到 "喵~(" 就被那个字母打断 ✧
                //   判定"后面是用户打的字" ✧ 把 "喵~(" 当残留剥掉 ✓
                if (startsWithAny(s, i, tokens)) {
                    i += suffix.length();
                    continue;
                }

                int j = i + suffix.length();
                int run = j;
                while (run < s.length() && !isProse(s.charAt(run)) && !isDecoPunct(s.charAt(run))) run++;
                // ...and the very next character has to be REAL TEXT. A decoration sitting
                // legitimately before a comma - "你好喵！(kao)，下一句" - also has a run of
                // decoration characters after its suffix, and stripping that one made every
                // re-run pick a fresh kaomoji. Punctuation after the run means it is in its
                // proper place; prose means the user has typed on and it is a leftover.
                if (run > j && run < s.length() && isProse(s.charAt(run))) {
                    s = s.substring(0, i) + s.substring(run);   // re-check at the same index
                } else {
                    i += suffix.length();
                }
            }
        }

        for (int k = 0; k < tokens.size(); k++) {
            String t = tokens.get(k);
            if (t.length() == 0) continue;
            int i = 0;
            while (i < s.length() && (i = s.indexOf(t, i)) >= 0) {
                int j = i + t.length();
                while (j < s.length() && (s.charAt(j) == ' ' || s.charAt(j) == '\t')) j++;
                if (j < s.length() && isProse(s.charAt(j))) s = s.substring(0, i) + s.substring(i + t.length());
                else i += t.length();
            }
        }
        return s;
    }

    static String peelPrefix(String s, List<String> pool) {
        for (int i = 0; i < pool.size(); i++) {
            String o = pool.get(i);
            if (o.length() > 0 && s.startsWith(o)) return o;
        }
        return null;
    }

    static String peelSuffix(String s, List<String> pool) {
        for (int i = 0; i < pool.size(); i++) {
            String o = pool.get(i);
            if (o.length() > 0 && s.endsWith(o)) return o;
        }
        return null;
    }

    // ---- 语境判断: keywords, NOT a model. Nothing is downloaded or sent anywhere. ----
    static final String[] QUESTION_WORDS = {
        "\u4EC0\u4E48", "\u600E\u4E48", "\u4E3A\u4EC0\u4E48", "\u4E3A\u5565",
        "\u54EA", "\u662F\u5426", "\u80FD\u4E0D\u80FD", "\u53EF\u4E0D\u53EF\u4EE5",
        "\u597D\u4E0D\u597D", "\u6709\u6CA1\u6709", "\u591A\u5C11", "\u8C01",
        "\u5417", "\u5462"
    };
    static final String[] SAD_WORDS = {
        "\u4E0D\u5F00\u5FC3", "\u96BE\u8FC7", "\u4F24\u5FC3", "\u59D4\u5C48",
        "\u60F3\u54ED", "\u5931\u843D", "\u96BE\u53D7", "\u90C1\u95F7",
        "\u75DB\u82E6", "\u5D29\u6E83", "\u54C0"
    };
    static final String[] ANGRY_WORDS = {
        "生气", "气死", "烦死", "好烦",
        "讨厌", "无语", "过分", "气人",
        "烦躁", "恼火",
        // ★ 2026-10-31：骂人话也是生气 ✗ 否则会被小模型猜成「感谢」✓
        "滚", "滚啊", "滚蛋", "滚开", "滚出去",
        "草泥马", "卧槽", "我操", "干你",
        "妈的", "他妈", "你妈", "傻逼", "傻b", "SB", "sb",
        "闭嘴", "去死", "有病", "恶心", "狗屎",
        "废物", "垃圾", "别烦"
    };

    /** ★ 2026-10-31：强生气词 ✗ 小模型不准把它改成正面情绪 ✓ */
    static final String[] STRONG_ANGRY = {
        "滚啊", "滚蛋", "滚开", "滚出去",
        "草泥马", "卧槽", "我操", "干你",
        "妈的", "你妈", "傻逼", "SB", "sb",
        "闭嘴", "去死", "有病", "废物"
    };

    static boolean looksAngry(String s) {
        if (s == null) return false;
        return containsAny(s, STRONG_ANGRY);
    }

    static boolean isPositiveMood(String m) {
        return "thanks".equals(m) || "praise".equals(m) || "love".equals(m)
            || "happy".equals(m) || "comfort".equals(m) || "greeting".equals(m)
            || "charm".equals(m) || "congrat".equals(m);
    }
    static final String[] HAPPY_WORDS = {
        "\u54C8\u54C8", "\u563F\u563F", "\u563B\u563B", "\u5F00\u5FC3",
        "\u9AD8\u5174", "\u592A\u597D\u4E86", "\u597D\u8036", "\u597D\u68D2",
        "\u8D5E", "\u559C\u6B22", "\u53EF\u7231", "\u597D\u73A9",
        "\u5389\u5BB3", "\u8C22\u8C22", "\u8212\u670D", "\u5E78\u798F"
    };

    // ---- 社交类：打招呼 / 分别 / 喜爱 / 夸奖 ----
    // These overlap with the emotional lists above, so the ORDER in mood() matters more
    // than the words themselves. 你好棒 contains 你好; 我喜欢你 contains 喜欢, which is
    // also a happy word.
    static final String[] GREETING_WORDS = {
        "\u4F60\u597D", "\u60A8\u597D", "\u65E9\u4E0A\u597D", "\u4E2D\u5348\u597D",
        "\u665A\u4E0A\u597D", "\u65E9\u5B89", "\u54C8\u55BD", "\u55E8",
        "\u5728\u5417", "\u5728\u4E0D\u5728", "\u5728\u4E48", "\u4F60\u5728\u5417",
        "\u4E45\u7B49\u4E86", "\u6211\u6765\u4E86", "\u65E9\u554A"
    };
    static final String[] FAREWELL_WORDS = {
        "\u518D\u89C1", "\u62DC\u62DC", "\u665A\u5B89", "\u5148\u8D70\u4E86",
        "\u6211\u8D70\u4E86", "\u4E0B\u7EBF\u4E86", "\u6211\u64A4\u4E86",
        "\u4E0B\u6B21\u804A", "\u6539\u5929\u804A", "\u5148\u7761\u4E86",
        "\u6211\u7761\u4E86", "\u6D17\u6D17\u7761\u4E86", "\u4E0B\u4E2A\u4E86"
    };
    static final String[] LOVE_WORDS = {
        "\u559C\u6B22\u4F60", "\u7231\u4F60", "\u60F3\u4F60", "\u60F3\u89C1\u4F60",
        "\u62B1\u62B1", "\u4EB2\u4EB2", "\u4E48\u4E48", "\u5FC3\u52A8",
        "\u820D\u4E0D\u5F97\u4F60", "\u60F3\u62B1", "\u597D\u60F3\u4F60"
    };
    static final String[] PRAISE_WORDS = {
        "\u597D\u68D2", "\u592A\u68D2", "\u5389\u5BB3", "\u592A\u5F3A",
        "\u4F18\u79C0", "\u505A\u5F97\u597D", "\u5E72\u5F97\u597D",
        "\u68D2\u6781\u4E86", "\u4E86\u4E0D\u8D77", "\u7EDD\u4E86",
        "\u4F69\u670D", "\u5B66\u5230\u4E86", "\u771F\u884C", "\u597D\u725B",
        "\u725B\u903C", "\u6709\u4E24\u4E0B\u5B50", "\u5927\u4F6C"
    };
    static final String[] THANKS_WORDS = {
        "\u8C22\u8C22", "\u591A\u8C22", "\u611F\u8C22", "\u8F9B\u82E6\u4E86",
        "\u9EBB\u70E6\u4F60\u4E86", "\u8C22\u5566", "\u9EBB\u70E6\u4E86", "\u8F9B\u82E6\u5566"
    };
    static final String[] APOLOGY_WORDS = {
        "\u5BF9\u4E0D\u8D77", "\u62B1\u6B49", "\u4E0D\u597D\u610F\u601D",
        "\u6211\u9519\u4E86", "\u89C1\u8C05", "\u539F\u8C05\u6211", "\u662F\u6211\u4E0D\u597D"
    };
    static final String[] CONGRAT_WORDS = {
        "\u606D\u559C", "\u795D\u8D3A", "\u751F\u65E5\u5FEB\u4E50", "\u65B0\u5E74\u5FEB\u4E50",
        "\u8282\u65E5\u5FEB\u4E50", "\u4E07\u4E8B\u5982\u610F", "\u8003\u8BD5\u987A\u5229",
        "\u795D\u4F60", "\u5927\u559C"
    };
    static final String[] INVITE_WORDS = {
        "\u4E00\u8D77", "\u966A\u6211", "\u7EA6\u5417", "\u8981\u4E0D\u8981\u4E00\u8D77",
        "\u8D70\u5427", "\u6765\u4E0D\u6765", "\u6709\u7A7A\u5417", "\u51FA\u6765\u73A9"
    };
    static final String[] SURPRISE_WORDS = {
        "\u54C7", "\u5929\u5450", "\u5929\u54EA", "\u5C45\u7136", "\u7ADF\u7136",
        "\u4E0D\u4F1A\u5427", "\u771F\u7684\u5047\u7684", "\u60CA\u4E86", "\u6211\u7684\u5929",
        "\u5413\u6B7B"
    };
    static final String[] FEAR_WORDS = {
        "\u597D\u6015", "\u5BB3\u6015", "\u6050\u6016", "\u62C5\u5FC3", "\u7D27\u5F20",
        "\u4E0D\u5B89", "\u614C", "\u5F88\u96BE\u529E"
    };
    static final String[] TIRED_WORDS = {
        "\u597D\u7D2F", "\u7D2F\u6B7B", "\u597D\u56F0", "\u56F0\u6B7B", "\u60F3\u7761",
        "\u6491\u4E0D\u4F4F", "\u75B2\u60EB", "\u6CA1\u529B\u6C14", "\u71AC\u591C",
        "\u52A0\u73ED\u5230\u5F88\u665A"
    };
    static final String[] BORED_WORDS = {
        "\u597D\u65E0\u804A", "\u65E0\u804A\u6B7B", "\u6CA1\u4E8B\u5E72",
        "\u6CA1\u610F\u601D", "\u597D\u95F2", "\u5F85\u7740\u6CA1\u4E8B"
    };
    static final String[] DISAPPOINTED_WORDS = {
        "\u5931\u671B", "\u767D\u9AD8\u5174", "\u767D\u671F\u5F85", "\u7A7A\u6B22\u559C",
        "\u679C\u7136\u4E0D\u884C", "\u7B97\u4E86\u5427", "\u6CA1\u7528\u4E86"
    };
    /** The user is comforting the OTHER person. Checked before 喜爱 and 难过, because
     *  "抱抱你" contains "抱抱" and "别难过" contains "难过". */
    static final String[] COMFORT_WORDS = {
        "\u522B\u96BE\u8FC7", "\u4E0D\u8981\u96BE\u8FC7", "\u522B\u54ED", "\u522B\u4F24\u5FC3",
        "\u6CA1\u4E8B\u7684", "\u4F1A\u597D\u7684", "\u62B1\u62B1\u4F60", "\u522B\u60F3\u592A\u591A",
        "\u5F00\u5FC3\u70B9", "\u6211\u966A\u4F60", "\u6709\u6211\u5728", "\u522B\u6015\u6709\u6211",
        "\u522B\u6015", "\u6211\u5728\u5462"
    };
    /** The user is ASKING for affection - "抱抱我", not "我喜欢你". Checked before 喜爱
     *  because "抱抱我" contains "抱抱". */
    static final String[] CHARM_WORDS = {
        // \u2605 \u7528\u6237 2026-10-17\uff1a\u300c\u4EB2\u6211\u561B\u300d\u5224\u6210\u4E86\u666E\u901A \u2717 \u8865\u9F50 \u2713
        "\u62B1\u62B1\u6211", "\u4EB2\u4EB2\u6211", "\u966A\u966A\u6211", "\u54C4\u6211",
        "\u6478\u6478\u5934", "\u8981\u62B1\u62B1", "\u62B1\u7D27\u6211", "\u60F3\u62B1\u62B1",
        "\u62B1\u62B1", "\u62B1\u4E00\u4E0B", "\u62B1\u6211", "\u8BA9\u6211\u62B1",
        "\u4EB2\u6211", "\u4EB2\u4E00\u4E0B", "\u4EB2\u4EB2", "\u8981\u4EB2\u4EB2",
        "\u60F3\u4EB2\u4EB2", "\u6478\u6478\u6211", "\u6478\u6478", "\u8E6D\u8E6D",
        "\u54C4\u54C4\u6211", "\u966A\u6211", "\u597D\u4E0D\u597D\u561B", "\u6C42\u4F60\u4E86",
        "\u4EBA\u5BB6", "\u8BA8\u538C\u5566", "\u624D\u4E0D\u8981", "\u4E0D\u561B",
        "\u8DDF\u6211\u73A9", "\u5E26\u6211"
    };

    static boolean containsAny(String s, String[] words) {
        for (int i = 0; i < words.length; i++)
            if (s.indexOf(words[i]) >= 0) return true;
        return false;
    }

    /**
     * Order is load-bearing - a message often contains words from several lists:
     *   1. 夸奖 before all          - "你好棒" contains "你好"
     *   2. 感谢/道歉/祝贺 before 喜爱 - they are more specific social acts
     *   3. 喜爱 before 开心          - "喜欢你" contains "喜欢", also a happy word
     *   4. 打招呼 before 疑问        - "你好吗" / "在吗" are greetings
     *   5. 邀请 before 疑问          - "要不要一起" is an invitation
     *   6. 惊讶/害怕/疲惫/无聊/失望 before 开心 - all sit near a mood word
     *   7. 难过/生气 before 开心     - "不开心" contains "开心"
     */
    public static String mood(String s) {
        if (s == null || s.length() == 0) return "plain";
        if (containsAny(s, PRAISE_WORDS)) return "praise";
        if (containsAny(s, THANKS_WORDS)) return "thanks";
        if (containsAny(s, APOLOGY_WORDS)) return "apology";
        if (containsAny(s, CONGRAT_WORDS)) return "congrat";
        // 安慰 before 喜爱 and before 难过 - see COMFORT_WORDS.
        if (containsAny(s, COMFORT_WORDS)) return "comfort";
        if (containsAny(s, CHARM_WORDS)) return "charm";
        if (containsAny(s, LOVE_WORDS)) return "love";
        if (containsAny(s, FAREWELL_WORDS)) return "farewell";
        if (containsAny(s, GREETING_WORDS)) return "greeting";
        if (containsAny(s, INVITE_WORDS)) return "invite";
        if (containsAny(s, SURPRISE_WORDS)) return "surprise";
        if (containsAny(s, FEAR_WORDS)) return "fear";
        if (containsAny(s, TIRED_WORDS)) return "tired";
        if (containsAny(s, BORED_WORDS)) return "bored";
        if (containsAny(s, DISAPPOINTED_WORDS)) return "disappointed";
        if (containsAny(s, QUESTION_WORDS)) return "question";
        if (containsAny(s, SAD_WORDS)) return "sad";
        if (containsAny(s, ANGRY_WORDS)) return "angry";
        if (containsAny(s, HAPPY_WORDS)) return "happy";
        int end = s.length();
        while (end > 0 && Character.isWhitespace(s.charAt(end - 1))) end--;
        if (end > 0 && (s.charAt(end - 1) == '\uFF01' || s.charAt(end - 1) == '!')) return "exclaim";
        return "plain";
    }

    static String pickCloser(List<String> pool) {
        return pick(pool);
    }

    /** a style only has to fill in the pools it cares about; the rest fall back */
    /** 小模型的中文标签 → 引擎内部的语气代号 ✓ */
    static String tinyToMood(String label) {
        if (label == null) return "plain";
        if (label.equals("\u5b89\u6170")) return "comfort";
        if (label.equals("\u611f\u8c22")) return "thanks";
        if (label.equals("\u5206\u522b")) return "farewell";
        if (label.equals("\u5f00\u5fc3")) return "happy";
        if (label.equals("\u7591\u95ee")) return "question";
        if (label.equals("\u751f\u6c14")) return "angry";
        if (label.equals("\u96be\u8fc7")) return "sad";
        if (label.equals("\u7d2f\u4e86")) return "tired";
        if (label.equals("\u559c\u7231")) return "love";
        if (label.equals("\u5938\u5956")) return "praise";
        if (label.equals("\u5bb3\u6015")) return "fear";
        if (label.equals("\u5931\u671b")) return "disappointed";
        return "plain";
    }

    String pickCloserFor(String plain) {
        String m = mood(plain);
        lastMood = m;   // ★ 给大模型用 ✓

        // ★★★ 关键词判不出来时 ✧ 问一下小模型 ★★★
        //
        // 为什么是「关键词优先」：
        //   关键词对【社交语气】（打招呼 / 分别 / 感谢 …）很准 ✧ 而且零成本 ✓
        //   小模型的优势在【看上文才能判的短回复】✧ 比如「好」「嗯」✓
        //   —— 所以只有 mood() 返回 plain（什么都没找到）时才问 ✓
        //   —— ★ 最坏情况 = 回到现在的行为 ✧ 不会更差 ✓
        // \u2605\u2605\u2605 \u7528\u6237 2026-10-17\uff1a\u300c\u628a\u5173\u952e\u8bcd\u6311\u5230\u6700\u540e \u2727 \u6a21\u578b\u51c6\u4e00\u70b9\u300d\u2605\u2605\u2605
        //
        // \u4f46\u4e0d\u80fd\u65e0\u6761\u4ef6\u4f18\u5148\uff1a\u5c0f\u6a21\u578b\u53ea\u80fd\u8868\u8fbe 7 \u79cd
        //   \u5b89\u6170 / \u611f\u8c22 / \u5206\u522b / \u5f00\u5fc3 / \u7591\u95ee / \u666e\u901a / \u751f\u6c14 \
        // \u5b83\u8bf4\u4e0d\u51fa\uff1a\u96be\u8fc7 / \u5bb3\u6015 / \u7d2f\u4e86 / \u5931\u671b / \u559c\u7231 / \u5938\u5956 \
        //   \u2014\u2014 \u65e0\u6761\u4ef6\u8986\u76d6\u4f1a\u628a\u5224\u5bf9\u7684\u964d\u7ea7\u6210\u5b89\u6170/\u666e\u901a \
        //
        // \u73b0\u5728\uff1a\u5173\u952e\u8bcd\u7ed9\u51fa\u7684\u60c5\u7eea\u5c0f\u6a21\u578b\u3010\u4e5f\u80fd\u8868\u8fbe\u3011\u65f6\u624d\u95ee\u5b83 \
        //       \u800c\u4e14\u53ea\u6709\u5b83\u7ed9\u51fa\u3010\u4e0d\u662f plain\u3001\u4e5f\u4e0d\u540c\u3011\u7684\u7b54\u6848\u65f6\u624d\u8986\u76d6 \
        boolean tinyCanSay = m.equals("plain") || m.equals("comfort") || m.equals("thanks")
                          || m.equals("farewell") || m.equals("happy") || m.equals("question")
                          || m.equals("angry");
        if (tinyMood && tinyCanSay) {
            try {
                String tm = TinyClf.classify(chatCtx, plain);
                if (tm != null) {
                    String nm = tinyToMood(tm);
                    // ★ 2026-10-31：骂人话不允许被改成感谢/夸奖/开心 ✓
                    if (looksAngry(plain) && !"angry".equals(nm)) {
                        // ★ 骂人话 ✗ 不采用模型的正面结论 ✗ 用生气池 ✓
                        if (!angryClosers.isEmpty()) return pick(angryClosers);
                        return pick(closers);
                    }
                    // \u2605 plain \u4e0d\u80fd\u628a\u5df2\u5224\u51fa\u7684\u60c5\u7eea\u964d\u7ea7 \u2713
                    if (!nm.equals("plain") && !nm.equals(m)) {
                        m = nm;
                    // \u2605 \u5199\u8fdb\u884c\u4e3a\u65e5\u5fd7 \u2727 \u4e0d\u7136\u7528\u6237\u6839\u672c\u770b\u4e0d\u51fa\u5206\u7c7b\u5668\u6709\u6ca1\u6709\u751f\u6548 \u2713
                    try {
                        Trace.add(Trace.now() + "  \u5c0f\u6a21\u578b\u5224\u65ad\uff1a\u300c"
                                  + Trace.brief(plain) + "\u300d\u2192 " + tm
                                  + "\uff08\u4e0a\u6587 " + (chatCtx == null ? 0 : chatCtx.length()) + " \u5b57\uff09");
                    } catch (Throwable t2) { }
                    }
                }
            } catch (Throwable t) { }
        }
        if (m.equals("praise") && !praiseClosers.isEmpty()) return pick(praiseClosers);
        if (m.equals("thanks") && !thanksClosers.isEmpty()) return pick(thanksClosers);
        if (m.equals("apology") && !apologyClosers.isEmpty()) return pick(apologyClosers);
        if (m.equals("congrat") && !congratClosers.isEmpty()) return pick(congratClosers);
        if (m.equals("love") && !loveClosers.isEmpty()) return pick(loveClosers);
        if (m.equals("farewell") && !farewellClosers.isEmpty()) return pick(farewellClosers);
        if (m.equals("greeting") && !greetingClosers.isEmpty()) return pick(greetingClosers);
        if (m.equals("invite") && !inviteClosers.isEmpty()) return pick(inviteClosers);
        if (m.equals("surprise") && !surpriseClosers.isEmpty()) return pick(surpriseClosers);
        if (m.equals("fear") && !fearClosers.isEmpty()) return pick(fearClosers);
        if (m.equals("tired") && !tiredClosers.isEmpty()) return pick(tiredClosers);
        if (m.equals("bored") && !boredClosers.isEmpty()) return pick(boredClosers);
        if (m.equals("disappointed") && !disappointedClosers.isEmpty()) return pick(disappointedClosers);
        if (m.equals("comfort") && !comfortClosers.isEmpty()) return pick(comfortClosers);
        if (m.equals("charm") && !charmClosers.isEmpty()) return pick(charmClosers);
        if (m.equals("question") && !questionClosers.isEmpty()) return pick(questionClosers);
        if (m.equals("sad") && !sadClosers.isEmpty()) return pick(sadClosers);
        if (m.equals("angry") && !angryClosers.isEmpty()) return pick(angryClosers);
        if (m.equals("happy") && !happyClosers.isEmpty()) return pick(happyClosers);
        if (m.equals("exclaim") && !exclaimClosers.isEmpty()) return pick(exclaimClosers);
        return pick(closers);
    }

    /// 逐步轨迹：每个 pass 之后的字符串。只在 trace 模式下收集，正常跑时是空的。
    ///
    /// 为什么需要它：两个引擎的每一处实现对比下来都逐行相同，光读代码看不出分岔在哪，
    /// 只能一步步打点，再和电脑版的 trace 逐步对齐 —— 第一个分歧的那一步就是答案。
    /// 对比【最终输出】是没有用的：语气风格每次随机挑装饰，两边本来就不可能一样。
    public static final java.util.List<String> TRAIL = new java.util.ArrayList<String>();
    public static boolean TRAILING = false;
    static void tr(String tag, String s) { if (TRAILING) TRAIL.add(tag + " | " + s); }

    /**
     * ★★★ 整段就是【机器用的编号】时 ✧ 什么都别做 ★★★
     *
     * 电脑版 Engine.Run 里已经有这一段 ✧ 安卓一直缺着 ✓
     * 起因：用户游戏里那个中文输入法工具，工作方式是
     *   打中文 → 转成 #121417 这种编号 → 输出到游戏。
     * 把那个编号当普通文字改掉 ✧ 那个工具会发出【错误的指令】✓
     *
     * 这类东西是【给程序读的】✧ 不是给人读的 ✧ 一个字符都不该动 ✓
     */
    static boolean looksLikeMachineCode(String s) {
        if (s == null || s.length() == 0) return false;
        String t = s.trim();
        if (t.length() < 4) return false;
        try {
            if (java.util.regex.Pattern.matches("^[#＃][0-9]{3,}$", t)) return true;
            if (java.util.regex.Pattern.matches("^[A-Za-z]{1,4}[-_#][0-9]{3,}$", t)) return true;
        } catch (Throwable e) {
        }
        return false;
    }

    /**
     * ★ 2026-10-22：删掉一个自定义风格的那一行 ✓
     *   look=true 删「外观风格 = 名字|...」✗ 否则删「语气风格 = ...」✓
     */

    /** ★ 2026-10-22：删掉一个组合的那一行 ✓ */
    public static String removeCombo(String text, String name) {
        if (text == null || name == null) return text;
        String nl = text.contains("\r\n") ? "\r\n" : "\n";
        String[] lines = text.split("\r\n|\n|\r", -1);
        StringBuilder sb = new StringBuilder();
        for (String ln : lines) {
            String t = ln.trim();
            boolean drop = false;
            int eq = t.indexOf('=');
            if (eq > 0) {
                String k = t.substring(0, eq).trim();
                String v = t.substring(eq + 1).trim();
                if ("combo".equals(canonicalKey(k))) {
                    String nm = v.split("\\|", -1)[0].trim();
                    if (nm.equals(name)) drop = true;
                }
            }
            if (!drop) sb.append(ln).append(nl);
        }
        return sb.toString();
    }

    public static String removeCustomStyle(String text, String name, boolean look) {
        if (text == null || name == null) return text;
        String nl = text.contains("\r\n") ? "\r\n" : "\n";
        String[] lines = text.split("\r\n|\n|\r", -1);
        StringBuilder sb = new StringBuilder();
        for (String ln : lines) {
            String t = ln.trim();
            boolean drop = false;
            int eq = t.indexOf('=');
            if (eq > 0) {
                String k = t.substring(0, eq).trim();
                String v = t.substring(eq + 1).trim();
                // \u2605 2026-10-22\uff1a\u5199\u8fdb\u53bb\u53eb\u300c\u81ea\u5b9a\u4e49\u5916\u89c2\u300d\u2717 \u5220\u7684\u65f6\u5019\u53ea\u8ba4\u300c\u5916\u89c2\u98ce\u683c\u300d\u2717 \u6c38\u8fdc\u5220\u4e0d\u6389 \u2713
                String ck = canonicalKey(k);
                if ((look && "lookstyle".equals(ck)) || (!look && "tonestyle".equals(ck))) {
                    String nm = v.split("\\|", -1)[0].trim();
                    if (nm.equals(name)) drop = true;
                }
            }
            if (!drop) sb.append(ln).append(nl);
        }
        return sb.toString();
    }

    public String transform(String input) {
        // \u2605\u2605\u2605 2026-10-23\uff1a@\u63d0\u53ca\u4e0d\u80fd\u78b0 \u2605\u2605\u2605
        //   \u300c@\u61d2\u4eba \u53ea\u80fd\u300d\u76f4\u63a5\u88c5\u9970\u4f1a\u53d8\u6210\u300c@\u61d2\u4eba\u55b5\uff01\u300d\u2717
        //   @\u90a3\u4e00\u4e32\u88ab\u6539\u4e86 \u2717 QQ/\u5fae\u4fe1\u5c31\u4e0d\u518d\u5f53\u5b83\u662f\u63d0\u53ca\u4e86 \u2713
        //   \u6240\u4ee5\uff1a\u5f00\u5934\u7684 @xxx + \u7a7a\u683c \u539f\u6837\u4fdd\u7559 \u2717 \u53ea\u88c5\u9970\u540e\u9762\u90a3\u6bb5 \u2713
        int mEnd = -1;
        if (input != null && input.length() > 1 && input.charAt(0) == '@') {
            int i = 0;
            while (i < input.length() && !Character.isWhitespace(input.charAt(i))) i++;
            if (i < input.length()) mEnd = i + 1;
        }
        if (mEnd > 0) {
            String head = input.substring(0, mEnd);
            String body = input.substring(mEnd);
            if (body.trim().length() == 0) return input;
            return head + transform(body);
        }

        reseed(input);   // \u2605 2026-10-18\uff1a\u540c\u4e00\u53e5\u8bdd\u7ed3\u679c\u56fa\u5b9a \u2713
        if (input == null || input.length() == 0) return input;
        if (looksLikeMachineCode(input)) return input;

        // Peel the wrapper BEFORE the word rules run, not after.
        //
        // The rules can rewrite the wrapper itself: 没有 => 才没有呢 turns the closing line
        // "才没有很高兴呢" into "才才没有呢很高兴呢", the peel then no longer matches, and
        // the decoration gets inserted into the middle of the closing line.
        rememberStyle();   // 登记这一轮用到的装饰素材 —— 换风格后还要靠它认旧装饰
        String s = input;
        tr("000 input", s);

        // Order matters, and this pair is the fix for a bug reported from the phone.
        //
        // PeelSuffix / the "already decorated?" checks all look at the END of the text. The
        // moment the user types one character after a transform, the closing line and the
        // decoration the engine just wrote are stranded in the MIDDLE - invisible to those
        // checks - so the next pass treated its own output as the user's words and appended
        // another one:
        //     你好喵~  +  1  ->  你好喵~1  ->  你好喵~1喵~  ->  你好喵~1喵~2喵~ ...
        // The closing line has to be removed first: while it is still attached the decoration
        // in front of it is followed by punctuation and looks perfectly legitimate.
        s = stripStaleClosers(s);
        tr("001 stripClosers", s);
        s = stripStaleDecorations(s);
        tr("002 stripDecorations#1", s);

        tr("003 peelPrefix in", s);
        String hadOpen = peelPrefix(s, openers);
        if (hadOpen != null) s = s.substring(hadOpen.length());

        String hadClose = peelSuffix(s, closers);
        if (hadClose == null) hadClose = peelSuffix(s, questionClosers);
        if (hadClose == null) hadClose = peelSuffix(s, exclaimClosers);
        // every mood pool has to be peeled too, or the decoration gets inserted into the
        // middle of a closing line that is still attached
        if (hadClose == null) hadClose = peelSuffix(s, happyClosers);
        if (hadClose == null) hadClose = peelSuffix(s, sadClosers);
        if (hadClose == null) hadClose = peelSuffix(s, angryClosers);
        if (hadClose == null) hadClose = peelSuffix(s, greetingClosers);
        if (hadClose == null) hadClose = peelSuffix(s, farewellClosers);
        if (hadClose == null) hadClose = peelSuffix(s, loveClosers);
        if (hadClose == null) hadClose = peelSuffix(s, praiseClosers);
        if (hadClose == null) hadClose = peelSuffix(s, thanksClosers);
        if (hadClose == null) hadClose = peelSuffix(s, apologyClosers);
        if (hadClose == null) hadClose = peelSuffix(s, congratClosers);
        if (hadClose == null) hadClose = peelSuffix(s, inviteClosers);
        if (hadClose == null) hadClose = peelSuffix(s, surpriseClosers);
        if (hadClose == null) hadClose = peelSuffix(s, fearClosers);
        if (hadClose == null) hadClose = peelSuffix(s, tiredClosers);
        if (hadClose == null) hadClose = peelSuffix(s, boredClosers);
        if (hadClose == null) hadClose = peelSuffix(s, disappointedClosers);
        if (hadClose == null) hadClose = peelSuffix(s, comfortClosers);
        if (hadClose == null) hadClose = peelSuffix(s, charmClosers);
        tr("004 peelSuffix(close=" + (hadClose != null) + ") in", s);
        if (hadClose != null) s = s.substring(0, s.length() - hadClose.length());
        tr("005 afterTruncateCloser", s);

        // ② 剥完包装之后【再清一次】。电脑版有这一步、安卓没有 —— 逐步 trace 对比
        // （Rules.TRAIL + RulesCheck 的 trace 参数）唯一报出来的结构差异就是它。
        //
        // 原因：上面所有"是否已经装饰过"的判断看的都是【结尾】，用户只要在转化之后再打一个字，
        // 引擎刚写的那段装饰就被顶到了中间，那些判断全都看不见它，于是下一轮把自己的产物当成
        // 用户的话，又追加一段：
        //     你好喵~  +  1  ->  你好喵~1  ->  你好喵~1喵~  ->  你好喵~1喵~2喵~ ...
        // 剥包装之前清一次是不够的：那时关闭语还挂在装饰后面，装饰后面跟着标点，看起来完全
        // 正当。只有等关闭语被剥掉，装饰才直接挡在用户新打的字前面，这一趟才看得见它。
        s = stripStaleDecorations(s);
        tr("006 stripDecorations#2", s);


        tr("007 applyRules", s);
        s = applyRules(s, rules, chatCtx == null ? "" : chatCtx);

        // Sentence type is read from the text WITHOUT re-decorating it, so the closing
        // punctuation is still the last character that matters.
        tr("008 decorateAll", s);
        String plain = s;

        if (!(kaomoji.isEmpty() && suffix.length() == 0)) {
            List<String> decorations = buildDecorations();
            if (eachSentence) {
                s = perSentence(s, decorations);
            } else {
                int end = s.length();
                while (end > 0 && Character.isWhitespace(s.charAt(end - 1))) end--;
                String body = s.substring(0, end);
                String tail = s.substring(end);
                // ★★★ 轻度路径：用户把标点补在颜文字后面 ✧ 挪到前面 ★★★
                String movedBody = null;
                {
                    String cb = splitTrailingPunct(body);
                    if (cb.length() < body.length()) {
                        movedBody = movePunctInsideDecoration(cb, body.substring(cb.length()), decorations);
                    }
                }

                if (movedBody != null) {
                    s = movedBody + tail;
                } else if (body.length() > 0 && hasTypableContent(body)
                        && isAllSuffix(stripPunct(body), new String[1])) {
                    // ★ 整段全是喵（可能带标点 ✓）✧ 保留个数 ✓
                    String pj = trailingPunct(body);
                    String coreJ = body.substring(0, body.length() - pj.length());
                    String[] tj = new String[1];
                    isAllSuffix(coreJ, tj);
                    String t1 = (tj[0].length() > 0 && coreJ.endsWith(tj[0])) ? "" : tj[0];
                    String kaoJ = kaoOnly();
                    s = placeDecoration(coreJ + t1 + kaoJ, kaoJ, pj,
                                        isEmotionalPunct(pj), suffix) + tail;
                } else if (body.length() > 0 && hasTypableContent(body)
                        && !endsWithPunctInsideDecoration(body)
                        && !endsWithAnyIn(body, allCloserLines())
                        && !endsWithSkip(body) && !endsWithAnyIn(body, decorations)
                        // A PARTIALLY deleted decoration counts as decorated too. Exact
                        // matching is not enough: trim one character out of a kaomoji and the
                        // tail matches nothing, so it reads as ordinary prose and a second
                        // decoration goes on behind the broken one -
                        //   你好喵~(´･ω･`) -> delete ´ -> 你好喵~(･ω･`) -> 你好喵~(･ω･`)喵~ฅ^•ﻌ•^ฅ
                        // The per-sentence path has had this via alreadyDecorated for a long
                        // time; this one never did, which is why it only showed up in 轻度.
                        && !suffixTailIsDecoration(body, Math.max(0, body.length() - 24),
                                                         Math.min(24, body.length()))) {
                    // ★ 轻度路径的 body 里【包含句尾标点】✧
                    //   这条路径没有 emitSegment 那个"把标点切下来"的步骤 ✓
                    //   —— 所以情绪判断要在这里补 ✓
                    int tEnd = body.length();
                    while (tEnd > 0 && isPunctLike(body.charAt(tEnd - 1))) tEnd--;
                    String lastPunct = body.substring(tEnd);

                    // ★★★ 标点放在"后缀"和"颜文字"【之间】★★★
                    //
                    // 用户 2026-10-06 选的：
                    //   你好！ → 你好喵！(=≧∇≦=)
                    //   你好。 → 你好喵~。(=≧∇≦=)
                    // —— 逐句模式【不动】✧ 那边颜文字在标点前面更好读 ✓
                    if (lastPunct.length() > 0) {
                        boolean emo2 = isEmotionalPunct(lastPunct);
                        String deco2 = decorate(emo2);
                        s = body.substring(0, tEnd)
                          + placeDecoration(deco2, lastKaomoji, lastPunct, emo2, suffix) + tail;
                    } else {
                        s = body + decorate(false) + tail;
                    }
                }
            }
        }

        // Keep whatever was already there instead of drawing again. Re-picking would
        // change the message on every trigger, and the service writes back whenever the
        // result differs - so the text would churn under the user for no reason.
        // 语气 = false strips any wrapper the text already carried (the peel above still
        // ran) but never adds one. That way switching to 只加末尾 on text that already has a
        // wrapper cleans it up instead of decorating on top of the old closing line.
        // 内容是纯网址（或整段都是受保护内容）时，装饰那一步已经跳过了，这里也必须跳过 ——
        // 否则第一遍只加语气包装、不加装饰；第二遍因为文本"已经不只是网址了"，装饰又加上了。
        //   输入  https://example.com/a?b=1&c=2
        //   一遍  别、别自作多情了！https://…，……其实我，也不是不想听啦
        //   两遍  别、别自作多情了！https://…喵！(｀・ω・´)，……其实我，也不是不想听啦
        // ★★★ 这里要和装饰那一步用同一个判断 ★★★
        //   不然 "123" 第一遍只装饰、第二遍又补包装 ✧ 不幂等 ✓
        boolean wrappable = hasTypableContent(plain);
        String open = (!tone || !wrappable) ? "" : (hadOpen != null ? hadOpen : pick(openers));
        String close = (!tone || !wrappable) ? "" : (hadClose != null ? hadClose : pickCloserFor(plain));

        if (open.length() > 0) s = open + s;
        if (close.length() > 0) s = s + close;
        return s;
    }
}
