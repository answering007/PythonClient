using System;
using System.Threading;

namespace PythonClientExample
{
    public class ThreadRunner : IDisposable
    {
        readonly Thread _thread;
        readonly AutoResetEvent _threadEvent = new AutoResetEvent(false);
        readonly AutoResetEvent _externalEvent = new AutoResetEvent(false);
        volatile bool _running = true;

        public ThreadRunner(string threadName = "ThreadRunner")
        {
            if (string.IsNullOrWhiteSpace(threadName)) throw new ArgumentException($"'{nameof(threadName)}' cannot be null or whitespace.", nameof(threadName));

            _thread = new Thread(Run) { Name = threadName };
            _thread.Start();
        }

        void Run()
        {
            while(_running)
            {
                _threadEvent.WaitOne();
                if (_doWork != null) _doWork();
                _externalEvent.Set();
            }
        }

        Action _doWork;
        public void DoWork(Action action)
        {
            _doWork = action;
            _threadEvent.Set();
            _externalEvent.WaitOne();
        }

        public void Dispose()
        {
            _running = false;
            DoWork(null);
            _thread.Join();
            _threadEvent.Dispose();
            _externalEvent.Dispose();
        }
    }
}