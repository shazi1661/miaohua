package com.miaohua.helper;

import android.animation.ValueAnimator;
import android.os.Handler;
import android.view.animation.AccelerateInterpolator;
import android.view.animation.DecelerateInterpolator;
import android.content.Context;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.graphics.PixelFormat;
import android.graphics.Outline;
import android.graphics.drawable.GradientDrawable;
import android.os.Build;
import android.text.TextUtils;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.ViewOutlineProvider;
import android.view.WindowManager;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

/**
 * ★★★ 悬浮窗（手动转换模式）★★★
 *
 * 用户 2026-10-16 要的：
 *   "像电脑端那个热键转换 ✧ 不进行自动转换 ✧
 *    显示将要转化的内容 ✧ 点一下按钮就能转换 ✧
 *    边上加一个换一种附加语句 ✧ 不喜欢就换一个"
 *
 * 交互：
 *   · 输入框【有字】的时候冒出一个半透明小圆球 ✧ 空了就消失 ✓
 *   · 点小球 → 展开面板 ✧ 上面显示转换后的预览 ✓
 *   · 面板两个按钮：【转换】写进输入框 ✧【换一个】重新随机 ✓
 *   · 小球可拖 ✧ 位置记在 SharedPreferences 里 ✓
 *
 * ★★★ 这里【一个匿名内部类都不能有】★★★
 *
 *   d8 8.2.2 处理不了匿名/泛型内部类 ✗ 会报
 *       NullPointerException ... at com.android.tools.r8.graph.u2.<init>
 *   而且【编译期不报错】✗ 是 dex 那一步才炸 ✗ 很难查 ✓
 *   —— ✗ 这个坑 2026-10-16 一天之内踩了三次 ✓
 *     （匿名 Runnable ✓ 匿名 Comparator ✓ 这次是全套监听器 ✓）
 *
 *   所以监听器全部写成【具名静态嵌套类 + 构造时传入外层的引用】✓
 *   而且多个按钮共用一个 Click 类 ✗ 用 action 区分 ✗ 少写几个类 ✓
 *
 * ★ 悬浮窗用 TYPE_APPLICATION_OVERLAY（API 26+）✧ minSdk 正好 26 ✓
 * ★ FLAG_NOT_FOCUSABLE：不抢输入焦点 ✧ 但按钮照样能点 ✓
 */
public class FloatBar {

    /** Service 要实现这三个 ✓ */
    public interface Host {
        /** 点【转换】✧ 把当前预览写进输入框 ✓ */
        void onConvert();
        /** 点【换一个】✧ 从【原文】重新转换 ✓ */
        void onReroll();
        /** 展开面板时叫一次 ✧ Service 借机刷新预览 ✓ */
        void onRefreshPreview();
        /** ★ 2026-10-18：撤销上一条 ✧ 把刚才喵化的那句还原回去 ✓ */
        void onUndo();
    }

    // 按钮动作编号（一个类搞定所有点击 ✓）
    static final int ACT_CONVERT = 1;
    static final int ACT_REROLL = 2;
    static final int ACT_COLLAPSE = 3;
    static final int ACT_UNDO = 4;

    static final String PREF = "miaohua_float";
    static final String K_X = "ball_x";
    static final String K_Y = "ball_y";

    private final Context ctx;
    private final Host host;
    private final WindowManager wm;
    private final SharedPreferences sp;

    private View ball;
    private View panel;
    private TextView preview;
    private TextView tip;

    private WindowManager.LayoutParams ballLp;
    private WindowManager.LayoutParams panelLp;

    // \u2605 2026-10-31\uff1a\u7ed3\u679c\u5c0f\u5c3e\u5df4 \u2717 \u4e00\u4e2a\u72ec\u7acb\u7684\u5c0f\u60ac\u6d6e\u7a97\u2713
    private View tail;
    private TextView tailText;
    private WindowManager.LayoutParams tailLp;
    private boolean tailAdded = false;
    /** \u2605 \u5f00\u5173\u7531 Service \u6309 rules.resultTail \u63a8\u8fdb\u6765 \u2713 */
    public boolean tailEnabled = true;

    private boolean added = false;
    private boolean expanded = false;

    // \u2605 2026-10-31\uff1a\u5c55\u5f00 / \u6536\u8d77\u7684\u52a8\u753b\u72b6\u6001 \u2713
    private boolean panelAdded = false;
    private boolean collapsing = false;
    private final Handler ui = new Handler();

    FloatBar(Context ctx, Host host) {
        this.ctx = ctx;
        this.host = host;
        this.wm = (WindowManager) ctx.getSystemService(Context.WINDOW_SERVICE);
        this.sp = ctx.getSharedPreferences(PREF, Context.MODE_PRIVATE);
    }

    // ---------------------------------------------------------------- 尺寸 / 圆角

    static int dp(Context c, float v) {
        return (int) TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_DIP, v,
                c.getResources().getDisplayMetrics());
    }

    static GradientDrawable round(int color, float radiusPx, int strokeW, int strokeColor) {
        GradientDrawable d = new GradientDrawable();
        d.setShape(GradientDrawable.RECTANGLE);
        d.setColor(color);
        d.setCornerRadius(radiusPx);
        if (strokeW > 0) d.setStroke(strokeW, strokeColor);
        return d;
    }

    static int overlayType() {
        return Build.VERSION.SDK_INT >= 26
                ? WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY
                : WindowManager.LayoutParams.TYPE_PHONE;
    }

    // ---------------------------------------------------------------- 点击 / 拖动
    // ★ 全部是具名静态类 ✗ 见类注释 ✓

    /**
     * ★ 把子控件裁成【正圆】✗ 否则图标会露出方角 ✓
     *
     * 为什么不是匿名类 ✗ 见类注释：d8 8.2.2 处理不了匿名内部类 ✗
     * dex 那一步才炸 ✗ 编译期看不出来 ✓
     */
    static class OvalOutline extends ViewOutlineProvider {
        public void getOutline(View v, Outline o) {
            o.setOval(0, 0, v.getWidth(), v.getHeight());
        }
    }

    /**
     * ★ 吸附动画 ✧ 从 from 滑到 to （均为 屏幕 x 坐标 ✓）
     *
     * 为什么是【具名静态类】✧ 见类注释：d8 处理不了匿名内部类 ✗
     */
    static class SnapAnim implements ValueAnimator.AnimatorUpdateListener {
        final FloatBar bar;
        final int from, to;
        SnapAnim(FloatBar b, int f, int t) { bar = b; from = f; to = t; }
        public void onAnimationUpdate(ValueAnimator a) {
            float p = a.getAnimatedFraction();
            bar.ballLp.x = (int) (from + (to - from) * p);
            try { bar.wm.updateViewLayout(bar.ball, bar.ballLp); } catch (Throwable t) { }
            bar.moveTailWithBall();
        }
    }

    static class Click implements View.OnClickListener {
        final FloatBar bar;
        final int act;
        Click(FloatBar b, int a) { bar = b; act = a; }
        public void onClick(View v) { bar.doAction(act); }
    }

    static class Drag implements View.OnTouchListener {
        final FloatBar bar;
        float downX, downY;
        int startX, startY;
        boolean moved;
        Drag(FloatBar b) { bar = b; }

        public boolean onTouch(View view, MotionEvent e) {
            switch (e.getActionMasked()) {
                case MotionEvent.ACTION_DOWN:
                    downX = e.getRawX(); downY = e.getRawY();
                    startX = bar.ballLp.x; startY = bar.ballLp.y;
                    moved = false;
                    // \u2605 \u6309\u4e0b\u7f29\u5230 92% \u2727 \u624b\u611f\u66f4\u50cf\u539f\u751f \u2713
                    if (Build.VERSION.SDK_INT >= 21) {
                        view.animate().scaleX(0.92f).scaleY(0.92f).setDuration(90).start();
                    }
                    return true;
                case MotionEvent.ACTION_MOVE: {
                    int dx = (int) (e.getRawX() - downX);
                    int dy = (int) (e.getRawY() - downY);
                    if (Math.abs(dx) > dp(bar.ctx, 6) || Math.abs(dy) > dp(bar.ctx, 6)) moved = true;
                    if (moved) {
                        bar.ballLp.x = startX + dx;
                        bar.ballLp.y = startY + dy;
                        try { bar.wm.updateViewLayout(bar.ball, bar.ballLp); } catch (Throwable t) { }
                        bar.moveTailWithBall();
                    }
                    return true;
                }
                case MotionEvent.ACTION_UP:
                    // \u2605 \u677e\u5f00\u5f39\u56de \u2713
                    if (Build.VERSION.SDK_INT >= 21) {
                        view.animate().scaleX(1f).scaleY(1f).setDuration(140).start();
                    }
                    if (moved) {
                        // ★★★ 用户 2026-10-17：吸附到屏幕边缘 ★★★
                        //   球心在左半 ✧ 吸左边 ✧ 右半 ✧ 吸右边 ✓
                        //   带 220ms 滑动动画 ✧ 不是瞬移 ✓
                        bar.snapToEdge();
                        bar.sp.edit().putInt(K_X, bar.ballLp.x)
                                     .putInt(K_Y, bar.ballLp.y).apply();
                        // 拖完就不当点击了 ✧ 不然一拖就弹面板 ✓
                    } else {
                        if (bar.expanded) bar.collapse(); else bar.expand();
                    }
                    return true;
                case MotionEvent.ACTION_CANCEL:
                    // \u2605 \u88ab\u53d6\u6d88\u4e86\u4e5f\u8981\u5f39\u56de \u2727 \u4e0d\u7136\u4e00\u76f4\u7f29\u7740 \u2713
                    if (Build.VERSION.SDK_INT >= 21) {
                        view.animate().scaleX(1f).scaleY(1f).setDuration(140).start();
                    }
                    return true;
            }
            return false;
        }
    }

    /** ★ 吸附到屏幕左/右边 ✧ 带 220ms 滑动动画 ✓ */
    void snapToEdge() {
        try {
            if (ball == null || ballLp == null) return;
            int screenW = ctx.getResources().getDisplayMetrics().widthPixels;
            int size = ballLp.width > 0 ? ballLp.width : dp(ctx, 46);
            int margin = dp(ctx, 4);
            int left = margin;
            int right = Math.max(left, screenW - size - margin);
            int center = ballLp.x + size / 2;
            int target = (center < screenW / 2) ? left : right;
            int cur = ballLp.x;
            if (Math.abs(target - cur) < dp(ctx, 2)) {
                ballLp.x = target;
                try { wm.updateViewLayout(ball, ballLp); } catch (Throwable t) { }
                return;
            }
            ValueAnimator va = ValueAnimator.ofFloat(0f, 1f);
            va.setDuration(220);
            va.addUpdateListener(new SnapAnim(this, cur, target));
            va.start();
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  吸附出错：" + t);
        }
    }

    void doAction(int act) {
        try {
            if (act == ACT_CONVERT) host.onConvert();
            else if (act == ACT_REROLL) host.onReroll();
            else if (act == ACT_COLLAPSE) collapse();
            else if (act == ACT_UNDO) host.onUndo();
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  悬浮窗动作出错：" + t);
        }
    }

    // ---------------------------------------------------------------- 显示 / 隐藏

    /** 输入框有字 ✧ 叫这个 ✓ 重复调用没事 ✓ */
    public void showBall() {
        if (added) return;
        try {
            if (ball == null) buildBall();
            wm.addView(ball, ballLp);
            added = true;
            // \u2605 2026-10-31\uff1a\u6de1\u5165 + \u5fae\u5fae\u653e\u5927 \u2717 \u4e0d\u7136\u662f\u201c\u549a\u201d\u5730\u4e00\u4e0b\u51fa\u73b0 \u2713
            if (Build.VERSION.SDK_INT >= 12) {
                ball.setAlpha(0f);
                ball.setScaleX(0.85f);
                ball.setScaleY(0.85f);
                ball.animate().alpha(1f).scaleX(1f).scaleY(1f)
                        .setDuration(230)
                        .setInterpolator(new DecelerateInterpolator(1.5f))
                        .start();
            }
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  悬浮球加不上：" + t);
        }
    }

    /** 输入框空了 ✧ 或切到别的应用 ✧ 叫这个 ✓ */
    public void hideAll() {
        collapse();
        hideTail();
        if (!added) return;
        try {
            if (ball != null) wm.removeView(ball);
        } catch (Throwable t) { }
        added = false;
    }

    public boolean isShowing() { return added; }
    public boolean isExpanded() { return expanded; }

    // ---------------------------------------------------------------- 小球

    private void buildBall() {
        // ★ 用户 2026-10-17：换成软件图标 + 美化 ✓
        //
        // 之前是一个紫色圆 + 白字「喵」 ✧ 跟软件的形象没关系 ✗
        // 现在是一个【圆形头像球】：
        //   · 白色圆底 + 淡粉描边（跟 app 的粉色主题一致 ✓）
        //   · 图标裁成正圆 ✧ 不会露出方角 ✓
        //   · 阴影 6dp ✧ 浮在聊天界面上更立体 ✓
        //   · 44 → 46dp ✧ 头像看得清 ✓

        FrameLayout wrap = new FrameLayout(ctx);

        GradientDrawable bg = new GradientDrawable();
        bg.setShape(GradientDrawable.OVAL);
        bg.setColor(0xFFFFFFFF);
        // 淡粉描边 ✧ 和界面主色同一个色系 ✓
        bg.setStroke(dp(ctx, 2), 0x40D96AA0);
        wrap.setBackground(bg);
        if (Build.VERSION.SDK_INT >= 21) {
            wrap.setElevation(dp(ctx, 6));
            wrap.setOutlineProvider(new OvalOutline());
            wrap.setClipToOutline(true);      // ★ 关键：把子控件裁成圆 ✓
        }

        ImageView iv = new ImageView(ctx);
        iv.setImageResource(R.mipmap.ic_launcher);
        iv.setScaleType(ImageView.ScaleType.CENTER_CROP);
        int inset = dp(ctx, 2);               // 留 2dp 给描边 ✧ 否则边被图标盖住 ✓
        FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT);
        lp.setMargins(inset, inset, inset, inset);
        wrap.addView(iv, lp);

        int w = dp(ctx, 46);
        ballLp = new WindowManager.LayoutParams(
                w, w, overlayType(),
                WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE
                        | WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS,
                PixelFormat.TRANSLUCENT);
        ballLp.gravity = Gravity.TOP | Gravity.START;
        ballLp.x = sp.getInt(K_X, dp(ctx, 12));
        ballLp.y = sp.getInt(K_Y, dp(ctx, 320));

        wrap.setOnTouchListener(new Drag(this));
        ball = wrap;
    }

    // ---------------------------------------------------------------- 面板

    private void buildPanel() {
        // ★ 用户 2026-10-17：面板美化 ✓
        //   圆角 14→18 ✧ 边框更淡 ✧ 阴影 8→12dp ✧ 内边距 10→14 ✓
        LinearLayout root = new LinearLayout(ctx);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackground(round(0xF7FFFFFF, dp(ctx, 18), dp(ctx, 1), 0x1F000000));
        if (Build.VERSION.SDK_INT >= 21) root.setElevation(dp(ctx, 12));
        root.setClipToOutline(true);
        int pad = dp(ctx, 14);
        root.setPadding(pad, pad, pad, dp(ctx, 10));

        // ★ 粉色标题行 ✧ 小头像 + 名字 ✓
        LinearLayout head = new LinearLayout(ctx);
        head.setOrientation(LinearLayout.HORIZONTAL);
        head.setGravity(Gravity.CENTER_VERTICAL);

        ImageView mini = new ImageView(ctx);
        mini.setImageResource(R.mipmap.ic_launcher);
        mini.setScaleType(ImageView.ScaleType.CENTER_CROP);
        GradientDrawable miniBg = new GradientDrawable();
        miniBg.setShape(GradientDrawable.OVAL);
        miniBg.setColor(0xFFFFFFFF);
        miniBg.setStroke(dp(ctx, 1), 0x40D96AA0);
        mini.setBackground(miniBg);
        if (Build.VERSION.SDK_INT >= 21) {
            mini.setClipToOutline(true);
            mini.setOutlineProvider(new OvalOutline());
        }
        int ms = dp(ctx, 22);
        head.addView(mini, new LinearLayout.LayoutParams(ms, ms));

        TextView title = new TextView(ctx);
        title.setText("\u55b5\u5316\u52a9\u624b");
        title.setTextSize(13);
        title.setTextColor(0xFFD96AA0);
        title.setPadding(dp(ctx, 6), 0, 0, 0);
        head.addView(title);
        root.addView(head);

        // ★ 一条淡粉分隔线 ✓
        View div = new View(ctx);
        div.setBackgroundColor(0x1FD96AA0);
        LinearLayout.LayoutParams dlp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, dp(ctx, 1));
        dlp.topMargin = dp(ctx, 8);
        dlp.bottomMargin = dp(ctx, 2);
        div.setLayoutParams(dlp);
        root.addView(div);

        tip = new TextView(ctx);
        tip.setText("\u5c06\u8981\u53d8\u6210\uff1a");
        tip.setTextSize(12);
        tip.setTextColor(0xFF9A8A92);
        LinearLayout.LayoutParams tlp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        tlp.topMargin = dp(ctx, 6);
        tip.setLayoutParams(tlp);
        root.addView(tip);

        // ★ 预览区换成「淡粉圆角底」✧ 像聊天气泡 ✓
        ScrollView sc = new ScrollView(ctx);
        LinearLayout.LayoutParams slp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, dp(ctx, 96));
        slp.topMargin = dp(ctx, 6);
        sc.setLayoutParams(slp);
        sc.setBackground(round(0xFFFFF3F8, dp(ctx, 12), 0, 0));
        int sp2 = dp(ctx, 10);
        sc.setPadding(sp2, sp2, sp2, sp2);
        sc.setClipToOutline(true);

        preview = new TextView(ctx);
        preview.setTextSize(15);
        preview.setTextColor(0xFF3A2A34);
        preview.setLineSpacing(0, 1.2f);
        sc.addView(preview);
        root.addView(sc);

        LinearLayout row = new LinearLayout(ctx);
        row.setOrientation(LinearLayout.HORIZONTAL);
        LinearLayout.LayoutParams rlp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        rlp.topMargin = dp(ctx, 8);
        row.setLayoutParams(rlp);

        // ★ 按钮换成 app 的粉色调 ✧ 原来是紫色 ✗
        Button ok = mkButton("\u8f6c\u6362", 0xFFD96AA0, Color.WHITE);
        ok.setOnClickListener(new Click(this, ACT_CONVERT));
        row.addView(ok, weight());

        Button again = mkButton("\u6362\u4e00\u4e2a", 0xFFFFE4EF, 0xFFC2537F);
        again.setOnClickListener(new Click(this, ACT_REROLL));
        LinearLayout.LayoutParams alp = weight();
        alp.leftMargin = dp(ctx, 8);
        row.addView(again, alp);

        root.addView(row);

        // ★ 2026-10-18：撤销上一条 ✓
        Button undo = mkButton("\u21B6 \u64A4\u9500\u4E0A\u4E00\u6761", 0xFFFFE4EF, 0xFFC2537F);
        undo.setOnClickListener(new Click(this, ACT_UNDO));
        LinearLayout.LayoutParams ulp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        ulp.topMargin = dp(ctx, 6);
        root.addView(undo, ulp);

        TextView close = new TextView(ctx);
        close.setText("\u6536\u8d77");
        close.setTextSize(12);
        close.setTextColor(0xFFB09AA4);
        close.setGravity(Gravity.CENTER);
        close.setPadding(0, dp(ctx, 6), 0, 0);
        close.setOnClickListener(new Click(this, ACT_COLLAPSE));
        root.addView(close);

        panelLp = new WindowManager.LayoutParams(
                dp(ctx, 240), WindowManager.LayoutParams.WRAP_CONTENT,
                overlayType(),
                WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE,
                PixelFormat.TRANSLUCENT);
        panelLp.gravity = Gravity.TOP | Gravity.START;
        panel = root;
    }

    static LinearLayout.LayoutParams weight() {
        return new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
    }

    private Button mkButton(String text, int bg, int fg) {
        Button b = new Button(ctx);
        b.setText(text);
        b.setAllCaps(false);
        b.setTextSize(14);
        b.setTextColor(fg);
        b.setBackground(round(bg, dp(ctx, 12), 0, 0));
        b.setPadding(0, dp(ctx, 8), 0, dp(ctx, 8));
        b.setMinHeight(0);
        b.setMinimumHeight(0);
        return b;
    }

    private void expand() {
        if (panel == null) buildPanel();
        try { host.onRefreshPreview(); } catch (Throwable t) { }
        // ★ 面板朝【屏幕中间】弹 ✧ 球在右边就往左弹 ✓
        //
        // 原来一律 panelLp.x = ballLp.x + 52dp ✧ 球吸到右边时面板就跑出屏幕 ✗
        int screenW = ctx.getResources().getDisplayMetrics().widthPixels;
        int pw = panelLp.width > 0 ? panelLp.width : dp(ctx, 240);
        int gap = dp(ctx, 8);
        boolean ballLeftHalf = (ballLp.x + dp(ctx, 23)) < screenW / 2;
        panelLp.x = ballLeftHalf ? (ballLp.x + dp(ctx, 52)) : (ballLp.x - pw - gap);
        if (panelLp.x < gap) panelLp.x = gap;
        if (panelLp.x + pw > screenW - gap) panelLp.x = Math.max(gap, screenW - pw - gap);
        panelLp.y = Math.max(0, ballLp.y - dp(ctx, 60));

        // ★★★ 2026-10-31：慢慢展开 ★★★
        //   从球那一侧往外长 ✗ 92% → 100% ✗ 淡入 ✗ 200ms 先快后慢 ✓
        collapsing = false;
        boolean wasAdded = panelAdded;
        if (!panelAdded) {
            panel.setAlpha(0f);
            panel.setScaleX(0.92f);
            panel.setScaleY(0.92f);
            panel.setPivotX(ballLeftHalf ? 0f : (float) pw);
            panel.setPivotY((float) dp(ctx, 60));
            try {
                wm.addView(panel, panelLp);
                panelAdded = true;
            } catch (Throwable t) {
                Trace.add(Trace.now() + "  悬浮面板加不上：" + t);
            }
        } else {
            try { wm.updateViewLayout(panel, panelLp); } catch (Throwable t) { }
        }
        expanded = true;
        if (panel != null && panelAdded) {
            panel.animate().cancel();
            panel.animate().alpha(1f).scaleX(1f).scaleY(1f)
                    .setDuration(wasAdded ? 180 : 300)
                    .setInterpolator(new DecelerateInterpolator(1.6f))
                    .start();
        }
    }

    private void collapse() {
        if (!expanded || panel == null || !panelAdded) { expanded = false; return; }
        expanded = false;
        collapsing = true;
        try {
            panel.animate().cancel();
            panel.animate().alpha(0f).scaleX(0.94f).scaleY(0.94f)
                    .setDuration(210)
                    .setInterpolator(new AccelerateInterpolator())
                    .start();
            ui.postDelayed(new PanelOut(this), 220);
        } catch (Throwable t) {
            try { wm.removeView(panel); } catch (Throwable t2) { }
            panelAdded = false;
            collapsing = false;
        }
    }

    /** \u2605 \u6de1\u51fa\u5b8c\u4e86\u518d\u771f\u6b63\u79fb\u9664 \u2717 \u4e2d\u9014\u53c8\u70b9\u4e86\u5c31\u4e0d\u5220 \u2713 */
    static class PanelOut implements Runnable {
        final FloatBar bar;
        PanelOut(FloatBar b) { bar = b; }
        public void run() {
            if (!bar.collapsing) return;
            bar.collapsing = false;
            try { if (bar.panel != null) bar.wm.removeView(bar.panel); } catch (Throwable t) { }
            bar.panelAdded = false;
        }
    }

    /** Service 把预览推进来 ✧ original 为空表示没读到 ✓ */
    // ---------------------------------------------------------------- 结果小尾巴

    /** ★ Service 把转换结果推进来 ✗ 空串 = 收起 ✓ */
    public void setTail(String transformed) {
        try {
            if (!tailEnabled || TextUtils.isEmpty(transformed)) { hideTail(); return; }
            if (tail == null) buildTail();
            tailText.setText(transformed);

            int screenW = ctx.getResources().getDisplayMetrics().widthPixels;
            int maxW = (int) (screenW * 0.55f);
            tailText.measure(View.MeasureSpec.UNSPECIFIED, View.MeasureSpec.UNSPECIFIED);
            int want = tailText.getMeasuredWidth() + dp(ctx, 22);
            int w = Math.min(want, maxW);
            if (w < dp(ctx, 60)) w = dp(ctx, 60);
            int h = dp(ctx, 32);

            if (tailLp == null) {
                tailLp = new WindowManager.LayoutParams(
                        w, h, overlayType(),
                        WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE
                                | WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS,
                        PixelFormat.TRANSLUCENT);
                tailLp.gravity = Gravity.TOP | Gravity.START;
            }
            tailLp.width = w;
            tailLp.height = h;
            placeTail(w, h);

            if (!tailAdded) {
                wm.addView(tail, tailLp);
                tailAdded = true;
                // \u2605 \u5c0f\u5c3e\u5df4\u4e5f\u6de1\u5165 \u2713
                tail.setAlpha(0f);
                tail.setTranslationX(dp(ctx, ballLp != null && ballLp.x > 0 && tailLp.x > ballLp.x ? 6 : -6));
                tail.animate().alpha(1f).translationX(0f)
                        .setDuration(220)
                        .setInterpolator(new DecelerateInterpolator(1.5f))
                        .start();
            } else {
                wm.updateViewLayout(tail, tailLp);
            }
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  小尾巴显示失败：" + t);
        }
    }

    public void hideTail() {
        if (!tailAdded) return;
        try { if (tail != null) wm.removeView(tail); } catch (Throwable t) { }
        tailAdded = false;
    }

    /** ★ 跟着球走 ✗ 球在左半就放右边 ✓ */
    private void placeTail(int w, int h) {
        if (tailLp == null || ballLp == null) return;
        int screenW = ctx.getResources().getDisplayMetrics().widthPixels;
        int ballSize = dp(ctx, 46);
        int gap = dp(ctx, 6);
        boolean ballLeftHalf = (ballLp.x + ballSize / 2) < screenW / 2;
        tailLp.x = ballLeftHalf ? (ballLp.x + ballSize + gap) : (ballLp.x - w - gap);
        if (tailLp.x < gap) tailLp.x = gap;
        if (tailLp.x + w > screenW - gap) tailLp.x = Math.max(gap, screenW - w - gap);
        tailLp.y = ballLp.y + (ballSize - h) / 2;
    }

    /** ★ 球被拖动 / 吸附时跟上 ✓ */
    public void moveTailWithBall() {
        if (!tailAdded || tailLp == null) return;
        try {
            placeTail(tailLp.width, tailLp.height);
            wm.updateViewLayout(tail, tailLp);
        } catch (Throwable t) { }
    }

    private void buildTail() {
        tailText = new TextView(ctx);
        tailText.setTextSize(12);
        tailText.setTextColor(0xFF3A2A34);
        tailText.setSingleLine(true);
        tailText.setEllipsize(TextUtils.TruncateAt.END);
        tailText.setGravity(Gravity.CENTER_VERTICAL);
        tailText.setPadding(dp(ctx, 10), 0, dp(ctx, 10), 0);
        tailText.setBackground(round(0xF2FFF3F8, dp(ctx, 16), dp(ctx, 1), 0x33D96AA0));
        tailText.setOnClickListener(new TailClick(this));
        if (Build.VERSION.SDK_INT >= 21) tailText.setElevation(dp(ctx, 4));
        tail = tailText;
    }

    /** ★ 点小尾巴 = 点球 ✗ 展开 / 收起 ✓ */
    static class TailClick implements View.OnClickListener {
        final FloatBar bar;
        TailClick(FloatBar b) { bar = b; }
        public void onClick(View v) {
            if (bar.expanded) bar.collapse(); else bar.expand();
        }
    }

    public void setPreview(String original, String transformed) {
        if (preview == null) return;
        if (TextUtils.isEmpty(transformed)) {
            preview.setText("\uff08\u8f93\u5165\u6846\u662f\u7a7a\u7684\uff09");
            return;
        }
        preview.setText(transformed);
        if (tip != null) {
            tip.setText(original != null && original.equals(transformed)
                    ? "\u8fd9\u53e5\u4e0d\u9700\u8981\u53d8\uff1a"
                    : "\u5c06\u8981\u53d8\u6210\uff1a");
        }
    }
}
