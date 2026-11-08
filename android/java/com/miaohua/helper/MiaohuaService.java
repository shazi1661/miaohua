package com.miaohua.helper;

import android.accessibilityservice.AccessibilityService;
import android.content.Intent;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.accessibility.AccessibilityEvent;
import android.view.accessibility.AccessibilityNodeInfo;
import android.view.accessibility.AccessibilityWindowInfo;

/**
 * Watches text changes in the apps listed in the rules file, and rewrites the
 * focused input box in place once you stop typing.
 *
 * Deliberately does nothing else: no window scraping, no clicks, no network.
 *
 * NOTE: no anonymous inner classes here on purpose - see the comment at the top
 * of MainActivity.java for why (d8 crashes on their MethodParameters attribute).
 */
public class MiaohuaService extends AccessibilityService {

    private final Handler handler = new Handler(Looper.getMainLooper());
    Runnable pending;
    private Rules rules;

    // guard against reacting to our own edit
    private String lastWritten = null;
    private long lastWrittenAt = 0L;

    // The write is applied by the editor AFTER this method returns, so an event arriving in
    // that gap reads the box as it was BEFORE the write, computes a second result and writes
    // that too - and since the first write already collapsed the selection, the second one
    // lands next to it instead of over it:
    //     你好喵~ฅ^•ﻌ•^ฅ你好喵~(๑•̀ㅂ•́)و✧
    // Two DIFFERENT kaomoji in one line is the signature: two runs, each reading 你好. Every
    // selection was correct; the two runs simply overlapped. Nothing is read until the editor
    // has had time to settle.
    private long settleUntil = 0L;
    /// 找不到输入框时的重试计数。真机上有些窗口只是"那一刻"读不到，隔一会儿就好了。
    int lookupRetry = 0;

    /// ★★ 事件到来那一瞬间抓到的输入框 ★★
    ///
    /// 真机实测的关键发现（2026-10-04 09:57 的日志）：
    ///   同一个微信窗口，事件到来时 getRoot() 是【有】的，
    ///   250ms 后再看就变成 null 了 —— 说明窗口树【只开放一瞬间】。
    ///
    /// 而原来的流程是：事件 → 等 400ms（防抖）→ 才开始找 → 早就关上了。
    /// 所以要在事件还"热"的时候立刻抓一次，抓到就存下来慢慢用。
    AccessibilityNodeInfo hotInput = null;

    /// 轻量扫描：不问日志，只找节点。事件到来时用。
    /// ★★★ 三个【从来没试过】的官方 API ★★★
    ///
    /// 前面把常规路径试遍了（findFocus / getRoot / 事件 source / 遍历窗口 /
    /// 各种 flag / 深层递归 / 坐标校验），这些都不行。下面三个是仅剩的、
    /// 走【不同实现路径】的官方接口 —— 成功率不高，但试完才叫试完。
    ///
    ///   （原本还想试 AccessibilityNodeInfo.obtain(event) ——
    ///     查了 API 才发现这个方法【不存在】，只有 obtain(View) /
    ///     obtain(AccessibilityNodeInfo) / obtain()。事件那条路只有 getSource()，
    ///     已经试过了。）
    ///   ② AccessibilityWindowInfo.getRoot(prefetchingStrategy)   （Android 13+）
    ///   ③ AccessibilityService.getRootInActiveWindow(prefetchingStrategy)（Android 13+）
    ///      带预取策略的版本。官方文档说预取只是改变"顺带取回哪些节点"，
    ///      理论上不能凭空创造根 —— 但没实测过，就得测。
    static final int PREFETCH_ALL =
            AccessibilityNodeInfo.FLAG_PREFETCH_ANCESTORS
          | AccessibilityNodeInfo.FLAG_PREFETCH_SIBLINGS
          | AccessibilityNodeInfo.FLAG_PREFETCH_DESCENDANTS_HYBRID;

    AccessibilityNodeInfo tryExotic(AccessibilityEvent ev) {


        // ② / ③ 带预取的取根（Android 13+）
        if (android.os.Build.VERSION.SDK_INT >= 33) {
            try {
                AccessibilityNodeInfo r = getRootInActiveWindow(PREFETCH_ALL);
                if (r != null) {
                    AccessibilityNodeInfo d = findEditableDeep(r, 0);
                    if (d != null) return d;
                } else {
                }
            } catch (Exception e) { Trace.add(Trace.now() + "  getRootInActiveWindow(prefetch) 出错：" + e); }
            try {
                java.util.List<AccessibilityWindowInfo> ws2 = getWindows();
                for (int i = 0; ws2 != null && i < ws2.size(); i++) {
                    AccessibilityWindowInfo w2 = ws2.get(i);
                    if (w2 == null) continue;
                    AccessibilityNodeInfo r2 = null;
                    try { r2 = w2.getRoot(PREFETCH_ALL); } catch (Exception e) { }
                    if (r2 == null) continue;
                    Trace.add(Trace.now() + "  ★ 窗口 " + i + " 的 getRoot(prefetch) 有根：" + safeClass(r2));
                    AccessibilityNodeInfo d = findEditableDeep(r2, 0);
                    if (d != null) return d;
                }
            } catch (Exception e) { Trace.add(Trace.now() + "  窗口 getRoot(prefetch) 出错：" + e); }
        }
        return null;
    }

    AccessibilityNodeInfo scanForInput() {
        // ★ 先试最省事的一条：活动窗口的根。
        //   它只调一次 API，比"列窗口 + 逐个 getRoot"快得多 ——
        //   而那个窗口只开放几毫秒，省下来的每一次调用都是机会。
        try {
            AccessibilityNodeInfo root = getRootInActiveWindow();
            if (root != null) {
                AccessibilityNodeInfo t = findEditableDeep(root, 0);
                if (t != null) return t;
            }
        } catch (Exception e) { }
        // 再退回到"列窗口逐个试"。
        try {
            java.util.List<AccessibilityWindowInfo> ws = getWindows();
            for (int i = 0; ws != null && i < ws.size(); i++) {
                AccessibilityWindowInfo w = ws.get(i);
                if (w == null) continue;
                AccessibilityNodeInfo r = null;
                try { r = w.getRoot(); } catch (Exception e) { }
                if (r == null) continue;
                AccessibilityNodeInfo t = findEditableDeep(r, 0);
                if (t != null) return t;
            }
        } catch (Exception e) { }
        return null;
    }
    /// 微信事件字段的诊断配额（只记前 12 条，免得刷屏）
    int eventDumpLeft = 0;          // 平时 0（不打事件字段），排查问题时改成 12
    /// 输入框身份诊断的配额（只记前 6 次，免得刷屏）
    int nodeDescLeft = 2;            // 只记前 2 次输入框身份
    /// ★ 事件自己带来的"是谁触发的"节点。
    /// 有些应用（微信实测）挡住了"读整个窗口"，但事件里仍然带着触发它的那个节点。
    /// 它是事件的一部分，不走读窗口那条路 —— 这是我们漏了很久的一条入口。
    AccessibilityNodeInfo lastEventSource = null;

    // ---------------------------------------------------------------- 悬浮窗
    //
    // ★ 用户 2026-10-16 要的：像电脑端的热键转换 —— 不自动转 ✗
    //   先显示要变成什么 ✗ 点一下才转换 ✗ 不喜欢还能换一个 ✓
    //
    // ★ 为什么必须记住【原文】：
    //   引擎对已经装饰过的文本不是幂等的 ✗ 拿上一次的结果再转一次会越叠越多 ✓
    //   所以"换一个"每次都从 floatOrig 重新转换 ✗ 不是在上一次结果上改 ✓
    //   —— ✗ 这个坑在电脑版踩过 ✓
    FloatBar fbar = null;
    String floatOrig = "";      // 输入框里的原文 ✓
    String floatOut = "";       // 当前预览 ✓
    // ★ 2026-10-31：点完「转换」球收起后 ✗
    //   不能被窗口切换事件又放回来 ✗ 等下次文字变化才重置 ✓
    boolean floatSuppressed = false;

    // ★ 为什么写成具名静态类 ✧ 见 FloatBar 的类注释 ✓
    //   匿名内部类会让 d8 8.2.2 报内部错误 ✧ 而且编译期不报 ✗
    static class FloatHost implements FloatBar.Host {
        private final MiaohuaService svc;
        FloatHost(MiaohuaService s) { svc = s; }
        public void onConvert() { svc.floatApply(); }
        public void onReroll() { svc.floatReroll(); }

        /** ★ 2026-10-18：撤销上一条 ✓ */
        public void onUndo() { svc.undoLast(); }
        public void onRefreshPreview() {
            if (svc.fbar != null) svc.fbar.setPreview(svc.floatOrig, svc.floatOut);
        }
    }

    final FloatBar.Host floatHost = new FloatHost(this);

    /** ★ 2026-10-31：按前台包名决定悬浮球要不要在 ✓
     *   模式 qq（默认）：只在「生效应用」里出现 ✗ 其他地方一律收起
     *   模式 always：一直显示 ✗ 切走不收
     */
        void updateBallForForeground(String pkg) {
        try {
            if (rules == null) return;
            if (!rules.floatBar) {                       // 悬浮窗总开关关着 ✗ 收起
                if (fbar != null) fbar.hideAll();
                return;
            }
            // \u2605 \u8f93\u5165\u6cd5\u4e5f\u4f1a\u53d1\u7a97\u53e3\u5207\u6362\u4e8b\u4ef6 \u2717 \u628a\u5b83\u5f53\u6210\u300c\u5207\u8d70\u300d\u5c31\u4f1a\u8bef\u6536\u7403 \u2713
            if (pkg != null && isImePackage(pkg)) return;
            boolean always = "always".equals(rules.floatMode);
            boolean inApp = pkg != null && rules.isPackageAllowed(pkg);
            if (floatSuppressed) return;                 // 刚点完转换 ✗ 先别放回来
            if (!always && !inApp) {
                if (fbar != null) fbar.hideAll();
                Trace.add(Trace.now() + "  切到 [" + pkg + "]，收起悬浮球");
                return;
            }
            // 回到生效应用（或 always 模式）✗ 有内容就放回来 ✓
            if (floatOrig == null || floatOrig.length() == 0) return;
            if (fbar == null) fbar = new FloatBar(this, floatHost);
            fbar.tailEnabled = rules.resultTail;
            fbar.showBall();
            if (rules.resultTail) fbar.setTail(floatOut);
            else fbar.hideTail();
            if (fbar.isExpanded()) fbar.setPreview(floatOrig, floatOut);
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  切应用处理出错：" + t);
        }
    }

    /** 点【转换】✧ 把预览写进输入框（不发送 ✓）*/
    /** ★ 2026-10-31：这个包名是不是输入法 ✓ */
    boolean isImePackage(String pkg) {
        try {
            android.view.inputmethod.InputMethodManager imm =
                    (android.view.inputmethod.InputMethodManager) getSystemService(INPUT_METHOD_SERVICE);
            if (imm == null) return false;
            java.util.List<android.view.inputmethod.InputMethodInfo> list = imm.getEnabledInputMethodList();
            if (list == null) return false;
            for (int i = 0; i < list.size(); i++) {
                android.view.inputmethod.InputMethodInfo mi = list.get(i);
                if (mi != null && pkg.equals(mi.getPackageName())) return true;
            }
            if (pkg.equals(getPackageName())) return true;   // 自己的界面也不算切走
        } catch (Throwable t) { }
        return false;
    }

    /** ★ 2026-10-31：以前这里写死了 "3.4.1" ✗ 每次发版都忘了改 ✓
     *   现在直接读 APK 自己的 versionName ✗ 永远不会对不上 ✓ */
    String verName() {
        try {
            return getPackageManager().getPackageInfo(getPackageName(), 0).versionName;
        } catch (Throwable t) {
            return "?";
        }
    }

    void floatApply() {
        try {
            if (floatOut == null || floatOut.length() == 0) return;
            AccessibilityNodeInfo node = findInputNode();
            if (node == null) {
                Trace.add(Trace.now() + "  悬浮窗：找不到输入框 ✗");
                return;
            }
            android.os.Bundle b = new android.os.Bundle();
            b.putCharSequence(AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE, floatOut);
            b.putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, floatOut.length());
            b.putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, floatOut.length());
            boolean ok = node.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, b);
            if (ok) floatSuppressed = true;
            if (ok) {
                Stats stats = Stats.load(this);
                stats.bump(this);
                lastWritten = floatOut;
                lastWrittenAt = System.currentTimeMillis();
                settleUntil = System.currentTimeMillis() + SETTLE_MS;
                Trace.add(Trace.now() + "  悬浮窗写回 [" + Trace.brief(floatOrig)
                          + "] -> [" + Trace.brief(floatOut) + "]");
            } else {
                Trace.add(Trace.now() + "  悬浮窗写回失败 ✗");
            }
            if (fbar != null) fbar.hideAll();      // 写完把球收起来 ✧ 不挡着聊天 ✓
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  悬浮窗写回出错：" + t);
        }
    }

    /** 点【换一个】✧ 从【原文】重新转换 ✧ 语气和颜文字都重挑 ✓ */
    void floatReroll() {
        try {
            if (floatOrig == null || floatOrig.length() == 0) return;
            Rules r = rulesFor(curPkg);
            String out = floatOut;
            // ★★★ 2026-10-23：每次「换一个」都要换种子 ★★★
            //   之前直接 transform(floatOrig) ✗
            //   reseed() 用的就是「日期 ^ 句子哈希 ^ salt」✗
            //   salt 不动 → 每次都是同一个结果 ✓
            //   现在：每次 +1 ✗ 一直试到结果和上一次不一样 ✓
            // ★ 2026-10-23：必须用【递增】序号 ✗
            //   之前 bumpSalt() 完就 reset → 每次都是同一个种子 ✗
            //   开头 / 结尾写死不变 ✗ 只有颜文字因为冷却池在变 ✓
            rerollSeq = (rerollSeq + 1) % 1000;
            for (int i = 0; i < 8; i++) {
                Rules.setSalt(rerollSeq + i);
                out = r.transform(floatOrig);
                if (out == null || !out.equals(floatOut)) break;
            }
            Rules.resetSalt();
            floatOut = out;
            if (fbar != null) fbar.setPreview(floatOrig, floatOut);
            Trace.add(Trace.now() + "  换一个 -> [" + Trace.brief(floatOut) + "]");
        } catch (Throwable t) { }
    }


    /** ★ 「换一个」的序号 ✗ 每次加一 ✓ */
    int rerollSeq = 0;

    static final long SETTLE_MS = 1500L;

    // SharedPreferences has no modification time, so Rules.saveText bumps a revision counter
    // and the service reloads whenever it changes.
    //
    // Without this the service parsed the rules ONCE in onServiceConnected and kept them
    // forever: picking 傲娇 on the 风格 page updated the text box, the stored rules and the
    // highlighted chip, but the running service went on decorating with whatever was loaded
    // when it started - so switching to 傲娇 still produced 萌系's 喵~. Restarting the
    // accessibility service was the only way to make a style change take effect.
    private long rulesRev = -1L;

    private void reloadRulesIfChanged() {
        long rev = Rules.revision(this);
        if (rev == rulesRev) return;
        rulesRev = rev;
        rules = Rules.load(this);
        Trace.add(Trace.now() + "  规则已重新载入（设置改过了）");
    }

    @Override
    public void onServiceConnected() {
        // \u2605 \u628a Context \u4ea4\u7ed9\u5206\u7c7b\u5668 \u2727 \u6743\u91cd\u7b49\u7b2c\u4e00\u6b21\u771f\u7528\u7684\u65f6\u5019\u518d\u8bfb\uff08\u61d2\u52a0\u8f7d \u2713\uff09
        //   \u2014\u2014 \u2605 \u4e3a\u4ec0\u4e48\u4e0d\u5728\u8fd9\u91cc\u8bfb\uff1a\u8bfb 7MB \u4f1a\u5361\u4e3b\u7ebf\u7a0b \u2717
        //   \u2014\u2014 \u2605 \u4e3a\u4ec0\u4e48\u4e0d\u5f00\u7ebf\u7a0b\uff1a\u533f\u540d\u5185\u90e8\u7c7b\u4f1a\u8ba9 d8 \u62a5\u5185\u90e8\u9519\u8bef \u2717
        //      \uff082026-10-16 \u5b9e\u6d4b\uff1aMiaohuaService$1 \u7f16\u4e0d\u8fc7 \u2713\uff09
        try { TinyClf.setContext(this); } catch (Throwable t) { }

        // ★ 构建标记：一眼看出装的是哪一版 —— 版本号对测试包不变，光看版本号分不出来。
        Trace.add(Trace.now() + "  ===== 喵化助手 " + verName() + " 服务已启动");
        // ★ 记录同时落一份到应用私有目录 —— 用 adb 也能直接读，不用重启应用。
                Trace.init(getFilesDir());
        INSTANCE = this;

        // ★ 运行时把无障碍配置拉满（不含任何会改变系统操作方式的项）
        //
        // 起因：用户实测【TalkBack 能读到微信的输入框】，而本服务读不到。
        // 那说明节点是【存在的】，差别在服务自己的能力声明上。
        // XML 里已经写了 typeAllMask 和几个 flag，但有些 flag 光写在 XML 里
        // 不一定被采纳 —— TalkBack 是运行时调的 API。这里照做一遍。
        try {
            android.accessibilityservice.AccessibilityServiceInfo si = getServiceInfo();
            if (si != null) {
                si.eventTypes = android.view.accessibility.AccessibilityEvent.TYPES_ALL_MASK;
                si.flags |= android.accessibilityservice.AccessibilityServiceInfo.FLAG_INCLUDE_NOT_IMPORTANT_VIEWS;
                si.flags |= android.accessibilityservice.AccessibilityServiceInfo.FLAG_REPORT_VIEW_IDS;
                si.flags |= android.accessibilityservice.AccessibilityServiceInfo.FLAG_RETRIEVE_INTERACTIVE_WINDOWS;
                setServiceInfo(si);   // ★ 公开 API 是 setServiceInfo；setAccessibilityServiceInfo 是隐藏的
            } else {
                Trace.add(Trace.now() + "  getServiceInfo() 返回 null，没法运行时改配置");
            }
        } catch (Exception e) {
            Trace.add(Trace.now() + "  运行时改配置出错：" + e);
        }
        super.onServiceConnected();
        rulesRev = Rules.revision(this);
        rules = Rules.load(this);
    }

    @Override
    public void onAccessibilityEvent(AccessibilityEvent event) {
        // ★ 每一条提前返回都要写记录。
        //
        // 原来这里全是静默 return —— 于是"完全不生效"的时候记录是空的，
        // 看不出到底是没收到事件、事件类型不对、还是包名不在列表里。
        // 记录本来就是为了回答这个问题的，每个出口都得留话。
if (event == null) { Trace.add(Trace.now() + "  事件是 null"); return; }

        // ★ 这里【不能】记窗口清单 —— 那个窗口只开放几毫秒，
        //   而记清单要对每个窗口调一次 getRoot()，等于把机会浪费在日志上。记清单挪到失败之后。

        // ★★★ 必须放在【事件类型判断之前】★★★
        //
        //   任何事件带来的节点都缓存下来 —— 输入框"获得焦点"那一刻的节点
        //   往往最完整、最有效；等"停止打字 400ms 后"再去读，可能早就失效了。
        //   （原来这段写在类型判断之后，所以焦点事件的节点一个都存不到，
        //     等于白加了那个事件类型。）
        CharSequence pkg = null;
        try { pkg = event.getPackageName(); } catch (Exception e) { }
        try {
            AccessibilityNodeInfo es = event.getSource();
            // ★ 【绝不能缓存自己界面的节点】——
            //   用户在自己设置页点过输入框，那些节点会一直留在缓存里，
            //   切到微信之后微信的事件又不带节点，于是每次都"找到"自己的框，
            //   读到设置页里的数字，还写不进去。真机实测踩过这个坑。
            boolean cacheIt = true;
            try {
                String ep = es.getPackageName() == null ? "" : es.getPackageName().toString();
                if ("com.miaohua.helper".equals(ep) || ep.length() == 0) cacheIt = false;
            } catch (Exception e) { cacheIt = false; }
            if (cacheIt) {
                lastEventSource = es;
            } else {
                lastEventSource = null;   // ★ 自己界面的事件一来就把缓存清掉
            }
// ★ 微信的事件字段【无条件记一份】。
                //   原来只在"没有节点"的分支里记 —— 而真机上大多数事件是
                //   "有节点但节点是空壳"（类名 null），走的是另一个分支，
                //   所以这份最关键的字段记录【一次都没执行过】。
                //   判断条件写反了，坑了两轮。
                if (eventDumpLeft > 0 && pkg != null && "com.tencent.mm".contentEquals(pkg)) {
                    eventDumpLeft--;
                    StringBuilder fb = new StringBuilder();
                    fb.append("类型=").append(event.getEventType());
                    try { fb.append(" 事件类名=").append(event.getClassName()); } catch (Exception e) { }
                    try { fb.append(" 事件文本=").append(Trace.brief(String.valueOf(event.getText()))); } catch (Exception e) { }
                    try { fb.append(" 变化前=").append(Trace.brief(String.valueOf(event.getBeforeText()))); } catch (Exception e) { }
                    try { fb.append(" 起点=").append(event.getFromIndex()); } catch (Exception e) { }
                    try { fb.append(" 新增=").append(event.getAddedCount()); } catch (Exception e) { }
                    try { fb.append(" 删除=").append(event.getRemovedCount()); } catch (Exception e) { }
                    try { fb.append(" 窗口编号=").append(event.getWindowId()); } catch (Exception e) { }
                    try { fb.append(" 密码=").append(event.isPassword()); } catch (Exception e) { }
// ★★ 趁事件还"热"，立刻抓一次输入框。
                //   实测：窗口树只开放一瞬间，等防抖那 400ms 过去就关上了。
                if (pkg != null && "com.tencent.mm".contentEquals(pkg)) {
                    AccessibilityNodeInfo hot = scanForInput();
                    if (hot == null) hot = tryExotic(event);   // ★ 三个冷门 API 也试一遍
                    if (hot != null) {
                        hotInput = hot;
                        Trace.add(Trace.now() + "  ★★★ 事件瞬间抓到了输入框：" + safeClass(hot)
                                  + " 可见=" + (hot.isVisibleToUser() ? "是" : "否"));
                    } else {
                        if (eventDumpLeft > 0) Trace.add(Trace.now() + "  （事件瞬间也没抓到输入框）");
                    }
                }
                }
            if (es != null) {
                lastEventSource = es;
                if (eventDumpLeft > 0 && pkg != null && "com.tencent.mm".contentEquals(pkg)) {
                    eventDumpLeft--;
                    boolean ed = false;
                    try { ed = es.isEditable(); } catch (Exception e) { }
                }
            } else if (eventDumpLeft > 0 && pkg != null && "com.tencent.mm".contentEquals(pkg)) {
                // 微信：连 source 都是 null —— 把事件自带的其他字段记一份，
                // 至少能确认"文字变化事件里到底有没有带文本"。
                eventDumpLeft--;
                StringBuilder eb = new StringBuilder();
                eb.append("类型=").append(event.getEventType());
                try { eb.append(" 类名=").append(event.getClassName()); } catch (Exception e) { }
                try { eb.append(" 文本=").append(Trace.brief(String.valueOf(event.getText()))); } catch (Exception e) { }
                try { eb.append(" 变化前=").append(Trace.brief(String.valueOf(event.getBeforeText()))); } catch (Exception e) { }
                try { eb.append(" 起点=").append(event.getFromIndex()); } catch (Exception e) { }
                try { eb.append(" 新增=").append(event.getAddedCount()); } catch (Exception e) { }
                try { eb.append(" 删除=").append(event.getRemovedCount()); } catch (Exception e) { }
                try { eb.append(" 窗口=").append(event.getWindowId()); } catch (Exception e) { }
                try { eb.append(" 密码=").append(event.isPassword()); } catch (Exception e) { }
                Trace.add(Trace.now() + "  事件（无节点）：" + eb);
            }
        } catch (Exception e) { }

        reloadRulesIfChanged();
        if (rules == null) { Trace.add(Trace.now() + "  规则没加载出来，跳过"); return; }

        // ★ 非文本变化的事件【静默返回】。
        //   现在监听了 6 种事件类型，每一种都写一行的话记录会被刷爆，
        //   真正有用的那几行反而看不见了。
if (event.getEventType() != AccessibilityEvent.TYPE_VIEW_TEXT_CHANGED) {
            // ★ 窗口结构变了，而我们【正在等】那个输入框出现 —— 借这个机会提前再试一次。
            //
            //   真机实测：微信有时候会开出一个【可读的窗口】（类型=1、根节点可用），
            //   它一出现就能读到输入框；但那个窗口不是一直在，等它出现全靠运气。
            //   而它出现时必然伴随"窗口发生变化"的事件 —— 那就是信号。
            //   傻等 250ms 一轮太慢，抓住这个信号可以立刻试。
            int et = event.getEventType();
            // ★★★ 2026-10-31：切应用时决定球的去留 ★★★
            //   之前只有「生效应用里有文字变化」才会显示 / 收起 ✗
            //   切到桌面、切到别的应用不会产生文字事件 ✗ 球就一直挂在屏幕上 ✓
            if (et == AccessibilityEvent.TYPE_WINDOW_STATE_CHANGED) {
                String fp = event.getPackageName() == null ? null : event.getPackageName().toString();
                updateBallForForeground(fp);
            }
            if (lookupRetry > 0
                    && (et == AccessibilityEvent.TYPE_WINDOW_STATE_CHANGED
                        || et == AccessibilityEvent.TYPE_WINDOWS_CHANGED
                        || et == AccessibilityEvent.TYPE_WINDOW_CONTENT_CHANGED)) {
                if (pending != null) handler.removeCallbacks(pending);
                pending = new PendingRun(this);
                handler.postDelayed(pending, 40);
            }
            return;
        }
        // ★ 开了悬浮窗就不自动写回 ✧ 但事件还得收 ✧
        //   不然小球永远不会出现 ✗
        if (!rules.autoMode && !rules.floatBar) {
            Trace.add(Trace.now() + "  自动模式关着，跳过");
            return;
        }

        if (pkg == null) { Trace.add(Trace.now() + "  事件没带包名"); return; }
        // ★ 2026-10-18：这个包里关了自动就不转 ✗ 悬浮球还能用 ✓
        if (!rules.isPackageAuto(pkg.toString())) {
            Trace.add(Trace.now() + "  包名 [" + pkg + "] 关了自动转换，跳过");
            if (fbar != null) fbar.showBall();
            return;
        }
        if (!rules.isPackageAllowed(pkg.toString())) {
            // ★ 切到不生效的应用 ✧ 把小球收起来 ✓
            if (fbar != null) fbar.hideAll();
            Trace.add(Trace.now() + "  包名 [" + pkg + "] 不在生效列表里，跳过");
            return;
        }

        // still settling from our own write - reading now would see the pre-write text
        if (System.currentTimeMillis() < settleUntil) {
            // ★★★ 不能直接丢 ✧ 事后没有新事件就永远不会再处理了 ★★★
            //
            // 安卓这边是【事件驱动】的 ✧ 不像电脑版那样 100ms 轮询一次 ✓
            // 丢掉一个事件 ✧ 如果用户接下来不再敲字 ✧ 就没有下一个事件了 ✓
            //   —— 表现就是"刚转换完马上补一个字 ✧ 他不识别 ✧ 要重新输入过" ✓
            //
            // 改成：排一个"落定之后再来一次"✓
            long wait = settleUntil - System.currentTimeMillis() + 40;
            Trace.add(Trace.now() + "  写入还没落定（还有 " + wait
                      + "ms），排到落定之后再处理");
            if (pending != null) handler.removeCallbacks(pending);
            pending = new PendingRun(this);
            handler.postDelayed(pending, Math.max(60, wait));
            return;
        }

        if (pending != null) handler.removeCallbacks(pending);
        // ★ 趁事件还活着，把它带来的节点存下来 —— doTransform 会比这晚几百毫秒。
        try {
            AccessibilityNodeInfo es = event.getSource();
            // ★ 什么事件带来的节点都要，不只是文本变化。
            //   输入框【获得焦点】那一刻的节点往往是最完整、最有效的；
            //   等"停止打字 400ms 后"再去读，可能早就失效了。
            //   所以这里对所有事件一视同仁地缓存，能拿到就留下。
            if (es != null) {
                lastEventSource = es;
                Trace.add(Trace.now() + "  事件带来了节点：" + safeClass(es)
                          + (es.isEditable() ? "（可编辑）" : "（不可编辑）"));
            } else {
            }
        } catch (Exception e) { Trace.add(Trace.now() + "  取事件节点时出错：" + e); }
        pending = new PendingRun(this);
        // \u2605 2026-10-18\uff1a\u667a\u80fd\u5ef6\u8fdf \u2727 \u7528\u4e0a\u4e00\u6b21\u8bfb\u5230\u7684\u957f\u5ea6\u4f30 \u2713
        int delay = rules.effectiveDelay(lastSeenText);
        if (lastSeenText != null)
            Trace.add(Trace.now() + "  delay: " + delay + "ms\uFF08" + lastSeenText.length() + " \u5b57\uff09");
        handler.postDelayed(pending, delay);
    }

    /// 往下找第一个可编辑的后代。
    ///
    /// 为什么需要：findFocus(FOCUS_INPUT) 在某些应用里返回的是【容器】而不是输入框本身
    /// （Chrome 的地址栏就是这样）。只看那一个节点会误判成"不是输入框"从而完全放弃。
    /// 深度限制 12 层，避免异常界面把递归撑爆。
    /// getClassName 在陈旧节点上会抛异常或者返回 null —— 包一层，免得把流程打断。
    static String safeClass(AccessibilityNodeInfo n) {
        if (n == null) return "null";
        try { CharSequence c = n.getClassName(); return c == null ? "null" : c.toString(); } catch (Exception e) { return "?"; }
    }

    /// 节点的包名（陈旧节点上会抛异常，包一层）
    static String safePkg(AccessibilityNodeInfo n) {
        if (n == null) return "null";
        try { CharSequence p = n.getPackageName(); return p == null ? "null" : p.toString(); } catch (Exception e) { return "?"; }
    }

    /// ★ 这个节点能不能拿来读写？
    ///
    /// 真机踩到的大坑：抓到的是【喵化助手自己设置页】里那个输入框 ——
    /// 因为"事件带来的节点"缓存是全局的，用户在自己界面点过的框一直被留着，
    /// 切到微信之后微信的事件又不带节点，于是每次都"找到"那个自己的框，
    /// 读到 600（用户在设置里打过的数），还不可见、坐标都是反的。
    ///
    /// 所以凡是要用的节点，必须过这一关：
    ///   · 必须可编辑
    ///   · 必须对用户可见（不可见的框写不进去，会报"编辑器拒绝了"）
    ///   · 绝不能是喵化助手自己的（绝不操作自己）

    /// ★ 诊断：窗口清单【变化时】才记一行。
    ///
    /// 为什么需要：真机上发现一个很强的规律 ——
    ///   窗口数量=4 时微信能读（多出一个可读的微信窗口）
    ///   窗口数量=3 时读不到（剩下的微信窗口根节点全是 null）
    /// 而且这个状态【不是随机的】：09:13~09:23 一直是 4、从 09:24 起一直是 3。
    /// 说明是"某个操作把状态切换了，然后一直保持"。
    /// 所以只要把【每次变化】记下来，就能反推出是哪个操作触发的 ——
    /// 如果能找到一个稳定触发的操作，微信就能用了。
    ///
    /// 只在数量变化时记，所以不会刷屏。
    static int lastWinCount = -1;
    /// 窗口树的打印配额（只记前 6 次）
    static int winTreeLeft = 0;      // 平时 0（不打窗口树），排查问题时改成 6

    void logWindowsIfChanged() {
        try {
            java.util.List<AccessibilityWindowInfo> ws = getWindows();
            int cnt = ws == null ? -1 : ws.size();
            if (cnt == lastWinCount) return;
            lastWinCount = cnt;
            StringBuilder sb = new StringBuilder();
            sb.append("★★ 窗口清单变成 ").append(cnt).append(" 个：");
            for (int i = 0; ws != null && i < ws.size(); i++) {
                AccessibilityWindowInfo w = ws.get(i);
                if (w == null) continue;
                AccessibilityNodeInfo r = null;
                try { r = w.getRoot(); } catch (Exception e) { }
                sb.append("\n        [").append(i).append("]");
                try { sb.append(" 类型=").append(w.getType()); } catch (Exception e) { }
                sb.append(" 包=").append(safePkg(r));
                sb.append(" 根=").append(r == null ? "null" : "有");
                try { if (w.isActive()) sb.append(" 活动"); } catch (Exception e) { }
                try { if (w.isFocused()) sb.append(" 聚焦"); } catch (Exception e) { }
                try { if (w.isAccessibilityFocused()) sb.append(" 无障碍焦点"); } catch (Exception e) { }
                try { sb.append(" 层=").append(w.getLayer()); } catch (Exception e) { }
                try { sb.append(" 编号=").append(w.getId()); } catch (Exception e) { }
            }
            // ★ 顺便把【每个有根窗口的树】也打出来。
        //   目的：查"长按输入框弹出的那个菜单"到底算不算一个窗口、
        //   它的树里有没有「全选 / 粘贴」这些文字。
        //   只记前几次，免得刷屏。
        if (winTreeLeft > 0) {
            winTreeLeft--;
            for (int i = 0; ws != null && i < ws.size(); i++) {
                AccessibilityWindowInfo wt = ws.get(i);
                if (wt == null) continue;
                AccessibilityNodeInfo rt = null;
                try { rt = wt.getRoot(); } catch (Exception e) { }
                if (rt == null) { sb.append("\n        窗口 ").append(i).append(" 根是 null"); continue; }
                StringBuilder tb = new StringBuilder();
                dumpTree(rt, 0, tb, new int[] { 40 });
                sb.append("\n        窗口 ").append(i).append(" 的树：").append(tb);
            }
        }
        Trace.add(Trace.now() + "  " + sb);
        } catch (Exception e) { }
    }





    /// 服务实例。界面要调 setWeChatMode 就得通过它。
    static MiaohuaService INSTANCE = null;

    boolean wechatMode = false;
    android.view.View focusBox = null;
    android.view.WindowManager focusWm = null;

    int dp2(int v) {
        try {
            return (int) (v * getResources().getDisplayMetrics().density + 0.5f);
        } catch (Exception e) { return v * 2; }
    }

    /// 把绿框挪到指定矩形上。已经有一个就先撤掉再加，简单可靠。
    void showFocusBox(android.graphics.Rect r) {
        if (!wechatMode || r == null) return;
        if (r.width() <= 0 || r.height() <= 0) return;
        try {
            if (focusWm == null) focusWm = (android.view.WindowManager) getSystemService(WINDOW_SERVICE);
            if (focusWm == null) return;
            hideFocusBox();
            focusBox = new android.view.View(this);
            android.graphics.drawable.GradientDrawable gd =
                    new android.graphics.drawable.GradientDrawable();
            gd.setColor(0x3300E676);                    // 半透明绿底
            gd.setStroke(dp2(3), 0xFF00E676);           // 绿色描边
            gd.setCornerRadius(dp2(6));
            focusBox.setBackground(gd);
            android.view.WindowManager.LayoutParams lp = new android.view.WindowManager.LayoutParams(
                    r.width(), r.height(),
                    android.view.WindowManager.LayoutParams.TYPE_ACCESSIBILITY_OVERLAY,
                    android.view.WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE
                  | android.view.WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE
                  | android.view.WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN,
                    android.graphics.PixelFormat.TRANSLUCENT);
            lp.gravity = android.view.Gravity.TOP | android.view.Gravity.LEFT;
            lp.x = r.left;
            lp.y = r.top;
            focusWm.addView(focusBox, lp);
        } catch (Exception e) {
            focusBox = null;
        }
    }

    void hideFocusBox() {
        try {
            if (focusWm != null && focusBox != null) focusWm.removeView(focusBox);
        } catch (Exception e) { }
        focusBox = null;
    }

    /// 开关微信模式。由界面上那个开关调用，也由服务启动时按存档恢复。
    public void setWeChatMode(boolean on) {
        wechatMode = on;
        try {
            getSharedPreferences("miaohua_style", MODE_PRIVATE).edit()
                    .putBoolean("wechat_mode", on).commit();
        } catch (Exception e) { }
        try {
            android.accessibilityservice.AccessibilityServiceInfo si = getServiceInfo();
            if (si != null) {
                if (on) {
                    si.flags |= android.accessibilityservice.AccessibilityServiceInfo
                            .FLAG_REQUEST_TOUCH_EXPLORATION_MODE;
                } else {
                    si.flags &= ~android.accessibilityservice.AccessibilityServiceInfo
                            .FLAG_REQUEST_TOUCH_EXPLORATION_MODE;
                }
                setServiceInfo(si);
            }
        } catch (Exception e) { }
        if (!on) hideFocusBox();
        updateWeChatNotice();
        try {
            Trace.add(Trace.now() + (on ? "  微信模式：已开启（单击=选中，双击=点击）"
                                        : "  微信模式：已关闭"));
        } catch (Exception e) { }
    }

    /// 微信模式开着的时候挂一条常驻通知 —— 用户随时能从通知栏点回来关掉。
    void updateWeChatNotice() {
        try {
            android.app.NotificationManager nm =
                    (android.app.NotificationManager) getSystemService(NOTIFICATION_SERVICE);
            if (nm == null) return;
            if (!wechatMode) { nm.cancel(1002); return; }
            if (android.os.Build.VERSION.SDK_INT >= 26) {
                android.app.NotificationChannel ch = new android.app.NotificationChannel(
                        "miaohua_wechat", "微信模式",
                        android.app.NotificationManager.IMPORTANCE_LOW);
                ch.setShowBadge(false);
                nm.createNotificationChannel(ch);
            }
            android.content.Intent it = new android.content.Intent(this, MainActivity.class);
            it.setFlags(android.content.Intent.FLAG_ACTIVITY_NEW_TASK);
            android.app.PendingIntent pi = android.app.PendingIntent.getActivity(
                    this, 0, it,
                    android.app.PendingIntent.FLAG_UPDATE_CURRENT
                  | android.app.PendingIntent.FLAG_IMMUTABLE);
            android.app.Notification.Builder b;
            if (android.os.Build.VERSION.SDK_INT >= 26) {
                b = new android.app.Notification.Builder(this, "miaohua_wechat");
            } else {
                b = new android.app.Notification.Builder(this);
            }
            b.setSmallIcon(android.R.drawable.ic_menu_edit)
             .setContentTitle("微信模式已开启")
             .setContentText("单击=选中，双击=点击。点这里可以关掉。")
             .setContentIntent(pi)
             .setOngoing(true);
            nm.notify(1002, b.build());
        } catch (Exception e) { }
    }

    static boolean usableInput(AccessibilityNodeInfo n) {
        if (n == null) return false;
        try { if (!n.isEditable()) return false; } catch (Exception e) { return false; }

        // ★ 绝不操作自己界面里的框 —— 这条是硬的。
        String p = safePkg(n);
        if ("com.miaohua.helper".equals(p)) return false;
        if ("null".equals(p) || "?".equals(p)) return false;

        // ★ 要有【合法且不空】的屏幕坐标。
        //
        //   为什么不用 isVisibleToUser()：无障碍那个可见性判断经常不准，
        //   真机上把好好的输入框判成不可见，结果一个都找不到。
        //   而失效节点有个更硬的破绽 —— 坐标是反的（left > right），
        //   真机实测抓到过 位置=91,581-766,278 这种，一眼就是无效。
        try {
            android.graphics.Rect r = new android.graphics.Rect();
            n.getBoundsInScreen(r);
            // ★ 读到了坐标才判；读不到就【放行】。
            //   正在关闭的窗口读坐标会抛异常，原来直接 return false 把好节点拒了。
            if (r.right <= r.left) return false;
            if (r.bottom <= r.top) return false;
            if (r.width() < 8 || r.height() < 8) return false;
        } catch (Exception e) {
            // 读不到坐标 = 不判，放行
        }
        return true;
    }

    /// ★ 诊断：把找到的那个输入框彻底交代清楚。
    ///
    /// 真机实测踩到的坑：每次都"找到 EditText"了，但读出来的内容是 [600]，
    /// 而用户明明在聊天框里打「你好」—— 说明抓到了另一个输入框。
    /// 光知道"找到了 EditText"没用，得知道它【是哪个】：
    ///   哪个包、哪个 view id、在屏幕的什么位置、到根节点这一路是什么。
    /// bounds 最能说明问题：屏幕上方多半是搜索框，下方才是聊天框。
    static void describeNode(StringBuilder sb, AccessibilityNodeInfo n, String tag) {
        sb.append("\n        ").append(tag).append("：");
        if (n == null) { sb.append("null"); return; }
        sb.append(safeClass(n));
        sb.append(" 包=").append(safePkg(n));
        try { String id = n.getViewIdResourceName(); sb.append(" id=").append(id == null ? "null" : id); } catch (Exception e) { }
        try { sb.append(" 可编辑=").append(n.isEditable()); } catch (Exception e) { }
        try { sb.append(" 已聚焦=").append(n.isFocused()); } catch (Exception e) { }
        try { sb.append(" 可见=").append(n.isVisibleToUser()); } catch (Exception e) { }
        try {
            CharSequence t = n.getText();
            sb.append(" 文字=").append(t == null ? "null" : Trace.brief(t.toString()));
        } catch (Exception e) { }
        try {
            android.graphics.Rect r = new android.graphics.Rect();
            n.getBoundsInScreen(r);
            sb.append(" 位置=").append(r.left).append(",").append(r.top)
              .append("-").append(r.right).append(",").append(r.bottom);
        } catch (Exception e) { }
        // 往上走，看看它在谁下面
        try {
            AccessibilityNodeInfo cur = n;
            StringBuilder path = new StringBuilder();
            for (int d = 0; d < 6 && cur != null; d++) {
                path.append(" < ").append(safeClass(cur));
                AccessibilityNodeInfo p = cur.getParent();
                if (p == null) break;
                cur = p;
            }
            sb.append(" 往上=").append(path);
        } catch (Exception e) { }
    }

    /// ★ 诊断用：把一棵节点树简短打出来。
    ///
    /// 为什么需要：真机上"五条路全找不到输入框"的时候，光知道"找不到"没用 ——
    /// 得看看服务【实际能看到什么】。是整棵树都是空的？还是有一堆节点但没一个
    /// 声明 editable？还是根本看的是别的窗口？这三种情况的修法完全不同。
    ///
    /// 限制：深度 8 层、最多 40 行，免得把记录冲爆。
    static void dumpTree(AccessibilityNodeInfo n, int depth, StringBuilder sb, int[] budget) {
        if (n == null || depth > 30 || budget[0] <= 0) return;
        budget[0]--;
        StringBuilder pad = new StringBuilder();
        for (int i = 0; i < depth; i++) pad.append("· ");
        String extra = "";
        try { if (n.isEditable()) extra += " [可编辑]"; } catch (Exception e) { }
        try { if (n.isFocused()) extra += " [已聚焦]"; } catch (Exception e) { }
        try { if (n.isVisibleToUser()) extra += " [可见]"; } catch (Exception e) { }
        try {
            CharSequence t = n.getText();
            if (t != null && t.length() > 0) extra += " 文字=" + Trace.brief(t.toString());
        } catch (Exception e) { }
        sb.append("\n        ").append(pad).append(safeClass(n)).append(extra);
        int c = 0;
        try { c = n.getChildCount(); } catch (Exception e) { return; }
        for (int i = 0; i < c; i++) {
            AccessibilityNodeInfo ch = null;
            try { ch = n.getChild(i); } catch (Exception e) { }
            if (ch != null) dumpTree(ch, depth + 1, sb, budget);
        }
    }

    /// 找不到输入框时，把能看到的都写进记录。
    void dumpWhatWeSee() {
        try {
            AccessibilityNodeInfo root = getRootInActiveWindow();
            Trace.add(Trace.now() + "  活动窗口: 根节点=" + safeClass(root) + " 包名=" + safePkg(root));
            if (root != null) {
                StringBuilder sb = new StringBuilder();
                dumpTree(root, 0, sb, new int[] { 40 });
                Trace.add(Trace.now() + "  活动窗口的结构：" + sb);
            } else {
                Trace.add(Trace.now() + "  getRootInActiveWindow() 返回 null");
            }
        } catch (Exception e) {
            Trace.add(Trace.now() + "  打结构时出错：" + e);
        }
        // ★ 关键诊断：把【每个有根的窗口】的树都打出来。
        //   一直只打"活动窗口"那一个 —— 而它的根是 null，什么都看不到。
        //   真正要问的是：那些"根=有"的窗口【里面到底有什么】。
        //   如果它们也是空的，那就确认了"微信没有给出可用的树"。
        try {
            java.util.List<AccessibilityWindowInfo> ws0 = getWindows();
            for (int i = 0; ws0 != null && i < ws0.size(); i++) {
                AccessibilityWindowInfo w0 = ws0.get(i);
                if (w0 == null) continue;
                AccessibilityNodeInfo r0 = null;
                try { r0 = w0.getRoot(); } catch (Exception e) { }
                if (r0 == null) { Trace.add(Trace.now() + "  窗口 " + i + "：根是 null，没得打"); continue; }
                StringBuilder t0 = new StringBuilder();
                dumpTree(r0, 0, t0, new int[] { 30 });
                Trace.add(Trace.now() + "  窗口 " + i + " 的树：" + t0);
            }
        } catch (Exception e) {
            Trace.add(Trace.now() + "  打各窗口结构时出错：" + e);
        }

        try {
            java.util.List<AccessibilityWindowInfo> ws = getWindows();
            Trace.add(Trace.now() + "  窗口数量：" + (ws == null ? "null" : String.valueOf(ws.size())));
            for (int i = 0; ws != null && i < ws.size(); i++) {
                AccessibilityWindowInfo w = ws.get(i);
                if (w == null) { Trace.add(Trace.now() + "    窗口 " + i + " = null"); continue; }
                AccessibilityNodeInfo r = null;
                try { r = w.getRoot(); } catch (Exception e) { }
                String flags = "";
                try { if (w.isActive()) flags += " 活动"; } catch (Exception e) { }
                try { if (w.isFocused()) flags += " 聚焦"; } catch (Exception e) { }
                try { flags += " 类型=" + w.getType(); } catch (Exception e) { }
                Trace.add(Trace.now() + "    窗口 " + i + "：" + safeClass(r) + " 包名=" + safePkg(r) + flags);
            }
        } catch (Exception e) {
            Trace.add(Trace.now() + "  列窗口时出错：" + e);
        }
    }

    /// 找当前那个能用来越级读写的输入框。
    ///
    /// 为什么不能只靠 findFocus：真机（微信）上它会给一个失效节点，
    /// 类名 null、子树空，完全没法用。所以按可靠性从高到低试：
    ///   ① findFocus，拿到先 refresh()
    ///   ② 它是可编辑的 → 直接用
    ///   ③ 不可编辑 → 往下翻子树
    ///   ④ 还不行 → 在【活动窗口】整棵树里找可编辑的
    ///   ⑤ 再不行 → 在【所有窗口】里找
    ///   ⑥ 全都不行才放弃，并且把每一层的情况写进记录
    AccessibilityNodeInfo findInputNode() {

// ★★ 第一条路：事件那一瞬间抓到的节点 ★★
        //   实测窗口树只开放一瞬间，等到这里（防抖之后）通常已经关上了。
        if (hotInput != null) {
            // ★ 同样不 refresh —— 它就是靠"新鲜"才有用的。
            try {
                if (usableInput(hotInput)) {
                    Trace.add(Trace.now() + "  ★★★ 用事件瞬间抓到的输入框：" + safeClass(hotInput));
                    return hotInput;
                }
            } catch (Exception e) { }
            hotInput = null;   // 失效了就丢掉，别再试
        }
        // ★ 第一条路：事件自己带来的那个节点。
        //
        //   它不走"读整个窗口"（getRoot），所以能绕过微信那类窗口保护 ——
        //   微信实测：所有窗口的 getRoot() 都是 null，但事件照发，
        //   而事件里的 source 就是触发它的那个输入框。
        if (lastEventSource != null) {
            try { lastEventSource.refresh(); } catch (Exception e) { }
            try {
                if (usableInput(lastEventSource)) {
                    Trace.add(Trace.now() + "  ★ 用事件带来的节点，直接就是输入框：" + safeClass(lastEventSource));
                    return lastEventSource;
                }
            } catch (Exception e) { }
            AccessibilityNodeInfo d0 = findEditableDeep(lastEventSource, 0);
            if (d0 != null) {
                Trace.add(Trace.now() + "  ★ 用事件带来的节点，往下找到输入框：" + safeClass(d0));
                return d0;
            }
        }
        AccessibilityNodeInfo f = null;
        try { f = findFocus(AccessibilityNodeInfo.FOCUS_INPUT); } catch (Exception e) { }
        if (f != null) {
            // ★ 陈旧节点的标准补救：refresh() 之后再看。
            try { f.refresh(); } catch (Exception e) { }
            try { if (f.isEditable()) { Trace.add(Trace.now() + "  findFocus 直接给到输入框：" + safeClass(f)); return f; } } catch (Exception e) { }
            AccessibilityNodeInfo d = findEditableDeep(f, 0);
            if (d != null) {
                Trace.add(Trace.now() + "  焦点在容器上，往下找到输入框：" + safeClass(d));
                return d;
            }
            try {
                CharSequence ft = f.getText();
                if (ft != null && ft.length() > 0) {
                    Trace.add(Trace.now() + "  焦点节点不可编辑但能读到文字，直接用：" + Trace.brief(ft.toString()));
                    return f;
                }
            } catch (Exception e) { }
        } else {
            Trace.add(Trace.now() + "  findFocus 什么都没返回，换别的路试");
        }

        AccessibilityNodeInfo root = null;
        try { root = getRootInActiveWindow(); } catch (Exception e) { }
        if (root != null) {
            AccessibilityNodeInfo t = findEditableDeep(root, 0);
            if (t != null) {
                Trace.add(Trace.now() + "  在活动窗口里找到了输入框：" + safeClass(t));
                return t;
            }
            // ★ 换两条【不同的代码路径】试试：按文本搜、按 view id 搜。
            //   它们内部不一定要我把整棵树翻一遍，也许能绕开"子节点读不到"。
            try {
                java.util.List<AccessibilityNodeInfo> byText =
                        root.findAccessibilityNodeInfosByText("你好");
                if (byText != null && byText.size() > 0) {
                    Trace.add(Trace.now() + "  ★ 按文本搜到了 " + byText.size() + " 个节点，第一个是 "
                              + safeClass(byText.get(0)));
                } else {
                    Trace.add(Trace.now() + "  按文本搜：没搜到");
                }
            } catch (Exception e) { Trace.add(Trace.now() + "  按文本搜出错：" + e); }
        }

        try {
            java.util.List<AccessibilityWindowInfo> ws = getWindows();
            // ★ "活动 / 聚焦"只能当【排序偏好】，不能当硬条件。
            //
            //   真机实测（微信，2026-10-04）：活动且聚焦的那个窗口 getRoot() 返回 null，
            //   而同一个包名的另一个普通应用窗口根节点是好的 —— 输入框在那里。
            //   原来第一轮只挑活动/聚焦的，正好挑中那个 null 根的，只能跳过，
            //   于是记录里写着"所有窗口都试过了"，其实一个都没试到。
            //
            //   现在两轮：第一轮先看活动/聚焦的（正常情况下就是它），
            //   第二轮把所有窗口都过一遍，只要根节点不是 null 就往下找。
            AccessibilityNodeInfo best = null;
            String bestWhy = "";
            for (int pass = 0; pass < 2 && best == null; pass++) {
                for (int i = 0; ws != null && i < ws.size(); i++) {
                    AccessibilityWindowInfo w = ws.get(i);
                    if (w == null) continue;
                    boolean active = false, focused = false;
                    try { active = w.isActive(); } catch (Exception e) { }
                    try { focused = w.isFocused(); } catch (Exception e) { }
                    if (pass == 0 && !active && !focused) continue;
                    AccessibilityNodeInfo r2 = null;
                    // ★ 根节点为 null 时多试几次 —— 有些应用（微信实测）第一次拿是 null，
                    //   重试两三次能拿到。真是 null 的就跳过，别浪费一轮。
                    for (int att = 0; att < 3 && r2 == null; att++) {
                        try { r2 = w.getRoot(); } catch (Exception e) { }
                        if (r2 == null) { try { Thread.sleep(40); } catch (Exception e) { } }
                    }
                    if (r2 == null) { Trace.add(Trace.now() + "    窗口 " + i + " 的根节点重试 3 次仍是 null，跳过"); continue; }
                    if (r2 == null) continue;      // ★ 根节点为 null 就跳过，别浪费一轮
                    AccessibilityNodeInfo t = findEditableDeep(r2, 0);
                    if (t != null) {
                        best = t;
                        bestWhy = "第 " + i + " 个窗口" + (active ? "(活动)" : "") + (focused ? "(聚焦)" : "");
                        break;
                    }
                }
            }
            if (best != null) {
                Trace.add(Trace.now() + "  在" + bestWhy + "里找到了输入框：" + safeClass(best));
                return best;
            }
        } catch (Exception e) { }

        return null;
    }
    /// 往下找可编辑的后代。★ 优先要"已聚焦"的那个 ★
    ///
    /// 为什么：一个界面里可能有好几个输入框（聊天框、搜索框、留言框……）。
    /// 只拿"第一个可编辑的"会抓错 —— 真机实测就抓到一个含 [600] 的框，
    /// 而用户明明在聊天框里打字。所以先找"可编辑且已聚焦"的，
    /// 实在没有才退回到"任意可编辑的"。
    ///
    /// 深度限制 30 层，避免异常界面把递归撑爆。
    AccessibilityNodeInfo findEditableDeep(AccessibilityNodeInfo n, int depth) {
        AccessibilityNodeInfo focused = findFocusedEditable(n, 0);
        if (focused != null) return focused;
        return findAnyEditable(n, 0);
    }

    /// 找"可编辑【且】已聚焦"的后代 —— 这才是用户正在打字的那个框。
    static AccessibilityNodeInfo findFocusedEditable(AccessibilityNodeInfo n, int depth) {
        if (n == null || depth > 30) return null;
        try {
            // ★ 不调 refresh()。实测：refresh() 会重新去取节点信息，
            //   而微信那个窗口是【正在关闭】的，重取必然失败 —— 于是本来
            //   好好的节点被自己弄失效了。刚拿到的节点本来就是新的，不需要刷。
            if (n.isEditable() && n.isFocused() && usableInput(n)) return n;
        } catch (Exception e) { }
        int c = 0;
        try { c = n.getChildCount(); } catch (Exception e) { return null; }
        for (int i = 0; i < c; i++) {
            AccessibilityNodeInfo ch = null;
            try { ch = n.getChild(i); } catch (Exception e) { }
            if (ch == null) continue;
            AccessibilityNodeInfo r = findFocusedEditable(ch, depth + 1);
            if (r != null) return r;
        }
        return null;
    }

    /// 退而求其次：任意可编辑的后代。
    static AccessibilityNodeInfo findAnyEditable(AccessibilityNodeInfo n, int depth) {
        if (n == null || depth > 30) return null;
        try {
            // ★ 同上，不调 refresh()。
            if (n.isEditable() && usableInput(n)) return n;
        } catch (Exception e) { }
        int c = 0;
        try { c = n.getChildCount(); } catch (Exception e) { return null; }
        for (int i = 0; i < c; i++) {
            AccessibilityNodeInfo ch = null;
            try { ch = n.getChild(i); } catch (Exception e) { }
            if (ch == null) continue;
            AccessibilityNodeInfo r = findAnyEditable(ch, depth + 1);
            if (r != null) return r;
        }
        return null;
    }
    // ★★★ 读聊天上下文（对方最近说的几条）★★★
    //
    // 思路：输入框上面的都是聊天内容 ✧ 按 y 坐标从下往上取几条 ✓
    //   —— ✧ 不用知道是 QQ 还是微信 ✧ 也不用知道它的控件叫什么 ✓
    //
    // 去重：容器节点和它的子节点常常是同一段文字 ✧
    //   所以排序后把"和旁边重复"或"包住旁边那句"的去掉 ✓
    static void collectTexts(AccessibilityNodeInfo n, int topY,
                             java.util.ArrayList<Object[]> out, int depth) {
        if (n == null || depth > 40 || out.size() > 500) return;
        try {
            CharSequence t = n.getText();
            if (t != null) {
                String s = t.toString().trim();
                if (s.length() > 0 && s.length() <= 120) {
                    android.graphics.Rect r = new android.graphics.Rect();
                    n.getBoundsInScreen(r);
                    // ★ 只要【完全在输入框上方】的 ✧ 而且得有实际高度 ✓
                    if (r.height() > 0 && r.bottom <= topY && r.top >= 0) {
                        out.add(new Object[] { Integer.valueOf(r.top), s });
                    }
                }
            }
        } catch (Throwable t) { }
        int c = 0;
        try { c = n.getChildCount(); } catch (Throwable t) { return; }
        for (int i = 0; i < c; i++) {
            AccessibilityNodeInfo ch = null;
            try { ch = n.getChild(i); } catch (Throwable t) { }
            if (ch != null) collectTexts(ch, topY, out, depth + 1);
        }
    }

    /** 读一条上下文 ✧ 读不到就返回 ""（调用方按"没有上下文"处理 ✓） */
    String readChatContext(AccessibilityNodeInfo inputNode) {
        try {
            int topY = 0;
            try {
                android.graphics.Rect r = new android.graphics.Rect();
                inputNode.getBoundsInScreen(r);
                topY = r.top;
            } catch (Throwable t) { return ""; }
            if (topY <= 0) return "";

            AccessibilityNodeInfo root = null;
            try { root = getRootInActiveWindow(); } catch (Throwable t) { }
            if (root == null) return "";

            java.util.ArrayList<Object[]> items = new java.util.ArrayList<Object[]>();
            collectTexts(root, topY, items, 0);
            if (items.isEmpty()) return "";

            // 按 y 从下往上排 ✧ 越靠下越新 ✓
            //   ★ 这里刻意不用 Collections.sort + 匿名 Comparator ✗
            //     泛型匿名内部类会让 d8 8.2.2 报内部错误 ✗
            //     （2026-10-16 实测：MiaohuaService$1 编不过 ✓）
            //   —— 反正最多就几百条 ✧ 手写冒泡排序就行 ✓
            for (int a = 0; a < items.size(); a++) {
                for (int b = a + 1; b < items.size(); b++) {
                    int ya = ((Integer) items.get(a)[0]).intValue();
                    int yb = ((Integer) items.get(b)[0]).intValue();
                    if (yb > ya) {
                        Object[] tmp = items.get(a);
                        items.set(a, items.get(b));
                        items.set(b, tmp);
                    }
                }
            }

            java.util.ArrayList<String> picked = new java.util.ArrayList<String>();
            for (int i = 0; i < items.size() && picked.size() < 8; i++) {
                String s = (String) items.get(i)[1];
                boolean dup = false;
                for (int k = 0; k < picked.size(); k++) {
                    String p = picked.get(k);
                    // 完全一样 ✧ 或者一个是另一个的一部分 ✧ 都算重复 ✓
                    if (p.equals(s) || p.indexOf(s) >= 0 || s.indexOf(p) >= 0) { dup = true; break; }
                }
                if (!dup) picked.add(s);
            }
            if (picked.isEmpty()) return "";

            // 反过来变成【从旧到新】✧ 读起来顺 ✗ 也符合"最近的在最后" ✓
            StringBuilder sb = new StringBuilder();
            for (int i = picked.size() - 1; i >= 0; i--) {
                if (sb.length() > 0) sb.append("\n");
                sb.append(picked.get(i));
                if (sb.length() > 600) break;
            }
            return sb.toString();
        } catch (Throwable t) {
            return "";
        }
    }

    // ★★★ 输入框里像不像正在打拼音 ★★★
    //
    // 为什么只能猜：
    //   Windows 有 ImmGetCompositionString 可以直接问“组合串是什么”✓
    //   安卓的 AccessibilityNodeInfo 里【没有】这个字段 ✗
    //   —— ★ 所以只能靠下面两个信号 ✓
    //
    // 信号①：有选区 ✧ 拼音组合区很多输入法会高亮 ✓
    //           而且用户在选东西时也不该动 ✓
    // 信号②：结尾像拼音 ✧ 但整句里得有中文 ✓
    //           （没中文 = 英文模式 ✧ 不能这么判 ✗）
    static boolean looksLikeComposing(AccessibilityNodeInfo node, String s) {
        try {
            int a = node.getTextSelectionStart();
            int b = node.getTextSelectionEnd();
            if (a >= 0 && b >= 0 && a != b) return true;
        } catch (Throwable t) { }

        if (s == null || s.length() == 0) return false;
        int i = s.length();
        boolean hasLetter = false;
        while (i > 0) {
            char c = s.charAt(i - 1);
            if (c == '\'' || c == ' ' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9')) {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) hasLetter = true;
                i--;
            } else break;
        }
        if (!hasLetter) return false;
        boolean hasCjk = false;
        for (int k = 0; k < s.length() && !hasCjk; k++) {
            char c = s.charAt(k);
            if (c >= '\u4e00' && c <= '\u9fff') hasCjk = true;
        }
        return hasCjk;
    }

    void doTransform() {
        AccessibilityNodeInfo node = null;
        try {
            // ★ 找输入框不能只靠 findFocus。
            //
            //   真机上（微信就是）findFocus 会返回一个已经【失效】的节点 ——
            //   getClassName() 直接是 null，往下翻子树也全是空的。于是记录里反复写
            //   "聚焦的不是可编辑框，往下也没找到（类名 null）"，用户看到的就是
            //   "微信里完全用不了"，而且从记录里根本看不出是节点失效。
            //
            //   现在按可靠性从高到低试几条路，哪条成了都行。
            node = findInputNode();
            if (node == null) {
if (lookupRetry == 0) logWindowsIfChanged();   // ★ 失败时才记窗口清单，别跟抓取抢那几毫秒
if (lookupRetry == 0 || lookupRetry >= 7) Trace.add(Trace.now() + "  没找到可编辑的输入框（第 " + (lookupRetry + 1) + " 次）");
            if (lookupRetry == 0) dumpWhatWeSee();   // 只在第一次失败时打结构，免得刷屏
            // ★ 隔一会儿再试几次。
            //   真机上遇到过：某个窗口的 getRoot() 在"刚停下来"那一刻是 null，
            //   ★ 微信实测：它【有时候】会开出第二个可读窗口（类型=1、根节点可用），
            //     成功那次就是靠它找到 EditText 的；失败时那个窗口还没出现。
            //     所以不是"屏蔽"，是"来得慢"—— 多等一会儿就有。
            //     8 次 x 250ms 约 2 秒，窗口一出现就立刻转换，不会白等满。
            //   稍等一下就有了。直接放弃等于白白丢掉这种情况。
            //   每次重试都写记录，所以事后能看出是"一直读不到"还是"偶尔读不到"。
            if (lookupRetry < 8) {
                lookupRetry++;
                if (lookupRetry == 1 || lookupRetry >= 8) Trace.add(Trace.now() + "  第 " + lookupRetry + " 次重试（最多 8 次）");
                if (pending != null) handler.removeCallbacks(pending);
                pending = new PendingRun(this);
                handler.postDelayed(pending, 250);
            } else {
                Trace.add(Trace.now() + "  重试 8 次仍然不行，这次放弃（约 2 秒）");
                lookupRetry = 0;
            }
            return;
            }
            lookupRetry = 0;   // 这次找到了，计数清零
            if (!usableInput(node)) { Trace.add(Trace.now() + "  找到的节点不可用（不可见/是自己的/包名空），放弃这次"); return; }
            CharSequence cur = node.getText();
            if (cur == null) return;
            String src = cur.toString();
            if (src.length() == 0) return;

            // \u2605\u2605\u2605 \u6b63\u5728\u6253\u62fc\u97f3\uff08\u7ec4\u5408\u4e2d\uff09\u2727 \u4e00\u4e2a\u5b57\u7b26\u90fd\u4e0d\u8981\u52a8 \u2605\u2605\u2605
            //   \u4e0d\u7136 ACTION_SET_TEXT \u4f1a\u6574\u6bb5\u66ff\u6362 \u2727 \u7ec4\u5408\u533a\u76f4\u63a5\u6ca1\u4e86 \u2717
            //   \uff08\u7528\u6237 2026-10-17 \u62a5\u7684\uff1a\u6253 ni'hao \u6253\u4e00\u534a\u5c31\u88ab\u51b2\u6389 \u2713\uff09
            if (looksLikeComposing(node, src)) {
                Trace.add(Trace.now() + "  \u6846\u91cc\u50cf\u5728\u6253\u62fc\u97f3\uff0c\u4e0d\u52a8\uff08"
                          + Trace.brief(src) + "\uff09");
                return;
            }

            // exactly what we wrote a moment ago -> it is our own edit echoing back
            if (lastWritten != null && src.equals(lastWritten)
                    && (System.currentTimeMillis() - lastWrittenAt) < 5000L) {
                Trace.add(Trace.now() + "  忽略自己写的内容 [" + Trace.brief(src) + "]");
                return;
            }

            // ★ 读一眼"对方最近说了什么" ✗ 只在真用得上时才读 ✓
            //   （规则里有「上文…」✗ 或者开了小模型语气 ✓）
            try {
                if (rules.wantsContext()) {
                    String cx = readChatContext(node);
                    Rules.chatCtx = cx;
                    if (cx.length() > 0) Trace.add(Trace.now() + "  读到上文 " + cx.length() + " 字");
                } else {
                    Rules.chatCtx = "";
                }
            } catch (Throwable t) { Rules.chatCtx = ""; }

            // ★★★ 悬浮窗模式 ✧ 不自动写回 ✧ 只更新小球和预览 ★★★
            if (rules.floatBar) {
                floatOrig = src;
                floatOut = rulesFor(curPkg).transform(src);
                if (fbar == null) fbar = new FloatBar(this, floatHost);
                fbar.tailEnabled = rules.resultTail;
                floatSuppressed = false;                 // 又有新文字了 ✗ 解除抑制
                if (src.length() > 0) {
                    fbar.showBall();
                    if (rules.resultTail) fbar.setTail(floatOut); else fbar.hideTail();
                    if (fbar.isExpanded()) fbar.setPreview(floatOrig, floatOut);
                } else {
                    fbar.hideAll();
                }
                Trace.add(Trace.now() + "  悬浮窗预览 [" + Trace.brief(floatOut) + "]");
                return;
            }

            String out = rulesFor(curPkg).transform(src);
            if (out.equals(src)) {
// ★ 诊断：把这个框的完整身份打出来 —— 每次都读到 [600] 说明抓错了框，
//   光看"找到了 EditText"看不出来，得看它在哪、叫什么 id、在屏幕什么位置。
                if (nodeDescLeft > 0) {
                    nodeDescLeft--;
                    StringBuilder db = new StringBuilder();
                    describeNode(db, node, "读到的框");
                    Trace.add(Trace.now() + db);
                }
                Trace.add(Trace.now() + "  读到 [" + Trace.brief(src) + "] 结果相同，不写");
                return;
            }

            // Re-read before writing. If the box moved since the read above - the user typed,
            // or a previous write has only just landed - then writing now would put the new
            // text beside the old one instead of over it, which is the duplication:
            //     你好喵~ฅ^•ﻌ•^ฅ你好喵~(๑•̀ㅂ•́)و✧
            // Cheap: a node read, no extra keystrokes. A stale run gives up and changes
            // nothing, so the worst case is a transform that simply did not happen.
            CharSequence again = node.getText();
            String now = again == null ? null : again.toString();
            if (now == null || !now.equals(src)) {
                Trace.add(Trace.now() + "  跳过 —— 写入前内容已经变了：读到 ["
                          + Trace.brief(src) + "]，现在是 [" + Trace.brief(now) + "]");
                return;
            }

            Bundle b = new Bundle();
            b.putCharSequence(AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE, out);
            b.putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, out.length());
            b.putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, out.length());

            boolean ok = node.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, b);
            if (ok) {
                // ⑥ 统计：只有【写成功】才计数。
                //
                // 不能放在"决定要转化"的地方 —— 那样写失败也会多算一次，
                // 用户看到的是"次数在涨但文字没变"。
                //
                // 和电脑版有一处差别要记着：电脑版是同步写入，写完再计数，
                // 界限很清楚；安卓的 ACTION_SET_TEXT 是异步落地的，
                // 没有可靠的写入完成回调，所以只能"提交成功之后"就计数 ——
                // 理论上存在提交成功但编辑器没落地的极小概率，那时候会多算一次。
                // 宁可这样，也不要放在提交之前（那样失败也会算）。
                Stats stats = Stats.load(this);
                stats.bump(this);

                lastWritten = out;
                lastSeenText = src;   // ★ 2026-10-18：智能延迟用 ✓
                // ★ 2026-10-18：记历史 + 猫猫冷却 ✓
                Stats.addRecent(src, out);
                if (Stats.streak == 18) {
                    Trace.add(Trace.now() + "  猫猫有点累了……（连续喵化 18 次）");
                }
                lastWrittenAt = System.currentTimeMillis();
                // ★ 2026-10-18：记下可撤销的一步 ✓
                undoFrom = src;
                undoTo = out;
                settleUntil = System.currentTimeMillis() + SETTLE_MS;
                Trace.add(Trace.now() + "  写回 [" + Trace.brief(src) + "] -> ["
                          + Trace.brief(out) + "]");
                // Deliberately NO immediate read-back.
                //
                // ACTION_SET_TEXT is applied by the editor asynchronously, so a getText()
                // right here returns the text as it was BEFORE the write, and logged a
                // frightening "!! 写入后实际是 [你好]" after every single successful write.
                // It never retried, so it was pure noise - noise that looked like a real
                // failure and sent me chasing the wrong thing twice. The next event's own
                // read is the check that matters, and that one is accurate.
            } else {
                Trace.add(Trace.now() + "  !! 写回失败（编辑器拒绝了）");
            }
        } catch (Exception e) {
            Trace.add(Trace.now() + "  !! 异常 " + e);
        } finally {
            if (node != null) {
                try { node.recycle(); } catch (Exception e) { }
            }
        }
    }

    @Override
    public void onInterrupt() { }

    @Override
    public boolean onUnbind(Intent intent) {
        handler.removeCallbacksAndMessages(null);
        pending = null;
        return super.onUnbind(intent);
    }

    /** ★★★ 2026-10-18：撤销用的两个值 ★★★
     *   undoFrom = 转换前的原文 ✧ undoTo = 写进去的结果
     *   —— 只有【框里还是 undoTo】时才还原 ✗ 用户又打字了就不碰 ✓ */
    static String undoFrom, undoTo;

    /** \u2605 2026-10-18\uff1a\u4e0a\u4e00\u6b21\u8bfb\u5230\u7684\u6587\u672c \u2727 \u667a\u80fd\u5ef6\u8fdf\u7528 \u2713 */
    String lastSeenText;

    /** \u2605 2026-10-18\uff1a\u8bed\u6c14\u540d \u2192 assets \u6587\u4ef6\u540d \u2727 \u548c MainActivity \u91cc\u90a3\u5f20\u8868\u4e00\u81f4 \u2713 */
    static final String[][] TONE_ASSETS = {
        { "\u50B2\u5A07",   "tsundere"  },
        { "\u9ECF\u4EBA",   "clingy"    },
        { "\u6175\u61D2",   "lazy"      },
        { "\u59D4\u5C48",   "aggrieved" },
        { "\u5143\u6C14",   "energetic" },
        { "\u5E94\u6FC0",   "stress"    },
        { "\u6E29\u67D4",   "gentle"    },
        { "\u9AD8\u51B7",   "cold"      },
        { "\u4E2D\u4E8C",   "chuuni"    },
        { "\u5929\u7136\u5446", "airhead"   },
        { "\u6BD2\u820C",   "sharp"     },
        { "\u75C5\u5A07",   "yandere"   },
        { "\u70ED\u8840",   "hyped"     },
        { "\u8179\u9ED1",   "scheming"  },
    };

    static String assetOf(String name) {
        for (int i = 0; i < TONE_ASSETS.length; i++)
            if (TONE_ASSETS[i][0].equals(name)) return TONE_ASSETS[i][1];
        return null;
    }

    /** \u2605 \u6309\u5305\u540D\u53D6\u4e00\u4efd\u89c4\u5219 \u2727 \u6ca1\u547d\u4e2d\u5c31\u7528\u5168\u5c40\u90a3\u4efd \u2713 */
    /** \u2605 \u5f53\u524d\u5305\u540D \u2727 \u6bcf\u6b21\u4e8b\u4ef6\u90fd\u66f4\u65b0 \u2713 */
    String curPkg;
    String styledPkg;
    Rules styledRules;

    Rules rulesFor(String pkg) {
        try {
            if (rules == null) return rules;
            if (pkg == null || rules.pkgStyles.isEmpty()) return rules;
            String style = rules.pkgStyles.get(pkg);
            if (style == null) return rules;
            if (styledRules != null && styledPkg != null && styledPkg.equals(pkg + "|" + style)) return styledRules;

            String text = rules.sourceText;
            if (text == null || text.length() == 0) return rules;

            Rules nr;
            if (style.equals("\u4E0D\u52A0") || style.equals("\u65E0") || style.equals("none")) {
                // \u2605 \u201c\u4e0d\u52a0\u201d = \u8fd9\u4e2a\u5305\u91cc\u5b8c\u5168\u4e0d\u55b5\u5316 \u2717 \u628a\u540e\u7F00\u548C\u989C\u6587\u5B57\u6E05\u7A7A \u2713
                String cleared = Rules.setKey(text, "\u540E\u7F00", "suffix", "");
                cleared = Rules.setKeys(cleared, "\u989C\u6587\u5B57", "kaomoji",
                                        new java.util.ArrayList<String>());
                nr = Rules.parse(cleared);
            } else {
                String asset = assetOf(style);
                if (asset == null) return rules;
                String st = null;
                try {
                    java.io.InputStream in = getAssets().open(asset + ".txt");
                    java.io.ByteArrayOutputStream bos = new java.io.ByteArrayOutputStream();
                    byte[] buf = new byte[8192];
                    int k;
                    while ((k = in.read(buf)) > 0) bos.write(buf, 0, k);
                    in.close();
                    st = new String(bos.toByteArray(), "UTF-8");
                } catch (Throwable e) { st = null; }
                if (st == null) return rules;
                String styled = Rules.applyStyleFromAsset(text, st);
                if (styled == null) return rules;
                nr = Rules.parse(styled);
            }
            styledPkg = pkg + "|" + style;
            styledRules = nr;
            Trace.add(Trace.now() + "  \u6309\u5305\u540D\u5207\u98CE\u683C\uff1a" + pkg + " -> " + style);
            return nr;
        } catch (Throwable e) {
            Trace.add(Trace.now() + "  \u5305\u540D\u98CE\u683C\u51FA\u9519\uff1a" + e);
            return rules;
        }
    }

    /** ★ 撤销上一条 ✧ 找当前聚焦的输入框 ✗ 写回原文 ✓ */
    void undoLast() {
        if (undoFrom == null || undoTo == null) {
            Trace.add(Trace.now() + "  undo: 还没有可撤销的");
            return;
        }
        try {
            AccessibilityNodeInfo root = getRootInActiveWindow();
            if (root == null) { Trace.add(Trace.now() + "  undo: 拿不到窗口"); return; }

            AccessibilityNodeInfo focus = root.findFocus(AccessibilityNodeInfo.FOCUS_INPUT);
            if (focus == null) {
                Trace.add(Trace.now() + "  undo: 找不到输入框");
                return;
            }

            CharSequence cur = focus.getText();
            String now = cur == null ? null : cur.toString();
            if (now == null || !now.equals(undoTo)) {
                Trace.add(Trace.now() + "  undo: 内容变了（现在是 ["
                          + Trace.brief(now) + "]），不撤销");
                return;
            }

            Bundle b = new Bundle();
            b.putCharSequence(AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE, undoFrom);
            b.putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, undoFrom.length());
            b.putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, undoFrom.length());

            boolean ok = focus.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, b);
            if (ok) {
                Trace.add(Trace.now() + "  undo: 已还原成 [" + Trace.brief(undoFrom) + "]");
                // ★ 撤过一次就清掉 ✧ 免得连按两次撤到更早的内容 ✓
                undoFrom = null;
                undoTo = null;
                lastWritten = null;
                settleUntil = 0;
            } else {
                Trace.add(Trace.now() + "  undo: 写回失败（编辑器拒绝了）");
            }
        } catch (Throwable t) {
            Trace.add(Trace.now() + "  undo: 异常 " + t);
        }
    }

    static class PendingRun implements Runnable {
        private final MiaohuaService svc;
        PendingRun(MiaohuaService s) { svc = s; }
        @Override
        public void run() {
            svc.pending = null;
            svc.doTransform();
        }
    }
}
