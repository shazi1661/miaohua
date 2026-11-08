package com.miaohua.helper;

import java.io.BufferedReader;
import java.io.FileInputStream;
import java.io.InputStreamReader;
import java.io.PrintWriter;
import java.nio.charset.StandardCharsets;

/**
 * 对拍：用 Java 端小分类器跑一批用例 ✧ 输出和 Python 端逐条对比 ✓
 *
 * 用法：
 *   java -cp <classpath> com.miaohua.helper.ClfCheck <clf.bin> <cases.txt> <out.txt>
 *
 * cases.txt 每行： 上下文 <TAB> 回复      （上下文里的换行写成 ⏎）
 * out.txt   每行： 预测标签 <TAB> 上下文 <TAB> 回复
 */
public class ClfCheck {

    public static void main(String[] args) throws Exception {
        if (args.length < 3) {
            System.out.println("用法: ClfCheck <clf.bin> <cases.txt> <out.txt>");
            return;
        }
        String bin = args[0], cases = args[1], out = args[2];

        FileInputStream fi = new FileInputStream(bin);
        boolean ok = TinyClf.load(fi);
        System.out.println("权重加载: " + (ok ? "✓" : "✗"));
        if (!ok) return;

        BufferedReader r = new BufferedReader(
                new InputStreamReader(new FileInputStream(cases), StandardCharsets.UTF_8));
        PrintWriter w = new PrintWriter(out, "UTF-8");
        String line;
        int n = 0, bad = 0;
        while ((line = r.readLine()) != null) {
            if (line.trim().length() == 0) continue;
            String[] p = line.split("\t", -1);
            String ctx = p.length > 0 ? p[0].replace("\u23ce", "\n") : "";
            String rep = p.length > 1 ? p[1] : "";
            String got = TinyClf.classify(ctx, rep);
            if (got == null) got = "(null)";
            w.println(got + "\t" + p[0] + "\t" + rep);
            n++;
        }
        r.close();
        w.close();
        System.out.println("跑了 " + n + " 条 -> " + out);
    }
}
