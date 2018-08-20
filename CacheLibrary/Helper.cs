using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheLibrary
{
    static class Helper
    {   
        public static bool Remove(this ConcurrentDictionary<string, Task> dict, string key)
        {
            do
            {
                Task t = null;
                if (dict.TryRemove(key, out t))
                    break;
                Logger.Warn("retrying Removing from tasks" + key);
            } while (true);
            return true;
        }
        public static bool Add(this ConcurrentDictionary<string, Task> dict, string key, Task task)
        {
            do
            {
                if (dict.TryAdd(key, task))
                    break;
                Logger.Warn("retrying Adding to tasks" + key);
            } while (true);
            return true;
        }

    }
}
