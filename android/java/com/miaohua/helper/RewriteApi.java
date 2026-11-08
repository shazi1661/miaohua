package com.miaohua.helper;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;

/**
 * ★ 2026-10-31：用大模型改写语气。
 *
 * 为什么要单独一个类：
 *   · 安卓这边以前【没有任何联网代码】✧ Rules 和 MiaohuaService 都很干净 ✓
 *   · 网络必须跑在后台线程 ✧ 无障碍回调在主线程上 ✧ 同步请求会直接 ANR ✓
 *   · 所以这里只放"纯函数"（发请求、解析、返回字符串）✧ 线程由 Service 管 ✓
 *
 * 接口形态：OpenAI 兼容的 POST {base}/v1/chat/completions
 *   base 可以是 https://api.deepseek.com、http://127.0.0.1:1234、https://api.openai.com 之类
 *   key 可空（本地 llama-server / Ollama 不需要）
 */
public class RewriteApi {

    /** 最近一次出错的文字 ✧ 给设置页和日志看 ✓ */
    public static volatile String lastError = "";

    /** ★ 整句重写：把原文改成那个口吻 ✓ */
    public static String rewrite(Rules r, String plain, String sample) {
        if (r == null || plain == null) return null;
        String system = "你是一个语气改写器。把用户给的句子改写成下面那种口吻："
                      + "保持原意不变、不加新信息、长度和原文接近（最多两倍）、"
                      + "可以加语气词和口头禅。"
                      + "只输出改写后的那一句话，不要解释、不要引号、不要换行。";
        String user = "口吻示例：" + sample + "\n要改写的句子：" + plain;
        String out = chat(r, system, user, 220, 0.8);
        return clean(out, plain);
    }

    static String norm(String p) {
        if (p == null) return "";
        p = p.trim();
        while (p.length() > 0) {
            char c = p.charAt(0);
            if (c == '"' || c == '\u201c' || c == '\u300c' || c == ' ') { p = p.substring(1).trim(); continue; }
            break;
        }
        p = p.replace("\r", "").replace("\n", "").replace("||", "").trim();
        if (p.equals("\u65e0") || p.equals("\u7a7a") || p.equals("-")) return "";
        return p;
    }

    /** ★ 只生成包装：返回 open + "\u0001" + close ✓ */
    static String moodCn(String m) {
        if ("angry".equals(m)) return "生气";
        if ("sad".equals(m)) return "难过";
        if ("happy".equals(m)) return "开心";
        if ("thanks".equals(m)) return "感谢";
        if ("question".equals(m)) return "疑问";
        if ("comfort".equals(m)) return "安慰";
        if ("farewell".equals(m)) return "告别";
        if ("greeting".equals(m)) return "打招呼";
        if ("love".equals(m)) return "喜爱";
        if ("praise".equals(m)) return "夸奖";
        if ("tired".equals(m)) return "累";
        if ("fear".equals(m)) return "害怕";
        if ("disappointed".equals(m)) return "失望";
        return "平常";
    }

    public static String wrap(Rules r, String plain, String rule,
                              String openerSample, String closerSample, String styleName) {
        if (r == null || plain == null) return null;
        // ★ 2026-11-01：开头结尾全由模型写 ✗ 只给规则结果不够 ✓
        String sys = "你在扮演一个语气是「" + (styleName == null ? "" : styleName) + "」的角色。"
                   + "现在要给一句话加上四个东西，每个都要符合这个风格："
                   + "开头（句首短语气词，如「嗯，」）、"
                   + "后缀（接在正文后面一两个字，如「喵~」）、"
                   + "颜文字（一个符号表情，如 (´・ω・`) ）、"
                   + "结尾（句尾一小句，如「你来啦」）。"
                   + "不要重复原句。"
                   + "只输出四段，用 || 分隔："
                   + "【开头】||【后缀】||【颜文字】||【结尾】。"
                   + "某一个没有就留空，不要解释、不要引号、不要换行。";
        String user = "这句话的情绪是「" + moodCn(Rules.lastMood) + "」，包装要跟着这个情绪走。"
                    + "\n风格：" + (styleName == null ? "" : styleName)
                    + "\n开头样例：" + (openerSample == null ? "" : openerSample)
                    + "\n结尾样例：" + (closerSample == null ? "" : closerSample)
                    + "\n要加包装的句子：" + plain;
        String out = chat(r, sys, user, 160, 0.9);
        if (out == null) return null;
        String[] parts = out.split("\\|\\|", -1);
        if (parts.length < 2) return null;
        String open = norm(parts[0]);
        String suf = parts.length > 1 ? norm(parts[1]) : "";
        String kao = parts.length > 2 ? norm(parts[2]) : "";
        String close = parts.length > 3 ? norm(parts[3]) : "";
        if (parts.length == 2) { suf = ""; kao = ""; }
        if (open.length() > 24) open = open.substring(0, 24);
        if (suf.length() > 8) suf = suf.substring(0, 8);
        if (kao.length() > 32) kao = kao.substring(0, 32);
        if (close.length() > 40) close = close.substring(0, 40);
        return open + "\u0001" + suf + "\u0001" + kao + "\u0001" + close;
    }

    static String strip(String s) {
        if (s == null) return "";
        s = s.trim();
        while (s.length() > 0) {
            char c = s.charAt(0);
            if (c == '"' || c == '\u201c' || c == '\u300c' || c == '\'') { s = s.substring(1).trim(); continue; }
            c = s.charAt(s.length() - 1);
            if (c == '"' || c == '\u201d' || c == '\u300d' || c == '\'' || c == '\u3002') { s = s.substring(0, s.length() - 1).trim(); continue; }
            break;
        }
        return s;
    }

    static String clean(String t, String plain) {
        if (t == null) return null;
        t = strip(t).replace("\r", " ").replace("\n", " ").trim();
        while (t.contains("  ")) t = t.replace("  ", " ");
        if (t.length() == 0) return null;
        // ★ 胡说八道（长度离谱）就不要 ✧ 退回规则结果 ✓
        if (t.length() > Math.max(24, plain.length() * 3)) return null;
        return t;
    }

    /** ★ 一次 OpenAI 兼容调用 ✓ */
    public static String chat(Rules r, String system, String user, int maxTokens, double temp) {
        String base = r.rewriteEndpoint;
        if (base == null || base.trim().length() == 0) return null;
        base = base.trim();
        String url = base;
        if (!url.contains("/chat/completions")) {
            if (url.endsWith("/")) url = url.substring(0, url.length() - 1);
            if (url.endsWith("/v1")) url = url + "/chat/completions";
            else url = url + "/v1/chat/completions";
        }
        String body = "{\"messages\":[{\"role\":\"system\",\"content\":\"" + esc(system) + "\"},"
                    + "{\"role\":\"user\",\"content\":\"" + esc(user) + "\"}],"
                    + "\"temperature\":" + temp + ",\"max_tokens\":" + maxTokens + ",\"stream\":false"
                    + (r.rewriteModel != null && r.rewriteModel.length() > 0
                        ? ",\"model\":\"" + esc(r.rewriteModel) + "\"" : "")
                    + "}";
        HttpURLConnection c = null;
        try {
            c = (HttpURLConnection) new URL(url).openConnection();
            c.setRequestMethod("POST");
            c.setConnectTimeout(r.rewriteTimeoutMs);
            c.setReadTimeout(r.rewriteTimeoutMs);
            c.setDoOutput(true);
            c.setRequestProperty("Content-Type", "application/json");
            if (r.rewriteKey != null && r.rewriteKey.length() > 0)
                c.setRequestProperty("Authorization", "Bearer " + r.rewriteKey);
            OutputStream os = c.getOutputStream();
            os.write(body.getBytes("UTF-8"));
            os.flush();
            os.close();
            int code = c.getResponseCode();
            if (code != 200) {
                lastError = "HTTP " + code;
                return null;
            }
            InputStream in = c.getInputStream();
            ByteArrayOutputStream bo = new ByteArrayOutputStream();
            byte[] buf = new byte[4096];
            int k;
            while ((k = in.read(buf)) > 0) bo.write(buf, 0, k);
            in.close();
            String json = new String(bo.toByteArray(), "UTF-8");
            lastError = "";
            return firstContent(json);
        } catch (Throwable t) {
            lastError = String.valueOf(t);
            return null;
        } finally {
            if (c != null) try { c.disconnect(); } catch (Throwable t) { }
        }
    }

    /** ★ 从 OpenAI 回复里抠第一条 content ✧ 故意不引 json 库 ✓ */
    static String firstContent(String json) {
        if (json == null) return null;
        int i = json.indexOf("\"content\"");
        if (i < 0) return null;
        int colon = json.indexOf(':', i);
        if (colon < 0) return null;
        int q = json.indexOf('"', colon + 1);
        if (q < 0) return null;
        StringBuilder sb = new StringBuilder();
        for (int k = q + 1; k < json.length(); k++) {
            char ch = json.charAt(k);
            if (ch == '\\' && k + 1 < json.length()) {
                k++;
                char e = json.charAt(k);
                if (e == 'n') sb.append('\n');
                else if (e == 't') sb.append('\t');
                else if (e == 'r') sb.append('\r');
                else if (e == 'u' && k + 4 < json.length()) {
                    try {
                        sb.append((char) Integer.parseInt(json.substring(k + 1, k + 5), 16));
                        k += 4;
                    } catch (Exception ex) { }
                } else sb.append(e);
                continue;
            }
            if (ch == '"') break;
            sb.append(ch);
        }
        return sb.toString();
    }

    static String esc(String s) {
        if (s == null) return "";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (c == '"') sb.append("\\\"");
            else if (c == '\\') sb.append("\\\\");
            else if (c == '\n') sb.append("\\n");
            else if (c == '\r') sb.append("\\r");
            else if (c == '\t') sb.append("\\t");
            else sb.append(c);
        }
        return sb.toString();
    }
}
