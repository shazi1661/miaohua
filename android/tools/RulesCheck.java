import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.List;

/**
 * Standalone check for the Android transform engine.
 * Only touches Rules.parse/transform, which never call into Android, so this runs
 * on a plain desktop JVM with android.jar on the classpath.
 *
 *   args[0] rules file
 *   args[1] output file
 *   args[2] optional test-case file (one case per line). Passing the SAME file the
 *           desktop --selftest uses keeps both engines honest about identical input.
 */
public class RulesCheck {

    public static void main(String[] args) throws Exception {
        String rulesText = new String(Files.readAllBytes(Paths.get(args[0])), "UTF-8");
        com.miaohua.helper.Rules r = com.miaohua.helper.Rules.parse(rulesText);

        List<String> cases = new ArrayList<String>();
        if (args.length > 2) {
            String raw = new String(Files.readAllBytes(Paths.get(args[2])), "UTF-8");
            for (String line : raw.split("\r\n|\n|\r")) cases.add(line);
        } else {
            cases.add("\u4F60\u597D");
            cases.add("\u4ECA\u5929\u5929\u6C14\u4E0D\u9519\u3002\u6211\u4EEC\u53BB\u5403\u996D\u5427\uFF01");
            cases.add("\u54C8\u54C8\uFF0C\u597D\u7684\u3002");
            cases.add("\u5DF2\u7ECF\u5E26\u55B5~");
            cases.add("");
        }

        StringBuilder sb = new StringBuilder();
        sb.append("rules=").append(r.rules.size())
          .append(" kaomoji=").append(r.kaomoji.size())
          .append(" each=").append(r.eachSentence)
          .append(" suffix=[").append(r.suffix).append("]\r\n");
        sb.append("--------------------------------------------------\r\n");

        // 第 4 个参数写 trace 时，额外输出【每一个 pass 之后的字符串】。
        // 对比最终输出没有意义（语气风格每次随机挑装饰），只能逐步比 ——
        // 第一个分歧的那一步才是答案。
        boolean traceMode = args.length > 3 && args[3].equals("trace");
        StringBuilder trace = new StringBuilder();

        int bad = 0;
        for (int i = 0; i < cases.size(); i++) {
            String c = cases.get(i);
            com.miaohua.helper.Rules.TRAILING = traceMode;
            com.miaohua.helper.Rules.TRAIL.clear();
            String once = r.transform(c);
            com.miaohua.helper.Rules.TRAILING = false;
            if (traceMode) {
                trace.append("=== case ").append(i + 1).append(" ===\r\n");
                for (String line : com.miaohua.helper.Rules.TRAIL)
                    trace.append("  ").append(line).append("\r\n");
                trace.append("  RESULT | ").append(once).append("\r\n\r\n");
            }
            String twice = r.transform(once);
            sb.append("IN   : ").append(c).append("\r\n");
            sb.append("OUT  : ").append(once).append("\r\n");
            if (once.equals(twice)) {
                sb.append("AGAIN: (identical - idempotent)\r\n");
            } else {
                sb.append("AGAIN: ").append(twice).append("   <<<<< NOT IDEMPOTENT\r\n");
                bad++;
            }
        }
        sb.append("--------------------------------------------------\r\n");
        sb.append(bad == 0 ? "RESULT: ALL IDEMPOTENT\r\n" : ("RESULT: " + bad + " CASE(S) NOT IDEMPOTENT\r\n"));

        if (traceMode) {
            Files.write(Paths.get(args[1] + ".trace"), trace.toString().getBytes("UTF-8"));
            System.out.println("wrote " + args[1] + ".trace");
        }

        // ---- style rewrite: it must touch ONLY the decoration lines -------------
        sb.append("--------------------------------------------------\r\n");
        sb.append("style rewrite\r\n");
        String[] kao = { "(\u2267\u25BD\u2266)", "(\u0E51\u2022\u0300\u0E3F\u2022\u0301)\u0E2F\u0E3F" };
        String styled = com.miaohua.helper.Rules.applyStyleText(
                rulesText, "{\u540E\u7F00}{\u989C\u6587\u5B57}", "\u55B5\uFF01", kao);
        com.miaohua.helper.Rules r2 = com.miaohua.helper.Rules.parse(styled);

        sb.append("  suffix = \u55B5\uFF01        : ").append(styled.contains("\u540E\u7F00 = \u55B5\uFF01")).append("\r\n");
        sb.append("  new kaomoji present : ").append(styled.contains(kao[0]) && styled.contains(kao[1])).append("\r\n");
        sb.append("  word rules kept     : ").append(r2.rules.size()).append("  (was ").append(r.rules.size()).append(")\r\n");
        sb.append("  kaomoji count       : ").append(r2.kaomoji.size()).append("  (was ").append(r.kaomoji.size()).append(")\r\n");
        sb.append("  comment lines kept  : ").append(countHash(styled)).append("  (was ").append(countHash(rulesText)).append(")\r\n");
        sb.append("  transform(\u4F60\u597D)     : ").append(r2.transform("\u4F60\u597D")).append("\r\n");

        // ---- 语气 wrapper + 重度: same expectations as the desktop selftest -----
        sb.append("--------------------------------------------------\r\n");
        sb.append("tone wrapper + intensity\r\n");

        com.miaohua.helper.Rules tone = com.miaohua.helper.Rules.parse(
            "\u540E\u7F00 = \u55B5\uFF01\r\n"
            + "\u989C\u6587\u5B57 = (=^\u2025^=)\r\n"
            + "\u5F00\u5934 = \u54FC\uFF0C\r\n"
            + "\u7ED3\u5C3E = \uFF0C\u624D\u4E0D\u662F\u7279\u610F\u544A\u8BC9\u4F60\u7684\u5462\r\n"
            + "\u7ED3\u5C3E = \uFF0C\u522B\u3001\u522B\u8BEF\u4F1A\u554A\uFF01\r\n"
            + "\u95EE\u53E5\u7ED3\u5C3E = \uFF0C\u4F60\u3001\u4F60\u95EE\u8FD9\u4E2A\u5E72\u561B\u2026\u2026\r\n"
            + "\u5F00\u5FC3\u7ED3\u5C3E = \uFF0C\u54FC\u3001\u54FC\uFF01\u624D\u6CA1\u6709\u5F88\u9AD8\u5174\u5462\uFF01\r\n"
            + "\u96BE\u8FC7\u7ED3\u5C3E = \uFF0C\u522B\u3001\u522B\u96BE\u8FC7\u5566\u2026\u2026\u6211\u52C9\u5F3A\u966A\u966A\u4F60\r\n"
            + "\u751F\u6C14\u7ED3\u5C3E = \uFF0C\u54FC\uFF01\u6211\u624D\u4E0D\u8DDF\u90A3\u79CD\u4EBA\u4E00\u822C\u89C1\u8BC6\r\n"
            // a rule that appears INSIDE a closing line, on purpose: this is what exposed
            // the peel-order bug (没有 => 才没有呢 mangled the wrapper before it was peeled)
            + "\u6CA1\u6709 => \u624D\u6CA1\u6709\u5462\r\n"
            + "\u6253\u62DB\u547C\u7ED3\u5C3E = \uFF0C\u54FC\uFF0C\u662F\u4F60\u554A\u2026\u2026\u6709\u4E8B\u5417\r\n"
            + "\u5206\u522B\u7ED3\u5C3E = \uFF0C\u54FC\uFF0C\u8D70\u5427\u8D70\u5427\u2026\u2026\u8DEF\u4E0A\u5C0F\u5FC3\r\n"
            + "\u559C\u7231\u7ED3\u5C3E = \uFF0C\u7A81\u3001\u7A81\u7136\u8BF4\u8FD9\u4E2A\u5E72\u561B\u2026\u2026\u7B28\u6B7B\u4E86\r\n"
            + "\u5938\u5956\u7ED3\u5C3E = \uFF0C\u54FC\uFF0C\u8FD9\u3001\u8FD9\u7B97\u4EC0\u4E48\u2026\u2026\u6211\u672C\u6765\u5C31\u5F88\u5389\u5BB3\r\n"
            // a word rule whose LEFT SIDE is also a pool alias. This is the trap that ate
            // "再见 => 拜拜": it must stay a replacement rule, not become a pool entry.
            + "\u518D\u89C1 => \u62DC\u62DC\r\n"
            + "\u611F\u8C22\u7ED3\u5C3E = \uFF0C\u54FC\uFF0C\u8FD9\u6709\u4EC0\u4E48\u597D\u8C22\u7684\u2026\u2026\u4E0D\u8FC7\uFF0C\u4E0D\u5BA2\u6C14\r\n"
            + "\u9053\u6B49\u7ED3\u5C3E = \uFF0C\u54FC\uFF0C\u77E5\u3001\u77E5\u9053\u9519\u4E86\u5C31\u597D\r\n"
            + "\u795D\u8D3A\u7ED3\u5C3E = \uFF0C\u54FC\uFF0C\u606D\u3001\u606D\u559C\u2026\u2026\u624D\u4E0D\u662F\u7279\u610F\u8BF4\u7684\u5462\r\n"
            + "\u9080\u8BF7\u7ED3\u5C3E = \uFF0C\u54FC\uFF0C\u65E2\u3001\u65E2\u7136\u4F60\u90FD\u8BF4\u4E86\u2026\u2026\u90A3\u5C31\u53BB\u5427\r\n"
            + "\u60CA\u8BB6\u7ED3\u5C3E = \uFF0C\u54FC\uFF0C\u6709\u3001\u6709\u4EC0\u4E48\u597D\u60CA\u8BB6\u7684\u2026\u2026\r\n"
            + "\u5BB3\u6015\u7ED3\u5C3E = \uFF0C\u54FC\uFF0C\u6015\u3001\u6015\u4EC0\u4E48\u2026\u2026\u6709\u6211\u5728\u5462\r\n"
            + "\u75B2\u60EB\u7ED3\u5C3E = \uFF0C\u54FC\uFF0C\u8C01\u3001\u8C01\u8BA9\u4F60\u4E0D\u65E9\u70B9\u7761\u7684\u2026\u2026\u5FEB\u53BB\u4F11\u606F\r\n"
            + "\u65E0\u804A\u7ED3\u5C3E = \uFF0C\u54FC\uFF0C\u65E0\u3001\u65E0\u804A\u5C31\u6765\u627E\u6211\u554A\u2026\u2026\r\n"
            + "\u5931\u671B\u7ED3\u5C3E = \uFF0C\u54FC\uFF0C\u8FD9\u3001\u8FD9\u7B97\u4EC0\u4E48\u2026\u2026\u4E0B\u6B21\u518D\u6765\u5C31\u597D\r\n"
            // 安慰 is the USER comforting the other. It holds the lines that used to sit
            // under 难过 by mistake - typing 不开心 produced "过来，给你靠一下".
            + "\u5B89\u6170\u7ED3\u5C3E = \uFF0C\u522B\u3001\u522B\u96BE\u8FC7\u5566\u2026\u2026\u6211\u3001\u6211\u52C9\u5F3A\u966A\u966A\u4F60\r\n"
            + "\u5B89\u6170\u7ED3\u5C3E = \uFF0C\u8FC7\u6765\uFF0C\u7ED9\u4F60\u9760\u4E00\u4E0B\u2026\u2026\u5C31\u4E00\u4F1A\u513F\u54E6\r\n"
            // 撒娇 is the USER asking for affection, not declaring it. The 喜爱 lines used to
            // be written as replies to being confessed to ("你先别说这种话"), which read
            // backwards once the user is the one who opened with 我喜欢你.
            + "\u6492\u5A07\u7ED3\u5C3E = \uFF0C\u2026\u2026\u54FC\uFF0C\u5C31\u3001\u5C31\u4E00\u4E0B\u54E6\r\n"
            + "\u559C\u7231\u7ED3\u5C3E = \uFF0C\u2026\u2026\u8BF4\u3001\u8BF4\u51FA\u6765\u4E86\u3002\u522B\u7B11\u6211\r\n");
        String[] tIn = {
            "\u4F60\u597D",
            "\u4F60\u5403\u996D\u4E86\u5417\uFF1F",
            "\u4F60\u89C9\u5F97\u5462",
            "\u4ECA\u5929\u597D\u5F00\u5FC3\u54C8\u54C8",
            "\u6211\u597D\u96BE\u8FC7\u554A",
            "\u70E6\u6B7B\u4E86\u771F\u8BA8\u538C",
            // the four social moods, plus the overlaps that decide the order
            "\u4F60\u597D\u68D2",
            "\u6211\u559C\u6B22\u4F60",
            "\u518D\u89C1\u5566",
            "\u665A\u5B89",
            "\u5728\u5417",
            // second round: 9 more categories
            "\u8C22\u8C22\u4F60",
            "\u5BF9\u4E0D\u8D77",
            "\u606D\u559C\u4F60",
            "\u8981\u4E0D\u8981\u4E00\u8D77\u5403\u996D",
            "\u54C7\u771F\u7684\u5047\u7684",
            "\u6211\u597D\u6015",
            "\u597D\u7D2F\u554A",
            "\u597D\u65E0\u804A\u554A",
            "\u597D\u5931\u671B",
            "\u522B\u96BE\u8FC7\u4E86",
            "\u62B1\u62B1\u4F60",
            "\u6211\u5728\u5462\u522B\u6015",
            "\u4F60\u80FD\u4E0D\u80FD\u62B1\u62B1\u6211"
        };
        for (int i = 0; i < tIn.length; i++) {
            String t1 = tone.transform(tIn[i]);
            String t2 = tone.transform(t1);
            sb.append("  IN   : ").append(tIn[i]).append("\r\n");
            sb.append("  OUT  : ").append(t1).append("\r\n");
            sb.append(t1.equals(t2) ? "  AGAIN: (identical - idempotent)\r\n"
                                    : "  AGAIN: " + t2 + "  <<<<< NOT IDEMPOTENT\r\n");
        }

        com.miaohua.helper.Rules heavy = com.miaohua.helper.Rules.parse(
            "\u540E\u7F00 = \u55B5~\r\n"
            + "\u989C\u6587\u5B57 = (=^\u2025^=)\r\n"
            + "\u7A0B\u5EA6 = \u91CD\u5EA6\r\n");
        String hc = "\u4ECA\u5929\u4E0D\u9519\uFF0C\u6211\u4EEC\u53BB\u5403\u996D\u5427\u3002";
        String h1 = heavy.transform(hc);
        String h2 = heavy.transform(h1);
        sb.append("  IN   : ").append(hc).append("\r\n");
        sb.append("  OUT  : ").append(h1).append("\r\n");
        sb.append(h1.equals(h2) ? "  AGAIN: (identical - idempotent)\r\n"
                                : "  AGAIN: " + h2 + "  <<<<< NOT IDEMPOTENT\r\n");

        // ---- 只加末尾: 语气 = false keeps the word rules and the end decoration but must
        // add NO wrapper. Same rule file as `tone` above except for that one line, so a
        // missing guard shows up immediately.
        sb.append("--------------------------------------------------\r\n");
        sb.append("\u53EA\u52A0\u672B\u5C3E\uFF08\u8BED\u6C14 = false\uFF09\r\n");
        com.miaohua.helper.Rules dry = com.miaohua.helper.Rules.parse(
            "\u540E\u7F00 = \u55B5\uFF01\r\n"
            + "\u989C\u6587\u5B57 = (=^\u2025^=)\r\n"
            + "\u8BED\u6C14 = false\r\n"
            + "\u5F00\u5934 = \u54FC\uFF0C\r\n"
            + "\u7ED3\u5C3E = \uFF0C\u624D\u4E0D\u662F\u7279\u610F\u544A\u8BC9\u4F60\u7684\u5462\r\n"
            + "\u60CA\u8BB6\u7ED3\u5C3E = \uFF0C\u54FC\uFF0C\u6709\u3001\u6709\u4EC0\u4E48\u597D\u60CA\u8BB6\u7684\u2026\u2026\r\n"
            + "\u518D\u89C1 => \u62DC\u62DC\r\n");
        String[] dIn = { "\u4F60\u597D", "\u518D\u89C1\u5566", "\u597D\u65E0\u804A\u554A" };
        for (int i = 0; i < dIn.length; i++) {
            String d1 = dry.transform(dIn[i]);
            String d2 = dry.transform(d1);
            sb.append("  IN   : ").append(dIn[i]).append("\r\n");
            sb.append("  OUT  : ").append(d1).append("\r\n");
            sb.append(d1.equals(d2) ? "  AGAIN: (identical - idempotent)\r\n"
                                    : "  AGAIN: " + d2 + "  <<<<< NOT IDEMPOTENT\r\n");
        }

        // ---- 风格切换: applyStyleFromAsset must leave alone any key the style does not
        // declare. dry.txt declares only 语气 / 每句, and an earlier version removed every
        // key a style omitted - so tapping 只加末尾 deleted all 22 pools at once.
        sb.append("--------------------------------------------------\r\n");
        sb.append("\u98CE\u683C\u5207\u6362 (applyStyleFromAsset)\r\n");
        String sample = "\u540E\u7F00 = \u55B5\uFF01\r\n"
                      + "\u8BED\u6C14 = true\r\n"
                      + "\u5F00\u5934 = \u54FC\uFF0C\r\n"
                      + "\u7ED3\u5C3E = \uFF0C\u624D\u4E0D\u662F\u5462\r\n"
                      + "\u597D\u7684 => \u597D\u54D2\r\n"
                      + "# \u6CE8\u91CA\r\n";
        String dryAsset = "\u8BED\u6C14 = false\r\n\u6BCF\u53E5 = false\r\n";
        String r1 = com.miaohua.helper.Rules.applyStyleFromAsset(sample, dryAsset);
        sb.append("  \u53EA\u52A0\u672B\u5C3E: \u8BCD\u5E93\u4FDD\u7559=" + (r1.contains("\u5F00\u5934 = \u54FC\uFF0C")
                && r1.contains("\u7ED3\u5C3E = \uFF0C\u624D\u4E0D\u662F\u5462"))
                + "  \u8BCD\u89C4\u5219\u4FDD\u7559=" + (r1.contains("\u597D\u7684 => \u597D\u54D2")
                && r1.contains("# \u6CE8\u91CA"))
                + "  \u8BED\u6C14\u5DF2\u5173=" + r1.contains("\u8BED\u6C14 = false") + "\r\n");

        String toneAsset = "\u540E\u7F00 = \u5636\u2014\u2014\r\n\u8BED\u6C14 = true\r\n"
                         + "\u5F00\u5934 = \uFF08\u70B8\u6BDB\uFF09\r\n\u7ED3\u5C3E = \uFF0C\u522B\u8FC7\u6765\r\n";
        String styleOut = com.miaohua.helper.Rules.applyStyleFromAsset(sample, toneAsset);
        sb.append("  \u5B8C\u6574\u98CE\u683C: \u540E\u7F00\u5DF2\u6362=" + styleOut.contains("\u540E\u7F00 = \u5636\u2014\u2014")
                + "  \u5F00\u5934\u5DF2\u6362=" + !styleOut.contains("\u5F00\u5934 = \u54FC\uFF0C")
                + "  \u8BCD\u89C4\u5219\u4ECD\u5728=" + styleOut.contains("\u597D\u7684 => \u597D\u54D2") + "\r\n");

        com.miaohua.helper.Rules reparsed = com.miaohua.helper.Rules.parse(styleOut);
        sb.append("  \u91CD\u65B0\u89E3\u6790: suffix=[" + reparsed.suffix + "] openers="
                + reparsed.openers.size() + " tone=" + reparsed.tone + "\r\n");

        // ---- 设置页用到的单键 / 多键读写。These must edit exactly one key and leave every
        // other line - word rules, comments, the pools - untouched, because the page writes
        // straight into the rules file the user can then read on the 规则 tab.
        sb.append("--------------------------------------------------\r\n");
        sb.append("\u8BBE\u7F6E\u9875\u8BFB\u5199 (getKey / setKey / getKeys / setKeys)\r\n");
        String s0 = "\u540E\u7F00 = \u55B5\uFF01\r\n"
                  + "\u5305\u540D = com.tencent.mobileqq\r\n"
                  + "\u5305\u540D = com.tencent.mm\r\n"
                  + "\u81EA\u52A8\u6A21\u5F0F = true\r\n"
                  + "\u597D\u7684 => \u597D\u54D2\r\n"
                  + "# \u6CE8\u91CA\r\n";
        sb.append("  \u8BFB\u5355\u952E: automode=" + com.miaohua.helper.Rules.getKey(s0, "\u81EA\u52A8\u6A21\u5F0F", "automode")
                + "  suffix=" + com.miaohua.helper.Rules.getKey(s0, "\u540E\u7F00", "suffix") + "\r\n");
        sb.append("  \u8BFB\u591A\u952E: \u5305\u540D\u5171 "
                + com.miaohua.helper.Rules.getKeys(s0, "\u5305\u540D", "package").size() + " \u4E2A\r\n");

        String s1 = com.miaohua.helper.Rules.setKey(s0, "\u81EA\u52A8\u6A21\u5F0F", "automode", "false");
        sb.append("  \u6539\u5355\u952E: \u5DF2\u6539=" + s1.contains("\u81EA\u52A8\u6A21\u5F0F = false")
                + "  \u8BCD\u89C4\u5219\u4ECD\u5728=" + s1.contains("\u597D\u7684 => \u597D\u54D2")
                + "  \u6CE8\u91CA\u4ECD\u5728=" + s1.contains("# \u6CE8\u91CA")
                + "  \u989C\u6587\u5B57\u4ECD\u5728=" + s1.contains("\u540E\u7F00 = \u55B5\uFF01") + "\r\n");

        java.util.List<String> one = new java.util.ArrayList<String>();
        one.add("com.tencent.tim");
        String s2 = com.miaohua.helper.Rules.setKeys(s0, "\u5305\u540D", "package", one);
        sb.append("  \u6539\u591A\u952E: \u65E7\u5305\u540D\u5DF2\u6E05=" + !s2.contains("com.tencent.mobileqq")
                + "  \u65B0\u5305\u540D\u5DF2\u5199=" + s2.contains("\u5305\u540D = com.tencent.tim")
                + "  \u53EA\u5269\u4E00\u884C=" + (com.miaohua.helper.Rules.getKeys(s2, "\u5305\u540D", "package").size() == 1)
                + "  \u8BCD\u89C4\u5219\u4ECD\u5728=" + s2.contains("\u597D\u7684 => \u597D\u54D2") + "\r\n");

        String s3 = com.miaohua.helper.Rules.setKey(s0, "\u81EA\u52A8\u6A21\u5F0F", "automode", null);
        sb.append("  \u5220\u5355\u952E: \u5DF2\u5220=" + (com.miaohua.helper.Rules.getKey(s3, "\u81EA\u52A8\u6A21\u5F0F", "automode") == null)
                + "  \u989C\u6587\u5B57\u4ECD\u5728=" + s3.contains("\u540E\u7F00 = \u55B5\uFF01") + "\r\n");

        com.miaohua.helper.Rules s3p = com.miaohua.helper.Rules.parse(s3);
        sb.append("  \u91CD\u65B0\u89E3\u6790: suffix=[" + s3p.suffix + "] pkgs=" + s3p.packages.size() + "\r\n");

        // ---- 边打边转换. The exact loop a user hit: transform, keep typing, transform again.
        // Whatever was added last time must be stripped before the new text is decorated, or
        // every pass stacks another 喵~ on top and the earlier output is treated as the user's
        // own words. Each step below appends a digit, which is what the user did.
        sb.append("--------------------------------------------------\r\n");
        sb.append("\u8FB9\u6253\u8FB9\u8F6C\u6362 (\u6253\u5B57 -> \u8F6C\u6362 -> \u63A5\u7740\u6253\u5B57 -> \u518D\u8F6C\u6362)\r\n");
        String loopRules = "\u540E\u7F00 = \u55B5~\r\n"
                         + "\u989C\u6587\u5B57 = (\u2267^\u03C9^\u2266)\r\n"
                         + "\u6BCF\u53E5 = true\r\n";
        com.miaohua.helper.Rules lp = com.miaohua.helper.Rules.parse(loopRules);
        String cur = "\u4F60\u597D";
        sb.append("  start : " + cur + "\r\n");
        java.util.List<String> toks = lp.decorationTokens();
        for (int step = 0; step < 4; step++) {
            String after = lp.transform(cur);
            sb.append("  pass  : " + after + "\r\n");
            // The check that matters: strip the decoration back off and what is left must be
            // exactly what the user has typed. Comparing the raw strings would be wrong -
            // moving the decoration from the middle to the end is the whole point.
            String plain = after;
            for (int k = 0; k < toks.size(); k++) plain = plain.replace(toks.get(k), "");
            String typed = cur;
            for (int k = 0; k < toks.size(); k++) typed = typed.replace(toks.get(k), "");
            if (!plain.equals(typed))
                sb.append("        <<<<< \u6B63\u6587\u88AB\u6539\u4E86: [" + typed + "] -> [" + plain + "]\r\n");
            cur = after + String.valueOf(step + 1);   // the user keeps typing
            sb.append("  type  : " + cur + "\r\n");
        }
        String finalPass = lp.transform(cur);
        sb.append("  final : " + finalPass + "\r\n");
        sb.append("  \u91CD\u590D\u88C5\u9970\u6B21\u6570: " + countOccurrences(finalPass, "\u55B5~") + "\r\n");

        // ---- 手动删掉装饰的一部分. Reported from the phone with exactly these strings. The
        // PC engine handles all four; if the Android one does not, the two have drifted.
        sb.append("--------------------------------------------------\r\n");
        sb.append("\u5220\u6389\u4E00\u90E8\u5206\u88C5\u9970\u4E4B\u540E\u518D\u8F6C\u6362\r\n");
        com.miaohua.helper.Rules pcfg = com.miaohua.helper.Rules.parse(
            "\u540E\u7F00 = \u55B5~\r\n"
            + "\u6A21\u677F = {\u540E\u7F00}{\u989C\u6587\u5B57}\r\n"
            + "\u6BCF\u53E5 = true\r\n"
            // the real 萌系 pool, all six, not a trimmed two - a bigger token list is what
            // could make a partial kaomoji match something by accident
            + "\u989C\u6587\u5B57 = (=^\uFF65\u03C9\uFF65^=)\r\n"
            + "\u989C\u6587\u5B57 = (=^\u2025^=)\r\n"
            + "\u989C\u6587\u5B57 = (\u00B4\uFF65\u03C9\uFF65`)\r\n"
            + "\u989C\u6587\u5B57 = \u0E3F^\u2022\u064C\u2022^\u0E3F\r\n"
            + "\u989C\u6587\u5B57 = (\u0E51\u2022\u0300\u3142\u2022\u0301)\u0648\u2727\r\n"
            + "\u989C\u6587\u5B57 = (\u2267\u25BD\u2266)\r\n");
        String[] pIn = {
            "\u4F60\u597D\u55B5~(\u00B4\uFF65\u03C9\uFF65`)",
            "\u4F60\u597D\u55B5~(\uFF65\u03C9\uFF65`)",
            "\u4F60\u597D\u55B5~",
            "\u4F60\u597D\u55B5~(\u00B4\uFF65\u03C9",
            // what the user actually ended up with
            "\u4F60\u597D\u55B5~(\uFF65\u03C9\uFF65`)\u55B5~\u0E3F^\u2022\u064C\u2022^\u0E3F",
            // verbatim from the user's log: a decoration with one character deleted, then more
            // typing. The old one used to survive and a fresh one was appended after it.
            "\u4F60\u597D\u55B5~\u0E3F^\u2022\u064C\u2022\u0E3F\u4F60\u597D",
            "\u4F60\u597D\u55B5~(=^\uFF65\u03C9\uFF65^)\u4F60\u597D",
            "\u4F60\u597D\u55B5~(=\u2025=\u4F60\u597D"
        };
        for (int i = 0; i < pIn.length; i++) {
            String p1 = pcfg.transform(pIn[i]);
            String p2 = pcfg.transform(p1);
            sb.append("  IN   : ").append(pIn[i]).append("\r\n");
            sb.append("  OUT  : ").append(p1).append("\r\n");
            sb.append(p1.equals(p2) ? "  AGAIN: (identical - idempotent)\r\n"
                                    : "  AGAIN: " + p2 + "   <<<<< NOT IDEMPOTENT\r\n");
        }

        // 轻度 (每句 = false) takes a different path through the decorator - the whole text is
        // one unit instead of per sentence - so it needs its own run.
        sb.append("  -- \u8F7B\u5EA6\uFF08\u6BCF\u53E5 = false\uFF09--\r\n");
        com.miaohua.helper.Rules lightCfg = com.miaohua.helper.Rules.parse(
            "\u540E\u7F00 = \u55B5~\r\n"
            + "\u6A21\u677F = {\u540E\u7F00}{\u989C\u6587\u5B57}\r\n"
            + "\u6BCF\u53E5 = false\r\n"
            + "\u989C\u6587\u5B57 = (\u00B4\uFF65\u03C9\uFF65`)\r\n"
            + "\u989C\u6587\u5B57 = \u0E3F^\u2022\u064C\u2022^\u0E3F\r\n");
        for (int i = 0; i < pIn.length; i++) {
            String p1 = lightCfg.transform(pIn[i]);
            String p2 = lightCfg.transform(p1);
            sb.append("  IN   : ").append(pIn[i]).append("\r\n");
            sb.append("  OUT  : ").append(p1).append("\r\n");
            sb.append(p1.equals(p2) ? "  AGAIN: (identical - idempotent)\r\n"
                                    : "  AGAIN: " + p2 + "   <<<<< NOT IDEMPOTENT\r\n");
        }

        // And the user's own follow-up: type, transform, delete a character, transform again -
        // the delete is itself a text change, so the service runs on it.
        sb.append("  -- \u5220\u4E00\u4E0B -> \u518D\u8F6C\u6362 --\r\n");
        String typed = "\u4F60\u597D";
        String t1 = pcfg.transform(typed);                       // 你好喵~(kao)
        String trimmed = t1.replace("(\u00B4\uFF65\u03C9\uFF65`)", "(\uFF65\u03C9\uFF65`)");   // user deletes ´
        String t2 = pcfg.transform(trimmed);
        String t3 = pcfg.transform(t2);
        sb.append("  1 type    : ").append(typed).append("\r\n");
        sb.append("  2 transform: ").append(t1).append("\r\n");
        sb.append("  3 delete   : ").append(trimmed).append("\r\n");
        sb.append("  4 transform: ").append(t2).append("\r\n");
        sb.append("     stable   : ").append(t2.equals(t3)).append("\r\n");

        Files.write(Paths.get(args[1]), sb.toString().getBytes("UTF-8"));
    }

    static int countOccurrences(String hay, String needle) {
        int n = 0, i = 0;
        while ((i = hay.indexOf(needle, i)) >= 0) { n++; i += needle.length(); }
        return n;
    }

    static int countHash(String s) {
        int n = 0;
        for (String line : s.split("\r\n|\n|\r"))
            if (line.trim().startsWith("#")) n++;
        return n;
    }
}
