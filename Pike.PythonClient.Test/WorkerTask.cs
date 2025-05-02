using System;
using System.Threading;

namespace Pike.PythonClient.Test
{
    public class WorkerTask: IDisposable
    {
        private readonly Thread _worker;
        private Action _action;
        private volatile bool _running = true;
        private readonly AutoResetEvent _currentThreadEvent = new AutoResetEvent(false);
        private readonly AutoResetEvent _workerThreadEvent = new AutoResetEvent(false);
        private readonly object _locker = new object();

        public WorkerTask(string threadName = nameof(WorkerTask))
        {
            if (string.IsNullOrWhiteSpace(threadName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(threadName));

            _worker = new Thread(Run) { Name = threadName, IsBackground = true};
            _worker.Start();
        }

        private void Run()
        {
            while (_running)
            {
                _action?.Invoke();
                _currentThreadEvent.Set();
                _workerThreadEvent.WaitOne();
            }
        }

        public void RunAction(Action action)
        {
            lock (_locker)
            {
                _action = action ?? throw new ArgumentNullException(nameof(action));
                _workerThreadEvent.Set();
                _currentThreadEvent.WaitOne();
            }
        }

        #region IDisposable

        public void Dispose()
        {
            _running = false;
            _workerThreadEvent.Set();
            _worker.Join();

            _currentThreadEvent?.Dispose();
            _workerThreadEvent?.Dispose();
        }

        #endregion
    }
}
