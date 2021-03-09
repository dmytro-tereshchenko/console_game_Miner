using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miner
{
    class Program
    {

        static void Main(string[] args)
        {
            Miner miner = new Miner(new User(), new SingleStatControlArrowFactory());
            miner.Start();
        }
    }
}
