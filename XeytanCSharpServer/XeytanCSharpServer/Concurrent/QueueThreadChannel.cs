using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetLib.Packets;

namespace XeytanCSharpServer.Concurrent
{
    class QueueThreadChannel<T> : ISingleThreadChannel<T>
    {
        private readonly BlockingCollection<T> queue = new BlockingCollection<T>(new ConcurrentQueue<T>());
        ManualResetEvent resetEvent = new ManualResetEvent(false);

        public T TakeSync()
        {
            lock (queue)
            {
                if (queue.Count > 0)
                {
                    var elem = queue.Take();
                    return elem;
                }
            }


            resetEvent.WaitOne();
            lock (queue)
            {
                var elem = queue.Take();
                return elem;
            }
        }


        public void SubmitSync(T value)
        {
            lock (queue)
            {
                queue.Add(value);
                resetEvent.Set();
                resetEvent.Reset();
            }
        }
    }
}