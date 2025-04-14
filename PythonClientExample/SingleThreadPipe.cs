using System;
using System.Collections.Concurrent;
using System.Threading;

namespace PythonClientExample
{
    public class SingleThreadPipe : IDisposable
    {
        readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
        readonly Thread _thread;
        readonly AutoResetEvent _threadEvent = new AutoResetEvent(false);
        volatile bool _running = true;

        public SingleThreadPipe(string threadName = "SingleThreadPipe")
        {
            if (string.IsNullOrWhiteSpace(threadName)) throw new ArgumentException($"'{nameof(threadName)}' cannot be null or whitespace.", nameof(threadName));

            _thread = new Thread(Run) { Name = threadName };
            _thread.Start();
        }

        void Run()
        {
            while (_running)
            {
                if (_queue.TryDequeue(out Action action))
                    action();
                _threadEvent.WaitOne();
            }
        }

        public void AddAction(Action action)
        {
            if (action is null) throw new ArgumentNullException(nameof(action));

            _queue.Enqueue(action);
            _threadEvent.Set();
        }

        public void Dispose()
        {
            _running = false;
            _threadEvent.Set();
            _thread.Join();
            _threadEvent.Dispose();
        }
    }
}