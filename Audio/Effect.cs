using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Audio
{
    public abstract class Effect
    {
        public bool running;
        public abstract int[] drawFrame();
        public abstract void Start();
        public abstract void Stop();
    }
}
