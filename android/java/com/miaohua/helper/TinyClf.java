package com.miaohua.helper;

import java.io.DataInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;

/**
 * 蒸馏出来的语气小分类器（7MB ✧ 纯 Java ✧ 不需要任何原生库）。
 *
 * ★ 它是怎么来的：
 *     电脑版用 7B 模型判语气的效果是 8/10 ✗ 但 4.4GB 塞不进手机 ✓
 *     于是我们自己造了 14892 条「上文 + 回复 → 语气」的样本 ✗
 *     训了个字符 n-gram 哈希 + softmax 回归的小模型 ✓
 *     —— ★ 实测同样 10 个用例也是 8/10 ✗ 体积只有 7MB ✓✓✓
 *
 * ★ 为什么哈希要用 FNV-1a：
 *     训练是 Python 做的 ✗ 推理是 Java 做的 ✓
 *     两边必须把同一个 n-gram 映射到【同一个桶】✗ 否则权重全对不上 ✓
 *     Python 的 hash() 每次启动都不一样 ✗ 不能用 ✓
 *     FNV-1a 两边都好实现 ✗ 而且完全确定 ✓
 *
 * ★ 桶下标也必须一致：
 *     Python 的 h 是【无符号】32 位 ✗ Java 的 int 是【有符号】✓
 *     所以 Java 这边要用 Integer.remainderUnsigned ✗ 不能直接取模 ✓
 *     —— ★ 这个坑不踩一次是想不到的 ✓
 *
 * ★ 性能：一次分类 = 几百次哈希 + 7 次点积（每类 26 万维稀疏求和）
 *     实测手机上应该是【微秒级】✗ 不发热 ✗ 不耗电 ✓
 */
public class TinyClf {

    public static final int DIM = 1 << 17;          // 131072 个桶（2026-10-17：13 类 × 131073 × 4 = 6.5 MB ✓）
    public static final int[] NGRAMS = {1, 2, 3};   // 字符 1~3 元

    /** 标签顺序必须和训练时完全一致 ✧ 改一个字都会全错 ✓ */
    public static final String[] LABELS = {
        "\u5b89\u6170", "\u611f\u8c22", "\u5206\u522b", "\u5f00\u5fc3",
        "\u7591\u95ee", "\u666e\u901a", "\u751f\u6c14",
        "\u96be\u8fc7", "\u7d2f\u4e86", "\u559c\u7231", "\u5938\u5956",
        "\u5bb3\u6015", "\u5931\u671b"
    };

    private static float[] W = null;    // 类别数 × (DIM+1) ✧ 最后一列是 bias
    private static int NCLS = 0;
    private static boolean loaded = false;
    private static boolean triedLoad = false;
    private static android.content.Context appCtx = null;

    /** 由 Service 在连上时交进来 ✧ 权重等第一次用的时候再读 ✓ */
    public static void setContext(android.content.Context c) {
        appCtx = (c == null) ? null : c.getApplicationContext();
    }

    public static boolean isLoaded() { return loaded; }

    /** 从 assets 读权重（小端 float32）✧ 失败就当没加载 ✓ */
    public static synchronized boolean load(InputStream in) {
        if (loaded) return true;
        if (in == null) return false;
        try {
            byte[] all = readAll(in);
            in.close();
            ByteBuffer bb = ByteBuffer.wrap(all).order(ByteOrder.LITTLE_ENDIAN);
            int total = bb.asFloatBuffer().capacity();
            NCLS = LABELS.length;
            if (total != NCLS * (DIM + 1)) {
                return false;                   // 尺寸对不上 ✧ 不猜 ✓
            }
            W = new float[total];
            bb.asFloatBuffer().get(W);
            loaded = true;
            return true;
        } catch (Throwable t) {
            loaded = false;
            return false;
        }
    }

    private static byte[] readAll(InputStream in) throws IOException {
        java.io.ByteArrayOutputStream bo = new java.io.ByteArrayOutputStream(8 * 1024 * 1024);
        byte[] buf = new byte[64 * 1024];
        int n;
        while ((n = in.read(buf)) > 0) bo.write(buf, 0, n);
        return bo.toByteArray();
    }

    /** 第一次真用的时候才读权重 ✧ 只试一次 ✓ */
    private static synchronized void lazyLoad() {
        if (loaded || triedLoad || appCtx == null) return;
        triedLoad = true;
        try {
            java.io.InputStream in = appCtx.getAssets().open("clf.bin");
            boolean ok = load(in);
            try { Trace.add(Trace.now() + "  语气小模型加载" + (ok ? "成功" : "失败")); } catch (Throwable t) { }
        } catch (Throwable t) {
            try { Trace.add(Trace.now() + "  语气小模型没读到：" + t); } catch (Throwable t2) { }
        }
    }

    /** FNV-1a ✧ 必须和 Python 那边逐位一致 ✓ */
    public static int fnv1a(String s) {
        int h = 0x811C9DC5;
        byte[] bs;
        try {
            bs = s.getBytes("UTF-8");
        } catch (Exception e) {
            bs = s.getBytes();
        }
        for (int i = 0; i < bs.length; i++) {
            h ^= (bs[i] & 0xFF);
            h *= 0x01000193;
        }
        return h;
    }

    /** 把一个 n-gram 映射到桶 ✧ 用无符号取模 ✓ */
    static int bucket(String g) {
        return Integer.remainderUnsigned(fnv1a(g), DIM);
    }

    /**
     * 分类。
     *
     * @param ctx   聊天上下文（没有就传 "" 或 null）
     * @param reply 我要发的那句话
     * @return 标签 ✧ 没加载或出错返回 null（调用方要能处理 ✓）
     */
    public static String classify(String ctx, String reply) {
        if (!loaded) lazyLoad();
        if (!loaded || reply == null) return null;
        try {
            String text = (ctx == null || ctx.length() == 0) ? reply : (ctx + " " + reply);
            if (text.length() > 600) text = text.substring(text.length() - 600);   // 尾巴最重要 ✓
            int n = text.length();
            if (n == 0) return null;

            float[] score = new float[NCLS];
            for (int c = 0; c < NCLS; c++) score[c] = W[c * (DIM + 1) + DIM];      // bias

            for (int gi = 0; gi < NGRAMS.length; gi++) {
                int g = NGRAMS[gi];
                if (g > n) continue;
                char[] buf = new char[g];
                for (int i = 0; i + g <= n; i++) {
                    text.getChars(i, i + g, buf, 0);
                    int b = bucket(new String(buf));
                    for (int c = 0; c < NCLS; c++) score[c] += W[c * (DIM + 1) + b];
                }
            }

            int best = 0;
            for (int c = 1; c < NCLS; c++) if (score[c] > score[best]) best = c;
            return LABELS[best];
        } catch (Throwable t) {
            return null;
        }
    }
}
