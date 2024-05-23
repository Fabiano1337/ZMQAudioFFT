using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Audio
{
    class PanelRenderer
    {
        int bufferSize;
        int superSampling;
        int screenX;
        int screenY;
        int renderX;
        int renderY;
        static List<byte[]> renderCache = new List<byte[]>();
        static RequestSocket client;
        static float sps;
        Thread sendThread;
        bool running = false;
        public PanelRenderer(int xResolution,int yResolution, int SuperSampling, int renderBufferSize)
        {
            screenX = xResolution;
            screenY = yResolution;
            superSampling = SuperSampling;
            bufferSize = renderBufferSize;
            renderX = screenX * superSampling;
            renderY = screenY * superSampling;
        }
        private void SendLoop()
        {
            Stopwatch sw = new Stopwatch();
            while (running)
            {
                sw.Start();
                while (renderCache.Count == 0) { }
                byte[] frame = renderCache[0];
                //byte[] frame = new byte[128 * 128 * 3];
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
        public float getSendRate()
        {
            return sps;
        }
        public void connect(string ip,int port)
        {
            client = new RequestSocket(">tcp://"+ip+":"+port.ToString());
            running = true;
            sendThread = new Thread(SendLoop);
            sendThread.Start();
        }
        public void disconnect()
        {
            running = false;
            client.Close();
        }
        public void addFrame(byte[] entry)
        {
            byte[] screenBuf = new byte[screenX * screenY * 3];
            for (int x = 0; x < screenX; x++)
            {
                for (int y = 0; y < screenY; y++)
                {
                    long totalR = 0;
                    long totalG = 0;
                    long totalB = 0;
                    for (int SSx = 0; SSx < superSampling; SSx++)
                    {
                        for (int SSy = 0; SSy < superSampling; SSy++)
                        {
                            totalR += entry[(((renderX - 1) - ((x * superSampling) + SSx)) * 3 + ((y * superSampling) + SSy) * renderX * 3) + 1];
                            totalB += entry[(((renderX - 1) - ((x * superSampling) + SSx)) * 3 + ((y * superSampling) + SSy) * renderX * 3) + 0];
                            totalG += entry[(((renderX - 1) - ((x * superSampling) + SSx)) * 3 + ((y * superSampling) + SSy) * renderX * 3) + 2];
                        }
                    }
                    byte avgR = (byte)Math.Round(((double)totalR / (superSampling * superSampling)), 0);
                    byte avgG = (byte)Math.Round(((double)totalG / (superSampling * superSampling)), 0);
                    byte avgB = (byte)Math.Round(((double)totalB / (superSampling * superSampling)), 0);

                    //if (avgR != 0) Console.WriteLine(avgR);
                    screenBuf[(((screenX - 1) - x) * 3 + y * screenX * 3) + 0] = avgB;
                    screenBuf[(((screenX - 1) - x) * 3 + y * screenX * 3) + 1] = avgR;
                    screenBuf[(((screenX - 1) - x) * 3 + y * screenX * 3) + 2] = avgG;
                }
            }
            if (renderCache.Count == bufferSize) renderCache.RemoveAt(bufferSize - 1);
            renderCache.Add(screenBuf);
        }
    }
}
