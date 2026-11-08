package com.miaohua.helper;

import android.app.Activity;
import android.content.Intent;
import android.animation.Animator;
import android.animation.ValueAnimator;
import android.content.res.ColorStateList;
import android.view.animation.AccelerateInterpolator;
import android.view.animation.DecelerateInterpolator;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.provider.Settings;
import android.text.Editable;
import android.text.InputType;
import android.text.TextWatcher;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.CheckBox;
import android.widget.CompoundButton;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

/*
 * NOTE FOR WHOEVER EDITS THIS LATER
 *
 * Do NOT use anonymous inner classes (new View.OnClickListener() { ... }) anywhere
 * in this project, and do not add lambdas.
 *
 * Two ways they break d8 (build-tools 34, R8 8.2.2) with an internal
 * NullPointerException on a null String:
 *
 *   1. an anonymous class declared inside an *instance* method gets a synthetic
 *      constructor parameter for the captured outer instance;
 *   2. an anonymous class implementing a *generic* interface (Comparator<String>,
 *      for example) gets a synthetic bridge method - and a lambda desugars into
 *      the same kind of synthetic class.
 *
 * Both are emitted in the MethodParameters attribute with a null parameter name,
 * and d8 crashes on that. That is why this Activity implements its own listeners
 * and dispatches on the view reference, and why Rules.java sorts by hand.
 */
public class MainActivity extends Activity implements View.OnClickListener, TextWatcher {

    // ---- palette, matching the desktop app
    static final int BG     = 0xFFFFF6F9;
    static final int CARD   = 0xFFFFFFFF;
    static final int ACCENT = 0xFFF06292;
    static final int TEXT   = 0xFF3A2A34;
    static final int MUTED  = 0xFF967A86;
    static final int LINE   = 0xFFF4DEE8;
    static final int SOFT   = 0xFFFFEDF4;   // 粉白底，用来替掉状态卡的绿色
    static final int OK     = 0xFF2E7D32;
    static final int OK_BG  = 0xFFE9F6EA;
    static final int WARN   = 0xFFC62828;
    static final int WARN_BG= 0xFFFDECEA;
    static final int WARN_TX= 0xFF8A4B2A;
    static final int OK_TX  = 0xFF356B3B;
    static final int SEL_BG = 0xFFFCE4EE;

    LinearLayout statusCard;
    // ⑥ 首页等级卡片上的几个控件
    TextView statsLevel;
    TextView statsToday;
    TextView statsTotal;
    TextView statsHint;
    /** ★ 2026-10-18：最近喵化列表 ✓ */
    LinearLayout recentBox;
    android.widget.ProgressBar statsBar;
    TextView headerMood;
    TextView headerSub;
    TextView statusView;
    TextView statusHint;
    // ⑦ 动画时长：预设按钮 + 一个自由输入框
    EditText setSlidePctField;      // 滑块比例（%），100 = 和淡化一样长
    EditText setDelayField;
    EditText setFadeField;
    TextView[] fadeButtons;
    EditText testIn;
    TextView testOut;
    EditText rulesBox;

    TextView btnOpen;
    TextView btnSave;
    TextView btnDefaults;
    /** \u2605 2026-10-18\uff1a\u914d\u7f6e\u5bfc\u51fa / \u5bfc\u5165 \u2713 */
    TextView btnExport;
    TextView btnImport;
    static final int REQ_EXPORT = 4001;
    static final int REQ_IMPORT = 4002;

    // style buttons, kept so the chosen one can be highlighted
    TextView[] lookButtons;
    TextView[] toneButtons;
    // display name of whatever was last applied, remembered across launches
    String appliedLook;
    String appliedTone;

    static final String PREF = "miaohua_style";

    Rules cached;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        Trace.init(getFilesDir());   // 记录落盘，方便排查
        // ★ 播种：把素材目录里的每一个自带风格都登记给引擎。
        //
        // 为什么：剥旧装饰、剥旧关闭语那两步只看【当前配置】。用户一切换风格，
        // 上一轮用旧风格写下的装饰就认不出来了 —— 会被当成他自己写的话，
        // 新一轮把整句再包一遍，越用越叠。电脑版有同样的问题，一起修的。
        //
        // 遍历素材目录而不是照着风格表来：风格表有两张（外观 / 语气）、形状还不一样，
        // 素材文件本身就是引擎认得的那种文本，直接解析最省事也最不容易漏。
        try {
            String[] assets = getAssets().list("");
            for (int i = 0; assets != null && i < assets.length; i++) {
                String fn = assets[i];
                if (!fn.endsWith(".txt")) continue;
                if (fn.equals("changelog.txt")) continue;
                try {
                    Rules seed = Rules.parse(readAsset(fn));
                    if (seed.suffix == null || seed.suffix.length() == 0) continue;
                    Rules.seedKnown(seed.suffix,
                                    seed.kaomoji.toArray(new String[0]),
                                    seed.allCloserLines().toArray(new String[0]));
                } catch (Exception ignored) { }
            }
        } catch (Exception ignored) { }
        super.onCreate(savedInstanceState);
        setContentView(buildUi());

        String text = Rules.storedText(this);
        rulesBox.setText(text);
        cached = Rules.parse(text);
        loadStyleSelection();
        updateStatus();
    }

    @Override
    protected void onResume() {
        super.onResume();
        updateStatus();
        refreshStats();     // 从 QQ 切回来时数字要跟着变
        // 导航高亮补摆一次：buildUi 里第一次 syncPages 时导航条还没量出宽度，
        // 摆不了。onResume 一定在布局之后，是补摆的好时机。
        //
        // ★ 这里【没有】用 View.OnLayoutChangeListener —— 那个接口有 9 个参数，
        //   build-tools 34.0.0 里那个 d8(8.2.2-dev) 对它内部 NPE：
        //     Cannot invoke "String.length()" because "<parameter1>" is null
        //   是 d8 自己的 bug，不是代码错。换一条路绕开。
        moveTabHighlight(false);
    }

    // ------------------------------------------------ helpers

    int dp(float v) {
        return (int) (v * getResources().getDisplayMetrics().density + 0.5f);
    }

    GradientDrawable rounded(int fill, int stroke, float radiusDp, float strokeDp) {
        GradientDrawable d = new GradientDrawable();
        d.setShape(GradientDrawable.RECTANGLE);
        d.setColor(fill);
        d.setCornerRadius(dp(radiusDp));
        if (strokeDp > 0) d.setStroke(dp(strokeDp), stroke);
        return d;
    }

    TextView card() {
        TextView v = new TextView(this);
        v.setBackground(rounded(CARD, LINE, 14, 1));
        v.setPadding(dp(16), dp(14), dp(16), dp(14));
        return v;
    }

    TextView sectionLabel(String s) {
        TextView t = new TextView(this);
        t.setText(s);
        t.setTextSize(13);
        t.setTextColor(MUTED);
        t.setPadding(dp(4), dp(14), 0, dp(6));
        return t;
    }

    TextView makeButton(String text, boolean primary) {
        TextView b = new TextView(this);
        b.setText(text);
        b.setTextSize(15);
        b.setGravity(Gravity.CENTER);
        b.setPadding(dp(18), dp(12), dp(18), dp(12));
        b.setTextColor(primary ? Color.WHITE : TEXT);
        b.setBackground(rounded(primary ? ACCENT : CARD, LINE, 12, primary ? 0 : 1));
        b.setClickable(true);
        b.setFocusable(true);
        b.setOnClickListener(this);
        return b;
    }

    EditText makeField(int minLines) {
        EditText e = new EditText(this);
        e.setBackground(rounded(CARD, LINE, 12, 1));
        e.setPadding(dp(12), dp(10), dp(12), dp(10));
        e.setTextSize(15);
        e.setTextColor(TEXT);
        e.setHintTextColor(0xFFC4AEB8);
        e.setGravity(Gravity.TOP | Gravity.START);
        e.setMinLines(minLines);
        e.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_MULTI_LINE);
        return e;
    }

    void tintCheck(CompoundButton c) {
        c.setButtonTintList(new ColorStateList(
                new int[][] { new int[] { android.R.attr.state_checked }, new int[] { } },
                new int[] { ACCENT, 0xFFCFC0C6 }));
    }

    CheckBox makeCheck(String text) {
        CheckBox c = new CheckBox(this);
        c.setText(text);
        c.setTextSize(14);
        c.setTextColor(TEXT);
        c.setPadding(dp(2), dp(6), 0, dp(6));
        tintCheck(c);
        return c;
    }

    // ------------------------------------------------ layout

    // Bottom tabs, because a phone screen is tall and narrow: the PC rail would not fit
    // sideways and one long scroll made everything hard to find.
    static final String[] TABS = { "试一下", "风格", "规则", "设置", "我的" };

    // 底部导航图标 —— 和电脑版【同一套字符】。
    //
    // 电脑版 Ui.cs 里的 glyphsCat 是：▷ ≡ ♡ ☺ ⚙ ▦ ✦ ?
    // 对应它的八页：试一下 / 喵化规则 / 喵化风格 / 颜文字库 / 设置 / 使用统计 / 更新公告 / 关于
    // 安卓这五页按【意思】对上：试一下=▷  风格=♡  规则=≡  设置=⚙  我的=?（电脑版的"关于"就是 ?）
    //
    // 之前用的是 android.R.drawable 里的系统图标 —— 能用，但和电脑版不是一套东西，
    // 放在一起看像两个软件。用字符就没有这个问题，而且照样不引第三方资源。
    static final String[] TAB_GLYPHS = { "\u25B7", "\u2661", "\u2261", "\u2699", "?" };

    // 每个 tab 两套图标：未选中（灰）和选中（粉）。
    // 字符画成位图之后颜色就固定了，所以得预先画两张，切页时换。
    android.graphics.drawable.Drawable[] tabIconOff;
    android.graphics.drawable.Drawable[] tabIconOn;

    /// 把一个字符画成指定颜色的方形小图，用来当导航图标。
    android.graphics.drawable.Drawable glyphIcon(String g, int color, int sizePx) {
        android.graphics.Bitmap bm = android.graphics.Bitmap.createBitmap(
                sizePx, sizePx, android.graphics.Bitmap.Config.ARGB_8888);
        android.graphics.Canvas cv = new android.graphics.Canvas(bm);
        android.graphics.Paint pt = new android.graphics.Paint(android.graphics.Paint.ANTI_ALIAS_FLAG);
        pt.setTextSize(sizePx * 0.86f);
        pt.setTextAlign(android.graphics.Paint.Align.CENTER);
        pt.setColor(color);
        // 垂直居中：用字体度量算基线，直接画在 sizePx/2 会偏上
        android.graphics.Paint.FontMetrics fm = pt.getFontMetrics();
        cv.drawText(g, sizePx / 2f, sizePx / 2f - (fm.ascent + fm.descent) / 2f, pt);
        android.graphics.drawable.BitmapDrawable bd =
                new android.graphics.drawable.BitmapDrawable(getResources(), bm);
        bd.setBounds(0, 0, sizePx, sizePx);
        return bd;
    }
    static final int TAB_TAG = 2000;

    // ★ FrameLayout 而不是 LinearLayout —— 五个页面要互相【叠加】才能交叉淡化。
    // 用 LinearLayout 时两页会上下排，新页被挤到下半屏，旧页隐藏后再跳回上面。
    FrameLayout contentHost;
    TextView[] tabButtons;
    View[] pages;
    /** ★ 2026-10-18：收藏颜文字按钮 ✓ */
    TextView favKaoBtn;
    /** \u2605 2026-10-18\uff1a\u6211\u7684\u7ec4\u5408 \u2713 */
    LinearLayout comboRow;
    TextView saveComboBtn;
    /** \u2605 2026-10-18\uff1a\u81ea\u5b9a\u4e49\u8bed\u6c14 \u2713 */
    TextView newToneBtn;
    final java.util.List<TextView> customToneButtons = new java.util.ArrayList<TextView>();
    LinearLayout customToneRow;
    /** ★ 2026-10-18：自定义外观 ✓ */
    TextView newLookBtn;
    LinearLayout customLookRow;
    int currentTab = 0;

    View buildUi() {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(BG);

        // ★ 必须是 FrameLayout，不能是 LinearLayout。
        //
        // 五个页面在竖直 LinearLayout 里是【上下排】的。交叉淡化需要新旧两页
        // 同时可见，于是两页叠着排 —— 新页被挤到下半屏，等旧页终于 GONE 之后
        // 再"跳"回上面。用户看到的就是：
        //   "新界面直接显示在下半面，动画时间过完（但没有动画）再跑到上面"
        // FrameLayout 里两页占同一个位置，才是真正的交叉淡化。
        contentHost = new FrameLayout(this);
        contentHost.setLayoutParams(new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));

        pages = new View[] {
            page(buildHeader(), buildStatusCard(), buildStatsCard(), buildTestCard()),
            page(sectionLabel("喵化风格"), buildStyleCard()),
            page(buildRulesCard(), buildButtons()),
            page(buildSettingsCard()),
            page(buildNote())
        };
        for (int i = 0; i < pages.length; i++)
            contentHost.addView(pages[i], new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        root.addView(contentHost);
        root.addView(buildTabBar());
        syncPages();
        return root;
    }

    /** One page = a scrolling column of cards, padded the same way everywhere. */
    ScrollView page(View... children) {
        LinearLayout col = new LinearLayout(this);
        col.setOrientation(LinearLayout.VERTICAL);
        col.setPadding(dp(16), dp(16), dp(16), dp(20));
        for (int i = 0; i < children.length; i++) col.addView(children[i]);

        ScrollView sc = new ScrollView(this);
        sc.setBackgroundColor(BG);
        sc.addView(col);
        return sc;
    }

    View buildTabBar() {
        // ④ 导航滑块：导航条改成 FrameLayout —— 底下铺五个 tab，上面浮一块高亮。
        // 切页时高亮用 translationX 从上一项滑到下一项（先快后慢）。
        //
        // 电脑版那边是把滑块画在父容器里（当时把滑块做成独立控件，结果压住的那一项
        // 文字被擦掉了 —— 那是 WinForms 透明度的毛病）。安卓的透明是真的，浮一层没问题。
        FrameLayout bar = new FrameLayout(this);
        bar.setBackgroundColor(CARD);
        bar.setPadding(dp(6), dp(6), dp(6), dp(6));
        tabBar = bar;

        tabHighlight = new View(this);
        tabHighlight.setBackground(rounded(TAB_ON, TAB_ON, 10, 0));
        FrameLayout.LayoutParams hlp = new FrameLayout.LayoutParams(0, 0);
        tabHighlight.setLayoutParams(hlp);
        bar.addView(tabHighlight);

        LinearLayout rowTabs = new LinearLayout(this);
        rowTabs.setOrientation(LinearLayout.HORIZONTAL);
        bar.addView(rowTabs, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        tabButtons = new TextView[TABS.length];
        for (int i = 0; i < TABS.length; i++) {
            TextView t = new TextView(this);
            t.setText(TABS[i]);

            // 图标 + 文字竖排。用【字符画的图】，和电脑版同一套。
            // setCompoundDrawables 要求 drawable 先设好 bounds，否则会按原图大小画。
            int iconSz = dp(19);
            if (tabIconOff == null) {
                tabIconOff = new android.graphics.drawable.Drawable[TABS.length];
                tabIconOn = new android.graphics.drawable.Drawable[TABS.length];
            }
            tabIconOff[i] = glyphIcon(TAB_GLYPHS[i], MUTED, iconSz);
            tabIconOn[i] = glyphIcon(TAB_GLYPHS[i], ACCENT, iconSz);
            t.setCompoundDrawables(null, tabIconOff[i], null, null);
            t.setCompoundDrawablePadding(dp(3));
            t.setTextSize(12);
            t.setTextSize(13);
            t.setGravity(Gravity.CENTER);
            t.setPadding(0, dp(10), 0, dp(10));
            t.setClickable(true);
            t.setFocusable(true);
            // tagged above the style indexes so the single onClick can tell them apart
            t.setTag(Integer.valueOf(TAB_TAG + i));
            t.setOnClickListener(this);

            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                    0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
            lp.rightMargin = dp(i == TABS.length - 1 ? 0 : 4);
            t.setLayoutParams(lp);
            rowTabs.addView(t);
            tabButtons[i] = t;
        }

        // ★ 布局完成后再摆一次 ✧ 不然首次进来滑块不显示 ✓
        try {
            bar.getViewTreeObserver().addOnGlobalLayoutListener(new TabLayoutListener(this));
        } catch (Throwable t) { }

        return bar;
    }

    static final int TAB_ON = 0xFFFCE4EE;

    /// 切页淡化的时长 —— 用户可以在「设置 → 界面动画」里改，存 SharedPreferences。
    ///
    /// 沿革：安卓第一版 170ms（用户："太快了我都看不清"）→ 400ms（对齐电脑版，
    /// 电脑版原话是"160ms 太短，眼睛读成'啪一下出现'"，它自己也是从 160 改到 400 的）
    /// → 用户还是觉得短 → 改成可配置，默认 600。
    ///
    /// 手机屏比电脑小、内容却更密，同样的时长看起来更快，所以默认比电脑版长一档。
    static final int FADE_DEFAULT = 600;
    static final int FADE_MIN = 100;
    static final int FADE_MAX = 3000;
    static final int[] FADE_PRESETS = { 300, 400, 600, 800, 1200 };

    /// 应用一个淡化时长：写进设置、更新按钮高亮、提示一下。
    void applyFadeMs(int ms) {
        int v = Math.max(FADE_MIN, Math.min(FADE_MAX, ms));
        prefs().edit().putInt("fade_ms", v).apply();
        syncFadeButtons();
        Toast.makeText(this, "动画时长已设为 " + v + " 毫秒" + (v == 600 ? "（默认）" : ""),
                Toast.LENGTH_SHORT).show();
    }

    /// 把预设按钮的高亮和输入框的内容对齐到当前值。
    void syncFadeButtons() {
        if (fadeButtons != null) {
            int cur = fadeMs();
            for (int i = 0; i < fadeButtons.length; i++) {
                boolean on = Math.abs(FADE_PRESETS[i] - cur) < 1;
                // ★★★ 和风格按钮统一 ★★★
                //   选中：浅粉底 + 强调色边框 2px + 强调色文字 + 前面一个 ✓
                String lbl = FADE_PRESETS[i] + " 毫秒";
                fadeButtons[i].setText(on ? "✓ " + lbl : lbl);
                fadeButtons[i].setTextColor(on ? ACCENT : TEXT);
                fadeButtons[i].setBackground(rounded(on ? SEL_BG : CARD,
                                                     on ? ACCENT : LINE, 12, on ? 2 : 1));
            }
        }
        if (setFadeField != null) setFadeField.setText(String.valueOf(fadeMs()));
    }

    /// 滑块比例（%）。和电脑版的 界面-滑块比例 同一个意思，范围 10 - 300。
    int slidePct() {
        int v = prefs().getInt("slide_pct", 100);
        return Math.max(10, Math.min(300, v));
    }

    /// 读用户设定的淡化时长。
    int fadeMs() {
        int v = prefs().getInt("fade_ms", FADE_DEFAULT);
        return Math.max(FADE_MIN, Math.min(FADE_MAX, v));
    }

    /// 导航滑块滑动的时长。跟着淡化走（约 2/3），这样两个动画不会一个拖一个赶。
    int slideMs() {
        // 53% 这个比例来自电脑版：它原来的滑块是 14 帧 x 15ms = 210ms，
        // 而淡化是 22 帧 x 18ms = 396ms，210/396 ≈ 0.53。
        // 两边用同一个比例，调完看起来才是一套动画。
        // 时长 = 淡化 x 用户设的比例%（默认 100 = 一样长）。
        // 53 就是最早那个手感（滑块先到位），也可以填 150 让它更慢。
        return Math.max(60, fadeMs() * slidePct() / 100);
    }

    /// 淡出结束后把旧页面收起来。
    ///
    /// 用【具名静态内部类】而不是匿名内部类 —— 早先踩过匿名内部类让 d8 崩掉的坑，
    /// 那个坑排查了很久。
    static class PageHider implements Runnable {
        final View v;
        PageHider(View v) { this.v = v; }
        public void run() {
            v.setAlpha(1f);
            v.setVisibility(View.GONE);
        }
    }

    /// 导航条和它上面那块滑动的高亮。
    FrameLayout tabBar;
    View tabHighlight;

    /// 把高亮块摆到当前 tab 上。animate=true 时滑过去，false 时直接落位。
    ///
    /// 宽度和位置都取自【那个 tab 自己的实际几何】——
    /// tab 之间还有 4dp 间隔，直接用"总宽 ÷ 5"会和文字对不齐。
    /// ★★★ 布局完成后再摆一次滑块 ★★★
    ///
    /// 为什么需要：
    ///   moveTabHighlight 开头有一句 `if (w <= 0) return;` ✧
    ///   刚进软件时宽度还是 0 ✧ 它就直接返回了 ✓
    ///   而之后【没人再叫它一次】✧ 滑块就一直不显示 ✓
    ///   —— 点一下别的页 ✧ 才又调了一次 ✧ 那时宽度有了 ✧ 就出来了 ✓
    /// （用户 2026-10-07："刚进软件的时候滑块不会显示，要点一下其他界面切换才会出来" ✓）
    ///
    /// 用命名类 ✧ d8 在匿名内部类上会崩 ✓
    static class TabLayoutListener implements android.view.ViewTreeObserver.OnGlobalLayoutListener {
        private final MainActivity act;
        TabLayoutListener(MainActivity a) { act = a; }
        @Override public void onGlobalLayout() {
            act.moveTabHighlight(false);
            if (act.tabBar != null && act.tabBar.getWidth() > 0) {
                try {
                    act.tabBar.getViewTreeObserver().removeOnGlobalLayoutListener(this);
                } catch (Throwable t) { }
            }
        }
    }

    void moveTabHighlight(boolean animate) {
        if (tabBar == null || tabHighlight == null) return;
        if (currentTab < 0 || currentTab >= tabButtons.length) return;
        TextView cur = tabButtons[currentTab];
        if (cur == null) return;
        int w = cur.getWidth();
        if (w <= 0 || tabBar.getWidth() <= 0) return;   // 还没量过，等布局回调再摆
        // ★★★ 不能再加 paddingLeft ★★★
        //
        //   tabHighlight 本身就是 tabBar 的子控件 ✧
        //   FrameLayout 已经把它放在 paddingLeft 的位置了 ✓
        //   手动再加一次 ✧ 就多偏了 6dp ✓
        // （用户 2026-10-07："底下这个滑块位置好像不是特别居中，好像是往右偏了" ✓）
        int x = cur.getLeft();

        ViewGroup.LayoutParams lp = tabHighlight.getLayoutParams();
        if (lp.width != w || lp.height != cur.getHeight()) {
            lp.width = w;
            lp.height = cur.getHeight();
            tabHighlight.setLayoutParams(lp);
        }
        tabHighlight.animate().cancel();
        if (animate) {
            tabHighlight.animate().translationX(x).setDuration(slideMs())
                    .setInterpolator(new android.view.animation.DecelerateInterpolator()).start();
        } else {
            tabHighlight.setTranslationX(x);
        }
    }

    /// 上一次显示的是哪一页。交叉淡化需要知道"从哪一页淡出"。
    int shownTab = -1;

    /// 切页 + 刷新导航。
    ///
    /// 页面用【交叉淡化】：新页从透明淡入，旧页同时淡出，淡完收起。
    /// 之前是直接 setVisibility(VISIBLE/GONE) —— 硬切，一闪。
    /// 电脑版那边的做法是抓两张位图混合，安卓有现成的 ViewPropertyAnimator，
    /// 不用自己画位图。
    void syncPages() { syncPages(true); }

    void syncPages(boolean animateTabs) {
        int from = shownTab;
        int to = currentTab;

        for (int i = 0; i < pages.length; i++) {
            View v = pages[i];
            v.animate().cancel();          // 上一次的动画可能还在跑
            if (i == to) {
                if (from == to || from < 0) {
                    v.setAlpha(1f);
                    v.setVisibility(View.VISIBLE);
                } else {
                    v.setAlpha(0f);
                    v.setVisibility(View.VISIBLE);
                    v.animate().alpha(1f).setDuration(fadeMs()).start();
                }
            } else if (i == from) {
                v.animate().alpha(0f).setDuration(fadeMs())
                 .withEndAction(new PageHider(v)).start();
            } else {
                v.setAlpha(1f);
                v.setVisibility(View.GONE);
            }
        }
        shownTab = to;

        // 高亮块滑过去（首次布局还没完成时它量不到宽度，那时直接摆好不滑）
        moveTabHighlight(animateTabs);
        for (int i = 0; i < tabButtons.length; i++) {
            boolean on = i == currentTab;
            tabButtons[i].setTextColor(on ? ACCENT : MUTED);
            // tab 自身背景透明 —— 高亮由下面那块滑动的 View 提供，
            // 这样才能"一整块滑过去"而不是"两个亮块同时存在"。
            tabButtons[i].setBackgroundColor(android.graphics.Color.TRANSPARENT);
            // 图标也跟着变粉 —— 电脑版那边图标和文字是同一个颜色。
            if (tabIconOff != null && tabIconOn != null) {
                tabButtons[i].setCompoundDrawables(null,
                        on ? tabIconOn[i] : tabIconOff[i], null, null);
            }
        }
    }

    View buildHeader() {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);

        ImageView logo = new ImageView(this);
        logo.setImageResource(R.mipmap.ic_launcher);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(dp(60), dp(60));
        logo.setLayoutParams(lp);
        row.addView(logo);

        LinearLayout col = new LinearLayout(this);
        col.setOrientation(LinearLayout.VERTICAL);
        col.setPadding(dp(14), 0, 0, 0);
        // 给文字列一个权重：占满头像右边的剩余宽度，长句子会自己换行。
        // 没有这一句时是 WRAP_CONTENT，窄屏上文字会把头像挤出去或直接溢出。
        col.setLayoutParams(new LinearLayout.LayoutParams(0,
                ViewGroup.LayoutParams.WRAP_CONTENT, 1f));

        // ① 这里原来写的是「喵化助手」，和系统标题栏重复了 —— 去掉。
        // ② 换成猫娘台词 + 一行状态，头像放大到 60dp，头部就有了主次。
        headerMood = new TextView(this);
        headerMood.setTextSize(21);
        headerMood.setTypeface(null, android.graphics.Typeface.BOLD);
        headerMood.setTextColor(ACCENT);
        col.addView(headerMood);

        headerSub = new TextView(this);
        headerSub.setTextSize(13);
        headerSub.setTextColor(MUTED);
        headerSub.setPadding(0, dp(3), 0, 0);
        col.addView(headerSub);

        row.addView(col);
        return row;
    }

    View buildStatusCard() {
        // The card looks genuinely different in the two states, not just a recoloured line
        // of text: red panel + call to action while the permission is missing, pink-white
        // panel once it is granted (it used to be green, which read like "warning cleared"
        // rather than "you are good to go").
        statusCard = new LinearLayout(this);
        statusCard.setOrientation(LinearLayout.VERTICAL);
        statusCard.setPadding(dp(16), dp(14), dp(16), dp(14));

        statusView = new TextView(this);
        statusView.setTextSize(17);
        statusView.setTypeface(null, android.graphics.Typeface.BOLD);
        statusCard.addView(statusView);

        statusHint = new TextView(this);
        statusHint.setTextSize(13);
        statusHint.setPadding(0, dp(6), 0, 0);
        statusCard.addView(statusHint);

        btnOpen = makeButton("打开无障碍设置", true);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.topMargin = dp(12);
        btnOpen.setLayoutParams(lp);
        statusCard.addView(btnOpen);

        updateStatus();
        return statusCard;
    }

    /// ⑥ 首页的「Lv. 喵化等级 / 今日喵化次数」。
    ///
    /// 数据来自 Stats（对照电脑版的 Stats 类写的）：
    ///     每 40 次升一级     Level = 1 + Total / 40
    ///     换一天今日清零     和电脑版 Bump() 同一个规则
    /// 电脑版还显示"最常用颜文字"，首页这一项不需要，所以没搬。
    View buildStatsCard() {
        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setBackground(rounded(CARD, LINE, 14, 1));
        card.setPadding(dp(16), dp(12), dp(16), dp(12));

        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);

        statsLevel = new TextView(this);
        statsLevel.setTextSize(20);
        statsLevel.setTypeface(null, android.graphics.Typeface.BOLD);
        statsLevel.setTextColor(ACCENT);
        row.addView(statsLevel);

        statsToday = new TextView(this);
        statsToday.setTextSize(13);
        statsToday.setTextColor(TEXT);
        statsToday.setPadding(dp(14), 0, 0, 0);
        row.addView(statsToday);

        statsTotal = new TextView(this);
        statsTotal.setTextSize(13);
        statsTotal.setTextColor(MUTED);
        statsTotal.setPadding(dp(10), 0, 0, 0);
        row.addView(statsTotal);

        card.addView(row);

        statsBar = new android.widget.ProgressBar(
                this, null, android.R.attr.progressBarStyleHorizontal);
        statsBar.setMax(Stats.PER_LEVEL);
        LinearLayout.LayoutParams blp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, dp(6));
        blp.topMargin = dp(10);
        statsBar.setLayoutParams(blp);
        card.addView(statsBar);

        statsHint = new TextView(this);
        statsHint.setTextSize(12);
        statsHint.setTextColor(MUTED);
        statsHint.setPadding(0, dp(6), 0, 0);
        card.addView(statsHint);

        // ★ 2026-10-18：最近喵化 ✗ 和电脑版一样只在本次运行内 ✓
        recentBox = new LinearLayout(this);
        recentBox.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams rblp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        rblp.topMargin = dp(12);
        recentBox.setLayoutParams(rblp);
        card.addView(recentBox);

        refreshStats();
        return card;
    }

    /// 读一次统计并刷新卡片。onResume 会调它 —— 从 QQ 切回来就能看到数字变了。
    void refreshStats() {
        if (statsLevel == null) return;
        Stats s = Stats.load(this);
        // 只做只读的换天判断：如果已经跨天，这里显示的是清零后的样子，
        // 但真正落盘留到下一次 bump（不然光是打开界面就会写一次盘）。
        s.rolloverIfNewDay();
        statsLevel.setText("Lv." + s.level());
        statsToday.setText("今日 " + s.today + " 次");
        statsTotal.setText("累计 " + s.total + " 次");
        statsBar.setProgress(s.total - s.levelFloor());
        statsHint.setText("距离 Lv." + (s.level() + 1) + " 还差 " + s.toNextLevel() + " 次");
        fillRecent();
    }

    /** ★ 2026-10-18：把最近喵化画出来 ✓ */
    void fillRecent() {
        try {
            if (recentBox == null) return;
            recentBox.removeAllViews();
            if (Stats.recent.isEmpty()) return;

            TextView head = new TextView(this);
            head.setText("最近喵化（最多 20 条，只在本次运行内）");
            head.setTextSize(12);
            head.setTextColor(MUTED);
            head.setPadding(0, 0, 0, dp(4));
            recentBox.addView(head);

            for (int i = Stats.recent.size() - 1; i >= 0; i--) {
                String[] row = Stats.recent.get(i);
                if (row == null || row.length < 2) continue;
                TextView a1 = new TextView(this);
                a1.setText("原文　" + Trace.brief(row[0]));
                a1.setTextSize(11);
                a1.setTextColor(MUTED);
                recentBox.addView(a1);
                TextView a2 = new TextView(this);
                a2.setText("结果　" + Trace.brief(row[1]));
                a2.setTextSize(11);
                a2.setTextColor(ACCENT);
                a2.setPadding(0, 0, 0, dp(6));
                recentBox.addView(a2);
            }
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  历史列表出错：" + t);
        }
    }
    /// ④ 拆成两张卡：「你说」和「喵化结果」。
    ///
    /// 原来一张卡里塞了四样东西 —— 说明、一个不显眼的「喵化之后」小标签、
    /// 输入框、输出框。输入和输出挤在一起，看不出哪个是哪个。
    /// 现在各占一张卡，卡片自己带标题；输出那张用粉白底，和输入区分开。
    ///
    /// 返回的是一个竖直容器（page() 收 varargs，容器可以当一项传进去）。
    /** \u2605 2026-10-18\uff1a\u4eca\u65e5\u55b5\u7b7e \u2727 \u6309\u65e5\u671f\u9009 \u2717 \u4e0d\u8054\u7f51 \u2713 */
    static final String[] SIGNS = {
        "\u4eca\u5929\u7684\u4f60\u975e\u5e38\u9002\u5408\u6492\u5a07\u55b5\uff01",
        "\u55b5\u4e86\u5c31\u662f\u55b5\u4e86\uff0c\u4e0d\u9700\u8981\u7406\u7531\u3002",
        "\u4eca\u5929\u8bed\u6c14\u8f6f\u4e00\u70b9\uff0c\u522b\u4eba\u4f1a\u5bf9\u4f60\u66f4\u597d\u3002",
        "\u522b\u4eba\u90fd\u5728\u804a\u5929\uff0c\u4f60\u5728\u55b5\u3002",
        "\u4eca\u5929\u9002\u5408\u5f88\u9ad8\u5174\u5730\u56de\u6d88\u606f\u3002",
        "\u732b\u732b\u4e0d\u8bf4\u8bdd\uff0c\u4f46\u732b\u732b\u90fd\u61c2\u3002",
        "\u55b5\u4e00\u4e0b\u4e0d\u4f1a\u6389\u7ea7\uff0c\u653e\u5fc3\u3002",
        "\u4eca\u5929\u53ef\u4ee5\u5c1d\u8bd5\u4e00\u4e2a\u4ece\u6765\u6ca1\u7528\u8fc7\u7684\u98ce\u683c\u3002",
        "\u522b\u4eba\u770b\u4e0d\u61c2\u7684\u55b5\u624d\u662f\u597d\u55b5\u3002",
        "\u4eca\u5929\u7684\u732b\u732b\u5f88\u4e56\uff0c\u4e5f\u5f88\u55b5\u3002",
        "\u8fd9\u4e2a\u65f6\u95f4\u70b9\u55b5\u4e00\u4e0b\uff0c\u5fc3\u60c5\u4f1a\u597d\u3002",
        "\u55b5\u5b8c\u5c31\u8dd1\uff0c\u4e0d\u56de\u5934\u3002",
    };

    static String dailySign() {
        java.util.Calendar c = java.util.Calendar.getInstance();
        int day = c.get(java.util.Calendar.YEAR) * 1000 + c.get(java.util.Calendar.DAY_OF_YEAR);
        int i = (int)(((day * 2654435761L) >>> 8) % SIGNS.length);
        if (i < 0) i = -i;
        StringBuilder sb = new StringBuilder();
        int stars = 1 + (day % 5);
        for (int k = 0; k < stars; k++) sb.append("\u2605");
        sb.append(" ").append(SIGNS[i]);
        return sb.toString();
    }

    // ---------------------------------------------------------------- 我的组合

    static class ComboClick implements View.OnClickListener {
        final MainActivity act;
        final int idx;
        ComboClick(MainActivity a, int i) { act = a; idx = i; }
        public void onClick(View v) { act.applyCombo(idx); }
    }

    static class SaveComboClick implements View.OnClickListener {
        final MainActivity act;
        SaveComboClick(MainActivity a) { act = a; }
        public void onClick(View v) { act.saveCurrentCombo(); }
    }

    static class ComboOk implements android.content.DialogInterface.OnClickListener {
        final MainActivity act;
        final android.widget.EditText box;
        ComboOk(MainActivity a, android.widget.EditText b) { act = a; box = b; }
        public void onClick(android.content.DialogInterface d, int which) {
            act.commitCombo(box.getText().toString());
        }
    }

    void rebuildComboChips() {
        try {
            if (comboRow == null) return;
            comboRow.removeAllViews();
            Rules r = Rules.load(this);
            if (r.combos.isEmpty()) {
                TextView none = new TextView(this);
                none.setText("还没有组合 ✧ 点上面「+ 存为组合」");
                none.setTextSize(11);
                none.setTextColor(MUTED);
                none.setPadding(dp(2), dp(4), 0, 0);
                comboRow.addView(none);
                return;
            }
            for (int i = 0; i < r.combos.size(); i++) {
                String[] c = r.combos.get(i);
                TextView b = makeButton("☆ " + c[0], false);
                b.setTextSize(12);
                b.setOnClickListener(new ComboClick(this, i));
                b.setOnLongClickListener(new ComboLongClick(this, i));   // \u2605 \u957f\u6309\u5220\u9664 \u2713
                LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
                lp.topMargin = dp(4);
                comboRow.addView(b, lp);
            }
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  组合列表出错：" + t);
        }
    }

    /** 当前用的风格名 ✧ 外观优先 ✓ */
    String currentStyleName() {
        if (appliedLook != null && appliedLook.length() > 0) return appliedLook;
        if (appliedTone != null && appliedTone.length() > 0 && !DRY_NAME.equals(appliedTone)) return appliedTone;
        return "";
    }

    void saveCurrentCombo() {
        try {
            String cur = currentStyleName();
            if (cur.length() == 0) {
                Toast.makeText(this, "先点一个风格，再存组合", Toast.LENGTH_SHORT).show();
                return;
            }
            android.widget.EditText box = new android.widget.EditText(this);
            box.setHint("组合名字");
            box.setTextSize(14);
            box.setSingleLine(true);
            int p = dp(16);
            box.setPadding(p, dp(8), p, dp(8));
            new android.app.AlertDialog.Builder(this)
                .setTitle("存为组合（当前：" + cur + "）")
                .setView(box)
                .setPositiveButton("保存", new ComboOk(this, box))
                .setNegativeButton("取消", null)
                .show();
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  存组合出错：" + t);
        }
    }

    void commitCombo(String name) {
        try {
            name = name == null ? "" : name.trim();
            if (name.length() == 0) return;
            String cur = currentStyleName();
            if (cur.length() == 0) return;
            String line = name + "|" + cur;
            String out = Rules.appendKey(rulesBox.getText().toString(), "我的组合", "combo", line);
            commit(out, "已存组合：" + name);
            rebuildComboChips();
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  保存组合出错：" + t);
        }
    }

    void applyCombo(int idx) {
        try {
            Rules r = Rules.load(this);
            if (idx < 0 || idx >= r.combos.size()) return;
            String styleName = r.combos.get(idx)[1];
            boolean done = false;
            for (int i = 0; i < TONE_STYLES.length; i++) {
                if (TONE_STYLES[i][0].equals(styleName)) { applyToneStyle(i); done = true; break; }
            }
            if (!done) {
                for (int i = 0; i < STYLES.length; i++) {
                    if (STYLES[i][0].equals(styleName)) { applyStyle(i); done = true; break; }
                }
            }
            if (done) Toast.makeText(this, "已应用组合：" + r.combos.get(idx)[0], Toast.LENGTH_SHORT).show();
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  应用组合出错：" + t);
        }
    }

    View buildTestCard() {
        LinearLayout col = new LinearLayout(this);
        col.setOrientation(LinearLayout.VERTICAL);

        // ★ 2026-10-18：今日喵签 ✧ 每天一句 ✓
        TextView sign = new TextView(this);
        sign.setText(dailySign());
        sign.setTextSize(12);
        sign.setTextColor(ACCENT);
        sign.setPadding(dp(2), 0, 0, dp(8));
        col.addView(sign);

        TextView hint = new TextView(this);
        hint.setText("在下面随便打几个字，立刻看到效果。纯本地处理，不会发给任何人。");
        hint.setTextSize(13);
        hint.setTextColor(MUTED);
        hint.setPadding(dp(2), 0, 0, dp(10));
        col.addView(hint);

        // ---- 你说 ----
        LinearLayout in = new LinearLayout(this);
        in.setOrientation(LinearLayout.VERTICAL);
        in.setBackground(rounded(CARD, LINE, 14, 1));
        in.setPadding(dp(16), dp(12), dp(16), dp(12));

        TextView inTitle = new TextView(this);
        inTitle.setText("你说");
        inTitle.setTextSize(13);
        inTitle.setTextColor(ACCENT);
        inTitle.setTypeface(null, android.graphics.Typeface.BOLD);
        in.addView(inTitle);

        testIn = makeField(3);
        testIn.setHint("在这里随便打几个字…");
        testIn.addTextChangedListener(this);
        LinearLayout.LayoutParams lpIn = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lpIn.topMargin = dp(8);
        testIn.setLayoutParams(lpIn);
        in.addView(testIn);
        col.addView(in);

        // ---- 喵化结果 ----
        LinearLayout out = new LinearLayout(this);
        out.setOrientation(LinearLayout.VERTICAL);
        // 粉白底 + 粉边：一眼看出这是"结果"，不是能打字的地方。
        out.setBackground(rounded(SOFT, ACCENT, 14, 1));
        out.setPadding(dp(16), dp(12), dp(16), dp(12));
        LinearLayout.LayoutParams lpOut = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lpOut.topMargin = dp(10);
        out.setLayoutParams(lpOut);

        TextView outTitle = new TextView(this);
        outTitle.setText("喵化结果");
        outTitle.setTextSize(13);
        outTitle.setTextColor(ACCENT);
        outTitle.setTypeface(null, android.graphics.Typeface.BOLD);
        out.addView(outTitle);

        // 字色从绿（OK）改成正文色 —— 绿色在这个粉色系里不搭，
        // 而且它是"结果正文"，不是"成功提示"。
        testOut = new TextView(this);
        testOut.setTextSize(16);
        testOut.setTextColor(TEXT);
        testOut.setPadding(dp(4), dp(8), dp(4), 0);
        testOut.setMinLines(2);
        out.addView(testOut);
        col.addView(out);

        return col;
    }

    /// Style presets. They change the DECORATION only - template, suffix and the
    /// kaomoji pool. Rewriting the sentence itself into a "personality" would change
    /// what the user is actually saying rather than how it sounds, so that part of the
    /// idea is deliberately left out.
    static final String[][] STYLES = {
        { "萌系", "{后缀}{颜文字}", "喵~", "(=^･ω･^=)", "(=^‥^=)", "(´･ω･`)", "ฅ^•ﻌ•^ฅ", "(=^･ｪ･^=)", "(=①ω①=)", "ฅ(•ㅅ•❀)ฅ", "(=TェT=)", "(ΦωΦ)", "(=｀ェ´=)", "(• - •)", "( ‘-ωก̀ )", "(ฅ´ω`ฅ)", "(๑•́ ₃•̀๑٥)", "( •́ .̫ •̀ )", "(●′ω`●)", "(´,,•ω•,,‘)", "(´；ω；‘)", "(›´ω`‹ )", "(๑•ั็ω•็ั๑)", "( ˘•ω•˘ )", "( •̀∀•́ )", "(๑•̀ㅂ•́)", "(ง •̀_•́)", "(*•̀ㅂ•́)" },
        { "元气", "{后缀}{颜文字}", "喵！", "(≧▽≦)", "٩(๑>ω<๑)۶", "(๑•̀ㅂ•́)و✧", "ヽ(•‿•)ノ", "(*^▽^*)", "(ﾉ◕ヮ◕)ﾉ*:･ﾟ✧", "o(≧▽≦)o", "＼(^o^)／", "v(≧∇≦)v", "(*´▽‘*)", "(′▽`)", "(//▽//)", "(⺻▽⺻ )", "(〃＾∇＾)", "(*≧m≦*)", "(。⌒∇⌒)", "(ﾉ≧∀≦)", "(^∇^*)", "(^▽^)", "(●´▽｀●)", "(〃’▽’〃)", "(＠゜▽゜)", "( ^▽^ )", "( ¯▽¯；)", "(╯▽╰)" },
        { "慵懒", "{后缀}{颜文字}", "……喵", "(￣o￣)", "(=_=)", "(˘ω˘)", "(-.-)Zzz", "zzZ", "(￣﹃￣)", "(∪｡∪)", "(－_－)", "(=ω=)", "(︶ω︶)", "( ˘ ³˘)", "(๑ºั ³ ˘๑)", "(ㆀ˘･з･˘)", "(￢_￢)", "(๛˘³˘)", "(..•˘_˘•..)", "(๑˘ ˘๑)", "(¯﹃¯ԅ)", "(¯﹃¯)", "( ╯-_-)", "(ノ—_—)", "(ノ*-_-*)", "(/= _ =)", "(╯   #-_-)", "( ╯#​   -_-)" },
        { "撒娇", "{后缀}{颜文字}", "喵♡", "(´,,•ω•,,)♡", "♡(>ω<♡)", "(づ｡◕‿‿◕｡)づ", "( ｡ớ ₃ờ)", "( ˘ ³˘)", "(๑ºั ³ ˘๑)", "(๑ơ ₃ ơ)", "(๑•́ ₃•̀๑)", "(*´▽‘*)", "(◍•ᴗ•◍)", "(• - •)", "( ‘-ωก̀ )", "(ฅ´ω`ฅ)", "(๑•́ ₃•̀๑٥)", "( •́ .̫ •̀ )", "(●′ω`●)", "(´,,•ω•,,‘)", "(´；ω；‘)", "(›´ω`‹ )", "(๑•ั็ω•็ั๑)", "( ˘•ω•˘ )", "( •̀∀•́ )", "(๑•̀ㅂ•́)", "(ง •̀_•́)", "(*•̀ㅂ•́)" },
        { "冷淡", "{后缀}{颜文字}", "喵。", "(・_・)", "(￣ー￣)", "(￣▽￣)", "(-_-)", "(¬_¬)", "(￢_￢)", "(￣〜￣)", "(・ω・)", "( =ω=)", "(´-ω-`)", "(;￣◇￣)", "(¬､¬)", "( ￣д￣；)", "(;¬_¬)", "(￣︶￣)", "(づ￣ 3￣)", "(￣ε￣)", "(￣^￣)", "(╯￣Д￣)", "(￣ω￣;)", "(=￣ω￣=)", "(*￣▽￣*)", "(￣ε￣*)", "(；￣Д￣)", "(*￣m￣)" },
        { "温柔", "{后缀}{颜文字}", "喵~", "(｡･ω･｡)", "( ´ ▽ ` )", "(◡‿◡)", "(´･ω･`)", "(๑´ω`๑)", "(﹡ˆᴗˆ﹡)", "(◍•ᴗ•◍)", "(ᵕ̈ )", "( ˘ω˘ )", "(´,,•ω•,,)", "(• - •)", "( ‘-ωก̀ )", "(ฅ´ω`ฅ)", "(๑•́ ₃•̀๑٥)", "( •́ .̫ •̀ )", "(●′ω`●)", "(´,,•ω•,,‘)", "(´；ω；‘)", "(›´ω`‹ )", "(๑•ั็ω•็ั๑)", "( ˘•ω•˘ )", "( •̀∀•́ )", "(๑•̀ㅂ•́)", "(ง •̀_•́)", "(*•̀ㅂ•́)" },
        { "害羞", "{后缀}{颜文字}", "喵…", "(/ω＼)", "(〃▽〃)", "(⁄ ⁄•⁄ω⁄•⁄ ⁄)", ">///<", "(⺣◡⺣)♡", "(≧////≦)", "(〃ω〃)", "(•́ ﹏ •̀)", "(⁄˘⁄ ⁄ω⁄ ⁄˘⁄)", "(• - •)", "( ‘-ωก̀ )", "(ฅ´ω`ฅ)", "(๑•́ ₃•̀๑٥)", "( •́ .̫ •̀ )", "(●′ω`●)", "(´,,•ω•,,‘)", "(´；ω；‘)", "(›´ω`‹ )", "(๑•ั็ω•็ั๑)", "( ˘•ω•˘ )", "( •̀∀•́ )", "(๑•̀ㅂ•́)", "(ง •̀_•́)", "(*•̀ㅂ•́)", "(๑•́ ₃•̀๑)" },
        { "中二", "{后缀}{颜文字}", "喵！", "✧･ﾟ: *✧･ﾟ:*", "(ﾉ≧∀≦)ﾉ", "━(d(・ω・)b)━", "٩(ˊᗜˋ*)و", "(ﾉ>ω<)ﾉ", "(≧∇≦)b", "☆*:.｡.o(≧▽≦)o.｡.:*☆", "(ﾉ≧∇≦)ﾉ", "(*ﾟｪﾟ*)", "(,,#ﾟДﾟ)", "(☆ﾟ∀ﾟ)", "(*ﾟ▽ﾟ*)", "(＞。☆)", "(• - •)", "(●｀∀´●)", "(●’◡’●)", "(*´▽‘*)", "(′▽`)", "( ‘-ωก̀ )", "(ฅ´ω`ฅ)", "(＾∀＾●)", "(●´∀｀)", "( ˘ ³˘)", "(๑•́ ₃•̀๑٥)", "(๑ºั ³ ˘๑)" },
        { "天然呆", "{后缀}{颜文字}", "…喵？", "(・_・?)", "(´･ω･`)?", "(°ロ°)", "(*ﾟﾛﾟ)", "(・ω・)", "(´・ω・`)", "(⊙_⊙)", "(￣ﾛ￣)", "(ㅇㅅㅇ)", "(・◇・)", "(๑*д*๑)", "(;´Д｀ﾍ)", "(っ °Д °;)", "( ° △ °|||)", "(;￢д￢)", "(//̀Д/́/)", "(｀д′)", "(ノ=Д=)", "(╯°Д°)", "(●´д｀)", "(.◕ฺˇд ˇ◕ฺ;)", "(๑°3°๑)", "( ͡° ͜ʖ ͡°)", "(°ˊДˋ°)", "(ﾉ´д｀)" },
        { "毒舌", "{后缀}{颜文字}", "喵。", "(￣〜￣)", "(￣ー￣)y-~~", "(・∀・)", "(￣▽￣)ノ_,", "(¬､¬)", "(￢_￣)", "(￣ｰ￣)", "(・_・)", "(-_-)", "(´-ω-)y-~~", "(;￣◇￣)", "( ￣д￣；)", "(;¬_¬)", "(￣︶￣)", "(づ￣ 3￣)", "(￣ε￣)", "(￣^￣)", "(╯￣Д￣)", "(￣▽￣)", "(￣ω￣;)", "(=￣ω￣=)", "(*￣▽￣*)", "(￣ε￣*)", "(；￣Д￣)", "(*￣m￣)" },
        { "口癖", "{后缀}{颜文字}", "喵帕斯", "(˘≧∇≦˘)", "＼(≧∀≦)／", "(๑˘∇˘๑)", "(=^･ω･^=)", "(ﾉ≧∀≦)ﾉ", "(๑•̀ㅂ•́)و✧", "ヽ(•‿•)ノ", "٩(ˊᗜˋ*)و", "(• - •)", "( ‘-ωก̀ )", "(ฅ´ω`ฅ)", "(๑•́ ₃•̀๑٥)", "( •́ .̫ •̀ )", "(●′ω`●)", "(´,,•ω•,,‘)", "(´；ω；‘)", "(›´ω`‹ )", "(๑•ั็ω•็ั๑)", "( ˘•ω•˘ )", "( •̀∀•́ )", "(๑•̀ㅂ•́)", "(ง •̀_•́)", "(*•̀ㅂ•́)", "(๑•́ ₃•̀๑)", "(๑•̀ㅁ•́ฅ)" },
        { "病娇", "{后缀}{颜文字}", "喵…", "(´∀｀)♡", "(◕‿◕)", "(‾◡◝)", "(｡•̀ᴗ-)✧", "(´｡• ᵕ •｡`)", "(๑´ㅂ`๑)", "♡(◡‿◡✿)", "(¬‿¬)", "(◕‿◕✿)", "(´,,•ω•,,)♡", "(๑♡⌓♡๑)", "(◍•ᴗ•◍)❤", "(´｡• ω •｡`)", "(๑•́ ₃ •̀๑)", "(´-ω-`)", "(´,,•ω•,,)", "(๑>ᴗ<๑)", "( ˘ ³˘)♥", "(｡♥‿♥｡)", "(◕ᴗ◕✿)", "( ´ ꒳ ` )", "(๑˃ᴗ˂)ﻭ", "(´｡• ᵕ •｡`) ♡", "(♡˙︶˙♡)", "(灬º‿º灬)♡" },
        { "热血", "{后缀}{颜文字}", "喵！", "(≧∇≦)ﾉ", "٩(ˊᗜˋ*)و", "(๑•̀ㅂ•́)و✧", "ヽ(•̀ω•́ )ゝ", "(ﾉ≧∀≦)ﾉ", "━(d(・ω・)b)━", "o(≧口≦)o", "٩(๑>◡<๑)۶", "(*≧▽≦)", "(ﾉ>ω<)ﾉ", "(≧◇≦)", "(๑>ᴗ<๑)", "(๑•̀ㅁ•́ฅ)", "٩(๑❛ᴗ❛๑)۶", "(๑˃̵ᴗ˂̵)و", "(ﾉ◕ヮ◕)ﾉ*:･ﾟ✧", "(≧∇≦*)", "(๑•̀ㅂ•́)ﾉ➹♡", "(๑•̀ㅂ•́)و✧", "(ﾉ≧∇≦)ﾉ", "(๑•̀ㅁ•́๑)✧", "٩(๑˃̶͈̀ω˂̶͈́)۶", "(๑˃ᴗ˂)و", "(☆▽☆)", "٩(◕‿◕)۶" },
        { "颜文字在前", "{颜文字}{后缀}", "喵~", "(=^･ω･^=)", "ฅ^•ﻌ•^ฅ", "(´･ω･`)", "(=^‥^=)", "(≧▽≦)", "(๑•̀ㅂ•́)و✧", "(=TェT=)", "(ΦωΦ)", "(• - •)", "( ‘-ωก̀ )", "(ฅ´ω`ฅ)", "(๑•́ ₃•̀๑٥)", "( •́ .̫ •̀ )", "(●′ω`●)", "(´,,•ω•,,‘)", "(´；ω；‘)", "(›´ω`‹ )", "(๑•ั็ω•็ั๑)", "( ˘•ω•˘ )", "( •̀∀•́ )", "(๑•̀ㅂ•́)", "(ง •̀_•́)", "(*•̀ㅂ•́)", "(๑•́ ₃•̀๑)", "(๑•̀ㅁ•́ฅ)" },
        { "只有喵~", "{后缀}{颜文字}", "喵~" }
    };

    // ---------------------------------------------------------------- 导出 / 导入

    @Override
    protected void onActivityResult(int req, int res, Intent data) {
        super.onActivityResult(req, res, data);
        if (res != RESULT_OK || data == null || data.getData() == null) return;
        android.net.Uri uri = data.getData();
        try {
            if (req == REQ_EXPORT) {
                String text = rulesBox.getText().toString();
                java.io.OutputStream os = getContentResolver().openOutputStream(uri);
                if (os == null) return;
                java.io.OutputStreamWriter w = new java.io.OutputStreamWriter(os, "UTF-8");
                w.write(text);
                w.flush();
                w.close();
                os.close();
                Toast.makeText(this, "\u5DF2\u5BFC\u51FA", Toast.LENGTH_SHORT).show();
            } else if (req == REQ_IMPORT) {
                java.io.InputStream is = getContentResolver().openInputStream(uri);
                if (is == null) return;
                java.io.ByteArrayOutputStream bos = new java.io.ByteArrayOutputStream();
                byte[] buf = new byte[8192];
                int k;
                while ((k = is.read(buf)) > 0) bos.write(buf, 0, k);
                is.close();
                String text = new String(bos.toByteArray(), "UTF-8");
                if (text == null || text.trim().length() == 0) {
                    Toast.makeText(this, "\u6587\u4EF6\u662F\u7A7A\u7684", Toast.LENGTH_SHORT).show();
                    return;
                }
                // \u2605 \u5148\u95EE\u4E00\u58F0 \u2727 \u8986\u76D6\u6389\u5F88\u96BE\u6062\u590D \u2717
                new android.app.AlertDialog.Builder(this)
                    .setTitle("\u786E\u5B9A\u5BFC\u5165\uFF1F")
                    .setMessage("\u4F1A\u8986\u76D6\u5F53\u524D\u7684\u89C4\u5219\u3002")
                    .setPositiveButton("\u5BFC\u5165", new ImportOk(this, text))
                    .setNegativeButton("\u53D6\u6D88", null)
                    .show();
            }
        } catch (Throwable t) {
            Toast.makeText(this, "\u5931\u8D25\uFF1A" + t, Toast.LENGTH_LONG).show();
        }
    }

    static class ImportOk implements android.content.DialogInterface.OnClickListener {
        final MainActivity act;
        final String text;
        ImportOk(MainActivity a, String t) { act = a; text = t; }
        public void onClick(android.content.DialogInterface d, int which) {
            act.rulesBox.setText(text);
            act.commit(text, "\u5DF2\u5BFC\u5165");
        }
    }

    static class RandomStyleClick implements View.OnClickListener {
        final MainActivity act;
        RandomStyleClick(MainActivity a) { act = a; }
        public void onClick(View v) { act.randomStyle(); }
    }

    void randomStyle() {
        try {
            java.util.Random r = new java.util.Random();
            int li = r.nextInt(STYLES.length);
            applyStyle(li);
            if (TONE_STYLES.length > 0) {
                int ti = r.nextInt(TONE_STYLES.length);
                applyToneStyle(ti);
                Toast.makeText(this, "随机到了：" + STYLES[li][0] + " + " + TONE_STYLES[ti][0], Toast.LENGTH_SHORT).show();
            } else {
                Toast.makeText(this, "随机到了：" + STYLES[li][0], Toast.LENGTH_SHORT).show();
            }
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  随机风格出错：" + t);
        }
    }


    /** \u2605 \u91cd\u5efa\u81ea\u5b9a\u4e49\u5916\u89c2\u90a3\u4e00\u884c \u2713 */
    // ★ 2026-10-22：自定义风格长按删除 ✓
    static class CustomLookLongClick implements View.OnLongClickListener {
        final MainActivity act; final int idx;
        CustomLookLongClick(MainActivity a, int i) { act = a; idx = i; }
        public boolean onLongClick(View v) { act.askDeleteCustom(false, idx); return true; }
    }

    static class CustomToneLongClick implements View.OnLongClickListener {
        final MainActivity act; final int idx;
        CustomToneLongClick(MainActivity a, int i) { act = a; idx = i; }
        public boolean onLongClick(View v) { act.askDeleteCustom(true, idx); return true; }
    }

    static class DeleteOk implements android.content.DialogInterface.OnClickListener {
        final MainActivity act; final boolean tone; final String nm;
        DeleteOk(MainActivity a, boolean t, String name) { act = a; tone = t; nm = name; }
        public void onClick(android.content.DialogInterface d, int which) { act.doDeleteCustom(tone, nm); }
    }

    static class ComboLongClick implements View.OnLongClickListener {
        final MainActivity act; final int idx;
        ComboLongClick(MainActivity a, int i) { act = a; idx = i; }
        public boolean onLongClick(View v) { act.askDeleteCombo(idx); return true; }
    }

    static class ComboDeleteOk implements android.content.DialogInterface.OnClickListener {
        final MainActivity act; final String nm;
        ComboDeleteOk(MainActivity a, String name) { act = a; nm = name; }
        public void onClick(android.content.DialogInterface d, int which) { act.doDeleteCombo(nm); }
    }

    void askDeleteCombo(int idx) {
        try {
            Rules r = Rules.load(this);
            if (idx < 0 || idx >= r.combos.size()) return;
            String nm = r.combos.get(idx)[0];
            new android.app.AlertDialog.Builder(this)
                .setTitle("\u5220\u6389\u300c" + nm + "\u300d\uff1f")
                .setPositiveButton("\u5220\u9664", new ComboDeleteOk(this, nm))
                .setNegativeButton("\u53d6\u6d88", null)
                .show();
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  \u5220\u9664\u7ec4\u5408\u786e\u8ba4\u51fa\u9519\uff1a" + t);
        }
    }

    void doDeleteCombo(String name) {
        try {
            String out = Rules.removeCombo(rulesBox.getText().toString(), name);
            commit(out, "\u5df2\u5220\u6389\uff1a" + name);
            rebuildComboChips();
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  \u5220\u9664\u7ec4\u5408\u51fa\u9519\uff1a" + t);
        }
    }

    void askDeleteCustom(boolean tone, int idx) {
        try {
            Rules r = Rules.load(this);
            java.util.List<String[]> list = tone ? r.customTones : r.customLooks;
            if (idx < 0 || idx >= list.size()) return;
            String nm = list.get(idx)[0];
            new android.app.AlertDialog.Builder(this)
                .setTitle("删掉「" + nm + "」？")
                .setMessage(tone ? "这个自定义语气会从规则里拿掉"
                                 : "这个自定义外观会从规则里拿掉")
                .setPositiveButton("删除", new DeleteOk(this, tone, nm))
                .setNegativeButton("取消", null)
                .show();
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  删除确认出错：" + t);
        }
    }

    void doDeleteCustom(boolean tone, String name) {
        try {
            String out = Rules.removeCustomStyle(rulesBox.getText().toString(), name, !tone);
            commit(out, "已删掉：" + name);
            if (tone) rebuildCustomToneChips(); else rebuildCustomLookChips();
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  删除自定义风格出错：" + t);
        }
    }

    void rebuildCustomLookChips() {
        try {
            if (customLookRow == null) return;
            customLookRow.removeAllViews();
            Rules r = Rules.load(this);
            if (r.customLooks.isEmpty()) return;
            for (int i = 0; i < r.customLooks.size(); i++) {
                final String[] t = r.customLooks.get(i);
                TextView b = makeButton("\u2606 " + t[0], false);
                b.setTextSize(12);
                b.setOnClickListener(new CustomLookClick(this, i));
                b.setOnLongClickListener(new CustomLookLongClick(this, i));   // ★ 长按删除 ✓
                LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
                lp.topMargin = dp(4);
                customLookRow.addView(b, lp);
            }
        } catch (Throwable e) {
            Trace.add(Trace.now() + "  \u81ea\u5b9a\u4e49\u5916\u89c2\u5217\u8868\u51fa\u9519\uff1a" + e);
        }
    }

    void applyCustomLook(int i) {
        try {
            Rules r = Rules.load(this);
            if (i < 0 || i >= r.customLooks.size()) return;
            String[] t = r.customLooks.get(i);
            String out = Rules.applyCustomLook(rulesBox.getText().toString(), t);
            commit(out, "\u5DF2\u5e94\u7528\u81ea\u5b9a\u4e49\u5916\u89c2\uff1a" + t[0]);
        } catch (Throwable e) {
            Trace.add(Trace.now() + "  \u5e94\u7528\u81ea\u5b9a\u4e49\u5916\u89c2\u51fa\u9519\uff1a" + e);
        }
    }

    void showNewLook() {
        try {
            LinearLayout box = new LinearLayout(this);
            box.setOrientation(LinearLayout.VERTICAL);
            int p = dp(16);
            box.setPadding(p, dp(8), p, 0);

            android.widget.EditText eName = newEdit("\u540D\u5B57\uFF08\u6700\u591A 8 \u4E2A\u5B57\uFF09", "");
            android.widget.EditText eSuf = newEdit("\u53E5\u5C3E\u540E\u7F00", "\u55B5~");
            android.widget.EditText eKao = newEdit("\u989C\u6587\u5B57\uFF08\u9017\u53F7\u5206\u9694\uFF09", "(\uFF61\uFF65\u03C9\uFF65\uFF61)");

            box.addView(eName);
            box.addView(eSuf);
            box.addView(eKao);

            new android.app.AlertDialog.Builder(this)
                .setTitle("\u65B0\u5EFA\u81EA\u5B9A\u4E49\u5916\u89C2")
                .setView(box)
                .setPositiveButton("\u4FDD\u5B58", new NewLookOk(this, eName, eSuf, eKao))
                .setNegativeButton("\u53D6\u6D88", null)
                .show();
        } catch (Throwable e) {
            Trace.add(Trace.now() + "  \u65B0\u5efa\u5916\u89c2\u5bf9\u8bdd\u6846\u51fa\u9519\uff1a" + e);
        }
    }

    static class CustomLookClick implements View.OnClickListener {
        final MainActivity act;
        final int idx;
        CustomLookClick(MainActivity a, int i) { act = a; idx = i; }
        public void onClick(View v) { act.applyCustomLook(idx); }
    }

    static class NewLookOk implements android.content.DialogInterface.OnClickListener {
        final MainActivity act;
        final android.widget.EditText name, suf, kao;
        NewLookOk(MainActivity a, android.widget.EditText n1, android.widget.EditText s1,
                  android.widget.EditText k1) { act = a; name = n1; suf = s1; kao = k1; }
        public void onClick(android.content.DialogInterface d, int which) {
            act.saveNewLook(name.getText().toString(), suf.getText().toString(), kao.getText().toString());
        }
    }

    void saveNewLook(String name, String suf, String kao) {
        try {
            name = name == null ? "" : name.trim();
            suf = suf == null ? "" : suf.trim();
            kao = kao == null ? "" : kao.trim();
            if (name.length() == 0) { Toast.makeText(this, "\u540D\u5B57\u4E0D\u80FD\u7A7A\u7740", Toast.LENGTH_SHORT).show(); return; }
            if (suf.length() == 0) { Toast.makeText(this, "\u540E\u7F00\u4E0D\u80FD\u7A7A\u7740", Toast.LENGTH_SHORT).show(); return; }
            if (kao.length() == 0) { Toast.makeText(this, "\u81F3\u5C11\u8981\u6709\u4E00\u4E2A\u989C\u6587\u5B57", Toast.LENGTH_SHORT).show(); return; }
            String line = name + "|" + suf + "|" + kao;
            String out = Rules.appendKey(rulesBox.getText().toString(), "\u81EA\u5B9A\u4E49\u5916\u89C2", "lookstyle", line);
            commit(out, "\u5DF2\u65B0\u5EFA\u5916\u89C2\uff1A" + name);
            rebuildCustomLookChips();
        } catch (Throwable e) {
            Trace.add(Trace.now() + "  \u4FDD\u5B58\u81EA\u5B9A\u4E49\u5916\u89c2\u51FA\u9519\uff1a" + e);
        }
    }

    // ---------------------------------------------------------------- 自定义语气

    /** \u2605 \u91cd\u5efa\u81ea\u5b9a\u4e49\u8bed\u6c14\u90a3\u4e00\u884c\uff08\u6bcf\u4e2a\u662f\u4e00\u4e2a\u5c0f\u6309\u94ae\uff09\u2713 */
    void rebuildCustomToneChips() {
        try {
            if (customToneRow == null) return;
            customToneRow.removeAllViews();
            customToneButtons.clear();
            Rules r = Rules.load(this);
            if (r.customTones.isEmpty()) return;
            for (int i = 0; i < r.customTones.size(); i++) {
                final String[] t = r.customTones.get(i);
                // \u2605 \u70b9\u4e00\u4e0b\u5c31\u5957\u7528\u8fd9\u4e2a\u81ea\u5b9a\u4e49\u8bed\u6c14 \u2713
                TextView b = makeButton("\u2605 " + t[0], false);
                b.setTextSize(12);
                b.setOnClickListener(new CustomToneClick(this, i));
                b.setOnLongClickListener(new CustomToneLongClick(this, i));   // ★ 长按删除 ✓
                LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
                lp.topMargin = dp(4);
                customToneRow.addView(b, lp);
                customToneButtons.add(b);
            }
        } catch (Throwable e) {
            Trace.add(Trace.now() + "  \u81ea\u5b9a\u4e49\u8bed\u6c14\u5217\u8868\u51fa\u9519\uff1a" + e);
        }
    }

    /** \u2605 \u5957\u7528\u7b2c i \u4e2a\u81ea\u5b9a\u4e49\u8bed\u6c14 \u2713 */
    void applyCustomTone(int i) {
        try {
            Rules r = Rules.load(this);
            if (i < 0 || i >= r.customTones.size()) return;
            String[] t = r.customTones.get(i);
            String out = Rules.applyCustomTone(rulesBox.getText().toString(), t);
            commit(out, "\u5DF2\u5e94\u7528\u81ea\u5b9a\u4e49\u8bed\u6c14\uff1a" + t[0]);
        } catch (Throwable e) {
            Trace.add(Trace.now() + "  \u5e94\u7528\u81ea\u5b9a\u4e49\u8bed\u6c14\u51fa\u9519\uff1a" + e);
        }
    }

    /** \u2605 \u5f39\u51fa\u65b0\u5efa\u5bf9\u8bdd\u6846 \u2713 */
    void showNewTone() {
        try {
            LinearLayout box = new LinearLayout(this);
            box.setOrientation(LinearLayout.VERTICAL);
            int p = dp(16);
            box.setPadding(p, dp(8), p, 0);

            android.widget.EditText eName = newEdit("\u540D\u5B57\uFF08\u6700\u591A 8 \u4E2A\u5B57\uFF09", "");
            eName.setMaxEms(8);
            android.widget.EditText eSuf = newEdit("\u53E5\u5C3E\u540E\u7F00", "\u55B5~");
            android.widget.EditText eKao = newEdit("\u989C\u6587\u5B57\uFF08\u9017\u53F7\u5206\u9694\uFF09", "(\uFF61\uFF65\u03C9\uFF65\uFF61),(\u25E1\u203F\u25E1)");
            android.widget.EditText eOpen = newEdit("\u5F00\u5934\uFF08\u9017\u53F7\u5206\u9694\uFF0C\u53EF\u7559\u7A7A\uFF09", "\u55EF\uFF0C");
            android.widget.EditText eClose = newEdit("\u7ED3\u5C3E\uFF08\u9017\u53F7\u5206\u9694\uFF0C\u53EF\u7559\u7A7A\uFF09", "\uFF0C\u5C31\u8FD9\u6837\u5427");

            box.addView(eName);
            box.addView(eSuf);
            box.addView(eKao);
            box.addView(eOpen);
            box.addView(eClose);

            new android.app.AlertDialog.Builder(this)
                .setTitle("\u65B0\u5EFA\u81EA\u5B9A\u4E49\u8BED\u6C14")
                .setView(box)
                .setPositiveButton("\u4FDD\u5B58", new NewToneOk(this, eName, eSuf, eKao, eOpen, eClose))
                .setNegativeButton("\u53D6\u6D88", null)
                .show();
        } catch (Throwable e) {
            Trace.add(Trace.now() + "  \u65B0\u5efa\u8bed\u6c14\u5bf9\u8bdd\u6846\u51fa\u9519\uff1a" + e);
        }
    }

    android.widget.EditText newEdit(String hint, String value) {
        android.widget.EditText e = new android.widget.EditText(this);
        e.setHint(hint);
        e.setText(value);
        e.setTextSize(13);
        e.setSingleLine(true);
        return e;
    }

    static class CustomToneClick implements View.OnClickListener {
        final MainActivity act;
        final int idx;
        CustomToneClick(MainActivity a, int i) { act = a; idx = i; }
        public void onClick(View v) { act.applyCustomTone(idx); }
    }

    static class NewToneOk implements android.content.DialogInterface.OnClickListener {
        final MainActivity act;
        final android.widget.EditText name, suf, kao, open, close;
        NewToneOk(MainActivity a, android.widget.EditText n1, android.widget.EditText s1,
                  android.widget.EditText k1, android.widget.EditText o1, android.widget.EditText c1) {
            act = a; name = n1; suf = s1; kao = k1; open = o1; close = c1;
        }
        public void onClick(android.content.DialogInterface d, int which) {
            act.saveNewTone(name.getText().toString(), suf.getText().toString(),
                            kao.getText().toString(), open.getText().toString(),
                            close.getText().toString());
        }
    }

    void saveNewTone(String name, String suf, String kao, String open, String close) {
        try {
            name = name == null ? "" : name.trim();
            suf = suf == null ? "" : suf.trim();
            kao = kao == null ? "" : kao.trim();
            if (name.length() == 0) { Toast.makeText(this, "\u540D\u5B57\u4E0D\u80FD\u7A7A\u7740", Toast.LENGTH_SHORT).show(); return; }
            if (suf.length() == 0) { Toast.makeText(this, "\u540E\u7F00\u4E0D\u80FD\u7A7A\u7740", Toast.LENGTH_SHORT).show(); return; }
            if (kao.length() == 0) { Toast.makeText(this, "\u81F3\u5C11\u8981\u6709\u4E00\u4E2A\u989C\u6587\u5B57", Toast.LENGTH_SHORT).show(); return; }
            String line = name + "|" + suf + "|" + kao + "|" + (open == null ? "" : open.trim())
                        + "|" + (close == null ? "" : close.trim());
            String out = Rules.appendKey(rulesBox.getText().toString(), "\u81EA\u5B9A\u4E49\u8BED\u6C14", "tonestyle", line);
            commit(out, "\u5DF2\u65B0\u5EFA\u8BED\u6C14\uFF1A" + name);
            rebuildCustomToneChips();
        } catch (Throwable e) {
            Trace.add(Trace.now() + "  \u4FDD\u5B58\u81EA\u5B9A\u4E49\u8BED\u6C14\u51FA\u9519\uff1a" + e);
        }
    }

    // ---------------------------------------------------------------- 收藏颜文字

    /** 多选列表的勾选状态 ✧ 匿名类 d8 会炸 ✗ 所以用具名静态类 ✓ */
    static class KaoFavPick implements android.content.DialogInterface.OnMultiChoiceClickListener {
        final boolean[] checked;
        KaoFavPick(boolean[] c) { checked = c; }
        public void onClick(android.content.DialogInterface d, int which, boolean isChecked) {
            checked[which] = isChecked;
        }
    }

    static class KaoFavOk implements android.content.DialogInterface.OnClickListener {
        final MainActivity act;
        final String[] all;
        final boolean[] checked;
        KaoFavOk(MainActivity a, String[] al, boolean[] c) { act = a; all = al; checked = c; }
        public void onClick(android.content.DialogInterface d, int which) {
            act.saveKaoFav(all, checked);
        }
    }

    void showKaoFav() {
        try {
            Rules r = Rules.load(this);
            java.util.List<String> pool = r.kaomoji;
            if (pool.isEmpty()) { android.widget.Toast.makeText(this, "还没有颜文字", android.widget.Toast.LENGTH_SHORT).show(); return; }
            String[] all = pool.toArray(new String[0]);
            boolean[] checked = new boolean[all.length];
            for (int i = 0; i < all.length; i++) checked[i] = r.favKao.contains(all[i]);

            new android.app.AlertDialog.Builder(this)
                .setTitle("收藏颜文字（勾中的才会被用到）")
                .setMultiChoiceItems(all, checked, new KaoFavPick(checked))
                .setPositiveButton("保存", new KaoFavOk(this, all, checked))
                .setNegativeButton("取消", null)
                .show();
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  收藏颜文字出错：" + t);
        }
    }

    void saveKaoFav(String[] all, boolean[] checked) {
        try {
            java.util.List<String> picked = new java.util.ArrayList<String>();
            for (int i = 0; i < all.length; i++) if (checked[i]) picked.add(all[i]);
            String out = Rules.setKeys(rulesBox.getText().toString(), "收藏", "favkao", picked);
            commit(out, "收藏了 " + picked.size() + " 个颜文字");
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  保存收藏出错：" + t);
        }
    }

    View buildStyleCard() {
        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setBackground(rounded(CARD, LINE, 14, 1));
        card.setPadding(dp(16), dp(14), dp(16), dp(14));

        TextView hint = new TextView(this);
        hint.setText("点一下套用。只替换规则里的「模板 / 后缀 / 颜文字」，词替换、白名单和注释都不动，之后还能自己手改。");
        hint.setTextSize(13);
        hint.setTextColor(MUTED);
        card.addView(hint);

card.addView(sectionLabel("程度（多久加一次装饰）"));

        TextView lvlHint = new TextView(this);
        lvlHint.setText("轻度 = 只在整段末尾加一次。普通 = 每个句号 / 问号 / 感叹号后都加。"
                 + "重度 = 连逗号后面也加。");
        lvlHint.setTextSize(12);
        lvlHint.setTextColor(MUTED);
        lvlHint.setPadding(dp(2), dp(2), 0, dp(6));
        card.addView(lvlHint);

        LinearLayout levelRow = new LinearLayout(this);
        levelRow.setOrientation(LinearLayout.HORIZONTAL);
        levelButtons = new TextView[LEVELS.length];
        for (int i = 0; i < LEVELS.length; i++) {
            TextView b = makeButton(LEVELS[i], false);
            b.setTextSize(14);
            b.setTag(Integer.valueOf(LEVEL_TAG + i));
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                    0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
            lp.rightMargin = dp(i == LEVELS.length - 1 ? 0 : 6);
            b.setLayoutParams(lp);
            levelRow.addView(b);
            b.setTextColor(TEXT);
            b.setBackground(rounded(CARD, LINE, 12, 1));
            levelButtons[i] = b;
        }
        card.addView(levelRow);

        TextView lnote = new TextView(this);
        lnote.setText("★ 改完立即生效。想看它写了什么，去「规则」页看「程度」那一行。");
        lnote.setTextSize(12);
        lnote.setTextColor(MUTED);
        lnote.setPadding(dp(2), dp(6), 0, dp(8));
        card.addView(lnote);

        
        // two rows of three. No anonymous inner classes anywhere: d8 (build-tools 34)
        // crashes on the synthetic outer-instance parameter javac emits for them, so
        // every button routes through the activity's own onClick using a tag.
        lookButtons = new TextView[STYLES.length];
        // ★ 2026-10-17：外观风格从 6 个变成 13 个 ✧ 行数不能写死 ✗
        int lookRows = (STYLES.length + 2) / 3;
        for (int rowI = 0; rowI < lookRows; rowI++) {
            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            LinearLayout.LayoutParams rlp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            rlp.topMargin = dp(rowI == 0 ? 12 : 8);
            row.setLayoutParams(rlp);

            for (int colI = 0; colI < 3; colI++) {
                int idx = rowI * 3 + colI;
                if (idx >= STYLES.length) break;

                TextView b = makeButton(STYLES[idx][0] + "\n你好" + STYLES[idx][2], false);
                b.setTextSize(12);
                b.setTag(Integer.valueOf(idx));
                b.setOnClickListener(this);
                lookButtons[idx] = b;

                LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(0,
                        ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
                lp.rightMargin = dp(colI == 2 ? 0 : 6);
                b.setLayoutParams(lp);
                row.addView(b);
            }
            card.addView(row);
        }

        // ---- 语气风格 -------------------------------------------------------------
        // These rewrite the whole sentence (opener + a closing line that matches the mood),
        // exactly like the PC version. The wording lives in assets/styles/*.txt rather than
        // in Java so a style stays plain text the user can read, copy out and edit - and so
        // adding one never means touching code.
        LinearLayout.LayoutParams slp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        slp.topMargin = dp(18);
        TextView sub = new TextView(this);
        sub.setText("语气风格 —— 会改写整句话（开头和结尾都加话）。\n"
                  + "和上面的外观风格是二选一：点了语气风格会换成它的句尾装饰，"
                  + "点外观风格则会把语气关掉。每边只写自己那几行，你的词库不会被删。");
        sub.setTextSize(13);
        sub.setTextColor(MUTED);
        sub.setLayoutParams(slp);
        card.addView(sub);

        toneButtons = new TextView[TONE_STYLES.length];
        // ★ 2026-10-17：语气从 6 个变成 11 个（+1 不加 = 12）✧ 行数也要动态 ✓
        int toneRows = (TONE_STYLES.length + 3) / 4;
        for (int rowI = 0; rowI < toneRows; rowI++) {
            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            LinearLayout.LayoutParams rlp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            rlp.topMargin = dp(rowI == 0 ? 10 : 8);
            row.setLayoutParams(rlp);

            int perRow = 4;
            for (int colI = 0; colI < perRow; colI++) {
                int idx = rowI * perRow + colI;
                if (idx >= TONE_STYLES.length) break;

                TextView b = makeButton(TONE_STYLES[idx][0], false);
                b.setTextSize(12);
                b.setTag(Integer.valueOf(1000 + idx));
                b.setOnClickListener(this);
                toneButtons[idx] = b;

                LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(0,
                        ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
                lp.rightMargin = dp(colI == perRow - 1 ? 0 : 6);
                b.setLayoutParams(lp);
                row.addView(b);
            }
            card.addView(row);
        }
        // ★ 2026-10-18：收藏颜文字 ✗ 安卓没有单独那页 ✗ 入口放这里 ✓
        favKaoBtn = makeButton("\u6536\u85CF\u989C\u6587\u5B57\u2026", false);
        favKaoBtn.setTextSize(12);
        favKaoBtn.setOnClickListener(this);
        LinearLayout.LayoutParams fklp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        fklp.topMargin = dp(10);
        favKaoBtn.setLayoutParams(fklp);
        card.addView(favKaoBtn);

        // ★ 2026-10-18：随机来一套 ✓
        TextView btnRandom = makeButton("★ 随机来一套", false);
        btnRandom.setTextSize(12);
        btnRandom.setOnClickListener(new RandomStyleClick(this));
        LinearLayout.LayoutParams rlp2 = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        rlp2.topMargin = dp(8);
        btnRandom.setLayoutParams(rlp2);
        card.addView(btnRandom);

        // \u2605 2026-10-18\uff1a\u6211\u7684\u7ec4\u5408 \u2727 \u4e00\u4e2a\u540d\u5b57 + \u4e00\u4e2a\u98ce\u683c\u540d \u2713
        saveComboBtn = makeButton("+ \u5B58\u4E3A\u7EC4\u5408", false);
        saveComboBtn.setTextSize(12);
        saveComboBtn.setOnClickListener(new SaveComboClick(this));
        LinearLayout.LayoutParams sclp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        sclp.topMargin = dp(8);
        saveComboBtn.setLayoutParams(sclp);
        card.addView(saveComboBtn);

        comboRow = new LinearLayout(this);
        comboRow.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams crlp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        crlp.topMargin = dp(6);
        comboRow.setLayoutParams(crlp);
        card.addView(comboRow);
        rebuildComboChips();

        // \u2605 2026-10-18\uff1a\u81ea\u5b9a\u4e49\u8bed\u6c14 \u2727 \u5efa\u4e00\u4e2a + \u5df2\u6709\u7684\u5c55\u793a\u4e0b\u6765 \u2713
        newLookBtn = makeButton("+ \u65B0\u5EFA\u5916\u89C2", false);
        newLookBtn.setTextSize(12);
        newLookBtn.setOnClickListener(this);
        LinearLayout.LayoutParams nllp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        nllp.topMargin = dp(8);
        newLookBtn.setLayoutParams(nllp);
        card.addView(newLookBtn);

        customLookRow = new LinearLayout(this);
        customLookRow.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams cllp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        cllp.topMargin = dp(6);
        customLookRow.setLayoutParams(cllp);
        card.addView(customLookRow);
        rebuildCustomLookChips();

        newToneBtn = makeButton("+ \u65B0\u5EFA\u8BED\u6C14", false);
        newToneBtn.setTextSize(12);
        newToneBtn.setOnClickListener(this);
        LinearLayout.LayoutParams ntlp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        ntlp.topMargin = dp(8);
        newToneBtn.setLayoutParams(ntlp);
        card.addView(newToneBtn);

        customToneRow = new LinearLayout(this);
        customToneRow.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams ctlp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        ctlp.topMargin = dp(6);
        customToneRow.setLayoutParams(ctlp);
        card.addView(customToneRow);
        rebuildCustomToneChips();


        return card;
    }

    // ------------------------------------------------ 设置

    // Tags for the settings page controls, all above the tab range so one onClick can
    // serve every button on the screen (no anonymous inner classes: d8 crashes on them).
    static final int SET_AUTO   = 5000;
    static final int SAVE_PKGS  = 5001;
    static final int REFRESH_LOG= 5002;
    static final int WECHAT_MODE = 5305;       // 微信模式开关
    static final int ACCESS_SETTINGS = 5304;   // 跳到系统无障碍设置
    static final int CLEAR_LOG  = 5003;
    static final int SLIDE_APPLY = 5298;   // 「滑块比例」的自由输入
    static final int KEEP_ALIVE  = 5301;   // 「常驻运行」开关
    static final int BATT_OPT    = 5302;   // 跳到系统电池优化页面
    static final int SET_FLOAT   = 5306;   // 悬浮窗（手动转换模式）开关
    // ★ 2026-10-31：悬浮窗的三个子选项 ✓
    static final int SET_FMODE_Q = 5310;   // 显示时机：只在生效应用
    static final int SET_FMODE_A = 5311;   // 显示时机：一直显示
    static final int SET_TAIL    = 5312;   // 结果小尾巴
    static final int SET_TINY    = 5308;   // ★ 语气小模型开关（clf.bin ✧ 7.3 MB）✓
    static final int FLOAT_PERM  = 5307;   // 跳到系统的「显示在其他应用上层」页面
    static final int DELAY_APPLY = 5199;   // 「停手多久」的自由输入
    static final int DELAY_MIN_MS = 100;
    static final int DELAY_MAX_MS = 5000;

    static final int FADE_TAG   = 5200;    // 5200..5204 预设
    static final int FADE_APPLY = 5299;    // 「应用」按钮
    static final int DELAY_TAG  = 5100;
    static final int[] DELAYS = { 400, 700, 1200 };

    // ★ 程度（轻度 / 普通 / 重度）✧ 和电脑版那三个按钮一一对应 ✓
    static final int LEVEL_TAG  = 5400;
    static final String[] LEVELS = { "轻度", "普通", "重度" };

    CheckBox setAuto;
    CheckBox setTiny;           // ★ 语气小模型开关 ✓
    CheckBox setFloat;          // 悬浮窗开关 ✓
    TextView floatHint;         // 提示当前有没有权限 ✓
    // ★ 2026-10-31：开了监控才展开的子选项 ✓
    LinearLayout floatOpts;
    CheckBox cbModeQq, cbModeAlways, cbTail;
    /** ★ 2026-10-31：要放动画时先把 syncSettings 里那次同步跳掉 ✓
     *   不然收起的时候它先把整块 GONE 了 ✗ getHeight()=0 → 动画直接结束 ✓ */
    boolean skipFloatSync = false;

    CompoundButton setKeepAlive;
    EditText setPkgs;
    TextView[] delayButtons;
    TextView[] levelButtons;
    TextView traceBox;

    /** \u2605 2026-10-31\uff1a\u603b\u5f00\u5173\u5173\u7740\u5c31\u628a\u5b50\u9009\u9879\u6574\u5757\u9690\u85cf \u2713 */
    void syncFloatOpts() { syncFloatOpts(false); }

    /** \u2605 2026-10-31\uff1a\u5c55\u5f00 / \u6536\u8d77\u5e26\u9ad8\u5ea6\u52a8\u753b \u2717
     *   \u76f4\u63a5 VISIBLE / GONE \u4f1a\u8ba9\u4e0b\u9762\u7684\u8bbe\u7f6e\u9879\u201c\u5693\u201d\u5730\u8df3\u4e00\u4e0b \u2713
     */
    void syncFloatOpts(boolean animate) {
        try {
            if (floatOpts == null || setFloat == null) return;
            boolean on = setFloat.isChecked();
            if (!animate) {
                floatOpts.animate().cancel();
                ViewGroup.LayoutParams lp0 = floatOpts.getLayoutParams();
                if (lp0 != null) { lp0.height = ViewGroup.LayoutParams.WRAP_CONTENT; floatOpts.setLayoutParams(lp0); }
                floatOpts.setAlpha(1f);
                floatOpts.setVisibility(on ? View.VISIBLE : View.GONE);
                return;
            }
            if (on) {
                floatOpts.setVisibility(View.VISIBLE);
                floatOpts.setAlpha(0f);
                floatOpts.measure(View.MeasureSpec.makeMeasureSpec(
                        getResources().getDisplayMetrics().widthPixels, View.MeasureSpec.AT_MOST),
                        View.MeasureSpec.makeMeasureSpec(0, View.MeasureSpec.UNSPECIFIED));
                int target = floatOpts.getMeasuredHeight();
                if (target <= 0) target = dp(140);
                ValueAnimator va = ValueAnimator.ofInt(0, target);
                va.setDuration(420);
                va.setInterpolator(new DecelerateInterpolator(1.4f));
                va.addUpdateListener(new HeightAnim(floatOpts));
                va.addListener(new ExpandEnd(floatOpts));
                va.start();
                floatOpts.animate().alpha(1f).setDuration(360).start();
            } else {
                int from = floatOpts.getHeight();
                if (from <= 0) {
                    floatOpts.setVisibility(View.GONE);
                    return;
                }
                ValueAnimator va = ValueAnimator.ofInt(from, 0);
                va.setDuration(320);
                va.setInterpolator(new AccelerateInterpolator(1.2f));
                va.addUpdateListener(new HeightAnim(floatOpts));
                va.addListener(new CollapseEnd(floatOpts));
                va.start();
                floatOpts.animate().alpha(0f).setDuration(260).start();
            }
        } catch (Throwable t) { }
    }

    /** \u2605 \u628a\u9ad8\u5ea6\u4e00\u5e27\u4e00\u5e27\u6539\u6210\u52a8\u753b\u503c \u2713 */
    static class HeightAnim implements ValueAnimator.AnimatorUpdateListener {
        final View v;
        HeightAnim(View x) { v = x; }
        public void onAnimationUpdate(ValueAnimator a) {
            try {
                int h = ((Integer) a.getAnimatedValue()).intValue();
                ViewGroup.LayoutParams lp = v.getLayoutParams();
                if (lp == null) return;
                lp.height = h;
                v.setLayoutParams(lp);
            } catch (Throwable t) { }
        }
    }

    /** \u2605 \u5c55\u5f00\u5b8c\u4e86\u628a\u9ad8\u5ea6\u8fd8\u7ed9 WRAP_CONTENT \u2713 */
    static class ExpandEnd implements Animator.AnimatorListener {
        final View v;
        ExpandEnd(View x) { v = x; }
        public void onAnimationStart(Animator a) { }
        public void onAnimationEnd(Animator a) {
            try {
                ViewGroup.LayoutParams lp = v.getLayoutParams();
                if (lp != null) { lp.height = ViewGroup.LayoutParams.WRAP_CONTENT; v.setLayoutParams(lp); }
                v.setAlpha(1f);
            } catch (Throwable t) { }
        }
        public void onAnimationCancel(Animator a) { onAnimationEnd(a); }
        public void onAnimationRepeat(Animator a) { }
    }

    /** \u2605 \u6536\u8d77\u5b8c\u4e86\u624d GONE \u2713 */
    static class CollapseEnd implements Animator.AnimatorListener {
        final View v;
        CollapseEnd(View x) { v = x; }
        public void onAnimationStart(Animator a) { }
        public void onAnimationEnd(Animator a) {
            try {
                v.setVisibility(View.GONE);
                ViewGroup.LayoutParams lp = v.getLayoutParams();
                if (lp != null) { lp.height = ViewGroup.LayoutParams.WRAP_CONTENT; v.setLayoutParams(lp); }
                v.setAlpha(1f);
            } catch (Throwable t) { }
        }
        public void onAnimationCancel(Animator a) { }
        public void onAnimationRepeat(Animator a) { }
    }

    View buildSettingsCard() {
        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setBackground(rounded(CARD, LINE, 14, 1));
        card.setPadding(dp(16), dp(14), dp(16), dp(14));

        TextView h = new TextView(this);
        h.setText("这些开关会直接写进规则文件。点「规则」页就能看到刚才改了什么，也能接着手改。");
        h.setTextSize(13);
        h.setTextColor(MUTED);
        card.addView(h);

        card.addView(sectionLabel("界面动画"));

        TextView ah = new TextView(this);
        ah.setText("切页淡化的时长（毫秒）。越长越容易看清，太短会像\"啪一下\"出现。\n"
                 + "电脑版是 400，手机屏小、内容密，同样的时长看着更快，所以这里默认 600。");
        ah.setTextSize(12);
        ah.setTextColor(MUTED);
        ah.setPadding(dp(2), dp(2), 0, dp(6));
        card.addView(ah);

        LinearLayout arow = new LinearLayout(this);
        arow.setOrientation(LinearLayout.HORIZONTAL);
        fadeButtons = new TextView[FADE_PRESETS.length];
        for (int i = 0; i < FADE_PRESETS.length; i++) {
            TextView b = makeButton(FADE_PRESETS[i] + " 毫秒", false);
            b.setTextSize(12);
            b.setTag(Integer.valueOf(FADE_TAG + i));
            b.setOnClickListener(this);
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                    0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
            lp.rightMargin = dp(i == FADE_PRESETS.length - 1 ? 0 : 5);
            b.setLayoutParams(lp);
            arow.addView(b);
            fadeButtons[i] = b;
        }
        card.addView(arow);

        // 也可以自己填一个数 —— 预设覆盖不到的档位（比如 1500、2000）从这里进
        LinearLayout frow = new LinearLayout(this);
        frow.setOrientation(LinearLayout.HORIZONTAL);
        frow.setGravity(Gravity.CENTER_VERTICAL);
        LinearLayout.LayoutParams flp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        flp.topMargin = dp(8);
        frow.setLayoutParams(flp);

        setFadeField = makeField(1);
        setFadeField.setInputType(InputType.TYPE_CLASS_NUMBER);
        setFadeField.setHint("自己填一个数（100 - 3000）");
        setFadeField.setTextSize(14);
        setFadeField.setLayoutParams(new LinearLayout.LayoutParams(
                0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        frow.addView(setFadeField);

        TextView fapply = makeButton("应用", true);
        fapply.setTextSize(13);
        fapply.setTag(Integer.valueOf(FADE_APPLY));
        fapply.setOnClickListener(this);
        LinearLayout.LayoutParams alp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        alp.leftMargin = dp(8);
        fapply.setLayoutParams(alp);
        frow.addView(fapply);
        card.addView(frow);

        // 滑块相对淡化的比例。100 = 和淡化一样长（默认）；
        // 53 是最早那个手感（滑块先到位、淡化还在淡）；150 则相反。
        TextView sh = new TextView(this);
        sh.setText("滑块比例（%）。100 = 导航滑块和淡化一样长；53 = 滑块先到位；150 = 滑块更慢。");
        sh.setTextSize(12);
        sh.setTextColor(MUTED);
        sh.setPadding(dp(2), dp(10), 0, dp(6));
        card.addView(sh);

        LinearLayout srow = new LinearLayout(this);
        srow.setOrientation(LinearLayout.HORIZONTAL);
        srow.setGravity(Gravity.CENTER_VERTICAL);

        setSlidePctField = makeField(1);
        setSlidePctField.setInputType(InputType.TYPE_CLASS_NUMBER);
        setSlidePctField.setHint("填一个数（10 - 300）");
        setSlidePctField.setTextSize(14);
        setSlidePctField.setLayoutParams(new LinearLayout.LayoutParams(
                0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        srow.addView(setSlidePctField);

        TextView sapply = makeButton("应用", true);
        sapply.setTextSize(13);
        sapply.setTag(Integer.valueOf(SLIDE_APPLY));
        sapply.setOnClickListener(this);
        LinearLayout.LayoutParams salp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        salp.leftMargin = dp(8);
        sapply.setLayoutParams(salp);
        srow.addView(sapply);
        card.addView(srow);

        // ── 常驻运行 ──
        //
        // 无障碍服务跑在应用进程里。手机应用开多了、或者系统省电策略激进，整个进程
        // 会被回收，无障碍跟着一起没 —— 症状就是"用着用着突然不生效了"。
        // 打开这个会挂一条常驻通知，把进程优先级提到前台，系统基本不会回收；
        // 被杀之后系统还会把它拉回来。
        card.addView(sectionLabel("常驻运行"));

        setKeepAlive = makeCheck("常驻运行（挂一条通知，别让系统把它清掉）");
        setKeepAlive.setTag(Integer.valueOf(KEEP_ALIVE));
        setKeepAlive.setOnClickListener(this);
        card.addView(setKeepAlive);

        TextView kh = new TextView(this);
        kh.setText("打开后会自动开机自启。不想要通知就把它关掉 —— 但那样系统可能在应用开多时清掉它。"
                 + "如果还是被清，点下面的按钮把喵化助手加进系统白名单。");
        kh.setTextSize(12);
        kh.setTextColor(MUTED);
        kh.setPadding(dp(2), dp(2), 0, dp(8));
        card.addView(kh);

        TextView batt = makeButton("打开系统电池优化设置", false);
        batt.setTextSize(13);
        batt.setTag(Integer.valueOf(BATT_OPT));
        batt.setOnClickListener(this);
        card.addView(batt);

        // ★★★ 程度：决定多久加一次装饰 ★★★
        //
        // 和电脑版对应：
        //   轻度 = 只在整段末尾加一次
        //   普通 = 每个句尾都加
        //   重度 = 连逗号后面也加
        // 以前只能在规则文件里手写「每句 = false」✧ 很难发现 ✓
        card.addView(sectionLabel("自动转换"));
        setAuto = makeCheck("停止打字后自动转换");
        setAuto.setTag(Integer.valueOf(SET_AUTO));
        setAuto.setOnClickListener(this);
        card.addView(setAuto);

        // ★ 悬浮窗（手动转换）✧ 和电脑版的热键转换对应 ✓
        setFloat = makeCheck("悬浮窗（不自动转 ✧ 点一下才转）");
        setFloat.setTag(Integer.valueOf(SET_FLOAT));
        setFloat.setOnClickListener(this);
        card.addView(setFloat);

        // ★★★ 2026-10-31：开了「启用悬浮窗」才展开下面这些 ★★★
        floatOpts = new LinearLayout(this);
        floatOpts.setOrientation(LinearLayout.VERTICAL);
        card.addView(floatOpts);

        floatHint = new TextView(this);
        floatHint.setTextSize(12);
        floatHint.setTextColor(MUTED);
        floatHint.setPadding(dp(2), dp(2), 0, dp(6));
        floatOpts.addView(floatHint);

        TextView modeLbl = new TextView(this);
        modeLbl.setText("显示时机");
        modeLbl.setTextSize(13);
        modeLbl.setTextColor(TEXT);
        modeLbl.setPadding(dp(2), dp(8), 0, 0);
        floatOpts.addView(modeLbl);

        cbModeQq = makeCheck("只在生效应用里出现");
        cbModeQq.setTag(Integer.valueOf(SET_FMODE_Q));
        cbModeQq.setOnClickListener(this);
        floatOpts.addView(cbModeQq);

        cbModeAlways = makeCheck("一直显示");
        cbModeAlways.setTag(Integer.valueOf(SET_FMODE_A));
        cbModeAlways.setOnClickListener(this);
        floatOpts.addView(cbModeAlways);

        cbTail = makeCheck("结果小尾巴（球旁显示转换结果）");
        cbTail.setTag(Integer.valueOf(SET_TAIL));
        cbTail.setOnClickListener(this);
        floatOpts.addView(cbTail);

        TextView pb = makeButton("授予悬浮窗权限", false);
        pb.setTextSize(13);
        pb.setTag(Integer.valueOf(FLOAT_PERM));
        floatOpts.addView(pb);

        syncFloatOpts();

        // ★ 语气小模型 ✧ 用户 2026-10-17：设置里要有开关 ✓
        setTiny = makeCheck("语气小模型");
        setTiny.setTag(Integer.valueOf(SET_TINY));
        setTiny.setOnClickListener(this);
        card.addView(setTiny);

        TextView tinyHint = new TextView(this);
        tinyHint.setTextSize(12);
        tinyHint.setTextColor(MUTED);
        tinyHint.setPadding(dp(2), dp(2), 0, dp(6));
        tinyHint.setText("开关后，关键词判不出语气时会问一个 7 MB 的本地小模型"
                        + "（看上下文）。不联网、不发热，比关键词懂反话。");
        card.addView(tinyHint);

        TextView dh = new TextView(this);
        dh.setText("停手多久之后转换（毫秒）。太短会在你思考时就把话改掉，太长会觉得迟钝。");
        dh.setTextSize(12);
        dh.setTextColor(MUTED);
        dh.setPadding(dp(2), dp(2), 0, dp(6));
        card.addView(dh);

        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        delayButtons = new TextView[DELAYS.length];
        for (int i = 0; i < DELAYS.length; i++) {
            TextView b = makeButton(DELAYS[i] + " 毫秒", false);
            b.setTextSize(13);
            b.setTag(Integer.valueOf(DELAY_TAG + i));
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                    0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
            lp.rightMargin = dp(i == DELAYS.length - 1 ? 0 : 6);
            b.setLayoutParams(lp);
            row.addView(b);
            delayButtons[i] = b;
        }
        card.addView(row);

        // 自己填一个 —— 预设只有三档，中间那些值（比如 500、1000、1500）从这里进
        LinearLayout drow = new LinearLayout(this);
        drow.setOrientation(LinearLayout.HORIZONTAL);
        drow.setGravity(Gravity.CENTER_VERTICAL);
        LinearLayout.LayoutParams dlp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        dlp.topMargin = dp(8);
        drow.setLayoutParams(dlp);

        setDelayField = makeField(1);
        setDelayField.setInputType(InputType.TYPE_CLASS_NUMBER);
        setDelayField.setHint("自己填一个数（100 - 5000）");
        setDelayField.setTextSize(14);
        setDelayField.setLayoutParams(new LinearLayout.LayoutParams(
                0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        drow.addView(setDelayField);

        TextView dapply = makeButton("应用", true);
        dapply.setTextSize(13);
        dapply.setTag(Integer.valueOf(DELAY_APPLY));
        dapply.setOnClickListener(this);
        LinearLayout.LayoutParams dalp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        dalp.leftMargin = dp(8);
        dapply.setLayoutParams(dalp);
        drow.addView(dapply);
        card.addView(drow);

        card.addView(sectionLabel("生效应用"));
        TextView ph = new TextView(this);
        ph.setText("一行一个安卓包名。\nQQ = com.tencent.mobileqq　　TIM = com.tencent.tim　　微信 = com.tencent.mm");
        ph.setTextSize(12);
        ph.setTextColor(MUTED);
        ph.setPadding(dp(2), dp(2), 0, dp(6));
        card.addView(ph);

        setPkgs = makeField(3);
        setPkgs.setTextSize(13);
        card.addView(setPkgs);

        TextView save = makeButton("保存生效应用", true);
        save.setTag(Integer.valueOf(SAVE_PKGS));
        LinearLayout.LayoutParams slp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        slp.topMargin = dp(10);
        save.setLayoutParams(slp);
        card.addView(save);

        // ---- 行为日志 ---------------------------------------------------------------
        // What the accessibility service actually read and wrote, newest last. Added because
        // a duplicated-text report could not be reproduced in the engine at all, so the next
        // report needs to show the read/write pair rather than a guess about it.
        card.addView(sectionLabel("行为日志"));
        TextView lh = new TextView(this);
        lh.setText("无障碍服务每次「读到什么、写了什么」都记在这里。\n"
                 + "遇到问题时：先点「清空」，去 QQ 里把问题重现一次，再回来点「刷新」截图。");
        lh.setTextSize(12);
        lh.setTextColor(MUTED);
        lh.setPadding(dp(2), dp(2), 0, dp(6));
        card.addView(lh);

        LinearLayout lrow = new LinearLayout(this);
        lrow.setOrientation(LinearLayout.HORIZONTAL);
        TextView lref = makeButton("刷新日志", false);
        lref.setTextSize(13);
        lref.setTag(Integer.valueOf(REFRESH_LOG));
        LinearLayout.LayoutParams llp = new LinearLayout.LayoutParams(
                0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
        llp.rightMargin = dp(6);
        lref.setLayoutParams(llp);
        lrow.addView(lref);
        TextView lclr = makeButton("清空", false);
        lclr.setTextSize(13);
        lclr.setTag(Integer.valueOf(CLEAR_LOG));
        lclr.setLayoutParams(new LinearLayout.LayoutParams(
                0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        lrow.addView(lclr);
        card.addView(lrow);

        // ★ 检查微信支持：微信只在系统有读屏服务运行时才把输入框交给无障碍，
        //   而这个状态只能由系统签名的读屏应用（比如 TalkBack）开启 ——
        //   普通应用自己请求无效（实测过）。所以这里只做"检查 + 引导"。
        TextView mhWmTitle = new TextView(this);
        mhWmTitle.setTextSize(13);
        mhWmTitle.setTextColor(0xFF333333);
        mhWmTitle.setText("微信支持");
        mhWmTitle.setPadding(dp(2), dp(14), dp(2), dp(2));
        card.addView(mhWmTitle);

        TextView mhWmHint = new TextView(this);
        mhWmHint.setTextSize(12);
        mhWmHint.setTextColor(0xFF8A8A8A);
        mhWmHint.setLineSpacing(0, 1.35f);
        mhWmHint.setText("微信只在系统【有读屏服务在运行】时，才把输入框交给无障碍服务。"
                       + "这个状态只能由系统自带的读屏应用开启，本应用自己请求无效。"
                       + "所以用微信时，请同时打开一个读屏服务（比如 TalkBack）。"
                       + "★ 读屏开着时：单击是「选中」、双击才是「点击」，滑动用两根手指。");
        card.addView(mhWmHint);

        TextView mhWmBtn = makeButton("检查微信支持（看看读屏开没开）", false);
        mhWmBtn.setTextSize(13);
        mhWmBtn.setTag(Integer.valueOf(WECHAT_MODE));
        mhWmBtn.setOnClickListener(this);
        LinearLayout.LayoutParams mhWmLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        mhWmLp.topMargin = dp(6);
        mhWmBtn.setLayoutParams(mhWmLp);
        card.addView(mhWmBtn);
        traceBox = new TextView(this);
        traceBox.setTextSize(11);
        traceBox.setTextColor(TEXT);
        traceBox.setBackground(rounded(BG, LINE, 10, 1));
        traceBox.setPadding(dp(10), dp(10), dp(10), dp(10));
        traceBox.setTextIsSelectable(true);
        LinearLayout.LayoutParams tlp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        tlp.topMargin = dp(8);
        traceBox.setLayoutParams(tlp);
        traceBox.setText(Trace.dump());
        card.addView(traceBox);

        // ★ 2026-10-18：配置导出 / 导入 ✗ 用系统文件选择器 ✓
        card.addView(sectionLabel("\u914D\u7F6E\u5907\u4EFD"));

        TextView eh = new TextView(this);
        eh.setText("\u628A\u4F60\u6539\u8FC7\u7684\u89C4\u5219\u5BFC\u51FA\u6210\u4E00\u4E2A\u6587\u4EF6\uFF0C"
                 + "\u6362\u624B\u673A\u6216\u91CD\u88C5\u540E\u518D\u5BFC\u5165\u56DE\u6765\u3002");
        eh.setTextSize(12);
        eh.setTextColor(MUTED);
        eh.setPadding(dp(2), dp(2), 0, dp(6));
        card.addView(eh);

        LinearLayout erow = new LinearLayout(this);
        erow.setOrientation(LinearLayout.HORIZONTAL);

        btnExport = makeButton("\u5BFC\u51FA\u914D\u7F6E", false);
        btnExport.setTextSize(12);
        btnExport.setOnClickListener(this);
        erow.addView(btnExport, new LinearLayout.LayoutParams(0,
                ViewGroup.LayoutParams.WRAP_CONTENT, 1f));

        btnImport = makeButton("\u5BFC\u5165\u914D\u7F6E", false);
        btnImport.setTextSize(12);
        btnImport.setOnClickListener(this);
        LinearLayout.LayoutParams ilp = new LinearLayout.LayoutParams(0,
                ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
        ilp.leftMargin = dp(8);
        erow.addView(btnImport, ilp);

        card.addView(erow);


        return card;
    }

    /** Pulls the current rules into the settings controls. Only called when the tab opens,
     *  so typing in the package box is never overwritten from under the user. */
    void syncSettings() {
        if (setAuto == null) return;
        String text = rulesBox.getText().toString();

        String auto = Rules.getKey(text, "自动模式", "automode");
        setAuto.setChecked(auto == null || Rules.parseBoolSafe(auto));

        // ★ 悬浮窗 ✧ 顺便告诉用户权限有没有给 ✓
        if (setFloat != null) {
            String fb = Rules.getKey(text, "悬浮窗", "floatbar");
            setFloat.setChecked(fb != null && Rules.parseBoolSafe(fb));
            String fm = Rules.getKey(text, "\u60AC\u6D6E\u7A97\u6A21\u5F0F", "floatmode");
            boolean alwaysMode = "always".equals(fm);
            cbModeQq.setChecked(!alwaysMode);
            cbModeAlways.setChecked(alwaysMode);
            String tl = Rules.getKey(text, "\u7ED3\u679C\u5C3E\u5DF4", "tail");
            cbTail.setChecked(tl == null || Rules.parseBoolSafe(tl));
            if (!skipFloatSync) syncFloatOpts();
        if (setTiny != null) {
            String tm = Rules.getKey(rulesBox.getText().toString(), "小模型语气", "tinymood");
            setTiny.setChecked(tm != null && Rules.parseBoolSafe(tm));
        }
            if (floatHint != null) {
                boolean perm = false;
                try { perm = android.provider.Settings.canDrawOverlays(this); } catch (Exception e) { }
                floatHint.setText(perm
                        ? "权限已经给了 ✧ 开启后输入框有字时会冒出一个小球 ✓"
                        : "还没有权限 ✧ 先点下面那个按钮去系统里打开「允许显示在其他应用上层」✓");
            }
        }

        // 常驻运行那个开关存在偏好里，不在规则文本里
        if (setKeepAlive != null) setKeepAlive.setChecked(KeepAliveService.enabled(this));

        // ★ 程度：从当前规则推出是哪一档 ✧ 再把对应的按钮填色 ✓
        //   推法和电脑版一样：逗号也加 → 重度；每句都加 → 普通；否则 → 轻度
        if (levelButtons != null) {
            int lv = 0;
            String lvKey = Rules.getKey(text, "程度", "level");
            if (lvKey != null && lvKey.contains("重")) lv = 2;
            else if (lvKey != null && lvKey.contains("普")) lv = 1;
            else if (lvKey != null && (lvKey.contains("轻") || lvKey.toLowerCase().contains("light"))) lv = 0;
            else {
                String es = Rules.getKey(text, "每句", "eachsentence");
                String cd = Rules.getKey(text, "逗号也加", "commadecorate");
                boolean e = es == null || Rules.parseBoolSafe(es);
                boolean c = cd != null && Rules.parseBoolSafe(cd);
                lv = c ? 2 : (e ? 1 : 0);
            }
            // ★★★ 和风格按钮画法一致 ★★★
            //
            // 风格按钮（paintRow ✓）选中时是：
            //   浅粉底 + 强调色边框 2px + 强调色文字 + 前面一个 ✓
            // 我上一版给程度用的是【实心填色】✧ 放在一起就不一样 ✓
            // （用户 2026-10-07："这俩按钮的按键能不能统一一下" ✓）
            for (int i = 0; i < levelButtons.length; i++) {
                boolean on = (i == lv);
                levelButtons[i].setText(on ? "✓ " + LEVELS[i] : LEVELS[i]);
                levelButtons[i].setTextColor(on ? ACCENT : TEXT);
                levelButtons[i].setBackground(rounded(on ? SEL_BG : CARD,
                                                       on ? ACCENT : LINE, 12, on ? 2 : 1));
            }
        }

        String delay = Rules.getKey(text, "延迟", "debounce");
        int d = 700;
        try { if (delay != null) d = Integer.parseInt(delay.trim()); } catch (Exception e) { }
        for (int i = 0; i < delayButtons.length; i++) {
            boolean on = Math.abs(DELAYS[i] - d) < 60;
            // ★★★ 和风格按钮统一 ★★★
            String lbl = DELAYS[i] + " 毫秒";
            delayButtons[i].setText(on ? "✓ " + lbl : lbl);
            delayButtons[i].setTextColor(on ? ACCENT : TEXT);
            delayButtons[i].setBackground(rounded(on ? SEL_BG : CARD,
                                                  on ? ACCENT : LINE, 12, on ? 2 : 1));
        }

        // 把当前延迟填进自由输入框（预设三档之外的值也能看出来是多少）
        if (setDelayField != null) setDelayField.setText(String.valueOf(d));

        if (setSlidePctField != null) setSlidePctField.setText(String.valueOf(slidePct()));

        syncFadeButtons();      // ⑦ 动画时长的预设高亮 + 输入框内容

        java.util.List<String> pkgs = Rules.getKeys(text, "包名", "package");
        if (pkgs.isEmpty()) pkgs = Rules.getKeys(text, "白名单", "whitelist");
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < pkgs.size(); i++) {
            if (i > 0) sb.append("\n");
            sb.append(pkgs.get(i));
        }
        setPkgs.setText(sb.toString());

        // the service shares this process, so the static Trace is the same buffer
        if (traceBox != null) traceBox.setText(Trace.dump());
    }

    void commit(String out, String toast) {
        rulesBox.setText(out);
        Rules.saveText(this, out);
        cached = Rules.parse(out);
        updatePreview();
        Toast.makeText(this, toast, Toast.LENGTH_SHORT).show();
    }

    /// 把「停手多久」设成任意毫秒数。
    /// 和预设按钮走同一条路 —— 都是写进规则文件的 延迟/debounce 键，
    /// 不另外存一份，免得两处对不上。
    void applyDelayMs(int ms) {
        int v = Math.max(DELAY_MIN_MS, Math.min(DELAY_MAX_MS, ms));
        commit(Rules.setKey(rulesBox.getText().toString(), "延迟", "debounce", String.valueOf(v)),
               "已设为 " + v + " 毫秒");
        syncSettings();
    }

    void applyLevelStyle(int index) {
        if (index < 0 || index >= DELAYS.length) return;
        commit(Rules.setKey(rulesBox.getText().toString(), "延迟", "debounce",
                            String.valueOf(DELAYS[index])),
               "已设为 " + DELAYS[index] + " 毫秒");
        syncSettings();
    }

    static final String[] TONE_STYLES_NAMES = { "傲娇", "黏人", "慵懒", "委屈", "元气", "应激", "温柔", "高冷", "中二", "天然呆", "毒舌", "病娇", "热血", "腹黑", "不加语气" };

    // display name -> asset file. ASCII file names on purpose: aapt2 cannot open an asset
    // whose path contains Chinese characters ("failed to open file"), which cost a build.
    static final String[][] TONE_STYLES = {
        { "傲娇",        "tsundere" },
        { "黏人",        "clingy" },
        { "慵懒",        "lazy" },
        { "委屈",        "aggrieved" },
        { "元气",        "energetic" },
        { "应激",        "stress" },
        { "温柔",        "gentle" },
        { "高冷",        "cold" },
        { "中二",        "chuuni" },
        { "天然呆",       "airhead" },
        { "毒舌",        "sharp" },
        { "病娇",        "yandere" },
        { "热血",        "hyped" },
        { "腹黑",        "scheming" },
        { "不加语气",      "dry" }
    };

    String readAsset(String path) {
        try {
            java.io.InputStream in = getAssets().open(path);
            java.io.ByteArrayOutputStream bos = new java.io.ByteArrayOutputStream();
            byte[] buf = new byte[8192];
            int n;
            while ((n = in.read(buf)) > 0) bos.write(buf, 0, n);
            in.close();
            return new String(bos.toByteArray(), "UTF-8");
        } catch (Exception e) {
            return null;
        }
    }

    /// Applies a tone style. Only the lines the style declares are rewritten - 只加末尾
    /// declares just 语气 / 每句, so it switches the wrapper off without touching the pools.
    void applyToneStyle(int index) {
        if (index < 0 || index >= TONE_STYLES.length) return;
        String name = TONE_STYLES[index][0];

        // Assets live FLAT in assets/, not in assets/styles/: aapt2 writes the sub-path
        // with the OS separator, so a nested folder produced "assets/styles\x.txt" and
        // AssetManager could never find it. A plain file name comes out as "assets/x.txt".
        String asset = readAsset(TONE_STYLES[index][1] + ".txt");
        if (asset == null) {
            Toast.makeText(this, "找不到风格文件：" + name, Toast.LENGTH_LONG).show();
            return;
        }

        String out = Rules.applyStyleFromAsset(rulesBox.getText().toString(), asset);
        // ★★★ 2026-10-22：选语气就必须把「语气」开关打开 ★★★
        //   语气风格文件里只有词池 ✗ 没有「语气 = true」✗ 之前点过外观就一直是 false ✗
        //   表现：外观 + 语气一起选 ✗ 只有外观的输出 ✓
        //   之前只要点过外观（把语气关了）✗ 之后选语气也不会开 ✗
        //   表现就是：外观+语气一起选 ✗ 只有外观的输出 ✓
        out = Rules.setKey(out, "语气", "tone", "true");
        rulesBox.setText(out);
        Rules.saveText(this, out);
        cached = Rules.parse(out);
        updatePreview();
        rememberStyle(false, name);
        // a tone style brings its own 模板/后缀/颜文字, so no look chip describes what is in
        // effect any more - clearing the highlight is more honest than leaving a stale one lit
        // ★ 叠加开着就不清外观高亮 ✗
        // \u2605 2026-10-18\uff1a\u9009\u8bed\u6c14\u4e5f\u4e0d\u6e05\u5916\u89c2\u9ad8\u4eae \u2717 \u4e24\u4e2a\u53ef\u4ee5\u540c\u65f6\u4eae \u2713
        paintStyleButtons();
        Toast.makeText(this, "已套用「" + name + "」风格", Toast.LENGTH_SHORT).show();
    }

    /// Swaps only the decoration lines of the rules text, leaving word rules, hotkeys,
    /// whitelist and every comment alone - so a style stays a visible edit the user can
    /// tweak afterwards instead of hidden magic. The rewrite itself lives in Rules so it
    /// can be exercised on a desktop JVM.
    void applyStyle(int index) {
        if (index < 0 || index >= STYLES.length) return;
        String[] s = STYLES[index];

        String[] kao = new String[s.length - 3];
        for (int i = 3; i < s.length; i++) kao[i - 3] = s[i];

        String out = Rules.applyStyleText(rulesBox.getText().toString(), s[1], s[2], kao);
        // ★ 2026-10-18：外观和语气可以叠加了 ✧ 和电脑版一致 ✓
        //   外观只管 模板 / 后缀 / 颜文字 ✧ 语气池一个字都不动 ✗
        //   想关语气就点「不加语气」那个胶囊 ✓
        rulesBox.setText(out);
        Rules.saveText(this, out);
        cached = Rules.parse(out);
        updatePreview();
        rememberStyle(true, s[0]);
        // \u2605 2026-10-18\uff1a\u4e0d\u518d\u6e05\u8bed\u6c14\u9ad8\u4eae \u2717 \u53e0\u52a0\u5f00\u7740\u5c31\u662f\u4e24\u4e2a\u90fd\u4eae \u2713
        paintStyleButtons();
        Toast.makeText(this, "已套用「" + s[0] + "」风格", Toast.LENGTH_SHORT).show();
    }

    View buildRulesCard() {        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setBackground(rounded(CARD, LINE, 14, 1));
        card.setPadding(dp(16), dp(14), dp(16), dp(14));

        TextView hint = new TextView(this);
        hint.setText("改完点「保存规则」立即生效，不用重启。");
        hint.setTextSize(13);
        hint.setTextColor(MUTED);
        card.addView(hint);

        rulesBox = makeField(10);
        rulesBox.setTextSize(12);
        rulesBox.setTypeface(android.graphics.Typeface.MONOSPACE);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.topMargin = dp(10);
        rulesBox.setLayoutParams(lp);
        card.addView(rulesBox);

        return card;
    }

    View buildButtons() {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setPadding(0, dp(12), 0, 0);

        btnSave = makeButton("保存规则", true);
        row.addView(btnSave);

        btnDefaults = makeButton("恢复默认", false);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.leftMargin = dp(10);
        btnDefaults.setLayoutParams(lp);
        row.addView(btnDefaults);

        return row;
    }

    View buildNote() {
        LinearLayout col = new LinearLayout(this);
        col.setOrientation(LinearLayout.VERTICAL);

        // ── 免费声明 ──
        //
        // 这类小工具经常被人打包拿去卖（换个名字、加个壳），所以把话说在最前面。
        // 用和"无障碍没开"同一套警示配色，一眼能看到，不会当成普通正文划过去。
        LinearLayout warn = new LinearLayout(this);
        warn.setOrientation(LinearLayout.VERTICAL);
        warn.setBackground(rounded(SOFT, ACCENT, 14, 1));
        warn.setPadding(dp(16), dp(14), dp(16), dp(14));

        TextView wt = new TextView(this);
        wt.setText("这个软件是【完全免费】的");
        wt.setTextSize(15);
        wt.setTextColor(ACCENT);
        wt.setTypeface(null, android.graphics.Typeface.BOLD);
        warn.addView(wt);

        TextView wb = new TextView(this);
        wb.setText("如果你是花钱买的，说明你被骗了 —— 请立即找商家退款。\n"
                  + "它没有在任何平台出售，也没有\"付费版\"\"高级版\"之类的东西。");
        wb.setTextSize(13);
        wb.setTextColor(TEXT);
        wb.setPadding(0, dp(6), 0, 0);
        warn.addView(wb);

        LinearLayout.LayoutParams wlp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        wlp.bottomMargin = dp(14);
        warn.setLayoutParams(wlp);
        col.addView(warn);

        TextView note = new TextView(this);
        note.setTextSize(12);
        note.setTextColor(MUTED);
        note.setPadding(dp(4), dp(4), dp(4), 0);
        note.setText("说明\n"
                + "· 本应用没有申请联网权限，所有替换都在手机本地完成，不会读取或上传任何内容。\n"
                + "· 它只在规则里列出的应用里生效，默认只有 QQ（com.tencent.mobileqq）。\n"
                + "· 工作方式是「停止打字后改写输入框内容」，光标会跳到结尾。\n"
                + "· 不想用了：到 系统设置 → 无障碍 里关掉，或直接卸载。");
        col.addView(note);

        // 更新公告. The text is the same 更新记录.txt that ships with the desktop package,
        // copied into assets by the build script - one source of truth, no second copy to
        // drift. It is bundled rather than fetched because the app has no network permission.
        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setBackground(rounded(CARD, LINE, 14, 1));
        card.setPadding(dp(16), dp(14), dp(16), dp(14));
        LinearLayout.LayoutParams clp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        clp.topMargin = dp(18);
        card.setLayoutParams(clp);

        TextView h = new TextView(this);
        h.setText("更新公告");
        h.setTextSize(16);
        h.setTextColor(ACCENT);
        h.setTypeface(null, android.graphics.Typeface.BOLD);
        card.addView(h);

        TextView body = new TextView(this);
        body.setTextSize(12);
        body.setTextColor(TEXT);
        body.setPadding(0, dp(8), 0, 0);
        body.setTextIsSelectable(true);

        String log = readAsset("changelog.txt");
        if (log == null) {
            body.setText("找不到公告文件。");
        } else {
            body.setText(log.replace("\r\n", "\n").trim());
        }
        card.addView(body);
        col.addView(card);

        return col;
    }

    // ------------------------------------------------ behaviour

    @Override
    public void onClick(View v) {
        if (v == btnOpen) {
            try {
                startActivity(new Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS));
            } catch (Exception e) {
                Toast.makeText(this, "打不开设置，请手动进入 系统设置 → 无障碍 里寻找", Toast.LENGTH_LONG).show();
            }
        } else if (v == btnExport) {
            try {
                Intent it = new Intent(Intent.ACTION_CREATE_DOCUMENT);
                it.addCategory(Intent.CATEGORY_OPENABLE);
                it.setType("text/plain");
                it.putExtra(Intent.EXTRA_TITLE, "miaohua-rules-" + System.currentTimeMillis() / 1000 + ".txt");
                startActivityForResult(it, REQ_EXPORT);
            } catch (Throwable t) {
                Toast.makeText(this, "\u6253\u4E0D\u5F00\u6587\u4EF6\u9009\u62E9\u5668",
                        Toast.LENGTH_SHORT).show();
            }
        } else if (v == btnImport) {
            try {
                Intent it = new Intent(Intent.ACTION_OPEN_DOCUMENT);
                it.addCategory(Intent.CATEGORY_OPENABLE);
                it.setType("*/*");
                startActivityForResult(it, REQ_IMPORT);
            } catch (Throwable t) {
                Toast.makeText(this, "\u6253\u4E0D\u5F00\u6587\u4EF6\u9009\u62E9\u5668",
                        Toast.LENGTH_SHORT).show();
            }
        } else if (v == newLookBtn) {
            showNewLook();
        } else if (v == newToneBtn) {
            showNewTone();
        } else if (v == favKaoBtn) {
            showKaoFav();
        } else if (v == btnSave) {
            String t = rulesBox.getText().toString();
            Rules.saveText(this, t);
            cached = Rules.parse(t);
            updatePreview();
            Toast.makeText(this, "已保存", Toast.LENGTH_SHORT).show();
        } else if (v == btnDefaults) {
            String d = Rules.defaultRulesText(this);
            rulesBox.setText(d);
            Rules.saveText(this, d);
            cached = Rules.parse(d);
            updatePreview();
            Toast.makeText(this, "已恢复默认", Toast.LENGTH_SHORT).show();
        } else if (v.getTag() instanceof Integer) {
            int idx = ((Integer) v.getTag()).intValue();
            // one listener serves every button on the screen; ranges keep them apart
            if (idx >= TAB_TAG && idx < TAB_TAG + TABS.length) {
                currentTab = idx - TAB_TAG;
                syncPages();
                // ★ 程度挪到风格页了 ✧ 那一页也要刷新按钮高亮 ✓
        if (TABS[currentTab].equals("设置") || TABS[currentTab].equals("风格")) syncSettings();
            } else if (idx == SLIDE_APPLY) {
                int pct;
                try {
                    pct = Integer.parseInt(setSlidePctField.getText().toString().trim());
                } catch (Exception e) {
                    pct = -1;
                }
                if (pct < 0) {
                    Toast.makeText(this, "请填一个数字（10 - 300）", Toast.LENGTH_SHORT).show();
                } else {
                    // ★ 别叫 v —— onClick(View v) 的参数已经占了那个名字（这个坑我踩两次了）
                    int pv = Math.max(10, Math.min(300, pct));
                    prefs().edit().putInt("slide_pct", pv).apply();
                    if (setSlidePctField != null) setSlidePctField.setText(String.valueOf(pv));
                    Toast.makeText(this, "滑块比例已设为 " + pv + "%", Toast.LENGTH_SHORT).show();
                }
            } else if (idx == DELAY_APPLY) {
                int ms;
                try {
                    ms = Integer.parseInt(setDelayField.getText().toString().trim());
                } catch (Exception e) {
                    ms = -1;
                }
                if (ms < 0) {
                    Toast.makeText(this, "请填一个数字（100 - 5000）", Toast.LENGTH_SHORT).show();
                } else {
                    applyDelayMs(ms);
                }
            } else if (idx >= FADE_TAG && idx < FADE_TAG + FADE_PRESETS.length) {
                applyFadeMs(FADE_PRESETS[idx - FADE_TAG]);
            } else if (idx == FADE_APPLY) {
                // 注意别叫 v —— onClick(View v) 的参数已经占了那个名字
                int ms;
                try {
                    ms = Integer.parseInt(setFadeField.getText().toString().trim());
                } catch (Exception e) {
                    ms = fadeMs();      // 填的不是数字就用回原来的
                }
                applyFadeMs(ms);
            } else if (idx >= DELAY_TAG && idx < DELAY_TAG + DELAYS.length) {
                applyLevelStyle(idx - DELAY_TAG);
            } else if (idx == REFRESH_LOG) {
                if (traceBox != null) traceBox.setText(Trace.dump());
                Toast.makeText(this, "日志已刷新", Toast.LENGTH_SHORT).show();
            } else if (idx == WECHAT_MODE) {
                // 只检查"系统里有没有【别的】读屏服务在跑"，然后引导用户。
                // 不自己去请求触摸探索 —— 实测系统不采纳，而且万一采纳了，
                // 因为没有焦点高亮框，手机会变成看不见选中的盲操作。
                boolean hasReader = false;
                String readerPkg = "";
                try {
                    android.view.accessibility.AccessibilityManager am =
                        (android.view.accessibility.AccessibilityManager)
                        getSystemService(ACCESSIBILITY_SERVICE);
                    if (am != null) {
                        java.util.List<android.accessibilityservice.AccessibilityServiceInfo> list =
                            am.getEnabledAccessibilityServiceList(
                                android.accessibilityservice.AccessibilityServiceInfo.FEEDBACK_SPOKEN);
                        if (list != null) {
                            for (int i = 0; i < list.size(); i++) {
                                android.accessibilityservice.AccessibilityServiceInfo asi = list.get(i);
                                if (asi == null || asi.getResolveInfo() == null
                                        || asi.getResolveInfo().serviceInfo == null) continue;
                                String pn = asi.getResolveInfo().serviceInfo.packageName;
                                if (pn == null || pn.length() == 0) continue;
                                if ("com.miaohua.helper".equals(pn)) continue;   // 不算自己
                                hasReader = true;
                                readerPkg = pn;
                                break;
                            }
                        }
                    }
                } catch (Exception e) { }
                if (hasReader) {
                    Toast.makeText(this, "检测到读屏服务（" + readerPkg
                                   + "）正在运行 —— 现在去微信试试就行",
                                   Toast.LENGTH_LONG).show();
                } else {
                    Toast.makeText(this, "没检测到读屏服务 —— 微信需要它。这就带你去无障碍设置",
                                   Toast.LENGTH_LONG).show();
                    try {
                        startActivity(new Intent(
                                android.provider.Settings.ACTION_ACCESSIBILITY_SETTINGS));
                    } catch (Exception e) { }
                }            } else if (idx == ACCESS_SETTINGS) {
                try {
                    startActivity(new Intent(android.provider.Settings.ACTION_ACCESSIBILITY_SETTINGS));
                } catch (Exception e) {
                    Toast.makeText(this, "打不开系统设置，请手动去 设置 → 无障碍",
                                   Toast.LENGTH_LONG).show();
                }
            } else if (idx == CLEAR_LOG) {
                Trace.clear();
                if (traceBox != null) traceBox.setText(Trace.dump());
                Toast.makeText(this, "日志已清空，现在去重现问题", Toast.LENGTH_SHORT).show();
            } else if (idx >= LEVEL_TAG && idx < LEVEL_TAG + LEVELS.length) {
                int li = idx - LEVEL_TAG;
                // ★ 和电脑版一样：写一个「程度」就行 ✧
                //   解析器会把它换成 每句 / 逗号也加 两个开关 ✓
                commit(Rules.setKey(rulesBox.getText().toString(), "程度", "level", LEVELS[li]),
                       "已改成「" + LEVELS[li] + "」");
                // ★★★ 重刷一下 ✧ 不然那三个按钮的颜色不会动 ★★★
                //   "延迟"那组按钮就是这么做的（applyLevelStyle 里调了 syncSettings ✓）
                //   我第一版漏了这一句 ✧ 所以点了没反应 ✓
                syncSettings();
            } else if (idx == SET_AUTO) {
                boolean on = setAuto.isChecked();
                commit(Rules.setKey(rulesBox.getText().toString(), "自动模式", "automode",
                                    on ? "true" : "false"),
                       on ? "已开启自动转换" : "已关闭自动转换");
            } else if (idx == KEEP_ALIVE) {
                boolean on = setKeepAlive.isChecked();
                KeepAliveService.setEnabled(this, on);
                Toast.makeText(this, on ? "已开启常驻运行，通知栏会多一条" : "已关闭常驻运行",
                               Toast.LENGTH_SHORT).show();
                // Android 13 起通知要授权，不然那条常驻通知不显示（服务本身照跑）
                if (on && android.os.Build.VERSION.SDK_INT >= 33) {
                    try {
                        if (checkSelfPermission("android.permission.POST_NOTIFICATIONS")
                                != android.content.pm.PackageManager.PERMISSION_GRANTED) {
                            requestPermissions(new String[] { "android.permission.POST_NOTIFICATIONS" }, 77);
                        }
                    } catch (Exception e) { }
                }
            } else if (idx == SET_TINY) {
                boolean on = setTiny.isChecked();
                commit(Rules.setKey(rulesBox.getText().toString(), "小模型语气", "tinymood",
                                    on ? "true" : "false"),
                       on ? "已开启语气小模型 ✧ 关键词判不出时会问它 ✓"
                          : "已关闭语气小模型");
            } else if (idx == SET_FLOAT) {
                boolean on = setFloat.isChecked();
                commit(Rules.setKey(rulesBox.getText().toString(), "悬浮窗", "floatbar",
                                    on ? "true" : "false"),
                       on ? "已开启悬浮窗 ✧ 不会再自动转了 ✓"
                          : "已关闭悬浮窗");
                // 开的时候顺便看一眼权限 ✧ 没给就提一句 ✓
                if (on) {
                    boolean perm = false;
                    try { perm = android.provider.Settings.canDrawOverlays(this); } catch (Exception e) { }
                    if (!perm) {
                        Toast.makeText(this, "还差一个权限 ✧ 点下面那个「授予悬浮窗权限」✓",
                                       Toast.LENGTH_LONG).show();
                    }
                }
                // ★ 2026-10-31：顺序不能反 ✗
                //   syncSettings() 里面会调无动画版本 ✗ 动画会被立即取消 ✓
                skipFloatSync = true;
                syncSettings();
                skipFloatSync = false;
                syncFloatOpts(true);
            } else if (idx == SET_FMODE_Q) {
                cbModeAlways.setChecked(false);
                if (!cbModeQq.isChecked()) cbModeQq.setChecked(true);
                commit(Rules.setKey(rulesBox.getText().toString(), "\u60AC\u6D6E\u7A97\u6A21\u5F0F", "floatmode", "qq"),
                       "\u663E\u793A\u65F6\u673A\uFF1A\u53EA\u5728\u751F\u6548\u5E94\u7528\u91CC");
            } else if (idx == SET_FMODE_A) {
                cbModeQq.setChecked(false);
                if (!cbModeAlways.isChecked()) cbModeAlways.setChecked(true);
                commit(Rules.setKey(rulesBox.getText().toString(), "\u60AC\u6D6E\u7A97\u6A21\u5F0F", "floatmode", "always"),
                       "\u663E\u793A\u65F6\u673A\uFF1A\u4E00\u76F4\u663E\u793A");
            } else if (idx == SET_TAIL) {
                boolean on = cbTail.isChecked();
                commit(Rules.setKey(rulesBox.getText().toString(), "\u7ED3\u679C\u5C3E\u5DF4", "tail",
                                    on ? "true" : "false"),
                       on ? "\u5DF2\u5F00\u542F\u7ED3\u679C\u5C0F\u5C3E\u5DF4" : "\u5DF2\u5173\u95ED\u7ED3\u679C\u5C0F\u5C3E\u5DF4");
            } else if (idx == FLOAT_PERM) {
                // ★ 只是跳到系统页面 ✧ 由用户自己打开 ✓
                try {
                    Intent it = new Intent(android.provider.Settings.ACTION_MANAGE_OVERLAY_PERMISSION,
                                           android.net.Uri.parse("package:" + getPackageName()));
                    startActivity(it);
                    Toast.makeText(this, "把「允许显示在其他应用上层」打开", Toast.LENGTH_LONG).show();
                } catch (Exception e) {
                    try {
                        startActivity(new Intent(android.provider.Settings.ACTION_MANAGE_OVERLAY_PERMISSION));
                    } catch (Exception e2) {
                        Toast.makeText(this, "打不开系统页面 ✧ 请手动去设置里找「显示在其他应用上层」✓",
                                       Toast.LENGTH_LONG).show();
                    }
                }
            } else if (idx == BATT_OPT) {
                // 只是【跳到系统页面】，不申请任何敏感权限 —— 用户自己决定加不加白名单。
                try {
                    startActivity(new Intent(android.provider.Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS));
                    Toast.makeText(this, "在列表里找到「喵化助手」，改成「不优化」", Toast.LENGTH_LONG).show();
                } catch (Exception e) {
                    try { startActivity(new Intent(android.provider.Settings.ACTION_SETTINGS)); } catch (Exception e2) { }
                }
            } else if (idx == SAVE_PKGS) {
                java.util.List<String> pkgs = new java.util.ArrayList<String>();
                for (String line : setPkgs.getText().toString().split("\n")) {
                    String t = line.trim();
                    if (t.length() > 0) pkgs.add(t);
                }
                if (pkgs.isEmpty()) pkgs.add("com.tencent.mobileqq");
                commit(Rules.setKeys(rulesBox.getText().toString(), "包名", "package", pkgs),
                       "已保存 " + pkgs.size() + " 个应用");
                syncSettings();
            } else if (idx >= 1000) {
                applyToneStyle(idx - 1000);
            } else {
                applyStyle(idx);
            }
        }
    }

    @Override public void beforeTextChanged(CharSequence s, int a, int b, int c) { }
    @Override public void onTextChanged(CharSequence s, int a, int b, int c) { }

    @Override
    public void afterTextChanged(Editable s) {
        updatePreview();
    }

    void updatePreview() {
        if (cached == null) cached = Rules.parse(rulesBox.getText().toString());
        testOut.setText(cached.transform(testIn.getText().toString()));
    }

    boolean serviceEnabled() {
        try {
            String enabled = Settings.Secure.getString(getContentResolver(),
                    Settings.Secure.ENABLED_ACCESSIBILITY_SERVICES);
            if (enabled == null) return false;
            if (enabled.contains(getPackageName() + "/" + MiaohuaService.class.getName())) return true;
            return enabled.contains(getPackageName() + "/." + MiaohuaService.class.getSimpleName());
        } catch (Exception e) {
            return false;
        }
    }

    void updateStatus() {
        if (statusView == null) return;
        // ② 头部台词跟着状态走
        if (headerMood != null) {
            boolean on = serviceEnabled();
            headerMood.setText(on ? "喵化助手" : "还没开启喵…");
            headerSub.setText(on ? "打完字停一下，整段变成喵喵的"
                                 : "需要无障碍权限，下面的红框里可以开");
            headerSub.setTextColor(on ? MUTED : WARN);
        }
        if (serviceEnabled()) {
            // ③ 原来是整块绿色。改成粉白主题 —— 和程序其它地方一套颜色，
            // 开启状态不该看起来像"警告已解除"的绿色提示条。
            // 未开启那一支保留红色：那是真的需要处理的事，醒目是对的。
            statusCard.setBackground(rounded(SOFT, ACCENT, 14, 2));
            statusView.setText("✓  已经开启了");
            statusView.setTextColor(ACCENT);
            statusHint.setText("现在去 QQ 里打字，停手一会儿它就会自己变。\n觉得太快或太慢，去「设置」页调等待时间。");
            statusHint.setTextColor(TEXT);
            btnOpen.setText("重新打开无障碍设置");
            btnOpen.setTextColor(ACCENT);
            btnOpen.setBackground(rounded(CARD, ACCENT, 12, 1));
        } else {
            statusCard.setBackground(rounded(WARN_BG, WARN, 14, 2));
            statusView.setText("✗  还没开启");
            statusView.setTextColor(WARN);
            statusHint.setText("喵化助手需要无障碍权限，才能读到你正在打的字。\n"
                             + "在系统设置的「已下载的服务」里找到「喵化助手」并打开它。");
            statusHint.setTextColor(WARN_TX);
            btnOpen.setText("去开启无障碍服务");
            btnOpen.setTextColor(android.graphics.Color.WHITE);
            btnOpen.setBackground(rounded(WARN, WARN, 12, 0));
        }
    }

    // ------------------------------------------------ 风格选中状态

    android.content.SharedPreferences prefs() {
        return getSharedPreferences(PREF, MODE_PRIVATE);
    }

    void rememberStyle(boolean look, String name) {
        if (look) appliedLook = name; else appliedTone = name;
        prefs().edit()
               .putString("look", appliedLook)
               .putString("tone", appliedTone)
               .apply();
    }

    /// Restores the highlight on launch. The look style can also be worked out from the
    /// file itself (same rule the PC uses: suffix + how many kaomoji), which covers the case
    /// where the rules were replaced or copied in from the desktop.
    void loadStyleSelection() {
        android.content.SharedPreferences sp = prefs();
        appliedLook = sp.getString("look", null);
        appliedTone = sp.getString("tone", null);

        String text = rulesBox.getText().toString();

        if (appliedLook == null) {
            String sfx = Rules.getKey(text, "后缀", "suffix");
            int n = Rules.getKeys(text, "颜文字", "kaomoji").size();
            for (int i = 0; i < STYLES.length; i++) {
                if (STYLES[i].length - 3 == n && STYLES[i][2].equals(sfx)) {
                    appliedLook = STYLES[i][0];
                    break;
                }
            }
        }

        // The remembered tone style is only true while 语气 is actually on. Picking a look
        // style switches the wrapper off, so leaving the old chip lit made it look as if both
        // were selected at once - which is exactly what it looked like on the phone.
        String tone = Rules.getKey(text, "语气", "tone");
        if (!Rules.parseBoolSafe(tone)) appliedTone = DRY_NAME;

        paintStyleButtons();
    }

    static final String DRY_NAME = "\u4E0D\u52A0\u8BED\u6C14";   // 不加语气

    void paintStyleButtons() {
        paintRow(lookButtons, appliedLook, null);
        paintRow(toneButtons, appliedTone, TONE_STYLES_NAMES);
    }

    /// A chosen chip gets the accent colour, a thicker border and a ✓. The label is rebuilt
    /// each time rather than stored, because the look chips carry a two-line sample.
    void paintRow(TextView[] btns, String selected, String[] toneNames) {
        if (btns == null) return;
        for (int i = 0; i < btns.length; i++) {
            if (btns[i] == null) continue;
            String name;
            String label;
            if (toneNames == null) {
                if (i >= STYLES.length) continue;
                name = STYLES[i][0];
                label = STYLES[i][0] + "\n你好" + STYLES[i][2];
            } else {
                if (i >= toneNames.length) continue;
                name = toneNames[i];
                label = toneNames[i];
            }
            boolean on = name.equals(selected);
            btns[i].setText(on ? "✓ " + label : label);
            btns[i].setTextColor(on ? ACCENT : TEXT);
            btns[i].setBackground(rounded(on ? SEL_BG : CARD, on ? ACCENT : LINE, 12, on ? 2 : 1));
        }
    }
}
