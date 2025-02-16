using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Audio
{
    class Effect_Flash : Effect
    {
        int[] buffer;
        int renderX, renderY, quadrant;
        float fadeOutTime;
        Stopwatch timer;
        Color col;
        public Effect_Flash(int renderX,int renderY,float fadeOutTime, Color col, int quadrant)
        {
            this.quadrant = quadrant;
            this.renderY = renderY;
            this.renderX = renderX;
            this.fadeOutTime = fadeOutTime;
            this.col = col;
        }
        public override void Start()
        {
            running = true;
            timer = Stopwatch.StartNew();
        }
        public override int[] drawFrame()
        {
            byte r = (byte)(col.R * (1 - (timer.ElapsedMilliseconds / fadeOutTime)));
            byte g = (byte)(col.G * (1 - (timer.ElapsedMilliseconds / fadeOutTime)));
            byte b = (byte)(col.B * (1 - (timer.ElapsedMilliseconds / fadeOutTime)));
            if (timer.ElapsedMilliseconds >= fadeOutTime) running=false;

            buffer = new int[renderX * renderY * 3];

            buffer = clearBuffer(buffer);

            buffer = fillBlanksBuffer(buffer, r, g, b, quadrant);

            return buffer;
        }
        public override void Stop()
        {
            running = false;
        }
        int[] clearBuffer(int[] buf)
        {
            for (int x = 0; x < renderX; x++)
            {
                for (int y = 0; y < renderY; y++)
                {
                    buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = -1;
                    buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] = -1;
                    buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] = -1;
                }
            }
            return buf;
        }
        int[] fillBlanksBuffer(int[] buf, byte r, byte g, byte b, int quadrant)
        {
            if (quadrant == -1)
            {
                for (int x = 0; x < renderX; x++)
                {
                    for (int y = 0; y < renderY; y++)
                    {
                            if (b != 0) buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = b;
                            if (r != 0) buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] = r;
                            if (g != 0) buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] = g;
                    }
                }
            }
            if (quadrant == 0)
            {
                for (int x = 0; x < renderX / 2; x++)
                {
                    for (int y = 0; y < renderY / 2; y++)
                    {
                        if (b != 0) buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = b;
                        if (r != 0) buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] = r;
                        if (g != 0) buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] = g;
                    }
                }
            }
            if (quadrant == 1)
            {
                for (int x = renderX / 2; x < renderX; x++)
                {
                    for (int y = 0; y < renderY / 2; y++)
                    {
                        if (b != 0) buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = b;
                        if (r != 0) buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] = r;
                        if (g != 0) buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] = g;
                    }
                }
            }
            if (quadrant == 2)
            {
                for (int x = 0; x < renderX / 2; x++)
                {
                    for (int y = renderY/2; y < renderY; y++)
                    {
                        if (b != 0) buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = b;
                        if (r != 0) buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] = r;
                        if (g != 0) buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] = g;
                    }
                }
            }
            if (quadrant == 3)
            {
                for (int x = renderX / 2; x < renderX; x++)
                {
                    for (int y = renderY/2; y < renderY; y++)
                    {
                        if (b != 0) buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = b;
                        if (r != 0) buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] = r;
                        if (g != 0) buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] = g;
                    }
                }
            }
            return buf;
        }
    }
}
