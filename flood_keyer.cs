using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;

class FloodKeyer
{
    static void Main(string[] args)
    {
        ProcessImage(args[0], args[1]);
    }

    static void ProcessImage(string srcPath, string dstPath)
    {
        var src = new Bitmap(srcPath);
        int w = src.Width, h = src.Height;
        var data = src.LockBits(new Rectangle(0,0,w,h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int stride = data.Stride;
        byte[] buf = new byte[stride * h];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, buf.Length);

        // 标记数组：0 = 未访问, 1 = 背景, 2 = 前景
        byte[] mark = new byte[w * h];
        bool IsLight(int idx)
        {
            byte b = buf[idx], g = buf[idx+1], r = buf[idx+2];
            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));
            return r > 200 && g > 200 && b > 200 && (max - min) < 35;
        }

        // 从四个边缘的所有"亮"像素开始 BFS
        var stack = new Stack<int>();
        void TrySeed(int x, int y)
        {
            int idx = y * stride + x * 4;
            if (mark[y*w+x] == 0 && IsLight(idx))
            {
                mark[y*w+x] = 1;
                stack.Push(y*w+x);
            }
        }
        for (int x = 0; x < w; x++) { TrySeed(x, 0); TrySeed(x, h-1); }
        for (int y = 0; y < h; y++) { TrySeed(0, y); TrySeed(w-1, y); }

        while (stack.Count > 0)
        {
            int p = stack.Pop();
            int x = p % w, y = p / w;
            int[] dx = {-1,1,0,0};
            int[] dy = {0,0,-1,1};
            for (int k = 0; k < 4; k++)
            {
                int nx = x + dx[k], ny = y + dy[k];
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                int nidx = ny * stride + nx * 4;
                int midx = ny * w + nx;
                if (mark[midx] != 0) continue;
                if (IsLight(nidx))
                {
                    mark[midx] = 1;
                    stack.Push(midx);
                }
            }
        }

        // 把标记为背景的像素 alpha 设为 0
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (mark[y*w+x] == 1)
                {
                    int idx = y * stride + x * 4;
                    buf[idx + 3] = 0;
                }
            }
        }

        System.Runtime.InteropServices.Marshal.Copy(buf, 0, data.Scan0, buf.Length);
        src.UnlockBits(data);

        // 找紧边界（仅看 alpha > 16 的像素），跳过底部 4% 水印
        int scanH = (int)(h * 0.96);
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < scanH; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (mark[y*w+x] != 1)  // 前景或未访问的不透明
                {
                    int idx = y * stride + x * 4;
                    if (buf[idx + 3] > 16)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }
        }
        int pad = 4;
        int cx = Math.Max(0, minX - pad);
        int cy = Math.Max(0, minY - pad);
        int cw = Math.Min(w - cx, maxX - minX + 1 + 2*pad);
        int ch = Math.Min(h - cy, maxY - minY + 1 + 2*pad);
        Console.WriteLine($"bbox: {cx},{cy} {cw}x{ch}");

        var crop = src.Clone(new Rectangle(cx, cy, cw, ch), PixelFormat.Format32bppArgb);
        crop.Save(dstPath, ImageFormat.Png);
        crop.Dispose();
        src.Dispose();
    }
}
