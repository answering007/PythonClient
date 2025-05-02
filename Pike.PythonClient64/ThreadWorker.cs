using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Pike.PythonClient64
{
    public class ThreadWorker : IDisposable
    {
        readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
        readonly Thread _thread;
        readonly AutoResetEvent _threadEvent = new AutoResetEvent(false);
        volatile bool _running = true;
        volatile bool _disposed;

        public ThreadWorker(string threadName = "ThreadWorker")
        {
            if (string.IsNullOrWhiteSpace(threadName)) throw new ArgumentException($"'{nameof(threadName)}' cannot be null or whitespace.", nameof(threadName));

            _thread = new Thread(Run) { Name = threadName };
            _thread.Start();
        }

        public string ThreadName => _thread.Name;

        public int ThreadId => _thread.ManagedThreadId;

        void Run()
        {
            while (_running)
            {
                if (_queue.TryDequeue(out var action))
                    action();
                _threadEvent.WaitOne();
            }
        }

        public void AddAction(Action action)
        {
            if (action is null) throw new ArgumentNullException(nameof(action));
            if (_disposed) throw new ObjectDisposedException(nameof(ThreadWorker));

            _queue.Enqueue(action);
            _threadEvent.Set();
        }

        public void Dispose()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ThreadWorker));

            _running = false;
            _threadEvent.Set();
            _thread.Join();
            _threadEvent.Dispose();
            _disposed = true;
        }
    }
}