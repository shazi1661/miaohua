using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Miaohua
{
    // ---------------------------------------------------------------- config

    /// ★★★ 读聊天上下文（UIA）★★★
    ///
    /// 为什么能读（实测过的结构 ✓）：
    ///   微信  List[mmui::RecyclerListView Name=消息]
    ///           └ ListItem[mmui::ChatTextItemView]  Name = 正文
    ///   QQ    Pane Name=[消息列表]
    ///           └ Group<message>
    ///               ├ Group<avatar-span>          → 昵称
    ///               └ Group<msg-content-container> → 正文
    ///
    /// 坑一：QQ 是 Chromium ✧ 它的无障碍树是【异步建】的 ✓
    ///   第一次问只回 9 个空壳 ✧ 要先注册一个 UIA 事件监听催它 ✓
    ///
    /// 坑二：QQ 左侧导航栏那个「消息」按钮也叫“消息” ✧ 只有 36x36 ✗
    ///   真正的消息区是 862x666 ✧ 所以按【面积】挑最大的 ✓
    ///
    /// 坑三：QQ 的昵称 / 等级 / 头衔都在同一棵子树里 ✧
    ///   直接把文字吐出来会混进 "LV" "管理员" "false" ✗
    ///   所以只取 msg-content-container 里的 ✓
    ///
    /// 坑四：怎么知道是谁说的 ✧ 用【左右位置】判 ✓
    ///   右边 = 自己 ✧ 左边 = 对方 ✓（微信不暴露发言人 ✧ 只能留空 ✓）
    ///
    /// 读不到就返回 "" ✧ 调用方静默退回原行为 ✓
    static class ChatCtx
    {
        class Nd
        {
            public int Parent = -1;
            public int Depth;
            public string Type = "";
            public string Name = "";
            public string Value = "";
            public string Cls = "";
            public double X, W;
            public bool HasRect;
        }

        static readonly List<Nd> flat = new List<Nd>();
        const int MAX_NODES = 20000;

        static void Walk(System.Windows.Automation.AutomationElement el, int depth, int parent)
        {
            if (el == null || depth > 26 || flat.Count >= MAX_NODES) return;
            System.Windows.Automation.AutomationElementCollection kids;
            try
            {
                kids = el.FindAll(System.Windows.Automation.TreeScope.Children,
                                  System.Windows.Automation.Condition.TrueCondition);
            }
            catch { return; }
            foreach (System.Windows.Automation.AutomationElement c in kids)
            {
                Nd d = new Nd();
                d.Parent = parent; d.Depth = depth;
                try { d.Type = c.Current.ControlType.ProgrammaticName.Replace("ControlType.", ""); } catch { }
                try { d.Name = c.Current.Name ?? ""; } catch { }
                try { d.Cls = c.Current.ClassName ?? ""; } catch { }
                try
                {
                    object v = c.GetCurrentPattern(System.Windows.Automation.ValuePattern.Pattern);
                    if (v != null) d.Value = ((System.Windows.Automation.ValuePattern)v).Current.Value ?? "";
                }
                catch { }
                try
                {
                    System.Windows.Rect r = c.Current.BoundingRectangle;
                    if (!r.IsEmpty && r.Width > 0) { d.X = r.X; d.W = r.Width; d.HasRect = true; }
                }
                catch { }
                flat.Add(d);
                Walk(c, depth + 1, flat.Count - 1);   // 刚加进去的那个就是它的父亲 ✓
            }
        }

        static bool Under(int idx, int anc)
        {
            int p = flat[idx].Parent;
            while (p >= 0) { if (p == anc) return true; p = flat[p].Parent; }
            return false;
        }

        static List<int> Kids(int root)
        {
            List<int> r = new List<int>();
            for (int i = root + 1; i < flat.Count; i++)
            {
                if (flat[i].Depth <= flat[root].Depth) break;
                r.Add(i);
            }
            return r;
        }

        static string BodyOf(int root, string cls)
        {
            List<int> d = Kids(root);
            int t = -1;
            foreach (int i in d)
                if (flat[i].Cls.StartsWith(cls, StringComparison.OrdinalIgnoreCase)) { t = i; break; }
            if (t < 0) return "";
            StringBuilder sb = new StringBuilder();
            foreach (int i in Kids(t))
            {
                Nd x = flat[i];
                if (x.Type == "Image" || x.Type == "Button") continue;
                string s = x.Name.Length > 0 ? x.Name : x.Value;
                if (s.Length == 0 || s == "true" || s == "false") continue;
                sb.Append(s);
            }
            return sb.ToString().Trim();
        }

        static int MsgList()
        {
            int best = -1; double bestArea = 0;
            for (int i = 0; i < flat.Count; i++)
            {
                Nd x = flat[i];
                if (x.Name != "消息列表" && x.Name != "消息") continue;
                if (x.Type == "Button" || x.Type == "Image") continue;
                double a = x.W * (x.HasRect ? 1 : 0);
                if (a > bestArea) { bestArea = a; best = i; }
            }
            return best;
        }

        static string _cache = "";
        static long _cacheAt = 0;
        static IntPtr _cacheHwnd = IntPtr.Zero;

        /// ★ 1.5 秒内 + 同一个窗口 → 直接复用 ✓
        ///   连续打字时不会每次都去走一遍 UIA ✧ 不卡 ✓
        public static string Recent(int want)
        {
            IntPtr h = IntPtr.Zero;
            try { h = Native.GetForegroundWindow(); } catch { }
            long now = Environment.TickCount;
            long age = now - _cacheAt;
            if (h == _cacheHwnd && age >= 0 && age < 1500) return _cache;
            string v = RecentRaw(want, h);
            _cacheHwnd = h; _cacheAt = now; _cache = v;
            return v;
        }

        /// 返回拼好的上下文 ✧ 读不到就是 "" ✓
        static string RecentRaw(int want, IntPtr h)
        {
            try
            {
                if (h == IntPtr.Zero) return "";
                System.Windows.Automation.AutomationElement root =
                    System.Windows.Automation.AutomationElement.FromHandle(h);
                if (root == null) return "";

                // ★ 先催一下 Chromium 的无障碍树 ✓
                System.Windows.Automation.AutomationFocusChangedEventHandler hh = null;
                try
                {
                    hh = delegate(object s, System.Windows.Automation.AutomationFocusChangedEventArgs e) { };
                    System.Windows.Automation.Automation.AddAutomationFocusChangedEventHandler(hh);
                }
                catch { }

                int tries = 0;
                for (; tries < 4; tries++)
                {
                    flat.Clear();
                    Walk(root, 0, -1);
                    if (flat.Count >= 50) break;
                    System.Threading.Thread.Sleep(350);
                    try { root = System.Windows.Automation.AutomationElement.FromHandle(h); } catch { }
                }
                if (hh != null)
                {
                    try { System.Windows.Automation.Automation.RemoveAutomationFocusChangedEventHandler(hh); }
                    catch { }
                }
                if (flat.Count < 50) return "";

                int mi = MsgList();
                if (mi < 0) return "";

                List<string> lines = new List<string>();
                double cx = flat[mi].X + flat[mi].W / 2.0;

                // ---- QQ ----
                bool any = false;
                for (int i = 0; i < flat.Count; i++)
                {
                    if (flat[i].Cls != "message" || !Under(i, mi)) continue;
                    string body = BodyOf(i, "msg-content-container");
                    if (body.Length == 0) continue;
                    string side = "";
                    foreach (int k in Kids(i))
                    {
                        if (flat[k].Cls.StartsWith("msg-content-container") && flat[k].HasRect)
                        {
                            side = (flat[k].X + flat[k].W / 2.0) > cx ? "我" : "对方";
                            break;
                        }
                    }
                    lines.Add(side + "：" + body);
                    any = true;
                }

                // ---- 微信 ----
                if (!any)
                {
                    foreach (int i in Kids(mi))
                    {
                        Nd x = flat[i];
                        if (x.Type != "ListItem" || x.Name.Length == 0) continue;
                        if (x.Cls.IndexOf("ChatTextItemView", StringComparison.OrdinalIgnoreCase) >= 0)
                            lines.Add("：" + x.Name);
                    }
                }

                if (lines.Count == 0) return "";
                int start = Math.Max(0, lines.Count - Math.Max(1, want));
                StringBuilder sb = new StringBuilder();
                for (int i = start; i < lines.Count; i++)
                {
                    sb.Append(lines[i]).Append("\n");
                    if (sb.Length > 1400) break;      // 别把提示词撑爆 ✓
                }
                return sb.ToString().TrimEnd('\n');
            }
            catch { return ""; }
        }
    }

    /// ★★★ 一个条件 ★★★
    ///
    /// 用户提的写法："如果 <条件1> 满足，又不满足 <条件2> 就……"
    /// —— 完全本地判断 ✧ 不联网 ✧ 不花钱 ✓
    class Cond
    {
        // contains / notcontains / prev / next / start / end / whole / len / mood
        public string Kind = "";
        public string Arg = "";
        public int Num = 0;
        public bool NumIsEq = false;   // 长度那一类：是否用 "="
    }

    class Rule
    {
        public string From = "";
        public string To = "";
        /// 空 = 无条件（老写法 ✓）✧ 非空 = 全部满足才生效 ✓
        public List<Cond> Conds = null;
    }

    class Config
    {
        public List<Rule> Rules = new List<Rule>();
        public List<string> Kaomoji = new List<string>();

        /// ★ 2026-10-18：收藏的颜文字 ✧ 非空时只从这里抽 ✓
        ///   —— 用户：88 个里抽到不喜欢的 ✧ 标一下就行 ✓
        public List<string> FavoriteKao = new List<string>();

        /// ★ 2026-10-18：按软件分配风格 ✧ 进程名 → 风格名 ✓
        ///   —— ★ 例：ProcessStyles["QQ.exe"] = "傲娇" ✓
        public Dictionary<string, string> ProcessStyles = new Dictionary<string, string>();

        /// ★ 2026-10-18：浅拷贝就够 ✗ 因为下面的"按软件套风格"只【替换引用】
        ///   从不修改原来那些 List ✗ 所以共享没问题 ✓
        public Config Clone()
        {
            return (Config)this.MemberwiseClone();
        }
        public List<string> SkipEnds = new List<string>();
        public List<string> Whitelist = new List<string>();
        public string Suffix = "";
        public string Template = "{kaomoji}{suffix}";
        public bool EachSentence = true;
        // 语气 styles wrap the whole message. There is no language understanding here:
        // the sentence TYPE picks which pool to draw from, and one variant is picked at
        // random inside it - that is what stops every message reading identically.
        public List<string> Openers = new List<string>();
        public List<string> Closers = new List<string>();
        public List<string> QuestionClosers = new List<string>();
        public List<string> ExclaimClosers = new List<string>();
        public List<string> HappyClosers = new List<string>();
        public List<string> SadClosers = new List<string>();
        public List<string> AngryClosers = new List<string>();
        // 打招呼 / 分别 / 喜爱 / 夸奖 - the social moods. They are checked before the plain
        // emotional ones because they overlap: 你好棒 contains 你好, 我喜欢你 contains 喜欢
        // (which is also a happy word).
        public List<string> GreetingClosers = new List<string>();
        public List<string> FarewellClosers = new List<string>();
        public List<string> LoveClosers = new List<string>();
        public List<string> PraiseClosers = new List<string>();
        // Second round, mapped from the DUT emotion ontology (乐/好/怒/哀/惧/恶/惊, 21
        // subcategories) but cut down to the ones that actually need a DIFFERENT reply.
        // 尊敬 / 相信 / 敬佩 were dropped: they read the same as 夸奖 in a chat.
        public List<string> ThanksClosers = new List<string>();
        public List<string> ApologyClosers = new List<string>();
        public List<string> CongratClosers = new List<string>();
        public List<string> InviteClosers = new List<string>();
        public List<string> SurpriseClosers = new List<string>();
        public List<string> FearClosers = new List<string>();
        public List<string> TiredClosers = new List<string>();
        public List<string> BoredClosers = new List<string>();
        public List<string> DisappointedClosers = new List<string>();
        // 安慰 = the user is comforting the OTHER person ("别难过了"), as opposed to 难过 =
        // the user is sad themselves ("我好难过"). They used to be one pool, and every line
        // in it was written from the comforting side - so typing 不开心 produced
        // "过来，给你靠一下", i.e. the user offering a shoulder while being the sad one.
        public List<string> ComfortClosers = new List<string>();
        // 撒娇 = the user is ASKING for affection ("抱抱我"), which is not the same as 喜爱 =
        // the user is declaring it ("我喜欢你"). Both are the user speaking, but the reply
        // they want to add is different.
        public List<string> CharmClosers = new List<string>();
        // 重度: decorate after commas too
        // 重度: decorate after commas too
        public bool CommaDecorate = false;

        // 语气 = add the opener / closing line wrapper at all. false gives the dry mode:
        // the text is still rewritten by the word rules and still gets 后缀 + 颜文字 at the
        // end (per 程度), but nothing is wrapped around it.
        //
        // This is an explicit switch rather than a side effect of 外观风格. It used to be
        // implied by that button clearing the pools, but once 外观风格 stopped touching the
        // pools (so a stray click could no longer delete a hand-written ruleset) there was
        // no way left to turn the wrapper off.
        public bool Tone = true;

        // 智能语气: ask a LOCAL model which mood the sentence is, then pick from the pools
        // above. The model never writes the reply itself - it only classifies, so a small
        // model cannot go off the rails and the words stay the user's own.
        public bool SmartTone = false;

        /// ★ 启动软件时【自动加载内置模型】吗 ✧ 默认 false ✓
        ///
        /// 用户 2026-10-17 要求：
        ///   "能不能默认不直接启动 ✧ 我想自己在几个模型里选一个启动 ✓"
        /// —— ★ 关了之后：
        ///      开机不会自动拉模型 ✧ 智能语气自动退回关键字 ✓
        ///      想用模型就点【启动内置模型】✧ 它按下面选的那个加载 ✓
        public bool ModelAutoStart = false;

        /// ★ 开不开【语气小模型】（clf.bin ✧ 7.3 MB）✓
        /// 和安卓版同名设置 ✧ 规则键「小模型语气」✓
        public bool TinyMood = false;
        public string LlmEndpoint = "http://127.0.0.1:1234";
        public string LlmModel = "";
        /// 空 = 自动挑（用最大的那个 gguf ✓）✧ 非空 = 用这个文件名 ✓
        public string LlmModelFile = "";

        /// ★ 读聊天上下文（UIA 读 QQ / 微信最近几条 ✓）
        ///
        /// 实测：上下文条数从 2 加到 50 ✧ 准确率一点没变 ✓
        /// 所以默认 8 条就够 ✧ 多了只是白花时间 ✓
        public bool UseContext = false;
        public int ContextLines = 8;
        public int LlmTimeoutMs = 2500;

        // ★ 2026-10-31：大模型改写语气 ✗
        //   off = 不用 / manual = 只有手动转换 / auto = 自动也走 / wrap = 只让它生成包装
        public string RewriteMode = "off";
        public string RewriteEndpoint = "";
        public string RewriteModel = "";
        public string RewriteKey = "";
        public int RewriteTimeoutMs = 3000;
        public int DebounceMs = 900;

    /// \u2605 2026-10-18\uff1a\u989c\u6587\u5b57\u51b7\u5374 \u2727 \u6700\u8fd1 N \u6b21\u7528\u8fc7\u7684\u4e0d\u518d\u62bd \u2717 0 = \u4e0d\u9650\u5236 \u2713
    public int KaoCooldown = 10;

    /// ★ 2026-10-18：语气叠加 ✧ true = 外观和语气可以同时生效 ✓
    ///   false = 回到原来的二选一 ✓
    public bool StackStyles = true;

    /// ★ 2026-10-18：按软件关自动 ✧ 进程名 → 要不要自动转换 ✓
    ///   没写的进程用全局开关 ✗ false = 这个软件里不自动转
    public Dictionary<string, bool> ProcAuto = new Dictionary<string, bool>();

    /// ★ 2026-10-18：语气稳定 ✧ 同一天内用同一批台词 ✓
    public bool StableMood = true;

    /// \u2605 2026-10-18\uff1a\u667a\u80fd\u5ef6\u8fdf \u2727 \u6309\u6572\u4e86\u591a\u5c11\u4e0b\u952e\u4f30\u53e5\u5b50\u957f\u77ed \u2713
    ///   \u77ed\u53e5\u5feb\u8f6c \u2727 \u957f\u53e5\u591a\u7b49 \u2727 \u5f00\u5173\u5173\u6389\u5c31\u7528\u56fa\u5b9a\u503c \u2713
    public bool SmartDelay = true;

    /// \u2605 2026-10-18\uff1a\u6211\u7684\u7ec4\u5408 \u2727 \u4e00\u7ec4\u8bbe\u7f6e\u5feb\u7167 \u2713
    ///   \u683c\u5f0f\uff1a\u540d\u5b57|\u98ce\u683c\u540d|\u7a0b\u5ea6|\u81ea\u52a8|\u5ef6\u8fdf
    public List<string> Combos = new List<string>();

        /// 页面淡入的时长（毫秒）。原来写死在 Ui.cs 里（22 帧 x 18ms = 396ms）。
        /// 手机上做了可配置之后两边对不上，所以电脑版也放出来。
        /// 默认 400 = 原来那个值，不改的话手感和以前完全一样。
        public int UiAnimMs = 400;
        /// 导航滑块 / 放大 相对淡化的百分比（%）。
        /// 100 = 和淡化一样长（默认）；53 = 电脑版最早那个比例（滑块先到位）；
        /// 也可以填 150 让它比淡化更慢。范围 10 - 300。
        public int UiSlidePct = 100;
        public bool AutoMode = true;
        /// ★ 图片保护（默认开）：输入框里有图片 / 附件时，什么都不做。
        ///
        /// 为什么需要：QQ / 微信的输入框【不把图片放进剪贴板】✗
        /// 而转换的最后一步是"全选 + 粘贴"✗ 图片会被替换掉 ✓
        /// 现在靠 UIA 读输入框结构来判断（有图片时会多出一个空的 Group 子元素 ✓）
        /// 想强行转换（明知道会丢图片）就把它关掉 ✓
        public bool ImageGuard = true;
        /// ★ 界面语言：zh 或 en ✗ 存进 rules.txt 的「语言」那行 ✓
        public string Language = "zh";
        public string HotkeyTransform = "Ctrl+Shift+M";
        public string HotkeyPause = "Ctrl+Alt+Shift+P";
        public string HotkeyReload = "Ctrl+Alt+Shift+R";
        /// ★ 撤销上一条（2026-10-18）✧ 把刚才喵化的那句还原回去 ✓
        public string HotkeyUndo = "Ctrl+Alt+Shift+Z";

        // The four look options, all on by default.
        //
        // They started as compile-time switches, which meant a different build for every
        // combination - and when three people wanted three different sets, that stopped being
        // workable. Runtime flags instead, so one build serves everyone: turn off whatever you
        // do not want on the 设置 page.
        public bool UiTopBar = true;    // 顶部猫娘状态气泡
        public bool UiCards = true;     // 运行状态四个数字卡
        public bool UiLab = true;       // 实验室强化（中间猫娘、粉底输出框、大按钮）
        public bool UiKao = true;       // 颜文字库胶囊网格

        // 第二批：同样先做成开关，逐个试。动画默认关，因为它会加重拖动时的负担。
        public bool UiNavIcon = true;   // 左侧导航用猫系线性图标
        public bool UiLevel = true;     // 升级时提示
        public bool UiColor = true;     // 颜色分层（降低粉色饱和度）
        public bool UiPaw = true;       // 卡片角落的小猫爪（最上层子控件）       // 卡片角落的淡猫爪
        public bool UiMood = true;      // 猫娘台词随今日次数变化
        public bool UiAnim = true;      // 页面淡入 + 导航滑块（用户已认可，默认开）

        /// \u2605 2026-10-22\uff1a\u53bb\u91cd\uff08\u4fdd\u6301\u987a\u5e8f\uff09\u2713
        static void Dedupe(List<string> l)
        {
            if (l == null || l.Count < 2) return;
            List<string> keep = new List<string>();
            for (int i = 0; i < l.Count; i++)
                if (!keep.Contains(l[i])) keep.Add(l[i]);
            l.Clear();
            l.AddRange(keep);
        }

        public static Config Load(string path)
        {
            Config c = new Config();
            // \u2605 2026-10-21\uff1a\u9ed8\u8ba4\u4e0d\u518d\u8df3\u8fc7\u4efb\u4f55\u7ed3\u5c3e \u2717
            //   \u539f\u6765\u6709 "\u55b5" / "~" / "\uff5e" \u4e09\u4e2a \u2717 \u53e5\u5c3e\u78b0\u5de7\u5e26\u4e0a\u5c31\u4e0d\u8f6c\u6362\u4e86 \u2717
            //   \u771f\u6b63\u7684\u91cd\u590d\u88c5\u9970\u7531 StripDecorations + SuffixTailIsDecoration \u8ba4 \u2713
            //   \u60f3\u81ea\u5df1\u52a0\u8df3\u8fc7\u8bcd \u2717 rules.txt \u91cc\u5199 \u300c\u8df3\u8fc7\u7ed3\u5c3e = xxx\u300d\u5c31\u884c \u2713
            // \u2605 2026-10-21\uff1a\u4e0d\u80fd\u628a\u6ce2\u6d6a\u53f7\u5f53\u300c\u5df2\u7ecf\u55b5\u5316\u300d \u2717
            //   \u7528\u6237\uff1a\u300c\u8fd9\u53e5\u77ed\u4e00\u70b9\u54e6~\u300d\u6309\u70ed\u952e\u6ca1\u53cd\u5e94 \u2717
            //   \u56e0\u4e3a SkipEnds \u91cc\u6709 "~" \u2717 \u53e5\u5c3e\u4e00\u4e2a\u6ce2\u6d6a\u53f7\u5c31\u88ab\u5f53\u6210\u5df2\u88c5\u9970 \u2717
            //   \u7528\u6237\u624b\u5199\u3001\u8bed\u97f3\u8f6c\u6587\u5b57\u3001\u7ffb\u8bd1\u7ed3\u679c\u90fd\u7231\u5e26~ \u2717 \u5168\u90e8\u88ab\u541e \u2713
            //   \u73b0\u5728\u53ea\u8ba4\u300c\u55b5\u300d\u2717 \u771f\u6b63\u7684\u91cd\u590d\u88c5\u9970\u9760 SuffixTailIsDecoration \u8ba4 \u2713
            if (!File.Exists(path)) { c.Whitelist.Add("QQ"); return c; }

            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#")) continue;
                if (line.StartsWith("//")) continue;

                // A word rule ("再见 => 拜拜") also contains an '='. It MUST be recognised
                // before the settings are parsed: the left side is looked up as a setting
                // name, and "再见" is an alias for the 分别结尾 pool - so that line used to
                // append the literal "> 拜拜" to the pool instead of defining a replacement.
                // ★★★ 条件替换 ★★★
                //
                //   如果 含"吗" 就将 好 替换成 好吗
                //   如果 含"吗"，不含"吧" 就将 好 替换成 好吗
                //
                // ★ 必须在下面那个 '=>' 判断之前 ✧ 也必须在 '=' 设置判断之前 ✓
                //   （不然 "如果……" 会被当成一个叫「如果……」的设置名 ✓）
                if (line.StartsWith("如果", StringComparison.Ordinal))
                {
                    Rule cr = Engine.ParseCondRule(line);
                    if (cr != null && cr.From.Length > 0) c.Rules.Add(cr);
                    continue;
                }

                int arrowPos = line.IndexOf("=>", StringComparison.Ordinal);
                if (arrowPos > 0)
                {
                    Rule wr = new Rule();
                    wr.From = line.Substring(0, arrowPos).Trim();
                    wr.To = line.Substring(arrowPos + 2).Trim();
                    if (wr.From.Length > 0) c.Rules.Add(wr);
                    continue;
                }

                int eq = line.IndexOf('=');
                string key = eq > 0 ? CanonicalKey(line.Substring(0, eq)) : "";
                string val = eq > 0 ? line.Substring(eq + 1).Trim() : "";

                if (key == "kaomoji") { c.Kaomoji.Add(val); continue; }
            if (key == "favkao") { if (!c.FavoriteKao.Contains(val)) c.FavoriteKao.Add(val); continue; }
            if (key == "smartdelay" || key == "\u667A\u80FD\u5EF6\u8FDF")
        {
            c.SmartDelay = (val == "true" || val == "1" || val == "\u5F00");
            continue;
        }
        if (key == "stablemood" || key == "\u8BED\u6C14\u7A33\u5B9A")
        {
            c.StableMood = (val == "true" || val == "1" || val == "\u5F00");
            continue;
        }
        if (key == "proauto" || key == "\u8FDB\u7A0B\u81EA\u52A8")
        {
            int eq2 = val.IndexOf('=');
            if (eq2 > 0)
            {
                string pn2 = val.Substring(0, eq2).Trim();
                string vn2 = val.Substring(eq2 + 1).Trim();
                if (pn2.Length > 0) c.ProcAuto[pn2] = (vn2 == "true" || vn2 == "1" || vn2 == "\u5F00");
            }
            continue;
        }
        if (key == "procstyle")
            {
                int eq0 = val.IndexOf('=');
                if (eq0 > 0)
                {
                    string pn = val.Substring(0, eq0).Trim();
                    string sn = val.Substring(eq0 + 1).Trim();
                    if (pn.Length > 0 && sn.Length > 0) c.ProcessStyles[pn] = sn;
                }
                continue;
            }
                if (key == "skipend") { c.SkipEnds.Add(val); continue; }
                if (key == "whitelist")
                {
                    // ★ 一行可以写多个（逗号 / 顿号 / 空格分隔都认）✓
                    //
                    // 原来是一行只当一个进程名 ✗ 于是
                    //     whitelist = QQ, Discord, Telegram
                    // 变成一个叫 "QQ, Discord, Telegram" 的进程名 ✗
                    // 白名单等于没生效 ✓
                    // 中文的 rules.txt 一直一行一个 ✗ 所以这个坑一直没露出来 ✓
                    foreach (string one in val.Split(new char[] { ',', '\uFF0C', '\u3001', ' ', '\t', ';', '\uFF1B' },
                                                     StringSplitOptions.RemoveEmptyEntries))
                    {
                        string nm = one.Trim();
                        if (nm.Length > 0 && !nm.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                            && !c.Whitelist.Contains(nm))
                            c.Whitelist.Add(nm);
                    }
                    continue;
                }
                if (key == "suffix") { c.Suffix = val; continue; }
                if (key == "template") { c.Template = val; continue; }
                if (key == "eachsentence") { c.EachSentence = ParseBool(val); continue; }
                if (key == "imageguard") { c.ImageGuard = ParseBool(val); continue; }
                if (key == "language") { c.Language = val.Trim().ToLowerInvariant(); continue; }
                if (key == "uitopbar") { c.UiTopBar = ParseBool(val); continue; }
                if (key == "uicards") { c.UiCards = ParseBool(val); continue; }
                if (key == "uilab") { c.UiLab = ParseBool(val); continue; }
                if (key == "uikao") { c.UiKao = ParseBool(val); continue; }
                if (key == "uinavicon") { c.UiNavIcon = ParseBool(val); continue; }
                if (key == "uilevel") { c.UiLevel = ParseBool(val); continue; }
                if (key == "uicolor") { c.UiColor = ParseBool(val); continue; }
                if (key == "uipaw") { c.UiPaw = ParseBool(val); continue; }
                if (key == "uimood") { c.UiMood = ParseBool(val); continue; }
                if (key == "uianim") { c.UiAnim = ParseBool(val); continue; }
                if (key == "opener") { c.Openers.Add(val); continue; }
                if (key == "closer") { c.Closers.Add(val); continue; }
                if (key == "questioncloser") { c.QuestionClosers.Add(val); continue; }
                if (key == "exclaimcloser") { c.ExclaimClosers.Add(val); continue; }
                if (key == "happycloser") { c.HappyClosers.Add(val); continue; }
                if (key == "sadcloser") { c.SadClosers.Add(val); continue; }
                if (key == "angrycloser") { c.AngryClosers.Add(val); continue; }
                if (key == "greetingcloser") { c.GreetingClosers.Add(val); continue; }
                if (key == "farewellcloser") { c.FarewellClosers.Add(val); continue; }
                if (key == "lovecloser") { c.LoveClosers.Add(val); continue; }
                if (key == "praisecloser") { c.PraiseClosers.Add(val); continue; }
                if (key == "thankscloser") { c.ThanksClosers.Add(val); continue; }
                if (key == "apologycloser") { c.ApologyClosers.Add(val); continue; }
                if (key == "congratcloser") { c.CongratClosers.Add(val); continue; }
                if (key == "invitecloser") { c.InviteClosers.Add(val); continue; }
                if (key == "surprisecloser") { c.SurpriseClosers.Add(val); continue; }
                if (key == "fearcloser") { c.FearClosers.Add(val); continue; }
                if (key == "tiredcloser") { c.TiredClosers.Add(val); continue; }
                if (key == "boredcloser") { c.BoredClosers.Add(val); continue; }
                if (key == "disappointedcloser") { c.DisappointedClosers.Add(val); continue; }
                if (key == "comfortcloser") { c.ComfortClosers.Add(val); continue; }
                if (key == "charmcloser") { c.CharmClosers.Add(val); continue; }
                if (key == "commadecorate") { c.CommaDecorate = ParseBool(val); continue; }
                if (key == "smarttone") { c.SmartTone = ParseBool(val); continue; }
                if (key == "modelautostart") { c.ModelAutoStart = ParseBool(val); continue; }
                if (key == "tinymood") { c.TinyMood = ParseBool(val); continue; }
                if (key == "tone") { c.Tone = ParseBool(val); continue; }
                if (key == "llmendpoint") { if (val.Length > 0) c.LlmEndpoint = val; continue; }
                if (key == "llmmodel") { c.LlmModel = val; continue; }
                if (key == "llmmodelfile") { c.LlmModelFile = val; continue; }
                if (key == "usecontext") { c.UseContext = ParseBool(val); continue; }
                if (key == "contextlines")
                {
                    int cv;
                    if (int.TryParse(val, out cv)) c.ContextLines = Math.Max(0, Math.Min(30, cv));
                    continue;
                }
                if (key == "rewritemode") { c.RewriteMode = val.ToLowerInvariant(); continue; }
            if (key == "rewriteendpoint") { c.RewriteEndpoint = val; continue; }
            if (key == "rewritemodel") { c.RewriteModel = val; continue; }
            if (key == "rewritekey") { c.RewriteKey = val; continue; }
            if (key == "rewritetimeout") { int vv; if (int.TryParse(val, out vv) && vv >= 300) c.RewriteTimeoutMs = vv; continue; }
            if (key == "llmtimeout")
                {
                    int v;
                    if (int.TryParse(val, out v) && v >= 200) c.LlmTimeoutMs = v;
                    continue;
                }
                if (key == "level")
                {
                    // 轻度 = decorate once at the very end, 普通 = per sentence,
                    // 重度 = per sentence and after every comma too
                    string v = val.ToLowerInvariant();
                    if (v.Contains("\u8F7B") || v.Contains("light")) { c.EachSentence = false; c.CommaDecorate = false; }
                    else if (v.Contains("\u91CD") || v.Contains("heavy")) { c.EachSentence = true; c.CommaDecorate = true; }
                    else { c.EachSentence = true; c.CommaDecorate = false; }
                    continue;
                }
                if (key == "automode") { c.AutoMode = ParseBool(val); continue; }
                if (key == "hotkey_transform") { c.HotkeyTransform = val; continue; }
                if (key == "hotkey_pause") { c.HotkeyPause = val; continue; }
                if (key == "hotkey_reload") { c.HotkeyReload = val; continue; }
            if (key == "hotkey_undo") { c.HotkeyUndo = val; continue; }
                if (key == "stack" || key == "\u8BED\u6C14\u53E0\u52A0")
        {
            c.StackStyles = (val == "true" || val == "1" || val == "\u5F00");
            continue;
        }
        if (key == "combo" || key == "\u6211\u7684\u7EC4\u5408")
        {
            if (!string.IsNullOrEmpty(val) && val.IndexOf('|') > 0) c.Combos.Add(val);
            continue;
        }
        if (key == "kaocool" || key == "\u989c\u6587\u5B57\u51B7\u5374")
        {
            int kv;
            if (int.TryParse(val, out kv) && kv >= 0 && kv <= 90) c.KaoCooldown = kv;
            continue;
        }
        if (key == "debounce")
                {
                    int v;
                    if (int.TryParse(val, out v) && v >= 100) c.DebounceMs = v;
                    continue;
                }
                if (key == "uianmims")
                {
                    int v;
                    if (int.TryParse(val, out v)) c.UiAnimMs = Math.Max(100, Math.Min(3000, v));
                    continue;
                }
                if (key == "uislidepct")
                {
                    int v;
                    if (int.TryParse(val, out v)) c.UiSlidePct = Math.Max(10, Math.Min(300, v));
                    continue;
                }
            }
            if (c.Whitelist.Count == 0) c.Whitelist.Add("QQ");

            // \u2605 2026-10-22\uff1a\u8bcd\u6c60\u53bb\u91cd \u2717 \u91cd\u590d\u5e94\u7528\u98ce\u683c\u4f1a\u628a\u540c\u4e00\u53e5\u8bdd\u5199\u8fdb\u53bb\u597d\u51e0\u6b21 \u2713
            Dedupe(c.Openers); Dedupe(c.Closers);
            Dedupe(c.QuestionClosers); Dedupe(c.ExclaimClosers);
            Dedupe(c.HappyClosers); Dedupe(c.SadClosers); Dedupe(c.AngryClosers);
            Dedupe(c.GreetingClosers); Dedupe(c.FarewellClosers); Dedupe(c.LoveClosers);
            Dedupe(c.PraiseClosers); Dedupe(c.ThanksClosers); Dedupe(c.ApologyClosers);
            Dedupe(c.CongratClosers); Dedupe(c.InviteClosers); Dedupe(c.SurpriseClosers);
            Dedupe(c.FearClosers); Dedupe(c.TiredClosers); Dedupe(c.BoredClosers);
            Dedupe(c.DisappointedClosers); Dedupe(c.ComfortClosers); Dedupe(c.CharmClosers);

            return c;
        }

        // Setting names accept English or Chinese, with or without separators, so
        // "hotkey_transform", "HotkeyTransform", "热键_转换" and "热键转换" all land on
        // the same setting. Anything unrecognised returns "" and the line is then
        // tried as a "from => to" replacement rule instead.
        static string CanonicalKey(string raw)
        {
            if (raw == null) return "";
            string k = raw.Trim().ToLowerInvariant()
                .Replace("_", "").Replace("-", "").Replace(" ", "").Replace(".", "");
            switch (k)
            {
                case "smartdelay": case "\u667A\u80FD\u5EF6\u8FDF":
            return "smartdelay";
        case "stablemood": case "\u8BED\u6C14\u7A33\u5B9A":
            return "stablemood";
        case "proauto": case "\u8FDB\u7A0B\u81EA\u52A8": case "\u8F6F\u4EF6\u81EA\u52A8":
            return "proauto";
        case "procstyle": case "\u8FDB\u7A0B\u98CE\u683C": case "\u8F6F\u4EF6\u98CE\u683C":
                    return "procstyle";
                case "favkao": case "\u6536\u85CF\u989C\u6587\u5B57": case "\u6536\u85CF":
                    return "favkao";
                case "kaomoji": case "\u989C\u6587\u5B57": case "\u8868\u60C5":
                    return "kaomoji";
                case "suffix": case "\u540E\u7F00": case "\u5C3E\u5DF4":
                    return "suffix";
                case "template": case "\u6A21\u677F":
                    return "template";
                case "skipend": case "\u8DF3\u8FC7": case "\u8DF3\u8FC7\u7ED3\u5C3E":
                case "\u7ED3\u5C3E\u8DF3\u8FC7":
                    return "skipend";
                case "language": case "\u8BED\u8A00": case "\u82F1\u6587":
                    return "language";
                case "imageguard": case "\u56FE\u7247\u4FDD\u62A4": case "\u4FDD\u62A4\u56FE\u7247":
                    return "imageguard";
                case "eachsentence": case "\u6BCF\u53E5": case "\u9010\u53E5":
                    return "eachsentence";
                // 界面-顶部气泡 / 界面-数字卡 / 界面-实验室 / 界面-颜文字胶囊
                case "uitopbar": case "\u754C\u9762\u9876\u90E8\u6C14\u6CE1": case "\u9876\u90E8\u6C14\u6CE1":
                    return "uitopbar";
                case "uicards": case "\u754C\u9762\u6570\u5B57\u5361": case "\u6570\u5B57\u5361":
                    return "uicards";
                case "uilab": case "\u754C\u9762\u5B9E\u9A8C\u5BA4": case "\u5B9E\u9A8C\u5BA4\u5F3A\u5316":
                    return "uilab";
                case "uikao": case "\u754C\u9762\u989C\u6587\u5B57\u80F6\u56CA": case "\u989C\u6587\u5B57\u80F6\u56CA":
                    return "uikao";
                case "uinavicon": case "\u754C\u9762\u5BFC\u822A\u56FE\u6807":
                    return "uinavicon";
                case "uilevel": case "\u754C\u9762\u5347\u7EA7\u63D0\u793A":
                    return "uilevel";
                case "uicolor": case "\u754C\u9762\u989C\u8272\u5206\u5C42":
                    return "uicolor";
                case "uipaw": case "\u754C\u9762\u732B\u722A\u88C5\u9970":
                    return "uipaw";
                case "uimood": case "\u754C\u9762\u53F0\u8BCD\u53D8\u5316":
                    return "uimood";
                case "uianim": case "\u754C\u9762\u52A8\u753B":
                    return "uianim";
            case "uianmims": case "\u754C\u9762\u52A8\u753B\u65F6\u957F":
                return "uianmims";
                case "uislidepct": case "界面滑块比例":
                    return "uislidepct";
                case "opener": case "\u5F00\u5934": case "\u5F00\u573A":
                    return "opener";
                case "closer": case "\u7ED3\u5C3E": case "\u6536\u5C3E":
                    return "closer";
                case "questioncloser": case "\u95EE\u53E5\u7ED3\u5C3E":
                case "\u7591\u95EE\u7ED3\u5C3E": case "\u95EE\u53F7\u7ED3\u5C3E":
                    return "questioncloser";
                case "exclaimcloser": case "\u53F9\u53E5\u7ED3\u5C3E":
                case "\u611F\u53F9\u7ED3\u5C3E": case "\u611F\u53F9\u53E5\u7ED3\u5C3E":
                    return "exclaimcloser";
                case "happycloser": case "\u5F00\u5FC3\u7ED3\u5C3E":
                case "\u9AD8\u5174\u7ED3\u5C3E": case "\u5F00\u5FC3":
                    return "happycloser";
                case "sadcloser": case "\u96BE\u8FC7\u7ED3\u5C3E":
                case "\u4F24\u5FC3\u7ED3\u5C3E": case "\u96BE\u8FC7":
                    return "sadcloser";
                case "angrycloser": case "\u751F\u6C14\u7ED3\u5C3E":
                case "\u751F\u6C14": case "\u70E6\u8E81\u7ED3\u5C3E":
                    return "angrycloser";
                case "greetingcloser": case "\u6253\u62DB\u547C": case "\u95EE\u5019":
                case "\u95EE\u597D": case "\u6253\u62DB\u547C\u7ED3\u5C3E":
                    return "greetingcloser";
                case "farewellcloser": case "\u5206\u522B": case "\u544A\u522B":
                case "\u518D\u89C1": case "\u544A\u522B\u7ED3\u5C3E":
                case "\u5206\u522B\u7ED3\u5C3E":
                    return "farewellcloser";
                case "lovecloser": case "\u559C\u7231": case "\u559C\u6B22":
                case "\u8868\u767D": case "\u559C\u7231\u7ED3\u5C3E":
                    return "lovecloser";
                case "praisecloser": case "\u5938\u5956": case "\u8868\u626C":
                case "\u79F0\u8D5E": case "\u5938\u5956\u7ED3\u5C3E":
                    return "praisecloser";
                case "thankscloser": case "\u611F\u8C22": case "\u591A\u8C22":
                case "\u81F4\u8C22": case "\u611F\u8C22\u7ED3\u5C3E":
                    return "thankscloser";
                case "apologycloser": case "\u9053\u6B49": case "\u62B1\u6B49":
                case "\u5BF9\u4E0D\u8D77": case "\u9053\u6B49\u7ED3\u5C3E":
                    return "apologycloser";
                case "congratcloser": case "\u795D\u8D3A": case "\u606D\u559C":
                case "\u606D\u8D3A": case "\u795D\u8D3A\u7ED3\u5C3E":
                    return "congratcloser";
                case "invitecloser": case "\u9080\u8BF7": case "\u7EA6":
                case "\u9080\u8BF7\u7ED3\u5C3E":
                    return "invitecloser";
                case "surprisecloser": case "\u60CA\u8BB6": case "\u60CA\u5947":
                case "\u5403\u60CA": case "\u60CA\u8BB6\u7ED3\u5C3E":
                    return "surprisecloser";
                case "fearcloser": case "\u5BB3\u6015": case "\u62C5\u5FC3":
                case "\u6050\u60E7": case "\u5BB3\u6015\u7ED3\u5C3E":
                    return "fearcloser";
                case "tiredcloser": case "\u75B2\u60EB": case "\u7D2F":
                case "\u56F0": case "\u75B2\u60EB\u7ED3\u5C3E":
                case "\u7D2F\u4E86\u7ED3\u5C3E":   // \u2605 2026-10-17 \u8865\uff1a\u539f\u6765\u53ea\u6709\u300c\u75B2\u60EB\u7ED3\u5C3E\u300d\u2717
                    return "tiredcloser";
                case "boredcloser": case "\u65E0\u804A":
                case "\u65E0\u804A\u7ED3\u5C3E":
                    return "boredcloser";
                case "disappointedcloser": case "\u5931\u671B":
                case "\u5931\u671B\u7ED3\u5C3E":
                    return "disappointedcloser";
                case "comfortcloser": case "\u5B89\u6170":
                case "\u5B89\u6170\u7ED3\u5C3E":
                    return "comfortcloser";
                case "charmcloser": case "\u6492\u5A07":
                case "\u6492\u5A07\u7ED3\u5C3E":
                    return "charmcloser";
                case "level": case "\u7A0B\u5EA6": case "\u5F3A\u5EA6":
                    return "level";
                case "commadecorate": case "\u9017\u53F7\u4E5F\u52A0": case "\u9017\u53F7":
                    return "commadecorate";
                case "smarttone": case "\u667A\u80FD\u8BED\u6C14": case "\u667A\u80FD":
                    return "smarttone";
                case "modelautostart": case "\u6A21\u578B\u81EA\u542F": case "\u81EA\u52A8\u52A0\u8F7D\u6A21\u578B":
                    return "modelautostart";
                case "tinymood": case "\u5C0F\u6A21\u578B\u8BED\u6C14": case "\u5C0F\u6A21\u578B":
                    return "tinymood";
                case "tone": case "\u8BED\u6C14": case "\u52A0\u8BED\u6C14":
                    return "tone";
                case "rewritemode": case "改写模式": return "rewritemode";
                case "rewriteendpoint": case "改写地址": return "rewriteendpoint";
                case "rewritemodel": case "改写模型": return "rewritemodel";
                case "rewritekey": case "改写密钥": return "rewritekey";
                case "rewritetimeout": case "改写超时": return "rewritetimeout";
                case "llmendpoint": case "\u6A21\u578B\u5730\u5740": case "\u6A21\u578B\u670D\u52A1":
                    return "llmendpoint";
                case "llmmodel": case "\u6A21\u578B\u540D": case "\u6A21\u578B":
                    return "llmmodel";
                case "llmmodelfile": case "\u6A21\u578B\u6587\u4EF6":
                    return "llmmodelfile";
                case "usecontext": case "\u8BFB\u4E0A\u4E0B\u6587": case "\u4E0A\u4E0B\u6587":
                    return "usecontext";
                case "contextlines": case "\u4E0A\u4E0B\u6587\u6761\u6570":
                    return "contextlines";
                case "llmtimeout": case "\u6A21\u578B\u8D85\u65F6":
                    return "llmtimeout";
                case "automode": case "\u81EA\u52A8\u6A21\u5F0F": case "\u81EA\u52A8":
                    return "automode";
                case "stack": case "\u8BED\u6C14\u53E0\u52A0":
            return "stack";
        case "combo": case "\u6211\u7684\u7EC4\u5408": case "\u7EC4\u5408":
            return "combo";
        case "kaocool": case "\u989C\u6587\u5B57\u51B7\u5374": case "\u51B7\u5374":
            return "kaocool";
        case "debounce": case "\u5EF6\u8FDF": case "\u505C\u624B": case "\u95F4\u9694":
                    return "debounce";
                case "whitelist": case "\u7A0B\u5E8F": case "\u767D\u540D\u5355":
                case "\u751F\u6548\u7A0B\u5E8F":
                    return "whitelist";
                case "hotkeytransform": case "\u8F6C\u6362\u70ED\u952E":
                case "\u70ED\u952E\u8F6C\u6362":
                    return "hotkey_transform";
                case "hotkeypause": case "\u6682\u505C\u70ED\u952E": case "\u70ED\u952E\u6682\u505C":
                    return "hotkey_pause";
                case "hotkeyreload": case "\u91CD\u8F7D\u70ED\u952E": case "\u70ED\u952E\u91CD\u8F7D":
                    return "hotkey_reload";
                case "hotkeyundo": case "\u64A4\u9500\u70ED\u952E": case "\u70ED\u952E\u64A4\u9500":
                    return "hotkey_undo";
            }
            return "";
        }

        // {kaomoji}/{suffix} and {颜文字}/{后缀} are interchangeable in the template
        public static string ApplyTemplate(string t, string kao, string suffix)
        {
            if (t == null) return "";
            t = t.Replace("{kaomoji}", kao).Replace("{\u989C\u6587\u5B57}", kao);
            t = t.Replace("{suffix}", suffix).Replace("{\u540E\u7F00}", suffix);
            return t;
        }

        static bool ParseBool(string s)
        {
            s = s.Trim().ToLowerInvariant();
            if (s == "1" || s == "true" || s == "yes" || s == "on") return true;
            if (s == "\u5F00" || s == "\u662F" || s == "\u771F" || s == "\u5BF9" || s == "\u542F\u7528") return true;
            return false;
        }
    }

    // ---------------------------------------------------------------- stats

    /// Real usage counters. The mockup this UI follows shows numbers like "127 次" and
    /// "Lv.12"; those were invented, so the same panels are driven from here instead -
    /// every figure on screen is something the program actually counted.
    class Stats
    {
        public string Day = "";
        public int Today = 0;
        public int Total = 0;
        public Dictionary<string, int> Kaomoji = new Dictionary<string, int>();

        /// \u2605 2026-10-18\uff1a\u6700\u8fd1\u55b5\u5316\u8fc7\u7684\uff08\u539f\u6587 / \u7ed3\u679c\uff09\u2727 \u53ea\u5b58\u5185\u5b58 \u2717 \u4e0d\u843d\u76d8 \u2713
        public List<string[]> Recent = new List<string[]>();

        /// \u2605 \u8fde\u7eed\u55b5\u5316\u4e86\u591a\u5c11\u6b21\uff08\u732b\u732b\u51b7\u5374\u5f69\u86cb\u7528\uff09\u2713
        public int Streak = 0;

        /// One level per 40 conversions. Arbitrary, but it is a fixed rule over a real
        /// count, which is the part that matters.
        public const int PerLevel = 40;

        public int Level { get { return 1 + Total / PerLevel; } }
        public int LevelFloor { get { return (Level - 1) * PerLevel; } }
        public int LevelCeil { get { return Level * PerLevel; } }

        public double LevelProgress
        {
            get
            {
                int span = LevelCeil - LevelFloor;
                if (span <= 0) return 0;
                return (double)(Total - LevelFloor) / span;
            }
        }

        public string MostUsed()
        {
            string best = "";
            int n = 0;
            foreach (KeyValuePair<string, int> kv in Kaomoji)
                if (kv.Value > n) { n = kv.Value; best = kv.Key; }
            return best;
        }

        public int KaomojiUses
        {
            get
            {
                int n = 0;
                foreach (KeyValuePair<string, int> kv in Kaomoji) n += kv.Value;
                return n;
            }
        }

        public static Stats Load(string path)
        {
            Stats s = new Stats();
            try
            {
                s.Day = DateTime.Now.ToString("yyyy-MM-dd");
                if (!File.Exists(path)) return s;

                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int eq = raw.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = raw.Substring(0, eq).Trim();
                    string v = raw.Substring(eq + 1);

                    if (k == "day") s.Day = v.Trim();
                    else if (k == "today") int.TryParse(v.Trim(), out s.Today);
                    else if (k == "total") int.TryParse(v.Trim(), out s.Total);
                    else if (k == "kao")
                    {
                        int tab = v.LastIndexOf('\t');
                        if (tab > 0)
                        {
                            int n;
                            if (int.TryParse(v.Substring(tab + 1).Trim(), out n))
                                s.Kaomoji[v.Substring(0, tab)] = n;
                        }
                    }
                }

                // a stored day that is not today resets only the daily figure
                if (s.Day != DateTime.Now.ToString("yyyy-MM-dd")) { s.Day = DateTime.Now.ToString("yyyy-MM-dd"); s.Today = 0; }
            }
            catch { }
            return s;
        }

        public void Save(string path)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("day=" + Day + "\r\n");
                sb.Append("today=" + Today + "\r\n");
                sb.Append("total=" + Total + "\r\n");
                foreach (KeyValuePair<string, int> kv in Kaomoji)
                    sb.Append("kao=" + kv.Key + "\t" + kv.Value + "\r\n");
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        public void Bump(string kaomoji)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (Day != today) { Day = today; Today = 0; }
            Today++;
            Total++;
            if (!string.IsNullOrEmpty(kaomoji))
            {
                int n;
                Kaomoji.TryGetValue(kaomoji, out n);
                Kaomoji[kaomoji] = n + 1;
            }
        }
    }

    // ---------------------------------------------------------------- local model

    /// Talks to a model server running ON THIS MACHINE (LM Studio, Ollama, anything with
    /// an OpenAI-compatible endpoint). It only ever asks one question - "which mood is
    /// this sentence" - and throws the answer away if it is not one of the five words we
    /// know, so the model can never inject its own text into someone's chat message.
    ///
    /// Nothing here reaches the internet: the endpoint is a loopback address and the
    /// whole feature is off unless the user turns it on.
    /// ★★★ 语气小分类器 ✧ 从安卓版逐行对齐过来的 ★★★
    ///
    /// 与 TinyClf.java 完全同构：
    ///   · 权重 clf.bin = 7 类 × 262145 维的 little-endian float32
    ///   · 分类取「py段最后 600 字」
    ///   · 1/2/3 元字符 n-gram ✧ FNV-1a 哈希 ✧ 桶 = h % 262144
    ///   · 得分 = bias + Σ权重[c][桶] ✧ argmax ✓
    ///
    /// ★ 为什么不能用 string.GetHashCode()
    ///   .NET 的字符串哈希【每次进程都不一样】✗
    ///   而且也跟 Python 训练那边对不上 ✧ 权重全废 ✗
    ///   —— 所以必须自己实现 FNV-1a ✓
    static class TinyClf
    {
        public const int DIM = 1 << 17;                 // 131072 个桶（13 类 × 131073 × 4 = 6.5 MB ✓）
        static readonly int[] NGRAMS = { 1, 2, 3 };

        /// ★ 标签顺序必须和训练时完全一致 ✧ 改一个字都会全错 ✓
        static readonly string[] LABELS = {
            "安慰", "感谢", "分别", "开心",
            "疑问", "普通", "生气",
            "难过", "累了", "喜爱", "夸奖",
            "害怕", "失望"
        };

        static float[] W;
        static int NCLS;
        static bool loaded, tried, failed;

        /// ★ 小模型中文标签 → 引擎内部的语气代号 ✓
        public static string ToMood(string label)
        {
            if (label == null) return "plain";
            if (label == "安慰") return "comfort";
            if (label == "感谢") return "thanks";
            if (label == "分别") return "farewell";
            if (label == "开心") return "happy";
            if (label == "疑问") return "question";
            if (label == "生气") return "angry";
            if (label == "难过") return "sad";
            if (label == "累了") return "tired";
            if (label == "喜爱") return "love";
            if (label == "夸奖") return "praise";
            if (label == "害怕") return "fear";
            if (label == "失望") return "disappointed";
            return "plain";
        }

        public static bool Loaded { get { return loaded; } }

        /// ★ 从模型文件夹里找 clf.bin ✧ 找不到就当没开启 ✓
        public static bool Ensure()
        {
            if (loaded) return true;
            if (tried) return loaded;
            tried = true;
            try
            {
                string path = FindWeights();
                if (path == null) { failed = true; return false; }
                byte[] all = File.ReadAllBytes(path);
                int total = all.Length / 4;
                NCLS = LABELS.Length;
                if (total != NCLS * (DIM + 1))
                {
                    Log("小模型：权重尺寸对不上（" + total + " 个 float），不用");
                    failed = true;
                    return false;
                }
                float[] w = new float[total];
                for (int i = 0; i < total; i++)
                    w[i] = BitConverter.ToSingle(all, i * 4);   // x86 上就是 little-endian ✓
                W = w;
                loaded = true;
                Log("小模型已加载：" + path + "（" + (all.Length / 1024 / 1024) + " MB）");
                return true;
            }
            catch (Exception ex)
            {
                failed = true;
                Log("小模型加载失败：" + ex.Message);
                return false;
            }
        }

        static string FindWeights()
        {
            // \u2605 --clf \u6a21\u5f0f\u4e0b\u6ca1\u6709 App \u5bf9\u8c61 \u2727 \u56de\u9000\u5230 exe \u6240\u5728\u76ee\u5f55 \u2713
            string baseDir = (App.Cur != null) ? App.Cur.BaseDir
                                             : AppDomain.CurrentDomain.BaseDirectory;
            if (string.IsNullOrEmpty(baseDir)) return null;
            foreach (string d in App.ModelDirs(baseDir))
            {
                try
                {
                    string p = Path.Combine(d, "clf.bin");
                    if (File.Exists(p)) return p;
                }
                catch { }
            }
            return null;
        }

        /// ★ FNV-1a ✧ 必须和 Python / Java 逐位一致 ✓
        public static uint Fnv1a(string str)
        {
            uint h = 0x811C9DC5u;
            byte[] bs = Encoding.UTF8.GetBytes(str);
            for (int i = 0; i < bs.Length; i++)
            {
                h ^= bs[i];
                h *= 0x01000193u;
            }
            return h;
        }

        static int Bucket(string g)
        {
            return (int)(Fnv1a(g) % (uint)DIM);
        }

        /// ★ 分类 ✧ 没加载或出错返回 null（调用方要能处理 ✓）
        public static string Classify(string ctx, string reply)
        {
            if (!Ensure()) return null;
            if (reply == null) return null;
            try
            {
                string text = string.IsNullOrEmpty(ctx) ? reply : (ctx + " " + reply);
                if (text.Length > 600) text = text.Substring(text.Length - 600);   // 尾巴最重要 ✓
                int len = text.Length;
                if (len == 0) return null;

                int stride = DIM + 1;
                float[] score = new float[NCLS];
                for (int c = 0; c < NCLS; c++) score[c] = W[c * stride + DIM];   // bias
                for (int gi = 0; gi < NGRAMS.Length; gi++)
                {
                    int g = NGRAMS[gi];
                    if (g > len) continue;
                    for (int i = 0; i + g <= len; i++)
                    {
                        int b = Bucket(text.Substring(i, g));
                        for (int c = 0; c < NCLS; c++) score[c] += W[c * stride + b];
                    }
                }
                int best = 0;
                for (int c = 1; c < NCLS; c++) if (score[c] > score[best]) best = c;
                return LABELS[best];
            }
            catch { return null; }
        }

        static void Log(string s)
        {
            try { App.LogStatic(s); } catch { }
        }
    }

    static class Llm
    {
        static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();
        public static string LastError = "";

        public static void ClearCache() { Cache.Clear(); }

        /// Builds the URL to call from whatever the user typed as 模型地址.
        ///
        /// Written to accept every shape a local server documents, because getting this wrong
        /// silently breaks the connection: LM Studio says http://127.0.0.1:1234, Ollama says
        /// http://127.0.0.1:11434/v1, and llama.cpp says just the host and port. The old code
        /// only checked for the FULL path, so pasting Ollama's documented address produced
        /// /v1/v1/chat/completions and never connected.
        public static string ApiUrl(string endpoint, string tail)
        {
            string url = (endpoint ?? "").Trim().TrimEnd('/');
            if (url.Length == 0) return url;

            // The user may have pasted a whole endpoint rather than a base address. Strip a
            // known call path first, otherwise the result doubles up:
            //   http://host:8080/v1/chat/completions  ->  .../v1/chat/completions/v1/models
            string[] known = { "/v1/chat/completions", "/v1/models", "/chat/completions", "/models" };
            foreach (string k in known)
            {
                if (url.EndsWith(k, StringComparison.OrdinalIgnoreCase))
                {
                    url = url.Substring(0, url.Length - k.Length).TrimEnd('/');
                    break;
                }
            }

            // tail is the part AFTER /v1 ("/chat/completions", "/models"), so a base that
            // already carries /v1 gets the tail appended directly. Getting this wrong is
            // silent - 智能语气 just falls back to keywords - so the shapes are pinned by a
            // self-test rather than by reading the code.
            if (url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) return url + tail;
            return url + "/v1" + tail;
        }

        /// Is anything answering on this endpoint right now? Used to decide whether the
        /// bundled server has to be started, and by the 测试连接 button.
        public static bool Ping(string endpoint, int timeoutMs)
        {
            if (string.IsNullOrEmpty(endpoint)) return false;
            try
            {
                string url = ApiUrl(endpoint, "/models");
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.Proxy = null;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    LastError = "";
                    return resp.StatusCode == HttpStatusCode.OK;
                }
            }
            catch (Exception ex) { LastError = ex.Message; return false; }
        }

        public static string Classify(string text, string endpoint, string model, int timeoutMs)
        {
            return Classify(text, "", endpoint, model, timeoutMs);
        }

        /// ★ 带上下文的版本 ✓
        ///
        /// ctx 空 = 和原来一模一样 ✧ 非空 = 把最近几条聊天记录一起给模型 ✓
        /// —— 实测：3B 带上下文 6~7/10 ✧ 不带 4/10 ✓
        ///    但条数从 2 加到 50 ✧ 准确率不变 ✧ 所以默认 8 条 ✓
        public static string Classify(string text, string ctx, string endpoint, string model, int timeoutMs)
        {
            if (string.IsNullOrEmpty(text)) return null;
            if (text.Length > 200) text = text.Substring(0, 200);
            if (ctx == null) ctx = "";
            if (ctx.Length > 1500) ctx = ctx.Substring(ctx.Length - 1500);

            // 缓存键要把上下文算进去 ✧ 不然同一句话在不同聊天里会互相污染 ✓
            string key = "M|" + ctx.GetHashCode() + "|" + text;
            if (Cache.ContainsKey(key)) return Cache[key];

            // Back to the five emotional words. A longer prompt that also asked for
            // 打招呼/分别/喜爱/夸奖 made the 3B model WORSE at the basics: 你吃饭了吗 came back
            // 普通 (was 疑问) and 烦死了 came back 难过 (was 生气). The social moods are decided
            // by keywords instead - see PickCloser.
            string sys = "\u4F60\u662F\u8BED\u6C14\u5206\u7C7B\u5668\u3002\u53EA\u56DE\u7B54\u4E00\u4E2A\u8BCD\uFF1A"
                       + "\u5F00\u5FC3 / \u96BE\u8FC7 / \u751F\u6C14 / \u7591\u95EE / \u666E\u901A\u3002"
                       + "\u4E0D\u8981\u89E3\u91CA\uFF0C\u4E0D\u8981\u6807\u70B9\uFF0C\u4E0D\u8981\u6362\u884C\u3002";
            string user = text;
            if (ctx.Length > 0)
            {
                sys = "你是语气分类器。看【最近的聊天记录】✧"
                    + "判断【用户接下来这句】是什么语气。"
                    + "只回答一个词："
                    + "开心 / 难过 / 生气 / 疑问 / 普通。"
                    + "不要解释，不要标点，不要换行。";
                user = "【最近的聊天记录】\n" + ctx
                     + "\n\n【用户接下来这句】\n" + text;
            }

            string mood = MapMood(Ask(sys, user, endpoint, model, timeoutMs));
            if (mood != null) Cache[key] = mood;
            return mood;
        }

        /// Asks the model to CHOOSE ONE of the candidates the user already wrote. It never
        /// writes the reply itself, so even a confused model cannot put words in someone's
        /// mouth - the worst case is picking a line that fits badly, and a -1 falls back
        /// to a random pick.
        public static int ChooseIndex(string text, List<string> options, string endpoint,
                                      string model, int timeoutMs)
        {
            if (options == null || options.Count < 2 || string.IsNullOrEmpty(text)) return -1;

            StringBuilder list = new StringBuilder();
            for (int i = 0; i < options.Count; i++)
                list.Append((i + 1) + ". " + options[i] + "\n");

            string key = "C|" + text + "|" + options.Count + "|" + string.Join("/", options.ToArray());
            if (Cache.ContainsKey(key))
            {
                int c;
                if (int.TryParse(Cache[key], out c)) return c;
            }

            string sys = "\u4ECE\u5019\u9009\u91CC\u9009\u4E00\u53E5\u6700\u9002\u5408\u63A5\u5728\u8FD9\u53E5\u8BDD\u540E\u9762\u7684\uFF0C"
                       + "\u53EA\u56DE\u7B54\u5E8F\u53F7\u6570\u5B57\uFF0C\u4E0D\u8981\u89E3\u91CA\u3002";
            string user = "\u53E5\u5B50\uFF1A" + text + "\n\n\u5019\u9009\uFF1A\n" + list.ToString();

            string raw = Ask(sys, user, endpoint, model, timeoutMs);
            int n = FirstNumber(raw);
            if (n < 1 || n > options.Count) return -1;
            Cache[key] = (n - 1).ToString();
            return n - 1;
        }

        static int FirstNumber(string s)
        {
            if (string.IsNullOrEmpty(s)) return -1;
            int i = 0;
            while (i < s.Length && !char.IsDigit(s[i])) i++;
            int j = i;
            while (j < s.Length && char.IsDigit(s[j])) j++;
            if (j <= i) return -1;
            int v;
            return int.TryParse(s.Substring(i, j - i), out v) ? v : -1;
        }

        /// ★ 现在用的是不是【我们自己启动的内置模型】★
        ///
        /// 用内置模型时请求【不带 model 字段】：llama-server 只会加载一个模型 ✗ 
        /// 带上文件名反而可能对不上（用户填的是 qwen2.5-3b-instruct ✗ 
        /// 而实际加载的是 qwen2.5-3b-instruct-q4_k_m.gguf ✓）✗ 于是连不上 ✓
        /// —— ★ 外部服务（LM Studio / Ollama）仍然按用户填的来 ✓
        public static bool BundledModelActive = false;

        public static string Ask(string sys, string user, string endpoint, string model, int timeoutMs)
        {
            try
            {
                string body = "{\"messages\":[{\"role\":\"system\",\"content\":\"" + Json(sys) +
                              "\"},{\"role\":\"user\",\"content\":\"" + Json(user) + "\"}]," +
                              "\"temperature\":0,\"max_tokens\":16,\"stream\":false" +
                              (model != null && model.Length > 0 && !BundledModelActive
                                  ? ",\"model\":\"" + Json(model) + "\"" : "") +
                              "}";

                string url = ApiUrl(endpoint, "/chat/completions");

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.Proxy = null;   // never let this go through a system proxy
                byte[] data = Encoding.UTF8.GetBytes(body);
                req.ContentLength = data.Length;
                using (Stream s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    LastError = "";
                    string raw = r.ReadToEnd();
                    string got = FirstContent2(raw);
                    if (string.IsNullOrEmpty(got))
                        LastError = "raw:" + (raw.Length > 300 ? raw.Substring(0, 300) : raw);
                    return got;
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return null;
            }
        }

        /// pulls the first "content" string out of an OpenAI-style reply. Deliberately not
        /// a full JSON parser: no library is available and this shape is fixed.
        /// \u2605\u2605\u2605 2026-10-31\uff1a\u901a\u7528 chat \u8c03\u7528 \u2717 \u652f\u6301 Key \u2713
        public static string Chat(string system, string user, string endpoint, string model,
                                   string key, int timeoutMs, int maxTokens, double temperature)
        {
            if (string.IsNullOrEmpty(endpoint)) return null;
            // ★ 2026-11-01：.NET 4.8 默认不开 TLS1.2 ✗ https 会直接失败 ✓
            try { System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12; } catch { }
            try
            {
                string body = "{\"messages\":[{\"role\":\"system\",\"content\":\"" + Json(system == null ? "" : system)
                            + "\"},{\"role\":\"user\",\"content\":\"" + Json(user == null ? "" : user) + "\"}],"
                            + "\"temperature\":" + temperature.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                            + ",\"max_tokens\":" + maxTokens + ",\"stream\":false"
                            + (model != null && model.Length > 0 && !BundledModelActive
                                ? ",\"model\":\"" + Json(model) + "\"" : "")
                            + "}";
                string url = ApiUrl(endpoint, "/chat/completions");
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.Proxy = null;
                if (key != null && key.Length > 0)
                    req.Headers["Authorization"] = "Bearer " + key;
                byte[] data = Encoding.UTF8.GetBytes(body);
                req.ContentLength = data.Length;
                using (Stream st = req.GetRequestStream()) st.Write(data, 0, data.Length);
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    LastError = "";
                    return FirstContent(r.ReadToEnd());
                }
            }
            catch (Exception ex) { LastError = ex.Message; return null; }
        }

        /// \u2605 \u6574\u53e5\u91cd\u5199\uff1a\u628a\u539f\u6587\u6539\u6210\u90a3\u4e2a\u53e3\u5473 \u2713
        public static string Rewrite(string plain, string sample, string endpoint, string model,
                                     string key, int timeoutMs)
        {
            string system = "\u4f60\u662f\u4e00\u4e2a\u8bed\u6c14\u6539\u5199\u5668\u3002"
                          + "\u628a\u7528\u6237\u7ed9\u7684\u53e5\u5b50\u6539\u5199\u6210\u4e0b\u9762\u90a3\u79cd\u53e3\u543b\uff1a"
                          + "\u4fdd\u6301\u539f\u610f\u4e0d\u53d8\u3001\u4e0d\u52a0\u65b0\u4fe1\u606f\u3001"
                          + "\u957f\u5ea6\u548c\u539f\u6587\u63a5\u8fd1\uff08\u6700\u591a\u4e24\u500d\uff09\u3001"
                          + "\u53ef\u4ee5\u52a0\u8bed\u6c14\u8bcd\u548c\u53e3\u5934\u7985\u3002"
                          + "\u53ea\u8f93\u51fa\u6539\u5199\u540e\u7684\u90a3\u4e00\u53e5\u8bdd\uff0c"
                          + "\u4e0d\u8981\u89e3\u91ca\u3001\u4e0d\u8981\u5f15\u53f7\u3001\u4e0d\u8981\u6362\u884c\u3002";
            string user = "\u53e3\u547b\u793a\u4f8b\uff1a" + sample + "\n\u8981\u6539\u5199\u7684\u53e5\u5b50\uff1a" + plain;
            string outTxt = Chat(system, user, endpoint, model, key, timeoutMs, 900, 0.8);   // ★ 推理型模型要留思考空间 ✓
            // ★ 2026-11-01：把模型原话写日志 ✗ 没这行就不知道它回了什么 ✓
            App.LogStatic("模型原文：[" + (outTxt == null ? "null" : outTxt) + "]");
            return Clean(outTxt, plain);
        }

        /// \u2605 \u53ea\u751f\u6210\u5305\u88c5\uff1a\u53e5\u9996 || \u53e5\u5c3e \u2713
        public static string Wrap(string plain, string rule, string openerSample, string closerSample,
                                  string styleName, string endpoint, string model,
                                  string key, int timeoutMs)
        {
            // ★★★ 2026-11-01：开头结尾全由模型写 ★★★
            //   只给「规则结果」是不够的 ✗
            //   风格是「只加末尾」时结果里根本没有开头结尾 ✗ 模型只能瞎编 ✓
            //   所以把【风格名 + 这个风格自己的开头/结尾样例】一起喂进去 ✓
            string sys = "你在扮演一个语气是「" + (styleName == null || styleName.Length == 0 ? styleName : styleName) + "」的角色。"
                       + "现在要给一句话加上四个东西，每个都要符合这个风格："
                       + "这句话的情绪是「" + MoodCn(Engine.CurrentMood) + "」，包装要跟着这个情绪走。"
                       + "开头（句首短语气词，如「嗯，」）、"
                       + "后缀（接在正文后面一两个字，如「喵~」）、"
                       + "颜文字（一个符号表情，如 (´・ω・`) ）、"
                       + "结尾（句尾一小句，如「你来啦」）。"
                       + "不要重复原句。"
                       + "只输出四段，用 || 分隔："
                       + "【开头】||【后缀】||【颜文字】||【结尾】。"
                       + "某一个没有就留空，不要解释、不要引号、不要换行。";
            string user = "风格：" + (styleName == null ? "" : styleName)
                        + "\n\u8fd9个风格的开头样例：" + (openerSample == null ? "" : openerSample)
                        + "\n\u8fd9个风格的结尾样例：" + (closerSample == null ? "" : closerSample)
                        + "\n\u8981加包装的句子：" + plain;
            string outTxt = Chat(sys, user, endpoint, model, key, timeoutMs, 800, 0.9);   // ★ 四段 ✓
            if (string.IsNullOrEmpty(outTxt)) return null;
            string[] parts = outTxt.Split(new string[] { "||" }, StringSplitOptions.None);
            if (parts.Length < 2) return null;
            string open = Norm(parts[0]);
            string suf = parts.Length > 1 ? Norm(parts[1]) : "";
            string kao = parts.Length > 2 ? Norm(parts[2]) : "";
            string close = parts.Length > 3 ? Norm(parts[3]) : "";
            if (parts.Length == 2) { suf = ""; kao = ""; }   // 旧格式：只回了开头 + 结尾
            if (open.Length > 24) open = open.Substring(0, 24);
            if (suf.Length > 8) suf = suf.Substring(0, 8);
            if (kao.Length > 32) kao = kao.Substring(0, 32);
            if (close.Length > 40) close = close.Substring(0, 40);
            return open + "\u0001" + suf + "\u0001" + kao + "\u0001" + close;  // 四段 ✓
        }

        /// ★ 2026-11-01：四段里的一段 ✗ 去引号、去换行、去 || ✓
        /** ★ 2026-11-01：情绪代号 → 中文 ✓ */
        static string MoodCn(string m)
        {
            if (m == "angry") return "生气";
            if (m == "sad") return "难过";
            if (m == "happy") return "开心";
            if (m == "thanks") return "感谢";
            if (m == "question") return "疑问";
            if (m == "comfort") return "安慰";
            if (m == "farewell") return "告别";
            if (m == "greeting") return "打招呼";
            if (m == "love") return "喜爱";
            if (m == "praise") return "夸奖";
            if (m == "tired") return "累";
            if (m == "fear") return "害怕";
            if (m == "disappointed") return "失望";
            if (m == "bored") return "无聊";
            return "平常";
        }


        static string Norm(string p)
        {
            if (p == null) return "";
            p = p.Trim().Trim('"', '\u201c', '\u201d', ' ', '\u3002', '\uff0c', ',');
            p = p.Replace("\r", "").Replace("\n", "").Replace("||", "").Trim();
            if (p == "无" || p == "空" || p == "-" || p == "—") return "";
            return p;
        }

        /// ★ 清理模型输出：去引号、去换行、超长就丢 ✓
        static string Clean(string t, string plain)
        {
            if (string.IsNullOrEmpty(t)) return null;
            t = t.Trim();
            t = t.Trim('"', '\u201c', '\u201d', '\'');
            t = t.Replace("\r", " ").Replace("\n", " ").Trim();
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            if (t.Length == 0) return null;
            // ★ 2026-11-01：上一版是 max(24, 原文×3) ✗ 短句模型回 30 字就被丢了 ✓
            if (t.Length > Math.Max(60, plain.Length * 3 + 20)) { LastError = "太长，丢弃：" + t; return null; }
            return t;
        }

        /** ★ 2026-11-01：找第一个非空的 content ✗ 空就继续往后找 ✓ */
        static string FirstContent2(string json)
        {
            int from = 0;
            while (true)
            {
                int i = json.IndexOf("\"content\"", from, StringComparison.Ordinal);
                if (i < 0) break;
                int colon = json.IndexOf(':', i);
                if (colon < 0) break;
                int q = json.IndexOf('"', colon + 1);
                if (q < 0) break;
                string v = ReadJsonString(json, q);
                if (v != null && v.Trim().Length > 0) return v;
                from = q + 1;
            }
            return null;
        }

        static string ReadJsonString(string json, int q)
        {
            StringBuilder sb = new StringBuilder();
            for (int k = q + 1; k < json.Length; k++)
            {
                char c = json[k];
                if (c == '\\' && k + 1 < json.Length)
                {
                    k++;
                    char e = json[k];
                    if (e == 'n') sb.Append('\n');
                    else if (e == 't') sb.Append('\t');
                    else if (e == 'r') sb.Append('\r');
                    else if (e == 'u' && k + 4 < json.Length)
                    {
                        try { sb.Append((char)Convert.ToInt32(json.Substring(k + 1, 4), 16)); k += 4; }
                        catch { }
                    }
                    else sb.Append(e);
                    continue;
                }
                if (c == '"') break;
                sb.Append(c);
            }
            return sb.ToString();
        }

        static string FirstContent(string json)
        {
            int i = json.IndexOf("\"content\"", StringComparison.Ordinal);
            if (i < 0) return null;
            int colon = json.IndexOf(':', i);
            if (colon < 0) return null;
            int q = json.IndexOf('"', colon + 1);
            if (q < 0) return null;

            StringBuilder sb = new StringBuilder();
            for (int k = q + 1; k < json.Length; k++)
            {
                char c = json[k];
                if (c == '\\' && k + 1 < json.Length)
                {
                    k++;
                    char e = json[k];
                    if (e == 'n') sb.Append(' ');
                    else if (e == 'u' && k + 4 < json.Length)
                    {
                        int cp;
                        if (int.TryParse(json.Substring(k + 1, 4),
                                         System.Globalization.NumberStyles.HexNumber,
                                         System.Globalization.CultureInfo.InvariantCulture, out cp))
                        {
                            sb.Append((char)cp);
                            k += 4;
                        }
                    }
                    else sb.Append(e);
                    continue;
                }
                if (c == '"') break;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// anything that is not one of our words is discarded - the model never gets to put
        /// words in the user's mouth.
        ///
        /// The social moods are checked FIRST and the list must stay in step with the prompt
        /// in Classify(). An earlier version only knew the five emotional words, so asked
        /// about "再见啦" the model answered 普通 and that answer OVERRODE the keyword
        /// classifier - 分别 was unreachable whenever 智能语气 was on.
        public static string MapMood(string reply)
        {
            if (string.IsNullOrEmpty(reply)) return null;
            if (reply.IndexOf("\u6253\u62DB\u547C", StringComparison.Ordinal) >= 0) return "greeting";
            if (reply.IndexOf("\u95EE\u5019", StringComparison.Ordinal) >= 0) return "greeting";
            if (reply.IndexOf("\u5206\u522B", StringComparison.Ordinal) >= 0) return "farewell";
            if (reply.IndexOf("\u544A\u522B", StringComparison.Ordinal) >= 0) return "farewell";
            if (reply.IndexOf("\u559C\u7231", StringComparison.Ordinal) >= 0) return "love";
            if (reply.IndexOf("\u8868\u767D", StringComparison.Ordinal) >= 0) return "love";
            if (reply.IndexOf("\u5938\u5956", StringComparison.Ordinal) >= 0) return "praise";
            if (reply.IndexOf("\u8868\u626C", StringComparison.Ordinal) >= 0) return "praise";
            if (reply.IndexOf("\u5F00\u5FC3", StringComparison.Ordinal) >= 0) return "happy";
            if (reply.IndexOf("\u96BE\u8FC7", StringComparison.Ordinal) >= 0) return "sad";
            if (reply.IndexOf("\u751F\u6C14", StringComparison.Ordinal) >= 0) return "angry";
            if (reply.IndexOf("\u7591\u95EE", StringComparison.Ordinal) >= 0) return "question";
            if (reply.IndexOf("\u666E\u901A", StringComparison.Ordinal) >= 0) return "plain";
            return null;
        }

        static string Json(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// the "测试连接" button: proves the endpoint answers before the user relies on it
        public static string Probe(string endpoint, string model, int timeoutMs, out string mood)
        {
            Cache.Clear();
            mood = Classify("\u4ECA\u5929\u597D\u5F00\u5FC3\u554A\u54C8\u54C8", endpoint, model, timeoutMs);
            if (mood != null) return "OK";
            return LastError.Length > 0 ? LastError : "\u6CA1\u6709\u54CD\u5E94";
        }
    }

    // ---------------------------------------------------------------- engine

    static class Engine
    {
        // 重度 also breaks a sentence at commas
        static bool IsComma(char c)
        {
            return c == '\uFF0C' || c == ',' || c == '\u3001';
        }

        /// Punctuation that a kaomoji never contains. Used to stop the damaged-decoration scan
        /// at the end of the kaomoji instead of running on into the next sentence.
        static bool IsDecoPunct(char c)
        {
            return c == '\u3002' || c == '\uFF01' || c == '\uFF1F' || c == '\uFF0C'
                || c == '\u3001' || c == '\uFF1B' || c == '\uFF1A' || c == '\u2026'
                || c == '\u2014' || c == '.' || c == ',' || c == '!' || c == '?'
                || c == ';' || c == ':' || c == ' ' || c == '\t' || c == '\n';
        }

        static bool IsBreak(char c, Config cfg)
        {
            return IsTerm(c) || (cfg.CommaDecorate && IsComma(c));
        }

        static bool IsTerm(char c)
        {
            return c == '\u3002'      // 。
                || c == '\uFF01'      // ！
                || c == '!'
                || c == '\uFF1F'      // ？
                || c == '?'
                || c == '\uFF1B'      // ；
                || c == ';'
                || c == '\u2026';     // …
        }

        // punctuation must never count as "content", so a fragment made only of
        // separators (a lone "，", say) does not get a kaomoji bolted onto it
        static bool IsPunctLike(char c)
        {
            if (char.IsWhiteSpace(c)) return true;
            if (IsTerm(c)) return true;
            return c == '\uFF0C' || c == ',' || c == '\u3001'
                || c == ':' || c == '\uFF1A'
                || c == '\u201C' || c == '\u201D' || c == '\u2018' || c == '\u2019'
                || c == '\u300A' || c == '\u300B' || c == '\u3008' || c == '\u3009'
                || c == '\u3010' || c == '\u3011'
                || c == '\u2014' || c == '-' || c == '\uFF0D';
        }

        static bool HasContent(string s)
        {
            foreach (char c in s)
                if (!IsPunctLike(c)) return true;
            return false;
        }

        static bool EndsWithAny(string s, List<string> list)
        {
            foreach (string t in list)
            {
                if (t == null || t.Length == 0) continue;
                if (s.EndsWith(t, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// The kaomoji picked by the most recent Decorate call, so the stats can tell
        /// which one gets used most. Set as a side effect because the alternative is
        /// threading it back out through every layer of the transform.
        public static string LastKaomoji = "";

        /// ★ 2026-10-18：按天固定种子的 Random ✧ 语气稳定用 ✓
        ///   开关关掉的时候还是真随机 ✗
        static Random dailyRnd;
        static int dailyRndDay = -1;

        /// \u2605 2026-10-18\uff1a\u6309\u300c\u65e5\u671f + \u53e5\u5b50\u300d\u56fa\u5b9a\u79cd\u5b50 \u2727 \u540c\u4e00\u53e5\u8bdd\u7ed3\u679c\u4e0d\u53d8 \u2713
        ///   salt \u975e 0 \u65f6\u53d8\u6210\u53ef\u4ee5\u300c\u6362\u4e00\u4e2a\u300d\u2717 \u6bcf\u6b21 +1 \u5c31\u4e0d\u540c \u2713
        public static int retrySalt = 0;

        public static Random RandomFor(string text)
        {
            if (!stableMood) return new Random();
            int day = DateTime.Now.Year * 1000 + DateTime.Now.DayOfYear;
            int h = 0;
            if (!string.IsNullOrEmpty(text))
            {
                // \u7a33\u5b9a\u7684\u54c8\u5e0c \u2727 string.GetHashCode() \u4e0d\u4fdd\u8bc1\u8de8\u7248\u672c\u4e00\u81f4 \u2717
                for (int i = 0; i < text.Length; i++) h = h * 31 + text[i];
            }
            return new Random(day ^ h ^ (retrySalt * 7919));
        }

        public static void BumpSalt() { retrySalt = (retrySalt + 1) % 1000; }

        public static void ResetSalt() { retrySalt = 0; }

        /// ★ 语气稳定开关 ✧ 默认开 ✓
        public static bool stableMood = true;

        /// \u2605\u2605\u2605 2026-10-18\uff1a\u989c\u6587\u5b57\u51b7\u5374 \u2605\u2605\u2605
        ///   \u6700\u8fd1\u7528\u8fc7\u7684\u5b58\u8fd9\u91cc \u2727 \u62bd\u7684\u65f6\u5019\u6392\u9664 \u2717
        ///   \u6c60\u5b50\u592a\u5c0f\u4e0d\u591f\u6392\u9664\u65f6\u81ea\u52a8\u653e\u5bbd \u2713
        static readonly List<string> kaoRecent = new List<string>();

        static string PickKaomoji(Config cfg, Random rnd)
        {
            List<string> pool = (cfg.FavoriteKao != null && cfg.FavoriteKao.Count > 0)
                                ? cfg.FavoriteKao : cfg.Kaomoji;
            if (pool == null || pool.Count == 0) return "";
            if (pool.Count == 1) return pool[0];

            int cool = cfg.KaoCooldown;
            // \u2605 \u51b7\u5374\u592a\u5927\u4f1a\u628a\u6c60\u5b50\u5168\u6392\u6389 \u2717 \u81ea\u52a8\u9650\u5236\u5230\u6c60\u5b50\u7684\u4e00\u534a \u2713
            int maxCool = Math.Max(0, pool.Count / 2);
            if (cool > maxCool) cool = maxCool;

            if (cool > 0)
            {
                // \u628a\u4e0d\u5728\u6c60\u5b50\u91cc\u7684\u65e7\u8bb0\u5f55\u6e05\u6389\uff08\u6362\u4e86\u98ce\u683c\u4f1a\u6362\u6c60\u5b50\uff09
                for (int i = kaoRecent.Count - 1; i >= 0; i--)
                    if (!pool.Contains(kaoRecent[i])) kaoRecent.RemoveAt(i);

                if (kaoRecent.Count > cool) kaoRecent.RemoveRange(0, kaoRecent.Count - cool);

                List<string> cand = new List<string>();
                for (int i = 0; i < pool.Count; i++)
                    if (!kaoRecent.Contains(pool[i])) cand.Add(pool[i]);

                if (cand.Count > 0)
                {
                    string pick = cand[rnd.Next(cand.Count)];
                    kaoRecent.Add(pick);
                    return pick;
                }
            }

            string any = pool[rnd.Next(pool.Count)];
            kaoRecent.Add(any);
            return any;
        }

/// ★ 句尾是【表达情绪的标点】吗（！……？ ✓）
        ///
        /// 用户要求：
        ///   "当用户添加了标点符号以后如果是！，……或？这种表达情绪的
        ///    喵后面就不要跟~了，如果是其他的例如。或，可以加上~"
        static bool IsEmotionalPunct(string punct)
        {
            if (string.IsNullOrEmpty(punct)) return false;
            foreach (char c in punct)
            {
                if (c == '\uFF01' || c == '!'        // ！ !
                    || c == '\uFF1F' || c == '?'     // ？ ?
                    || c == '\u2026'                 // …
                    || c == '\u2757' || c == '\u2755'   // ❗ ❕
                    || c == '\u2753' || c == '\u2754')  // ❓ ❔
                    return true;
            }
            return false;
        }

        /// ★ 把后缀末尾的【波浪号】去掉 ✓
        ///   支持半角 ~ 全角 ～ 和日文 〜 三种 ✓
        /// ★★★ 按标点决定装饰怎么摆 ★★★
        ///
        /// 用户 2026-10-06 要的（现在仍然有效 ✓）：
        ///   ?  !  ……   →  后缀（去掉 ~）+ 标点 + 颜文字
        ///                   你好？ → 你好喵？(=≧∇≦=)
        ///   没有标点   →  后缀 + 颜文字
        ///
        /// ★ 用户 2026-10-16 改要求了（✧ 这一条翻过来了 ✓）：
        ///   ，  。  ；  ：   →  标点 + 后缀 + 颜文字
        ///                   你好， → 你好，喵~(=≧∇≦=)
        ///   —— ★ 以前是「你好喵~(=≧∇≦=)，」✧
        ///      逗号跟在颜文字后面 ✧ 看着像打错了 ✗
        ///      而且一边打字一边转换时 ✧ 颜文字会被甩到句子中间 ✗
        ///   —— ★ 两种程度（轻度 / 逐句）都照这条来 ✓
        static string PlaceDecoration(string deco, string kao, string punct, bool emo, string suffix)
        {
            if (string.IsNullOrEmpty(punct)) return deco;
            string pre = deco, k = "";
            if (!string.IsNullOrEmpty(kao) && deco.EndsWith(kao, StringComparison.Ordinal))
            {
                pre = deco.Substring(0, deco.Length - kao.Length);
                k = kao;
            }
            if (!emo)
            {
                // ★ 非情绪标点（，。；：）✧ 插到【后缀前面】✓
                //
                // 为什么不能直接 punct + deco：
                //   这个函数有【两个调用处】✧ 传的东西不一样 ✓
                //     ① PlaceDecoration(decoHere, ...)          deco = 纯装饰 喵~(...)
                //     ② PlaceDecoration(body + t1 + kaoJ, ...)  deco = 正文+装饰 你好喵~(...)
                //   —— ★ 直接拼会把② 变成「，你好喵~(...)」✧ 逗号跑到最前面 ✗
                //   —— ★ 所以要在 pre 里把后缀找出来 ✧ 插在它前面 ✓
                string sufBare2 = StripTrailingTilde(suffix);
                int q = pre.LastIndexOf(suffix, StringComparison.Ordinal);
                if (q < 0 && sufBare2.Length > 0 && sufBare2 != suffix)
                    q = pre.LastIndexOf(sufBare2, StringComparison.Ordinal);
                if (q >= 0) return pre.Substring(0, q) + punct + pre.Substring(q) + k;
                return pre + k + punct;     // 找不到后缀 ✧ 退回老行为 ✓
            }
            return StripTrailingTilde(pre) + punct + k;
        }

        /// ★★★ 把【装饰后面补的标点】挪到【整个装饰前面】★★★
        ///
        /// ★ 2026-10-16 改：以前非情绪标点是直接跟在最后（core + punct ✗）
        ///   现在改成整个挪到装饰前面 ✧ 和 PlaceDecoration 保持一致 ✓
        ///
        /// 用户要求："后面加的标点符号能不能挪到颜文字前面"
        ///   读到  呐，你好喵~(´･ω･`)，嗨嗨！今天也一起吗？
        ///   期望  呐，你好喵？(´･ω･`)，你终于来了……我好想你
        /// 理由："你好喵？"才是句子结尾 ✧ 颜文字是跟在后面的表情 ✓
        ///
        /// 返回重拼好的 core（已含标点）✧ 拼不了就返回 null ✓
        static string MovePunctInsideDecoration(string core, string punct,
                                                Config cfg, List<string> decorations)
        {
            if (string.IsNullOrEmpty(core) || string.IsNullOrEmpty(punct)) return null;
            if (cfg.Suffix.Length == 0 || cfg.Kaomoji.Count == 0) return null;
            foreach (char c in punct) if (!IsPunctLike(c)) return null;

            // ★★★ 通用搜索：在 core 结尾找【后缀 … 颜文字】这个骨架 ★★★
            //
            // 不能只看 decorations 列表 ✧ 那是"枚举形状"✓
            //   枚举的话每加一条规则就多一种形状 ✧ 漏一种就重装 ✓
            //   实测漏掉的：后缀 + 标点 + 颜文字 + 又一个标点 ✓
            //     （“你知道吗，你好喵？(っ´ω`c)？” ✓）
            string sufBare = StripTrailingTilde(cfg.Suffix);
            bool emo = IsEmotionalPunct(punct);

            foreach (string k in cfg.Kaomoji)
            {
                if (k == null || k.Length == 0) continue;
                if (!core.EndsWith(k, StringComparison.Ordinal)) continue;
                string kHead = core.Substring(0, core.Length - k.Length);

                // 后缀在 kHead 里的最后位置（带波浪号、不带，两种都认 ✓）
                int p = kHead.LastIndexOf(cfg.Suffix, StringComparison.Ordinal);
                string sufUse = cfg.Suffix;
                if (p < 0 && sufBare.Length > 0 && sufBare != cfg.Suffix)
                {
                    p = kHead.LastIndexOf(sufBare, StringComparison.Ordinal);
                    sufUse = sufBare;
                }
                if (p < 0) continue;

                string mid = kHead.Substring(p + sufUse.Length);
                bool allPunct = true;
                foreach (char mc in mid) if (!IsPunctLike(mc)) { allPunct = false; break; }
                if (!allPunct) continue;

                // ★ 已经夹过同样的标点了 ✧ 不重复加 ✓
                if (mid.EndsWith(punct, StringComparison.Ordinal)) return core;

                // ★ 非情绪标点（，。）✧ 挪到【整个装饰前面】✓
                //   你好喵~(...) + ，  →  你好，喵~(...) ✓
                //   不能挪进【后缀和颜文字之间】✧
                //   那样会变成「你好喵，(...)」✧ 比原来还怪 ✗
                if (!emo) return kHead.Substring(0, p) + punct + sufUse + mid + k;

                string sufOut = sufBare;
                return kHead.Substring(0, p) + sufOut + mid + punct + k;
            }
            return null;
        }

        /// ★ core 末尾是否已经是"装饰 + 标点"这个形状 ✓
        ///   认得出来才不会再装一次 ✓
        static string SplitTrailingPunct(string core, out string punct)
        {
            punct = "";
            if (string.IsNullOrEmpty(core)) return core;
            int t = core.Length;
            while (t > 0 && IsPunctLike(core[t - 1])) t--;
            if (t >= core.Length || t == 0) return core;
            punct = core.Substring(t);
            return core.Substring(0, t);
        }

        /// ★ body 是不是"…后缀 + 标点 + 颜文字"这个形状 ✓
        ///   —— 就是用户把标点补在颜文字后面、被我们挪进来的样子 ✓
        static bool EndsWithPunctInsideDecoration(string body, Config cfg)
        {
            if (string.IsNullOrEmpty(body) || cfg.Kaomoji.Count == 0) return false;
            string suf = StripTrailingTilde(cfg.Suffix);
            if (string.IsNullOrEmpty(suf)) return false;

            foreach (string k in cfg.Kaomoji)
            {
                if (k == null || k.Length == 0) continue;
                if (!body.EndsWith(k, StringComparison.Ordinal)) continue;
                string head = body.Substring(0, body.Length - k.Length);
                int p = head.LastIndexOf(suf, StringComparison.Ordinal);
                if (p < 0) continue;
                string mid = head.Substring(p + suf.Length);
                if (mid.Length == 0) continue;
                bool allPunct = true;
                foreach (char c in mid) if (!IsPunctLike(c)) { allPunct = false; break; }
                if (allPunct) return true;
            }
            return false;
        }

        /// ★★★ 整段是不是全由【后缀的字】拼成的 ★★★
        ///
        /// 喵 / 喵~ / 喵喵 / 喵喵~ 都算 ✓
        /// —— 用户 2026-10-07："连打两个喵他不会进行输出" ✓
        ///    原因是规则里的「跳过结尾 = 喵」✧ 一个「喵」字就被跳过了 ✓
        static bool IsAllSuffix(string s, Config cfg, out string tilde)
        {
            tilde = "";
            if (string.IsNullOrEmpty(s) || cfg.Suffix.Length == 0) return false;
            string bare = StripTrailingTilde(cfg.Suffix);
            if (bare.Length == 0) return false;
            tilde = cfg.Suffix.Substring(bare.Length);

            for (int i = 0; i < s.Length; )
            {
                if (i + bare.Length <= s.Length
                    && string.CompareOrdinal(s, i, bare, 0, bare.Length) == 0)
                {
                    i += bare.Length;
                    continue;
                }
                if (tilde.Length > 0 && tilde.IndexOf(s[i]) >= 0) { i++; continue; }
                return false;
            }
            return true;
        }

        /// ★ 整段就是【后缀本身】吗（喵 / 喵~ ✓）
        ///
        /// 用户："我就想单独打一个喵然后让他自动加颜文字"
        /// —— 这种情况不能按平常那样装饰（会变成"喵喵~(颜文字)" ✗）
        ///    而是【只补一个颜文字】✓
        static bool IsJustSuffix(string s, Config cfg)
        {
            if (string.IsNullOrEmpty(s) || cfg.Suffix.Length == 0) return false;
            string t = s.Trim();
            if (t == cfg.Suffix) return true;
            string bare = StripTrailingTilde(cfg.Suffix);
            return bare.Length > 0 && t == bare;
        }

        /// ★ 只取一个颜文字 ✧ 不带后缀 ✓
        static string KaoOnly(Config cfg, Random rnd)
        {
            // \u2605 2026-10-18\uff1a\u6536\u85cf\u975e\u7a7a\u65f6\u53ea\u4ece\u6536\u85cf\u91cc\u62bd\uff08\u7528\u6237\u6311\u8fc7\u7684\uff09\u2713
            List<string> kp = (cfg.FavoriteKao != null && cfg.FavoriteKao.Count > 0)
                              ? cfg.FavoriteKao : cfg.Kaomoji;
            string k = PickKaomoji(cfg, rnd);
            LastKaomoji = k;
            return k;
        }

        static string StripTrailingTilde(string suf)
        {
            if (string.IsNullOrEmpty(suf)) return suf;
            while (suf.Length > 0)
            {
                char c = suf[suf.Length - 1];
                if (c == '~' || c == '\uFF5E' || c == '\u301C') suf = suf.Substring(0, suf.Length - 1);
                else break;
            }
            return suf;
        }

        static string Decorate(Config cfg, Random rnd)
        {
            return Decorate(cfg, rnd, false);
        }

        /// ★ noTilde = 句尾是情绪标点 ✧ 后缀末尾不加 ~ ✓
        static string Decorate(Config cfg, Random rnd, bool noTilde)
        {
            // \u2605 \u540c\u4e0a\uff1a\u6536\u85cf\u4f18\u5148 \u2713
            List<string> kp2 = (cfg.FavoriteKao != null && cfg.FavoriteKao.Count > 0)
                               ? cfg.FavoriteKao : cfg.Kaomoji;
            string kao = PickKaomoji(cfg, rnd);
            LastKaomoji = kao;
            string suf = noTilde ? StripTrailingTilde(cfg.Suffix) : cfg.Suffix;
            string d = Config.ApplyTemplate(cfg.Template, kao, suf);
            // ★ 英文是"词 + 词"的语言 ✗ 装饰直接贴上去会连成一个词 ✓
            //     hello + meow~ → hellomeow~   ✗
            //     hello + meow~ → hello meow~  ✓
            // 中文没这个问题（汉字后面跟"喵"是自然的 ✓）
            // ★ 判据刻意不看语言设置 ✗ 只看后缀首字符是不是 ASCII 字母/数字 ✓
            if (d.Length > 0 && NeedsSpaceBefore(suf)) d = " " + d;
            return d;
        }

        static bool NeedsSpaceBefore(string suffix)
        {
            if (string.IsNullOrEmpty(suffix)) return false;
            char c = suffix[0];
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
        }

        static bool StartsWithAny(string s, int pos, List<string> list)
        {
            foreach (string t in list)
            {
                if (t.Length == 0) continue;
                if (pos < 0 || pos + t.Length > s.Length) continue;
                if (string.CompareOrdinal(s, pos, t, 0, t.Length) == 0) return true;
            }
            return false;
        }

        // every decoration string that could already be sitting in the text
        static List<string> BuildDecorations(Config cfg)
        {
            List<string> list = new List<string>();
            string t = cfg.Template == null ? "" : cfg.Template;
            // ★ 同时把【去掉波浪号】的版本也算进去 ✓
            //   否则 "你好喵(●'ω'●)！" 会被当成没装饰过 ✧ 再装一次 ✓
            string sufNoTilde = StripTrailingTilde(cfg.Suffix);
            if (sufNoTilde != cfg.Suffix)
            {
                foreach (string k2 in cfg.Kaomoji)
                {
                    string d2 = Config.ApplyTemplate(t, k2, sufNoTilde);
                    if (d2.Length > 0 && !list.Contains(d2)) list.Add(d2);
                }
            }
            foreach (string k in cfg.Kaomoji)
            {
                string d = Config.ApplyTemplate(t, k, cfg.Suffix);
                if (d.Length > 0 && NeedsSpaceBefore(cfg.Suffix)) d = " " + d;
                if (d.Length > 0 && !list.Contains(d)) list.Add(d);
            }
            string d0 = Config.ApplyTemplate(t, "", cfg.Suffix);
            if (d0.Length > 0 && NeedsSpaceBefore(cfg.Suffix)) d0 = " " + d0;
            if (d0.Length > 0 && !list.Contains(d0)) list.Add(d0);
            if (cfg.Suffix.Length > 0 && !list.Contains(cfg.Suffix)) list.Add(cfg.Suffix);
            return list;
        }

        // Prose = something a person typed, as opposed to decoration material.
        // Kaomoji are built from symbols plus stray Greek/Thai/Hangul/Arabic letters
        // ((=^･ω･^=), ฅ^•ﻌ•^ฅ, (๑•̀ㅂ•́)و✧), so only CJK and ASCII alphanumerics count
        // as prose here.
        static bool IsProse(char c)
        {
            if (c >= 0x3400 && c <= 0x9FFF) return true;    // CJK
            if (c >= 0xF900 && c <= 0xFAFF) return true;
            if (c >= 'a' && c <= 'z') return true;
            if (c >= 'A' && c <= 'Z') return true;
            if (c >= '0' && c <= '9') return true;
            return false;
        }

        // True when the suffix appears within s[from, from+len) and NOTHING but
        // decoration material follows it. That is the signature of a kaomoji the user
        // has partially deleted: "hello meow~(kao" still looks like a broken
        // decoration, while "meow~ today was nice" is ordinary prose containing the
        // suffix and must still get decorated.
        // Every closing line the engine may have written, longest first - a long closing line
        // must not be partially eaten by a shorter one from the same table.
        /// 播种：把软件自带的每一个风格都登记进来。
        ///
        /// 为什么要在启动时做：只靠"这次运行见过什么"不够 —— 用户切完风格重启程序，
        /// 框里那句用旧风格写下的话还是认不出来。自带的风格是已知的，启动就全登记，
        /// 这样任何时刻都能认出自己写过的任何一种装饰。
        public static void SeedKnownClosers(string[] lines, string suffix, string[] kaomoji)
        {
            if (suffix != null && suffix.Length > 0 && !EverSuffixes.Contains(suffix))
                EverSuffixes.Add(suffix);
            if (kaomoji != null)
                foreach (string k in kaomoji)
                    if (!string.IsNullOrEmpty(k) && !EverKaomoji.Contains(k)) EverKaomoji.Add(k);
            if (lines != null)
                foreach (string c in lines)
                    if (!string.IsNullOrEmpty(c) && !EverClosers.Contains(c)) EverClosers.Add(c);
        }

        public static List<string> AllCloserLines(Config cfg)
        {
            List<string> all = new List<string>();
            List<string>[] pools = new List<string>[] {
                cfg.Closers, cfg.QuestionClosers, cfg.ExclaimClosers, cfg.HappyClosers,
                cfg.SadClosers, cfg.AngryClosers, cfg.GreetingClosers, cfg.FarewellClosers,
                cfg.LoveClosers, cfg.PraiseClosers, cfg.ThanksClosers, cfg.ApologyClosers,
                cfg.CongratClosers, cfg.InviteClosers, cfg.SurpriseClosers, cfg.FearClosers,
                cfg.TiredClosers, cfg.BoredClosers, cfg.DisappointedClosers,
                cfg.ComfortClosers, cfg.CharmClosers
            };
            foreach (List<string> p in pools) if (p != null) all.AddRange(p);
            // ★ 再加上"写过的所有风格"的关闭语 —— 换风格后旧关闭语也要能认出来。
            all.AddRange(EverClosers);
            all.Sort(delegate(string a, string b) { return b.Length.CompareTo(a.Length); });
            return all;
        }

        // The same "take back my own edit" rule, for the closing line.
        //
        // PeelSuffix only looks at the very end, so the moment the user types one character
        // after a transform the old closing line is stranded in the middle and stops being
        // recognised. A second closing line is then added behind the new text - which is
        // exactly the report: "it treats what it wrote before as if I had typed it".
        // A closer is ordinary prose, so the only evidence is position: punctuation or the end
        // of the text after it means it is still where the engine put it.
        static string StripStaleClosers(string s, Config cfg)
        {
            if (s == null) return s;

            // Exact matches first.
            foreach (string c in AllCloserLines(cfg))
            {
                if (c.Length == 0) continue;
                int i = 0;
                while (i < s.Length && (i = s.IndexOf(c, i, StringComparison.Ordinal)) >= 0)
                {
                    int j = i + c.Length;
                    while (j < s.Length && (s[j] == ' ' || s[j] == '\t')) j++;

                    // ★★★ 判断这个收尾句是不是"搁浅"了 ★★★
                    //
                    // 用户日志：
                    //   read  [你知道吗，你好123喵~(=；ω；=)，我看到你消息就马上回了哦？]
                    //   write [...哦？喵(=；ω；=)，你终于来了……我好想你]   ← 又装了一次 ✓
                    //
                    // 原因：只看 IsProse ✧ 而它只认【CJK / 字母 / 数字】✓
                    //   全角 ？！。， 这些【都不算】✧
                    //   于是收尾句没被剥掉 ✧ 装饰被夹在中间 ✧ 下一遍就再装一次 ✓
                    //
                    // 修法：后面【只跟了一串标点、而且一直到结尾】也算搁浅 ✓
                    //   （正常装饰的收尾句是顶在最后的 ✧ 不会有东西跟在后面 ✓）
                    bool stranded;
                    if (j >= s.Length) stranded = false;              // 到尾了 ✧ 正常装饰 ✓
                    else if (IsProse(s[j])) stranded = true;          // 后面是用户打的字 ✓
                    else
                    {
                        int k = j;
                        while (k < s.Length && IsPunctLike(s[k])) k++;
                        stranded = (k >= s.Length);                    // 后面只有标点 ✧ 也算 ✓
                    }

                    if (stranded)
                        s = s.Remove(i, c.Length);        // stranded - the user typed on
                    else
                        i += c.Length;                    // still attached at the end
                }
            }

            // Then DAMAGED ones, which is what the exact pass cannot see.
            //
            // From a user's log:
            //   08:52:56 write [哼，你好喵！(￣ヘ￣)，哦，是你啊……我、我刚好在忙]
            //   08:53:03 read  [哼，你好喵！(￣ヘ￣)，哦，是你啊……我、我刚好在你好]
            //                                                          ↑ 忙 was overwritten
            // The closer no longer matches, so it stayed in the middle as if it were the
            // user's own words and the next transform wrapped the whole thing AGAIN:
            //           ...我、我刚好在你好喵！(｀・ω・´)，哼，终于想起我了？
            // A closer that is present only as a PREFIX of itself, with the user's text running
            // straight on, is that leftover. Requiring a long run (12 chars) keeps ordinary
            // prose from ever looking like one.
            const int MinDamaged = 12;
            foreach (string c in AllCloserLines(cfg))
            {
                if (c.Length < MinDamaged + 1) continue;
                int i = 0;
                while (i < s.Length)
                {
                    // longest prefix of c that actually appears at i
                    int n = 0;
                    while (n < c.Length && i + n < s.Length && s[i + n] == c[n]) n++;
                    bool damaged = n >= MinDamaged && n < c.Length;
                    if (damaged)
                    {
                        int j = i + n;
                        while (j < s.Length && (s[j] == ' ' || s[j] == '\t')) j++;
                        if (j < s.Length && IsProse(s[j]))
                        {
                            s = s.Remove(i, n);
                            continue;                     // re-check the same index
                        }
                    }
                    i++;
                }
            }
            return s;
        }

        // Every exact string this template + kaomoji pool can produce. Matching whole
        // decorations (not a bare suffix) is what makes stripping safe: the engine only ever
        // writes these exact strings.
        /// 引擎自己【写过】的后缀和颜文字，跨风格累积。
        ///
        /// 为什么需要它：剥旧装饰那两步只看【当前配置】—— 当前后缀、当前颜文字。
        /// 用户一切换风格，上一轮用旧风格写下的装饰就认不出来了，于是新装饰叠在
        /// 旧装饰后面，越用越长。用户的日志里看得很清楚：
        ///     16:52:28  切到萌系：后缀 喵！→喵~，颜文字 10 个→6 个
        ///     16:52:35  read  [哼，你好喵！(￣^￣)，哦，是你啊……我、我刚好在忙你好]
        ///               write [你好喵！(￣^￣)你好喵~ฅ^•ﻌ•^ฅ]
        ///                        ↑ 包装剥掉了，但「喵！(￣^￣)」剥不掉 —— 它属于旧风格
        ///
        /// 记在内存里就够：残留只可能出现在本次运行期间（用户改风格、程序还开着）。
        public static readonly List<string> EverSuffixes = new List<string>();
        public static readonly List<string> EverKaomoji = new List<string>();
        /// 引擎自己写过的【关闭语】（"哼，终于想起我了？"这类）。
        /// 和上面两个同理：只看当前配置的话，换风格后旧关闭语就认不出来，
        /// 会被当成用户自己写的话，下一轮再把整句包一遍。
        public static readonly List<string> EverClosers = new List<string>();

        /// 每次转换前登记一下当前配置用的装饰素材。
        /// 放在 Run 的最前面 —— 这样"用过什么风格"是自然累积出来的，不需要额外的接线。
        public static void RememberStyle(Config cfg)
        {
            if (cfg == null) return;
            if (!string.IsNullOrEmpty(cfg.Suffix) && !EverSuffixes.Contains(cfg.Suffix))
                EverSuffixes.Add(cfg.Suffix);
            foreach (string k in cfg.Kaomoji)
                if (!string.IsNullOrEmpty(k) && !EverKaomoji.Contains(k))
                    EverKaomoji.Add(k);
        }

        public static List<string> DecorationTokens(Config cfg)
        {
            List<string> t = new List<string>();
            // ★ 当前配置 + 这次运行里【用过的所有】后缀 / 颜文字。
            //   只算当前配置的话，换风格之后旧装饰就认不出来，会一层层叠上去：
            //     切到萌系（后缀 喵！→喵~）之后，框里那句「你好喵！(￣^￣)」就剥不掉了，
            //     新装饰跟在后面，越用越长。
            List<string> sufs = new List<string>();
            if (cfg.Suffix != null) sufs.Add(cfg.Suffix);
            foreach (string s2 in EverSuffixes) if (!sufs.Contains(s2)) sufs.Add(s2);
            List<string> kaos = new List<string>();
            foreach (string k in cfg.Kaomoji) if (!kaos.Contains(k)) kaos.Add(k);
            foreach (string k in EverKaomoji) if (!kaos.Contains(k)) kaos.Add(k);
            foreach (string suf in sufs)
            {
                if (suf == null) continue;
                foreach (string k in kaos)
                {
                    string a = cfg.Template
                                  .Replace("{后缀}", suf).Replace("{颜文字}", k)
                                  .Replace("{suffix}", suf).Replace("{kaomoji}", k);
                    if (a.Length > 0 && !t.Contains(a)) t.Add(a);
                }
            }
            // longest first, so a token containing another one still matches whole
            t.Sort(delegate(string a, string b) { return b.Length.CompareTo(a.Length); });
            return t;
        }

        // Removes decoration tokens that are no longer where the engine put them - leftovers
        // from an earlier transform that the user has kept typing after.
        //
        // The test is the FIRST character after the token. A decoration is always written just
        // before a terminator, so what follows it is punctuation or the next sentence. Meeting
        // prose immediately means the user has typed straight on:
        //      你好喵~  +  1  ->  你好喵~1      the '1' is prose  -> stale, strip it
        //      你好喵~，你今天                  the '，' is not  -> still in place, keep it
        // Checking the whole tail instead would call every non-final decoration in a
        // multi-sentence message stale and strip a legitimately decorated sentence.
        // Set just before StripStaleDecorations so it can reach the configured suffix without
        // threading Config through every call. Single-threaded by design: every transform runs
        // on the UI thread, so there is no chance of two configs overlapping.
        [ThreadStatic] static Config currentSuffixCfg;

        static string StripStaleDecorations(string s, List<string> tokens)
        {
            if (s == null) return s;

            // DAMAGED decorations first. This is the case the log finally pinned down:
            //   引擎写入   你好喵~ฅ^•ﻌ•^ฅ
            //   用户删一个 你好喵~ฅ^•ﻌ•ฅ          <- no longer matches any token
            //   用户接着打 你好喵~ฅ^•ﻌ•ฅ你好
            //   引擎追加   你好喵~ฅ^•ﻌ•ฅ你好喵~(kao)   <- looks like the text was duplicated
            // Exact matching cannot see the damaged one, and suffixTailIsDecoration says no
            // because prose follows it. The shape to look for is different: a suffix followed
            // by a run of NON-PROSE characters, with real prose after that run. That is the
            // engine's own leftover, so the suffix and the run go together.
            Config cfg = currentSuffixCfg;
            if (cfg != null && cfg.Suffix.Length > 0)
            {
                int i = 0;
                while (i < s.Length && (i = s.IndexOf(cfg.Suffix, i, StringComparison.Ordinal)) >= 0)
                {
                    // ★★★ 先看一眼：这里是不是【一个完整的已知装饰】★★★
                    //
                    // 不然会误伤【本身就含 ASCII 字母】的颜文字 ✓
                    //   (っ´ω`c) 里的 `c` 是字母 ✧ (TwT) 里的 T 也是 ✓
                    //   而 IsProse 把字母当散文 ✧
                    //   于是扫到 "喵~(" 就被那个字母打断 ✧
                    //   判定"后面是用户打的字" ✧ 把 "喵~(" 当残留剥掉 ✓
                    // （实测：你好喵~(っ´ω`c)，再见啦 → 你好c)喵~(… ✓）
                    if (tokens != null && StartsWithAny(s, i, tokens))
                    {
                        i += cfg.Suffix.Length;
                        continue;
                    }

                    int j = i + cfg.Suffix.Length;
                    int run = j;
                    // The run has to be DECORATION characters only - not prose, and not
                    // punctuation either. Without the second half this ate the suffix of a
                    // perfectly good decoration sitting before a comma:
                    //   你好(=^‥^=)喵~，你今天  ->  你好(=^‥^=)你今天     (wrong)
                    while (run < s.Length && !IsProse(s[run]) && !IsDecoPunct(s[run])) run++;
                    // ...and the very next character has to be REAL TEXT. A decoration sitting
                    // legitimately before a comma - "你好喵！(kao)，下一句" - also has a run of
                    // decoration characters after its suffix, and stripping that one made every
                    // re-run pick a fresh kaomoji. Punctuation after the run means it is in its
                    // proper place; prose means the user has typed on and it is a leftover.
                    if (run > j && run < s.Length && IsProse(s[run]))
                    {
                        s = s.Remove(i, run - i);
                        // no advance: the removal may expose another one at the same index
                    }
                    else i += cfg.Suffix.Length;
                }
            }

            if (tokens == null || tokens.Count == 0) return s;
            foreach (string t in tokens)
            {
                if (t.Length == 0) continue;
                int i = 0;
                while (i < s.Length && (i = s.IndexOf(t, i, StringComparison.Ordinal)) >= 0)
                {
                    int j = i + t.Length;
                    while (j < s.Length && (s[j] == ' ' || s[j] == '\t')) j++;
                    if (j < s.Length && IsProse(s[j]))
                        s = s.Remove(i, t.Length);        // stale - re-check from the same index
                    else
                        i += t.Length;                    // still in place - move past it
                }
            }
            return s;
        }

        static bool SuffixTailIsDecoration(string s, int from, int len, Config cfg)
        {
            if (cfg.Suffix.Length == 0 || len <= 0) return false;
            // EVERY occurrence inside the window is considered, not just the first.
            //
            // That was the bug behind "it treats its own output as mine": once a text carries
            // two decorations ("你好喵~(kao)你好喵~(kao") the FIRST suffix is followed by the
            // user's own words, so checking only the first one answered "not decorated" and a
            // third decoration was appended - every pass added one more.
            int at = from;
            int limit = from + len;
            while (true)
            {
                int idx = s.IndexOf(cfg.Suffix, at, StringComparison.Ordinal);
                if (idx < 0 || idx >= limit) return false;
                bool clean = true;
                for (int i = idx + cfg.Suffix.Length; i < s.Length; i++)
                    if (IsProse(s[i])) { clean = false; break; }
                if (clean) return true;
                at = idx + 1;
            }
        }

        // Is this sentence already decorated?
        //
        // Exact matching alone is not enough: if the user edits one character out of an
        // existing kaomoji the match breaks, the sentence looks undecorated, and a
        // SECOND decoration gets bolted on -
        //   "hello meow~(kao)"  -> delete a char ->  "hello meow~(kao"
        //   -> "...meow~(kao" + " meow~(kao)"  == "hello meow~(kao meow~(kao)"
        /// \u2605 \u6574\u6bb5\u770b\u662f\u4e0d\u662f\u5df2\u7ecf\u88c5\u9970\u8fc7\u4e86 \u2713
        /// \u2605 \u628a\u6211\u4eec\u81ea\u5df1\u88c5\u4e0a\u53bb\u7684\u4e1c\u897f\u786c\u5207\u6389 \u2717 \u53ea\u7559\u6838\u5fc3\u53e5\u5b50 \u2713
        static string StripDecorations(string s, Config cfg)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string t = s;

            // 1) \u5f00\u5934\u7684\u8bed\u6c14\u5305\u88c5\uff08\u4e00\u5b9a\u662f\u62ec\u53f7\u5f00\u5934\uff0c\u5982\u300c\uff08\u4e00\u6296\uff09\u300d\uff09
            if (cfg.Openers != null && cfg.Openers.Count > 0)
            {
                for (int i = 0; i < cfg.Openers.Count; i++)
                {
                    string op = cfg.Openers[i];
                    if (string.IsNullOrEmpty(op) || op.Length < 2) continue;
                    if (t.StartsWith(op))
                    {
                        t = t.Substring(op.Length);
                        // \u5f00\u5934\u5e38\u5e26\u4e00\u4e2a\u7701\u7565\u53f7 / \u7a7a\u683c \u2727 \u4e00\u8d77\u6253\u6389 \u2713
                        t = t.TrimStart('\u2026', '\u3002', '\u3001', ' ', '\u3000', '\uff0c');
                        break;
                    }
                }
            }

            // 2) \u540e\u7f00\u7b2c\u4e00\u6b21\u51fa\u73b0\u7684\u4f4d\u7f6e\uff1a\u540e\u9762\u5168\u662f\u989c\u6587\u5b57\u548c\u8bed\u6c14\u5c3e\u5df4
            if (!string.IsNullOrEmpty(cfg.Suffix) && cfg.Suffix.Length >= 2)
            {
                int p = t.IndexOf(cfg.Suffix);
                if (p > 0) t = t.Substring(0, p);
                else if (p == 0)
                {
                    // \u540e\u7f00\u5728\u6700\u524d\u9762 \u2727 \u6ca1\u6709\u6838\u5fc3\u53e5\u5b50 \u2717 \u4e0d\u52a8 \u2713
                    return s;
                }
            }

            return t.Trim();
        }

        static bool LooksAlreadyDecorated(string s, Config cfg)
        {
            if (string.IsNullOrEmpty(s)) return false;
            string t = s.TrimEnd();

            // \u2605 \u6700\u7a33\u7684\u5224\u636e\uff1a\u6211\u4eec\u7684\u8f93\u51fa\u3010\u5fc5\u7136\u542b\u540e\u7f00\u3011\u2717 \u800c\u5e73\u5e38\u7684\u8bdd\u4e0d\u4f1a \u2713
            if (!string.IsNullOrEmpty(cfg.Suffix) && cfg.Suffix.Length >= 2 && t.Contains(cfg.Suffix)) return true;
            string bare2 = StripTrailingTilde(cfg.Suffix);
            if (!string.IsNullOrEmpty(bare2) && bare2.Length >= 2 && bare2 != cfg.Suffix && t.Contains(bare2)) return true;

            // \u53e5\u5c3e\u4ee5\u540e\u7f00\u7ed3\u5c3e\uff08\u542b\u53bb\u6389\u6ce2\u6d6a\u53f7\u7684\u5f62\u5f0f\uff09
            if (!string.IsNullOrEmpty(cfg.Suffix) && t.EndsWith(cfg.Suffix)) return true;
            string bare = StripTrailingTilde(cfg.Suffix);
            if (!string.IsNullOrEmpty(bare) && bare.Length >= 2 && t.EndsWith(bare)) return true;

            // \u53e5\u5c3e\u4ee5\u989c\u6587\u5b57\u7ed3\u5c3e\uff08\u91cd\u70b9\uff1a\u6cdb\u6c60\u91cc\u4efb\u4f55\u4e00\u4e2a\uff09
            if (cfg.Kaomoji != null)
            {
                for (int i = 0; i < cfg.Kaomoji.Count; i++)
                {
                    string k = cfg.Kaomoji[i];
                    if (!string.IsNullOrEmpty(k) && k.Length >= 3 && t.EndsWith(k)) return true;
                }
            }
            // \u5f00\u5934\u5df2\u7ecf\u662f\u8bed\u6c14\u5305\u88c5\u7684\u5f00\u5934 \u2717 \u7ed3\u5c3e\u53c8\u5e26\u7740\u989c\u6587\u5b57 \u2713
            if (cfg.Openers != null && cfg.Openers.Count > 0 && t.IndexOf('(') >= 0 && t.IndexOf(')') > 0)
            {
                for (int i = 0; i < cfg.Openers.Count; i++)
                {
                    string op = cfg.Openers[i];
                    if (!string.IsNullOrEmpty(op) && op.Length >= 2 && s.StartsWith(op)) return true;
                }
            }
            return false;
        }

        static bool AlreadyDecorated(string s, int afterTerm, string body,
                                     Config cfg, List<string> decorations)
        {
            const int WINDOW = 24;

            // The decoration lives INSIDE the sentence, just before the punctuation, so
            // what matters is whether the body already ends with one.
            if (EndsWithAny(body, decorations)) return true;
            if (EndsWithAny(body, cfg.SkipEnds)) return true;
            // ★ 已经挪好的形状（后缀 + 标点 + 颜文字 ✓）也算已装饰 ✓
            if (EndsWithPunctInsideDecoration(body, cfg)) return true;

            int from = Math.Max(0, body.Length - WINDOW);
            if (SuffixTailIsDecoration(body, from, body.Length - from, cfg)) return true;

            // Text that an older build already decorated has it AFTER the punctuation.
            // Still recognise that shape so nothing gets a second kaomoji bolted on.
            if (afterTerm >= 0)
            {
                if (StartsWithAny(s, afterTerm, decorations)) return true;
                if (StartsWithAny(s, afterTerm, cfg.SkipEnds)) return true;
                if (SuffixTailIsDecoration(s, afterTerm,
                                           Math.Min(WINDOW, s.Length - afterTerm), cfg)) return true;
            }
            return false;
        }

        // s[from..to) is one sentence, terminator included. posAfter is the index just
        // past that terminator, or -1 if the sentence did not end with one.
        //
        // The decoration goes AFTER the terminator, so on a re-run it becomes the head
        // of the next sentence and the sentence still "ends undecorated" - which used
        // to make every pass bolt on another kaomoji. Hence the lookahead.
        static void EmitSegment(StringBuilder outp, string s, int from, int to, int posAfter,
                                Config cfg, Random rnd, List<string> decorations, bool[] deco)
        {
            int end = to;
            while (end > from && (s[end - 1] == ' ' || s[end - 1] == '\t')) end--;

            // Split the trailing punctuation off the body so the decoration lands BEFORE
            // it: "怎么样喵~。" instead of "怎么样。喵~". Stop at a decoration boundary -
            // a suffix like "喵！" ends in a terminator of its own, and eating that would
            // leave the body looking undecorated.
            int punctStart = end;
            // ★★★ 不能只在 posAfter >= 0 时切 ★★★
            //
            //   posAfter 只有【这一段是被标点断开的】才有值 ✓
            //   最后一段（没有断点）是 -1 ✧ 于是不切 ✓
            //   —— 而用户打的 "你好，" 正好就是最后一段 ✗
            //      逗号留在了原地 ✧ 变成 "你好，喵~(颜文字)" ✓
            //   —— 现在改成【总是切】✧ 和轻度那条路一致 ✓
            if (true)
                // ★★★ 逗号也算 ★★★
                //
                // 用户 2026-10-06 要的是：
                //   你好， → 你好喵~(颜文字)，     （逗号在颜文字后面 ✓）
                // 而原来只切【句末标点】✧ 逗号在普通模式下不是 break ✓
                //   于是留在了 body 里 ✧ 变成 "你好，喵~(颜文字)" ✓
                // —— 加上 IsComma 就一致了 ✓
                //   （重度模式本来就会把逗号当 break ✧ 所以只影响普通模式 ✓）
                while (punctStart > from
                       && (IsBreak(s[punctStart - 1], cfg) || IsComma(s[punctStart - 1]))
                       && (deco == null || !deco[punctStart - 1])) punctStart--;

            string body = s.Substring(from, punctStart - from);
            string punct = s.Substring(punctStart, end - punctStart);
            string tail = s.Substring(end, to - end);

            if (body.Length == 0 || !HasTypableContent(body))
            {
                outp.Append(s, from, to - from);
                return;
            }

            // ★ 整段就是后缀本身 ✧ 只补一个颜文字 ✓
            // ★ 整段就是后缀本身 ✧ 只补一个颜文字 ✓
            //   标点也要按规则摆：
            //     喵？ → 喵？(颜文字)      （情绪标点顶掉 ~ ✓）
            //     喵。 → 喵~(颜文字)。     （保留 ~ ✓）
            {
                string tildeJ1;
                if (IsAllSuffix(body, cfg, out tildeJ1))
                {
                    string t1 = (tildeJ1.Length > 0 && body.EndsWith(tildeJ1)) ? "" : tildeJ1;
                    bool emoJ = IsEmotionalPunct(punct);
                    string kaoJ = KaoOnly(cfg, rnd);
                    outp.Append(PlaceDecoration(body + t1 + kaoJ, kaoJ, punct, emoJ, cfg.Suffix));
                    outp.Append(tail);
                    return;
                }
            }

            int p = posAfter;
            if (p >= 0) while (p < s.Length && (s[p] == ' ' || s[p] == '\t')) p++;

            if (AlreadyDecorated(s, p, body, cfg, decorations))
            {
                // ★★★ 用户把标点补在颜文字后面 ✧ 挪到颜文字前面 ★★★
                string moved = MovePunctInsideDecoration(body, punct, cfg, decorations);
                if (moved != null)
                {
                    outp.Append(moved);
                    outp.Append(tail);
                    return;
                }
                outp.Append(s, from, to - from);
                return;
            }

            // ★★★ 按标点摆位置 ★★★
            //   ?  !  ……  → 喵！(颜文字)       后缀去波浪号 ✓
            //   ，  。      → 喵~(颜文字)，      波浪号保留 ✓
            bool emoHere = IsEmotionalPunct(punct);
            string decoHere = Decorate(cfg, rnd, emoHere);
            outp.Append(body);
            outp.Append(PlaceDecoration(decoHere, LastKaomoji, punct, emoHere, cfg.Suffix));
            outp.Append(tail);
        }

        /// Marks the spans of s that are already a known decoration. A suffix like "喵！"
        /// contains a sentence terminator of its own, so without this the splitter cuts
        /// the decoration in half, treats the kaomoji as a fresh sentence and decorates it
        /// again: "你好喵！(￣^￣)" -> "你好喵！(￣^￣)喵！(｀∀´)".
        static bool[] DecorationMask(string s, List<string> decorations)
        {
            bool[] mask = new bool[s.Length];
            if (decorations == null) return mask;
            foreach (string d in decorations)
            {
                if (d.Length == 0) continue;
                int at = 0;
                while (at <= s.Length - d.Length)
                {
                    int i = s.IndexOf(d, at, StringComparison.Ordinal);
                    if (i < 0) break;
                    for (int k = i; k < i + d.Length && k < mask.Length; k++) mask[k] = true;
                    at = i + 1;
                }
            }
            return mask;
        }

        static string PerSentence(string s, Config cfg, Random rnd, List<string> decorations)
        {
            // A "?" or ";" inside a URL or an e-mail is not the end of a sentence. Without
            // this the link https://x.com/a?b=1 got cut at the "?" and the tail stopped
            // looking like a link at all, so it was decorated.
            bool[] guarded = ProtectedMask(s);
            bool[] deco = DecorationMask(s, decorations);

            StringBuilder outp = new StringBuilder();
            int segStart = 0;
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\n' || c == '\r')
                {
                    EmitSegment(outp, s, segStart, i, -1, cfg, rnd, decorations, deco);
                    outp.Append(c);
                    i++;
                    segStart = i;
                    continue;
                }
                if (IsBreak(c, cfg) && !guarded[i] && !deco[i])
                {
                    int j = i;
                    while (j + 1 < s.Length && IsBreak(s[j + 1], cfg) && !guarded[j + 1] && !deco[j + 1]) j++;
                    EmitSegment(outp, s, segStart, j + 1, j + 1, cfg, rnd, decorations, deco);
                    i = j + 1;
                    segStart = i;
                    continue;
                }
                i++;
            }
            if (segStart < s.Length)
                EmitSegment(outp, s, segStart, s.Length, -1, cfg, rnd, decorations, deco);
            return outp.ToString();
        }

        // Rules must be idempotent. A rule like 哈哈 => 哈哈哈 has a replacement that
        // contains its own pattern, so running the rules again over text that was
        // already transformed keeps growing it (哈哈哈 -> 哈哈哈哈 -> ...). Same for
        // 没有 => 才没有呢. Fix: before applying anything, hide the occurrences of each
        // such replacement behind a placeholder, then restore them afterwards.
        /// Spans that must never be rewritten: links, e-mail addresses, Windows paths,
        /// and any alphanumeric token containing a digit (order numbers, versions,
        /// IDs). Mangled links and order numbers are the fastest way to make a tool
        /// like this unbearable, so both the word rules and the sentence decoration
        /// leave these alone.
        static bool[] ProtectedMask(string s)
        {
            bool[] mask = new bool[s.Length];
            Mark(mask, s, @"(https?://|www\.)[^\s，。！？；：、）】」》]+");
            Mark(mask, s, @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}");
            Mark(mask, s, @"[A-Za-z]:\\[^\s]*");
            // no \b: .NET counts CJK as word characters, so "买了3个" would have no
            // boundary around the digit at all
            Mark(mask, s, @"(?<![A-Za-z0-9_])[A-Za-z0-9_]*[0-9][A-Za-z0-9_]*(?![A-Za-z0-9_])");
            return mask;
        }

        static void Mark(bool[] mask, string s, string pattern)
        {
            try
            {
                foreach (Match m in Regex.Matches(s, pattern))
                    for (int i = m.Index; i < m.Index + m.Length && i < mask.Length; i++)
                        mask[i] = true;
            }
            catch { }
        }

        static int[] GuardRuns(string s)
        {
            List<int> runs = new List<int>();
            bool[] mask = ProtectedMask(s);
            int i = 0;
            while (i < s.Length)
            {
                if (!mask[i]) { i++; continue; }
                int j = i;
                while (j < s.Length && mask[j]) j++;
                runs.Add(i);
                runs.Add(j);
                i = j;
            }
            return runs.ToArray();
        }

        /// \u2605\u2605\u2605 \u6761\u4ef6\u66ff\u6362\uff1a\u89e3\u6790 + \u6c42\u503c \u2605\u2605\u2605
        ///
        /// \u5199\u6cd5\uff1a\u5982\u679c \u542b"\u5417" \u5c31\u5c06 \u597d \u66ff\u6362\u6210 \u597d\u5417
        /// \u591a\u4e2a\u6761\u4ef6\u7528\u3010\uff0c\u3001,\u3001\u4e14\u3011\u9694\u5f00 \u2727 \u5168\u90e8\u6ee1\u8db3\u624d\u751f\u6548 \u2713
        /// \u2605 \u5b8c\u5168\u672c\u5730 \u2727 \u4e0d\u8054\u7f51 \u2727 \u4e0d\u82b1\u94b1 \u2713
        public static Rule ParseCondRule(string line)
        {
            string body = line.Substring(2).Trim();
            int at = IndexOfAny(body, new string[] { " \u5c31\u5c06 ", " \u5219\u5c06 ", "\u5c31\u5c06", "\u5219\u5c06" });
            if (at < 0) return null;
            string condPart = body.Substring(0, at).Trim();
            string rest = body.Substring(at).Trim();
            if (rest.StartsWith(" \u5c31\u5c06 ")) rest = rest.Substring(4).Trim();
            else if (rest.StartsWith(" \u5219\u5c06 ")) rest = rest.Substring(4).Trim();
            else rest = rest.Substring(2).Trim();

            int rp = IndexOfAny(rest, new string[] { " \u66ff\u6362\u6210 ", "\u66ff\u6362\u6210" });
            if (rp < 0) return null;
            string fromPart = rest.Substring(0, rp).Trim();
            string toPart = rest.Substring(rp).Trim();
            if (toPart.StartsWith(" \u66ff\u6362\u6210 ")) toPart = toPart.Substring(5).Trim();
            else if (toPart.StartsWith("\u66ff\u6362\u6210")) toPart = toPart.Substring(3).Trim();

            if (fromPart.Length == 0) return null;

            Rule r = new Rule();
            r.From = fromPart;
            r.To = toPart;
            r.Conds = new List<Cond>();

            string[] parts = condPart.Split(new char[] { '\uFF0C', ',', '\u3001' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawOne in parts)
            {
                string one = rawOne.Trim();
                if (one.StartsWith("\u4e14")) one = one.Substring(1).Trim();
                if (one.StartsWith("\u5e76")) one = one.Substring(1).Trim();
                if (one.Length == 0) continue;
                Cond cd = ParseOneCond(one);
                if (cd == null) return null;
                r.Conds.Add(cd);
            }
            if (r.Conds.Count == 0) return null;
            return r;
        }

        static int IndexOfAny(string s, string[] keys)
        {
            int best = -1;
            foreach (string k in keys)
            {
                int i = s.IndexOf(k, StringComparison.Ordinal);
                if (i >= 0 && (best < 0 || i < best)) best = i;
            }
            return best;
        }

        static string Unquote(string s)
        {
            s = s.Trim();
            if (s.Length >= 2)
            {
                char a = s[0], b = s[s.Length - 1];
                if ((a == '"' && b == '"') || (a == '\u201C' && b == '\u201D')
                    || (a == '\u300C' && b == '\u300D') || (a == '\'' && b == '\''))
                    return s.Substring(1, s.Length - 2);
            }
            return s;
        }

        static Cond ParseOneCond(string one)
        {
            Cond c = new Cond();
            if (one == "\u5728\u53e5\u9996" || one == "\u53e5\u9996") { c.Kind = "start"; return c; }
            if (one == "\u5728\u53e5\u5c3e" || one == "\u53e5\u5c3e") { c.Kind = "end"; return c; }
            // \u2605 \u770b\u4e0a\u6587\uff08\u804a\u5929\u4e0a\u4e0b\u6587\uff09\u2727 \u987a\u5e8f\u5f88\u91cd\u8981\uff1a\u5148\u8ba4\u957f\u7684 \u2713
            if (one.StartsWith("\u4e0a\u6587\u4e0d\u542b")) { c.Kind = "ctxnotcontains"; c.Arg = Unquote(one.Substring(4)); return c.Arg.Length > 0 ? c : null; }
            if (one.StartsWith("\u4e0a\u6587\u542b"))      { c.Kind = "ctxcontains";    c.Arg = Unquote(one.Substring(3)); return c.Arg.Length > 0 ? c : null; }
            if (one.StartsWith("\u4e0d\u542b"))  { c.Kind = "notcontains"; c.Arg = Unquote(one.Substring(2)); return c.Arg.Length > 0 ? c : null; }
            if (one.StartsWith("\u542b"))      { c.Kind = "contains";    c.Arg = Unquote(one.Substring(1)); return c.Arg.Length > 0 ? c : null; }
            if (one.StartsWith("\u524d\u6709")) { c.Kind = "prev"; c.Arg = Unquote(one.Substring(2)); return c.Arg.Length > 0 ? c : null; }
            if (one.StartsWith("\u540e\u6709")) { c.Kind = "next"; c.Arg = Unquote(one.Substring(2)); return c.Arg.Length > 0 ? c : null; }
            if (one.StartsWith("\u6574\u53e5\u662f")) { c.Kind = "whole"; c.Arg = Unquote(one.Substring(3)); return c.Arg.Length > 0 ? c : null; }
            if (one.StartsWith("\u8bed\u6c14=") || one.StartsWith("\u8bed\u6c14\uFF1D"))
            { c.Kind = "mood"; c.Arg = one.Substring(3).Trim(); return c.Arg.Length > 0 ? c : null; }
            if (one.StartsWith("\u957f\u5ea6"))
            {
                string t = one.Substring(2).Trim();
                string op = "";
                int k = 0;
                while (k < t.Length && (t[k] == '>' || t[k] == '<' || t[k] == '=' || t[k] == '\uFF1E' || t[k] == '\uFF1C' || t[k] == '\uFF1D'))
                {
                    char ch = t[k];
                    op += (ch == '\uFF1E') ? ">" : ((ch == '\uFF1C') ? "<" : ((ch == '\uFF1D') ? "=" : ch.ToString()));
                    k++;
                }
                int num;
                if (op.Length == 0 || !int.TryParse(t.Substring(k).Trim(), out num)) return null;
                c.Kind = "len"; c.Num = num; c.Arg = op;
                return c;
            }
            return null;
        }

        /// ★ --ctx 注入的假上下文（只给测试用 ✧ null = 正常从窗口读 ✓）
        public static string CtxOverride = null;

        /// \u2605\u2605\u2605 2026-10-22\uff1a\u9884\u89c8\u6a21\u5f0f \u2605\u2605\u2605
        ///   \u8bd5\u4e00\u4e0b\u9875\u6bcf\u6572\u4e00\u4e2a\u5b57\u90fd\u5728\u8dd1\u5b8c\u6574\u5f15\u64ce \u2717
        ///   \u8bfb\u4e0a\u4e0b\u6587\uff08UIA\uff09\u3001\u7b49\u672c\u673a\u6a21\u578b\u56de\u7b54 \u2717 \u4e00\u5361\u5c31\u662f\u534a\u79d2\u2713
        ///   \u9884\u89c8\u53ea\u9700\u8981\u770b\u88c5\u9970\u6548\u679c \u2717 \u60c5\u7eea\u7528\u5173\u952e\u8bcd\u5c31\u591f\u4e86 \u2713
        public static bool PreviewMode = false;

        /// \u2605 2026-10-31\uff1a\u5927\u6a21\u578b\u6539\u5199\u94a9\u5b50 \u2717 (\u539f\u6587, \u89c4\u5219\u7ed3\u679c) -> \u6700\u7ec8\u6587\u672c \u2713
        public static Func<string, string, string> Rewriter = null;

        /// ★ 2026-11-01：当前情绪 ✗ 大模型写包装时要知道这句是什么语气 ✓
        public static string CurrentMood = "";

        /** ★ 2026-11-01：这次是不是热键触发 ✗ manual 模式只有热键才用 AI ✓ */
        public static bool ManualTrigger = true;

        /** ★ 2026-11-01：改写结果缓存 ✗ 同一句话第二次瞬回 ✓ */
        public static readonly Dictionary<string, string> RewriteCache = new Dictionary<string, string>();

        /// ★ 2026-11-01：当前风格名 ✗ 给大模型写开头结尾时当参考 ✓
        public static string StyleName = "";

        /// 规则里有没有用到「上文…」条件 ✧ 没用就别去读聊天记录 ✓
        public static bool NeedsCtx(List<Rule> rules)
        {
            if (rules == null) return false;
            for (int i = 0; i < rules.Count; i++)
            {
                Rule r = rules[i];
                if (r.Conds == null) continue;
                for (int j = 0; j < r.Conds.Count; j++)
                {
                    string k = r.Conds[j].Kind;
                    if (k == "ctxcontains" || k == "ctxnotcontains") return true;
                }
            }
            return false;
        }

        static bool CondOk(Cond c, string s, int at, int fromLen, string plain)
        {
            return CondOk(c, s, at, fromLen, plain, "");
        }

        /// ctx = 聊天上下文 ✧ 读不到就是空串 ✓
        static bool CondOk(Cond c, string s, int at, int fromLen, string plain, string ctx)
        {
            switch (c.Kind)
            {
                // ★ 看上文 ✧ 读不到上下文（ctx 为空）时【两个都返回 false】✗ 不动 ✓
                //   宁可不动 ✧ 也不猜错 ✓
                case "ctxcontains":    return ctx.Length > 0 && ctx.IndexOf(c.Arg, StringComparison.Ordinal) >= 0;
                case "ctxnotcontains": return ctx.Length > 0 && ctx.IndexOf(c.Arg, StringComparison.Ordinal) < 0;
                case "contains":    return plain.IndexOf(c.Arg, StringComparison.Ordinal) >= 0;
                case "notcontains": return plain.IndexOf(c.Arg, StringComparison.Ordinal) < 0;
                case "prev":        return at >= c.Arg.Length
                                           && string.CompareOrdinal(s, at - c.Arg.Length, c.Arg, 0, c.Arg.Length) == 0;
                case "next":        return at + fromLen + c.Arg.Length <= s.Length
                                           && string.CompareOrdinal(s, at + fromLen, c.Arg, 0, c.Arg.Length) == 0;
                case "start":       return at == 0;
                case "end":         return at + fromLen == s.Length;
                case "whole":       return s == c.Arg;
                case "mood":        return Mood(plain) == MoodKey(c.Arg);
                case "len":
                    if (c.Arg == ">")  return plain.Length >  c.Num;
                    if (c.Arg == "<")  return plain.Length <  c.Num;
                    if (c.Arg == ">=") return plain.Length >= c.Num;
                    if (c.Arg == "<=") return plain.Length <= c.Num;
                    return plain.Length == c.Num;
            }
            return false;
        }

        static string MoodKey(string w)
        {
            w = w.Trim();
            if (w == "开心" || w == "高兴" || w == "happy")   return "happy";
            if (w == "难过" || w == "伤心" || w == "sad")     return "sad";
            if (w == "生气" || w == "愤怒" || w == "angry")   return "angry";
            if (w == "疑问" || w == "question") return "question";
            if (w == "普通" || w == "平常" || w == "平淡" || w == "plain") return "plain";
            if (w == "惊讶" || w == "surprise") return "surprise";
            if (w == "害怕" || w == "恐惧" || w == "fear")   return "fear";
            if (w == "疲惫" || w == "累" || w == "tired")         return "tired";
            if (w == "无聊" || w == "bored")         return "bored";
            if (w == "失望" || w == "disappointed") return "disappointed";
            if (w == "安慰" || w == "comfort")       return "comfort";
            if (w == "喜爱" || w == "喜欢" || w == "love") return "love";
            if (w == "分别" || w == "再见" || w == "farewell") return "farewell";
            if (w == "打招呼" || w == "问候" || w == "greeting") return "greeting";
            if (w == "邀请" || w == "invite") return "invite";
            if (w == "夸奖" || w == "表扬" || w == "praise") return "praise";
            if (w == "感谢" || w == "谢谢" || w == "thanks") return "thanks";
            if (w == "道歉" || w == "apology") return "apology";
            if (w == "恭喜" || w == "congrat") return "congrat";
            if (w == "撒娇" || w == "charm") return "charm";
            if (w == "感叹" || w == "exclaim") return "exclaim";
            return w;
        }

        static string ApplyRules(string s, List<Rule> rules)
        {
            return ApplyRules(s, rules, "");
        }

        static string ApplyRules(string s, List<Rule> rules, string ctx)
        {
            // Hide protected spans FIRST, so the replacements below (which run
            // sequentially and can feed into each other) simply never see them.
            int[] runs = GuardRuns(s);
            List<string> guarded = new List<string>();
            if (runs.Length > 0)
            {
                StringBuilder tmp = new StringBuilder();
                int at = 0;
                for (int k = 0; k < runs.Length; k += 2)
                {
                    tmp.Append(s, at, runs[k] - at);
                    guarded.Add(s.Substring(runs[k], runs[k + 1] - runs[k]));
                    tmp.Append("\uE100" + guarded.Count + "\uE101");
                    at = runs[k + 1];
                }
                tmp.Append(s, at, s.Length - at);
                s = tmp.ToString();
            }

            List<string> protect = new List<string>();
            foreach (Rule r in rules)
            {
                if (r.From.Length == 0 || r.To.Length == 0) continue;
                if (r.To.IndexOf(r.From, StringComparison.Ordinal) < 0) continue;
                if (!protect.Contains(r.To)) protect.Add(r.To);
            }
            protect.Sort(delegate(string a, string b) { return b.Length.CompareTo(a.Length); });

            for (int i = 0; i < protect.Count; i++)
                s = s.Replace(protect[i], "\uE000" + i.ToString() + "\uE001");

            // ★★★ 逐条应用 ★★★
            //
            // 无条件的走老路（String.Replace ✧ 快 ✓）
            // 有条件的得知道【匹配在哪个位置】✧
            //   所以手动扫一遍 ✧ 位置不满足就原样放回去 ✓
            foreach (Rule r in rules)
            {
                if (r.Conds == null || r.Conds.Count == 0)
                {
                    s = s.Replace(r.From, r.To);
                    continue;
                }
                if (r.From.Length == 0 || r.To.Length == 0) continue;

                StringBuilder ob = new StringBuilder();
                int scan = 0;
                while (scan <= s.Length)
                {
                    int hit = s.IndexOf(r.From, scan, StringComparison.Ordinal);
                    if (hit < 0) { ob.Append(s, scan, s.Length - scan); break; }
                    ob.Append(s, scan, hit - scan);
                    bool ok = true;
                    for (int ci = 0; ci < r.Conds.Count; ci++)
                        if (!CondOk(r.Conds[ci], s, hit, r.From.Length, s, ctx)) { ok = false; break; }
                    ob.Append(ok ? r.To : r.From);
                    scan = hit + r.From.Length;
                }
                s = ob.ToString();
            }

            for (int i = 0; i < protect.Count; i++)
                s = s.Replace("\uE000" + i.ToString() + "\uE001", protect[i]);

            for (int i = 0; i < guarded.Count; i++)
                s = s.Replace("\uE100" + (i + 1) + "\uE101", guarded[i]);

            return s;
        }

        // a sentence that is nothing but a link or a number is not worth decorating
        static bool HasContentOutsideProtected(string s)
        {
            bool[] mask = ProtectedMask(s);
            for (int i = 0; i < s.Length; i++)
            {
                if (mask[i]) continue;
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') continue;
                if (IsPunctLike(c)) continue;
                return true;
            }
            return false;
        }

        /// ★★★ "用户到底打没打字" ✧ 数字也算 ★★★
        ///
        /// 用户要求："把标点符号和数字也算进打字里面，
        ///            有的时候想加个数字或者标点符号结果他不识别"
        ///
        /// 原因：ProtectedMask 里有一条把【任何数字】整段保护起来 ✧
        /// 目的是不让词库把它改掉 ✓ 但侧效是【只打一个数字】
        /// 就被当成"没内容" ✧ 一个装饰都不加 ✓
        ///
        /// 所以这里单独看一遍：
        ///   只要有一个数字 ✧ 而且它不在【网址 / 邮箱 / 路径】里 ✧ 就算打了字 ✓
        /// —— 那些地方的数字照旧不算 ✧ 免得发个链接也被装饰 ✓
        static bool HasTypableContent(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (HasContentOutsideProtected(s)) return true;

            bool[] hard = new bool[s.Length];
            Mark(hard, s, @"(https?://|www\.)[^\s，。！？；：、）】」》]+");
            Mark(hard, s, @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}");
            Mark(hard, s, @"[A-Za-z]:\\[^\s]*");

            // ★★★ 整段是网址 / 邮箱 / 路径 ✧ 不算打字 ★★★
            for (int i = 0; i < s.Length; i++)
                if (hard[i]) return false;

            // ★★★ 其余情况：只要有一个非空白字符就算 ★★★
            //
            // 用户 2026-10-06："或者打个标点符号转换一下"
            //   原来标点不算内容 ✧ 一个孤零零的「，」不会被装饰 ✓
            //   现在改成：只要不是网址那类 ✧ 标点也算 ✓
            // （数字也跟着一起算了 ✧ 原来那段单独判数字的可以去掉 ✓）
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') continue;
                return true;
            }
            return false;
        }

        /// 逐步轨迹：每个 pass 之后的字符串。只在 --trace 模式下收集，正常跑时是空的。
        ///
        /// 为什么需要它：两个引擎的每一处实现对比下来都逐行相同，但输出就是不一样（5/65），
        /// 说明分岔在【哪一步先做】或者【某个字段的取值】上 —— 光读代码看不出来，只能打点。
        public static List<string> Trail = new List<string>();
        public static bool Trailing = false;

        static void Tr(string tag, string s)
        {
            if (Trailing) Trail.Add(tag + " | " + s);
        }
        /// ★ 看起来像【机器用的编号】吗？
        ///
        /// 这类东西是给程序读的 ✗ 不是给人读的 ✗ 喵化会把它破坏掉 ✓
        ///     #121417        井号 + 数字（游戏内输入法工具的产物）
        ///     1234567        一长串纯数字（验证码 / 订单号之类）
        ///     AB-12345       字母打头的编号
        static bool LooksLikeMachineCode(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            string t = s.Trim();
            if (t.Length < 4) return false;
            try
            {
                if (Regex.IsMatch(t, @"^[#＃][0-9]{3,}$")) return true;
                // ★★★ 去掉了【纯数字】这一条 ★★★
                //   原来是 ^[0-9]{4,}$ ✧ 但那样用户想发个数字都发不了 ✓
                //   （"有的时候想加个数字结果他不识别" ✓）
                //   真正要保护的是 #121417 这种带前缀的编号 ✧
                //   以及 ABC-123 这种带字母前缀的 ID ✧ 那两条保留 ✓
                if (Regex.IsMatch(t, @"^[A-Za-z]{1,4}[-_#][0-9]{3,}$")) return true;
            }
            catch { }
            return false;
        }
        public static string Run(string input, Config cfg, Random rnd)
        {
            stableMood = cfg.StableMood;

            // ★★★ 整段就是【机器用的编号】时，原样返回 ★★★
            //
            // 起因：游戏里那个中文输入法工具的工作方式是
            //   打中文 → 转成 #121417 这种编号 → 输出到游戏。
            // 掩码(ProtectedMask)只保证#121417 这几个字符不被替换规则改掉 ✗
            // 但【语气包装和句尾装饰照样会加上去】——
            //   结果变成「呜哇……#121417……喵」✗ 那个编号就被包在话里了 ✓
            // 那个工具拿到这种内容 ✗ 要么认不出 ✗ 要么发出错误的指令 ✓
            //
            // 这类东西是【给程序读的】✗ 不是给人读的 ✗ 一个字符都不该动 ✓
            if (LooksLikeMachineCode(input)) return input;
            if (input == null || input.Length == 0) return input;

            // \u2605\u2605\u2605 2026-10-18\uff1a\u5df2\u7ecf\u88c5\u9970\u8fc7\u7684\u5c31\u4e0d\u518d\u88c5\u9970 \u2605\u2605\u2605
            //   \u7528\u6237\u8f6c\u5b8c\u4e00\u6b21\u53c8\u6309\u4e00\u6b21\u5feb\u6377\u952e \u2717
            //   \u539f\u6765\u4f1a\u5728\u5df2\u7ecf\u5e26\u989c\u6587\u5b57\u7684\u53e5\u5c3e\u540e\u9762\u518d\u52a0\u4e00\u5c42 \u2717
            //   \u8868\u73b0\u5c31\u662f\u4e00\u53e5\u8bdd\u8d8a\u8f6c\u8d8a\u957f \u2713
            //   \u5224\u636e\uff1a\u53e5\u5c3e\u5df2\u7ecf\u662f\u67d0\u4e2a\u989c\u6587\u5b57\uff0c\u6216\u8005\u5df2\u7ecf\u4ee5\u540e\u7f00\u7ed3\u5c3e \u2713
            // \u2605 2026-10-18\uff1a\u4e0d\u80fd\u76f4\u63a5 return \u2717
            //   \u7528\u6237\u628a\u5df2\u7ecf\u55b5\u5316\u7684\u5185\u5bb9\u5220\u6389\u4e00\u622a \u2717 \u518d\u6309\u5feb\u6377\u952e \u2717
            //   \u5982\u679c\u8fd9\u91cc\u76f4\u63a5\u8fd4\u56de \u2717 \u5c3e\u5df4\u6c38\u8fdc\u8865\u4e0d\u56de\u6765 \u2713
            //   \u6b63\u786e\u505a\u6cd5\uff1a\u8ba9\u4e0b\u9762\u90a3\u4e2a\u300c\u5265\u6389\u65e7\u88c5\u9970\u300d\u7684\u6b65\u9aa4\u53bb\u5904\u7406 \u2717
            //   \u5265\u5b8c\u91cd\u65b0\u88c5 \u2727 \u5c31\u80fd\u628a\u7f3a\u7684\u90a3\u622a\u8865\u56de\u6765 \u2713
            // \u2605\u2605\u2605 2026-10-18\uff1a\u5df2\u7ecf\u662f\u6211\u4eec\u7684\u8f93\u51fa\u65f6\uff0c\u5148\u786c\u5207\u6389\u65e7\u88c5\u9970 \u2605\u2605\u2605
            //   \u4e3a\u4ec0\u4e48\u8981\u786c\u5207\uff1aQQ \u7684 UIA \u8bfb\u51fa\u6765\u662f\u622a\u65ad\u7684 \u2717
            //   \u5c3e\u5df4\u53ea\u5269\u5230\u3010\u300c\u522b\u3001\u522b\u7ad9\u90a3\u4e48\u3011\u2717 \u539f\u6765\u90a3\u4e00\u6b65\u5265\u4e0d\u5e72\u51c0 \u2713
            //   \u6240\u4ee5\uff1a\u627e\u5230\u540e\u7f00\u7b2c\u4e00\u6b21\u51fa\u73b0\u7684\u4f4d\u7f6e \u2727 \u524d\u9762\u5168\u6253\u6389 \u2727 \u53ea\u7559\u6838\u5fc3\u53e5\u5b50 \u2713
            string core = StripDecorations(input, cfg);
            if (core != null && core.Length > 0 && core != input)
            {
                input = core;
            }

            // Peel the wrapper BEFORE the word rules run, not after.
            //
            // The rules can rewrite the wrapper itself: 没有 => 才没有呢 turns the closing
            // line "才没有很高兴呢" into "才才没有呢很高兴呢", the peel then no longer
            // matches, and the decoration gets inserted into the middle of the closing
            // line. Everything downstream is idempotent only if this order is right.
            RememberStyle(cfg);   // 登记这一轮用到的装饰素材 —— 换风格后还要靠它认旧装饰
            string s = input;
            Tr("000 input", s);

            // Order matters. The closing line has to go first: while it is still attached,
            // the decoration in front of it is followed by punctuation and looks legitimate.
            // Once the closer is gone the decoration is the thing directly in front of the
            // user's new text, and the decoration pass can see it.
            s = StripStaleClosers(s, cfg);
            Tr("001 stripClosers", s);
            currentSuffixCfg = cfg;      // StripStaleDecorations needs the suffix to spot damaged ones
            s = StripStaleDecorations(s, DecorationTokens(cfg));
            Tr("002 stripDecorations#1", s);

            Tr("003 peelPrefix in", s);
            string hadOpen = PeelPrefix(s, cfg.Openers);
            if (hadOpen != null) s = s.Substring(hadOpen.Length);

            string hadClose = PeelSuffix(s, cfg.Closers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.QuestionClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.ExclaimClosers);
            // every mood pool has to be peeled too, or the decoration gets inserted into
            // the middle of a closing line that is still attached
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.HappyClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.SadClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.AngryClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.GreetingClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.FarewellClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.LoveClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.PraiseClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.ThanksClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.ApologyClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.CongratClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.InviteClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.SurpriseClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.FearClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.TiredClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.BoredClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.DisappointedClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.ComfortClosers);
            if (hadClose == null) hadClose = PeelSuffix(s, cfg.CharmClosers);
            Tr("004 peelSuffix(close=" + (hadClose != null) + ") in", s);
            if (hadClose != null) s = s.Substring(0, s.Length - hadClose.Length);
            Tr("005 afterTruncateCloser", s);

            // Take back any decoration the user has since typed PAST.
            //
            // The "already decorated?" check looks at the END of a sentence, so it cannot see
            // a decoration that is now in the middle. Appending one character used to be
            // enough to lose that protection entirely:
            //     你好喵~   +   1   ->   你好喵~1   ->   你好喵~1喵~   ->  你好喵~1喵~2喵~ ...
            // Every pass treated its own earlier output as the user's words and bolted on
            // another kaomoji. Nothing after a token means it is still the tail, so it is
            // left alone - that is what keeps a plain re-run idempotent.
            s = StripStaleDecorations(s, DecorationTokens(cfg));
            Tr("006 stripDecorations#2", s);

            // ★ 只有【真用得上】时才去读聊天上下文 ✧ 不然白费时间 ✓
            //   （ChatCtx 里面还有 1.5 秒缓存 ✧ 连续打字不会重复走 UIA ✓）
            string ctxNow = "";
            try
            {
                if (PreviewMode) ctxNow = "";                 // ★ 预览不读上下文 ✗ 不走 UIA ✓
            else if (CtxOverride != null) ctxNow = CtxOverride;
                else if (cfg.UseContext || NeedsCtx(cfg.Rules))
                    ctxNow = ChatCtx.Recent(cfg.ContextLines);
            }
            catch { ctxNow = ""; }
            s = ApplyRules(s, cfg.Rules, ctxNow);
            Tr("007 applyRules", s);
            // Sentence type is read from the text WITHOUT re-decorating it, so the closing
            // punctuation is still the last character that matters.
            string plain = s;

            s = DecorateAll(s, cfg, rnd);
            Tr("008 decorateAll", s);

            // Keep whatever was already there instead of drawing again. Re-picking would
            // change the message on every trigger, and the auto mode pastes whenever the
            // result differs - so the text would churn under the user for no reason.
            //
            // With 语气 off the wrapper is STRIPPED (the peel above still ran) but never
            // re-added. Stripping matters: switching the mode while the box already holds a
            // wrapped message would otherwise decorate on top of the old closing line and
            // stack a second kaomoji.
            // 内容是纯网址（或整段都是受保护内容）时，装饰那一步已经跳过了，这里也必须跳过 ——
            //
            // 否则第一遍只加语气包装、不加装饰；第二遍因为文本"已经不只是网址了"，装饰又加上了，
            // 两次结果不同。自检里那条 NOT IDEMPOTENT 就是它：
            //   输入  https://example.com/a?b=1&c=2
            //   一遍  别、别自作多情了！https://…，……其实我，也不是不想听啦
            //   两遍  别、别自作多情了！https://…喵！(｀・ω・´)，……其实我，也不是不想听啦
            // ★★★ 这里要和装饰那一步用同一个判断 ★★★
            //   不然 "123" 第一遍只装饰、第二遍又补包装 ✧ 不幂等 ✓
            //   （装饰那边已经改用 HasTypableContent 了 ✓）
            bool wrappable = HasTypableContent(plain);
            // ★ 2026-11-01：wrap 模式下开头结尾由模型写 ✗ 规则先不加 ✓
            bool wrapMode = cfg.RewriteMode == "wrap" && !PreviewMode;
            string opener = ((!cfg.Tone || wrapMode || !wrappable)) ? "" : (hadOpen != null ? hadOpen : Pick(cfg.Openers, rnd, "opener"));
            // ★ 2026-11-01：负面情绪不加可爱开头 ✗
            //   开头池只有一份（「笨蛋，」「别、别误会，」）✗
            //   骂人话前面挂一个这个就很怪 ✓
            string moodNow = Mood(plain);
            if (opener.Length > 0 && (moodNow == "angry" || moodNow == "sad" || moodNow == "disappointed")) opener = "";
            string closer = ((!cfg.Tone || wrapMode || !wrappable)) ? "" : (hadClose != null ? hadClose : PickCloser(plain, cfg, rnd));

            // ★★★ 2026-10-22：外观 + 语气叠加时的出输规则 ★★★
            //   用户报的：两个一起开 ✗ 一句话变三段（开头+正文+后缀+颜文字+结尾）✗
            //   “你好”两个字 → 30 字 ✗ 结尾台词还是按“回应别人”写的 ✗ 贴上去就像硬接的第二句 ✓
            //
            // 分工：外观管“形”（后缀+颜文字）✗ 语气管“神”（开头/结尾）✓
            //   句尾只能有一个负责人 ✗ 所以按长度分工 ✓
            //     短句 / 中等（≤ 15 字）✗ 保留开头 ✗ 不加结尾 ✓
            //     长句（> 15 字）  ✗ 不加开头 ✗ 保留结尾 ✓
            //   度 = 重（逐句）时不限制 ✗ 用户要就是那个效果 ✓
            //   只对“这一轮新挑的”生效 ✗ 上一轮带着的原样接回 ✓→保证幂等
            bool lookTailed = s.Length > plain.Length;   // \u88c5饰那一步确实加了东西
            if (!cfg.CommaDecorate)
            {
                // ★ 2026-11-01：负面情绪破例加结尾 ✗ 不然生气池的台词永远出不来 ✓
            bool negMood = Mood(plain) == "angry" || Mood(plain) == "sad" || Mood(plain) == "disappointed";
            if (closer.Length > 0 && hadClose == null && lookTailed && plain.Length <= 15 && !negMood)
                    closer = "";
                if (opener.Length > 0 && hadOpen == null && plain.Length > 15)
                    opener = "";
            }

            // \u2605 2026-10-22\uff1a\u5f00\u5934\u6ca1\u5e26\u6807\u70b9\u5c31\u8865\u4e00\u4e2a\u9017\u53f7 \u2717
            //   \u6c60\u5b50\u91cc\u6709\u300c\u2026\u2026\u597d\u300d\u8fd9\u79cd \u2717 \u63a5\u4e0a\u6b63\u6587\u5c31\u53d8\u300c\u2026\u2026\u597d\u4f60\u597d\u300d\u2713
            if (opener.Length > 0)
            {
                string op = opener.TrimEnd();
                if (op.Length > 0)
                {
                    char lastCh = op[op.Length - 1];
                    if ("\uFF0C,\u3001\u2026\uFF5E~\uFF01!\uFF1F?\u3002\uFF1A:\uFF1B; ".IndexOf(lastCh) < 0)
                        op += "\uFF0C";
                }
                opener = op;
                s = opener + s;
            }
            if (closer.Length > 0) s = TidyJoin(s, closer);

            // ★ 收尾清理：重复标点、多余全角空格 ✓
            s = TidyText(s);
            // 双喵：正文已经以「喵」结尾，后缀又是「喵~」—— 合并成一个
            if (cfg.Suffix.Length >= 2 && cfg.Suffix[0] == '\u55B5')
            {
                string dup = "\u55B5" + cfg.Suffix;
                if (s.Contains(dup)) s = s.Replace(dup, cfg.Suffix);
            }

            // \u2605\u2605\u2605 2026-10-31\uff1a\u5927\u6a21\u578b\u6539\u5199 \u2605\u2605\u2605
            //   \u9884\u89c8\u91cc\u4e0d\u8dd1\uff08\u4f1a\u5361\u754c\u9762\uff09\u2717 \u5931\u8d25\u5c31\u7528\u89c4\u5219\u7ed3\u679c \u2713
            if (!PreviewMode && Rewriter != null && cfg.RewriteMode != "off" && !string.IsNullOrEmpty(plain))
            {
                try
                {
                    string api = Rewriter(plain, s);
                    if (!string.IsNullOrEmpty(api)) s = api;
                }
                catch { }
            }
            return s;
        }

        /// \u2605 2026-10-22\uff1a\u63a5\u7ed3\u5c3e\u53f0\u8bcd\u65f6\u5148\u628a\u6807\u70b9\u529e\u597d \u2713
        ///   \u539f\u6765\u76f4\u63a5 s + closer \u2717 \u4e24\u8fb9\u90fd\u5e26\u6807\u70b9\u5c31\u4f1a\u53d8\u6210
        ///   \u300c...\u4e86\uff0c\u3002\u2026\u300d\u8fd9\u79cd\u4e1c\u897f \u2717 \u4e5f\u4f1a\u591a\u51fa\u5168\u89d2\u7a7a\u683c \u2713
        static string TidyJoin(string s, string closer)
        {
            if (string.IsNullOrEmpty(closer)) return s;
            string left = s.TrimEnd();
            string right = closer.TrimStart();
            // \u5de6\u8fb9\u5df2\u7ecf\u662f\u53e5\u53f7 / \u95ee\u53f7 / \u611f\u53f9\u53f7 \u2717 \u53f3\u8fb9\u53c8\u4ee5\u9017\u53f7\u5f00\u5934 \u2717 \u53bb\u6389\u9017\u53f7
            if (left.Length > 0 && "\u3002\uff01\uff1f\uff5e".IndexOf(left[left.Length - 1]) >= 0)
                right = right.TrimStart('\uff0c', '\u3001', ' ');
            // \u5de6\u8fb9\u5c3e\u5df2\u7ecf\u6709\u9017\u53f7 \u2717 \u53f3\u8fb9\u518d\u6765\u4e00\u4e2a\u5c31\u91cd\u4e86
            if (left.Length > 0 && "\uff0c\u3001".IndexOf(left[left.Length - 1]) >= 0)
                right = right.TrimStart('\uff0c', '\u3001');
            return left + right;
        }

        /// \u2605 2026-10-22\uff1a\u6536\u5c3e\u6e05\u7406 \u2717 \u91cd\u590d\u6807\u70b9 / \u591a\u4f59\u5168\u89d2\u7a7a\u683c \u2713
        static string TidyText(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string t = s;
            t = t.Replace("\u3002\u3002", "\u3002").Replace("\uff0c\uff0c", "\uff0c")
                 .Replace("\uff01\uff01", "\uff01").Replace("\uff1f\uff1f", "\uff1f")
                 .Replace("\uff5e\uff5e", "\uff5e").Replace("\u3001\u3001", "\u3001")
                 .Replace("\u3000\u3000", "\u3000").Replace("\u3000\uff0c", "\uff0c")
                 .Replace("\uff0c\u3000", "\uff0c").Replace("\u3000\u3002", "\u3002");
            return t.Trim();
        }

        public static string MoodName(string m)
        {
            if (m == "happy") return "\u5F00\u5FC3";
            if (m == "sad") return "\u96BE\u8FC7";
            if (m == "angry") return "\u751F\u6C14";
            if (m == "question") return "\u7591\u95EE";
            if (m == "exclaim") return "\u611F\u53F9";
            if (m == "greeting") return "\u6253\u62DB\u547C";
            if (m == "farewell") return "\u5206\u522B";
            if (m == "love") return "\u559C\u7231";
            if (m == "praise") return "\u5938\u5956";
            if (m == "thanks") return "\u611F\u8C22";
            if (m == "apology") return "\u9053\u6B49";
            if (m == "congrat") return "\u795D\u8D3A";
            if (m == "invite") return "\u9080\u8BF7";
            if (m == "surprise") return "\u60CA\u8BB6";
            if (m == "fear") return "\u5BB3\u6015";
            if (m == "tired") return "\u75B2\u60EB";
            if (m == "bored") return "\u65E0\u804A";
            if (m == "disappointed") return "\u5931\u671B";
            if (m == "comfort") return "\u5B89\u6170";
            if (m == "charm") return "\u6492\u5A07";
            return "\u666E\u901A";
        }

        static string Pick(List<string> pool, Random rnd)
        {
            if (pool == null || pool.Count == 0) return "";
            return pool[rnd.Next(pool.Count)];
        }

        static string PeelPrefix(string s, List<string> pool)
        {
            foreach (string o in pool)
                if (o.Length > 0 && s.StartsWith(o, StringComparison.Ordinal)) return o;
            return null;
        }

        static string PeelSuffix(string s, List<string> pool)
        {
            foreach (string o in pool)
                if (o.Length > 0 && s.EndsWith(o, StringComparison.Ordinal)) return o;
            return null;
        }

        // ---- 语境判断：关键字，不是模型 ----
        //
        // Deliberately dumb, and deliberately honest about it. Nothing is downloaded and
        // nothing is sent anywhere; it just looks for question words and mood words. It
        // WILL be wrong sometimes ("这么好看" is read as a question because of "么"), and
        // no amount of word-list tuning fixes that - only a real language model would,
        // which is a ~2GB download plus a runtime.
        static readonly string[] QuestionWords =
        {
            "\u4EC0\u4E48", "\u600E\u4E48", "\u4E3A\u4EC0\u4E48", "\u4E3A\u5565",
            "\u54EA", "\u662F\u5426", "\u80FD\u4E0D\u80FD", "\u53EF\u4E0D\u53EF\u4EE5",
            "\u597D\u4E0D\u597D", "\u6709\u6CA1\u6709", "\u591A\u5C11", "\u8C01",
            "\u5417", "\u5462"
        };
        static readonly string[] SadWords =
        {
            "\u4E0D\u5F00\u5FC3", "\u96BE\u8FC7", "\u4F24\u5FC3", "\u59D4\u5C48",
            "\u60F3\u54ED", "\u5931\u843D", "\u96BE\u53D7", "\u90C1\u95F7",
            "\u75DB\u82E6", "\u5D29\u6E83", "\u54C0"
        };
        static readonly string[] AngryWords =
        {
            "生气", "气死", "烦死", "好烦",
            "讨厌", "无语", "过分", "气人",
            "烦躁", "恼火",
            // ★ 2026-10-31：骂人话也是生气 ✗
            //   之前「滚啊」「草泥马」全部落到 plain ✗
            //   然后小模型猜成「感谢」✗ 输出变成「嗯……？滚啊喵~(•̀∀•́ )」✓
            "滚", "滚啊", "滚蛋", "滚开", "滚出去",
            "草泥马", "草泥马·", "卧槽", "我操", "干你",
            "妈的", "他妈", "你妈", "傻逼", "傻B", "SB", "sb",
            "闭嘴", "去死", "有病", "恶心", "狗屎",
            "废物", "垃圾", "别烦", "没完没了"
        };
        static readonly string[] HappyWords =
        {
            "\u54C8\u54C8", "\u563F\u563F", "\u563B\u563B", "\u5F00\u5FC3",
            "\u9AD8\u5174", "\u592A\u597D\u4E86", "\u597D\u8036", "\u597D\u68D2",
            "\u8D5E", "\u559C\u6B22", "\u53EF\u7231", "\u597D\u73A9",
            "\u5389\u5BB3", "\u8C22\u8C22", "\u8212\u670D", "\u5E78\u798F"
        };

        // ---- 社交类：打招呼 / 分别 / 喜爱 / 夸奖 ----
        // These four overlap with the emotional lists above, so the ORDER in Mood() matters
        // more than the words themselves. 你好棒 contains 你好; 我喜欢你 contains 喜欢, which
        // is also a happy word; 再见 is not a farewell signal when it appears inside 再也不.
        static readonly string[] GreetingWords =
        {
            "\u4F60\u597D", "\u60A8\u597D", "\u65E9\u4E0A\u597D", "\u4E2D\u5348\u597D",
            "\u665A\u4E0A\u597D", "\u65E9\u5B89", "\u54C8\u55BD", "\u55E8",
            "\u5728\u5417", "\u5728\u4E0D\u5728", "\u5728\u4E48", "\u4F60\u5728\u5417",
            "\u4E45\u7B49\u4E86", "\u6211\u6765\u4E86", "\u65E9\u554A"
        };
        static readonly string[] FarewellWords =
        {
            "\u518D\u89C1", "\u62DC\u62DC", "\u665A\u5B89", "\u5148\u8D70\u4E86",
            "\u6211\u8D70\u4E86", "\u4E0B\u7EBF\u4E86", "\u6211\u64A4\u4E86",
            "\u4E0B\u6B21\u804A", "\u6539\u5929\u804A", "\u5148\u7761\u4E86",
            "\u6211\u7761\u4E86", "\u6D17\u6D17\u7761\u4E86", "\u4E0B\u4E2A\u4E86"
        };
        static readonly string[] LoveWords =
        {
            "\u559C\u6B22\u4F60", "\u7231\u4F60", "\u60F3\u4F60", "\u60F3\u89C1\u4F60",
            "\u62B1\u62B1", "\u4EB2\u4EB2", "\u4E48\u4E48", "\u5FC3\u52A8",
            "\u820D\u4E0D\u5F97\u4F60", "\u60F3\u62B1", "\u597D\u60F3\u4F60"
        };
        static readonly string[] PraiseWords =
        {
            "\u597D\u68D2", "\u592A\u68D2", "\u5389\u5BB3", "\u592A\u5F3A",
            "\u4F18\u79C0", "\u505A\u5F97\u597D", "\u5E72\u5F97\u597D",
            "\u68D2\u6781\u4E86", "\u4E86\u4E0D\u8D77", "\u7EDD\u4E86",
            "\u4F69\u670D", "\u5B66\u5230\u4E86", "\u771F\u884C", "\u597D\u725B",
            "\u725B\u903C", "\u6709\u4E24\u4E0B\u5B50", "\u5927\u4F6C"
        };
        static readonly string[] ThanksWords =
        {
            "\u8C22\u8C22", "\u591A\u8C22", "\u611F\u8C22", "\u8F9B\u82E6\u4E86",
            "\u9EBB\u70E6\u4F60\u4E86", "\u8C22\u5566", "\u9EBB\u70E6\u4E86", "\u8F9B\u82E6\u5566"
        };
        static readonly string[] ApologyWords =
        {
            "\u5BF9\u4E0D\u8D77", "\u62B1\u6B49", "\u4E0D\u597D\u610F\u601D",
            "\u6211\u9519\u4E86", "\u89C1\u8C05", "\u539F\u8C05\u6211", "\u662F\u6211\u4E0D\u597D"
        };
        static readonly string[] CongratWords =
        {
            "\u606D\u559C", "\u795D\u8D3A", "\u751F\u65E5\u5FEB\u4E50", "\u65B0\u5E74\u5FEB\u4E50",
            "\u8282\u65E5\u5FEB\u4E50", "\u4E07\u4E8B\u5982\u610F", "\u8003\u8BD5\u987A\u5229",
            "\u795D\u4F60", "\u5927\u559C"
        };
        static readonly string[] InviteWords =
        {
            "\u4E00\u8D77", "\u966A\u6211", "\u7EA6\u5417", "\u8981\u4E0D\u8981\u4E00\u8D77",
            "\u8D70\u5427", "\u6765\u4E0D\u6765", "\u6709\u7A7A\u5417", "\u51FA\u6765\u73A9"
        };
        static readonly string[] SurpriseWords =
        {
            "\u54C7", "\u5929\u5450", "\u5929\u54EA", "\u5C45\u7136", "\u7ADF\u7136",
            "\u4E0D\u4F1A\u5427", "\u771F\u7684\u5047\u7684", "\u60CA\u4E86", "\u6211\u7684\u5929",
            "\u5413\u6B7B"
        };
        static readonly string[] FearWords =
        {
            "\u597D\u6015", "\u5BB3\u6015", "\u6050\u6016", "\u62C5\u5FC3", "\u7D27\u5F20",
            "\u4E0D\u5B89", "\u614C", "\u5F88\u96BE\u529E"
        };
        static readonly string[] TiredWords =
        {
            "\u597D\u7D2F", "\u7D2F\u6B7B", "\u597D\u56F0", "\u56F0\u6B7B", "\u60F3\u7761",
            "\u6491\u4E0D\u4F4F", "\u75B2\u60EB", "\u6CA1\u529B\u6C14", "\u71AC\u591C",
            "\u52A0\u73ED\u5230\u5F88\u665A"
        };
        static readonly string[] BoredWords =
        {
            "\u597D\u65E0\u804A", "\u65E0\u804A\u6B7B", "\u6CA1\u4E8B\u5E72",
            "\u6CA1\u610F\u601D", "\u597D\u95F2", "\u5F85\u7740\u6CA1\u4E8B"
        };
        static readonly string[] DisappointedWords =
        {
            "\u5931\u671B", "\u767D\u9AD8\u5174", "\u767D\u671F\u5F85", "\u7A7A\u6B22\u559C",
            "\u679C\u7136\u4E0D\u884C", "\u7B97\u4E86\u5427", "\u6CA1\u7528\u4E86"
        };
        /// The user is comforting the other person. These are checked BEFORE 难过, because
        /// "别难过" contains "难过" and would otherwise be read as the user being sad.
        static readonly string[] ComfortWords =
        {
            "\u522B\u96BE\u8FC7", "\u4E0D\u8981\u96BE\u8FC7", "\u522B\u54ED", "\u522B\u4F24\u5FC3",
            "\u6CA1\u4E8B\u7684", "\u4F1A\u597D\u7684", "\u62B1\u62B1\u4F60", "\u522B\u60F3\u592A\u591A",
            "\u5F00\u5FC3\u70B9", "\u6211\u966A\u4F60", "\u6709\u6211\u5728", "\u522B\u6015\u6709\u6211",
            "\u522B\u6015", "\u6211\u5728\u5462"
        };
        /// The user is ASKING for affection - "抱抱我", not "我喜欢你". Checked before 喜爱
        /// because "抱抱我" contains "抱抱".
        static readonly string[] CharmWords =
        {
            // \u2605 \u7528\u6237 2026-10-17\uff1a\u300c\u4EB2\u6211\u561B\u300d\u5224\u6210\u4E86\u666E\u901A \u2717
            //   \u539F\u6765\u53EA\u6709 8 \u4E2A\u8BCD \u2727 \u300C\u4EB2\u4EB2\u6211\u300D\u5339\u4E0D\u4E0A\u300C\u4EB2\u6211\u561B\u300D\u2717
            //   \u2014\u2014 \u8865\u5230 30 \u4E2A \u2727 \u628A\u5E38\u7528\u7684\u6492\u5A07\u53E5\u5F0F\u90FD\u6536\u8FDB\u6765 \u2713
            "\u62B1\u62B1\u6211", "\u4EB2\u4EB2\u6211", "\u966A\u966A\u6211", "\u54C4\u6211",
            "\u6478\u6478\u5934", "\u8981\u62B1\u62B1", "\u62B1\u7D27\u6211", "\u60F3\u62B1\u62B1",
            "\u62B1\u62B1", "\u62B1\u4E00\u4E0B", "\u62B1\u6211", "\u8BA9\u6211\u62B1",
            "\u4EB2\u6211", "\u4EB2\u4E00\u4E0B", "\u4EB2\u4EB2", "\u8981\u4EB2\u4EB2",
            "\u60F3\u4EB2\u4EB2", "\u6478\u6478\u6211", "\u6478\u6478", "\u8E6D\u8E6D",
            "\u54C4\u54C4\u6211", "\u966A\u6211", "\u597D\u4E0D\u597D\u561B", "\u6C42\u4F60\u4E86",
            "\u4EBA\u5BB6", "\u8BA8\u538C\u5566", "\u624D\u4E0D\u8981", "\u4E0D\u561B",
            "\u8DDF\u6211\u73A9", "\u5E26\u6211"
        };

        static bool ContainsAny(string s, string[] words)
        {
            for (int i = 0; i < words.Length; i++)
                if (s.IndexOf(words[i], StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// Order is load-bearing - a message often contains words from several lists:
        ///   1. 夸奖 before all            - "你好棒" contains "你好"
        ///   2. 安慰 before 喜爱 / 难过     - "抱抱你" has 抱抱, "别难过" has 难过, but in both
        ///                                 the USER is the comforter
        ///   3. 感谢/道歉/祝贺 before 喜爱  - they are more specific social acts
        ///   4. 喜爱 before 开心            - "喜欢你" contains "喜欢", also a happy word
        ///   5. 打招呼 before 疑问          - "你好吗" / "在吗" are greetings
        ///   6. 邀请 before 疑问            - "要不要一起" is an invitation
        ///   7. 惊讶/害怕/疲惫/无聊/失望 before 开心 - all sit near a mood word
        ///   8. 难过/生气 before 开心       - "不开心" contains "开心"
        public static string Mood(string s)
        {
            if (string.IsNullOrEmpty(s)) return "plain";
            if (ContainsAny(s, PraiseWords)) return "praise";
            if (ContainsAny(s, ThanksWords)) return "thanks";
            if (ContainsAny(s, ApologyWords)) return "apology";
            if (ContainsAny(s, CongratWords)) return "congrat";
            // 安慰 sits BEFORE 喜爱 and BEFORE 难过: "抱抱你" contains "抱抱" (a 喜爱 word)
            // and "别难过" contains "难过". Both have to be read as the USER comforting
            // someone, not as the user being in love or being sad.
            if (ContainsAny(s, ComfortWords)) return "comfort";
            if (ContainsAny(s, CharmWords)) return "charm";
            if (ContainsAny(s, LoveWords)) return "love";
            if (ContainsAny(s, FarewellWords)) return "farewell";
            if (ContainsAny(s, GreetingWords)) return "greeting";
            if (ContainsAny(s, InviteWords)) return "invite";
            if (ContainsAny(s, SurpriseWords)) return "surprise";
            if (ContainsAny(s, FearWords)) return "fear";
            if (ContainsAny(s, TiredWords)) return "tired";
            if (ContainsAny(s, BoredWords)) return "bored";
            if (ContainsAny(s, DisappointedWords)) return "disappointed";
            if (ContainsAny(s, QuestionWords)) return "question";
            if (ContainsAny(s, SadWords)) return "sad";
            if (ContainsAny(s, AngryWords)) return "angry";
            if (ContainsAny(s, HappyWords)) return "happy";

            int end = s.Length;
            while (end > 0 && char.IsWhiteSpace(s[end - 1])) end--;
            if (end > 0 && (s[end - 1] == '\uFF01' || s[end - 1] == '!')) return "exclaim";
            return "plain";
        }

        /// The pools the model is NOT allowed to overrule. All of them are triggered by
        /// concrete words, and the 3B model answered 疑问 for 在吗 and 普通 for 抱抱 when it
        /// was asked about them - worse than plain word matching.
        /// ★ 2026-10-31：强生气词（脏话）✗ 小模型不准把它改成正面情绪 ✓
        static readonly string[] StrongAngryWords =
        {
            "滚啊", "滚蛋", "滚开", "滚出去",
            "草泥马", "卧槽", "我操", "干你",
            "妈的", "你妈", "傻逼", "SB", "sb",
            "闭嘴", "去死", "有病", "狗屄", "废物"
        };

        public static bool LooksAngry(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            return ContainsAny(s, StrongAngryWords);
        }

        public static bool IsPositiveMood(string m)
        {
            return m == "thanks" || m == "praise" || m == "love" || m == "happy"
                || m == "comfort" || m == "greeting" || m == "charm" || m == "congrat";
        }

        public static bool IsSocialMood(string m)
        {
            return m == "greeting" || m == "farewell" || m == "love" || m == "praise"
                || m == "thanks" || m == "apology" || m == "congrat" || m == "invite"
                || m == "surprise" || m == "fear" || m == "tired" || m == "bored"
                || m == "disappointed" || m == "comfort" || m == "charm";
        }

        /// Which pool the closing line comes from. A style only has to fill in the pools
        /// it cares about; anything missing falls back to the plain one. With 智能语气 on,
        /// a local model gets first say - and if it is not running, the keyword classifier
        /// answers instead, so nothing ever breaks.
        /// Set on every tone decision so the UI and the log can show WHERE the mood came
        /// from. Without this the user has no way to tell whether the local model is
        /// actually being used or whether it silently fell back to keywords.
        public static string LastMoodInfo = "";

        /// Which candidate was used last, per pool. Landing the same line twice in a row is
        /// what makes the whole thing feel mechanical, so a repeat is avoided when the pool
        /// has something else to offer.
        static readonly Dictionary<string, string> LastPicked = new Dictionary<string, string>();

        static string Pick(List<string> pool, Random rnd, string key)
        {
            if (pool == null || pool.Count == 0) return "";
            if (pool.Count == 1) return pool[0];

            string last = null;
            LastPicked.TryGetValue(key, out last);
            for (int i = 0; i < 8; i++)
            {
                string v = pool[rnd.Next(pool.Count)];
                if (v != last) { LastPicked[key] = v; return v; }
            }
            string f = pool[rnd.Next(pool.Count)];
            LastPicked[key] = f;
            return f;
        }

        static string PickCloser(string plain, Config cfg, Random rnd)
        {
            // Keywords decide first. The four social moods are triggered by unambiguous words
            // (你好 / 再见 / 我喜欢你 / 你好棒) and the model is NOT asked about them: given the
            // longer nine-word prompt it answered 疑问 for 在吗 and 普通 for 抱抱, i.e. it was
            // worse than plain word matching. It is only asked to refine the emotional moods,
            // which is exactly where it beats keywords (it reads 我不难过 correctly).
            string mood = Mood(plain);
            string via = "\u5173\u952E\u5B57";
            bool social = IsSocialMood(mood);

            if (cfg.SmartTone && !social && !PreviewMode)
            {
                // \u2605 \u5f00\u4e86\u300c\u8bfb\u4e0a\u4e0b\u6587\u300d\u5c31\u5148\u628a\u6700\u8fd1\u51e0\u6761\u804a\u5929\u8bfb\u51fa\u6765 \u2713
                //   \u8bfb\u4e0d\u5230\uff08\u6ca1\u5f00 QQ/\u5fae\u4fe1 \u2727 \u7ed3\u6784\u53d8\u4e86 \u2727 UIA \u6ca1\u54cd\u5e94\uff09\u5c31\u662f\u7a7a\u4e32 \u2727
                //   \u884c\u4e3a\u5b8c\u5168\u540c\u4e8e\u4ee5\u524d \u2713
                string ctx = "";
                if (cfg.UseContext)
                {
                    try { ctx = ChatCtx.Recent(cfg.ContextLines); }
                    catch { ctx = ""; }
                }
                string m2 = Llm.Classify(plain, ctx, cfg.LlmEndpoint, cfg.LlmModel, cfg.LlmTimeoutMs);
                if (m2 != null)
                {
                    mood = m2;
                    via = ctx.Length > 0 ? "\u6A21\u578B\uFF08\u5E26\u4E0A\u4E0B\u6587\uFF09" : "\u6A21\u578B";
                }
                else via = "\u5173\u952E\u5B57\uFF08\u6A21\u578B\u6CA1\u5E94\u7B54\uFF09";
            }
            else if (cfg.SmartTone)
            {
                via = "\u5173\u952E\u5B57\uFF08\u793E\u4EA4\u7C7B\uFF09";
            }

            // ★★★ 语气小模型（clf.bin ✧ 7.3 MB）★★★
            //
            // 用户 2026-10-17：安卓那个小模型电脑上也要有 ✓
            //
            // 为什么放在这（开关之后、选词池之前）：
            //   LLM 得 8/10 ✧ 小模型也是 8/10 但只有 7.3 MB ✓
            //   ★ 有 LLM 就不抢它的活 ✧ 没 LLM（无模型版）就靠它 ✓✓✓
            //   关键词已经判出东西的时候不问它 ✧ 跟安卓一致 ✓
            // ★★★ 用户 2026-10-17：「把关键词挑到最后 ✧ 模型准一点」★★★
            //
            // 但不能直接让它无条件优先 ✧ 小模型只能表达 7 种：
            //   安慰 / 感谢 / 分别 / 开心 / 疑问 / 普通 / 生气 ✓
            // 它说不出【难过 / 害怕 / 累了 / 失望 / 喜爱 / 夸奖】✗
            //   —— ★ 无条件覆盖的话：
            //      「我很难过」关键词判对了 ✧ 小模型只能说安慰/普通
            //      反而把对的覆盖成错的 ✗
            //
            // 所以：只有当关键词给出的情绪【小模型也能表达】时才问它 ✓
            //       而且只在它给出一个【不是 plain、也不同】的答案时才覆盖 ✓
            // \u2605 13 \u7c7b\u7248\u4e4b\u540e\uff1a\u5c0f\u6a21\u578b\u80fd\u8868\u8fbe\u7684\u6c60\u4ece 7 \u4e2a\u53d8\u6210 13 \u4e2a \u2713
            bool tinyCanSay = mood == "plain" || mood == "comfort" || mood == "thanks"
                           || mood == "farewell" || mood == "happy" || mood == "question"
                           || mood == "angry" || mood == "sad" || mood == "tired"
                           || mood == "love" || mood == "praise" || mood == "fear"
                           || mood == "disappointed";
            if (cfg.TinyMood && tinyCanSay && !PreviewMode)   // ★ 预览不跑小模型 ✓
            {
                try
                {
                    string ctx2 = "";
                    if (cfg.UseContext)
                    {
                        try { ctx2 = ChatCtx.Recent(cfg.ContextLines); }
                        catch { ctx2 = ""; }
                    }
                    string tl = TinyClf.Classify(ctx2, plain);
                    if (tl != null)
                    {
                        string nm = TinyClf.ToMood(tl);
                        // ★ plain 不能把已经判出的情绪降级 ✓
                        // ★ 2026-10-31：明显是骂人的话 ✗ 不允许小模型改成感谢/夸奖/开心 ✓
                        // ★ 2026-11-01：脏话只允许「生气」✗
                        //   上一版只拦正面情绪 ✗ 小模型改说「疑问」就漏过去了 ✓
                        bool veto = LooksAngry(plain) && nm != "angry";
                        if (veto)
                        {
                            App.LogStatic("小模型判断：「" + plain
                                          + "」→ " + tl + "（骂人话，拦住不采用）");
                        }
                        if (nm != "plain" && nm != mood && !veto)
                        {
                            App.LogStatic("小模型判断：「"
                                          + (plain.Length > 24 ? plain.Substring(0, 24) + "…" : plain)
                                          + "」→ " + tl + "（原来是 " + mood
                                          + "，上文 " + ctx2.Length + " 字）");
                            mood = nm;
                            via = "小模型";
                        }
                    }
                }
                catch { }
            }


            Engine.CurrentMood = mood;   // ★ 给大模型写包装用 ✓
            List<string> pool = null;
            if (mood == "praise" && cfg.PraiseClosers.Count > 0) pool = cfg.PraiseClosers;
            else if (mood == "thanks" && cfg.ThanksClosers.Count > 0) pool = cfg.ThanksClosers;
            else if (mood == "apology" && cfg.ApologyClosers.Count > 0) pool = cfg.ApologyClosers;
            else if (mood == "congrat" && cfg.CongratClosers.Count > 0) pool = cfg.CongratClosers;
            else if (mood == "charm" && cfg.CharmClosers.Count > 0) pool = cfg.CharmClosers;
            else if (mood == "love" && cfg.LoveClosers.Count > 0) pool = cfg.LoveClosers;
            else if (mood == "farewell" && cfg.FarewellClosers.Count > 0) pool = cfg.FarewellClosers;
            else if (mood == "greeting" && cfg.GreetingClosers.Count > 0) pool = cfg.GreetingClosers;
            else if (mood == "invite" && cfg.InviteClosers.Count > 0) pool = cfg.InviteClosers;
            else if (mood == "surprise" && cfg.SurpriseClosers.Count > 0) pool = cfg.SurpriseClosers;
            else if (mood == "fear" && cfg.FearClosers.Count > 0) pool = cfg.FearClosers;
            else if (mood == "tired" && cfg.TiredClosers.Count > 0) pool = cfg.TiredClosers;
            else if (mood == "bored" && cfg.BoredClosers.Count > 0) pool = cfg.BoredClosers;
            else if (mood == "disappointed" && cfg.DisappointedClosers.Count > 0) pool = cfg.DisappointedClosers;
            else if (mood == "comfort" && cfg.ComfortClosers.Count > 0) pool = cfg.ComfortClosers;
            else if (mood == "question" && cfg.QuestionClosers.Count > 0) pool = cfg.QuestionClosers;
            else if (mood == "sad" && cfg.SadClosers.Count > 0) pool = cfg.SadClosers;
            else if (mood == "angry" && cfg.AngryClosers.Count > 0) pool = cfg.AngryClosers;
            else if (mood == "happy" && cfg.HappyClosers.Count > 0) pool = cfg.HappyClosers;
            else if (mood == "exclaim" && cfg.ExclaimClosers.Count > 0) pool = cfg.ExclaimClosers;
            if (pool == null) pool = cfg.Closers;

            // NOTE: I tried letting the model CHOOSE one of these candidates instead of
            // rolling dice. With a 3B model it does not work - asked to pick the most
            // fitting of 8 lines it answered "，别、别误会啊！" for 5 of 6 sentences, and
            // with a more detailed prompt it collapsed onto a single answer for all 6.
            // That is strictly worse than random, so the choice stays random (with the
            // no-repeat rule above) and the model is only trusted with the mood, which it
            // gets right. Llm.ChooseIndex is kept for a larger model.
            LastMoodInfo = MoodName(mood) + " \u00B7 \u6765\u6E90\uFF1A" + via;
            return Pick(pool, rnd, "closer");
        }

        static string DecorateAll(string s, Config cfg, Random rnd)
        {
            if (cfg.Kaomoji.Count == 0 && cfg.Suffix.Length == 0) return s;

            List<string> decorations = BuildDecorations(cfg);
            if (cfg.EachSentence) return PerSentence(s, cfg, rnd, decorations);

            int end = s.Length;
            while (end > 0 && char.IsWhiteSpace(s[end - 1])) end--;
            string body = s.Substring(0, end);
            string tail = s.Substring(end);
            if (body.Length == 0 || !HasTypableContent(body)) return s;

            // ★ 整段就是后缀本身 ✧ 只补一个颜文字 ✓
            //   ★ 用 cfg.Suffix 而不是 body ✧ 让波浪号一定在 ✓
            //     用户 2026-10-06："单独打喵的时候也加上波浪号"
            //     打「喵」→ 喵~(颜文字)   而不是 喵(颜文字) ✓
            {
                string tildeJ0;
                if (IsAllSuffix(body, cfg, out tildeJ0))
                {
                    string t0 = (tildeJ0.Length > 0 && body.EndsWith(tildeJ0)) ? "" : tildeJ0;
                    return body + t0 + KaoOnly(cfg, rnd) + tail;
                }
            }

            // ★★★ 轻度路径：用户把标点补在颜文字后面 ✧ 挪到前面 ★★★
            //   （这条路径没有 EmitSegment 那个"把末尾标点切下来"的步骤 ✧ 要自己切 ✓）
            {
                string pn;
                string coreBody = SplitTrailingPunct(body, out pn);
                if (pn.Length > 0)
                {
                    string mv = MovePunctInsideDecoration(coreBody, pn, cfg, decorations);
                    if (mv != null) return mv + tail;
                }
            }
            if (EndsWithAny(body, cfg.SkipEnds) || EndsWithAny(body, decorations)) return s;
            // ★ 已经挪好的形状（后缀 + 标点 + 颜文字 ✓）也算已装饰 ✓
            //   不然下一遍又会跟一个新的在后面 ✓
            if (EndsWithPunctInsideDecoration(body, cfg)) return s;

            // ★★★ 末尾是【引擎自己写的收尾句】✧ 也说明已经装饰过了 ★★★
            //
            // 轻度模式下的写法是：body + 装饰 + 收尾句 ✓
            //   而认装饰的函数看的是"后缀后面还有没有散文" ✧
            //   收尾句里当然有散文 ✧ 于是认不出来 ✓
            //   —— 结果：每按一次热键就再加一个 ✗ 越用越长 ✓
            // （实测："你好喵~(´･ω･`)，欸！在的在的" ✓）
            if (EndsWithAny(body, AllCloserLines(cfg))) return s;

            // A PARTIALLY deleted decoration counts as decorated too.
            //
            // Exact matching alone is not enough here: trim one character out of a kaomoji and
            // the tail no longer matches anything, so it reads as ordinary prose and a second
            // decoration goes on behind the broken one -
            //   你好喵~(´･ω･`)  ->  delete ´  ->  你好喵~(･ω･`)  ->  你好喵~(･ω･`)喵~ฅ^•ﻌ•^ฅ
            // The per-sentence path has had this protection for a long time (AlreadyDecorated
            // calls SuffixTailIsDecoration); the 轻度 path never did, which is why the bug
            // only showed up with 程度 = 轻度 and went unnoticed until someone reported it.
            int win = Math.Min(24, body.Length);
            if (SuffixTailIsDecoration(body, body.Length - win, win, cfg)) return s;

            // ★★★ 轻度路径（不逐句）的 body 里【包含句尾标点】★★★
            //
            // 因为这条路径没有 EmitSegment 那个"把标点切下来"的步骤 ✧
            // body 就是整句（含末尾的 。！……？ ✓）✓
            // —— 所以情绪判断要在这里补 ✧ 不然只有逐句模式生效 ✓
            int tEnd = body.Length;
            while (tEnd > 0 && IsPunctLike(body[tEnd - 1])) tEnd--;
            string lastPunct = body.Substring(tEnd);

            // ★★★ 标点放在"后缀"和"颜文字"【之间】★★★
            //
            // 用户 2026-10-06 选的：
            //   你好！ → 你好喵！(=≧∇≦=)
            //   你好。 → 你好喵~。(=≧∇≦=)
            //   你好   → 你好喵(=≧∇≦=)      （没标点 ✧ 不变 ✓）
            // —— 逐句模式（普通/重度）【不动】✧
            //    用户说那边"颜文字放标点前面更好读" ✓
            //      今天不错喵(=∧ω∧=)，我们去吃饭吧喵(≧▽≦)。
            if (lastPunct.Length > 0)
            {
                bool emo2 = IsEmotionalPunct(lastPunct);
                string deco2 = Decorate(cfg, rnd, emo2);
                string coreJ = body.Substring(0, tEnd);

                // ★★★ 剔完标点剩下的就是【喵】本身 ★★★
                //
                //   用户 2026-10-07："同时打喵加问号会输出喵喵？…
                //                  但我想要的是 喵？…"
                //   上一版的特判跑在【切标点之前】✧
                //   所以 "喵？" 整体不等于 "喵" ✧ 没匹配上 ✓
                //   于是变成 "喵" + "喵？(颜文字)" = 喵喵？(颜文字) ✓
                // ★ 整段全是喵 ✧ 保留打的个数 ✧ 补波浪号 + 颜文字 ✧ 再按标点规则摆 ✓
                string tildeJ = "";
                if (coreJ.Length > 0 && IsAllSuffix(coreJ, cfg, out tildeJ))
                {
                    string tJ = (tildeJ.Length > 0 && coreJ.EndsWith(tildeJ)) ? "" : tildeJ;
                    string kaoJ = KaoOnly(cfg, rnd);
                    return PlaceDecoration(coreJ + tJ + kaoJ, kaoJ, lastPunct,
                                           IsEmotionalPunct(lastPunct), cfg.Suffix) + tail;
                }

                return coreJ
                     + PlaceDecoration(deco2, LastKaomoji, lastPunct, emo2, cfg.Suffix) + tail;
            }

            return body + Decorate(cfg, rnd, false) + tail;
        }
    }

    // ---------------------------------------------------------------- native

    static class Native
    {
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_NOREPEAT = 0x4000;

        public const byte VK_CONTROL = 0x11;
        public const byte VK_SHIFT = 0x10;
        public const byte VK_MENU = 0x12;   // Alt
        public const byte VK_LWIN = 0x5B;
        public const byte VK_A = 0x41;
        public const byte VK_C = 0x43;
        public const byte VK_V = 0x56;
        public const byte VK_X = 0x58;   // X —— 用来"剪切"探测框里有没有图片
        public const byte VK_M = 0x4D;
        public const byte VK_P = 0x50;
        public const byte VK_R = 0x52;

        const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        public static uint IdleMilliseconds()
        {
            LASTINPUTINFO lii = new LASTINPUTINFO();
            lii.cbSize = (uint)Marshal.SizeOf(lii);
            if (!GetLastInputInfo(ref lii)) return 0;
            return (uint)Environment.TickCount - lii.dwTime;
        }

        static void KeyDown(byte vk) { keybd_event(vk, 0, 0, IntPtr.Zero); MarkSynthetic(); }
        static void KeyUp(byte vk) { keybd_event(vk, 0, KEYEVENTF_KEYUP, IntPtr.Zero); MarkSynthetic(); }

        public static void SendCtrl(byte vk)
        {
            KeyDown(VK_CONTROL);
            Thread.Sleep(15);
            KeyDown(vk);
            Thread.Sleep(15);
            KeyUp(vk);
            Thread.Sleep(15);
            KeyUp(VK_CONTROL);
        }

        public static bool ParseHotkey(string spec, out uint mods, out uint vk)
        {
            mods = MOD_NOREPEAT;
            vk = 0;
            if (string.IsNullOrEmpty(spec)) return false;
            string[] parts = spec.Split('+');
            foreach (string raw in parts)
            {
                string p = raw.Trim().ToLowerInvariant();
                if (p.Length == 0) continue;
                if (p == "ctrl" || p == "control") { mods |= MOD_CONTROL; continue; }
                if (p == "alt") { mods |= MOD_ALT; continue; }
                if (p == "shift") { mods |= MOD_SHIFT; continue; }
                if (p == "win") { mods |= 0x0008; continue; }
                if (p.Length == 1 && p[0] >= 'a' && p[0] <= 'z') { vk = (uint)char.ToUpperInvariant(p[0]); continue; }
                if (p.Length == 1 && p[0] >= '0' && p[0] <= '9') { vk = (uint)p[0]; continue; }
                if (p.Length >= 2 && p[0] == 'f')
                {
                    int n;
                    if (int.TryParse(p.Substring(1), out n) && n >= 1 && n <= 24) { vk = (uint)(0x70 + n - 1); continue; }
                }
                return false;
            }
            return vk != 0 && (mods & ~MOD_NOREPEAT) != 0;
        }

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        public static bool IsKeyDown(byte vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        public const byte VK_END = 0x23;
        public const byte VK_DELETE = 0x2E;

        // TickCount of the last key WE injected. Our own Ctrl+A / Ctrl+C / End reset
        // the system idle timer exactly like real input does, so without this the tool
        // armed itself from its own keystrokes and looped: fire -> Ctrl+A/C -> abort ->
        // End -> looks like typing -> fire again, forever.
        public static int LastSyntheticInput = 0;
        static void MarkSynthetic() { LastSyntheticInput = Environment.TickCount; }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT p);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
        // ★ Native 类里自己也要一份 —— 另一份在 Dumptree 那个类里，这里看不到。
        //   （同一个 P/Invoke 在两个类各声明一次，是这套单文件写法的老毛病）
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumProc cb, IntPtr l);

        public delegate bool EnumProc(IntPtr h, IntPtr l);

        /// Brings the already-running copy's window up.
        ///
        /// A second instance starts whenever the desktop shortcut is double-clicked while the
        /// tool is sitting in the tray, and the old code answered that with a "it is already
        /// running" dialog - not what double-clicking an icon should do. ShowWindow works
        /// across processes, so no IPC is needed.
        public static bool ActivateExistingWindow()
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                StringBuilder t = new StringBuilder(200);
                GetWindowTextW(h, t, 200);
                string s = t.ToString();
                // the 已拦截 dialogs also start with the product name - never grab those
                if ((s.StartsWith("\u55B5\u5316\u52A9\u624B", StringComparison.Ordinal) ||
                     s.StartsWith("Miaohua", StringComparison.Ordinal)) &&
                    s.IndexOf("\u5DF2\u62E6\u622A", StringComparison.Ordinal) < 0 &&
                    s.IndexOf("\u5B89\u88C5\u5305", StringComparison.Ordinal) < 0)
                {
                    found = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (found == IntPtr.Zero) return false;
            ShowWindow(found, 9);   // SW_RESTORE - also un-hides a window hidden to the tray
            ShowWindow(found, 5);   // SW_SHOW
            SetForegroundWindow(found);
            return true;
        }

        public static bool TryGetCursor(out int x, out int y)
        {
            POINT p;
            if (GetCursorPos(out p)) { x = p.X; y = p.Y; return true; }
            x = 0; y = 0;
            return false;
        }

        // Mouse buttons, via the same "pressed since last poll" bit.
        public static bool MouseButtonPressedSinceLastPoll()
        {
            if ((GetAsyncKeyState(0x01) & 1) != 0) return true;   // left
            if ((GetAsyncKeyState(0x02) & 1) != 0) return true;   // right
            if ((GetAsyncKeyState(0x04) & 1) != 0) return true;   // middle
            return false;
        }

        // Keys that mean "I am editing what is already there" rather than "I am
        // composing new text".
        static readonly byte[] EditKeys = new byte[]
        {
            0x08,                    // backspace
            VK_DELETE,               // delete
            0x25, 0x26, 0x27, 0x28,  // arrows
            0x24, VK_END,            // home / end
            0x21, 0x22               // page up / page down
        };

        // True when any editing key was pressed since the PREVIOUS call. The low bit of
        // GetAsyncKeyState means exactly "pressed since the last query", which is how
        // deleting can be told apart from typing without installing a keyboard hook
        // (a global hook is what keyloggers look like, and would be flagged as one).
        // Must be polled every tick so the flags stay fresh.
        public static bool EditKeyPressedSinceLastPoll()
        {
            bool hit = false;
            for (int i = 0; i < EditKeys.Length; i++)
                if ((GetAsyncKeyState(EditKeys[i]) & 1) != 0) hit = true;
            return hit;
        }

        public static void SendKey(byte vk)
        {
            KeyDown(vk);
            Thread.Sleep(15);
            KeyUp(vk);
        }

        public const byte VK_HOME = 0x24;

        /// \u2605 \u628a"\u6ca1\u6709\u7a97\u53e3\u7684\u81ea\u5df1"\u6740\u6389 \u2727 \u5361\u6b7b\u7684\u50f5\u5c38\u8fdb\u7a0b \u2713
        ///
        /// \u7528\u6237 2026-10-17 \u9047\u5230\u7684\uff1a
        ///   \u7a0b\u5e8f\u5361\u4f4f \u2727 \u5173\u4e0a\u7a97\u53e3\u4f46\u8fdb\u7a0b\u6ca1\u9000 \u2727
        ///   \u518d\u5f00\u5c31\u88ab\u5355\u5b9e\u4f8b\u9501\u6321\u4f4f \u2727 \u5f39\u4e2a\u6846\u5c31\u9000\u4e86\u2727
        ///   \u2014\u2014 \u2605 \u73b0\u5728\uff1a\u53d1\u73b0\u5b83\u8fde\u7a97\u53e3\u90fd\u6ca1\u6709 \u2727 \u5c31\u5f53\u5b83\u5361\u6b7b\u4e86 \u2727 \u76f4\u63a5\u6740\u6389\u91cd\u5f00 \u2713
        ///
        /// \u53ea\u6740\u3010\u540c\u4e00\u4e2a exe \u8def\u5f84\u3011\u7684\u8fdb\u7a0b \u2727 \u4e0d\u4f1a\u8bef\u4f24\u522b\u7684\u4e1c\u897f \u2713
        public static bool KillStuckSibling()
        {
            bool killed = false;
            try
            {
                string me = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                int myPid = System.Diagnostics.Process.GetCurrentProcess().Id;
                foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcessesByName("Miaohua"))
                {
                    try
                    {
                        if (p.Id == myPid) continue;
                        string path = p.MainModule.FileName;
                        if (!string.Equals(path, me, StringComparison.OrdinalIgnoreCase)) continue;
                        // \u2605 \u53ea\u6709\u3010\u6ca1\u6709\u4e3b\u7a97\u53e3\u3011\u7684\u624d\u7b97\u5361\u6b7b \u2713
                        if (p.MainWindowHandle != IntPtr.Zero) continue;
                        p.Kill();
                        try { p.WaitForExit(3000); } catch { }
                        killed = true;
                    }
                    catch { }
                }
            }
            catch { }
            return killed;
        }

        // ---------------------------------------------------------- IME 组合状态
        //
        // ★ Windows 的正规接口 ✧ 不是猜 ✓
        //   ImmGetCompositionStringW(himc, GCS_COMPSTR, ...) 返回组合串的字节数 ✓
        //   大于 0 = 正在拼音 ✧ 还没上屏 ✓
        //
        // ★ 为什么可能读不到：
        //   QQ / 微信是 Chromium ✧ 它把输入放在渲染子窗口里 ✓
        //   对顶层窗口调 ImmGetContext 可能拿不到那个组合串 ✗
        //   —— ★ 所以这只能当【多一道保险】✧
        //      读不到就当没这回事 ✧ 不会误伤 ✓
        //      （真正靠得住的是第二道 UIA 闸门 ✓）
        [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr ImmGetContext(IntPtr hWnd);
        [DllImport("imm32.dll")]
        static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);
        [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
        static extern int ImmGetCompositionStringW(IntPtr hIMC, int dwIndex, byte[] lpBuf, int dwBufLen);
        const int GCS_COMPSTR = 0x0008;

        /// ★ 前台窗口正在用输入法打字（拼音还没上屏）？
        /// 读不到就返回 false ✧ 当没这回事 ✓
        public static bool IsImeComposing()
        {
            IntPtr fg = IntPtr.Zero;
            IntPtr himc = IntPtr.Zero;
            try
            {
                fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;
                himc = ImmGetContext(fg);
                if (himc == IntPtr.Zero) return false;
                int len = ImmGetCompositionStringW(himc, GCS_COMPSTR, null, 0);
                return len > 0;
            }
            catch { return false; }
            finally
            {
                if (himc != IntPtr.Zero) { try { ImmReleaseContext(fg, himc); } catch { } }
            }
        }
        public const byte VK_RIGHT = 0x27;

        public static void SendShift(byte vk)
        {
            KeyDown(VK_SHIFT);
            Thread.Sleep(15);
            KeyDown(vk);
            Thread.Sleep(15);
            KeyUp(vk);
            Thread.Sleep(15);
            KeyUp(VK_SHIFT);
        }

        // WM_HOTKEY arrives the moment the key goes DOWN, while the user is still
        // holding Ctrl+Shift. If we synthesise Ctrl+A right then it is seen as
        // Ctrl+Shift+A (so nothing gets selected, and a stray "a" lands in the box),
        // and our own modifier events desync from the real ones.
        // So: wait for the user to let go, then explicitly clear any stuck modifier.
        public static void WaitForModifiersReleased()
        {
            for (int waited = 0; waited < 700; waited += 20)
            {
                if (!IsKeyDown(VK_CONTROL) && !IsKeyDown(VK_SHIFT)
                    && !IsKeyDown(VK_MENU) && !IsKeyDown(VK_LWIN))
                    break;
                Thread.Sleep(20);
            }
            KeyUp(VK_SHIFT);
            KeyUp(VK_MENU);
            KeyUp(VK_LWIN);
            KeyUp(VK_CONTROL);
            Thread.Sleep(40);
        }

        /// ★ 把前台窗口的身份完整说出来：进程名、标题、窗口类名。
        ///
        /// 为什么需要：有些工具（游戏里的输入法小窗之类）的窗口属于别的进程，
        /// 光看进程名判断不出它到底是什么。窗口类名是最有信息量的一个 ——
        ///   class=Edit / RichEdit  → 真的 Windows 编辑框，剪贴板快捷键应该有效
        ///   别的（尤其是自绘的）   → 它根本不接受 Ctrl+A / Ctrl+C
        public static string ForegroundWindowInfo()
        {
            try
            {
                IntPtr h = GetForegroundWindow();
                if (h == IntPtr.Zero) return "proc=? title=? class=? （没有前台窗口）";
                StringBuilder tb = new StringBuilder(300);
                GetWindowTextW(h, tb, 300);
                StringBuilder cb = new StringBuilder(300);
                GetClassNameW(h, cb, 300);
                string title = tb.ToString();
                if (title.Length > 60) title = title.Substring(0, 60) + "…";
                return "proc=" + ForegroundProcessName()
                     + " class=" + cb.ToString()
                     + " title=[" + title + "]";
            }
            catch (Exception e) { return Lang.T("取窗口信息出错：") + e.Message; }
        }
        public static string ForegroundProcessName()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return "";
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid == 0) return "";
            try
            {
                using (System.Diagnostics.Process p = System.Diagnostics.Process.GetProcessById((int)pid))
                    return p.ProcessName;
            }
            catch { return ""; }
        }

        public static int ForegroundProcessId()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return 0;
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            return (int)pid;
        }
    }

    // ---------------------------------------------------------------- hotkey window

    class HotkeyForm : Form
    {
        public event Action<int> HotkeyPressed;

        public HotkeyForm()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-4000, -4000);
            Size = new Size(1, 1);
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && HotkeyPressed != null)
                HotkeyPressed(m.WParam.ToInt32());
            base.WndProc(ref m);
        }
    }

    // ---------------------------------------------------------------- app

    class App : IDisposable
    {
        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValueName = "Miaohua";

        // marker put on the clipboard to detect "Ctrl+C copied nothing"
        const string ClipboardMarker = "\u0001MIAOHUA-EMPTY\u0001";

        public Config Cfg;
        public string BaseDir;

        /// ★ 静态自引用 ✧ TinyClf 这种静态类要用 App 的日志和路径 ✓
        public static App Cur;
        public static void LogStatic(string s)
        {
            try { if (Cur != null) Cur.Log(s); } catch { }
        }
        string RulesPath;
        string LogPath;
        string StatsPath;

        // real usage counters, shown on the 使用统计 card
        public Stats Stat = new Stats();

        NotifyIcon tray;
        HotkeyForm hkForm;
        System.Windows.Forms.Timer timer;
        // ★ 2026-10-18：语气稳定 ✧ 同一天用同一批台词 ✓
        Random rnd = new Random();

        bool paused = false;
        bool armed = false;
        int lastTransformTick = 0;

        // auto-mode bookkeeping: how many polls in a row saw fresh input while the
        // target app was in front (see OnTick), plus a cached foreground identity
        int typeTicks = 0;
        int lastTickAt = 0;
        int lastCursorX = int.MinValue;
        int lastCursorY = int.MinValue;
        IntPtr lastFgWnd = IntPtr.Zero;
        string lastFgProc = "";
        public int HotkeyFailures = 0;
        public MainForm Window;
        public bool StartHidden = false;
        public bool ReallyExiting = false;

        public App()
        {
            Cur = this;   // ★ 静态引用 ✧ TinyClf 要用 ✓
            BaseDir = Path.GetDirectoryName(Application.ExecutablePath);
            RulesPath = Path.Combine(BaseDir, "rules.txt");
            LogPath = Path.Combine(BaseDir, "miaohua.log");
            StatsPath = Path.Combine(BaseDir, "stats.txt");
            Stat = Stats.Load(StatsPath);
            // ★ 2026-10-31：把大模型改写挂上 ✗ 没开就不会调 ✓
            Engine.Rewriter = RewriteText;
        }

        // ------------------------------------------------------------- 内置模型
        //
        // The release package ships llama.cpp plus a quantised Qwen GGUF, so 智能语气 works
        // with nothing installed. The server is started only when it is needed AND nothing
        // already answers on the configured endpoint - a user who runs LM Studio keeps
        // using that, and nothing is duplicated.

        System.Diagnostics.Process modelProc;
        volatile bool modelStarting;

        public static string[] ModelDirs(string baseDir)
        {
            return new string[]
            {
                Path.Combine(baseDir, "\u6A21\u578B\u8FD0\u884C\u65F6"),   // 模型运行时
                Path.Combine(baseDir, "\u6A21\u578B"),                     // 模型
                Path.Combine(baseDir, "runtime"),
                Path.Combine(baseDir, "model"),
                baseDir
            };
        }

        public string BundledServerPath()
        {
            List<string> all = BundledServerPaths();
            return all.Count > 0 ? all[0] : null;
        }

        /// Every bundled llama-server, best first.
        ///
        /// The main one is the Vulkan build, which needs a Vulkan driver - it will not even
        /// load on a machine whose GPU driver does not provide vulkan-1.dll. The package also
        /// carries a CPU-only build under 备用-没有独显就用这个, so the list is ordered
        /// "Vulkan first, CPU as the fallback" and the starter tries them in turn. Without
        /// this the server simply failed to start on such a machine and 智能语气 fell back to
        /// keywords - silently.
        public List<string> BundledServerPaths()
        {
            List<string> found = new List<string>();
            string[] names = {
                "llama-server.exe",
                "\u5907\u7528-\u6CA1\u6709\u72EC\u663E\u5C31\u7528\u8FD9\u4E2A\\llama-server.exe"
            };
            foreach (string d in ModelDirs(BaseDir))
            {
                foreach (string n in names)
                {
                    try
                    {
                        string p = Path.Combine(d, n);
                        if (File.Exists(p) && !found.Contains(p)) found.Add(p);
                    }
                    catch { }
                }
            }
            return found;
        }

        /// ★ 列出模型文件夹里所有的 .gguf ✧ 给「模型」按钮循环用 ✓
        public List<string> AvailableModels()
        {
            List<string> outp = new List<string>();
            foreach (string d in ModelDirs(BaseDir))
            {
                try
                {
                    if (!Directory.Exists(d)) continue;
                    Array.Sort(Directory.GetFiles(d, "*.gguf"), StringComparer.OrdinalIgnoreCase);
                    foreach (string f in Directory.GetFiles(d, "*.gguf"))
                        outp.Add(Path.GetFileName(f));
                    if (outp.Count > 0) break;      // 只看第一个有模型的目录 ✓
                }
                catch { }
            }
            return outp;
        }

        /// ★ 当前跑着的模型简称（7B 这样）✧ 界面上显示用 ✓
        public string RunningModelName = "";

        /// ★ 从 gguf 文件名拿一个短名字 ✓
        public static string ShortModel(string f)
        {
            if (string.IsNullOrEmpty(f)) return "";
            string t = Path.GetFileName(f);
            if (t.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                t = t.Substring(0, t.Length - 5);
            try
            {
                System.Text.RegularExpressions.Match mm =
                    System.Text.RegularExpressions.Regex.Match(t, @"(\d+(?:\.\d+)?)[bB]");
                if (mm.Success) return mm.Groups[1].Value + "B";
            }
            catch { }
            return t.Length > 16 ? t.Substring(0, 16) + "\u2026" : t;
        }

        public string BundledModelPath()
        {
            foreach (string d in ModelDirs(BaseDir))
            {
                try
                {
                    if (!Directory.Exists(d)) continue;
                    string[] g = Directory.GetFiles(d, "*.gguf");
                    if (g.Length == 0) continue;

                    // ★ ① 规则里显式指定的优先 ✓
                    if (Cfg.LlmModelFile.Length > 0)
                    {
                        foreach (string one in g)
                        {
                            if (string.Equals(Path.GetFileName(one), Cfg.LlmModelFile,
                                              StringComparison.OrdinalIgnoreCase))
                                return one;
                        }
                        Log("model: 规则里指定的模型文件没找到："
                            + Cfg.LlmModelFile + " ✧ 改用最大的那个 ✓");
                    }

                    // ★ ② 否则用最大的 ✧ 越大越好 ✓
                    //   用户往文件夹里丢一个更好的模型 ✧ 重启就自动用上 ✓
                    string best = g[0];
                    long bestLen = -1;
                    foreach (string one in g)
                    {
                        try
                        {
                            long L = new FileInfo(one).Length;
                            if (L > bestLen) { bestLen = L; best = one; }
                        }
                        catch { }
                    }
                    if (g.Length > 1)
                        Log("model: 发现 " + g.Length + " 个 gguf ✧ 选最大的："
                            + Path.GetFileName(best));
                    return best;
                }
                catch { }
            }
            return null;
        }

        public bool ModelServerRunning() { return Llm.Ping(Cfg.LlmEndpoint, 700); }

        /// \u2605\u2605\u2605 2026-10-31\uff1a\u5927\u6a21\u578b\u6539\u5199 \u2605\u2605\u2605
        ///   wrap \u6a21\u5f0f\uff1a\u53ea\u8981\u53e5\u9996\u3001\u53e5\u5c3e\u4e24\u6bb5 \u2717 \u88c5\u9970\u8fd8\u662f\u89c4\u5219\u505a\u7684 \u2713
        ///   \u5176\u4ed6\u6a21\u5f0f\uff1a\u6574\u53e5\u4ea4\u7ed9\u6a21\u578b \u2717 \u5931\u8d25\u8fd4\u56de null \u2192 \u7528\u89c4\u5219\u7ed3\u679c \u2713
        /// ★ 2026-11-01：从池子里抽 3 条当样例 ✓
        static string PoolsSample(List<string> pool)
        {
            if (pool == null || pool.Count == 0) return "";
            StringBuilder sb = new StringBuilder();
            int take = Math.Min(3, pool.Count);
            for (int i = 0; i < take; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(pool[i]);
            }
            return sb.ToString();
        }

        string RewriteText(string plain, string rule)
        {
            Config c = Cfg;
            if (c == null || plain == null) return null;
            if (c.RewriteMode == "off" || string.IsNullOrEmpty(c.RewriteEndpoint)) return null;
            // ★ 缓存命中就直接用 ✗ 不再等网络 ✓
            string ck = c.RewriteMode + "|" + plain + "|" + rule;
            string hit;
            if (Engine.RewriteCache.TryGetValue(ck, out hit)) return hit;

            // ★ 2026-11-01：manual = 只有热键才用 AI ✗ 自动转换保持规则 ✓
            if (c.RewriteMode == "manual" && !Engine.ManualTrigger) return null;

            if (c.RewriteMode == "wrap")
            {
                // ★ 把风格名和它自己的样例一起给模型 ✓
                string opS = PoolsSample(c.Openers);
                string clS = PoolsSample(c.Closers);
                string w = Llm.Wrap(plain, rule, opS, clS, Engine.StyleName,
                                    c.RewriteEndpoint, c.RewriteModel,
                                    c.RewriteKey, c.RewriteTimeoutMs);
                if (string.IsNullOrEmpty(w)) return null;
                string[] q = w.Split('\u0001');
                if (q.Length < 4) return null;
                string op = q[0], suf = q[1], kao = q[2], cl = q[3];
                if (op.Length == 0 && suf.Length == 0 && kao.Length == 0 && cl.Length == 0) return null;
                // ★ 四段都由模型写 ✗ 正文用原句 ✓
                StringBuilder sb = new StringBuilder();
                if (op.Length > 0) sb.Append(op);
                sb.Append(plain);
                if (suf.Length > 0) sb.Append(suf);
                if (kao.Length > 0) sb.Append(kao);
                if (cl.Length > 0)
                {
                    char lastCh = sb.Length > 0 ? sb[sb.Length - 1] : ' ';
                    if ("。！？…～，、".IndexOf(lastCh) < 0) sb.Append('，');
                    sb.Append(cl);
                }
                return sb.ToString();
            }
            // ★ 2026-11-01：连接被对方切断是常事 ✗ 重试一次再说 ✓
            string rr = null;
            for (int attempt = 0; attempt < 2 && string.IsNullOrEmpty(rr); attempt++)
            {
                rr = Llm.Rewrite(plain, rule, c.RewriteEndpoint, c.RewriteModel,
                                 c.RewriteKey, c.RewriteTimeoutMs);
                if (string.IsNullOrEmpty(rr) && attempt == 0)
                    System.Threading.Thread.Sleep(120);
            }
            if (!string.IsNullOrEmpty(rr))
            {
                if (Engine.RewriteCache.Count > 400) Engine.RewriteCache.Clear();
                Engine.RewriteCache[ck] = rr;
            }
            if (string.IsNullOrEmpty(rr))
                App.LogStatic("大模型没回应（" + (Llm.LastError.Length == 0 ? "空回复" : Llm.LastError) + "），用规则结果");
            else
                App.LogStatic("大模型改写 -> [" + rr + "]");
            return rr;
        }

        static int PortOf(string endpoint)
        {
            try
            {
                Uri u = new Uri(endpoint);
                return u.Port > 0 ? u.Port : 1234;
            }
            catch { return 1234; }
        }

        /// Brings the bundled server up in the background. Cheap to call repeatedly: it
        /// returns immediately if the endpoint already answers, if a start is in flight, or
        /// if there is no bundled runtime to start.
        public void EnsureModelServerAsync()
        {
            if (!Cfg.SmartTone) return;
            if (modelStarting) return;
            List<string> servers = BundledServerPaths();
            if (servers.Count == 0) return;
            string gguf = BundledModelPath();
            if (gguf == null) return;

            modelStarting = true;
            Thread t = new Thread(delegate()
            {
                try
                {
                    if (ModelServerRunning())
                    {
                        Log("model: something already answers at " + Cfg.LlmEndpoint + " - not starting the bundled one");
                        return;
                    }

                    int port = PortOf(Cfg.LlmEndpoint);

                    // Try each bundled build in turn. The Vulkan one cannot even load on a
                    // machine whose GPU driver has no vulkan-1.dll, and the CPU build is right
                    // there in the same package - without this the server just failed and
                    // 智能语气 quietly used keywords instead.
                    foreach (string exe in servers)
                    {
                        Log("model: starting " + Path.GetFileName(exe) + " on port " + port
                            + " (" + Path.GetFileName(gguf) + ")");

                        System.Diagnostics.ProcessStartInfo si = new System.Diagnostics.ProcessStartInfo();
                        si.FileName = exe;
                        si.WorkingDirectory = Path.GetDirectoryName(exe);
                        si.UseShellExecute = false;
                        si.CreateNoWindow = true;
                        si.Arguments = "-m \"" + gguf + "\" --host 127.0.0.1 --port " + port +
                                       " -ngl 99 -c 4096 --no-webui";
                        modelProc = System.Diagnostics.Process.Start(si);

                        bool up = false;
                        for (int i = 0; i < 120; i++)
                        {
                            Thread.Sleep(500);
                            if (ModelServerRunning()) { up = true; break; }
                            if (modelProc != null && modelProc.HasExited)
                            {
                                Log("model: exited early with code " + modelProc.ExitCode);
                                break;
                            }
                        }
                        if (!up) { Log("model: no answer from " + Path.GetFileName(exe)); continue; }

                        // ★ 记住这次加载的是哪个 ✧ 界面上要显示 ✓
                    try { RunningModelName = ShortModel(gguf); } catch { }

                    Llm.BundledModelActive = true;   // ★ 之后请求不带 model 字段 ✓
                    Log("model: ready at " + Cfg.LlmEndpoint);
                        string warm;
                        Llm.Probe(Cfg.LlmEndpoint, "", 60000, out warm);
                        Llm.ClearCache();
                        Log("model: warmed up (" + (warm == null ? "no answer" : warm) + ")");
                        return;
                    }

                    Log("model: no bundled build could be started - 智能语气 will use keywords");
                }
                catch (Exception ex) { Log("model start failed: " + ex.Message); }
                finally { modelStarting = false; }
            });
            t.Start();
        }

        /// Killed on exit so the package does not leave a 2 GB process behind.
        public void StopModelServer()
        {
            Llm.BundledModelActive = false;   // ★ 内置模型停了 ✗ 恢复按用户设置走 ✓
            try
            {
                if (modelProc != null && !modelProc.HasExited)
                {
                    modelProc.Kill();
                    Log("model: bundled server stopped");
                }
            }
            catch { }
        }

        public void Start()
        {
            ReloadRules();
            BuildTray();
            BuildHotkeys();

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 100;   // poll fast enough to resolve the shortest debounce
            timer.Tick += OnTick;
            timer.Start();

            // Printed once at startup: the only reliable way to tell whether rules.txt was
            // parsed as expected. Changes to the 界面-* keys look identical on screen when the
            // key silently fails to match, and that is exactly what happened.
            Log("ui flags: topbar=" + Cfg.UiTopBar + " cards=" + Cfg.UiCards
                + " lab=" + Cfg.UiLab + " kao=" + Cfg.UiKao
                + " nav=" + Cfg.UiNavIcon + " lvl=" + Cfg.UiLevel 
                + " color=" + Cfg.UiColor + " paw=" + Cfg.UiPaw + " mood=" + Cfg.UiMood
                + " anim=" + Cfg.UiAnim + " animms=" + Cfg.UiAnimMs + " slidepct=" + Cfg.UiSlidePct);            Log("started; version=" + Program.Version + " (" + Program.BuildDate + ")  autorun=" + IsAutoRunEnabled());

            // The package can ship llama.cpp + a GGUF so 智能语气 works with nothing
            // installed. Started here in the background: bringing a 2 GB model up takes a
            // few seconds, and the UI must not wait for it. If something already answers on
            // the configured endpoint (LM Studio, say) nothing is started at all.
            // ★ 用户 2026-10-17：默认不自动加载 ✧ 要用再点「启动内置模型」✓
            // ★ 调试：启动时把模型列表打出来 ✓
            try
            {
                List<string> ms = AvailableModels();
                Log("model: 扫到 " + ms.Count + " 个 gguf："
                    + (ms.Count == 0 ? "(空)" : string.Join(" | ", ms.ToArray())));
            }
            catch (Exception ex) { Log("model: 打印列表失败：" + ex.Message); }

            if (Cfg.ModelAutoStart) EnsureModelServerAsync();
            else Log("model: 自动加载已关（模型自启 = false），等用户手动启动");

            Window = new MainForm(this);
            if (StartHidden) ShowStartupBalloon();
            else Window.ShowWindow();
        }

        void ShowStartupBalloon()
        {
            if (tray == null) return;
            try
            {
                if (HotkeyFailures > 0)
                {
                    tray.BalloonTipTitle = Lang.T("喵化助手已启动（有快捷键冲突）");
                    tray.BalloonTipText = Lang.T("有 ") + HotkeyFailures + Lang.T(" 个快捷键被别的软件占用了。")
                        + Lang.T("打开 rules.txt 换一个组合，再按 ") + Cfg.HotkeyReload + Lang.T(" 重载。");
                    tray.ShowBalloonTip(10000);
                }
                else
                {
                    tray.BalloonTipTitle = Lang.T("喵化助手已启动");
                    tray.BalloonTipText = Lang.T("在 QQ 里打完字，按 ") + Cfg.HotkeyTransform
                        + Lang.T(" 就能喵化。双击这个图标可以看状态。");
                    tray.ShowBalloonTip(8000);
                }
            }
            catch { }
        }

        // ---- state shared with the main window ----

        public bool IsPaused { get { return paused; } }

        public int RuleCount { get { return Cfg.Rules.Count; } }
        public int KaomojiCount { get { return Cfg.Kaomoji.Count; } }

        // how many conversions this run has done - shown as a real number, never an
        // invented one
        int sessionCount = 0;
        public int SessionCount { get { return sessionCount; } }
        public int LastTransformTick { get { return lastTransformTick; } }

        public void SetPaused(bool v)
        {
            paused = v;
            UpdateTrayText();
            Log("paused=" + v);
            if (Window != null) { Window.SyncCheckboxes(); Window.RefreshStatus(); }
        }

        public bool AutoModeEnabled { get { return Cfg.AutoMode; } }

        public void SetAutoMode(bool v)
        {
            Cfg.AutoMode = v;
            Log("automode=" + v);
            if (Window != null) { Window.SyncCheckboxes(); Window.RefreshStatus(); }
        }

        public string RulesFilePath { get { return RulesPath; } }

        public string LogFilePath { get { return LogPath; } }

        // Win32 edit controls (and therefore WinForms TextBox) only break lines on
        // CRLF. A file written with bare LF shows up as ONE gigantic line, so every
        // string crossing the rules box has to be normalised in both directions.
        public static string ToCrlf(string s)
        {
            if (s == null) return "";
            return s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        }

        public string RulesText()
        {
            try { return ToCrlf(File.ReadAllText(RulesPath, Encoding.UTF8)); }
            catch { return ""; }
        }

        public void ApplyRulesText(string text)
        {
            // Goes through the same guard as PatchRules. An empty rules box used to be
            // written straight over the file - one stray click on 保存规则 and the whole
            // ruleset, comments and all, was gone.
            if (!WriteRulesSafely(ToCrlf(text), "save rules", false))
                throw new InvalidOperationException(
                    Lang.T("内容比现有规则少太多，已拦截。请检查是不是不小心清空了编辑框。"));
            ReloadRules();
            if (Window != null) Window.RefreshStatus();
        }

        /// \u2605 2026-10-22\uff1a\u9884\u89c8\u4e0d\u770b\u4e0a\u4e0b\u6587\u3001\u4e0d\u7b49\u6a21\u578b \u2717
        ///   \u5426\u5219\u6bcf\u6572\u4e00\u4e2a\u5b57\uff0c\u4ee5\u53ca\u6bcf\u6b21\u5207\u6362\u98ce\u683c\uff0c\u90fd\u4f1a\u5361\u4e0a\u534a\u79d2 \u2713
        public string TransformForPreview(string s)
        {
            if (Cfg == null || s == null) return s;
            bool prev = Engine.PreviewMode;
            Engine.PreviewMode = true;
            try { return Engine.Run(s, Cfg, rnd); }
            finally { Engine.PreviewMode = prev; }
        }

        // does this line set one of our keys? Chinese and English spellings both count,
        // and commented-out lines must never be rewritten.
        static bool IsKeyLine(string line, string cn, string en)
        {
            string s = line.Trim();
            if (s.Length == 0 || s[0] == '#') return false;
            int eq = s.IndexOf('=');
            if (eq <= 0) return false;
            string k = s.Substring(0, eq).Trim().ToLowerInvariant();
            k = k.Replace("_", "").Replace("-", "").Replace(" ", "").Replace(".", "");
            // ★ cn 也必须做同样的归一化。
            //
            // 之前只归一化了文件里读出来的 k，却拿它跟表里【原样】的中文键比 ——
            // 于是所有【带横杠的键】永远匹配不上：界面-顶部气泡、界面-动画、
            // 界面-动画时长、界面-滑块比例… 全部 11 个界面开关。
            // 老键（模板 / 后缀 / 语气 / 延迟…）都没有横杠，所以这个 bug 一直没露头。
            //
            // 后果：保存时认不出那些行 —— 旧行留着、新行追加，存几次 rules.txt
            // 里就堆几行同名配置；解析器读最后一行，于是"改了设置没反应"。
            string cnk = cn.ToLowerInvariant()
                .Replace("_", "").Replace("-", "").Replace(" ", "").Replace(".", "");
            return k == cnk || k == en;
        }

        /// Every key the UI is allowed to rewrite, with both spellings. Anything not in
        /// this list is never touched by the editor pages.
        public static readonly string[][] PatchKeys = new string[][]
        {
            new string[] { "\u6A21\u677F", "template" },
            new string[] { "\u540E\u7F00", "suffix" },
            new string[] { "\u989C\u6587\u5B57", "kaomoji" },
            new string[] { "\u5F00\u5934", "opener" },
            new string[] { "\u7ED3\u5C3E", "closer" },
            new string[] { "\u95EE\u53E5\u7ED3\u5C3E", "questioncloser" },
            new string[] { "\u53F9\u53E5\u7ED3\u5C3E", "exclaimcloser" },
            new string[] { "\u5F00\u5FC3\u7ED3\u5C3E", "happycloser" },
            new string[] { "\u96BE\u8FC7\u7ED3\u5C3E", "sadcloser" },
            new string[] { "\u751F\u6C14\u7ED3\u5C3E", "angrycloser" },
            new string[] { "\u6253\u62DB\u547C\u7ED3\u5C3E", "greetingcloser" },
            new string[] { "\u5206\u522B\u7ED3\u5C3E", "farewellcloser" },
            new string[] { "\u559C\u7231\u7ED3\u5C3E", "lovecloser" },
            new string[] { "\u5938\u5956\u7ED3\u5C3E", "praisecloser" },
            new string[] { "\u611F\u8C22\u7ED3\u5C3E", "thankscloser" },
            new string[] { "\u9053\u6B49\u7ED3\u5C3E", "apologycloser" },
            new string[] { "\u795D\u8D3A\u7ED3\u5C3E", "congratcloser" },
            new string[] { "\u9080\u8BF7\u7ED3\u5C3E", "invitecloser" },
            new string[] { "\u60CA\u8BB6\u7ED3\u5C3E", "surprisecloser" },
            new string[] { "\u5BB3\u6015\u7ED3\u5C3E", "fearcloser" },
            new string[] { "\u75B2\u60EB\u7ED3\u5C3E", "tiredcloser" },
            new string[] { "\u65E0\u804A\u7ED3\u5C3E", "boredcloser" },
            new string[] { "\u5931\u671B\u7ED3\u5C3E", "disappointedcloser" },
            new string[] { "\u5B89\u6170\u7ED3\u5C3E", "comfortcloser" },
            new string[] { "\u6492\u5A07\u7ED3\u5C3E", "charmcloser" },
            new string[] { "\u7A0B\u5EA6", "level" },
            new string[] { "\u56FE\u7247\u4FDD\u62A4", "imageguard" },
            new string[] { "\u8BED\u8A00", "language" },
            new string[] { "\u5EF6\u8FDF", "debounce" },
            new string[] { "\u8F6C\u6362\u70ED\u952E", "hotkey_transform" },
            new string[] { "\u6682\u505C\u70ED\u952E", "hotkey_pause" },
            new string[] { "\u91CD\u8F7D\u70ED\u952E", "hotkey_reload" },
            // ★ 2026-10-18：颜文字收藏 ✗ PatchKeys 里必须有 ✗ 否则 PatchRules 改不掉它 ✓
            new string[] { "\u6536\u85CF", "favkao" },
            new string[] { "\u8FDB\u7A0B\u98CE\u683C", "procstyle" },
            new string[] { "\u8FDB\u7A0B\u81EA\u52A8", "proauto" },
            new string[] { "\u8BED\u6C14\u7A33\u5B9A", "stablemood" },
            new string[] { "\u64A4\u9500\u70ED\u952E", "hotkey_undo" },
            new string[] { "\u7A0B\u5E8F", "whitelist" },
            new string[] { "\u667A\u80FD\u8BED\u6C14", "smarttone" },
            new string[] { "\u8BED\u6C14", "tone" },
            new string[] { "\u6A21\u578B\u5730\u5740", "llmendpoint" },
            new string[] { "\u6A21\u578B\u540D", "llmmodel" },
            new string[] { "\u6A21\u578B\u8D85\u65F6", "llmtimeout" },
            // ★ 界面开关也必须是可改写键。之前这里一个都没有 —— 保存时认不出这些行，
            //   旧行留着、新行追加，存几次 rules.txt 里就堆几行同名配置；解析器读最后一行，
            //   所以"改完没反应 / 行为怪怪的"都是这么来的。
            new string[] { "界面-顶部气泡",   "uitopbar" },
            new string[] { "界面-数字卡",     "uicards" },
            new string[] { "界面-实验室",     "uilab" },
            new string[] { "界面-颜文字胶囊", "uikao" },
            new string[] { "界面-导航图标",   "uinavicon" },
            new string[] { "界面-升级提示",   "uilevel" },
            new string[] { "界面-颜色分层",   "uicolor" },
            new string[] { "界面-猫爪装饰",   "uipaw" },
            new string[] { "界面-台词变化",   "uimood" },
            new string[] { "界面-动画",       "uianim" },
            new string[] { "界面-动画时长",   "uianmims" },
            new string[] { "界面-滑块比例",   "uislidepct" },

            // ★ 2026-10-18：这五个新键必须进这张表 ✗ 不然 PatchRules 写不进去 ✓
            new string[] { "语气叠加",   "stack" },
            new string[] { "颜文字冷却", "kaocool" },
            new string[] { "我的组合",   "combo" },
            new string[] { "智能延迟",   "smartdelay" },
            // ★ 2026-10-31：大模型改写 ✗ 不在这里就保存不了 ✓
            new string[] { "改写模式", "rewritemode" },
            new string[] { "改写地址", "rewriteendpoint" },
            new string[] { "改写模型", "rewritemodel" },
            new string[] { "改写密钥", "rewritekey" },
            new string[] { "改写超时", "rewritetimeout" },
        };

        static string MatchPatchKey(string line)
        {
            for (int i = 0; i < PatchKeys.Length; i++)
                if (IsKeyLine(line, PatchKeys[i][0], PatchKeys[i][1])) return PatchKeys[i][0];
            return null;
        }

        /// Rewrites the listed keys in rules.txt and NOTHING else. `values` maps a
        /// canonical Chinese key to its new lines; an empty array removes the key.
        /// Comments, word rules and blank lines all survive, so the file stays
        /// hand-editable and the editor never becomes the only way in.
        /// Counts lines that actually carry content - blank lines and comments do not count,
        /// because a file that keeps only its comments has still lost the user's work.
        static int CountContentLines(string text)
        {
            if (text == null) return 0;
            int n = 0;
            foreach (string raw in text.Split('\n'))
            {
                string s = raw.Trim();
                if (s.Length == 0) continue;
                if (s.StartsWith("#")) continue;
                n++;
            }
            return n;
        }

        /// Refuses a write that would wipe the file. This exists because it HAPPENED: a
        /// stray click left rules.txt as a single newline (9 bytes) and the user's whole
        /// ruleset was gone.
        ///
        /// allowBigShrink is for callers that have their own, exact check. The blunt
        /// "lost two thirds" test must NOT be applied to PatchRules: switching to a look
        /// style deliberately deletes all twenty 语气 pools at once, which is a legitimate
        /// 80% reduction and was wrongly blocked by an earlier version of this guard.
        bool WriteRulesSafely(string newText, string what, bool allowBigShrink)
        {
            string before = "";
            try { if (File.Exists(RulesPath)) before = File.ReadAllText(RulesPath, Encoding.UTF8); }
            catch { }

            int b = CountContentLines(before);
            int a = CountContentLines(newText);
            if (!allowBigShrink && b >= 20 && a < b / 3)
            {
                Log("REFUSED " + what + ": would cut content lines " + b + " -> " + a +
                    ". rules.txt left untouched.");
                if (Window != null)
                    MessageBox.Show(Window,
                        Lang.T("这次改动会把规则文件从 ") + b + Lang.T(" 条内容削减到 ") + a + Lang.T(" 条，已经拦下没有写入。\r\n\r\n") +
                        Lang.T("如果这确实是你想要的（比如想清空重写），请直接编辑 rules.txt。"),
                        Lang.T("喵化助手 · 已拦截"));
                return false;
            }

            try
            {
                if (before.Length > 0)
                    File.WriteAllText(RulesPath + ".bak", before, new UTF8Encoding(false));
                File.WriteAllText(RulesPath, newText, new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                Log("write rules failed: " + ex.Message);
                return false;
            }
        }

        /// Every content line this patch does NOT own - word rules, comments, hotkeys,
        /// anything unrecognised - counted as a multiset. PatchRules is only allowed to
        /// touch the keys it was handed, so this set must come out identical. That is an
        /// exact invariant, unlike counting lines.
        static Dictionary<string, int> NonOwnedLines(string text, Dictionary<string, string[]> values)
        {
            Dictionary<string, int> d = new Dictionary<string, int>();
            if (text == null) return d;
            foreach (string raw in text.Split('\n'))
            {
                string s = raw.Trim();
                if (s.Length == 0) continue;
                string key = MatchPatchKey(raw);
                if (key != null && values.ContainsKey(key)) continue;
                int n;
                d.TryGetValue(s, out n);
                d[s] = n + 1;
            }
            return d;
        }

        static bool NonOwnedLinesSurvive(string before, string after, Dictionary<string, string[]> values)
        {
            Dictionary<string, int> b = NonOwnedLines(before, values);
            Dictionary<string, int> a = NonOwnedLines(after, values);
            foreach (KeyValuePair<string, int> kv in b)
            {
                int have;
                a.TryGetValue(kv.Key, out have);
                if (have < kv.Value) return false;
            }
            return true;
        }

        /// ★ 写全局设置（语言 / 快捷键 / 延迟 / 界面开关）★ 永远写 rules.txt
        /// ★ 写指定文件的安全版本 ★
        ///
        /// 现有 WriteRulesSafely 是写死 RulesPath 的。
        /// 这里临时把 RulesPath 换成目标路径、调用、再换回来 ——
        /// WinForms 是单线程，中间不会被别处读到；finally 保证一定还原。
        /// 改动最小 ✗ 逻辑和原路径完全一致 ✓
        bool WriteRulesSafelyTo(string path, string text, string what, bool allowShrink)
        {
            string saved = RulesPath;
            try
            {
                RulesPath = path;
                return WriteRulesSafely(text, what, allowShrink);
            }
            finally { RulesPath = saved; }
        }

        public bool PatchRules(Dictionary<string, string[]> values)
        {
            return PatchRulesCore(RulesPath, values);
        }

        /// ★ 写风格（模板 / 后缀 / 颜文字 / 语气词库）★ 按语言选文件
        ///
        /// 为什么要分开：这几项是【分语言】的 —— 中文的风格池和英文的
        /// 是两套内容，各自存在自己的规则文件里。
        /// 而快捷键、延迟那些跟语言无关 ✗ 放哪份都该一样 ✓
        public bool PatchStyleRules(Dictionary<string, string[]> values)
        {
            return PatchRulesCore(ActiveRulesPath, values);
        }

        bool PatchRulesCore(string path, Dictionary<string, string[]> values)
        {
            try
            {
                string before = File.ReadAllText(path, Encoding.UTF8);
                string[] lines = ToCrlf(before).Split(new string[] { "\r\n" }, StringSplitOptions.None);

                List<string> outp = new List<string>();
                HashSet<string> done = new HashSet<string>();

                foreach (string raw in lines)
                {
                    string key = MatchPatchKey(raw);
                    if (key == null) { outp.Add(raw); continue; }
                    if (!values.ContainsKey(key)) { outp.Add(raw); continue; }
                    if (done.Contains(key)) continue;
                    done.Add(key);
                    string[] vals = values[key];
                    for (int i = 0; i < vals.Length; i++) outp.Add(key + " = " + vals[i]);
                }

                foreach (KeyValuePair<string, string[]> kv in values)
                {
                    if (done.Contains(kv.Key)) continue;
                    for (int i = 0; i < kv.Value.Length; i++)
                        outp.Add(kv.Key + " = " + kv.Value[i]);
                }

                string result = string.Join("\r\n", outp.ToArray());
                if (!NonOwnedLinesSurvive(before, result, values))
                {
                    Log("REFUSED patch rules: a line this operation does not own would have " +
                        "been lost. rules.txt left untouched.");
                    if (Window != null)
                        MessageBox.Show(Window,
                            Lang.T("这次改动会丢掉不属于它的行（词规则或注释），已经拦下没有写入。\r\n\r\n") +
                            Lang.T("请把 rules.txt 发我看看。"),
                            Lang.T("喵化助手 · 已拦截"));
                    return false;
                }
                if (!WriteRulesSafelyTo(path, result, "patch rules", true)) return false;

                ReloadRules();
                if (Window != null) { Window.SyncCheckboxes(); Window.RefreshStatus(); }
                return true;
            }
            catch (Exception ex)
            {
                Log("patch rules failed: " + ex.Message);
                return false;
            }
        }

        /// Swaps only the decoration lines in the rules file. Word replacements, hotkeys,
        /// whitelist and every comment stay untouched, so a style is a visible edit the user
        /// can then tweak.
        ///
        /// pools maps a pool id ("closer", "questioncloser", "greetingcloser", ...) to its
        /// lines. A missing id, or a null value, writes an EMPTY array - and an empty array
        /// removes the key from the file, which is how 外观风格 switches the 语气 wrapper off.
        /// Passing a dictionary instead of twenty arguments keeps the caller honest: a pool
        /// that is forgotten simply comes out empty rather than silently shifting position.
        public bool ApplyStyle(string template, string suffix, string[] kaomoji,
                               Dictionary<string, string[]> pools)
        {
            return ApplyStyle(template, suffix, kaomoji, pools, null);
        }

        /// ★ tone 是可选的：非 null 时顺便把「语气」开关一起写进去 ★
        ///
        /// 为什么：点外观风格原本要写两遍文件（一遍风格、一遍语气开关），
        /// 每次都是「读 → 改 → 写 → 重载」✗ 两遍就是两倍开销 ✓
        /// 合并成一次写入，点下去能快一截。
        /// 用可选参数是为了【现有调用一行都不用改】✗ 风险最小 ✓
        public bool ApplyStyle(string template, string suffix, string[] kaomoji,
                               Dictionary<string, string[]> pools, bool? tone)
        {
            Dictionary<string, string[]> v = new Dictionary<string, string[]>();
            v["\u6A21\u677F"] = new string[] { template };
            v["\u540E\u7F00"] = new string[] { suffix };
            v["\u989C\u6587\u5B57"] = kaomoji;
            if (tone.HasValue)
                v["\u8BED\u6C14"] = new string[] { tone.Value ? "true" : "false" };

            StringBuilder sizes = new StringBuilder();
            // pools == null means "外观风格": it only owns 模板 / 后缀 / 颜文字 and must leave
            // the 语气词库 completely alone. The two used to be wired as mutually exclusive,
            // so a single click on 萌系 deleted all twenty pools - a hand-written ruleset
            // gone by accident. There is no reason they cannot coexist either
            // (萌系 kaomoji + 傲娇 tone is a perfectly sensible combination).
            if (pools != null)
            {
                foreach (string[] pair in PatchKeys)
                {
                    string cn = pair[0], en = pair[1];
                    if (en != "opener" && en != "closer" && !en.EndsWith("closer")) continue;
                    string[] lines = null;
                    pools.TryGetValue(en, out lines);
                    v[cn] = lines ?? new string[0];
                    sizes.Append(" ").Append(en).Append("=").Append(lines == null ? 0 : lines.Length);
                }
            }
            else
            {
                sizes.Append(" (\u5916\u89C2\u98CE\u683C: \u8BED\u6C14\u8BCD\u5E93\u4E0D\u52A8)");
            }

            // ★ 风格要写【当前语言那份】✗ 否则英文模式下写完就被覆盖 ✓
            bool ok = PatchStyleRules(v);
            Log("style applied: template=[" + template + "] suffix=[" + suffix +
                "] kaomoji=" + kaomoji.Length + sizes.ToString()
                + " -> " + Path.GetFileName(ActiveRulesPath));
            return ok;
        }

        public void OpenRulesInNotepad()
        {
            try { System.Diagnostics.Process.Start("notepad.exe", "\"" + RulesPath + "\""); } catch { }
        }

        public void OpenLogInNotepad()
        {
            try { System.Diagnostics.Process.Start("notepad.exe", "\"" + LogPath + "\""); } catch { }
        }

        public string StatusHead()
        {
            // ★ 整串是拼出来的 ✗ 所以每一段都要单独翻 ✓
            //   英文比中文长 ✗ 这里刻意用极短的词 ✗ 免得和旁边挤在一起 ✓
            return (paused ? Lang.T("已暂停") : Lang.T("运行中"))
                + "        " + Cfg.Rules.Count + " " + Lang.T("条替换")
                + " · " + Cfg.Kaomoji.Count + " " + Lang.T("个颜文字")
                + "        " + Lang.T("自动模式") + " "
                + (Cfg.AutoMode ? Lang.T("开") : Lang.T("关"));
        }

        /// ★ 状态行：显示【当前实际情况】而不是固定文案 ✓
        ///
        /// 原来是"立即转换 Ctrl+Shift+M … 生效程序 QQ"这种说明文字 ✗
        /// 每次打开都一模一样 ✗ 说的全是说明书 ✗ 不是"现在怎么样"✓
        ///
        /// 现在三项都是运行时真值 ✗ 改了设置立刻就反映出来：
        ///     生效程序 / 自动模式与延迟 / 语气开关
        /// 快捷键不放了 —— 「设置」页里本来就有 ✓
        public string StatusDetail()
        {
            // ★ 这一行宽度有限 ✗ 进程名最多列 3 个 ✓
            //   （汉字窄、英文宽 ✗ 同样内容占位差一倍 ✓）
            string sep = Lang.English ? ", " : "、";
            string[] w = Cfg.Whitelist.ToArray();
            string apps;
            if (w.Length == 0) apps = Lang.T("全部程序");
            else
            {
                string head = string.Join(sep, w, 0, Math.Min(3, w.Length));
                if (w.Length <= 3) apps = head;
                else apps = head + (Lang.English
                        ? ", …+" + (w.Length - 3)
                        : Lang.T(" 等 ") + w.Length + Lang.T(" 个"));
            }

            string auto = Cfg.AutoMode
                ? Lang.T("自动") + " " + Cfg.DebounceMs + "ms"
                : Lang.T("只按热键");

            // ★ 状态行：程序 · 快捷键 · 自动 · 语气 ✓
            //   全是运行时真值 ✗ 改设置立刻反映 ✓
            //   快捷键只放最常用的那一个 —— 四个都放会超宽（英文尤其 ✓）✓
            // ★★★ 分隔符用【不换行空格】★★★
            //   statusDetail 是 Dock=Fill ✧ 宽度跟父容器走 ✓
            //   切语言时右侧卡片会 Clear+重建 ✧ 触发整个窗体重算布局 ✓
            //   那一瞬间它的宽度可能是 0 ✧ 文字就在"·"处断成四行 ✓
            //   —— 换成 U+00A0（不换行空格）✧ 再窄也不会在这里断 ✓
            //      顶多溢出一帧 ✧ 比换成四行好得多 ✓
            string nb = "\u00A0";
            return Lang.T("生效") + "：" + apps
                + nb + "·" + nb + Lang.T("立即转换") + " " + Cfg.HotkeyTransform
                + nb + "·" + nb + auto
                + nb + "·" + nb + Lang.T("语气") + " " + (Cfg.Tone ? Lang.T("开") : Lang.T("关"));
        }

        public string StatusWarn()
        {
            if (HotkeyFailures > 0)
                return Lang.T("⚠ 有 ") + HotkeyFailures + Lang.T(" 个快捷键已被别的软件占用，没能注册上；")
                    + Lang.T("请到「规则」页改 hotkey_ 开头的设置。");
            return "";
        }

        /// ★ 按指定语言重新加载规则 ✗ 不看文件里写的 language ✓
        ///
        /// 为什么需要单独一个：切换语言时，文件里的 language 还没被改写 ✓
        /// 如果 ReloadRules 去读文件 ✗ 读到的还是旧值 ✗ 加载的还是旧那份规则 ✗
        /// 表现就是"切了语言，但规则数 / 颜文字数不变"✓
        ///
        /// 这个版本把语言当参数传进来 ✗ 调用顺序就不会再坑人 ✓
        public void ReloadRulesFor(string lang)
        {
            Cfg.Language = string.IsNullOrEmpty(lang) ? "zh" : lang.Trim().ToLowerInvariant();

            string path = RulesPath;
            if (Cfg.Language == "en")
            {
                string enPath = Path.Combine(BaseDir, "rules-en.txt");
                if (File.Exists(enPath)) path = enPath;
                else Log("language=en but rules-en.txt is missing - keeping the Chinese rules");
            }

            Cfg = Config.Load(path);
            Cfg.Language = string.IsNullOrEmpty(lang) ? "zh" : lang.Trim().ToLowerInvariant();
            // ★ 这一句很关键：把语言钉在内存里 ✗ 
            //   之后任何 ReloadRules 都会沿用这个值 ✓

            Log(string.Format("rules reloaded for [{0}]: {1} replacements, {2} kaomoji, {3} kaomoji-lines",
                Cfg.Language, Cfg.Rules.Count, Cfg.Kaomoji.Count, Cfg.Kaomoji.Count));
        }

        /// ★ 当前语言对应的规则文件 ★
        ///
        /// ★★★ 为什么需要它 ★★★
        ///   英文模式下 ReloadRules 读 rules-en.txt ✗
        ///   而 PatchRules 原来一直写 rules.txt ✓
        ///   → 点风格写进去的东西【立刻被 rules-en.txt 覆盖】✗ 
        ///     界面显示的永远是 rules-en.txt 那一批 ✓
        ///   —— ★ 凡是"写 A 读 B"✗ 写完就白写 ✓
        public string ActiveRulesPath
        {
            get
            {
                if (Cfg != null && Cfg.Language == "en")
                {
                    string en = Path.Combine(BaseDir, "rules-en.txt");
                    if (File.Exists(en)) return en;
                }
                return RulesPath;
            }
        }

        /// ★ 把【规则相关】的字段从另一份配置拷进来 ★
        ///
        /// 英文模式下两份都要用：
        ///     rules.txt      —— 全局设置（智能语气 / 模型 / 快捷键 / 延迟 / 界面开关）
        ///     rules-en.txt   —— 英文的替换规则和语气词库
        /// 所以先读 rules.txt 当底 ✗ 再用这个函数把英文那份的规则覆盖上去 ✓
        ///
        /// ★ 不拷的东西：所有全局设置 ✗ 它们只认 rules.txt ✓
        void MergeRulesFrom(Config from)
        {
            if (from == null) return;
            Cfg.Rules = from.Rules;
            Cfg.Kaomoji = from.Kaomoji;
            Cfg.SkipEnds = from.SkipEnds;
            Cfg.Suffix = from.Suffix;
            Cfg.Template = from.Template;
            Cfg.EachSentence = from.EachSentence;
            Cfg.CommaDecorate = from.CommaDecorate;
            Cfg.Tone = from.Tone;

            Cfg.Openers = from.Openers;
            Cfg.Closers = from.Closers;
            Cfg.QuestionClosers = from.QuestionClosers;
            Cfg.ExclaimClosers = from.ExclaimClosers;
            Cfg.HappyClosers = from.HappyClosers;
            Cfg.SadClosers = from.SadClosers;
            Cfg.AngryClosers = from.AngryClosers;
            Cfg.GreetingClosers = from.GreetingClosers;
            Cfg.FarewellClosers = from.FarewellClosers;
            Cfg.LoveClosers = from.LoveClosers;
            Cfg.PraiseClosers = from.PraiseClosers;
            Cfg.ThanksClosers = from.ThanksClosers;
            Cfg.ApologyClosers = from.ApologyClosers;
            Cfg.CongratClosers = from.CongratClosers;
            Cfg.InviteClosers = from.InviteClosers;
            Cfg.SurpriseClosers = from.SurpriseClosers;
            Cfg.FearClosers = from.FearClosers;
            Cfg.TiredClosers = from.TiredClosers;
            Cfg.BoredClosers = from.BoredClosers;
            Cfg.DisappointedClosers = from.DisappointedClosers;
            Cfg.ComfortClosers = from.ComfortClosers;
            Cfg.CharmClosers = from.CharmClosers;
        }

        public void ReloadRules()
        {
            // ==== language comes from MEMORY, not from the file ====
            //
            // Switching the language writes it to rules.txt through PatchRules,
            // but that write goes through a safety check and can be refused.
            // And a file can only hold one value - only memory knows which
            // language is active right now.
            //
            // Symptom this fixes (user-reported): start in English, switch to
            // Chinese, then click a style - the numbers jump to the English
            // rules first and then back, because that reload read the stale
            // value from disk.
            string langWanted = Cfg != null ? Cfg.Language : null;

            // ★★★ 直接读【该读的那一份】✗ 不要先读 rules.txt 再读 rules-en ★★★
            //
            // ① 原来无条件先读 rules.txt，唯一目的是从文件里拿 language。
            //   但 language 现在【以内存为准】✗ 所以这一步是白读（读盘 + 解析）✓
            // ② 首次启动时内存里没有语言 ✗ 那时才需要读 rules.txt ✓
            //   —— ★ 用户实测："以前只有中文的时候点了立马就刷新"✗ 
            //      英文模式慢就是因为每次重载都多读一遍文件 ✓
            // ★ 永远先读 rules.txt ✗ 全局设置只在那里 ✓
            //   （英文的规则内容稍后从 rules-en.txt 合并进来）
            string prePath = RulesPath;

            Cfg = Config.Load(prePath);
// ★★★ 英文模式下，规则内容改用 rules-en.txt ★★★
            //
            // 为什么语言设置不跟着换文件：那是鸡生蛋 —— 得先知道语言才知道
            // 语言改成以内存为准 —— 见上面的 langNow 说明。
            // 只有【规则内容】按语言换一份。
            //
            // 好处：切语言不会覆盖用户在 rules.txt 里改过的内容 ✓
            //       两份规则各自独立 ✗ 互不干扰 ✓
            // ★ 最终语言 = 调用前内存里的那个（非空时）优先，否则用文件里的 ✓
            //   首次启动：内存是空的 → 听文件的 ✓
            //   切完语言再重载：听内存的 → 不受文件写没写成功影响 ✓
            string langNow = string.IsNullOrEmpty(langWanted)
                ? (string.IsNullOrEmpty(Cfg.Language) ? "zh" : Cfg.Language)
                : langWanted;

            // ★★★ 英文：把 rules-en.txt 的【规则】合并进来 ✗ 全局设置保留 rules.txt 的 ★★★
            //
            // 原来这里是【整份替换成 rules-en.txt】✗ 那会丢掉：
            //     智能语气（→ 内置模型永远不启动 ✓）
            //     模型地址 / 模型名 / 模型超时
            //     转换热键 / 暂停热键 / 重载热键 / 延迟
            //     界面-* 十几项开关
            // 现在只覆盖规则相关的字段 ✗ 其余照旧 ✓
            if (langNow == "en")
            {
                string enPath = Path.Combine(BaseDir, "rules-en.txt");
                if (File.Exists(enPath))
                {
                    Config enCfg = Config.Load(enPath);
                    MergeRulesFrom(enCfg);
                    Log("english rules merged from rules-en.txt (rules only; "
                        + "settings still come from rules.txt)");
                }
                else
                {
                    Log("language=en but rules-en.txt is missing - keeping the Chinese rules");
                }
            }
            // ★ 无论读的哪份 ✗ 都把内存里那个语言写回去 ✓
            //   否则后面任何按 Cfg.Language 判断的代码都会看错 ✓
            Cfg.Language = langNow;
            Log(string.Format("rules loaded: {0} replacements, {1} kaomoji, each={2}, auto={3}",
                Cfg.Rules.Count, Cfg.Kaomoji.Count, Cfg.EachSentence, Cfg.AutoMode));
            // the debounce is what the user configures; the cooldown is derived, so
            // print the real numbers - otherwise a setting can look ignored
            Log("auto timing: debounce=" + Cfg.DebounceMs
                + "ms  cooldown=" + Math.Max(600, Math.Min(1500, Cfg.DebounceMs * 2))
                + "ms  needs 3 separate input events to arm");
        }

        void BuildHotkeys()
        {
            hkForm = new HotkeyForm();
            IntPtr h = hkForm.Handle; // force handle creation
            hkForm.HotkeyPressed += OnHotkey;

            RegisterOne(h, 1, Cfg.HotkeyTransform, "transform");
            RegisterOne(h, 2, Cfg.HotkeyPause, "pause");
            RegisterOne(h, 3, Cfg.HotkeyReload, "reload");
            RegisterOne(h, 4, Cfg.HotkeyUndo, "undo");
        }

        void RegisterOne(IntPtr h, int id, string spec, string what)
        {
            uint mods;
            uint vk;
            if (!Native.ParseHotkey(spec, out mods, out vk))
            {
                Log("hotkey " + what + ": cannot parse [" + spec + "] - fix it in rules.txt");
                return;
            }
            bool ok = Native.RegisterHotKey(h, id, mods, vk);
            if (ok) Log("hotkey " + what + " [" + spec + "] ok");
            else
            {
                HotkeyFailures++;
                Log("hotkey " + what + " [" + spec + "] FAILED - already used by another program; pick another in rules.txt");
            }
        }

        void OnHotkey(int id)
        {
            if (id == 1) DoTransform("hotkey");
            else if (id == 2) { paused = !paused; UpdateTrayText(); Log("paused=" + paused); }
            else if (id == 3) { ReloadRules(); UpdateTrayText(); }
            else if (id == 4) UndoLast();
        }

        void BuildTray()
        {
            tray = new NotifyIcon();
            tray.Icon = Art.AppIcon();
            // ★ 2026-10-18：不再挂系统菜单 ✗ 改成自定义粉色卡片 ✓
            tray.MouseUp += delegate(object s1, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
                    ShowTrayPanel();
            };
            tray.DoubleClick += delegate { if (Window != null) Window.ShowWindow(); };
            tray.Visible = true;
            UpdateTrayText();
        }

        ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            // ★ 2026-10-18：粉色主题 ✗ 和主界面统一 ✓
            m.Renderer = new PinkMenuRenderer();
            m.BackColor = Theme.Card;
            m.ForeColor = Theme.Text;
            m.Font = Theme.Body;
            m.ShowImageMargin = false;
            m.Padding = new Padding(4, 6, 4, 6);

            ToolStripMenuItem miOpen = new ToolStripMenuItem(Lang.T("打开主界面"));
            miOpen.Click += delegate { if (Window != null) Window.ShowWindow(); };
            m.Items.Add(miOpen);

            m.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem miPause = new ToolStripMenuItem("暂停 / 恢复        " + Cfg.HotkeyPause);
            miPause.Click += delegate { SetPaused(!paused); };
            m.Items.Add(miPause);

            ToolStripMenuItem miNow = new ToolStripMenuItem("立即转换当前输入框  " + Cfg.HotkeyTransform);
            miNow.Click += delegate { DoTransform("menu"); };
            m.Items.Add(miNow);

            m.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem miAuto = new ToolStripMenuItem(Lang.T("自动模式（停止打字后自动转换）"));
            miAuto.Checked = Cfg.AutoMode;
            miAuto.Click += delegate(object s, EventArgs e)
            {
                SetAutoMode(!Cfg.AutoMode);
                ((ToolStripMenuItem)s).Checked = Cfg.AutoMode;
            };
            m.Items.Add(miAuto);

            ToolStripMenuItem miReload = new ToolStripMenuItem("重新加载规则        " + Cfg.HotkeyReload);
            miReload.Click += delegate { ReloadRules(); };
            m.Items.Add(miReload);

            // ★ 2026-10-18：配置导出 / 导入 ✧ 换电脑探一下就行 ✓
            ToolStripMenuItem miExport = new ToolStripMenuItem(Lang.T("导出配置…"));
            miExport.Click += delegate { ExportConfig(); };
            m.Items.Add(miExport);
            ToolStripMenuItem miImport = new ToolStripMenuItem(Lang.T("导入配置…"));
            miImport.Click += delegate { ImportConfig(); };
            m.Items.Add(miImport);

            ToolStripMenuItem miEdit = new ToolStripMenuItem(Lang.T("编辑规则 rules.txt…"));
            miEdit.Click += delegate { try { System.Diagnostics.Process.Start("notepad.exe", "\"" + RulesPath + "\""); } catch { } };
            m.Items.Add(miEdit);

            m.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem miRun = new ToolStripMenuItem(Lang.T("开机自动启动"));
            miRun.Checked = IsAutoRunEnabled();
            miRun.Click += delegate(object s, EventArgs e)
            {
                SetAutoRun(!IsAutoRunEnabled());
                ((ToolStripMenuItem)s).Checked = IsAutoRunEnabled();
                Log("autorun=" + IsAutoRunEnabled());
                if (Window != null) Window.SyncCheckboxes();
            };
            m.Items.Add(miRun);

            ToolStripMenuItem miLog = new ToolStripMenuItem(Lang.T("查看日志…"));
            miLog.Click += delegate { OpenLogInNotepad(); };
            m.Items.Add(miLog);

            m.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem miExit = new ToolStripMenuItem(Lang.T("退出"));
            miExit.Click += delegate { ExitApp(); };
            m.Items.Add(miExit);

            return m;
        }

        /// ★★★ 2026-10-18：粉色卡片托盘（参考 GPT 设计稿）★★★
        ///   原来用 ContextMenuStrip ✗ 换成一个无边框圆角小窗口 ✓
        TrayPanel trayPanel;

        void ShowTrayPanel()
        {
            if (trayPanel != null && trayPanel.Visible) { trayPanel.Close(); return; }

            TrayPanel p = new TrayPanel("\u55B5\u5316\u52A9\u624B",
                                       paused ? Lang.T("\u5DF2\u6682\u505C") : Lang.T("\u8FD0\u884C\u4E2D"));
            if (paused) p.SetState(Lang.T("\u5DF2\u6682\u505C"), Theme.Muted);

            p.AddItem("▶", Lang.T("\u6253\u5F00\u4E3B\u754C\u9762"), "", delegate
            {
                p.Close();
                if (Window != null) Window.ShowWindow();
            });
            p.AddSeparator();

            p.AddItem(paused ? "⏵" : "⏸", Lang.T("\u6682\u505C / \u6062\u590D"),
                      Cfg.HotkeyPause, delegate
            {
                p.Close();
                SetPaused(!paused);
            });

            p.AddItem("⚡", Lang.T("\u7ACB\u5373\u8F6C\u6362\u5F53\u524D\u8F93\u5165\u6846"),
                      Cfg.HotkeyTransform, delegate
            {
                p.Close();
                DoTransform("menu");
            });

            ToggleRow2 autoRow = p.AddToggle("↻", Lang.T("\u81EA\u52A8\u6A21\u5F0F"),
                      Cfg.AutoMode, delegate
            {
                SetAutoMode(!Cfg.AutoMode);
            });

            p.AddItem("♻", Lang.T("\u91CD\u65B0\u52A0\u8F7D\u89C4\u5219"),
                      Cfg.HotkeyReload, delegate
            {
                p.Close();
                ReloadRules();
                UpdateTrayText();
            });

            p.AddSeparator();

            p.AddItem("📤", Lang.T("\u5BFC\u51FA\u914D\u7F6E\u2026"), "", delegate
            {
                p.Close();
                ExportConfig();
            });
            p.AddItem("📥", Lang.T("\u5BFC\u5165\u914D\u7F6E\u2026"), "", delegate
            {
                p.Close();
                ImportConfig();
            });
            p.AddItem("📝", Lang.T("\u7F16\u8F91\u89C4\u5219 rules.txt\u2026"), "", delegate
            {
                p.Close();
                try { System.Diagnostics.Process.Start("notepad.exe", "\"" + RulesPath + "\""); } catch { }
            });

            p.AddSeparator();

            ToggleRow2 runRow = p.AddToggle("🚀", Lang.T("\u5F00\u673A\u81EA\u52A8\u542F\u52A8"),
                      IsAutoRunEnabled(), delegate
            {
                SetAutoRun(!IsAutoRunEnabled());
                Log("autorun=" + IsAutoRunEnabled());
                if (Window != null) Window.SyncCheckboxes();
            });

            p.AddItem("📄", Lang.T("\u67E5\u770B\u65E5\u5FD7\u2026"), "", delegate
            {
                p.Close();
                OpenLogInNotepad();
            });

            p.AddSeparator();

            p.AddItem("⏻", Lang.T("\u9000\u51FA"), "", delegate
            {
                p.Close();
                ExitApp();
            });

            p.Finish(Cursor.Position);
            trayPanel = p;
            p.Show();
        }

        void UpdateTrayText()
        {
            if (tray == null) return;
            tray.Text = "\u55B5\u5316\u52A9\u624B " + Program.Version +
                        (paused ? " \u00B7 \u5DF2\u6682\u505C" : " \u00B7 \u8FD0\u884C\u4E2D");
        }

        public void ExitApp()
        {
            ReallyExiting = true;
            StopModelServer();   // never leave a 2 GB llama-server behind
            try { timer.Stop(); } catch { }
            try { if (tray != null) { tray.Visible = false; tray.Dispose(); } } catch { }
            try
            {
                IntPtr h = hkForm.Handle;
                Native.UnregisterHotKey(h, 1);
                Native.UnregisterHotKey(h, 2);
                Native.UnregisterHotKey(h, 3);
            }
            catch { }
            Application.Exit();
        }

        void OnTick(object sender, EventArgs e)
        {
            if (paused || !Cfg.AutoMode || Cfg.Whitelist.Count == 0) return;

            // \u2605 2026-10-18\uff1a\u8fd9\u4e2a\u8f6f\u4ef6\u91cc\u5173\u4e86\u81ea\u52a8\u5c31\u4e0d\u8f6c \u2717 \u5feb\u6377\u952e\u8fd8\u80fd\u7528 \u2713
            if (Cfg.ProcAuto.Count > 0)
            {
                try
                {
                    string fpn = Native.ForegroundProcessName();
                    if (!string.IsNullOrEmpty(fpn))
                    {
                        bool allow = true;
                        if (Cfg.ProcAuto.TryGetValue(fpn, out allow) && !allow) return;
                    }
                }
                catch { }
            }

            int now = Environment.TickCount;
            int sinceLastTick = now - lastTickAt;
            lastTickAt = now;
            if (sinceLastTick <= 0) sinceLastTick = 100;

            // polled every tick so the "pressed since last query" flags stay current
            bool edited = Native.EditKeyPressedSinceLastPoll();
            bool mouseButton = Native.MouseButtonPressedSinceLastPoll();

            int cx, cy;
            bool cursorMoved = false;
            if (Native.TryGetCursor(out cx, out cy))
            {
                if (lastCursorX != int.MinValue && (cx != lastCursorX || cy != lastCursorY))
                    cursorMoved = true;
                lastCursorX = cx;
                lastCursorY = cy;
            }

            // Which app is in front? GetForegroundWindow is cheap; resolving the
            // process name opens a handle, so only do that when the window changes.
            IntPtr fgWnd = Native.GetForegroundWindow();
            if (fgWnd != lastFgWnd)
            {
                lastFgWnd = fgWnd;
                lastFgProc = Native.ForegroundProcessName();
            }

            bool inList = false;
            foreach (string w in Cfg.Whitelist)
                if (string.Equals(w, lastFgProc, StringComparison.OrdinalIgnoreCase)) { inList = true; break; }

            if (!inList)
            {
                armed = false;
                typeTicks = 0;
                return;
            }

            // \u2605\u2605\u2605 2026-10-18\uff1a\u521a\u8f6c\u6362\u5b8c\u7684 3 \u79d2\u5185\u4e0d\u51c6\u91cd\u65b0\u6b66\u88c5 \u2605\u2605\u2605
            //   \u7c98\u8d34\u4e5f\u4f1a\u4ea7\u751f\u8f93\u5165\u4e8b\u4ef6 \u2717 \u88ab\u5f53\u6210\u300c\u7528\u6237\u5728\u6253\u5b57\u300d\u2717
            //   \u4e8e\u662f\u8f6c\u5b8c\u81ea\u5df1\u7684\u8f93\u51fa\u53c8\u88ab\u8f6c\u4e00\u904d \u2717 \u65e0\u9650\u53e0\u52a0 \u2713
            if (lastTransformTick != 0 && now - lastTransformTick < 3000)
            {
                armed = false;
                typeTicks = 0;
                return;
            }

            uint idle = Native.IdleMilliseconds();

            // Did NEW input arrive since the previous poll? Comparing idle against the
            // poll gap answers that exactly: if the newest input is older than the gap,
            // nothing has happened since we last looked.
            //
            // The earlier version instead counted polls that fell inside an "activity
            // window". That was wrong: ONE keystroke keeps idle below a 300ms window
            // for three consecutive 100ms polls, so a single Alt-Tab or a mouse click
            // already looked like three typing samples and fired the tool - which is
            // exactly the "I only switched to QQ and it copied" report.
            if (idle <= (uint)sinceLastTick)
            {
                // Ignore input WE injected. Ctrl+A / Ctrl+C / End reset the idle timer
                // just like real keys, and counting them armed the trigger again right
                // after it ran - a self-sustaining select / deselect loop.
                // ★★★ 这 900ms 是"我们自己刚注入过按键"的窗口 ★★★
                //
                // 本意：别把我们自己发的 Ctrl+A / Ctrl+C / Ctrl+V
                //       当成用户在打字 ✧ 不然会自己把自己唤醒 ✓
                // 但用户在这段时间里敲的键 ✧ 也落在同一个窗口里 ✧ 一起被忽略了 ✓
                //   —— 表现就是"刚转换完马上补一个字 ✧ 他不识别 ✧ 要重新输入过" ✓
                //
                // 区分办法：比一下两个时间 ✓
                //   idle                     = 距离【最近一次输入】过了多久
                //   now - LastSyntheticInput = 距离【我们最后一次注入】过了多久
                //   idle 更小 → 最近这次输入发生在我们注入【之后】✧ 那就是用户敲的 ✓
                if (now - Native.LastSyntheticInput < 900
                    && idle >= (uint)(now - Native.LastSyntheticInput)) return;

                // Clicking around, moving the mouse, or fixing text up is not composing.
                // Firing then is destructive, because the transform starts with Ctrl+A.
                if (edited || cursorMoved || mouseButton)
                {
                    armed = false;
                    typeTicks = 0;
                    return;
                }
                if (++typeTicks >= 3) armed = true;   // real typing produces many events
                return;
            }

            if (!armed) return;
            // ★ 2026-10-18：智能延迟 ✧ 短句快转 ✧ 长句多等 ✓
            int effDelay = Cfg.DebounceMs;
            if (Cfg.SmartDelay)
            {
                // typeTicks ≈ 这次输入持续了几个 100ms tick
                //   少 = 短句 ✧ 多 = 长句
                if (typeTicks <= 3) effDelay = effDelay * 60 / 100;
                else if (typeTicks >= 10) effDelay = effDelay * 130 / 100;
                else if (typeTicks >= 6) effDelay = effDelay * 105 / 100;
                if (effDelay < 200) effDelay = 200;
                if (effDelay > 2000) effDelay = 2000;
            }
            if (idle < (uint)effDelay) return;

            // ★★★ 先过所有闸门 ✧ 都通过了才消耗 armed ★★★
            //
            // 原来是先 armed = false ✧ 再看冷却 / settle ✓
            // 于是被挡掉的那一次 ✧ armed 已经没了 ✧ 不会重试 ✓
            //   —— 用户刚转换完马上补字 ✧ 触发时还在
            //      settle（900ms）或冷却（最长 1400ms）里 ✧ 这一次就白等了 ✓
            // —— 现在：还没到时就留着 armed ✧ 100ms 后再来看 ✓

            // cooldown between two automatic conversions
            int cooldown = Math.Max(3000, Math.Min(8000, Cfg.DebounceMs * 3));   // \u2605 \u2265 3 \u79d2 \u2713
            if (now - lastTransformTick < cooldown) return;

            // 上一次还没落定 ✧ 也留着 armed ✓
            if (transforming) return;

            // ★★★ 正在打拼音 ✧ 【不消耗 armed】★★★
            //
            // 用户 2026-10-17 实测报的问题：
            //   拼音中途被拦下了 ✓ ✅
            //   但 armed 也被消耗掉了 ✗ ❌
            //   上屏只产生【1 次】输入事件 ✧
            //   而武装要求【连续 3 次】✧ 于是再也武装不起来 ✧ 再也不转了 ✗
            //
            // 所以把闸门【挪到消耗 armed 之前】✧
            //   被拦下时 armed 还在 ✧ 等上屏了还能接着转 ✓✓✓
            if (ShouldSkipForComposing("auto")) return;

            armed = false;
            typeTicks = 0;

            DoTransform("auto");
        }

        // ---- the actual in-place transform ----

        // Undo the Ctrl+A we use to read the box.
        //
        // Leaving the whole box selected is dangerous: the user's next keystroke - a
        // Backspace while fixing a typo, say - then deletes EVERYTHING instead of one
        // character. Every abort path that already sent Ctrl+A must call this.
        /// ★ 看起来像【机器用的编号】吗？
        ///
        /// 这类东西是给程序读的 ✗ 不是给人读的 ✗ 喵化会把它破坏掉 ✓
        ///    #121417         —— 井号 + 数字（游戏内输入法工具的产物）
        ///    1234567         —— 一长串纯数字（验证码 / 订单号之类）
        ///    AB-12345        —— 字母打头的编号
        static bool LooksLikeMachineCode(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            string t = s.Trim();
            if (t.Length < 4) return false;
            try
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^[#＃][0-9]{3,}$")) return true;
                if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^[0-9]{4,}$")) return true;
                if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^[A-Za-z]{1,4}[-_#][0-9]{3,}$")) return true;
            }
            catch { }
            return false;
        }
        /// ★★★ 用 UIA 判断输入框里有没有图片 ★★★
        ///
        /// 为什么需要：剪贴板那条路行不通 —— QQ 的输入框【不把图片放进剪贴板】
        /// （复制不行，剪切也不行）。但用户实测【讲述人能区分】两种情况：
        ///     只有文字 → 念出文字
        ///     有图片   → 只念"选中"，读不到内容
        /// 那就说明 UIA 里有这个信息。dump 出两棵树一对比，答案非常干净：
        ///
        ///   只有文字：                    有图片：
        ///     Group []                      Group []
        ///       Text  [ ]                     Text  []          ← 空了
        ///       Text  [你好]                  ★ Group [] ★      ← 凭空多一个
        ///                                     Text  [ ]
        ///
        /// —— ★ 那个多出来的【空 Group】就是图片（或别的非文字附件）✓
        ///
        /// 怎么定位到输入框：找 Name=[发送] 那个按钮，
        /// 往上两层到"发送区"，再取它的【前一个兄弟】就是输入区。
        /// （dump 里看得很清楚：输入区和发送区是同一层的兄弟 ✓）
        ///
        /// ★ 读不到任何东西时一律返回 false（不拦）——
        ///   别的软件没有"发送"按钮，不能因为读不到就不让人用 ✓
        /// ★ 把一个 UIA 子树打成一行行文字（诊断用）。
        /// ★ 往下找"真正的输入区"。
        ///
        /// 从"发送"按钮往上两层拿到的是个【壳】✗ 真正的输入区在它下面 ✓
        /// 判据：子元素里【有 Text】的那个 Group ✓
        /// （层数不固定 ✗ 用户实测有时候一层有时候两层 ✓ 所以递归找 ✓）
        System.Windows.Automation.AutomationElement FindInnerBox(
            System.Windows.Automation.AutomationElement el, int depth)
        {
            if (el == null || depth > 4) return null;
            try
            {
                System.Windows.Automation.AutomationElementCollection kids =
                    el.FindAll(System.Windows.Automation.TreeScope.Children,
                               System.Windows.Automation.Condition.TrueCondition);
                // 先看自己这一层：子元素里有 Text 就是它
                foreach (System.Windows.Automation.AutomationElement c in kids)
                {
                    if (c.Current.ControlType == System.Windows.Automation.ControlType.Text)
                        return el;
                }
                // 没有就往下钻
                foreach (System.Windows.Automation.AutomationElement c in kids)
                {
                    if (c.Current.ControlType != System.Windows.Automation.ControlType.Group) continue;
                    System.Windows.Automation.AutomationElement r = FindInnerBox(c, depth + 1);
                    if (r != null) return r;
                }
            }
            catch { }
            return null;
        }
        void DumpUiaSub(System.Windows.Automation.AutomationElement el, int depth,
                        StringBuilder sb, int budget)
        {
            if (el == null || depth > 6 || budget <= 0) return;
            try
            {
                System.Windows.Automation.AutomationElementCollection kids =
                    el.FindAll(System.Windows.Automation.TreeScope.Children,
                               System.Windows.Automation.Condition.TrueCondition);
                foreach (System.Windows.Automation.AutomationElement c in kids)
                {
                    if (budget-- <= 0) return;
                    string nm = "";
                    try { nm = c.Current.Name; } catch { }
                    if (nm != null && nm.Length > 24) nm = nm.Substring(0, 24) + "…";
                    sb.Append("\r\n" + new string(' ', (depth + 1) * 2)
                              + c.Current.ControlType.ProgrammaticName + " [" + nm + "]");
                    DumpUiaSub(c, depth + 1, sb, budget);
                }
            }
            catch { }
        }
        bool UiaSaysHasAttachment(IntPtr hwnd)
        {
            try
            {
                System.Windows.Automation.AutomationElement root =
                    System.Windows.Automation.AutomationElement.FromHandle(hwnd);
                if (root == null) return false;

                System.Windows.Automation.AutomationElement send =
                    root.FindFirst(System.Windows.Automation.TreeScope.Descendants,
                        new System.Windows.Automation.PropertyCondition(
                            System.Windows.Automation.AutomationElement.NameProperty, Lang.T("发送")));
                if (send == null) return false;   // 不是聊天软件 ✗ 不拦

                System.Windows.Automation.TreeWalker wk =
                    System.Windows.Automation.TreeWalker.ControlViewWalker;
                System.Windows.Automation.AutomationElement p1 = wk.GetParent(send);
                if (p1 == null) return false;
                System.Windows.Automation.AutomationElement sendArea = wk.GetParent(p1);
                if (sendArea == null) return false;
                System.Windows.Automation.AutomationElement box = wk.GetPreviousSibling(sendArea);
                if (box == null) return false;

                System.Windows.Automation.AutomationElementCollection kids =
                    box.FindAll(System.Windows.Automation.TreeScope.Children,
                                System.Windows.Automation.Condition.TrueCondition);
                int groups = 0, texts = 0;
                foreach (System.Windows.Automation.AutomationElement c in kids)
                {
                    if (c.Current.ControlType == System.Windows.Automation.ControlType.Group)
                        groups++;
                    else if (c.Current.ControlType == System.Windows.Automation.ControlType.Text)
                        texts++;
                }
                // ★ 只有文字时是"两个 Text ✗ 零个 Group"✓
                //   有图片时会多出一个空 Group ✗ 而且 Text 少一个 ✓
                // ★★★ 往下找【真正的】输入区 ★★★
                //
                // 从"发送"按钮往上两层拿到的那一层只是【壳】——
                // 用户实测的日志：
                //     候选输入区 Group children=1 groups=1 texts=0
                //     它的子树 → Group [] | Group [] | Text [你好]
                //   真正的输入区是【再下面那一层】（子元素里有 Text 的那个 ✓）
                //   而且层数会变：有时候一层 ✗ 有时候两层 ✓
                //   → 所以递归往下找 ✗ 找【第一个子元素里含 Text 的 Group】✓
                System.Windows.Automation.AutomationElement inner = FindInnerBox(box, 0);
                if (inner == null)
                {
                    Log("uia: 没找到真正的输入区（结构可能变了），跳过这个检查。");
                    return false;
                }

                System.Windows.Automation.AutomationElementCollection ik =
                    inner.FindAll(System.Windows.Automation.TreeScope.Children,
                                  System.Windows.Automation.Condition.TrueCondition);
                int ig = 0, it = 0;
                foreach (System.Windows.Automation.AutomationElement c in ik)
                {
                    if (c.Current.ControlType == System.Windows.Automation.ControlType.Group) ig++;
                    else if (c.Current.ControlType == System.Windows.Automation.ControlType.Text) it++;
                }
                Log("uia: 真正的输入区 children=" + ik.Count + " groups=" + ig + " texts=" + it);
                if (ig > 0)
                {
                    Log("uia: ★ 输入区里有 " + ig + " 个 Group 子元素 —— 按“有图片或附件”处理。");
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                Log("uia: 检查出错（" + e.Message + "），跳过这个检查。");
                return false;
            }
        }

        void CollapseSelection()
        {
            try { Native.SendKey(Native.VK_END); Thread.Sleep(50); } catch { }
        }

        /// QQ renders an @mention as a rich ATOM, not as ordinary characters. The normal
        /// Ctrl+A / Ctrl+C / Ctrl+A / Ctrl+V cycle flattens it into plain "@name" text, the
        /// mention stops being a mention, and the reply linkage is lost.
        ///
        /// So when the box content starts with "@", the caret is walked past the leading
        /// mention and only the REMAINDER is selected, copied and pasted back over itself -
        /// leaving the atom untouched.
        ///
        /// How many Right presses the atom costs is not knowable from here: the editor may
        /// treat it as one unit or as its individual characters. Up to four are tried, and
        /// every candidate is CHECKED before it is trusted: the copied text has to be a
        /// strict suffix of the whole box content. A wrong guess would silently delete part
        /// of the user's sentence, so nothing is pasted unless that test passes.
        ///
        /// Returns the body text (with exactly that range selected), or null to fall back
        /// to the plain select-everything path.
        string TrySelectAfterMention(string whole)
        {
            for (int k = 1; k <= 4; k++)
            {
                Native.SendKey(Native.VK_HOME);
                Thread.Sleep(60);
                for (int i = 0; i < k; i++)
                {
                    Native.SendKey(Native.VK_RIGHT);
                    Thread.Sleep(30);
                }
                Native.SendShift(Native.VK_END);
                Thread.Sleep(90);

                try { Clipboard.SetText(ClipboardMarker); } catch { }
                Thread.Sleep(40);
                Native.SendCtrl(Native.VK_C);
                Thread.Sleep(170);

                string t1 = null;
                try { if (Clipboard.ContainsText()) t1 = Clipboard.GetText(); }
                catch { }
                if (string.IsNullOrEmpty(t1) || t1 == ClipboardMarker) continue;
                if (t1.Length >= whole.Length) continue;
                if (!whole.EndsWith(t1, StringComparison.Ordinal)) continue;

                Log("@mention: left the first " + (whole.Length - t1.Length) +
                    " char(s) in place (caret moved " + k + " step(s)); mention kept as a mention");
                return t1;
            }

            Log("@mention: no safe split found, so the mention cannot be protected this time - " +
                "falling back to selecting everything");
            return null;
        }

        /// Shortens text for the log without hiding its shape - quotes and length kept, middle
        /// elided. The old log only printed character counts, which is useless for diagnosing a
        /// duplication: "transformed 2 -> 12 chars" looks the same either way.
        public static string Brief(string s)
        {
            if (s == null) return "null";
            s = s.Replace("\r", "").Replace("\n", "\u21B5");
            // Long enough to hold a full decorated sentence, because diagnosing a duplication
            // means seeing the WHOLE read and write strings, not a summary of them.
            if (s.Length <= 220) return s;
            return s.Substring(0, 110) + "\u2026(" + s.Length + ")" + s.Substring(s.Length - 80);
        }

        // A paste is handled by the OTHER application after DoTransform has already returned,
        // so a trigger arriving in that gap reads the box as it was BEFORE the paste. It then
        // computes a second result and pastes that too - and because the first paste collapsed
        // the selection, the second one INSERTS instead of replacing:
        //     你好喵~ฅ^•ﻌ•^ฅ你好喵~(๑•̀ㅂ•́)و✧
        // Two DIFFERENT kaomoji in one line is the signature: two runs, each reading 你好.
        // Every Ctrl+A was correct; the two runs simply overlapped. The window stays shut
        // briefly after the paste so the editor has finished before anything reads again.
        volatile bool transforming;
        System.Windows.Forms.Timer settleTimer;

        void BeginSettle()
        {
            if (settleTimer == null)
            {
                settleTimer = new System.Windows.Forms.Timer();
                settleTimer.Interval = 900;
                settleTimer.Tick += delegate { settleTimer.Stop(); transforming = false; };
            }
            settleTimer.Stop();
            settleTimer.Start();
        }

void DoTransform(string why)
        {
            // ★ 刚撤销完 ✧ auto 不许动手（手动热键照常 ✓）
            if (why == "auto" && Environment.TickCount < suppressAutoUntil)
            {
                Log("auto: 刚撤销过，静默中，跳过");
                return;
            }
            if (transforming)
            {
                Log(why + ": 上一次转换还没落定，跳过（防止两次粘贴叠在一起）");
                return;
            }

            // ★ 最多来两轮。
            //
            // 为什么要循环：读完到写完之间，用户可能还在打字，
            // 或者系统正在补全 @提及 —— 那一步会把框里的内容改掉。
            // 老代码遇到这种就直接跳过 ✗ 表现是"第一次按没反应，再按一次才行"。
            // 现在改成：内容变了就按【新内容】重新来一遍 ✗ 最多两次。
            for (int round = 0; round < 3; round++)
            {
                contentChanged = false;
                transforming = true;
                try { DoTransformCore(why); }
                finally
                {
                    // held open only when something was actually written; an early return means
                    // the box was never touched and the next trigger can go straight through
                    if (!settleArmed) transforming = false;
                    settleArmed = false;
                }
                if (!contentChanged) break;
                Log(why + ": 框里的内容在我们处理期间变了，按新内容再来一次（第 "
                    + (round + 1) + Lang.T(" 次）"));
                Thread.Sleep(160);
            }
        }


        /// ★ "框里的内容在我们读完以后又变了" —— 由 DoTransformCore 设置。
        ///
        /// 用户实测的场景：正在打 @提及，系统异步把选中的名字插进去，
        /// 而这一步发生在【我们读完】之后。原来会直接跳过 → 表现是"第一次不生效"。
        bool contentChanged;
        bool settleArmed;

        /// ★ 用 UIA 直接读一眼输入框 ✧ 不动键盘 ✧ 不动剪贴板 ✓
        /// —— ★ 只用来判断【要不要跳过】
        ///    不用它的内容去转换 ✧ 读错了最多漏转一次 ✧ 不会转错 ✓
        static bool TryPeekBoxViaUia(out string text)
        {
            text = null;
            try
            {
                System.Windows.Automation.AutomationElement el =
                    System.Windows.Automation.AutomationElement.FocusedElement;
                if (el == null) return false;

                // ValuePattern 优先（普通输入框 ✓）
                try
                {
                    object v = el.GetCurrentPattern(
                        System.Windows.Automation.ValuePattern.Pattern);
                    if (v != null)
                    {
                        string t = ((System.Windows.Automation.ValuePattern)v).Current.Value;
                        if (t != null) { text = t; return true; }
                    }
                }
                catch { }

                // TextPattern 兜底（contenteditable 这类 ✧ QQ 就是 ✓）
                try
                {
                    var tp = el.GetCurrentPattern(
                        System.Windows.Automation.TextPattern.Pattern)
                        as System.Windows.Automation.TextPattern;
                    if (tp != null)
                    {
                        var r = tp.DocumentRange;
                        string t = r.GetText(4000);
                        if (t != null) { text = t; return true; }
                    }
                }
                catch { }
            }
            catch { }
            return false;
        }

        /// ★ 尾巴像拼音吗（ni / ni'hao / hao3 … ✧ 还没上屏 ✓）
        static bool LooksLikePinyinTail(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            int i = s.Length;
            while (i > 0)
            {
                char c = s[i - 1];
                if (c == '\'' || c == ' ' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9'))
                    i--;
                else break;
            }
            int run = s.Length - i;
            // 尾巴至少有 1 个字母/数字 ✧ 且整句里有中文 ✓
            //   （没中文 = 英文模式 ✧ 不能这么判 ✗）
            if (run == 0) return false;
            bool hasCjk = false;
            for (int k = 0; k < s.Length && !hasCjk; k++)
                if (s[k] >= '\u4e00' && s[k] <= '\u9fff') hasCjk = true;
            if (!hasCjk) return false;
            // 纯数字尾巴可能是验证码 ✧ 不算拼音 ✓
            bool hasLetter = false;
            for (int k = i; k < s.Length; k++)
            {
                char c = s[k];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) { hasLetter = true; break; }
            }
            return hasLetter;
        }

        /// ★ 现在不该动手吗 ✧ 返回 true = 一个键都不碰 ✓
        bool ShouldSkipForComposing(string why)
        {
            // ① Windows 正规接口
            if (Native.IsImeComposing())
            {
                Log(why + ": 输入法正在打字（IME 组合中），不动");
                return true;
            }

            // ② UIA 看一眼尾巴（读不到就当没这条 ✓）
            string probe;
            if (TryPeekBoxViaUia(out probe) && probe != null)
            {
                string t = probe.Replace("\r", "").Replace("\n", "");
                if (LooksLikePinyinTail(t))
                {
                    Log(why + ": 框里尾巴像拼音，不动（" + Brief(t) + "）");
                    return true;
                }
            }
            return false;
        }

        /// ★ 配置导出（2026-10-18）
        ///   把规则 + 风格文件打成一个 zip ✗ 换电脑直接导入 ✓
        public void ExportConfig()
        {
            try
            {
                string dir = Path.GetDirectoryName(Application.ExecutablePath);
                string name = "喵化助手_配置_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".zip";
                SaveFileDialog dlg = new SaveFileDialog();
                dlg.Filter = "配置包 (*.zip)|*.zip";
                dlg.FileName = name;
                dlg.Title = Lang.T("导出配置（规则 + 风格）");
                if (dlg.ShowDialog() != DialogResult.OK) return;

                if (File.Exists(dlg.FileName)) File.Delete(dlg.FileName);
                System.IO.Compression.ZipFile.CreateFromDirectory(dir, dlg.FileName,
                    System.IO.Compression.CompressionLevel.Optimal, true);
                Log("export: " + dlg.FileName);
                MessageBox.Show(Lang.T("已导出：") + "\r\n" + dlg.FileName,
                    Lang.T("导出成功"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("export failed: " + ex.Message);
                MessageBox.Show(Lang.T("导出失败：") + "\r\n" + ex.Message,
                    Lang.T("导出配置"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// ★ 配置导入（2026-10-18）
        ///   解压到程序目录 ✗ 覆盖 rules / style ✗ 然后重载 ✓
        public void ImportConfig()
        {
            try
            {
                OpenFileDialog dlg = new OpenFileDialog();
                dlg.Filter = "配置包 (*.zip)|*.zip";
                dlg.Title = Lang.T("导入配置（规则 + 风格）");
                if (dlg.ShowDialog() != DialogResult.OK) return;

                if (MessageBox.Show(
                        Lang.T("导入会覆盖当前的规则和风格文件，确定吗？"),
                        Lang.T("导入配置"), MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning) != DialogResult.Yes) return;

                string dir = Path.GetDirectoryName(Application.ExecutablePath);
                // ★ 先解到临时目录 ✗ 再逐个覆盖 ✗ 免得把不相关的文件也糊上去 ✓
                string tmp = Path.Combine(Path.GetTempPath(), "miaohua-cfg-" + Guid.NewGuid().ToString("N"));
                System.IO.Compression.ZipFile.ExtractToDirectory(dlg.FileName, tmp);

                int copied = 0;
                foreach (string f in Directory.GetFiles(tmp, "*.txt", SearchOption.AllDirectories))
                {
                    string fn = Path.GetFileName(f);
                    if (fn != "rules.txt" && fn != "rules-en.txt"
                        && !fn.StartsWith("style-")) continue;
                    File.Copy(f, Path.Combine(dir, fn), true);
                    copied++;
                }
                try { Directory.Delete(tmp, true); } catch { }

                ReloadRules();
                Log("import: " + dlg.FileName + " (" + copied + " files)");
                MessageBox.Show(Lang.T("已导入 ") + copied + Lang.T(" 个文件，规则已重新加载。"),
                    Lang.T("导入成功"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("import failed: " + ex.Message);
                MessageBox.Show(Lang.T("导入失败：") + "\r\n" + ex.Message,
                    Lang.T("导入配置"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// ★ 撤销用的两个值（2026-10-18）
        ///   undoFrom = 转换前的原文 ✗ undoTo = 转换后写进去的东西
        ///   —— ★ 只有【框里还是 undoTo】时才还原 ✗ 用户又打字了就不碰 ✓
        string undoFrom, undoTo;

        /// ★ 2026-10-18：撤销后的静默期
        ///   原因：撤销是“粘贴”进去的 ✧ 粘贴本身就是一次输入事件 ✗
        ///   —— 自动转换数到 3 次就会重新开火 ✧ 结果就是“撤销完过一会儿又转了” ✓
        ///   —— 所以撤销后给 auto 一段不动手的窗口 ✓
        int suppressAutoUntil = 0;

        void UndoLast()
        {
            if (string.IsNullOrEmpty(undoFrom))
            {
                Log("undo: 还没有可撤销的");
                return;
            }
            IntPtr fg = Native.GetForegroundWindow();
            if (fg == IntPtr.Zero) { Log("undo: no foreground window"); return; }
            if (Native.ForegroundProcessId() == System.Diagnostics.Process.GetCurrentProcess().Id)
            {
                Log("undo: foreground is this program's own window, skipped");
                return;
            }

            object backup = GrabClipboard();
            try
            {
                Native.WaitForModifiersReleased();

                Clipboard.SetText(ClipboardMarker);
                Thread.Sleep(60);
                Native.SendCtrl(Native.VK_A);
                Thread.Sleep(80);
                Native.SendCtrl(Native.VK_C);
                Thread.Sleep(180);

                string now = null;
                try { if (Clipboard.ContainsText()) now = Clipboard.GetText(); }
                catch { }

                if (now == ClipboardMarker || string.IsNullOrEmpty(now))
                {
                    RestoreClipboard(backup);
                    CollapseSelection();
                    Log("undo: 读不到框里的内容，跳过");
                    return;
                }
                if (now != undoTo)
                {
                    RestoreClipboard(backup);
                    CollapseSelection();
                    Log("undo: 框里的内容变了（现在是 [" + Brief(now) + "]），不撤销");
                    return;
                }

                if (!SetClipboardText(undoFrom))
                {
                    RestoreClipboard(backup);
                    CollapseSelection();
                    Log("undo: 写剪贴板失败");
                    return;
                }
                Native.SendCtrl(Native.VK_V);
                Thread.Sleep(300);
                RestoreClipboard(backup);
                Log("undo: 已还原成 [" + Brief(undoFrom) + "]");

                // ★ 撤过一次就清掉 ✗ 免得连按两次撤到更早的内容 ✓
                undoFrom = null;
                undoTo = null;
                // ★ 不要再 arm settle ✧ 直接给 auto 5 秒静默 ✓
                suppressAutoUntil = Environment.TickCount + 5000;
            }
            catch (Exception ex)
            {
                try { RestoreClipboard(backup); } catch { }
                Log("undo: " + ex.Message);
            }
        }

        void DoTransformCore(string why)
        {
            // ★ 2026-11-01：记下来源 ✗ 自动转换不等 AI ✓
            Engine.ManualTrigger = (why == "hotkey");
            lastTransformTick = Environment.TickCount;
            // ★★★ 先看一眼该不该动手 ✧ 不该就一个键都不碰 ★★★
            //   —— ★ 必须在 Ctrl+A 之前判 ✧ 不然光标已经跳了 ✗
            if (ShouldSkipForComposing(why)) return;

            IntPtr fg = Native.GetForegroundWindow();
            if (fg == IntPtr.Zero) { Log(why + ": no foreground window"); return; }

            // ★ 每次都把前台窗口的身份记下来。
            //   手动按快捷键这条路以前【不打进程名】—— 出了问题根本看不出
            //   当时焦点在哪个窗口上。这一行是排查"某个窗口里用不了"的第一手资料。
            Log(why + ": 前台窗口 " + Native.ForegroundWindowInfo());

            // ★ 这里【故意不碰输入焦点】。
            //   试过 AttachThreadInput 把两个线程的输入队列挂在一起 ✗
            //   它确实让某个"置顶但不激活"的游戏小窗能读到了 ✗
            //   ★★★ 但代价是【之后所有剪贴板操作都失败】★★★
            //   （"clipboard write failed: 所请求的剪贴板操作失败"）
            //   因为剪贴板 API 依赖线程的输入队列 ✗ 挂接状态下会出错 ✓
            //   收益是一个本来就用不了的小窗 ✗ 代价是 QQ 和微信全废 ✓
            //   撤掉 ★ 不值得 ✓

            // never operate on our own window: with the main window focused, the
            // hotkey would otherwise rewrite our own rules box
            if (Native.ForegroundProcessId() == System.Diagnostics.Process.GetCurrentProcess().Id)
            {
                Log(why + ": foreground is this program's own window, skipped");
                return;
            }

            string procAtStart = Native.ForegroundProcessName();

            // must happen before we synthesise any keystroke
            Native.WaitForModifiersReleased();

            object backup = GrabClipboard();

            // Put a marker on the clipboard first. If the input box is empty (or nothing
            // is selected) Ctrl+C copies NOTHING and the clipboard keeps its old content -
            // which the old code then read as "the text in the box" and pasted back.
            try { Clipboard.SetText(ClipboardMarker); } catch { }
            Thread.Sleep(60);

            Native.SendCtrl(Native.VK_A);
            Thread.Sleep(80);
            Native.SendCtrl(Native.VK_C);
            Thread.Sleep(180);

            string procAfterRead = Native.ForegroundProcessName();

            string src = null;
            try { if (Clipboard.ContainsText()) src = Clipboard.GetText(); }
            catch (Exception ex) { Log(why + ": clipboard read failed: " + ex.Message); }

            // ★ 没复制到就重试。
            //
            // 为什么需要：Ctrl+A / Ctrl+C 是模拟按键，Electron 应用（VS Code 这类）
            // 响应比原生窗口慢，代码原来只等 180ms。等不够时剪贴板里还是那个标记，
            // 于是判定"框里没内容"直接中止 —— 用户看到的就是"有时候按了没反应"，
            // 而且再打一次字往往就好了（下一次的时机刚好够）。
            // 重试两次能把这种偶发失败压下去，代价只有几十毫秒。
            int copyTries = 0;
            bool usedFallbackSelect = false;
            // ★ 重试次数 3 → 1（用户 2026-10-17）
            //   每次重试都要重新 End + Shift+Home 选一遍 ✧
            //   在输入法打字时那就是光标来回乱跳 ✗
            //   —— ★ 多试两次对'偶发读不到'的收益 ✧
            //      远不如它打断打字的代价大 ✓
            while ((string.IsNullOrEmpty(src) || src == ClipboardMarker) && copyTries < 1)
            {
                copyTries++;
                Thread.Sleep(100);

                // ★ 从第二次重试起，换一种【选中全部】的办法。
                //
                // 为什么需要：Ctrl+A 并不是处处都等于"全选"。
                //   · Tkinter（Python + Tk 写的程序）里，Ctrl+A 是【光标移到行首】✗
                //     它的全选要用 Ctrl+/ ✗ 和 Windows 习惯完全不同 ✓
                //   · 一些自绘编辑器 likewise 不认 ✓
                // 而【End → Shift+Home】是通用的"从末尾选到行首"，
                // 单行输入框里就等于全选 ✗ 两边都认 ✓
                //
                // 用户的实测就是这个：游戏里那个中文输入法小窗是 Tkinter 写的，
                //   Ctrl+V 能粘进去（Tk 认 <<Paste>>）✗
                //   但 Ctrl+A + Ctrl+C 什么都复制不到（"still empty"）✗
                // 换成 End + Shift+Home 就能选中了。
                if (copyTries >= 2)
                {
                    usedFallbackSelect = true;
                    Native.SendKey(Native.VK_END);
                    Thread.Sleep(50);
                    Native.SendShift(Native.VK_HOME);
                }
                else
                {
                    Native.SendCtrl(Native.VK_A);
                }
                Thread.Sleep(70);
                Native.SendCtrl(Native.VK_C);
                Thread.Sleep(220);
                try { if (Clipboard.ContainsText()) src = Clipboard.GetText(); } catch { }
            }
            if (copyTries > 0)
                Log(why + ": copy retried " + copyTries + " time(s)"
                        + (usedFallbackSelect ? "（后几次改用了 End+Shift+Home 选法）" : "")
                        + ", " + (string.IsNullOrEmpty(src) || src == ClipboardMarker ? "still empty" : "got it"));

            // ★★★ 框里有图片 / 附件吗？有就【什么都别做】★★★
            //
            // Ctrl+A 会把输入框里的图片一起选上，但图片【复制不成文字】——
            // 剪贴板里只有文字。后面 Ctrl+V 粘回去时，图片就被整个替换掉了。
            // 用户辛苦加好的图，一次自动转换就没了。
            //
            // 所以复制完立刻查一下剪贴板里有没有图片或文件：
            //   有 → 这次什么都不做，把选择收起来（不然下次一按退格全没了），
            // ★★★ 这里【不能】用 Ctrl+X 去探测图片 ★★★
            //
            // 试过：Ctrl+A + Ctrl+X（剪切）✗ 剪贴板里"应该"有全部内容 ✗
            // 查完再 Ctrl+V 粘回去 ✓
            // ★ 用户实测：★★★ 剪切那一下【图片就已经没了】★★★
            //   他看到的全过程是："先全选 → 弄没 → 再复制回来 → 图片被吞了 → 再输出"
            // —— 也就是说 QQ 的输入框【根本没办法把图片放进剪贴板】✗
            //    复制不行 ✗ 剪切也不行 ✓
            // ★ 这个探测比原问题更糟：原来只在最后写回时才丢 ✗
            //   加了它之后【探测第一步就丢】✓ 所以撤掉 ✓
            // ★★★ 先用 UIA 查一次：输入框里有没有图片 ★★★
            //   剪贴板那条路查不到（QQ 不把图片放进剪贴板）✗
            //   但 UIA 树里能看到 —— 有图片时会多出一个空的 Group 子元素 ✓
            if (Cfg.ImageGuard && UiaSaysHasAttachment(fg))
            {
                Log(why + ": ★ UIA 看出输入框里有图片或附件，这次不动它 —— "
                          + Lang.T("全选替换会把图片弄没。要转换就先把图片去掉。"));
                CollapseSelection();
                RestoreClipboard(backup);
                return;
            }

            bool hasAttachment = false;
            try
            {
                IDataObject dobj = Clipboard.GetDataObject();
                if (dobj != null)
                {
                    hasAttachment = dobj.GetDataPresent(DataFormats.Bitmap)
                                 || dobj.GetDataPresent(DataFormats.Dib)
                                 || dobj.GetDataPresent(DataFormats.FileDrop);
                }
                if (!hasAttachment && Clipboard.ContainsImage()) hasAttachment = true;
                if (!hasAttachment && Clipboard.ContainsFileDropList()) hasAttachment = true;
            }
            catch { }

            if (hasAttachment)
            {
                Log(why + ": 输入框里有图片或附件，这次不动它 —— 全选替换会把图片弄没。"
                          + Lang.T("先去把图片去掉，再转换。"));
                CollapseSelection();
                RestoreClipboard(backup);
                return;
            }

            // ★★★ 整段就是【机器用的编号】时，什么都别做 ★★★
            //
            // 起因：用户游戏里那个中文输入法工具，工作方式是
            //   打中文 → 转成 #121417 这种编号 → 输出到游戏。
            // 喵化助手把那个编号当成普通文字改掉 ✗ 那个工具就会发出
            // 【错误的指令】—— 这比"用不了"糟得多。
            //
            // 而且这类东西本来就该在"不碰"的名单里（网址 / 邮箱 / 路径 / 编号），
            // 之前那条原则只写在说明文档里 ✗ 引擎里并没有真的检查 ✓
            if (LooksLikeMachineCode(src))
            {
                Log(why + ": 读到的是一串机器编号（" + src.Trim()
                          + "），不动它 —— 改了可能让别的程序发出错误指令。");
                CollapseSelection();
                RestoreClipboard(backup);
                return;
            }
            if (string.IsNullOrEmpty(src) || src == ClipboardMarker)
            {
                RestoreClipboard(backup);
                CollapseSelection();
                Log(why + ": nothing was copied (box empty or nothing selected) - aborted, no paste"
                    + " (selection collapsed)");
                return;
            }

            // ---- @mention -------------------------------------------------------------
            // If the box content starts with an @mention, only the part after it is
            // rewritten. The mention is a rich atom in QQ; pushing it through the clipboard
            // turns it into ordinary text and it stops working, so it must never be part of
            // the selection. TrySelectAfterMention leaves EXACTLY the body range selected,
            // which is also why the later Ctrl+A has to be skipped in this mode.
            string body = src;
            bool mentionMode = false;
            if (src.Length > 1 && src[0] == '@')
            {
                string t1 = TrySelectAfterMention(src);
                if (t1 != null) { body = t1; mentionMode = true; }
            }

            // ★ 2026-10-18：语气稳定 ✧ 同一句话用同一个种子 ✓
            //   不同的句子结果仍然不同 ✧ 「再喵一次」通过 BumpSalt 变化 ✓
            Random rndForThis = Engine.RandomFor(body);
            Config effCfg = MainForm.StyleConfig(procAtStart) ?? Cfg;
            string outp = Engine.Run(body, effCfg, rndForThis);   // ★ 2026-10-18：按软件切风格 ✓

            // ★★★ 2026-10-18：已经是我们的输出时，再按一次 = 换一个 ★★★
            //   用户：“为什么不会换一个” ✧ 对 ✗ 这个应该换 ✓
            //   但不能无限轮 ✗ 最多试 3 次 ✓
            if (outp == body && why != "auto")
            {
                for (int attempt = 0; attempt < 3 && outp == body; attempt++)
                {
                    Engine.BumpSalt();
                    outp = Engine.Run(body, effCfg, Engine.RandomFor(body));
                }
                // 用完把 salt 正常化一下 ✗ 不然下一句也受影响 ✓
                Engine.ResetSalt();
                if (outp != body) Log("  retry: 换了一个结果");
            }
            Log("  read  [" + Brief(src) + "]");
            if (outp != body) Log("  write [" + Brief(outp) + "]");

            if (outp == body)
            {
                RestoreClipboard(backup);
                CollapseSelection();
                Log(why + ": result identical to the box content (already transformed) - no paste"
                    + " (selection collapsed)");
                return;
            }

            if (!SetClipboardText(outp))
            {
                Log(why + ": clipboard write failed (12 retries)");
                RestoreClipboard(backup);
                CollapseSelection();
                return;
            }

            Thread.Sleep(80);
            if (!mentionMode)
            {
                // Confirm the box still holds exactly what was read, then paste over it.
                //
                // This does NOT add a keystroke: the sequence was already "Ctrl+A, Ctrl+V",
                // and all that changes is a Ctrl+C in between to compare with. If the box has
                // moved since the read - because the user typed, or because a previous run's
                // paste has only just landed - pasting now would sit the new text beside the
                // old one instead of replacing it, which is the duplication:
                //     你好喵~ฅ^•ﻌ•^ฅ你好喵~(๑•̀ㅂ•́)و✧
                // Rather than keep guessing at why two runs overlapped, the overlap is made
                // harmless: a stale run gives up and changes nothing.
                Native.SendCtrl(Native.VK_A);
                Thread.Sleep(70);
                Native.SendCtrl(Native.VK_C);
                Thread.Sleep(140);

                string now = null;
                try { if (Clipboard.ContainsText()) now = Clipboard.GetText(); } catch { }

                if (now == ClipboardMarker || now == null)
                {
                    RestoreClipboard(backup);
                    CollapseSelection();
                    Log(why + ": 粘贴前复读失败（剪贴板没拿到内容），跳过");
                    return;
                }
if (now != src && now != outp)
                {
                    // ★ 内容变了 —— 先别急着说"已经写过"。
                    //
                    // 已写过的情况：内容【正好等于我生成的结果】→ 那个下面会处理。
                    // 其他情况：多半是用户还在打字 / 系统正在补全 @提及
                    //   （用户实测：# 读到 [ 赶紧踢一个嘛]，一秒后变成
                    //    [@麓霭 赶紧踢一个嘛] —— 那是系统补全，不是我们写的）
                    // → ★ 设个标志 ✗ 让外层的 DoTransform 按新内容重来一遍 ✓
                    RestoreClipboard(backup);
                    CollapseSelection();
                    contentChanged = true;
                    Log(why + ": 框里的内容在处理期间变了。读到的是 [" + Brief(src)
                        + "]，现在是 [" + Brief(now) + "] —— 按新的来一次。");
                    return;
                }
                if (now == outp)
                {
                    RestoreClipboard(backup);
                    CollapseSelection();
                    Log(why + ": 跳过 —— 框里已经是我生成的结果了，不重复粘贴。");
                    return;
                }

                // the check left the box selected and the clipboard holding its own copy.
                // No further Ctrl+A: the verify above selected everything a moment ago and
                // nothing but a clipboard write has happened since, so re-selecting would only
                // widen the window in which a keystroke from the user could land in it.
                if (!SetClipboardText(outp))
                {
                    Log(why + ": clipboard rewrite failed (12 retries)");
                    RestoreClipboard(backup);
                    CollapseSelection();
                    return;
                }
            }
            // ★ 2026-10-18：撤销用 ✧ 记下【原文】和【即将写入的结果】✓
            undoFrom = src;
            undoTo = outp;

            string procBeforePaste = Native.ForegroundProcessName();
            Native.SendCtrl(Native.VK_V);
            // Long enough for the other application to have processed the paste, so the next
            // trigger reads the NEW text instead of the old one.
            Thread.Sleep(400);
            settleArmed = true;
            BeginSettle();
            string procAfterPaste = Native.ForegroundProcessName();

            // The read-back check that used to live here is gone. It existed to catch a paste
            // that inserted instead of replacing, but it cost another Ctrl+A right after the
            // paste - and if the user starts typing again in that window, the next character
            // lands inside a full selection and wipes the line. The actual cause turned out to
            // be in the engine (SuffixTailIsDecoration only looked at the FIRST 喵~), so the
            // diagnostic is no longer worth its risk. The read/write pair above is still logged.

            RestoreClipboard(backup);
            sessionCount++;
            Stat.Bump(Engine.LastKaomoji);
            // \u2605 2026-10-18\uff1a\u8bb0\u4e00\u6761\u5386\u53f2 \u2727 \u6700\u591a\u7559 20 \u6761 \u2717 \u53ea\u5728\u5185\u5b58 \u2713
            try
            {
                Stat.Recent.Add(new string[] { src, outp });
                while (Stat.Recent.Count > 20) Stat.Recent.RemoveAt(0);
                Stat.Streak++;
                if (Stat.Streak == 18)
                {
                    Log("\u732b\u732b\u6709\u70b9\u7d2f\u4e86\u2026\u2026\uff08\u8fde\u7eed\u55b5\u5316 18 \u6b21\uff09");
                    // \u2605 \u754c\u9762\u90a3\u8fb9\u6ca1\u73b0\u6210\u7684\u95ea\u72b6\u6001\u680f \u2717 \u5148\u53ea\u8bb0\u65e5\u5fd7 \u2713
                }
            }
            catch { }
            Stat.Save(StatsPath);
            if (Engine.LastMoodInfo.Length > 0) Log("  tone: " + Engine.LastMoodInfo);
            Log(why + ": transformed " + body.Length + " -> " + outp.Length + " chars"
                + (mentionMode ? " (after the @mention; mention left in place)" : "")
                + "  [fg start=" + procAtStart + " read=" + procAfterRead
                + " paste=" + procBeforePaste + "->" + procAfterPaste + "]");
        }

        object GrabClipboard()
        {
            try
            {
                IDataObject src = Clipboard.GetDataObject();
                if (src == null) return null;
                DataObject copy = new DataObject();
                foreach (string fmt in src.GetFormats())
                {
                    try
                    {
                        object o = src.GetData(fmt);
                        if (o != null) copy.SetData(fmt, o);
                    }
                    catch { }
                }
                return copy;
            }
            catch { return null; }
        }

        /// ★★★ 写剪贴板要重试 ★★★
        ///
        /// Windows 剪贴板同一时刻只能被一个程序打开 ✧
        /// QQ / 微信 自己也盯着剪贴板 ✧ 撞上就抛
        ///   "所请求的剪贴板操作失败。" ✓
        /// —— ★ 这是【偶发】的 ✧ 隔几十毫秒再来一次就好 ✓
        /// （用户 2026-10-07 日志：连试三次里两次失败 ✧ 第三次成功 ✓）
        bool SetClipboardText(string text)
        {
            // ★★★ 顺序很重要：快的排前面 ★★★
            //
            // 第一止：原始 Win32（不走 OLE ✓）
            //   一次就到 ✧ 几乎不花时间 ✓
            //
            // 第二止：才退回 .NET 的 Clipboard.SetText
            //   它走 OLE ✧ OLE 被占时会失败 ✓
            //   而且它【内部自己会重试约 1 秒】✧ 放前面就是白等 1 秒 ✓
            //
            // 用户 2026-10-07："全选完要过好久大约 3 秒左右才替换上去"
            //   —— 就是上一版把慢的那条排在前面了 ✓
            for (int i = 0; i < 3; i++)
            {
                if (SetClipboardRaw(text)) return true;
                System.Threading.Thread.Sleep(40);
            }

            for (int i = 0; i < 2; i++)
            {
                try { Clipboard.SetText(text); return true; }
                catch { System.Threading.Thread.Sleep(40); }
            }
            return false;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool CloseClipboard();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool EmptyClipboard();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern IntPtr GlobalLock(IntPtr hMem);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool GlobalUnlock(IntPtr hMem);

        /// ★ 不走 OLE 的剪贴板写法 ✧ OLE 被占的时候这条路还能走✓
        static bool SetClipboardRaw(string text)
        {
            const uint CF_UNICODETEXT = 13;
            const uint GMEM_MOVEABLE = 2;
            if (!OpenClipboard(IntPtr.Zero)) return false;
            try
            {
                if (!EmptyClipboard()) return false;
                int bytes = (text.Length + 1) * 2;
                IntPtr h = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
                if (h == IntPtr.Zero) return false;
                IntPtr p = GlobalLock(h);
                if (p == IntPtr.Zero) return false;
                System.Runtime.InteropServices.Marshal.Copy(text.ToCharArray(), 0, p, text.Length);
                System.Runtime.InteropServices.Marshal.WriteInt16(p, text.Length * 2, 0);
                GlobalUnlock(h);
                if (SetClipboardData(CF_UNICODETEXT, h) == IntPtr.Zero) return false;
                return true;
            }
            catch { return false; }
            finally { CloseClipboard(); }
        }

        void RestoreClipboard(object backup)
        {
            if (backup == null) return;
            try { Clipboard.SetDataObject(backup, true); }
            catch { }
        }

        // ---- autorun ----

        public static bool IsAutoRunEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (k == null) return false;
                    return k.GetValue(RunValueName) != null;
                }
            }
            catch { return false; }
        }

        public static void SetAutoRun(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\" --tray");
                    else k.DeleteValue(RunValueName, false);
                }
            }
            catch { }
        }

        public void Log(string msg)
        {
            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + "\r\n";
                File.AppendAllText(LogPath, line, Encoding.UTF8);
            }
            catch { }
        }

        public void Dispose()
        {
            try { if (tray != null) tray.Dispose(); } catch { }
            try { if (hkForm != null) hkForm.Dispose(); } catch { }
            try { if (timer != null) timer.Dispose(); } catch { }
        }
    }

    // ---------------------------------------------------------------- entry

    static class Program
    {
        /// The one place the version lives. It goes into the window title, the tray tooltip,
        /// the 关于 page, the startup log and the package name - all of which used to say
        /// "1.0" by hand, so after a dozen rounds of changes the build still claimed to be
        /// the first one. Never hard-code it anywhere else.
        // Bumped ONLY when the PC program itself changes. The Android side keeps its own
        // number in AndroidManifest.xml, and the two are deliberately NOT kept in step:
        // raising this one for an Android-only release made "2.3" mean nothing here, since
        // the binary was byte-identical to 2.1. Two products, two numbers.
        public const string Version = "2.17.0";
        public const string BuildDate = "2026-10-04";

        /// Which UI experiment this build carries, or "" for the real one. Set by the
        /// /define: switches build-ui-experiments.ps1 passes; shown in the title bar so a
        /// variant can be told apart from the normal build at a glance.
        public const string UiTag =
#if UI_TOPBAR && UI_CARDS && UI_LAB && UI_KAO
            "试验-全部整合";
#elif UI_TOPBAR
            "试验-顶部气泡";
#elif UI_CARDS
            "试验-数字卡";
#elif UI_LAB
            "试验-实验室";
#elif UI_KAO
            "试验-颜文字胶囊";
#else
            "";
#endif

        static Mutex instanceLock;

        /// <summary>
        /// Records an unhandled UI exception in the log instead of showing the "未将对象引用
        /// 设置到对象的实例" dialog and carrying on from an unknown state.
        ///
        /// Added after a crash that only happened when the user scrolled the 设置 page, which
        /// could not be reproduced from here and left nothing behind: the dialog was clicked
        /// through, so the process never terminated and Windows logged no event at all.
        /// The stack is what is actually needed, so the app now writes it itself.
        /// </summary>
        static void LogCrash(object ex)
        {
            try
            {
                string dir = Path.GetDirectoryName(Application.ExecutablePath);
                File.AppendAllText(Path.Combine(dir, "miaohua.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  !! UNHANDLED: " + ex + "\r\n",
                    Encoding.UTF8);
            }
            catch { }
        }

        static void InstallCrashLogging()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
            {
                LogCrash(e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                LogCrash(e.ExceptionObject);
            };
        }

        [STAThread]
        static int Main(string[] args)
        {
            InstallCrashLogging();
            // 把自带的每个风格都登记给引擎 —— 换风格之后它还要靠这个认出自己写过什么。
            // 放在入口：正常开界面和命令行那些模式（--trace 等）都会覆盖到。
            MainForm.SeedEngineStyles();
            // ★ 2026-10-18：--models 列出模型文件夹里认得出的 .gguf ✓
            // ★ 2026-10-18：--try <文字> [次数] ✧ 直接跑一遍引擎看效果 ✓
            //   用来调颜文字冷却 / 语气稳定性这种看不见的东西
            if (args.Length > 1 && args[0] == "--try")
            {
                App a2 = new App();
                a2.Cfg = Config.Load(a2.RulesFilePath);
                int times = 1;
                if (args.Length > 2) { try { times = Math.Max(1, Math.Min(50, int.Parse(args[2]))); } catch { } }
                for (int t = 0; t < times; t++)
                {
                    // ★ 用和真实转换一样的种子 ✗ 才能验证稳定性 ✓
                    Engine.stableMood = a2.Cfg.StableMood;
                    Random rr = Engine.RandomFor(args[1]);
                    Console.WriteLine(Engine.Run(args[1], a2.Cfg, rr));
                    Engine.BumpSalt();
                }
                return 0;
            }

            if (args.Length > 0 && args[0] == "--models")
            {
                string bd = AppDomain.CurrentDomain.BaseDirectory;
                Console.WriteLine("base: " + bd);
                List<string> found = new List<string>();
                foreach (string dd in App.ModelDirs(bd))
                {
                    Console.WriteLine("  dir: " + dd + (Directory.Exists(dd) ? "  [exists]" : "  [missing]"));
                    if (!Directory.Exists(dd)) continue;
                    string[] g = Directory.GetFiles(dd, "*.gguf");
                    Array.Sort(g, StringComparer.OrdinalIgnoreCase);
                    foreach (string f in g)
                    {
                        string nm = Path.GetFileName(f);
                        if (nm.StartsWith("\u5907\u7528")) continue;
                        found.Add(nm);
                    }
                    if (found.Count > 0) break;
                }
                Console.WriteLine("found " + found.Count + " model(s):");
                foreach (string nm in found)
                    Console.WriteLine("  " + nm);
                return 0;
            }

            if (args.Length > 0 && args[0] == "--selftest")
                return SelfTest();

            // ★ --ctx "对方：我今天考砸了" --trace "没事的"
            //   给「上文…」条件注入一段假的聊天上下文 ✧ 方便验证规则 ✓
            if (args.Length > 0 && args[0] == "--ctx")
            {
                Engine.CtxOverride = args.Length > 1 ? args[1] : "";
                string[] rest = new string[args.Length - 2];
                Array.Copy(args, 2, rest, 0, rest.Length);
                if (rest.Length > 0 && rest[0] == "--trace")
                {
                    string[] texts = new string[rest.Length - 1];
                    Array.Copy(rest, 1, texts, 0, texts.Length);
                    return TraceRun(texts);
                }
                return 0;
            }
            if (args.Length > 0 && args[0] == "--trace")
            {
                string[] texts = new string[args.Length - 1];
                Array.Copy(args, 1, texts, 0, texts.Length);
                return TraceRun(texts);
            }
            if (args.Length > 0 && args[0] == "--icons")
                return IconSheet(args.Length > 1 ? args[1] : "icons.png");
            if (args.Length > 0 && args[0] == "--film")
            {
                int pg = args.Length > 2 ? int.Parse(args[2]) : 4;
                int fr = args.Length > 3 ? int.Parse(args[3]) : 16;
                int iv = args.Length > 4 ? int.Parse(args[4]) : 20;
                return Film(args.Length > 1 ? args[1] : "film", pg, fr, iv);
            }
            if (args.Length > 0 && args[0] == "--uia") return UiaProbe(args);
        if (args.Length > 0 && args[0] == "--shot")
            {
                int pg = -1;
                if (args.Length > 2) { try { pg = int.Parse(args[2]); } catch { pg = -1; } }
                return Shot(args.Length > 1 ? args[1] : "shot.png", pg);
            }
            if (args.Length > 0 && args[0] == "--dumptree")
            {
                int pg = -1;
                if (args.Length > 1) { try { pg = int.Parse(args[1]); } catch { pg = -1; } }
                return DumpTree(pg);
            }

            // "--style 傲娇" applies a style and exits. Exists so a style can be set from a
            // shortcut or a script instead of by clicking a chip - and so recovering from a
            // wrong click does not depend on clicking the right pixel.
            if (args.Length > 1 && args[0] == "--style")
            {
                App a = new App();       // the constructor already resolves BaseDir and paths
                a.Cfg = Config.Load(a.RulesFilePath);
                if (MainForm.ApplyStyleByName(a, args[1]))
                {
                    Console.WriteLine("style applied: " + args[1]);
                    return 0;
                }
                Console.WriteLine("unknown style: " + args[1]);
                Console.WriteLine("available: " + MainForm.StyleNames());
                return 1;
            }

            if (args.Length > 0 && args[0] == "--styles")
            {
                Console.WriteLine(MainForm.StyleNames());
                return 0;
            }

            // Prints the bare version so the packaging script can name the zip from the
            // build instead of keeping its own copy of the number.
            // \u2605 \u5bf9\u62cd\u7528\uff1a\u8dd1\u4e00\u6279\u7528\u4f8b \u2727 \u8f93\u51fa\u8ddf Java \u7248\u9010\u6761\u5bf9\u6bd4 \u2713
        if (args.Length > 1 && args[0] == "--clf")
            return ClfRun(args[1]);

        if (args.Length > 0 && args[0] == "--version")
            {
                Console.WriteLine(Version);
                return 0;
            }

            bool startHidden = false;
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "--tray") startHidden = true;

            // only one copy may run: a second copy cannot register the hotkeys
            // (the first one owns them) and would just sit there doing nothing.
            bool createdNew;
            instanceLock = new Mutex(true, "Local\\MiaohuaHelper_SingleInstance", out createdNew);
            if (!createdNew)
            {
                // Double-clicking the desktop shortcut while the tool sits in the tray must
                // open the window, not lecture the user about single instances.
                if (Native.ActivateExistingWindow()) return 0;

                // \u2605\u2605\u2605 \u53eb\u4e0d\u9192 \u2727 \u5f88\u53ef\u80fd\u662f\u5361\u6b7b\u7684\u50f5\u5c38\uff08\u8fde\u7a97\u53e3\u90fd\u6ca1\u6709\uff09\u2605\u2605\u2605
                //   \u7528\u6237 2026-10-17\uff1a\u5361\u4f4f \u2727 \u5173\u7a97\u53e3\u2727 \u518d\u5f00\u5c31\u5f39"\u5df2\u7ecf\u5728\u8fd0\u884c"\u2727 \u770b\u4e0d\u5230\u7a97\u53e3 \u2717
                //   \u2014\u2014 \u5f53\u5b83\u5361\u6b7b\u5904\u7406 \u2727 \u6740\u6389\u91cd\u5f00 \u2713
                try { instanceLock.Dispose(); } catch { }
                bool killedStuck = Native.KillStuckSibling();
                try { Thread.Sleep(400); } catch { }
                instanceLock = new Mutex(true, "Local\\MiaohuaHelper_SingleInstance", out createdNew);
                if (createdNew)
                {
                    if (killedStuck) { /* \u7ee7\u7eed\u5f80\u4e0b \u2727 \u6b63\u5e38\u542f\u52a8 \u2713 */ }
                }
            }
            if (!createdNew)
            {
                MessageBox.Show(
                    Lang.T("喵化助手已经在运行了，不用再开一个。\n\n") +
                    Lang.T("它可能正缩在右下角任务栏的托盘里。\n") +
                    Lang.T("看不到图标的话，点一下任务栏上的 ^ 展开隐藏的图标，\n") +
                    Lang.T("再双击那个粉色猫头就能打开主界面。"),
                    Lang.T("喵化助手"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            App app = new App();
            app.StartHidden = startHidden;
            app.Start();
            Application.Run();
            app.Dispose();
            GC.KeepAlive(instanceLock);
            return 0;
        }

        // reads selftest-in.txt (UTF-8), writes selftest-out.txt (UTF-8)
        /// Writes every control in the main window with its real bounds, so "the click
        /// lands somewhere else" can be answered with numbers instead of guesswork.
        /// Draws the window to a PNG. "--shot out.png" or "--shot out.png 4" for a page.
        ///
        /// This is not a screen grab - it is Form.DrawToBitmap, so it works while the window
        /// is in the background and needs no desktop access. It is the only way to actually
        /// LOOK at a UI change from here; coordinates from --dumptree say nothing about how
        /// something looks, and guessing at appearance is how the "status text hidden behind
        /// a button" bug shipped.
        static int Shot(string file, int page = -1)
        {
            App app = new App();
            app.ReloadRules();
            MainForm f = new MainForm(app);
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(0, 0);
            f.Show();
            Application.DoEvents();
            f.PerformLayout();
            Application.DoEvents();
            if (page >= 0)
            {
                f.ShowPage(page);
                Application.DoEvents();
                f.PerformLayout();
                Application.DoEvents();
            }
            System.Threading.Thread.Sleep(400);
            Application.DoEvents();

            int w = f.Width, h = f.Height;
            using (Bitmap bmp = new Bitmap(w, h))
            {
                f.DrawToBitmap(bmp, new Rectangle(0, 0, w, h));
                bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
            }
            f.Close();
            Console.WriteLine("wrote " + file + "  " + w + "x" + h);
            return 0;
        }

        /// Dumps every control's bounds. "--dumptree 4" selects page 4 first, so the pages that
        /// are not on screen at startup (设置, 统计, 公告, 关于) can be checked too - a layout
        /// bug on one of those is otherwise invisible until a user reports it.
        /// 逐帧拍下页面切换的动画。
        ///
        /// "--film out 4 16 20" = 前缀 out、切到第 4 页、16 帧、每帧 20 毫秒。
        ///
        /// Why this exists: an animation cannot be judged from a single screenshot, and
        /// starting a fresh process per shot (which --shot does) can never catch a frame
        /// mid-flight. Here one process switches the page and captures each frame itself,
        /// pumping the message loop in between so the timers actually tick.
        /// ★ UIA 探针：把前台窗口的无障碍结构打出来。
        ///
        /// 起因：QQ 输入框里有图片时，喵化助手靠剪贴板【查不到】
        /// （复制/剪切都不会把图片放进剪贴板）。
        /// 但用户实测【讲述人能区分】：
        ///     只有文字 → 念出文字
        ///     有图片   → 只念"选中"，读不到内容
        /// 说明 UIA 这条路能读到输入框的内容 —— 那就有可能判断"有没有图片"。
        ///
        /// 这个探针用来确认【到底能读到什么】：控件类型、名字、值。
        /// 用法：Miaohua.exe --uia   → 3 秒内切到目标窗口 → 结果写进文件
        static int UiaProbe(string[] args)
        {
            string outFile = args.Length > 1 ? args[1]
                : @"D:\harness gzq\009\界面试验\uia-out.txt";
            Console.WriteLine("3 秒后读取前台窗口的 UIA 结构 —— 现在切过去");
            System.Threading.Thread.Sleep(3000);
            StringBuilder sb = new StringBuilder();
            try
            {
                IntPtr h = Native.GetForegroundWindow();
                sb.Append("前台窗口: " + Native.ForegroundWindowInfo() + "\r\n");
                System.Windows.Automation.AutomationElement root =
                    System.Windows.Automation.AutomationElement.FromHandle(h);
                if (root == null) { sb.Append("FromHandle 返回 null\r\n"); }
                else
                {
                    sb.Append("根元素: " + root.Current.ControlType.ProgrammaticName
                              + "  Name=[" + root.Current.Name + "]\r\n\r\n");
                    WalkUia(root, 0, sb, 80);
                }
            }
            catch (Exception e) { sb.Append(Lang.T("出错: ") + e + "\r\n"); }
            try { File.WriteAllText(outFile, sb.ToString(), Encoding.UTF8); } catch { }
            Console.WriteLine("wrote " + outFile);
            return 0;
        }

        static void WalkUia(System.Windows.Automation.AutomationElement el, int depth,
                            StringBuilder sb, int budget)
        {
            if (el == null || depth > 12 || budget <= 0) return;
            try
            {
                System.Windows.Automation.AutomationElementCollection kids =
                    el.FindAll(System.Windows.Automation.TreeScope.Children,
                               System.Windows.Automation.Condition.TrueCondition);
                foreach (System.Windows.Automation.AutomationElement c in kids)
                {
                    if (budget-- <= 0) return;
                    string line = new string(' ', depth * 2)
                        + c.Current.ControlType.ProgrammaticName
                        + "  Name=[" + Trim(c.Current.Name, 50) + "]";
                    string val = "";
                    try
                    {
                        object v = c.GetCurrentPattern(
                            System.Windows.Automation.ValuePattern.Pattern);
                        if (v != null) val = ((System.Windows.Automation.ValuePattern)v).Current.Value;
                    }
                    catch { }
                    if (val.Length > 0) line += "  Value=[" + Trim(val, 60) + "]";
                    sb.Append(line + "\r\n");
                    WalkUia(c, depth + 1, sb, budget);
                }
            }
            catch { }
        }

        static string Trim(string s, int n)
        {
            if (s == null) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }
        public static bool SaveSnapshots = false;

        static int Film(string prefix, int page, int frames, int intervalMs)
        {
            SaveSnapshots = true;
            App app = new App();
            app.ReloadRules();
            MainForm f = new MainForm(app);
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(0, 0);
            f.Show();
            Application.DoEvents();
            f.PerformLayout();
            Application.DoEvents();
            System.Threading.Thread.Sleep(300);
            Application.DoEvents();

            int w = f.Width, h = f.Height;
            int n = 0;
            Action<string> grab = delegate(string tag)
            {
                using (Bitmap bmp = new Bitmap(w, h))
                {
                    f.DrawToBitmap(bmp, new Rectangle(0, 0, w, h));
                    bmp.Save(prefix + "-" + n.ToString("00") + "-" + tag + ".png",
                             System.Drawing.Imaging.ImageFormat.Png);
                }
                n++;
            };

            Console.WriteLine("frame 00 before   " + f.LayoutProbe());
            grab("before");
            f.ShowPage(page);                       // 开始动画
            for (int i = 0; i < frames; i++)
            {
                System.Threading.Thread.Sleep(intervalMs);
                Application.DoEvents();             // 让计时器跑，让画面更新
                Console.WriteLine("frame " + (i + 1).ToString("00") + "          " + f.LayoutProbe());
                grab("f" + i.ToString("00"));
            }
            f.Close();
            Console.WriteLine("wrote " + n + " frames with prefix " + prefix);
            return 0;
        }

        /// 把 paw 图标按多个尺寸画出来存成一张对照图，用来判断多小还能看出是爪印。
        static int IconSheet(string path)
        {
            int[] sizes = { 10, 12, 14, 16, 20, 24, 32, 44 };
            int total = 0;
            foreach (int s in sizes) total += s + 12;
            using (Bitmap bmp = new Bitmap(total + 20, 60))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int x = 10;
                foreach (int s in sizes)
                {
                    Image art = VecIcon.VecIconPublic("paw", Color.FromArgb(214, 106, 160), s);
                    if (art != null) g.DrawImage(art, x, 10, s, s);
                    g.DrawString(s.ToString(), SystemFonts.DefaultFont, Brushes.Gray, x, 42);
                    x += s + 12;
                }
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            Console.WriteLine("wrote " + path);
            return 0;
        }
        /// --trace "文本"  ：打印这条文本经过每一个 pass 之后的样子。
        /// --trace 后面可以跟多个文本，一次看多条。
        static int TraceRun(string[] texts)
        {
            App app = new App();
            app.ReloadRules();
            Engine.Trailing = true;
            StringBuilder sb = new StringBuilder();
            sb.Append("rules=" + app.RuleCount + " kaomoji=" + app.KaomojiCount + "\r\n\r\n");
            for (int i = 0; i < texts.Length; i++)
            {
                Engine.Trail.Clear();
                string once = Engine.Run(texts[i], app.Cfg, new Random(7));
                sb.Append("=== case " + (i + 1) + " ===\r\n");
                foreach (string line in Engine.Trail) sb.Append("  " + line + "\r\n");
                sb.Append("  RESULT | " + once + "\r\n\r\n");
            }
            Engine.Trailing = false;
            // 控制台在中文 Windows 上不是 UTF-8，直接写文件更可靠
            File.WriteAllText(@"D:\harness gzq\009\界面试验\trace-pc.txt", sb.ToString(),
                              new UTF8Encoding(false));
            Console.WriteLine("wrote trace-pc.txt");
            return 0;
        }
        static int DumpTree(int page = -1)
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            App app = new App();
            app.ReloadRules();
            MainForm f = new MainForm(app);
            f.Show();
            Application.DoEvents();
            f.PerformLayout();
            Application.DoEvents();
            if (page >= 0)
            {
                f.ShowPage(page);
                Application.DoEvents();
                f.PerformLayout();
                Application.DoEvents();
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("window client=" + f.ClientSize.Width + "x" + f.ClientSize.Height + "\r\n");
            sb.Append("page=" + page + "\r\n");
            Walk(f, 0, sb);

            // resolve what is under each interesting point, the same way Windows would
            IntPtr h = f.Handle;
            int[] xs = { 40, 58, 70, 82, 92 };
            foreach (int x in xs)
                sb.Append("hit at (" + x + ",675) -> " + Describe(f.PointToScreen(new Point(x, 675))) + "\r\n");

            File.WriteAllText(Path.Combine(dir, "dumptree-out.txt"), sb.ToString(), Encoding.UTF8);
            f.Close();
            return 0;
        }

        static void Walk(Control c, int depth, StringBuilder sb)
        {
            sb.Append(new string(' ', depth * 2));
            sb.Append(c.GetType().Name);
            sb.Append(" bounds=" + c.Left + "," + c.Top + " " + c.Width + "x" + c.Height);
            sb.Append(" visible=" + c.Visible + " enabled=" + c.Enabled + " tabstop=" + c.TabStop);
            // absolute coordinates: clicking needs screen pixels, not parent-relative ones
            try
            {
                Rectangle scr = c.RectangleToScreen(c.ClientRectangle);
                sb.Append(" screen=" + scr.Left + "," + scr.Top + " " + scr.Width + "x" + scr.Height);
            }
            catch { }
            if (c.Text != null && c.Text.Length > 0)
                sb.Append(" text=[" + (c.Text.Length > 26 ? c.Text.Substring(0, 26) : c.Text) + "]");
            sb.Append("\r\n");
            foreach (Control k in c.Controls) Walk(k, depth + 1, sb);
        }

        static string Describe(Point screen)
        {
            IntPtr h = WindowFromPoint(new POINT { X = screen.X, Y = screen.Y });
            if (h == IntPtr.Zero) return "nothing";
            StringBuilder cls = new StringBuilder(200);
            GetClassNameW(h, cls, 200);
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            return "hwnd=0x" + h.ToInt64().ToString("X") + " class=" + cls;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        static extern IntPtr WindowFromPoint(POINT p);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

        /// \u2605 \u5c0f\u5206\u7c7b\u5668\u5bf9\u62cd\uff1a\u8bfb cases.txt\uff08\u4e0a\u4e0b\u6587 TAB \u56de\u590d\uff09\u2727 \u8f93\u51fa\u6807\u7b7e TAB \u4e0a\u4e0b\u6587 TAB \u56de\u590d \u2713
    static int ClfRun(string casesPath)
    {
        try
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(casesPath));
            string outp = Path.Combine(dir, "pc-out.txt");
            string[] lines = File.ReadAllLines(casesPath, Encoding.UTF8);
            List<string> res = new List<string>();
            int n = 0;
            foreach (string line in lines)
            {
                if (line.Trim().Length == 0) continue;
                string[] p = line.Split('\t');
                string ctx = (p.Length > 0 ? p[0] : "").Replace("\u23CE", "\n");
                string reply = p.Length > 1 ? p[1] : "";
                string lab = TinyClf.Classify(ctx, reply);
                res.Add((lab == null ? "?" : lab) + "\t" + p[0] + "\t" + reply);
                n++;
            }
            File.WriteAllLines(outp, res.ToArray(), new UTF8Encoding(false));
            Console.WriteLine("\u5c0f\u6a21\u578b\uff1a" + (TinyClf.Loaded ? "\u2713 \u52a0\u8f7d\u6210\u529f" : "\u2717 \u6ca1\u52a0\u8f7d\u5230"));
            Console.WriteLine("\u8dd1\u4e86 " + n + " \u6761 -> " + outp);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("\u51fa\u9519\uff1a" + ex.Message);
            return 1;
        }
    }

    static int SelfTest()
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            string inPath = Path.Combine(dir, "selftest-in.txt");
            string outPath = Path.Combine(dir, "selftest-out.txt");
            string rulesPath = Path.Combine(dir, "rules.txt");

            Config cfg = Config.Load(rulesPath);
            // the selftest must stay deterministic and offline: if the user's rules file has
            // 智能语气 on, this would fire an HTTP request per case and never be repeatable
            cfg.SmartTone = false;
            Random rnd = new Random(12345);

            StringBuilder sb = new StringBuilder();
            sb.Append("rules=" + cfg.Rules.Count + " kaomoji=" + cfg.Kaomoji.Count +
                      " each=" + cfg.EachSentence + " suffix=[" + cfg.Suffix + "]\r\n");
            sb.Append("tone wrapper=" + cfg.Tone + "  smarttone=" + cfg.SmartTone + "\r\n");
            sb.Append("template=[" + cfg.Template + "]\r\n");
            sb.Append("auto=" + cfg.AutoMode + " debounce=" + cfg.DebounceMs +
                      " whitelist=[" + string.Join(",", cfg.Whitelist.ToArray()) + "]\r\n");
            sb.Append("hotkeys transform=[" + cfg.HotkeyTransform + "] pause=[" + cfg.HotkeyPause +
                      "] reload=[" + cfg.HotkeyReload + "]\r\n");
            sb.Append("skipends=[" + string.Join(" ", cfg.SkipEnds.ToArray()) + "]\r\n");
            // Every pool is listed with its size. A missing CanonicalKey alias shows up here
            // as a zero, which is exactly how the 分别结尾 typo was found - it silently fell
            // back to the plain pool and looked like "the classifier is wrong".
            sb.Append("pools: opener=" + cfg.Openers.Count +
                      " closer=" + cfg.Closers.Count +
                      " question=" + cfg.QuestionClosers.Count +
                      " exclaim=" + cfg.ExclaimClosers.Count +
                      " happy=" + cfg.HappyClosers.Count +
                      " sad=" + cfg.SadClosers.Count +
                      " angry=" + cfg.AngryClosers.Count + "\r\n");
            sb.Append("pools: greeting=" + cfg.GreetingClosers.Count +
                      " farewell=" + cfg.FarewellClosers.Count +
                      " love=" + cfg.LoveClosers.Count +
                      " praise=" + cfg.PraiseClosers.Count + "\r\n");
            sb.Append("pools: thanks=" + cfg.ThanksClosers.Count +
                      " apology=" + cfg.ApologyClosers.Count +
                      " congrat=" + cfg.CongratClosers.Count +
                      " invite=" + cfg.InviteClosers.Count + "\r\n");
            sb.Append("pools: surprise=" + cfg.SurpriseClosers.Count +
                      " fear=" + cfg.FearClosers.Count +
                      " tired=" + cfg.TiredClosers.Count +
                      " bored=" + cfg.BoredClosers.Count +
                      " disappointed=" + cfg.DisappointedClosers.Count +
                      " comfort=" + cfg.ComfortClosers.Count +
                      " charm=" + cfg.CharmClosers.Count + "\r\n");
            sb.Append("--------------------------------------------------\r\n");

            // how the rules file renders inside the editor box. A file with bare LF
            // shows up there as ONE gigantic line, because Win32 edit controls only
            // break on CRLF - so this catches that regression.
            try
            {
                App probe = new App();
                probe.ReloadRules();
                MainForm f = new MainForm(probe);
                string box = f.RulesBoxText;
                int breaks = 0;
                for (int i = 0; i + 1 < box.Length; i++)
                    if (box[i] == '\r' && box[i + 1] == '\n') breaks++;
                string raw = File.ReadAllText(rulesPath, Encoding.UTF8);
                int rawCrlf = 0;
                for (int i = 0; i + 1 < raw.Length; i++)
                    if (raw[i] == '\r' && raw[i + 1] == '\n') rawCrlf++;
                sb.Append("rules file on disk : " + raw.Length + " chars, " + (rawCrlf + 1) + " CRLF lines\r\n");
                sb.Append("editor box         : " + box.Length + " chars, " + (breaks + 1) + " CRLF lines\r\n");
                sb.Append("editor line breaks : " + breaks + "  (must be > 60)\r\n");
                f.Dispose();
            }
            catch (Exception ex)
            {
                sb.Append("editor box check FAILED: " + ex.Message + "\r\n");
            }
            sb.Append("--------------------------------------------------\r\n");
            // 边打边转换: transform, keep typing, transform again. Reported from a phone -
            // the decoration added by the previous pass ended up in the MIDDLE of the box,
            // the tail-only "already decorated?" check could not see it, and every pass
            // bolted on another kaomoji while treating its own output as the user's words.
            sb.Append("边打边转换 (打字 -> 转换 -> 接着打字 -> 再转换)\r\n");
            {
                string cur = "你好";
                sb.Append("  start : " + cur + "\r\n");
                int decorationsSeen = 0;
                for (int step = 0; step < 4; step++)
                {
                    string after = Engine.Run(cur, cfg, rnd);
                    sb.Append("  pass  : " + after + "\r\n");
                    cur = after + (step + 1).ToString();     // the user keeps typing
                    sb.Append("  type  : " + cur + "\r\n");
                    decorationsSeen++;
                }
                string fin = Engine.Run(cur, cfg, rnd);
                string finAgain = Engine.Run(fin, cfg, rnd);
                sb.Append("  final : " + fin + "\r\n");
                // one decoration per sentence is fine; one per keystroke is the bug
                int tokenCount = 0;
                foreach (string t in Engine.DecorationTokens(cfg))
                {
                    int at = 0;
                    while (t.Length > 0 && (at = fin.IndexOf(t, at, StringComparison.Ordinal)) >= 0)
                    { tokenCount++; at += t.Length; }
                }
                bool stable = (fin == finAgain);
                sb.Append("  装饰数量: " + tokenCount + "  末尾稳定: " + stable +
                          (stable ? "  OK\r\n" : "   <<<<< NOT IDEMPOTENT\r\n"));
                if (!stable) sb.Append("AGAIN: " + finAgain + "\r\n");
            }
            sb.Append("--------------------------------------------------\r\n");
            // 手动删掉装饰的一部分之后再转换. Reported from the phone: the user trimmed one
            // character out of a kaomoji, the exact-match strip no longer recognised it, and a
            // second decoration was bolted on behind the broken one.
            sb.Append(Lang.T("删掉一部分装饰之后再转换\r\n"));
            {
                Config tcfg = new Config();
                tcfg.Suffix = "喵~";
                tcfg.Template = "{后缀}{颜文字}";
                tcfg.EachSentence = true;
                tcfg.Kaomoji.Add("(´･ω･`)");
                tcfg.Kaomoji.Add("ฅ^•ﻌ•^ฅ");
                string[] partial = {
                    "你好喵~(´･ω･`)",   // untouched - must stay exactly as it is
                    "你好喵~(･ω･`)",    // one character trimmed off the kaomoji
                    "你好喵~",          // kaomoji gone entirely
                    "你好喵~(´･ω",      // half a kaomoji left
                    "你好喵~(´･ω･)你好喵~(´･ω･)",   // already doubled by an earlier bug
                    "你好喵~(´･ω･)你好",            // doubled then edited
                    // the exact strings from the user's screenshots
                    "你好喵~ฅ^•ﻌ•^ฅ",
                    "你好喵~(๑•̀ㅂ•́)و✧",
                    "你好喵~ฅ^•ﻌ•^ฅ你好喵~(๑•̀ㅂ•́)و✧",
                    // verbatim from the user's log: a decoration with one character deleted,
                    // then more typing. The old one used to survive and a new one was appended.
                    "你好喵~ฅ^•ﻌ•ฅ你好",
                    "你好喵~(=^･ω･^)你好",
                    "你好喵~(=^‥=你好",
                    "你好喵~(´･ω･)你好",
                };
                foreach (string p in partial)
                {
                    string once = Engine.Run(p, tcfg, rnd);
                    string twice = Engine.Run(once, tcfg, rnd);
                    sb.Append("  IN   : " + p + "\r\n");
                    sb.Append("  OUT  : " + once + "\r\n");
                    sb.Append(once == twice
                        ? "  AGAIN: (identical - idempotent)\r\n"
                        : "  AGAIN: " + twice + "   <<<<< NOT IDEMPOTENT\r\n");
                }

                // 轻度 (EachSentence = false) walks a DIFFERENT branch of the decorator, and
                // that branch had no partial-decoration protection at all - which is why this
                // was reported from a phone set to 轻度 while every existing test used the
                // default 普通. Both modes are exercised now.
                sb.Append("  -- 轻度 (EachSentence = false) --\r\n");
                tcfg.EachSentence = false;
                foreach (string p in partial)
                {
                    string once = Engine.Run(p, tcfg, rnd);
                    string twice = Engine.Run(once, tcfg, rnd);
                    sb.Append("  IN   : " + p + "\r\n");
                    sb.Append("  OUT  : " + once + "\r\n");
                    sb.Append(once == twice
                        ? "  AGAIN: (identical - idempotent)\r\n"
                        : "  AGAIN: " + twice + "   <<<<< NOT IDEMPOTENT\r\n");
                }
            }
            sb.Append("--------------------------------------------------\r\n");

            sb.Append("--------------------------------------------------\r\n");
            // 模型地址解析. Each of these is how a real local server documents its endpoint.
            // Getting it wrong is silent - 智能语气 just falls back to keywords - so the
            // accepted shapes are pinned down here.
            sb.Append("模型地址解析\r\n");
            string[] urls = {
                "http://127.0.0.1:1234",                        // LM Studio
                "http://127.0.0.1:1234/",
                "http://127.0.0.1:11434",                       // Ollama, bare
                "http://127.0.0.1:11434/v1",                    // Ollama, as documented
                "http://127.0.0.1:11434/v1/",                   // ...with a trailing slash
                "http://127.0.0.1:8080/v1/chat/completions"     // fully spelled out
            };
            foreach (string u in urls)
                sb.Append("  " + u + "\r\n      -> " + Llm.ApiUrl(u, "/chat/completions")
                          + "\r\n      -> " + Llm.ApiUrl(u, "/models") + "\r\n");
            sb.Append("--------------------------------------------------\r\n");

            if (File.Exists(inPath))
            {
                string[] lines = File.ReadAllLines(inPath, Encoding.UTF8);
                foreach (string line in lines)
                {
                    string once = Engine.Run(line, cfg, rnd);
                    string twice = Engine.Run(once, cfg, rnd);
                    sb.Append("IN   : " + line + "\r\n");
                    sb.Append("OUT  : " + once + "\r\n");
                    // transforming already-transformed text must change nothing, otherwise
                    // the text grows every time the tool fires
                    sb.Append(once == twice
                        ? "AGAIN: (identical - idempotent)\r\n"
                        : "AGAIN: " + twice + "   <<<<< NOT IDEMPOTENT\r\n");

                    // diagnostic for the wrapper peel: when a case is not idempotent, show
                    // exactly which pool the result was supposed to be peeled from
                    if (once != twice)
                    {
                        sb.Append("  diag: openers=" + cfg.Openers.Count +
                                  " closers=" + cfg.Closers.Count +
                                  " question=" + cfg.QuestionClosers.Count +
                                  " exclaim=" + cfg.ExclaimClosers.Count +
                                  " happy=" + cfg.HappyClosers.Count +
                                  " sad=" + cfg.SadClosers.Count +
                                  " angry=" + cfg.AngryClosers.Count + "\r\n");
                        foreach (string h in cfg.HappyClosers)
                            sb.Append("  diag: once.EndsWith([" + h + "]) = " +
                                      once.EndsWith(h, StringComparison.Ordinal) + "\r\n");
                        foreach (string h in cfg.Openers)
                            sb.Append("  diag: once.StartsWith([" + h + "]) = " +
                                      once.StartsWith(h, StringComparison.Ordinal) + "\r\n");
                    }
                }
            }

            // 语气 wrapper + 程度: both extend the output, so idempotency is what breaks
            // first. Checked against configs built in memory so the user's own rules file
            // is not involved.
            sb.Append("--------------------------------------------------\r\n");
            sb.Append("tone wrapper (开头 / 结尾)\r\n");

            Config tone = new Config();
            tone.Kaomoji.Add("(=^\u2025^=)");
            // deliberately a suffix that contains its own terminator: this is what used to
            // make the splitter cut a finished decoration in half
            tone.Suffix = "\u55B5\uFF01";
            tone.Openers.Add("\u54FC\uFF0C");
            tone.Closers.Add("\uFF0C\u624D\u4E0D\u662F\u7279\u610F\u544A\u8BC9\u4F60\u7684\u5462");
            tone.Closers.Add("\uFF0C\u522B\u3001\u522B\u8BEF\u4F1A\u554A\uFF01");
            tone.QuestionClosers.Add("\uFF0C\u4F60\u3001\u4F60\u95EE\u8FD9\u4E2A\u5E72\u561B\u2026\u2026");
            Rule rr = new Rule();
            rr.From = "\u597D\u7684";
            rr.To = "\u597D\u55D2";
            tone.Rules.Add(rr);

            string[] toneCases = {
                "\u4ECA\u5929\u5929\u6C14\u4E0D\u9519\u3002\u6211\u4EEC\u53BB\u5403\u996D\u5427\uFF01",
                "\u597D\u7684",
                "\u4F60\u5403\u996D\u4E86\u5417\uFF1F"
            };
            foreach (string tc in toneCases)
            {
                // different seeds on purpose: if the second pass drew a new variant
                // instead of keeping the one already in the text, this would differ
                string o1 = Engine.Run(tc, tone, new Random(7));
                string o2 = Engine.Run(o1, tone, new Random(99));
                sb.Append("IN   : " + tc + "\r\n");
                sb.Append("OUT  : " + o1 + "\r\n");
                sb.Append(o1 == o2 ? "AGAIN: (identical - idempotent)\r\n"
                                   : "AGAIN: " + o2 + "   <<<<< NOT IDEMPOTENT\r\n");
            }

            sb.Append("--------------------------------------------------\r\n");
            sb.Append("intensity 重度 (commas too)\r\n");
            Config heavy = new Config();
            heavy.Kaomoji.Add("(=^\u2025^=)");
            heavy.Suffix = "\u55B5~";
            heavy.EachSentence = true;
            heavy.CommaDecorate = true;
            string hc = "\u4F60\u597D\uFF0C\u4F60\u4ECA\u5929\u8FC7\u5F97\u600E\u4E48\u6837";
            string h1 = Engine.Run(hc, heavy, new Random(7));
            string h2 = Engine.Run(h1, heavy, new Random(7));
            sb.Append("IN   : " + hc + "\r\n");
            sb.Append("OUT  : " + h1 + "\r\n");
            sb.Append(h1 == h2 ? "AGAIN: (identical - idempotent)\r\n"
                               : "AGAIN: " + h2 + "   <<<<< NOT IDEMPOTENT\r\n");

            // ---- 语气 + 语境: same style, different closing line per mood ------------
            sb.Append("--------------------------------------------------\r\n");
            sb.Append("tone + context\r\n");

            Config ctx = new Config();
            ctx.Kaomoji.Add("(=^\u2025^=)");
            ctx.Suffix = "\u55B5\uFF01";
            ctx.Openers.Add("\u54FC\uFF0C");
            ctx.Closers.Add("\uFF0C\u624D\u4E0D\u662F\u7279\u610F\u544A\u8BC9\u4F60\u7684\u5462");
            ctx.QuestionClosers.Add("\uFF0C\u4F60\u3001\u4F60\u95EE\u8FD9\u4E2A\u5E72\u561B\u2026\u2026");
            ctx.HappyClosers.Add("\uFF0C\u54FC\u3001\u54FC\uFF01\u624D\u6CA1\u6709\u5F88\u9AD8\u5174\u5462\uFF01");
            ctx.SadClosers.Add("\uFF0C\u522B\u3001\u522B\u96BE\u8FC7\u5566\u2026\u2026\u6211\u52C9\u5F3A\u966A\u966A\u4F60");
            ctx.AngryClosers.Add("\uFF0C\u54FC\uFF01\u6211\u624D\u4E0D\u8DDF\u90A3\u79CD\u4EBA\u4E00\u822C\u89C1\u8BC6");

            string[] ctxCases = {
                "\u4F60\u5403\u996D\u4E86\u5417\uFF1F",
                "\u4F60\u89C9\u5F97\u5462",
                "\u4ECA\u5929\u597D\u5F00\u5FC3\u54C8\u54C8",
                "\u6211\u597D\u96BE\u8FC7\u554A",
                "\u70E6\u6B7B\u4E86\u771F\u8BA8\u538C",
                "\u4F60\u597D"
            };
            foreach (string cc in ctxCases)
            {
                string c1 = Engine.Run(cc, ctx, new Random(7));
                string c2 = Engine.Run(c1, ctx, new Random(99));
                sb.Append("IN   : " + cc + "\r\n");
                sb.Append("OUT  : " + c1 + "\r\n");
                sb.Append(c1 == c2 ? "AGAIN: (identical - idempotent)\r\n"
                                   : "AGAIN: " + c2 + "   <<<<< NOT IDEMPOTENT\r\n");
            }

            // ---- 语境判断: what the keyword classifier actually decides ----------------
            sb.Append("--------------------------------------------------\r\n");
            sb.Append("\u8BED\u5883\u5224\u65AD\uFF08\u5173\u952E\u5B57\uFF0C\u4E0D\u662F\u6A21\u578B\uFF09\r\n");
            string[] moodCases = {
                "\u4F60\u597D",
                "\u4F60\u5403\u996D\u4E86\u5417\uFF1F",
                "\u4F60\u89C9\u5F97\u5462",
                "\u4ECA\u5929\u597D\u5F00\u5FC3\u54C8\u54C8",
                "\u6211\u597D\u96BE\u8FC7\u554A",
                "\u70E6\u6B7B\u4E86\u771F\u8BA8\u538C",
                "\u592A\u597D\u4E86\uFF01\uFF01\uFF01",
                "\u8FD9\u4E48\u597D\u770B\u7684\u4E1C\u897F",
                "\u665A\u5B89\uFF0C\u65E9\u70B9\u7761",
                // ---- the four social moods, plus the overlaps that decide the order ----
                "\u4F60\u597D\u68D2",              // praise, NOT greeting (contains 你好)
                "\u4F60\u597D\u5389\u5BB3\u554A",   // praise
                "\u6211\u559C\u6B22\u4F60",        // love, NOT happy (喜欢 is a happy word)
                "\u60F3\u4F60\u4E86",              // love
                "\u62B1\u62B1",                    // love
                "\u518D\u89C1\u5566",              // farewell
                "\u665A\u5B89",                    // farewell, NOT plain
                "\u6211\u5148\u7761\u4E86",        // farewell
                "\u5728\u5417",                    // greeting, NOT question
                "\u65E9\u4E0A\u597D",              // greeting
                "\u54C8\u55BD\uFF5E",              // greeting
                "\u4F60\u597D\uFF0C\u4ECA\u5929\u8FC7\u5F97\u600E\u4E48\u6837",  // greeting wins over 疑问
                // ---- second round: 9 more categories, plus the overlaps that set the order ----
                "\u8C22\u8C22\u4F60",              // thanks
                "\u8F9B\u82E6\u5566",              // thanks
                "\u5BF9\u4E0D\u8D77",              // apology
                "\u4E0D\u597D\u610F\u601D\u554A",   // apology
                "\u606D\u559C\u4F60",              // congrat
                "\u751F\u65E5\u5FEB\u4E50",         // congrat ("快乐" is not a happy word, good)
                "\u8981\u4E0D\u8981\u4E00\u8D77\u5403\u996D",  // invite, NOT question
                "\u6211\u4EEC\u4E00\u8D77\u8D70\u5427",        // invite
                "\u54C7\u771F\u7684\u5047\u7684",   // surprise
                "\u5929\u5450\u4F60\u4E5F\u5728",   // surprise
                "\u6211\u597D\u6015",              // fear
                "\u6709\u70B9\u62C5\u5FC3",         // fear
                "\u597D\u7D2F\u554A",              // tired
                "\u6211\u597D\u56F0",              // tired
                "\u597D\u65E0\u804A\u554A",         // bored
                "\u597D\u5931\u671B",              // disappointed, NOT sad
                "\u522B\u96BE\u8FC7\u4E86",         // comfort, NOT sad (contains 难过!)
                "\u62B1\u62B1\u4F60",              // comfort (contains 抱抱, which is 喜爱)
                "\u6211\u5728\u5462\u522B\u6015",   // comfort
                "\u62B1\u62B1\u6211",              // charm, NOT love (contains 抱抱)
                "\u4F60\u80FD\u4E0D\u80FD\u62B1\u62B1\u6211",  // charm
                "\u4F60\u597D\u68D2\uFF01",         // praise still wins over greeting
                "\u8C22\u8C22\u4F60\u5938\u6211"    // thanks (谢谢 comes before 夸奖? no - praise is
                                                   // first, but 夸我 has no praise WORD, only 夸)
            };
            foreach (string mc in moodCases)
                sb.Append("  [" + Engine.Mood(mc) + "]  " + mc + "\r\n");

            // ---- 语气输出: one input per category, through the real engine -----------
            // Printed here rather than checked in the GUI, because the only thing that can
            // be wrong with a pool is whether the line reads correctly next to the input -
            // and this is the version that stays reproducible.
            sb.Append("--------------------------------------------------\r\n");
            sb.Append("\u8BED\u6C14\u8F93\u51FA\u6837\u4F8B\r\n");
            string[] sampleIn = {
                "\u4F60\u597D", "\u518D\u89C1\u5566", "\u6211\u559C\u6B22\u4F60", "\u4F60\u597D\u68D2",
                "\u8C22\u8C22\u4F60", "\u5BF9\u4E0D\u8D77", "\u606D\u559C\u4F60",
                "\u4F60\u80FD\u4E0D\u80FD\u62B1\u62B1\u6211", "\u522B\u96BE\u8FC7\u4E86",
                "\u6211\u4E0D\u5F00\u5FC3", "\u6211\u597D\u6015", "\u597D\u7D2F\u554A",
                "\u597D\u5931\u671B", "\u4ECA\u5929\u597D\u5F00\u5FC3\u54C8\u54C8", "\u4F60\u5403\u996D\u4E86\u5417"
            };
            Random sr = new Random(20261002);
            foreach (string si in sampleIn)
                sb.Append("  " + si + "  ->  " + Engine.Run(si, cfg, sr) + "\r\n");

            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
            return 0;
        }
    }
}
