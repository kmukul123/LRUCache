using CacheLibrary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CacheLibrary
{
    /// <summary>
    /// internal class to provide the functionality of a ordered set 
    /// in order to implement the LRU
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class CacheList<T, V>
    {
        private readonly LinkedList<CacheNode<T, V>> list;
        private LinkedListNode<CacheNode<T, V>> FirstNode;
        private LinkedListNode<CacheNode<T, V>> LastNode;

        public CacheList(uint size)
        {
            list = new LinkedList<CacheNode<T, V>>();
            FirstNode = CacheNode<T, V>.CreateLLNode(default(T), default(V));
            list.AddFirst(FirstNode);
            LastNode = CacheNode<T, V>.CreateLLNode(default(T), default(V));
            list.AddLast(LastNode);
        }


        /// <summary>
        /// gets the last element from the cache which can be removed if needed
        /// </summary>
        /// <returns></returns>
        internal LinkedListNode<CacheNode<T, V>> Last()
        {
            if (list.Count>2) return list.Last.Previous;
            return null;
        }

        /// <summary>
        /// gets the last element from the cache which can be removed if needed
        /// </summary>
        /// <returns></returns>
        internal LinkedListNode<CacheNode<T, V>> First()
        {
            if (list.Count > 2) return list.First;
            return null;
        }

        internal uint Count => (uint)list.Count() - 2;

        
        /// <summary>
        /// thread safe method which adds the given node to the top of the list
        /// </summary>
        /// <param name="cachenode">node to be added, if the node is added by some other thread it returns</param>
        internal void AddFirst(LinkedListNode<CacheNode<T, V>> cachenode)
        {
            bool added = false;
            int retrycount = 0;
            do
            {
                bool firstlocktaken = false;
                bool nextlocktaken = false;
                bool curlocktaken = false;
                CacheNode<T,V> nextNode = null;
                try
                {
                    retrycount++;

                    firstlocktaken = FirstNode.Value.TryLock();
                    if (!firstlocktaken)
                        continue;
                    curlocktaken = cachenode.Value.TryLock();
                    if (!curlocktaken)
                        continue;
                    if (cachenode.List != null) return; //added by another thread
                    if (FirstNode.Next == cachenode) return; //no need for promotion
                    nextNode = FirstNode.Next.Value;
                    nextlocktaken = nextNode.TryLock();
                    if (!nextlocktaken)
                        continue;

                    this.list.AddAfter(FirstNode, cachenode);
                    added = true;
                } finally
                {
                    if (nextlocktaken) nextNode.UnlockNode();
                    if (curlocktaken) cachenode.Value.UnlockNode();
                    if (firstlocktaken) FirstNode.Value.UnlockNode();
                    if (retrycount % 100 == 0)
                        Logger.Info($"retryAddFirst {cachenode.Value.key} tries {retrycount}");
                    Thread.Sleep(0);
                }
            } while (!added);
        
        }


        internal bool TryLock(LinkedListNode<CacheNode<T, V>> cachenode, LinkedListNode<CacheNode<T, V>> prevNode, ArrayList locksTaken)
        {
            bool prevlockTaken = false;
            bool curlockTaken = false;
            bool nextlockTaken = false;
            bool locksucceeded = false;
            LinkedListNode<CacheNode<T, V>> nextNode=null;
            try
            {
                if (prevNode == null) return false;
                prevlockTaken = prevNode.Value.TryLock();
                if (!prevlockTaken) return false;
                if (prevNode.Next != cachenode) return false;

                curlockTaken = cachenode.Value.TryLock() ;
                if (!curlockTaken) return false;

                nextNode = cachenode.Next;
                
                nextlockTaken = nextNode.Value.TryLock();
                if (!nextlockTaken) return false;
                locksucceeded = true;
                locksTaken.Add(nextNode.Value);
                locksTaken.Add(cachenode.Value);
                locksTaken.Add(prevNode.Value);
            } finally
            {   if (!locksucceeded)
                {
                    if (nextlockTaken) nextNode.Value.UnlockNode();
                    if (curlockTaken) cachenode.Value.UnlockNode();
                    if (prevlockTaken) prevNode.Value.UnlockNode();
                }

            }
            return locksucceeded;
        }
        private void unlock(ArrayList locks)
        {
            (locks[0] as CacheNode<T, V>).UnlockNode();
            (locks[1] as CacheNode<T, V>).UnlockNode();
            (locks[2] as CacheNode<T, V>).UnlockNode();
        }

        internal void promote(LinkedListNode<CacheNode<T, V>> cachenode)
        {
            Debug.Assert(this.list.Count >= 2);
            if (FirstNode.Next == cachenode) return;

            
            if (this.Remove(cachenode)) 
                //the condition on this isnt required but will reduce the concurrency locks
                //its a good test to remove the if to test concucurrency of addfirst
                this.AddFirst(cachenode);
        }

        /// <summary>
        /// remove the given element from the given list the method is threadsafe
        /// it tries to lock the previous element in the list and the current and next before trying the operation
        /// it keeps retrying until successful
        /// </summary>
        /// <param name="valueRemoved"></param>
        /// <returns>if the element is removed by current call it returns true
        /// if the element is removed by some other thread it returns false
        /// </returns>
        internal bool Remove(LinkedListNode<CacheNode<T, V>> valueRemoved)
        {
            var trycount = 0;
            LinkedListNode<CacheNode<T, V>> prevNode = null;
            ArrayList locks = new ArrayList();
            do
            {
                bool lockedcurrent = false;
                try 
                {
                    lockedcurrent =valueRemoved.Value.TryLock();
                    if (!lockedcurrent)
                        continue;
                    if (valueRemoved.List == null)
                        return false;
                    prevNode = valueRemoved.Previous;
                }
                finally
                {
                    if (lockedcurrent) valueRemoved.Value.UnlockNode();
                }
                try 
                {
                    lockedcurrent = this.TryLock(valueRemoved, prevNode, locks);
                    if (lockedcurrent)
                    {
                        this.list.Remove(valueRemoved);
                        break;
                    }
                }
                finally
                {
                    if (lockedcurrent) this.unlock(locks);
                }
                trycount++;
                if (trycount %100==0)
                    Logger.Info($"retryremove {valueRemoved.Value.key} removed {trycount}");
                Thread.Sleep(0);
            } while (true);
            return true;
        }


    }
}
