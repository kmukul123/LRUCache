using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CacheLibrary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace CacheLibraryTest
{
    [TestClass]
    public class UnitTest1 
    {
        private LRUCache<string, string> cache;
        private Random rand = new Random();
        private void CreateCache(uint size=2)
        {
            this.cache = new LRUCache<string, string>(size);
        }
        [TestMethod]
        public void TestCornerCases()
        {
            try
            {
                CreateCache(0);
                throw new Exception("UnExpected exception");
            } catch (ArgumentException)
            {

            }
            try
            {
                CreateCache(1);
                this.cache.AddOrUpdate(null, "1");
                throw new Exception("UnExpected exception");
            }
            catch (ArgumentException)
            {

            }
            
            string onevalue;
            CreateCache(1);
            this.cache.AddOrUpdate("one", null);
            Assert.IsTrue(this.cache.TryGetValue("one", out onevalue));
            Assert.IsFalse(this.cache.TryGetValue("two", out onevalue));

            CreateCache(3);
            this.cache.AddOrUpdate("one", "1");
            Assert.AreEqual(this.cache.Count, 1U);
            this.cache.AddOrUpdate("two", "2");
            Assert.AreEqual(this.cache.Count, 2U);
            this.cache.AddOrUpdate("three", "3");
            Assert.AreEqual(this.cache.Count, 3U);
        }
        [TestMethod]
        public void TestEviction()
        {
            CreateCache(2);
            this.cache.AddOrUpdate("one", "1");
            Assert.AreEqual( this.cache.Count, 1U);
            this.cache.AddOrUpdate("two", "2");
            Assert.AreEqual(this.cache.Count, 2U);
            this.cache.AddOrUpdate("three", "3");
            Assert.AreEqual(this.cache.Count, 2U);
        }

        [TestMethod]
        public async Task QueryParallel()
        {
            CreateCache(2);
            var maxconcurrent = 100;
            var pendingTasks = new ConcurrentDictionary<string, Task>();
            bool faulted = false;
            for (int i = 0; i < 100000; i++)
            {
                var taskid = $"task_{i}";
                Assert.IsFalse(faulted);
                var task = new Task<string>(() =>
                {
                    try
                    {
                        Thread.CurrentThread.Name = taskid;
                        string oneValue, twoValue, threevalue;
                        this.cache.AddOrUpdate("one", getTestvalue("one"));
                        Assert.IsTrue(this.cache.TryGetValue("one", out oneValue));
                        this.cache.AddOrUpdate("two", getTestvalue("two"));
                        Assert.IsTrue(this.cache.TryGetValue("two", out twoValue));
                        Assert.IsFalse(this.cache.TryGetValue("three", out threevalue));
                        this.checkvalue("one", oneValue);
                        this.checkvalue("two", twoValue);
                        return taskid;
                    } catch (Exception ex)
                    {
                        faulted = false;
                        Logger.Warn($"{ex.ToString()}");
                        throw;
                    }
                });
                task.ContinueWith(t => { pendingTasks.Remove(taskid); });
                pendingTasks.Add(taskid, task);
                task.Start();
                if (i % 10000 == 0)
                    Logger.Info($"Ran {i} tasks");
                while (pendingTasks.Count > maxconcurrent)
                {
                    Logger.Warn($"tasks={pendingTasks.Count}");
                    await Task.Delay(100);
                }
            }
            while (pendingTasks.Count > 0)
            {
                Logger.Warn($"Waiting for tasks {pendingTasks.Count}");
                await Task.Delay(100);
            }
        }

        [TestMethod]
        public async Task InsertOrUpdateParallel()
        {
            CreateCache(2);
            var maxconcurrent = 100;
            var faulted = false;
            var pendingTasks = new ConcurrentDictionary<string, Task>();
            for (int i = 0; i < 500000; i++)
            {
                var taskid = $"task_{i}";
                Assert.IsFalse(faulted);
                var task = new Task<string>(() =>
                {
                    try
                    {
                        Thread.CurrentThread.Name = taskid;
                        string oneValue, twoValue, threeValue;
                        this.cache.AddOrUpdate("one", getTestvalue("one"));
                        if (this.cache.TryGetValue("one", out oneValue))
                            this.checkvalue("one", oneValue);

                        this.cache.AddOrUpdate("two", getTestvalue("two"));
                        if (this.cache.TryGetValue("two", out twoValue))
                            this.checkvalue("two", twoValue);

                        this.cache.AddOrUpdate("three", getTestvalue("three"));

                        if (this.cache.TryGetValue("three", out threeValue))
                            this.checkvalue("three", threeValue);
                        return taskid;
                    } catch (Exception ex)
                    {
                        faulted = true;
                        Logger.Warn($"{ex.ToString()}");
                        throw;
                    }
                });
                task.ContinueWith(t => pendingTasks.Remove(taskid)); 
                pendingTasks.Add(taskid, task);
                task.Start();
                while (pendingTasks.Count > maxconcurrent)
                {
                    Logger.Warn($"Waiting for tasks count={pendingTasks.Count}");
                    await Task.Delay(100);
                }
            }
            while (pendingTasks.Count > 0)
            {
                Logger.Warn($"Waiting for tasks count={pendingTasks.Count}");
                await Task.Delay(100);
            }
        }

        private void checkvalue(string key, string keyvalue)
        {
            char digit = getDigit(key);
            for (int i = 0; i < keyvalue.Length; i++)
            {
                if (keyvalue[i] != digit)
                    throw new Exception($"invalid value for {key} {keyvalue}");
            }
        }

        private static char getDigit(string key)
        {
            char digit;
            switch (key)
            {
                case "one":
                    digit = '1'; break;
                case "two":
                    digit = '2'; break;
                case "three":
                    digit = '3'; break;
                default: throw new ArgumentException(key);
            }

            return digit;
        }

        private string getTestvalue(string key)
        {
            char digit = getDigit(key);
           
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i< rand.Next(100); i++)
            {
                sb.Append(digit);
            }
            return sb.ToString();
        }
    }
}
