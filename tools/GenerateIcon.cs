using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class IconGenerator {
    static Bitmap RenderIcon(int size) {
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp)) {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float s = size / 256.0f;

            // Base squircle
            float margin = 6.0f * s;
            float w = size - 2.0f * margin;
            RectangleF baseRect = new RectangleF(margin, margin, w, w);
            float baseRadius = 50.0f * s;

            using (GraphicsPath path = CreateRoundedRect(baseRect, baseRadius)) {
                // Background dark titanium/carbon gradient
                using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                    new PointF(margin, margin),
                    new PointF(size - margin, size - margin),
                    Color.FromArgb(255, 22, 28, 42),
                    Color.FromArgb(255, 7, 9, 15))) {
                    g.FillPath(bgBrush, path);
                }

                // Cyber radial aura in background
                using (GraphicsPath glowPath = new GraphicsPath()) {
                    glowPath.AddEllipse(margin + 12f * s, margin + 12f * s, w - 24f * s, w - 24f * s);
                    using (PathGradientBrush pbg = new PathGradientBrush(glowPath)) {
                        pbg.CenterColor = Color.FromArgb(75, 0, 242, 255);
                        pbg.SurroundColors = new Color[] { Color.FromArgb(0, 7, 9, 15) };
                        g.FillPath(pbg, glowPath);
                    }
                }

                // Electric Cyan glowing rim (outer neon rim)
                float rimWidth = Math.Max(1.0f, 3.5f * s);
                using (Pen rimPen = new Pen(Color.FromArgb(240, 0, 242, 255), rimWidth)) {
                    g.DrawPath(rimPen, path);
                }

                // Inner chamfer contour
                if (size >= 32) {
                    RectangleF inRect = new RectangleF(margin + 4f * s, margin + 4f * s, w - 8f * s, w - 8f * s);
                    float inRad = Math.Max(4f, baseRadius - 4f * s);
                    using (GraphicsPath inPath = CreateRoundedRect(inRect, inRad))
                    using (Pen inPen = new Pen(Color.FromArgb(40, 255, 255, 255), Math.Max(0.75f, 1.2f * s))) {
                        g.DrawPath(inPen, inPath);
                    }
                }

                // Side cooling strakes (MSI Gaming hardware motif)
                if (size >= 48) {
                    using (Brush strakeBrush = new SolidBrush(Color.FromArgb(100, 0, 242, 255))) {
                        g.FillPolygon(strakeBrush, new PointF[] {
                            new PointF(26f * s, 112f * s),
                            new PointF(36f * s, 102f * s),
                            new PointF(36f * s, 130f * s),
                            new PointF(26f * s, 140f * s)
                        });
                        g.FillPolygon(strakeBrush, new PointF[] {
                            new PointF(230f * s, 112f * s),
                            new PointF(220f * s, 102f * s),
                            new PointF(220f * s, 130f * s),
                            new PointF(230f * s, 140f * s)
                        });
                    }
                }

                // ================= VECTOR CHEVRON ("V") =================
                PointF[] leftWing = new PointF[] {
                    new PointF(46f * s, 66f * s),
                    new PointF(86f * s, 66f * s),
                    new PointF(128f * s, 160f * s),
                    new PointF(128f * s, 192f * s),
                    new PointF(106f * s, 192f * s),
                    new PointF(46f * s, 76f * s)
                };
                PointF[] rightWing = new PointF[] {
                    new PointF(210f * s, 66f * s),
                    new PointF(170f * s, 66f * s),
                    new PointF(128f * s, 160f * s),
                    new PointF(128f * s, 192f * s),
                    new PointF(150f * s, 192f * s),
                    new PointF(210f * s, 76f * s)
                };

                using (LinearGradientBrush cyanGrad = new LinearGradientBrush(
                    new PointF(128f * s, 60f * s),
                    new PointF(128f * s, 195f * s),
                    Color.FromArgb(255, 0, 245, 255),
                    Color.FromArgb(255, 0, 130, 235))) {
                    g.FillPolygon(cyanGrad, leftWing);
                    g.FillPolygon(cyanGrad, rightWing);
                }

                // Sharp highlights on V leading edges
                using (Pen edgePen = new Pen(Color.FromArgb(240, 210, 255, 255), Math.Max(0.75f, 1.5f * s))) {
                    g.DrawLines(edgePen, new PointF[] {
                        new PointF(46f * s, 76f * s),
                        new PointF(46f * s, 66f * s),
                        new PointF(86f * s, 66f * s),
                        new PointF(128f * s, 160f * s)
                    });
                    g.DrawLines(edgePen, new PointF[] {
                        new PointF(210f * s, 76f * s),
                        new PointF(210f * s, 66f * s),
                        new PointF(170f * s, 66f * s),
                        new PointF(128f * s, 160f * s)
                    });
                }

                // ================= POWER CORE LIGHTNING BOLT =================
                PointF[] bolt = new PointF[] {
                    new PointF(146f * s, 36f * s),
                    new PointF(96f * s,  116f * s),
                    new PointF(128f * s, 116f * s),
                    new PointF(106f * s, 216f * s),
                    new PointF(162f * s, 108f * s),
                    new PointF(132f * s, 108f * s),
                    new PointF(146f * s, 36f * s)
                };

                // Dark obsidian contour for 3D depth separation over the cyan V
                float depthWidth = Math.Max(1.5f, 6.0f * s);
                using (Pen depthPen = new Pen(Color.FromArgb(255, 7, 9, 15), depthWidth)) {
                    depthPen.LineJoin = LineJoin.Miter;
                    g.DrawPolygon(depthPen, bolt);
                }

                // Amber outer energy aura (if size >= 32)
                if (size >= 32) {
                    using (Pen amberGlow = new Pen(Color.FromArgb(140, 255, 185, 0), Math.Max(1.5f, 3.5f * s))) {
                        amberGlow.LineJoin = LineJoin.Miter;
                        g.DrawPolygon(amberGlow, bolt);
                    }
                }

                // Amber bolt gradient fill
                using (LinearGradientBrush amberGrad = new LinearGradientBrush(
                    new PointF(128f * s, 36f * s),
                    new PointF(128f * s, 216f * s),
                    Color.FromArgb(255, 255, 225, 70),
                    Color.FromArgb(255, 255, 120, 0))) {
                    g.FillPolygon(amberGrad, bolt);
                }

                // White plasma center core
                PointF[] hotCore = new PointF[] {
                    new PointF(142f * s, 46f * s),
                    new PointF(106f * s, 112f * s),
                    new PointF(128f * s, 112f * s),
                    new PointF(114f * s, 200f * s),
                    new PointF(152f * s, 110f * s),
                    new PointF(128f * s, 110f * s),
                    new PointF(142f * s, 46f * s)
                };
                using (LinearGradientBrush hotGrad = new LinearGradientBrush(
                    new PointF(128f * s, 46f * s),
                    new PointF(128f * s, 200f * s),
                    Color.FromArgb(255, 255, 255, 255),
                    Color.FromArgb(190, 255, 240, 160))) {
                    g.FillPolygon(hotGrad, hotCore);
                }

                // Top & bottom status notches (for high-res)
                if (size >= 64) {
                    using (Brush notchBrush = new SolidBrush(Color.FromArgb(255, 0, 242, 255))) {
                        g.FillRectangle(notchBrush, 122f * s, 11f * s, 12f * s, 3.5f * s);
                        g.FillRectangle(notchBrush, 122f * s, 241.5f * s, 12f * s, 3.5f * s);
                    }
                }
            }
        }
        return bmp;
    }

    static GraphicsPath CreateRoundedRect(RectangleF rect, float radius) {
        GraphicsPath path = new GraphicsPath();
        float diameter = radius * 2.0f;
        RectangleF arc = new RectangleF(rect.X, rect.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    static byte[] CreateDibImageBytes(Bitmap bmp) {
        int width = bmp.Width;
        int height = bmp.Height;

        // BITMAPINFOHEADER is 40 bytes
        // Pixel data is width * height * 4
        // Mask data is (widthInBytes) * height
        int maskRowBytes = ((width + 31) / 32) * 4;
        int maskSize = maskRowBytes * height;
        int imageBytesSize = 40 + (width * height * 4) + maskSize;

        byte[] data = new byte[imageBytesSize];
        using (MemoryStream ms = new MemoryStream(data))
        using (BinaryWriter bw = new BinaryWriter(ms)) {
            // BITMAPINFOHEADER
            bw.Write((uint)40);          // biSize
            bw.Write((int)width);        // biWidth
            bw.Write((int)(height * 2)); // biHeight (doubled for XOR + AND masks)
            bw.Write((ushort)1);         // biPlanes
            bw.Write((ushort)32);        // biBitCount
            bw.Write((uint)0);           // biCompression (BI_RGB)
            bw.Write((uint)(width * height * 4)); // biSizeImage
            bw.Write((int)0);            // biXPelsPerMeter
            bw.Write((int)0);            // biYPelsPerMeter
            bw.Write((uint)0);           // biClrUsed
            bw.Write((uint)0);           // biClrImportant

            // Bottom-up BGRA pixels
            for (int y = height - 1; y >= 0; y--) {
                for (int x = 0; x < width; x++) {
                    Color c = bmp.GetPixel(x, y);
                    bw.Write(c.B);
                    bw.Write(c.G);
                    bw.Write(c.R);
                    bw.Write(c.A);
                }
            }

            // AND mask (all 0 for 32bpp alpha pixels, or 1 if completely transparent)
            for (int y = height - 1; y >= 0; y--) {
                byte[] row = new byte[maskRowBytes];
                for (int x = 0; x < width; x++) {
                    Color c = bmp.GetPixel(x, y);
                    if (c.A == 0) {
                        row[x / 8] |= (byte)(0x80 >> (x % 8));
                    }
                }
                bw.Write(row);
            }
        }
        return data;
    }

    static byte[] CreatePngImageBytes(Bitmap bmp) {
        using (MemoryStream ms = new MemoryStream()) {
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }

    public static void Main(string[] args) {
        int[] sizes = new int[] { 256, 128, 64, 48, 32, 16 };
        byte[][] imagePayloads = new byte[sizes.Length][];
        Bitmap[] bitmaps = new Bitmap[sizes.Length];

        if (!Directory.Exists("previews")) Directory.CreateDirectory("previews");

        for (int i = 0; i < sizes.Length; i++) {
            int sz = sizes[i];
            bitmaps[i] = RenderIcon(sz);
            bitmaps[i].Save(Path.Combine("previews", "icon_" + sz + ".png"), ImageFormat.Png);

            if (sz == 256) {
                // 256x256 uses PNG compression per Windows icon spec
                imagePayloads[i] = CreatePngImageBytes(bitmaps[i]);
            } else {
                // 128, 64, 48, 32, 16 use DIB for universal compatibility
                imagePayloads[i] = CreateDibImageBytes(bitmaps[i]);
            }
        }

        string icoPath = "app.ico";
        using (FileStream fs = new FileStream(icoPath, FileMode.Create, FileAccess.Write))
        using (BinaryWriter bw = new BinaryWriter(fs)) {
            // Icon Header (6 bytes)
            bw.Write((ushort)0);              // Reserved
            bw.Write((ushort)1);              // Type: 1 = ICO
            bw.Write((ushort)sizes.Length);   // Image count

            // Directory Entries (16 bytes each)
            int offset = 6 + (16 * sizes.Length);
            for (int i = 0; i < sizes.Length; i++) {
                int sz = sizes[i];
                bw.Write((byte)(sz >= 256 ? 0 : sz)); // bWidth (0 = 256)
                bw.Write((byte)(sz >= 256 ? 0 : sz)); // bHeight (0 = 256)
                bw.Write((byte)0);                     // bColorCount (0 if >=8bpp)
                bw.Write((byte)0);                     // bReserved
                bw.Write((ushort)1);                    // wPlanes
                bw.Write((ushort)32);                   // wBitCount
                bw.Write((uint)imagePayloads[i].Length);// dwBytesInRes
                bw.Write((uint)offset);                 // dwImageOffset

                offset += imagePayloads[i].Length;
            }

            // Image Payloads
            for (int i = 0; i < sizes.Length; i++) {
                bw.Write(imagePayloads[i]);
            }
        }

        Console.WriteLine("SUCCESS: Generated " + icoPath + " (" + new FileInfo(icoPath).Length + " bytes)");
        for (int i = 0; i < sizes.Length; i++) {
            bitmaps[i].Dispose();
        }
    }
}
