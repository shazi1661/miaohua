// ============================================================================
//  语言支持（中 / 英）
// ============================================================================
//
//  为什么要这么设计
//  ----------------
//  界面里的中文是【硬编码】在 Ui.cs 里的，几百处。如果一处处改成 L("设置")
//  这种写法，风险大（容易改坏代码）、也容易漏。
//
//  所以换个思路：★ 不动 Ui.cs 一行代码 ★ —— 等窗口全部建好之后，
//  遍历一遍控件树，把每个控件的 .Text 拿去查表，查到英文就换掉。
//
//  ★ 关键点：把每个控件【第一次见到的中文】记在一个弱引用字典里。
//    切回中文时从这个字典取原文，而不是"反向查英文表"——
//    因为不同的中文可能翻成同一个英文，反查会张冠李戴。
//    用 ConditionalWeakTable 是为了控件被销毁后能自动回收，不会越用越占内存。
//
//  好处：
//    · 漏翻的自动保持中文，不会崩、不会显示成 key
//    · 切换语言只要再跑一次遍历，立刻生效，不用重建窗口
//    · 将来加第三种语言只要再加一个字典
//
//  注意：只能处理【静态界面文字】。运行时才赋值的（状态行、统计数字）要
//  单独调 Lang.T(...) —— 但那些不翻也能用，所以放在次要位置。
//
// ============================================================================

using System.Drawing;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Miaohua
{
    static class Lang
    {
        /// ★ 当前是不是英文。
        public static bool English = false;

        /// ★ 每个控件的原始中文（第一次见到时记下来）。
        static readonly ConditionalWeakTable<Control, string> Original =
            new ConditionalWeakTable<Control, string>();

        /// ★ 上次翻译之后这个控件显示的是什么 ★
        ///
        /// 用来识别【别人把它改了】—— 统计数字那种是运行时赋值的✧
        /// 只按 Original 翻译的话 ✧ 数字一变就会被拉回旧值 ✧ 来回跳 ✓
        /// 有了这张表 ✧ 一发现值变了就把它当新的原文 ✧ 不再拉回去 ✓
        /// ★★★ 正在翻译中 ✧ 期间不允许重绘 ★★★
        ///
        /// Walk 是一个控件一个控件改的 ✧ 改完就 Invalidate ✓
        /// 但那样会出现一个问题：消息循环可能在 Walk 跑到一半时就画一帧 ✧
        /// 屏幕上就是【半中半英】✧ 用户看到的就是闪一下 ✓
        /// —— 所以翻译期间先不标脏 ✧ 全部改完再统一标 ✓
        // ★★★ 翻译期间冻结重绘 ★★★
        //
        // 上一轮只是"不主动标脏"✧ 但消息队列里可能已经排着 WM_PAINT ✓
        // 它在 Walk 跑到一半时被处理 ✧ 照样画出【半中半英】的一帧 ✓
        // 用 WM_SETREDRAW 把整个窗口冻结 ✧ 改完再解开 ✧ 中间哪条都丢掉 ✓
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        const int WM_SETREDRAW = 0x000B;

        static bool walking = false;

        /// ★ 这次翻译里【真的改过 .Text 的控件】 ✓
        ///
        /// 冻结期间 Invalidate 会被丢弃 ✧ 所以解冻后要重新标脏 ✓
        /// 但不能标整棵树：那会先把整块擦白、再一个个画 ✧
        /// 中间几帧就是空白的 ✧ 用户看到的就是闪 ✓
        static readonly List<Control> dirtyControls = new List<Control>();

        /// ★★★ 待清理的区域 ★★★
        ///
        /// AutoSize 的 Label 文字变短时 ✧ 旧的那一截没人擦 ✓
        /// 但 Walk 是在【冻结期间】跑的 ✧ 那时 Invalidate 会被丢弃 ✓
        /// 所以先记下来 ✧ 等解冻后再标 ✓
        static readonly List<KeyValuePair<Control, Rectangle>> pendingClean =
            new List<KeyValuePair<Control, Rectangle>>();

        /// ★ 把一个窗口的重绘冻结 / 解冻 ✓
        ///
        /// 用在"一批修改要一起生效"的场景 ✧ 切语言就是 ✓
        /// 冻结期间不管多少条 WM_PAINT 都被丢掉 ✧ 解冻后只画一次 ✓
        public static void Freeze(Control root)
        {
            if (root == null) return;
            try { SendMessage(root.Handle, WM_SETREDRAW, 0, 0); } catch { }
            // ★ 同时挂起布局 ✓
            //   不然右侧卡片 Clear+重建时会触发整个窗体重算布局 ✧
            //   那一瞬间状态行那一格会变窄 ✧ 文字被裁掉后半段 ✓
            //   （实测：会出现两帧只显示 "生效: QQ, Code" ✓）
            try { root.SuspendLayout(); } catch { }
        }

        public static void Thaw(Control root)
        {
            if (root == null) return;
            try
            {
                SendMessage(root.Handle, WM_SETREDRAW, 1, 0);
                root.ResumeLayout(false);   // ★ 恢复布局 ✧ 宽度回到最终值 ✓

                // ★★★ 先处理【待清理区域】★★★
                //   AutoSize 的 Label 文字变短时 ✧ 旧的那一截没人擦 ✓
                //   但 Walk 跑在冻结期间 ✧ 那时 Invalidate 会被丢弃 ✓
                //   所以 Walk 里只是记下来 ✧ 到这里才真正标 ✓
                foreach (KeyValuePair<Control, Rectangle> kv in pendingClean)
                {
                    try { kv.Key.Invalidate(kv.Value, true); } catch { }
                }
                pendingClean.Clear();

                // ★★★ 解冻后必须标脏 ✧ 否则屏幕停在冻结前那一帧 ★★★
                //
                // 原来只标【dirtyControls】里的控件 ✧ 但那个列表是
                // Lang.Apply 里的 Walk 填的 ✓
                // 而点风格 / 点语气走的是 ApplyLookAt / ApplyToneAt ✧
                // 它们根本不调 Walk ✧ 所以列表是空的 ✓
                // —— 结果：解冻后一个像素都没标脏 ✧ 屏幕不动 ✓
                //   （实测："整个页面都不刷新加载了" ✓）
                //
                // 所以：无条件把整棵树标脏 ✧ 而且只在解冻后标这一次 ✓
                //   冻结期间一直没画 ✧ 所以这一次就是最终状态 ✓
                try
                {
                    foreach (Control c in dirtyControls)
                    {
                        try
                        {
                            c.Invalidate();
                            NavButton nb = c as NavButton;
                            if (nb != null) nb.Invalidate();
                        }
                        catch { }
                    }
                    dirtyControls.Clear();
                    root.Invalidate(true);
                }
                catch { }
                if (root.IsHandleCreated)
                {
                    root.BeginInvoke((MethodInvoker)delegate
                    {
                        try { root.Update(); } catch { }
                    });
                }
                else root.Update();
            }
            catch { }
        }

        static readonly ConditionalWeakTable<Control, string> LastSeen =
            new ConditionalWeakTable<Control, string>();

        /// ★ 中文原文 → 英文。
        static readonly Dictionary<string, string> EN = new Dictionary<string, string>
        {
            { "AI 写开头结尾", "AI writes opener + closer" },
            { "关：用规则　整句：模型重写整句　AI：开头结尾现场写",
              "Off: rules   Whole: model rewrites   AI: opener + closer written live" },
            { "大模型改写（让模型直接写语气）", "Model rewriting (let the model write the tone)" },
            { "不用模型", "No model" },
            { "整句重写", "Rewrite whole line" },
            { "整句重写（自动）", "Rewrite whole line (auto)" },
            { "只生成包装", "Wrapper only" },
            { "接口地址", "API address" },
            { "密钥", "API key" },
            { "本地服务不用填", "Not needed for local servers" },
            { "关：用规则　整句：模型重写整句　包装：只要句首句尾", "Off: rules   Whole: model rewrites   Wrapper: only head/tail" },
            { "已关闭：照旧用规则装饰", "Off: decorations come from the rules again" },
            { "记得点一下下面的保存（设置页最后那个）", "Remember to press Save at the bottom of this page" },
            { "先填一个接口地址", "Fill in an API address first" },
            { "正在问模型…", "Asking the model..." },
            { "没回应：", "No answer: " },
            { "模型回了：", "Model said: " },

            // ---- 补充翻译 ----
            { "当前生效：外观风格「", "Active: look \"" },
            { "」　+　语气「", "\"  + tone \"" },
            { "　+　程度「", "  + level \"" },
            { "　（已写入 rules.txt）", "  (written to rules.txt)" },
            { " (外观风格 -> 语气关闭，词库保留)", " (look -> tone off, pools kept)" },
            { " 个", " more" },
            { " 等 ", " and " },
            { " 次）", " times)" },
            { " 次，继续加油喵～", " times, keep going!" },
            { "\r\n\r\n累计喵化 ", "\r\n\r\nTotal " },
            { "喵化等级提升！\r\n\r\nLv.", "Level up!\r\n\r\nLv." },
            { "⚠ 有 ", "Warning: " },
            { " 个快捷键已被别的软件占用，没能注册上；", " hotkey(s) are already taken by other apps and could not be registered;" },
            { "呜……快捷键被占用了", "Uh oh... the hotkey is taken" },
            { "取窗口信息出错：", "could not read window info: " },
            { "读不了这个文件：", "Cannot read this file: " },
            { " 太小", " too small" },
            { " 开关关", " paw off" },
            { " 有滚动条", " has scrollbar" },
            { "px，只有 ", "px, only " },
            { "!! PageNote 文字太长会被截断（需要 ", "!! PageNote text will be truncated (needs " },
            { "）\r\n", ")\r\n" },
            { "删除", "Delete" },
            { "发送", "Send" },
            { "退出", "Quit" },
            { "复制全文", "Copy all" },
            { "刷新预览", "Refresh preview" },
            { "重新载入", "Reload" },
            { "用记事本打开", "Open in Notepad" },
            { "打开主界面", "Open main window" },
            { "启动内置模型", "Start model" },
            { "查看日志…", "View log..." },
            { "更多 ›", "More ›" },
            { "管理 ›", "Manage ›" },
            { "更多风格 ›", "More ›" },
            { "管理规则 ›", "Manage ›" },
            { "编辑规则 rules.txt…", "Edit rules.txt..." },
            { "打开 rules.txt", "Open rules.txt" },
            { "重新加载规则", "Reload rules" },
            { "立即转换当前输入框", "Transform the current input box" },
            { "自动模式（停止打字后自动转换）", "Auto mode (transform after you stop typing)" },
            { "自动模式 ", "Auto " },
            { " 条替换规则", " rules" },
            { " 种颜文字", " kaomoji" },
            { "本次已喵化 ", "Meowed this run: " },
            { " 次\r\n", " times\r\n" },
            { "今日喵化　", "Today      " },
            { "等级　　　Lv.", "Level      Lv." },
            { "颜文字使用次数", "Kaomoji usage" },
            { "升到 Lv.", "Next Lv." },
            { " 还需要 ", " - need " },
            { " 次喵化　·　每 40 次升一级", " more · level up every 40" },
            { "不加（只加句尾）", "No tone (suffix only)" },
            { "已开启（自定义）", "On (custom)" },
            { "语气：", "Tone: " },
            { "模型", "Model" },
            { "没应答", "No response" },
            { "休息中喵……", "Resting..." },
            { "准备好喵～", "Ready!" },
            { "开心", "Happy" },
            { "难过", "Sad" },
            { "生气", "Angry" },
            { "疑问", "Question" },
            { "（还没有规则）", "(no rules yet)" },
            { "（还没有颜文字）", "(no kaomoji yet)" },
            { "未找到文件", "File not found" },
            { "找不到更新记录文件。", "Changelog file not found." },
            { "找不到「更新记录.txt」。\r\n\r\n", "\"更新记录.txt\" was not found.\r\n\r\n" },
            { "已复制 ✓", "Copied ✓" },
            { "　左键复制 · 右键删除", "  · left click copies, right click deletes" },
            { "至少要留一个颜文字。", "Keep at least one kaomoji." },
            { "这个颜文字已经在列表里了。", "That kaomoji is already in the list." },
            { "请把 rules.txt 发我看看。", "Please send me rules.txt." },
            { "喵化助手 · 已拦截", "Miaohua · Blocked" },
            { "喵化助手 · 恭喜", "Miaohua · Congrats" },
            { "已保存。\r\n\r\n快捷键如果改了，需要重开一次程序才会生效", "Saved.\r\n\r\nIf you changed the hotkey, restart the app for it to take effect" },
            { "（缓存已清空，可直接用）", "(cache cleared - ready to use)" },
            { "只读。用「用记事本打开」改，存盘后点「重新载入」。　", "Read-only. Use \"Open in Notepad\" to edit, then click \"Reload\"." },
            { "只在整段末尾加一次", "Once, at the very end" },
            { "每个句尾都加", "Every sentence end" },
            { "句号和逗号后面都加", "After commas too" },
            { "① 喵化程度 —— 决定多久加一次装饰", "1. Meow level - how often to decorate" },
            { "② 外观风格 —— 只改句尾装饰，会关掉语气", "2. Look - changes only the tail; turns the tone off" },
            { "③ 语气改写 —— 二选一，点了会把外观风格换掉", "3. Tone - pick one; choosing it replaces the look" },
            { "它们只是外观，不影响转换结果。", "These are cosmetic and do not affect the result." },
            { "四项美化可以单独关掉，关掉哪项、下次打开程序就不显示哪项。", "Each of the four decorations can be turned off separately." },
            { "这些设置就是 rules.txt 里对应的几行，保存后文件里也能看到、能直接改。", "These are just lines in rules.txt - you can edit them there too." },
            { "界面那四项也是重开程序后生效。", "The four UI options also take effect after a restart." },
            { "（Windows 的全局热键只能在启动时注册）。\r\n\r\n", "(Windows only registers global hotkeys at startup.)\r\n\r\n" },
            { "毫秒。页面淡入用多久。400 是原来写死的值，越长越容易看清。", "Milliseconds. How long the page fade takes. 400 was the old fixed value." },
            { "百分比。100 = 滑块和淡化一样长（默认）；53 = 滑块先到位（最早的手感）；150 = 滑块更慢。", "Percent. 100 = slider and fade finish together (default); 53 = slider first; 150 = slider slower." },
            { "喵化助手 ", "Miaohua " },
            { "一个本地运行的小工具：在 QQ 里打完字，停一下，整段文字就会变成喵喵的。\r\n\r\n", "A small local tool: type in QQ, pause, and the whole line turns into meows.\r\n\r\n" },
            { "── 它能做什么 ──\r\n", "-- What it does --\r\n" },
            { "· 句尾装饰：每个句子后面加后缀和随机颜文字\r\n", "- Tail decoration: adds a suffix and a random kaomoji to every sentence\r\n" },
            { "· 三种程度：轻度（只加一次）/ 普通（每句）/ 重度（连逗号）\r\n", "- Three levels: light (once) / normal (every sentence) / heavy (commas too)\r\n" },
            { "· 两套风格：外观风格只改句尾；语气改写会替你在句首句尾加话\r\n", "- Two style kinds: looks change only the tail; tones add words at both ends\r\n" },
            { "· 语气会跟着情绪变：疑问 / 开心 / 难过 / 生气 / 普通 各走各的词库\r\n", "- Tone follows mood: question / happy / sad / angry / normal each have their own pools\r\n" },
            { "· 智能语气（可选）：用你自己电脑上的模型判断情绪，比关键词准得多\r\n", "- Smart tone (optional): uses a model on your own PC, far more accurate than keywords\r\n" },
            { "· 两种触发：全局快捷键，或停止打字后自动转换\r\n\r\n", "- Two triggers: a global hotkey, or auto-transform after you stop typing\r\n\r\n" },
            { "── 它不会做什么 ──\r\n", "-- What it does NOT do --\r\n" },
            { "· 默认不联网。断网也能完整使用，只是语气判断用关键词做\r\n", "- No network by default. Works fully offline; tone falls back to keywords\r\n" },
            { "· 只有你打开「智能语气」时，它才会连 127.0.0.1，也就是你自己这台电脑\r\n", "- Only with Smart tone on does it connect to 127.0.0.1 - your own machine\r\n" },
            { "  上的模型服务（LM Studio 之类）。全程不出这台机器，也不经过任何服务器\r\n", "  (LM Studio or similar). Nothing leaves your PC and no server is involved\r\n" },
            { "· 模型只负责判断情绪（回一个词），加进你句子里的话仍然是 rules.txt 里你自己配的\r\n", "- The model only picks the mood (one word). The words added are still yours, from rules.txt\r\n" },
            { "· 模型没开着、或者判断不出来，会自动退回关键词判断，不会卡住也不会出错\r\n", "- If the model is off or unsure, it falls back to keywords - no hang, no error\r\n" },
            { "· 不上传、不保存你输入的内容（只在你自己的输入框里原地改写）\r\n", "- Your text is never uploaded or stored (edited in place in your own input box)\r\n" },
            { "· 不碰网址、邮箱、文件路径和含数字的编号\r\n", "- Leaves URLs, emails, file paths and numeric codes alone\r\n" },
            { "· 不改代码里的东西，也不碰你没列进「生效程序」的应用\r\n\r\n", "- Does not touch code, and only works in the apps you whitelist\r\n\r\n" },
            { "── 文件在哪 ──\r\n", "-- Where the files are --\r\n" },
            { "· rules.txt　规则（设置页和风格页改的就是它）\r\n", "- rules.txt   the rules (what the Settings and Styles pages edit)\r\n" },
            { "· miaohua.log　运行日志，快捷键没生效时先看这里\r\n", "- miaohua.log   the log; check here first if the hotkey does nothing\r\n" },
            { "· stats.txt　喵化次数统计\r\n", "- stats.txt   meow counters\r\n" },
            { "· 都在程序所在目录，用下面的按钮可以直接打开。\r\n\r\n", "- All live next to the program; the buttons below open them.\r\n\r\n" },
            { "── 一句话提醒 ──\r\n", "-- One reminder --\r\n" },
            { "语气改写会改变你「说的内容」。要谈正事之前，先到「喵化风格」里点一下「萌系」把它关掉。", "Tone rewriting changes what you actually say. Before serious talk, open Styles and tap any look to switch it off." },
            { "这个软件是【完全免费】的\r\n", "This app is completely free\r\n" },
            { "如果你是花钱买的，说明你被骗了 —— 请立即找商家退款。\r\n", "If you paid for it, you were scammed - ask for a refund.\r\n" },
            { "它没有在任何平台出售，也没有\"付费版\"\"高级版\"之类的东西。", "It is not sold anywhere and has no paid or premium version." },
            { "自己做一份也行——就是一个纯文本文件，怎么改都行。", "You can also make your own - it is just a plain text file." },
            { "它应该和程序放在同一个包里（电脑版文件夹的上一层）。\r\n", "It should sit next to the program (one level above the PC folder).\r\n" },
            { "模型服务在运行 ✓　", "Model server is running ✓  " },
            { "模型已经在运行了 ✓　地址 ", "Model already running ✓  at " },
            { "模型服务没在跑 ✗　程序暂时按关键字判断情绪（结果照常，只是不懂否定句）", "Model server is not running ✗  falling back to keywords (results are the same, but negations are missed)" },
            { "正在问本机模型…（最多等 ", "Asking the local model... (up to " },
            { " 毫秒）", " ms)" },
            { "正在后台启动内置模型…（第一次要加载约 2GB 模型，请稍等十几秒）", "Starting the bundled model in the background... (first load is about 2 GB, please wait ~15s)" },
            { "模型文件夹里还没有 .gguf。", "There is no .gguf in the model folder yet." },
            { "可以用带模型的整包，或者自己开 LM Studio 的本地服务。", "Use the bundle that includes the model, or start your own LM Studio server." },
            { "　（智能语气目前是关的）", "  (Smart tone is currently off)" },
            { "　（智能语气开着，但没有模型时会退回关键字判断）", "  (Smart tone is on; without a model it falls back to keywords)" },
            { "　—— 内置模型没起来的话，看「查看日志」里的 model: 那几行", "  - if the bundled model did not start, check the model: lines in View log" },
            { "LM Studio 里已加载的模型标识；Ollama 填 qwen2.5:3b 这样的名字。留空用服务器上的第一个", "Model id loaded in LM Studio; for Ollama use a name like qwen2.5:3b. Empty = first on the server" },
            { "只连 127.0.0.1，也就是你自己这台电脑，不会连外网。模型只负责判断情绪，", "Connects only to 127.0.0.1 - your own machine, never the internet. The model only picks the mood; " },
            { "加的词还是下面那几行里你自己配的。模型没开着就自动退回关键字判断。", "the words added are still the ones you configured below." },
            { "图片保护 = ", "Image guard = " },
            { "开 —— 输入框里有图片就不转换", "On - skip when the box contains an image" },
            { "关 —— 照样转换（图片会被弄没）", "Off - transform anyway (the image will be lost)" },
            { "（输入框里有图片就不转换）", "(no transform when the box has an image)" },
            { "（照样转换，图片会被弄没）", "(transform anyway - the image will be lost)" },
            { "先去把图片去掉，再转换。", "Remove the image first, then transform." },
            { "全选替换会把图片弄没。要转换就先把图片去掉。", "Select-all replace destroys images. Remove it first if you want to transform." },
            { "删掉一部分装饰之后再转换\r\n", "Trim some decorations and try again\r\n" },
            { "喵化助手已启动", "Miaohua started" },
            { "喵化助手已启动（有快捷键冲突）", "Miaohua started (hotkey conflict)" },
            { "有 ", "" },
            { " 个快捷键被别的软件占用了。", " hotkey(s) are taken by other apps." },
            { "打开 rules.txt 换一个组合，再按 ", "Open rules.txt, pick another combo, then press " },
            { " 重载。", " to reload." },
            { "在 QQ 里打完字，按 ", "Type in QQ, then press " },
            { " 就能喵化。双击这个图标可以看状态。", " to meow it. Double-click this icon for status." },
            { "再双击那个粉色猫头就能打开主界面。", "Double-click the pink cat head to open the window." },
            { "喵化助手已经在运行了，不用再开一个。\n\n", "Miaohua is already running - no need to start another.\n\n" },
            { "它可能正缩在右下角任务栏的托盘里。\n", "It is probably tucked in the system tray.\n" },
            { "看不到图标的话，点一下任务栏上的 ^ 展开隐藏的图标，\n", "If you cannot see the icon, click ^ in the taskbar to show hidden icons,\n" },
            { "这次改动会丢掉不属于它的行（词规则或注释），已经拦下没有写入。\r\n\r\n", "This change would have dropped lines it does not own (rules or comments), so it was blocked.\r\n\r\n" },
            { "这次改动会把规则文件从 ", "This change would shrink the rules file from " },
            { " 条内容削减到 ", " entries to " },
            { " 条，已经拦下没有写入。\r\n\r\n", " entries, so it was blocked.\r\n\r\n" },
            { "如果这确实是你想要的（比如想清空重写），请直接编辑 rules.txt。", "If you really want this (for example a full rewrite), edit rules.txt directly." },
            { "内容比现有规则少太多，已拦截。请检查是不是不小心清空了编辑框。", "Far fewer entries than before, so it was blocked. Did you clear the box by accident?" },
            { "请到「规则」页改 hotkey_ 开头的设置。", "Change the hotkey_ settings on the Rules page." },
            { "⚠ 语气改写会替你在句子开头和结尾加话，等于改了你「说的内容」。\r\n", "Warning: tone rewriting adds words at both ends - it changes what you actually say.\r\n" },
            { "　　两个风格是二选一的，点外观风格就会把它关掉——你的词库不会被删，点回语气风格就全回来。", "The two are either/or: picking a look turns the tone off. Your word pools are kept - pick a tone to bring them back." },
            { "轻度 = 只在整段末尾加一次　　普通 = 每个句号/问号/感叹号后都加　　重度 = 连逗号后面也加", "Light = once at the end.  Normal = after every . / ? / !  Heavy = after commas too" },
            { "启用", "Enable" },
            { "禁用", "Disable" },
            { "（没有更新记录）", "(no changelog)" },
            { "　（", "  (" },
            { " 字）", " chars)" },
            { "出错: ", "error: " },
            { "喵化助手", "Miaohua" },
            { "试一下", "Try it" },
            { "喵化规则", "Rules" },
            { "喵化风格", "Styles" },
            { "颜文字库", "Kaomoji" },
            { "设置", "Settings" },
            { "使用统计", "Stats" },
            { "更新公告", "News" },
            { "关于", "About" },
            { "打完字按一下，整段变成喵喵的",
              "Type, hit the key,\r\nthe line becomes meows" },
            { "今天是超级喵化日！", "It's a super meow day!" },
            { "今天已经喵了好多次啦！", "Meowed a lot today!" },
            { "今天也要加油喵～", "Do your best today, meow~" },
            { "喵化实验室　输入你想说的话，下面立刻显示效果", "Meow Lab — type anything, the result shows below" },
            { "喵化之后", "After" },
            { "你打的字", "You type" },
            { "喵化实验室", "Meow Lab" },
            { "运行中", "Live" },
            { "正常工作", "OK" },
            { "条替换规则", "rules" },
            { "种颜文字", "faces" },
            { "本次已喵化", "this run" },
            { "累计喵化　", "Total " },
            { "喵化次数", "Meow count" },
            { "今日", "Today" },
            { "喵化等级", "Level" },
            { "距离 Lv.", "Next Lv." },
            { "最常用", "most used" },
            { "立即喵化", "Meow now" },
            { "立即转换", "Transform" },
            { "复制结果", "Copy result" },
            { "再试一次", "Try again" },
            { "再来一次", "Again" },
            { "清空", "Clear" },
            { "保存设置", "Save settings" },
            { "保存界面设置", "Save UI settings" },
            { "查看日志", "View log" },
            { "退出程序", "Quit" },
            { "隐藏到托盘", "Hide to tray" },
            { "打开规则文件", "Open rules file" },
            { "用记事本打开规则", "Open rules in Notepad" },
            { "打开所在文件夹", "Open folder" },
            { "测试连接", "Test LLM" },
            { "重新载入规则", "Reload rules" },
            { "重置为默认", "Reset to default" },
            { "添加规则", "Add rule" },
            { "添加颜文字", "Add kaomoji" },
            { "管理", "Manage" },
            { "管理规则", "Manage" },
            { "更多", "More" },
            { "收起", "Less" },
            { "全局快捷键", "Global hotkeys" },
            { "暂停 / 恢复", "Pause / resume" },
            { "重载规则", "Reload rules" },
            { "自动模式", "Auto" },
            { "停止打字后", "After you stop typing" },
            { "生效程序", "Apps" },
            { "图片保护", "Image guard" },
            { "图片保护 —— 有图片就不转换", "Image guard — skip when the box has an image" },
            { "停止打字后自动转换", "Transform after I stop typing" },
            { "暂停（所有转换都不生效）", "Pause (nothing is transformed)" },
            { "开机自动启动", "Start with Windows" },
            { "界面", "Appearance" },
            { "动画时长", "Animation speed" },
            { "滑块比例", "Slider size" },
            { "顶部猫娘状态气泡", "Top cat bubble" },
            { "数字卡片", "Number cards" },
            { "实验室", "Lab" },
            { "颜文字胶囊", "Kaomoji pills" },
            { "导航图标", "Nav icons" },
            { "等级进度", "Level bar" },
            { "配色", "Colors" },
            { "爪印", "Paw prints" },
            { "心情气泡", "Mood bubble" },
            { "动画", "Animation" },
            { "智能语气（用本机大模型判断情绪）", "Smart tone (a local model reads the mood)" },
            { "开启智能语气", "Enable smart tone" },
            { "模型地址", "Model address" },
            { "模型名", "Model name" },
            { "喵化程度 —— 决定多久加一次装饰", "Meow level — how often to decorate" },
            { "外观风格 —— 只改句尾装饰，会关掉语气", "Look — changes only the tail; tone turns off" },
            { "语气改写 —— 二选一，点了会把外观风格换掉", "Tone — pick one; it replaces the look" },
            { "轻度", "Light" },
            { "普通", "Normal" },
            { "重度", "Heavy" },
            { "原文", "Original" },
            { "替换内容", "Replace with" },
            { "文本替换", "Text rules" },
            { "模板", "Template" },
            { "后缀", "Suffix" },
            { "开头", "Opener" },
            { "结尾", "Closer" },
            { "猫娘台词随次数变化", "Lines change as the count grows" },
            { "每次转换从这里随机挑一个。", "One is picked at random each time." },
            { "已暂停", "Paused" },
            { "管理颜文字", "Manage" },
            { "添加一条", "Add one" },
            { "切换为经典模式", "Classic mode" },
            { "点击编辑", "click to edit" },
            { "未设置", "not set" },
            { "更多风格", "More" },
            { "喵化中…", "Meowing…" },
            { "这里会显示喵化后的结果", "The result shows here" },
            { "在这里输入你想说的话…", "Type something here…" },
            { "生成结果", "Generate" },
            { "喵化助手正在工作", "Miaohua is running" },
            { "已就绪", "Ready" },
            { "正在启动内置模型…", "Starting the built-in model…" },
            { "本地服务没有响应，正在启动内置模型…", "Local service offline - starting the built-in model…" },
            { "连接成功 ✓　「今天好开心啊哈哈」被判为：", "Connected - a happy sample reads as: " },
            { "连不上：", "Cannot reach: " },
            { "冲突", "Conflict" },
            { "休息", "Idle" },
            { "快捷键被占用", "Hotkey taken" },
            { "暂停中，不转换", "Paused" },
            { "今天的统计", "Today" },
            { "今天也要加喵~", "Time to meow~" },
            { "毫秒。300 左右比较跟手，打字慢的话调到 600~900。", "ms. Around 300 feels responsive; 600-900 if you type slowly." },
            { "进程名，不要带 .exe。多个用逗号或空格分开。", "Process names, no .exe. Separate with commas or spaces." },
            { "模型服务没在跑", "No model running" },
            { "运行状态四个数字卡", "Status number cards" },
            { "实验室强化（中间猫娘、粉底结果框、大按钮）", "Lab extras (cat, pink result box, big button)" },
            { "实验室强化", "Lab extras" },
            { "颜文字库胶囊网格", "Kaomoji pill grid" },
            { "导航猫系图标", "Cat nav icons" },
            { "颜色分层（降低饱和度）", "Color shading (less saturated)" },
            { "颜色分层", "Color shading" },
            { "升级时提示", "Update notice" },
            { "卡片角落淡猫爪", "Paw prints on cards" },
            { "动画与过渡", "Animation & transitions" },
            { "今日统计", "Today" },
            { "次", "times" },
            { " 次", " times" },
            { " 还差 ", " need " },
            { "还差 ", "need " },
            { " 次　最常用 ", " more, top: " },
            { "萌系", "Cute" },
            { "元气", "Cheerful" },
            { "慵懒", "Sleepy" },
            { "撒娇", "Clingy" },
            { "冷淡", "Cold" },
            { "颜文字在前", "Kaomoji first" },
            { "自定义", "Custom" },
            { "傲娇", "Tsundere" },
            { "黏人", "Needy" },
            { "委屈", "Sad" },
            { "应激", "Startled" },
            { "不加语气", "No tone" },
            { "语气改写", "Tone" },
            { "外观风格", "Look" },
            { "再喵一次", "Meow again" },
            { "喵化中~", "Meowing~" },
            { "这里会显示喵化后的结果…", "The result shows here…" },
            { "清空输入", "Clear" },
            { "每次转换从这里随机挑一个。改动立即写入 rules.txt（只动「颜文字」那几行）。", "One is picked at random each time. Edits are written straight to rules.txt (only the kaomoji lines)." },
            { "四项美化可以单独关掉，关掉哪项、下次打开程序就不显示哪项。它们只是外观，不影响转换结果。", "Each of these can be turned off on its own; the program remembers it next time. They are cosmetic only and never change the result." },
            { "改完点这个保存，下次打开程序生效。上面那个「保存设置」是同一件事。", "Click to save; takes effect next launch. The \"Save settings\" button above does the same thing." },
            { "只连 127.0.0.1，也就是你自己这台电脑，不会连外网。模型只负责判断情绪，加的词还是下面那几行里你自己配的。模型没开着就自动退回关键字判断。", "Connects only to 127.0.0.1 (your own machine), never the internet. The model only reads the mood; the words it adds still come from the lines below. If no model is running it falls back to keyword matching." },
            { "读取失败", "Could not read" },
            { "语言 / Language", "Language / 语言" },
            { "语言", "Language" },
            { "中文", "Chinese" },
            { "英文", "English" },
            { "确定", "OK" },
            { "取消", "Cancel" },
            { "关闭", "off" },
            { "保存", "Save" },
            { "喵化成功！", "Done!" },
            { "暂停/恢复", "Pause" },
            { "条替换", "rules" },
            { "个颜文字", "kaomoji" },
            { "开", "on" },
            { "关", "off" },
            { "添加", "Add" },
            { "当前生效：", "In effect: " },
            { "语气", "Tone" },
            { "程度", "Meow level" },
            { "未套用", "none" },
            { "已写入 rules.txt", "written to rules.txt" },
            { "轻度 = 只在整段末尾加一次　普通 = 每个句号/问号/感叹号后都加　重度 = 连逗号后面也加", "Light = once at the very end.  Normal = after every . ! ?  Heavy = after commas too." },
            { "轻度 = 只在整段末尾加一次", "Light = once at the very end" },
            { "普通 = 每个句号/问号/感叹号后都加", "Normal = after every . ! ?" },
            { "重度 = 连逗号后面也加", "Heavy = after commas too" },
            { "二选一：点外观就关语气，点语气就换掉句尾装饰（词语替换不受影响）。", "Pick one: choosing a look turns the tone off, and vice versa. Word replacements are unaffected." },
            { "语气改写会替你在句子开头和结尾加话，等于改了你「说的内容」。", "Tone adds words at the start and end of your sentence - it changes what you actually say." },
            { "两个风格是二选一的，点外观风格就会把它关掉——你的调校不会被销毁，点回语气风格就全回来。", "Look and tone are either/or: picking a look switches the tone off. Your tuning is not lost - pick the tone again and it all comes back." },
            { "生效", "Active" },
            { "全部程序", "all apps" },
            // ---- 2026-10-18 补漏 ----
            { "喵化中～", "Meowing~" },
            // ---- 2026-10-18 风格名（胶囊上的名字）----
            { "温柔", "Gentle" },
            { "害羞", "Shy" },
            { "中二", "Chuuni" },
            { "天然呆", "Airhead" },
            { "毒舌", "Sharp" },
            { "口癖", "Tic" },
            { "病娇", "Yandere" },
            { "热血", "Hyped" },
            { "只有喵~", "Just meow~" },
            { "高冷", "Aloof" },
            { "腹黑", "Scheming" },
            { "只连本机、不连外网。模型只判断情绪，加的词还是你自己配的。", "Local only, never the internet. The model only reads the mood; the words it adds are still yours." },
            { "喵化，让聊天更有趣！", "Meow it - make chats more fun!" },
            { "🐾 立即喵化", "🐾 Meow now" },
            // ---- 2026-10-18 新增功能（2.16.3）----
            { "我的组合", "My combos" },
            { "+ 存为组合", "+ Save combo" },
            { "存为组合", "Save combo" },
            { "☆ 我的组合（风格 + 程度 + 自动 + 延迟）", "☆ My combos (style + level + auto + delay)" },
            { "组合名字（最多 8 个字）", "Combo name (up to 8 chars)" },
            { "还没有组合 —— 点下面「+ 存为组合」把当前风格存下来", "No combos yet - click \"+ Save combo\" below to store the current style" },
            { "先点一个外观或语气风格，再存组合。", "Pick a look or tone style first, then save a combo." },
            { "组合只记风格名，改过的词库不会被覆盖", "Combos store only style names, so tuned word pools are never overwritten" },
            { "删除组合", "Delete combo" },
            { "删掉这个组合", "Delete this combo" },
            { "名字不能空着。", "Name cannot be empty." },
            { "名字里不能有 |", "Name cannot contain |" },
            { "这个名字已经有了，换一个。", "That name is taken - pick another." },
            { "当前风格：", "Current style: " },
            { "★ 随机来一套", "★ Roll a random set" },
            { "随机到了：", "Rolled: " },
            { "语气叠加", "Tone stacking" },
            { "启用语气风格叠加（外观和语气同时生效）", "Enable tone stacking (look and tone at the same time)" },
            { "叠加开着：外观管句尾装饰，语气管整句说法，两个同时生效。", "Stacking on: the look owns the ending, the tone owns how the sentence reads - both apply." },
            { " (外观风格，语气保持不变)", " (look only, tone unchanged)" },
            { " (外观风格 -> 语气关闭)", " (look -> tone off)" },
            { "② 外观风格 —— 只改句尾装饰，不动语气（可以叠加）", "2) Look styles - change only the ending, never the tone (stackable)" },
            { "按软件分配…", "Per-app styles…" },
            { "按软件分配风格", "Per-app styles" },
            { "进程自动", "Process auto" },
            { "进程名", "Process name" },
            { "进程名不能空着。", "Process name cannot be empty." },
            { "用哪个风格", "Which style" },
            { "这个软件里自动转换（不勾 = 只能按快捷键）", "Auto-convert in this app (unchecked = hotkey only)" },
            { "在指定程序里用指定风格，也能单独关自动转换", "Use a given style in a given app, and turn auto off per app" },
            { "在指定的程序里自动用指定的风格 ✗ 没写的程序用全局设置 ✓", "Apps listed here use their own style; anything else uses the global setting." },
            { "没写的程序用全局设置～", "Apps not listed use the global setting~" },
            { "在不同的软件里用不同的风格～", "Different styles in different apps~" },
            { "不加（完全不喵化）", "None (no meow at all)" },
            { "删除自定义语气", "Delete custom tone" },
            { "删除自定义风格", "Delete custom look" },
            { "删除这个自定义语气", "Delete this custom tone" },
            { "删除这个自定义风格", "Delete this custom look" },
            { "新建自定义语气", "New custom tone" },
            { "新建自定义风格", "New custom look" },
            { "+ 新建", "+ New" },
            { "+ 新建语气", "+ New tone" },
            { "名字（最多 8 个字）", "Name (up to 8 chars)" },
            { "句尾后缀（比如 喵★）", "Suffix (e.g. meow★)" },
            { "开头（一行一个，可留空）", "Openers (one per line, may be empty)" },
            { "结尾（一行一个，可留空）", "Closers (one per line, may be empty)" },
            { "颜文字（一行一个，至少 1 个）", "Kaomoji (one per line, at least 1)" },
            { "至少要有一个颜文字。", "At least one kaomoji is required." },
            { "后缀不能空着。", "Suffix cannot be empty." },
            { "保存失败，看看日志。", "Save failed - check the log." },
            { "以后点一下就能切回来～", "One click brings it back later~" },
            { "打造属于你自己的喵化风格～", "Build a meow style of your own~" },
            { "让每一句话都带上你的语气～", "Give every sentence your tone~" },
            { "让每一句话，都变得喵喵的～", "Make every sentence meow~" },
            { "语气是改你说话的方式，不只是句尾～", "Tone changes how you speak, not just the ending~" },
            { "最近喵化（最多 20 条，只在本次运行内）", "Recent (last 20, this session only)" },
            { "原文　", "Original  " },
            { "结果　", "Result    " },
            { "导出配置", "Export config" },
            { "导入配置", "Import config" },
            { "导出配置…", "Export config…" },
            { "导入配置…", "Import config…" },
            { "导出配置（规则 + 风格）", "Export config (rules + styles)" },
            { "导入配置（规则 + 风格）", "Import config (rules + styles)" },
            { "导出成功", "Export OK" },
            { "导入成功", "Import OK" },
            { "已导出：", "Exported: " },
            { "已导入 ", "Imported " },
            { "导出失败：", "Export failed: " },
            { "导入失败：", "Import failed: " },
            { "导入会覆盖当前的规则和风格文件，确定吗？", "Import overwrites the current rules and style files. Continue?" },
            { "导出 = 把规则和风格打成一个 zip；导入 = 选 zip 还原（会覆盖当前的）。", "Export packs rules and styles into a zip; import restores from one (overwrites current)." },
            { " 个文件，规则已重新加载。", " file(s); rules reloaded." },
            { "正在从 ", "Loading from " },
            { "取消收藏", "Unfavourite" },
            { "收藏这个", "Favourite this" },
            { "删除选中", "Delete selected" },
            { "小模型语气", "Tiny mood model" },
            { "自动加载", "Auto-load" },
            { "自动（用最大的）", "Auto (largest)" },
            { "模型：", "Model: " },
            { "模型：自动", "Model: auto" },
            { "　·　当前模型：", " - current model: " },
            { "正在检查模型服务…", "Checking model service…" },
            { " …（十几秒）", " …(10s or so)" },
            { "这个文件夹里没有带模型（缺 llama-server.exe）。", "This folder has no bundled model (llama-server.exe is missing)." },
            { "模型文件夹里还没有 .gguf —— 把下载的模型丢进「电脑版/模型/」就行", "No .gguf in the model folder yet - drop a downloaded model into the model folder" },
            { "　—— 想用内置模型就点旁边那个「启动内置模型」✓", "  - to use the bundled model, click \"Start bundled model\" ✓" },
            { "外观：", "Look: " },
            { "生效：外观「", "Active: look \"" },
            { "」·语气「", "\" - tone \"" },
            { "·程度「", " - level \"" },
            { "不加", "None" },
            { "删掉「", "Delete \"" },
            { " 换成 ", " -> " },
            { "!! 提示文字真的会被截断（需要 ", "!! Hint text will really be truncated (needs " },
            { "（只显示最近的部分，完整记录请用「用记事本打开规则」旁边那个文件）", "(only the recent part is shown; the full log is the file next to Open rules in Notepad)" },
            { "自动", "Auto" },
            { "只按热键", "hotkey only" },
        };

        /// ★ 整串查不到时用的兜底：把串里较长的中文片段换掉。
        ///
        /// 为什么需要：有些文字是拼出来的 ✗ 整串在字典里没有 ✓
        ///     "更多风格 ›"        （带了个箭头符号）
        ///     "距离 Lv.5 还差 6 次"（带数字）
        ///     "每次转换从这里随机挑一个。改动立即写入 rules.txt（只动「颜文字」那几行）。"
        /// 这些查一次表肯定查不到 ✗ 但里面【每一段】都在字典里 ✓
        ///
        /// ★ 只替换长度 >= 3 的 key ✗
        ///   否则"次"这种单字会把句子里的每个"次"都换掉 ✗ 反而更乱 ✓
        static string[] partialKeys;

        static string PartialT(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (partialKeys == null)
            {
                List<string> ks = new List<string>();
                foreach (KeyValuePair<string, string> kv in EN)
                    if (kv.Key.Length >= 3) ks.Add(kv.Key);
                // 长的先换 ✗ 否则短的可能把长的一半吃掉 ✓
                ks.Sort(delegate(string a, string b) { return b.Length.CompareTo(a.Length); });
                partialKeys = ks.ToArray();
            }
            foreach (string k in partialKeys)
            {
                if (s.IndexOf(k, StringComparison.Ordinal) >= 0)
                    s = s.Replace(k, EN[k]);
            }
            return s;
        }
        /// ★ 查表。查不到就原样返回中文。
        public static string T(string zh)
        {
            if (!English || string.IsNullOrEmpty(zh)) return zh;
            string en;
            return EN.TryGetValue(zh, out en) ? en : zh;
        }

        /// ★ 把控件树里的界面文字换成当前语言。
        public static void Apply(Control root)
        {
            if (root == null) return;

            // ★★★ 翻译期间冻结重绘 ★★★
            IntPtr h = IntPtr.Zero;
            bool frozen = false;
            try
            {
                h = root.Handle;
                SendMessage(h, WM_SETREDRAW, 0, 0);
                frozen = true;
            }
            catch { }

            dirtyControls.Clear();
            pendingClean.Clear();
            walking = true;
            try
            {
                Walk(root, 0);
            }
            finally
            {
                walking = false;
            }

            try
            {
                if (frozen) SendMessage(h, WM_SETREDRAW, 1, 0);

                // ★★★ 先处理【待清理区域】★★★
                //   AutoSize 的 Label 文字变短时 ✧ 旧的那一截没人擦 ✓
                //   Walk 跑在冻结期间 ✧ 那时 Invalidate 会被丢弃 ✓
                //   所以 Walk 里只记下来 ✧ 到这里才真正标 ✓
                foreach (KeyValuePair<Control, Rectangle> kv in pendingClean)
                {
                    try { kv.Key.Invalidate(kv.Value, true); } catch { }
                }
                pendingClean.Clear();
            }
            catch { }

            // ★★★ 解冻后【等布局稳定】再标脏 ★★★
            //
            // 为什么不能马上标：
            //   刚解冻时布局还没跑 ✧ 控件的宽度可能还是 0 或很小 ✓
            //   Invalidate() 标的是【当前宽度那么大】的区域 ✧
            //   于是只重绘了左边一小块 ✧ 右边一直是白的 ✓
            //   （实测：状态行会出现三帧只剩 "生效: QQ, Code"、后半段空白 ✓）
            // —— 投递到消息队列 ✧ 那时布局已经稳定 ✧ 标脏区域就是全宽了 ✓
            if (root.IsHandleCreated)
            {
                try
                {
                    root.BeginInvoke((MethodInvoker)delegate
                    {
                        try
                        {
                            foreach (Control c in dirtyControls)
                            {
                                try
                                {
                                    c.Invalidate();
                                    NavButton nb = c as NavButton;
                                    if (nb != null) nb.Invalidate();
                                }
                                catch { }
                            }
                            dirtyControls.Clear();
                            // ★ 无条件整树标脏 ✧ 防止一像素都不刷 ✓
                            root.Invalidate(true);
                            root.Update();
                        }
                        catch { }
                    });
                    return;
                }
                catch { }
            }

            // 没句柄（窗口还没建好）就老实同步一次 ✓
            try
            {
                foreach (Control c in dirtyControls)
                {
                    try { c.Invalidate(); } catch { }
                }
                dirtyControls.Clear();
                root.Update();
            }
            catch { }
        }

        /// ★ 打了这个标记的控件（连同它里面的子控件）不参与自动翻译 ✓
        ///   语言开关按钮用它：它的文字由代码直接控制 ✗ 被翻译反而会错 ✓
        public const string NoTranslate = "nolang";

        static void Walk(Control c, int depth)
        {
            if (c == null || depth > 40) return;
            // ★ 语言开关本身不能被翻译 ✗ 否则切到英文后又被改回 "Chinese" ✓
            if (c.Tag is string && (string)c.Tag == NoTranslate) return;
            try
            {
                string now = c.Text;
                if (!string.IsNullOrEmpty(now))
                {
                    string last, prev;
                    bool haveLast = LastSeen.TryGetValue(c, out last);
                    bool havePrev = Original.TryGetValue(c, out prev);

                    // ★ 别人改过它吗 ✓
                    //   haveLast 说明"我们上次设过"✧ last 就是那个值 ✓
                    //   now != last → 有人在我们之后改了它 ✧
                    //   统计数字就是这种 ✧ 不重登的话会被拉回旧值来回跳 ✓
                    bool external = haveLast && last != now;

                    string zh;
                    if (!havePrev || external)
                    {
                        if (havePrev) { try { Original.Remove(c); } catch { } }
                        try { Original.Add(c, now); } catch { }
                        zh = now;
                    }
                    else
                    {
                        zh = prev;
                    }

                    string want = English ? PartialT(T(zh)) : zh;

                    // ★★★ 无条件记下"我们把它设成了什么" ★★★
                    //   之前只在"文字真的变了"时才记 ✧
                    //   于是没变的控件永远没有 LastSeen ✧
                    //   下次就无法判断它是不是被外部改了 ✓
                    try { LastSeen.Remove(c); } catch { }
                    try { LastSeen.Add(c, want); } catch { }


                    if (c.Text != want)
                    {
                        // ★★★ 文字变短时 ✧ 让父控件擦掉【旧的更大区域】★★★
                        //
                        // AutoSize = true 的 Label 宽度跟着文字走 ✧
                        //   英文比中文长 ✧ 切回中文时宽度变小 ✓
                        //   而它只擦【自己当前】那么大的区域 ✧
                        //   右边那块原来画英文的地方就没人擦 ✓
                        //   （实测："喵化实验室 输入你想说的话，下面立刻显示效果 ; below" ✓）
                        // —— 记下改之前的边界 ✧ 改完比一下 ✧ 变小了就把两者的并集标给父控件 ✓
                        // ★★★ 用【文字测量】判断变短 ✧ 不能靠 Bounds ★★★
                        //   AutoSize 的 Label 是【布局时】才调整大小 ✧
                        //   设完 c.Text 后立刻读 c.Bounds ✧ 读到的还是旧值 ✓
                        //   所以上一版的判断永远不成立 ✧ 等于没写 ✓
                        // ★★★ 无条件清理 ✧ 不做尺寸比较 ★★★
                        //
                        // 为什么不比：中英文宽度很可能差不多 ✧
                        //   中文 22 字 × 17px ≈ 374 ✧ 英文 47 字符 × 8px ≈ 376 ✓
                        //   两者几乎一样宽 ✧ 判断 beforeW > afterW 根本不成立 ✓
                        // 而且屏幕上确实有残留 ✧ 说明测量和实际渲染对不上 ✓
                        // —— 所以干脆用【旧的实际宽度】标脏 ✧ 它就是屏幕上占的地方 ✓
                        int oldW = 0;
                        try { oldW = c.Width; } catch { }

                        c.Text = want;

                        if (oldW > 0 && c.Parent != null)
                        {
                            // ★ 先记下来 ✧ 解冻后再标 ✓
                            //   Walk 跑在冻结期间 ✧ 现在 Invalidate 会被丢弃 ✓
                            try
                            {
                                pendingClean.Add(new KeyValuePair<Control, Rectangle>(
                                    c.Parent,
                                    new Rectangle(c.Left, c.Top, oldW + 8, c.Height)));
                            }
                            catch { }
                        }
                        // ★★★ 改了 .Text 必须自己重绘 ★★★
                        //   WinForms 不会因为文字变了就自动重画 ✧
                        //   它只是把值写进字段 ✧ 等下一次重绘（鼠标经过 / 窗口移动）才画 ✓
                        // ★ 翻译期间先不标脏 ✧ 全部改完再统一标 ✓
                        //   不然消息循环会在 Walk 跑到一半时画一帧 ✧
                        //   那一帧就是【半中半英】✧ 看起来就是闪 ✓
                        if (walking)
                        {
                            // ★ 翻译中 ✧ 先记下来 ✧ 等解冻后统一标脏 ✓
                            if (!dirtyControls.Contains(c)) dirtyControls.Add(c);
                        }
                        else
                        {
                            c.Invalidate();
                            // 有些自绘控件要额外踢一下
                            NavButton nb = c as NavButton;
                            if (nb != null) nb.Invalidate();
                        }
                    }
                }
            }
            catch { }
            try
            {
                foreach (Control k in c.Controls) Walk(k, depth + 1);
            }
            catch { }
        }

        /// ★ 给新建出来的控件补登记（窗口建好后再加的控件）。
        public static void Register(Control c)
        {
            if (c == null) return;
            Walk(c, 0);
        }
    }
}
