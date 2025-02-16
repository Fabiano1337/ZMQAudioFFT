using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Audio
{
    public class EffectManager
    {
        List<Effect> effects;
        int renderX, renderY;
        public EffectManager(int renderX,int renderY)
        {
            this.renderX = renderX;
            this.renderY = renderY;
            effects = new List<Effect>();
            var t = new Thread(() => run());
            t.Start();
        }
        public void runEffect(Effect effect)
        {
            effect.Start();
            lock (effects)
                effects.Add(effect);
        }
        void run()
        {
            Thread.Sleep(500); // crash fix lol
            while (true)
            {
                lock (effects)
                {
                    for (int i = 0; i < effects.Count; i++)
                    {
                        if (!effects[i].running)
                        {
                            effects.RemoveAt(i);
                            i--;
                        }
                    }
                }

                if (effects.Count == 0)
                {
                    byte[] emptyFrame = new byte[renderX * renderY * 3];
                    Audio.SendToScreenBuffer(emptyFrame);
                    continue;
                }
                int[][] frames;
                lock (effects)
                {
                    frames = new int[effects.Count][];
                    for (int i = 0; i < effects.Count; i++)
                    {
                        int[] frame = effects[i].drawFrame();
                        frames[i] = frame;
                    }
                }

                byte[] fullFrame = new byte[renderX * renderY * 3];
                //fullFrame= clearBuffer(fullFrame);
                for (int j = 0; j < frames.Length; j++)
                {
                    for (int x = 0; x < renderX; x++)
                    {
                        for (int y = 0; y < renderY; y++)
                        {
                            if (frames[j][(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] != -1)
                            {
                                fullFrame[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = (byte)frames[j][(((renderX - 1) - x) * 3 + y * renderX * 3) + 0];
                            }
                            if (frames[j][(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] != -1)
                            {
                                fullFrame[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] = (byte)frames[j][(((renderX - 1) - x) * 3 + y * renderX * 3) + 1];
                            }
                            if (frames[j][(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] != -1)
                            {
                                fullFrame[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] = (byte)frames[j][(((renderX - 1) - x) * 3 + y * renderX * 3) + 2];
                            }
                        }
                    }
                }
                Audio.SendToScreenBuffer(fullFrame);
            }
        }
        byte[] clearBuffer(byte[] buf)
        {
            for (int x = 0; x < renderX; x++)
            {
                for (int y = 0; y < renderY; y++)
                {
                    if (buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] == 0 && buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] == 0 && buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] == 0)
                    {
                        buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 0] = 0;
                        buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 1] = 0;
                        buf[(((renderX - 1) - x) * 3 + y * renderX * 3) + 2] = 0;
                    }
                }
            }
            return buf;
        }
    }
}
