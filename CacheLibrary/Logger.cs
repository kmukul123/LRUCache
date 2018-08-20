using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheLibrary
{
    static class Logger
    {
        public static void Info(string s)
        {
            Trace.TraceInformation(DateTime.Now.ToShortTimeString()+" "+s);
        }

        internal static void Error(string v)
        {
            throw new NotImplementedException();
        }

        internal static void Warn(string v)
        {
            Trace.TraceWarning(v);
        }
    }
}
