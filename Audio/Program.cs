// See https://aka.ms/new-console-template for more information
using ConsoleRenderer;
using FftSharp;
using NAudio.Wave;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Intrinsics.X86;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Audio // Note: actual namespace depends on the project name.
{
    class Audio
    {
        static readonly int SampleRate = 44100;
        static readonly int BitDepth = 16;
        static readonly int ChannelCount = 1;
        static readonly int BufferMilliseconds = 100;
        static double[] AudioValues;
        static double[] FftValues;
        static double hue = 0d;
        static RequestSocket client;
        static Stopwatch upsTimer = new Stopwatch();
        static Byte[] buffer = new Byte[128 * 128 * 3];
        static Byte[] smoothBuffer = new Byte[128 * 128 * 3];

        static int bufferSelector = 1;
        static Byte[] sendBuffer1 = new Byte[128 * 128 * 3];
        static Byte[] sendBuffer2 = new Byte[128 * 128 * 3];
        static bool dropping = false;
        static float ups, drawps,sps;
        static List<byte[]> renderCache = new List<byte[]>();
        static void SendLoop()
        {
            Stopwatch sw = new Stopwatch();
            while(true)
            {
                sw.Start();
                while (renderCache.Count == 0) { }
                byte[] frame = renderCache[0];
                while (frame == null) { frame = renderCache[0]; }
                client.SendFrame(frame);
                renderCache.RemoveAt(0);
                Msg message = new Msg();
                message.InitEmpty();
                client.Receive(ref message);
                sw.Stop();
                sps = 1000 / sw.ElapsedMilliseconds;
                sw.Reset();
            }
        }

        static Stopwatch timer = new Stopwatch();
        static void Drop()
        {
            for (int x = 0; x < 128; x++)
            {
                for (int y = 127; y >= 0; y--)
                {
                    if (buffer[((127 - x) * 3 + y * 128 * 3) + 0] == 255)
                    {
                        buffer[((127 - x) * 3 + y * 128 * 3) + 0] = 0;
                        break;
                    }
                }
            }
        }
        static void GenerateBuffer(double[] data)
        {
            double[] paddedAudio = FftSharp.Pad.ZeroPad(data);
            double[] fftMag = FftSharp.Transform.FFTmagnitude(paddedAudio);
            double fftPeriod = FftSharp.Transform.FFTfreqPeriod(SampleRate, fftMag.Length);
            for (int i = 0; i < 128 * 128 * 3; i++)
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


            int maxX = 128;
            for (int x = 0; x < maxX; x++)
            {
                double sum = 0;
                int values = 0;


                double factor = 1+((double)(x) / ((double)maxX));
                double factorNext = 1+((double)(x+1) / ((double)maxX));
                
                double logScale = 10; // 4.3
                double frequencyCut = (Math.Pow(factor, logScale))*20;
                double frequencyCutNext = (Math.Pow(factorNext, logScale)) * 20;

                //Console.WriteLine(frequencyCut);
                //Console.WriteLine(frequencyCutNext);
                //if (factor > 1) continue;
                //Console.WriteLine(fft.Length);
                for (int i = 0; i < fft.Length; i++)
                {
                    //double frequency = i * fftPeriod;
                    double frequency = freq[i];
                    //Console.WriteLine(frequency.ToString());
                    if (frequency > frequencyCutNext) continue;
                    if (frequency < frequencyCut) continue;
                    //Console.WriteLine("test");
                    values++;
                    sum += FftValues[i];
                }

                sum = sum / values;
                sum = sum * 1.5d;
                for (int y = 0; y < sum; y++)
                {
                    buffer[((127 - x) * 3 + y * 128 * 3) + 0] = 255;
                }
            }
        }
        static void GenerateFrame()
        {
            timer.Start();
            int maxX = 128;
            for (int x = 0; x < 128; x++)
            {
                double factor = (double)(x + 1) / ((double)maxX + 1);
                for (int y = 127; y >= 0; y--)
                {
                    if (buffer[((127 - x) * 3 + y * 128 * 3) + 0] != 255) continue;
                    for (int fx = 0; fx < 128; fx++)
                    {
                        double quadraticFactor = -(Math.Pow(factor+0.75,10));
                        double cur = (quadraticFactor * Math.Pow((fx - x), 2) + y);
                        if (cur < 0) continue;
                        if (cur > 255) continue;
                        for (int cy = 0; cy < (byte)cur; cy++)
                        {
                            if (fx < ((maxX / 3d) * 1))
                            {
                                smoothBuffer[((127 - fx) * 3 + cy * 128 * 3) + 0] = 255;
                                smoothBuffer[((127 - fx) * 3 + cy * 128 * 3) + 2] = 0;
                                smoothBuffer[((127 - fx) * 3 + cy * 128 * 3) + 1] = 0;
                                continue;
                            }
                            if (fx < ((maxX / 3d) * 2))
                            {
                                smoothBuffer[((127 - fx) * 3 + cy * 128 * 3) + 2] = 255;
                                smoothBuffer[((127 - fx) * 3 + cy * 128 * 3) + 0] = 0;
                                smoothBuffer[((127 - fx) * 3 + cy * 128 * 3) + 1] = 0;
                                continue;
                            }
                            if (fx < ((maxX / 3d) * 3))
                            {
                                smoothBuffer[((127 - fx) * 3 + cy * 128 * 3) + 1] = 255;
                                smoothBuffer[((127 - fx) * 3 + cy * 128 * 3) + 0] = 0;
                                smoothBuffer[((127 - fx) * 3 + cy * 128 * 3) + 2] = 0;
                                continue;
                            }
                        }
                    }
                    break;
                }
            }
            smoothBuffer = IntensifyBuffer(smoothBuffer);
            smoothBuffer = cloneRoateBuffer(smoothBuffer);
            Color c = ColorFromHSV(hue, 1, 1);
            hue += 0.1d;
            //smoothBuffer = fillBlanksBuffer(smoothBuffer, c.R, c.G, c.B);
            byte[] entry = new byte[smoothBuffer.Length];
            smoothBuffer.CopyTo(entry,0);

            if (renderCache.Count == 5) renderCache.RemoveAt(4);
            renderCache.Add(entry);



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
            client = new RequestSocket(">tcp://192.168.178.53:1500");
            FConsole.Initialize("Visualizer", ConsoleColor.Red, ConsoleColor.Black);
            AudioValues = new double[SampleRate * BufferMilliseconds / 1000];
            double[] paddedAudio = FftSharp.Pad.ZeroPad(AudioValues);
            double[] fftMag = FftSharp.Transform.FFTmagnitude(paddedAudio);
            FftValues = new double[fftMag.Length];
            var capture = new WasapiLoopbackCapture();
            capture.WaveFormat = new NAudio.Wave.WaveFormat(rate: SampleRate, bits: BitDepth, channels: ChannelCount);

            capture.DataAvailable += (s, a) =>
            {
                for (int i = 0; i < a.Buffer.Length / 2; i++)
                    AudioValues[i] = BitConverter.ToInt16(a.Buffer, i * 2);
                upsTimer.Stop();
                dataLength = a.BytesRecorded;
                //Console.WriteLine("UPS : " + (1000 / upsTimer.ElapsedMilliseconds));
                ups = 1000 / upsTimer.ElapsedMilliseconds;
                upsTimer.Reset();
                upsTimer.Start();
            };
            capture.StartRecording();
            upsTimer.Start();
            for (int i = 0; i < 128 * 128 * 3; i++)
            {
                buffer[i] = 0;
            }
            Thread t = new Thread(SendLoop);
            t.Start();
            while (true)
            {
                if (dataLength == 0) continue;
                //Console.WriteLine(dataLength);
                //Console.WriteLine(AudioValues.Length);
                try {
                    double[] shortenedData = new double[(dataLength / 2)];
                    Array.Copy(AudioValues, 0, shortenedData, 0, dataLength / 2);

                    int samples = dataLength / 882;
                    //Console.WriteLine(samples);
                    for (int i = 0; i < samples; i++)
                    {
                        double[] data = new double[shortenedData.Length / samples];
                        Array.Copy(shortenedData, i * (shortenedData.Length / samples), data, 0, shortenedData.Length / samples);
                        paddedAudio = FftSharp.Pad.ZeroPad(data);
                        GenerateBuffer(paddedAudio);
                        GenerateFrame();
                        Drop();
                        Drop();
                        Drop();
                        Stopwatch wait = new Stopwatch();
                        wait.Start();
                        //while(wait.ElapsedMilliseconds < 4)
                        //{

                        //Drop();
                        //GenerateFrame();
                        //Thread.Sleep(1);
                        //}
                        wait.Stop();
                        wait.Reset();
                    }
                } 
                catch(Exception ex)
                {

                }
                
                Console.Clear();
                Console.SetCursorPosition(0, 0);
                Console.WriteLine("Draw : " + drawps + "/s");
                Console.WriteLine("Update : " + ups + "/s");
                Console.WriteLine("Send : " + sps + "/s");
            }

            Console.WriteLine("C# Audio Level Meter");
            Console.WriteLine("(press any key to exit)");
            Console.ReadKey();
        }
        static byte[] IntensifyBuffer(byte[] buf)
        {
            for (int x = 0; x < 128; x++)
            {
                for (int i = 0; i < 3; i++)
                {
                    int maxY = -1;
                    for (int y = 127; y >= 0; y--)
                    {
                        if (buf[((127 - x) * 3 + y * 128 * 3) + i] > 0)
                        {
                            maxY = y;
                            break;
                        }
                    }
                    if (maxY == -1) continue;
                    for (int y = 0; y < maxY+1; y++)
                    {
                        double scalingFactor = 0.8d;
                        double factor = 1d-((double)(y + 1) / (double)(maxY + 1));
                        factor = Math.Pow(factor, scalingFactor);
                        buf[((127 - x) * 3 + y * 128 * 3) + i] = (byte)(buf[((127 - x) * 3 + y * 128 * 3) + i]*factor);
                    }
                }
            }
            return buf;
        }
        static byte[] cloneRoateBuffer(byte[] buf)
        {
            for (int x = 0; x < 128; x++)
            {
                for (int y = 0; y < 128; y++)
                {
                    if (buf[((127 - x) * 3 + y * 128 * 3) + 0] > 0)
                        buf[((x) * 3 + (127 - y) * 128 * 3) + 0] = buf[((127 - x) * 3 + y * 128 * 3) + 0];
                    if (buf[((127 - x) * 3 + y * 128 * 3) + 1] > 0)
                        buf[((x) * 3 + (127 - y) * 128 * 3) + 1] = buf[((127 - x) * 3 + y * 128 * 3) + 1];
                    if (buf[((127 - x) * 3 + y * 128 * 3) + 2] > 0)
                        buf[((x) * 3 + (127 - y) * 128 * 3) + 2] = buf[((127 - x) * 3 + y * 128 * 3) + 2];
                }
            }
            return buf;
        }
        static byte[] fillBlanksBuffer(byte[] buf,byte r,byte g, byte b)
        {
            for (int x = 0; x < 128; x++)
            {
                for (int y = 0; y < 128; y++)
                {
                    if(buf[((127 - x) * 3 + y * 128 * 3) + 0] == 0 && buf[((127 - x) * 3 + y * 128 * 3) + 1] == 0 && buf[((127 - x) * 3 + y * 128 * 3) + 2] == 0)
                    {
                        buf[((127 - x) * 3 + y * 128 * 3) + 0] = b;
                        buf[((127 - x) * 3 + y * 128 * 3) + 1] = r;
                        buf[((127 - x) * 3 + y * 128 * 3) + 2] = g;
                    }
                }
            }
            return buf;
        }
        static WaveInEvent waveIn = new NAudio.Wave.WaveInEvent
        {
            DeviceNumber = 0, // indicates which microphone to use
            WaveFormat = new NAudio.Wave.WaveFormat(rate: 44100, bits: 16, channels: 1),
            BufferMilliseconds = 20
        };
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

        static void WaveIn_DataAvailable(object? sender, NAudio.Wave.WaveInEventArgs e)
        {
            // copy buffer into an array of integers
            Int16[] values = new Int16[e.Buffer.Length / 2];
            Buffer.BlockCopy(e.Buffer, 0, values, 0, e.Buffer.Length);

            // determine the highest value as a fraction of the maximum possible value
            float fraction = (float)values.Max() / 32768;

            // print a level meter using the console
            string bar = new('#', (int)(fraction * 70));
            string meter = "[" + bar.PadRight(60, '-') + "]";
            Console.CursorLeft = 0;
            Console.CursorVisible = false;
            Console.Write($"{meter} {fraction * 100:00.0}%");
        }
    }
}
