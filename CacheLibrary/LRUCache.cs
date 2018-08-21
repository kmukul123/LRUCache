using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CacheLibrary
{
    /// <summary>
    /// Thread Safe cache which implements ICache Methods for a given cache size
    /// the cache is LRU which 
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    class LRUCache<TKey, TValue> : ICache<TKey, TValue>
    {
        
        internal uint Count => (uint) cacheDictionary.Count; 

        //TODO: we can have interfaces for these
        private CacheList<TKey,TValue> cacheList; //the cachelist elements are in the order of they have been recently accessed the element best to remove is at the end
        private ConcurrentDictionary<TKey, LinkedListNode<CacheNode<TKey,TValue>>> cacheDictionary;

        private uint cacheSize;
        private const uint minsize = 1;
        private const uint listSizeVariance = 2;
        /// <summary>
        /// creates a new cache with a given size, the size cannot be changed
        /// this method doesnt need to be threadsafe
        /// </summary>
        /// <param name="cacheSize"></param>
        public LRUCache(uint cacheSize)
        {
            if (cacheSize < minsize)
                throw new ArgumentException("please specify a greater size");
            this.cacheList = new CacheList<TKey, TValue>(cacheSize);
            this.cacheDictionary = new ConcurrentDictionary<TKey, LinkedListNode<CacheNode<TKey, TValue>>>();
            this.cacheSize = cacheSize;
        }

        /// <summary>
        /// add or update in the cache.
        /// if the cache grows over the maximum size one element is evicted
        /// the element removed fromt the cache is element which is last accessed by AddOrUpdate or TryGetValue
        /// the method is thread safe but order of execution of different operations is not guaranteed
        /// size may temporarily grow over cachesize
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void AddOrUpdate(TKey key, TValue value)
        {
            if (key==null) throw new ArgumentException("null key is not supported");

            var cachenode = this.cacheDictionary.GetOrAdd(key, createNewLockedNode(value));
            if (cachenode.List == null)
            {
                if (Monitor.IsEntered(cachenode.Value.lockobject))
                {
                    Logger.Info($"added new node to cache {cachenode.Value}");
                    cacheList.AddFirst(cachenode);
                    cachenode.Value.UnlockNode();
                } else // we can promote this node here
                {
                    cachenode.Value.cachedValue = value;
                    //Logger.Info($"ignoring just found node as its added by another thread with key {cachenode.Value}");
                }
            } else
            {
                Logger.Info($"updated value in cache key={key} value={value}");
                cachenode.Value.cachedValue = value;

                //this can be done in background also
                this.cacheList.promote(cachenode);
            }

            if (this.Count >= this.cacheSize + listSizeVariance)
            {
                while (this.Count > this.cacheSize)
                {
                    this.EvictLastUsed();
                }
            }
            //checkSize();
        }

        private Func<TKey, LinkedListNode<CacheNode<TKey, TValue>>> createNewLockedNode(TValue value)
        {
            return (key) =>
            {
                var newNode = CacheNode<TKey, TValue>.CreateLLNode(key, value);
                do
                {
                } while (!newNode.Value.TryLock());
                return newNode;
            };
        }

        private void EvictLastUsed()
        {
            var lastNode = this.cacheList.Last();
            if (lastNode != null)
            {
                Logger.Info("freeing up cache removing last used element " + lastNode.Value);
                LinkedListNode<CacheNode<TKey, TValue>> valueRemoved;
                var removed = false;
                do
                {
                    removed = this.cacheDictionary.TryRemove(lastNode.Value.key, out valueRemoved);
                    if (removed) break;
                    if (valueRemoved == null) break;
                    Logger.Warn($"tryremove from hashtable {lastNode.Value.key} removed {removed}");
                } while (true);
                if (valueRemoved != null)
                {
                    int retry = 0;
                    while (true) {
                        if (this.cacheList.Remove(valueRemoved))
                            break;
                        retry++;
                        if (retry % 10 == 0)
                            Logger.Warn($"Remove from list {lastNode.Value} failed tries:{retry}");
                        var lockedcurrent = valueRemoved.Value.TryLock();
                        //this could be because it got promoted, 
                        //we can remove another element in this case as well
                        //this could also be because another thread removed it
                        if (!lockedcurrent)
                            continue;
                        if (valueRemoved.List == null)
                            return;
                        valueRemoved.Value.UnlockNode();
                    }
                }
            } else
            {
                Logger.Warn("error cacheList.Last returned null");
            }
        }

        /// <summary>
        /// gets the element from cache with the given key, 
        /// the element is also marked as accessed so its removal will change accordingly.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            LinkedListNode<CacheNode<TKey, TValue>> valueNode;
            value = default(TValue);
            bool ret = this.cacheDictionary.TryGetValue(key, out valueNode);
            if (ret)
            {
                value = valueNode.Value.cachedValue;
                this.cacheList.promote(valueNode);
            }
            return ret;
        }

        internal void checkSize(int expectedSize)
        {
            //Debug.Assert(this.cacheDictionary.Count() == expectedSize);

            if (this.cacheList.Count <(uint)expectedSize && this.cacheList.Count > expectedSize+ listSizeVariance)
            {
                Logger.Warn($"cache list size :{this.cacheList.Count} expected{expectedSize}");
                var node = this.cacheList.First().Next;
                do
                {
                    Logger.Warn($"failed {node.Value}");
                    node = node.Next;
                } while (node.Next != null);
                throw new Exception($"cache list size:{this.cacheList.Count} expected{expectedSize}");
            }
        }
    }
}
