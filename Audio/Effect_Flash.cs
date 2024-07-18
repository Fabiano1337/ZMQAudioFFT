using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Audio
{
    class Effect_Flash
    {
        Byte[] buffer;
        int renderX, renderY;
        float fadeOutTime;
        Stopwatch timer;
        Color col;
        public Effect_Flash(int renderX,int renderY,float fadeOutTime, Color col)
        {
            this.renderY = renderY;
            this.renderX = renderX;
            this.fadeOutTime = fadeOutTime;
            this.col = col;
        }
        public void Start()
        {
            timer = Stopwatch.StartNew();
            while(true)
            {
                byte r = (byte)(col.R * (1-(timer.ElapsedMilliseconds / fadeOutTime)));
                byte g = (byte)(col.G * (1-(timer.ElapsedMilliseconds / fadeOutTime)));
                byte b = (byte)(col.B * (1-(timer.ElapsedMilliseconds / fadeOutTime)));
                Color dimCol = Color.FromArgb(255,r,g,b);
                if (timer.ElapsedMilliseconds >= fadeOutTime) break;
                sendColor(dimCol);
                //Thread.Sleep(5);
            }
            sendColor(Color.FromArgb(0,0,0,0));
        }
        private void sendColor(Color col)
        {
            buffer = new Byte[renderX * renderY * 3];

            buffer = fillBlanksBuffer(buffer, col.R, col.G, col.B);

            Audio.SendToScreenBuffer(buffer);
        }
        byte[] fillBlanksBuffer(byte[] buf, byte r, byte g, byte b)
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
    }
}
