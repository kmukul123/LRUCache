using CacheLibraryTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            UnitTest1 u = new UnitTest1();
            u.TestCornerCases();
            u.TestEviction();
            //Task.Run( u.QueryParallel).GetAwaiter().GetResult();
            Task.Run(u.ParallelInsertUpdate).GetAwaiter().GetResult();


        }
    }
}
