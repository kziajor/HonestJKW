// GIF generator for JKW Monitor animations
// No external dependencies — raw GIF89a format + LZW encoder

using System.Collections.Generic;
using System.IO;
using System;

const int W = 200, H = 200;
const string Out = "../../Assets/Animations";
Directory.CreateDirectory(Out);

GenIdle();
GenWorking();
GenWaiting();
GenError();
GenSuccess();
Console.WriteLine("Done.");

// ─── Animation generators ─────────────────────────────────────────────────────

void GenIdle()
{
    // Slow-pulsing gray ring — 10 frames × 120 ms ≈ 1.2 s
    byte[][] pal = [[0,0,0],[200,200,200],[140,140,140],[90,90,90]];
    int frames = 10;
    var data = new List<byte[]>();
    for (int f = 0; f < frames; f++)
    {
        double t = f / (double)frames;
        double r = 52 + 14 * Math.Sin(t * Math.PI * 2);
        data.Add(DrawRing(r, r - 14, 1, 2, 3));
    }
    WriteGif(Path.Combine(Out, "idle.gif"), pal, data, 12);
}

void GenWorking()
{
    // Yellow spinner — 12 frames × 70 ms ≈ 0.84 s
    byte[][] pal = [[0,0,0],[251,203,24],[200,160,15],[120,90,5]];
    int frames = 12;
    var data = new List<byte[]>();
    for (int f = 0; f < frames; f++)
    {
        double startAngle = f / (double)frames * Math.PI * 2;
        double sweepAngle = Math.PI * 1.1; // ~200°
        data.Add(DrawArc(startAngle, sweepAngle, 60, 15, 1, 2, 3));
    }
    WriteGif(Path.Combine(Out, "working.gif"), pal, data, 7);
}

void GenWaiting()
{
    // Three blue dots bouncing in sequence — 12 frames × 100 ms
    byte[][] pal = [[0,0,0],[50,150,220],[30,100,160],[15,55,90]];
    int frames = 12;
    var data = new List<byte[]>();
    for (int f = 0; f < frames; f++)
    {
        data.Add(DrawBouncingDots(f, frames, 1, 2, 3));
    }
    WriteGif(Path.Combine(Out, "waiting.gif"), pal, data, 10);
}

void GenError()
{
    // Flashing red X — 8 frames × 150 ms
    byte[][] pal = [[0,0,0],[220,50,50],[160,30,30],[80,10,10]];
    int frames = 8;
    var data = new List<byte[]>();
    for (int f = 0; f < frames; f++)
    {
        // Pulse: bright on even frames, dark on odd
        byte col1 = (f % 2 == 0) ? (byte)1 : (byte)2;
        byte col2 = (f % 2 == 0) ? (byte)2 : (byte)3;
        data.Add(DrawX(col1, col2));
    }
    WriteGif(Path.Combine(Out, "error.gif"), pal, data, 15);
}

void GenSuccess()
{
    // Green checkmark appearing then glowing — 10 frames × 80 ms
    byte[][] pal = [[0,0,0],[50,210,80],[30,150,55],[15,80,30]];
    int frames = 10;
    var data = new List<byte[]>();
    for (int f = 0; f < frames; f++)
    {
        double progress = Math.Min(1.0, (f + 1) / 6.0); // draw in over 6 frames
        double glow = f >= 6 ? Math.Sin((f - 6) / 4.0 * Math.PI) : 0;
        data.Add(DrawCheckmark(progress, glow, 1, 2, 3));
    }
    WriteGif(Path.Combine(Out, "success.gif"), pal, data, 8);
}

// ─── Frame renderers ─────────────────────────────────────────────────────────

byte[] DrawRing(double outerR, double innerR, byte bright, byte mid, byte dark)
{
    var px = new byte[W * H];
    double cx = W / 2.0, cy = H / 2.0;
    for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            double d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            if (d <= outerR && d >= innerR)
            {
                double mid2 = (outerR + innerR) / 2.0;
                double half = (outerR - innerR) / 2.0;
                double fade = 1 - Math.Abs(d - mid2) / half;
                px[y * W + x] = fade > 0.7 ? bright : fade > 0.4 ? mid : dark;
            }
        }
    return px;
}

byte[] DrawArc(double startAngle, double sweep, double radius, double thickness,
               byte bright, byte mid, byte dark)
{
    var px = new byte[W * H];
    double cx = W / 2.0, cy = H / 2.0;
    double outerR = radius, innerR = radius - thickness;
    for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            double dx = x - cx, dy = y - cy;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d <= outerR && d >= innerR)
            {
                double angle = Math.Atan2(dy, dx);
                if (angle < 0) angle += Math.PI * 2;
                double start = (startAngle % (Math.PI * 2) + Math.PI * 2) % (Math.PI * 2);
                double end   = (start + sweep) % (Math.PI * 2);
                bool inArc = start < end
                    ? angle >= start && angle <= end
                    : angle >= start || angle <= end;
                if (inArc)
                {
                    double mid2 = (outerR + innerR) / 2.0;
                    double half = (outerR - innerR) / 2.0;
                    double fade = 1 - Math.Abs(d - mid2) / half;
                    px[y * W + x] = fade > 0.6 ? bright : fade > 0.3 ? mid : dark;
                }
            }
        }
    return px;
}

byte[] DrawBouncingDots(int frame, int totalFrames, byte bright, byte mid, byte dark)
{
    var px = new byte[W * H];
    double cx = W / 2.0, cy = H / 2.0;
    int[] dotX = [W / 2 - 35, W / 2, W / 2 + 35];
    double baseY = cy + 10;
    double bounceHeight = 18;
    int dotR = 12;

    for (int d = 0; d < 3; d++)
    {
        // Each dot leads by 1/3 of cycle
        double phase = (frame / (double)totalFrames + d / 3.0) % 1.0;
        double yOff = -bounceHeight * Math.Max(0, Math.Sin(phase * Math.PI * 2));
        double dotCy = baseY + yOff;
        byte color = phase > 0.15 && phase < 0.85 ? mid : bright;

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                double dx = x - dotX[d], dy = y - dotCy;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist <= dotR)
                {
                    double fade = 1 - dist / dotR;
                    px[y * W + x] = fade > 0.6 ? color : (color == bright ? mid : dark);
                }
            }
    }
    return px;
}

byte[] DrawX(byte bright, byte dark)
{
    var px = new byte[W * H];
    double cx = W / 2.0, cy = H / 2.0;
    double armLen = 48, thickness = 12;
    for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            double dx = x - cx, dy = y - cy;
            // Distance from diagonal line y=x (rotated 45°)
            double d1 = Math.Abs((dx - dy) / Math.Sqrt(2));
            double d2 = Math.Abs((dx + dy) / Math.Sqrt(2));
            double along1 = (dx + dy) / Math.Sqrt(2);
            double along2 = (dx - dy) / Math.Sqrt(2);
            bool onArm1 = d1 <= thickness / 2 && Math.Abs(along1) <= armLen;
            bool onArm2 = d2 <= thickness / 2 && Math.Abs(along2) <= armLen;
            if (onArm1 || onArm2)
            {
                double minDist = Math.Min(onArm1 ? d1 : double.MaxValue,
                                          onArm2 ? d2 : double.MaxValue);
                double fade = 1 - minDist / (thickness / 2);
                px[y * W + x] = fade > 0.5 ? bright : dark;
            }
        }
    return px;
}

byte[] DrawCheckmark(double progress, double glow, byte bright, byte mid, byte dark)
{
    var px = new byte[W * H];
    // Checkmark: two segments — short leg (100,130)→(80,115) and long leg (80,115)→(125,75)
    // Actually let's center it
    double cx = W / 2.0 + 5, cy = H / 2.0 + 5;
    // Points: start of short leg, corner, end of long leg
    double x0 = cx - 45, y0 = cy - 5;   // start
    double x1 = cx - 15, y1 = cy + 30;  // corner
    double x2 = cx + 45, y2 = cy - 40;  // end

    // Total path length for progress
    double len1 = Math.Sqrt((x1-x0)*(x1-x0)+(y1-y0)*(y1-y0));
    double len2 = Math.Sqrt((x2-x1)*(x2-x1)+(y2-y1)*(y2-y1));
    double totalLen = len1 + len2;
    double drawn = progress * totalLen;

    double thickness = 10 + glow * 6;

    for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            double minDist = double.MaxValue;
            // Segment 1
            if (drawn > 0)
            {
                double segEnd1 = Math.Min(drawn, len1) / len1;
                double ex1 = x0 + (x1 - x0) * segEnd1;
                double ey1 = y0 + (y1 - y0) * segEnd1;
                double d = DistToSegment(x, y, x0, y0, ex1, ey1);
                if (d < minDist) minDist = d;
            }
            // Segment 2
            if (drawn > len1)
            {
                double segEnd2 = (drawn - len1) / len2;
                double ex2 = x1 + (x2 - x1) * segEnd2;
                double ey2 = y1 + (y2 - y1) * segEnd2;
                double d = DistToSegment(x, y, x1, y1, ex2, ey2);
                if (d < minDist) minDist = d;
            }
            if (minDist <= thickness / 2)
            {
                double fade = 1 - minDist / (thickness / 2);
                px[y * W + x] = fade > 0.65 ? bright : fade > 0.35 ? mid : dark;
            }
        }
    return px;
}

double DistToSegment(double px, double py, double ax, double ay, double bx, double by)
{
    double dx = bx - ax, dy = by - ay;
    double lenSq = dx * dx + dy * dy;
    if (lenSq < 1e-9) return Math.Sqrt((px-ax)*(px-ax)+(py-ay)*(py-ay));
    double t = Math.Max(0, Math.Min(1, ((px-ax)*dx+(py-ay)*dy) / lenSq));
    double nearX = ax + t * dx, nearY = ay + t * dy;
    return Math.Sqrt((px-nearX)*(px-nearX)+(py-nearY)*(py-nearY));
}

// ─── GIF89a writer ────────────────────────────────────────────────────────────

void WriteGif(string path, byte[][] palette, List<byte[]> frames, int delayCs)
{
    // palette: list of [R,G,B] — index 0 is always transparent
    // delayCs: frame delay in centiseconds (1/100 s)
    // Pad palette to nearest power of 2
    int palCount = 1;
    while (palCount < palette.Length) palCount <<= 1;
    int palBits = (int)Math.Log2(palCount) - 1; // GCT size field value

    using var fs = File.OpenWrite(path);
    using var bw = new BinaryWriter(fs);

    // Header
    bw.Write("GIF89a"u8);

    // Logical Screen Descriptor
    bw.Write((ushort)W);
    bw.Write((ushort)H);
    bw.Write((byte)(0x80 | (palBits & 0x07))); // Global CT present, 1 byte color res, GCT size
    bw.Write((byte)0); // background color index
    bw.Write((byte)0); // pixel aspect ratio

    // Global Color Table
    for (int i = 0; i < palCount; i++)
    {
        if (i < palette.Length) { bw.Write(palette[i][0]); bw.Write(palette[i][1]); bw.Write(palette[i][2]); }
        else                    { bw.Write((byte)0); bw.Write((byte)0); bw.Write((byte)0); }
    }

    // Netscape Application Extension (loop forever)
    bw.Write((byte)0x21); bw.Write((byte)0xFF); bw.Write((byte)11);
    bw.Write("NETSCAPE2.0"u8);
    bw.Write((byte)3); bw.Write((byte)1);
    bw.Write((ushort)0); // loop count = 0 means infinite
    bw.Write((byte)0);

    int minCodeSize = Math.Max(2, palBits + 1);

    foreach (var frame in frames)
    {
        // Graphic Control Extension — transparent color index 0
        bw.Write((byte)0x21); bw.Write((byte)0xF9); bw.Write((byte)4);
        bw.Write((byte)0x09); // disposal=2 (restore bg), transparent flag set
        bw.Write((ushort)delayCs);
        bw.Write((byte)0); // transparent color index
        bw.Write((byte)0);

        // Image Descriptor
        bw.Write((byte)0x2C);
        bw.Write((ushort)0); bw.Write((ushort)0); // left, top
        bw.Write((ushort)W); bw.Write((ushort)H);
        bw.Write((byte)0); // no local color table, not interlaced

        // Image Data
        bw.Write((byte)minCodeSize);
        byte[] lzw = LzwEncode(frame, minCodeSize);
        int offset = 0;
        while (offset < lzw.Length)
        {
            int block = Math.Min(255, lzw.Length - offset);
            bw.Write((byte)block);
            bw.Write(lzw, offset, block);
            offset += block;
        }
        bw.Write((byte)0); // block terminator
    }

    bw.Write((byte)0x3B); // GIF trailer
}

// ─── LZW encoder ────────────────────────────────────────────────────────────

byte[] LzwEncode(byte[] pixels, int minCodeSize)
{
    int clearCode = 1 << minCodeSize;
    int eoiCode   = clearCode + 1;

    var bitWriter = new BitWriter();
    int codeSize  = minCodeSize + 1;
    int nextCode  = eoiCode + 1;
    int maxCode   = 1 << codeSize;

    // Code table: key = (prefix code, next pixel) → new code
    var table = new Dictionary<(int, byte), int>();

    bitWriter.Write(clearCode, codeSize);

    int prefix = pixels[0];

    for (int i = 1; i < pixels.Length; i++)
    {
        byte pixel = pixels[i];
        var key = (prefix, pixel);

        if (table.TryGetValue(key, out int existing))
        {
            prefix = existing;
        }
        else
        {
            bitWriter.Write(prefix, codeSize);

            if (nextCode < 4096)
            {
                table[key] = nextCode++;
                if (nextCode > maxCode && codeSize < 12)
                {
                    codeSize++;
                    maxCode <<= 1;
                }
            }
            else
            {
                // Table full — reset
                bitWriter.Write(clearCode, codeSize);
                table.Clear();
                nextCode  = eoiCode + 1;
                codeSize  = minCodeSize + 1;
                maxCode   = 1 << codeSize;
            }

            prefix = pixel;
        }
    }

    bitWriter.Write(prefix, codeSize);
    bitWriter.Write(eoiCode, codeSize);
    return bitWriter.ToBytes();
}

sealed class BitWriter
{
    private readonly List<byte> _bytes = [];
    private int _cur, _bits;

    public void Write(int code, int n)
    {
        _cur  |= code << _bits;
        _bits += n;
        while (_bits >= 8)
        {
            _bytes.Add((byte)(_cur & 0xFF));
            _cur  >>= 8;
            _bits -= 8;
        }
    }

    public byte[] ToBytes()
    {
        if (_bits > 0) _bytes.Add((byte)(_cur & 0xFF));
        return [.. _bytes];
    }
}
