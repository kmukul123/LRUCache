using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CacheLibrary
{
    internal class CacheNode<TKey, TValue> /* todo we can have an interface here: ILockedCacheNode */
    {
        internal TKey key { get; private set; }
        //TODO: we can consider using WeakReference in a real cache
        internal TValue cachedValue { get; set; }
        internal object lockobject { get; private set; }
        internal static LinkedListNode<CacheNode<TKey, TValue>> CreateLLNode(TKey key, TValue value)
        {
            return new LinkedListNode<CacheNode<TKey, TValue>>(new CacheNode<TKey, TValue>(key, value));
        }
        internal CacheNode(TKey key, TValue value)
        {
            this.key = key;
            this.cachedValue = value;
            this.lockobject = new object(); // as an optimization we can use this as the as well as it is supposedly internal
        }
        internal void UnlockNode()
        {
            Monitor.Exit(lockobject);
        }
        internal bool TryLock()
        {
            return Monitor.TryEnter(lockobject);
        }
        public override string ToString()
        {
            return $"{key}:{cachedValue}";
        }
        
    }
}
