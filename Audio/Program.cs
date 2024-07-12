// See https://aka.ms/new-console-template for more information
using ConsoleRenderer;
using FftSharp;
using NAudio.Wave;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography.Xml;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Audio // Note: actual namespace depends on the project name.
{
    class Audio // Todo : testing SuperSampling
    {
        static readonly int SampleRate = 48000;
        static readonly int FftSize = 4096/2;
        static readonly int BitDepth = 16;
        static readonly int ChannelCount = 1;
        static readonly int BufferMilliseconds = 100;
        static double[] AudioValues;
        static double[] FftValues;
        static double hue = 0d;
        static Stopwatch upsTimer = new Stopwatch();
        static Byte[] buffer = new Byte[renderX * renderY * 3];
        static Byte[] smoothBufferMaxY = new byte[renderX * 3];
        static int[] standTimeDropping = new int[renderX * 3];
        static Byte[] smoothBuffer = new Byte[renderX * renderY * 3];

        static double[] AudioBuffer = new double[SampleRate*10];
        static long AudioBufferLength = 0;
        static float ups, drawps;
        static List<byte[]> renderCache = new List<byte[]>();

        const bool generateFunction = false;

        public const int superSampling = 2;
        public const int screenX = 128;
        public const int screenY = 128;

        public const int renderX = screenX * superSampling;
        public const int renderY = screenY * superSampling;

        static PanelRenderer renderer;

        static Stopwatch timer = new Stopwatch();
        static void Drop()
        {
            for (int x = 0; x < renderX; x++)
            {
                for (int y = renderY - 1; y >= 0; y--)
                {
                    if (buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] == 255)
                    {
                        buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = 0;
                        break;
                    }
                }
            }
        }
        static void DropTopping()
        {
            for (int x = 0; x < renderX; x++)
            {
                if (standTimeDropping[x] <= 200) continue;
                for (int i = 0; i < (double)(standTimeDropping[x]-200)/5d; i++)
                {
                    if (smoothBufferMaxY[x] > 0) smoothBufferMaxY[x] -= 1;
                }
            }
        }
        static int colIndex = 0;
        static Color[] colArray = {Color.Red,Color.Blue,Color.Green,Color.Yellow,Color.White};
        public static void GenerateNextColorFrame()
        {
            smoothBuffer = new Byte[renderX * renderY * 3];
            
            smoothBuffer = fillBlanksBuffer(smoothBuffer, colArray[colIndex].R, colArray[colIndex].G, colArray[colIndex].B);
            colIndex++;
            if (colIndex >= colArray.Length) colIndex = 0;

            SendToScreenBuffer(smoothBuffer);
        }
        static Byte[] GenerateTopping(byte[] buf)
        {
            for (int x = 0; x < renderX; x++)
            {
                for (int i = 0; i < 3; i++)
                {
                    int maxY = 0;
                    for (int y = renderY - 1; y >= 0; y--)
                    {
                        if (buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + i] > 0)
                        {
                            maxY = y;
                            break;
                        }
                    }
                    if (smoothBufferMaxY[x] < maxY)
                    {
                        smoothBufferMaxY[x] = (byte)maxY;
                        standTimeDropping[x] = 0;
                    }
                    else
                    {
                        standTimeDropping[x] += 1;
                    }
                    buf[(((renderX - 1) - x) * 3 + smoothBufferMaxY[x] * renderX * 3) + i] = 255;
                }
            }
            return buf;
        }
        /*static float getMaxInRange(float[] range)
        {
            
        }*/
        static double lastMax = 0;
        static Byte[] fadeScreen(byte[] buf)
        {
            for (int x = 0; x < renderX; x++)
            {
                for (int y = 0; y < renderY; y++)
                {
                    if (buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] > 0)
                        buf[(x * 3 + ((renderY - 1) - y) * renderX * 3) + 0] = (byte)(buf[(x * 3 + ((renderY - 1) - y) * renderX * 3) + 0] - 1);
                    if (buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] > 0)
                        buf[(x * 3 + ((renderY - 1) - y) * renderX * 3) + 1] = (byte)(buf[(x * 3 + ((renderY - 1) - y) * renderX * 3) + 1] - 1);
                    if (buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] > 0)
                        buf[(x * 3 + ((renderY - 1) - y) * renderX * 3) + 2] = (byte)(buf[(x * 3 + ((renderY - 1) - y) * renderX * 3) + 2] - 1);
                }
            }
            return buf;
        }
        static KickDetector kickDetector;
        static void testForKick(double[] data)
        {
            /*double[] paddedAudio = FftSharp.Pad.ZeroPad(data);
            double[] fftMag = FftSharp.Transform.FFTmagnitude(paddedAudio);
            double fftPeriod = FftSharp.Transform.FFTfreqPeriod(SampleRate, fftMag.Length);
            for (int i = 0; i < renderX * renderY * 3; i++)
            {
                smoothBuffer[i] = 0;
            }
            var window = new FftSharp.Windows.Hanning();
            window.ApplyInPlace(data);

            paddedAudio = FftSharp.Pad.ZeroPad(data);
            System.Numerics.Complex[] spectrum = FftSharp.FFT.Forward(paddedAudio);
            */

            var floatvalues = new float[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                floatvalues[i] = (float)data[i];
            }
            kickDetector.ProcessAudioFrame(floatvalues);
        }
        static void GenerateKickFrame(double[] data)
        {

            double[] paddedAudio = FftSharp.Pad.ZeroPad(data);
            double[] fftMag = FftSharp.Transform.FFTmagnitude(paddedAudio);
            double fftPeriod = FftSharp.Transform.FFTfreqPeriod(SampleRate, fftMag.Length);
            for (int i = 0; i < renderX * renderY * 3; i++)
            {
                smoothBuffer[i] = 0;
            }
            var window = new FftSharp.Windows.Hanning();
            window.ApplyInPlace(data);

            paddedAudio = FftSharp.Pad.ZeroPad(data);
            System.Numerics.Complex[] spectrum = FftSharp.FFT.Forward(paddedAudio);
            double[] fft = FftSharp.FFT.Power(spectrum);
            double[] freq = FftSharp.FFT.FrequencyScale(fft.Length, SampleRate);

            Array.Copy(fft, FftValues, fft.Length);
            double maxVal = 0f;
            double minFreq = 20;
            double maxFreq = 200;

            for (int i = 0; i < fft.Length; i++)
            {
                double frequency = freq[i];
                if (frequency > maxFreq) continue;
                if (frequency < minFreq) continue;

                if (FftValues[i] > maxVal) maxVal = FftValues[i];
            }

            double diff = maxVal - lastMax;
            lastMax = maxVal;

            int falloff = 8;
            if(diff > 0.5d)
            {
                for (int x = 0; x < renderX; x++)
                {
                    for (int y = 0; y < renderY; y++)
                    {
                        Color col = Color.FromArgb(255, 255, 255, 255);
                        buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = col.R;
                        buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] = col.G;
                        buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] = col.B;
                    }
                }
            }
            else
            {
                for (int x = 0; x < renderX; x++)
                {
                    for (int y = 0; y < renderY; y++)
                    {
                        if (buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] - falloff >= 0)
                            buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = (byte)(buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] - falloff);
                        else
                            buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = 0;

                        if (buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] - falloff >= 0)
                            buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] = (byte)(buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] - falloff);
                        else
                            buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] = 0;

                        if (buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] - falloff >= 0)
                            buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] = (byte)(buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] - falloff);
                        else
                            buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] = 0;
                    }
                }
            }

            Array.Copy(buffer, 0, smoothBuffer, 0, buffer.Length);

            byte[] entry = new byte[smoothBuffer.Length];
            smoothBuffer.CopyTo(entry, 0);

            SendToScreenBuffer(entry);

            timer.Stop();
            if (timer.ElapsedMilliseconds == 0)
            {
                drawps = 1000;
            }
            else
            {
                drawps = 1000 / timer.ElapsedMilliseconds;
            }
            timer.Reset();
        }
        static void GenerateBuffer(double[] data)
        {
            double[] paddedAudio = FftSharp.Pad.ZeroPad(data);
            double[] fftMag = FftSharp.Transform.FFTmagnitude(paddedAudio);
            double fftPeriod = FftSharp.Transform.FFTfreqPeriod(SampleRate, fftMag.Length);
            for (int i = 0; i < renderX * renderY * 3; i++)
            {
                smoothBuffer[i] = 0;
            }
            var window = new FftSharp.Windows.Hanning();
            window.ApplyInPlace(data);

            paddedAudio = FftSharp.Pad.ZeroPad(data);
            System.Numerics.Complex[] spectrum = FftSharp.FFT.Forward(paddedAudio);
            double[] fft = FftSharp.FFT.Power(spectrum);
            double[] freq = FftSharp.FFT.FrequencyScale(fft.Length, SampleRate);

            Array.Copy(fft, FftValues, fft.Length);

            for (int x = 0; x < renderX; x++)
            {
                double sum = 0;
                int values = 0;


                double factor = 1 + ((double)(x) / ((double)renderX));
                double factorNext = 1 + ((double)(x + 1) / ((double)renderX));

                double minFreq = 20;
                double maxFreq = 20000;
                double bassFreq = 250;
                double highFreq = 4000;
                if (x < ((double)renderX / 3)*1) // Bass
                {
                    double logScale = Math.Log(bassFreq / minFreq, (1d + (1d / 3d)));
                    double frequencyCut = (Math.Pow(factor, logScale)) * minFreq;
                    double frequencyCutNext = (Math.Pow(factorNext, logScale)) * minFreq;
                    
                    for (int i = 0; i < fft.Length; i++)
                    {
                        double frequency = freq[i];
                        if (frequency > frequencyCutNext) continue;
                        if (frequency < frequencyCut) continue;
                        values++;
                        sum += FftValues[i];
                    }
                }
                if (x < ((double)renderX / 3) * 2 && x > ((double)renderX / 3) * 1) // Mid
                {
                    factor -= 1d / 3d;
                    factorNext -= 1d / 3d;
                    double logScale = Math.Log(highFreq / bassFreq, (1d + (1d / 3d)));
                    double frequencyCut = (Math.Pow(factor, logScale)) * bassFreq;
                    double frequencyCutNext = (Math.Pow(factorNext, logScale)) * bassFreq;
                    //Console.WriteLine
                    for (int i = 0; i < fft.Length; i++)
                    {
                        double frequency = freq[i];
                        if (frequency > frequencyCutNext) continue;
                        if (frequency < frequencyCut) continue;
                        values++;
                        sum += FftValues[i];
                    }
                }
                if (x < ((double)renderX / 3) * 3 && x > ((double)renderX / 3) * 2) // High
                {
                    factor -= 2d / 3d;
                    factorNext -= 2d / 3d;
                    double logScale = Math.Log(maxFreq / highFreq, (1d + (1d / 3d)));
                    double frequencyCut = (Math.Pow(factor, logScale)) * highFreq;
                    double frequencyCutNext = (Math.Pow(factorNext, logScale)) * highFreq;
                    
                    for (int i = 0; i < fft.Length; i++)
                    {
                        double frequency = freq[i];
                        if (frequency > frequencyCutNext) continue;
                        if (frequency < frequencyCut) continue;
                        values++;
                        sum += FftValues[i];
                    }
                }
                

                sum = sum / values;
                sum = sum * 1.5d * superSampling;
                for (int y = 0; y < sum; y++)
                {
                    buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = 255;
                }
            }
        }
        public static void SendToScreenBuffer(byte[] entry)
        {
            renderer.addFrame(entry);
        }
        static void GenerateFrame()
        {
            timer.Start();
            if (generateFunction)
            {
                for (int x = 0; x < renderX; x++)
                {
                    double factor = (double)(x + 1) / ((double)renderX + 1);
                    for (int y = renderY - 1; y >= 0; y--)
                    {
                        if (buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] != 255) continue;
                        for (int fx = 0; fx < renderX; fx++)
                        {
                            double quadraticFactor = -(Math.Pow(factor + 0.75, 10));
                            double cur = (quadraticFactor * Math.Pow((fx - x), 2) + y);
                            if (cur < 0) continue;
                            //if (cur > 255) continue;
                            for (int cy = 0; cy < (int)cur; cy++)
                            {
                                if (fx < ((renderX / 3d) * 1))
                                {
                                    smoothBuffer[(((renderX - 1) - fx) * 3 + cy * renderX * 3) + 0] = 255;
                                    smoothBuffer[(((renderX - 1) - fx) * 3 + cy * renderX * 3) + 2] = 0;
                                    smoothBuffer[(((renderX - 1) - fx) * 3 + cy * renderX * 3) + 1] = 0;
                                    continue;
                                }
                                if (fx < ((renderX / 3d) * 2))
                                {
                                    smoothBuffer[(((renderX - 1) - fx) * 3 + cy * renderX * 3) + 2] = 255;
                                    smoothBuffer[(((renderX - 1) - fx) * 3 + cy * renderX * 3) + 0] = 0;
                                    smoothBuffer[(((renderX - 1) - fx) * 3 + cy * renderX * 3) + 1] = 0;
                                    continue;
                                }
                                if (fx < ((renderX / 3d) * 3))
                                {
                                    smoothBuffer[(((renderX - 1) - fx) * 3 + cy * renderX * 3) + 1] = 255;
                                    smoothBuffer[(((renderX - 1) - fx) * 3 + cy * renderX * 3) + 0] = 0;
                                    smoothBuffer[(((renderX - 1) - fx) * 3 + cy * renderX * 3) + 2] = 0;
                                    continue;
                                }
                            }
                        }
                        break;
                    }
                }
            }
            else
            {
                int[] yMax = new int[renderX];
                for (int x = 0; x < renderX; x++)
                {
                    int maxY = 0;
                    for (int y = renderY - 1; y >= 0; y--)
                    {
                        if (buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] != 255) continue;
                        //Console.WriteLine("tset");
                        maxY = y;
                        break;
                    }
                    yMax[x] = maxY;
                }
                int lastX = 0;
                for (int x = 0; x < renderX; x++)
                {
                    int x1 = lastX;
                    int y1 = yMax[x1];
                    int x2=0;
                    for (int cx = x; cx < renderX; cx++)
                    {
                        if (yMax[cx] == 0) continue;
                        x2 = cx;
                        break;
                    }
                    int y2 = yMax[x2];
                    int curMaxY = (int)((((double)(y2 - y1)) / ((double)(x2 - x1))) * ((double)(x - x1))) + y1;
                    if (yMax[x] != 0) lastX = x;
                    if (x1 == 0)
                    {
                        continue;
                    }
                    if (x2 == 0)
                    {
                        continue;
                    }
                    double huerange = 90;
                    for (int y = 0; y < renderY; y++)
                    {
                        if(y < curMaxY)
                        {
                            Color col = ColorFromHSV(huerange - (((double)y / (double)renderY) * huerange), 1, 1);
                            smoothBuffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = col.R;
                            smoothBuffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] = col.G;
                            smoothBuffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] = col.B;
                            continue;
                            if (x < ((renderX / 100d) * 33.333d))
                            {
                                //Color col = ColorFromHSV((((double)y / (double)renderY) * 116d), 1, 1);
                                smoothBuffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = col.R;
                                smoothBuffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] = col.G;
                                smoothBuffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] = col.B;
                                continue;
                            }
                            if (x < ((renderX / 100d) * 66.666d))
                            {
                                smoothBuffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] = 255;
                                smoothBuffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = 0;
                                smoothBuffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] = 0;
                                continue;
                            }
                            if (x < ((renderX / 100d) * 100d))
                            {
                                smoothBuffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] = 255;
                                smoothBuffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = 0;
                                smoothBuffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] = 0;
                                continue;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            smoothBuffer = IntensifyBuffer(smoothBuffer);
            smoothBuffer = GenerateTopping(smoothBuffer);
            //smoothBuffer = cloneRoateBuffer(smoothBuffer);

            Color c = ColorFromHSV(hue, 1, 1);
            hue += 0.1d;
            //smoothBuffer = fillBlanksBuffer(smoothBuffer, c.R, c.G, c.B);
            byte[] entry = new byte[smoothBuffer.Length];
            smoothBuffer.CopyTo(entry, 0);

            SendToScreenBuffer(entry);



            timer.Stop();
            //Thread.Sleep(1);
            if (timer.ElapsedMilliseconds == 0)
            {
                drawps = 1000;
            }
            else
            {
                drawps = 1000 / timer.ElapsedMilliseconds;
            }
            timer.Reset();
        }
        static int dataLength;
        static void Main(string[] args)
        {
            //Console.WriteLine();
            //while (true) { }
            renderer = new PanelRenderer(screenX,screenY,superSampling,3);
            renderer.connect("192.168.178.53", 1500);

            //client = new RequestSocket(">tcp://192.168.178.53:1500");
            FConsole.Initialize("Visualizer", ConsoleColor.Red, ConsoleColor.Black);
            AudioValues = new double[SampleRate * BufferMilliseconds / 1000];
            double[] paddedAudio = FftSharp.Pad.ZeroPad(AudioValues);
            double[] fftMag = FftSharp.Transform.FFTmagnitude(paddedAudio);
            var capture = new WasapiLoopbackCapture();
            capture.WaveFormat = new NAudio.Wave.WaveFormat(rate: SampleRate, bits: BitDepth, channels: ChannelCount);

            capture.DataAvailable += (s, a) =>
            {
                for (int i = 0; i < a.Buffer.Length / 2; i++)
                    AudioValues[i] = BitConverter.ToInt16(a.Buffer, i * 2);
                upsTimer.Stop();
                dataLength = a.BytesRecorded;
                Array.Copy(AudioValues, 0, AudioBuffer, AudioBufferLength, a.BytesRecorded/2);
                AudioBufferLength += a.BytesRecorded/2;
                ups = 1000 / upsTimer.ElapsedMilliseconds;
                upsTimer.Reset();
                upsTimer.Start();
            };
            capture.StartRecording();
            upsTimer.Start();
            for (int i = 0; i < renderX * renderY * 3; i++)
            {
                buffer[i] = 0;
            }

            int sampleSize = 2400;//4800
            int skipSize = FftSize; //sampleSize / 8
            kickDetector = new KickDetector(SampleRate, FftSize, 2000000, 7500);
            //kickDetector.onKickEnter += Kick();
            Stopwatch cycle = new Stopwatch();
            cycle.Start();
            while (true)
            {
                try
                {
                    if (AudioBufferLength < sampleSize)
                    {
                        continue;
                    }
                    //double[] shortenedData = new double[AudioValues.Length];
                    //Array.Copy(AudioValues, 0, shortenedData, 0, AudioValues.Length);

                    //int samples = dataLength / 882;
                    
                    
                    double[] data = new double[FftSize];

                    //Array.Copy(shortenedData, i * (shortenedData.Length / samples), data, 0, shortenedData.Length / samples);
                    Array.Copy(AudioBuffer, 0, data, 0, FftSize);
                    Array.Copy(AudioBuffer, skipSize, AudioBuffer, 0, SampleRate);
                    AudioBufferLength -= skipSize;
                    //Console.WriteLine(AudioBufferLength);
                    
                    //Console.WriteLine(AudioBufferLength);
                    paddedAudio = FftSharp.Pad.ZeroPad(data);
                    //GenerateBuffer(paddedAudio);
                    //GenerateFrame();
                    //GenerateKickFrame(paddedAudio);
                    testForKick(data);

                    //for (int z = 0; z < 4 * superSampling; z++) Drop();
                    //for (int z = 0; z < 1; z++) DropTopping();
                    /*for (int x = 0; x < renderX; x++)
                    {
                        for (int y = renderY - 1; y >= 0; y--)
                        {
                            buffer[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = 0;
                        }
                    }*/

                    Console.Clear();
                    Console.SetCursorPosition(0, 0);
                    Console.WriteLine("Draw : " + (1000/cycle.ElapsedMilliseconds) + "/s");
                    Console.WriteLine("Update : " + ups + "/s");
                    Console.WriteLine("Send : " + renderer.getSendRate() + "/s");
                    Console.WriteLine("Rendercache : " + renderCache.Count);
                    Console.WriteLine("AudioBuffer : " + AudioBufferLength);
                    cycle.Stop();
                    cycle.Restart();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

            }

            Console.WriteLine("C# Audio Level Meter");
            Console.WriteLine("(press any key to exit)");
            Console.ReadKey();
        }
        static void Kick()
        {
            Console.WriteLine("OnKickEnter");
        }
        static byte[] IntensifyBuffer(byte[] buf)
        {
            for (int x = 0; x < renderX; x++)
            {
                for (int i = 0; i < 3; i++)
                {
                    int maxY = -1;
                    for (int y = renderY - 1; y >= 0; y--)
                    {
                        if (buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + i] > 0)
                        {
                            maxY = y;
                            break;
                        }
                    }
                    if (maxY == -1) continue;
                    for (int y = 0; y < maxY + 1; y++)
                    {
                        double scalingFactor = 0.3d;
                        double factor = 1d - ((double)(y + 1) / (double)(maxY + 1));
                        factor = Math.Pow(factor, scalingFactor);
                        buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + i] = (byte)(buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + i] * factor);
                    }
                }
            }
            return buf;
        }
        static byte[] cloneRoateBuffer(byte[] buf)
        {
            for (int x = 0; x < renderX; x++)
            {
                for (int y = 0; y < renderY; y++)
                {
                    if (buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] > 0)
                        buf[(x * 3 + ((renderY - 1) - y) * renderX * 3) + 0] = buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0];
                    if (buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] > 0)
                        buf[(x * 3 + ((renderY - 1) - y) * renderX * 3) + 1] = buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1];
                    if (buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] > 0)
                        buf[(x * 3 + ((renderY - 1) - y) * renderX * 3) + 2] = buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2];
                }
            }
            return buf;
        }
        static byte[] fillBlanksBuffer(byte[] buf, byte r, byte g, byte b)
        {
            for (int x = 0; x < renderX; x++)
            {
                for (int y = 0; y < renderY; y++)
                {
                    if (buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] == 0 && buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] == 0 && buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] == 0)
                    {
                        buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = b;
                        buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] = r;
                        buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] = g;
                    }
                }
            }
            return buf;
        }
        public static void ColorToHSV(Color color, out double hue, out double saturation, out double value)
        {
            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));

            hue = color.GetHue();
            saturation = (max == 0) ? 0 : 1d - (1d * min / max);
            value = max / 255d;
        }

        public static Color ColorFromHSV(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            int v = Convert.ToInt32(value);
            int p = Convert.ToInt32(value * (1 - saturation));
            int q = Convert.ToInt32(value * (1 - f * saturation));
            int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

            if (hi == 0)
                return Color.FromArgb(255, v, t, p);
            else if (hi == 1)
                return Color.FromArgb(255, q, v, p);
            else if (hi == 2)
                return Color.FromArgb(255, p, v, t);
            else if (hi == 3)
                return Color.FromArgb(255, p, q, v);
            else if (hi == 4)
                return Color.FromArgb(255, t, p, v);
            else
                return Color.FromArgb(255, v, p, q);
        }
    }
}
