namespace LStudio.Operation
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class Operator
    {
        private Thread _thread;
        private AutoResetEvent _resetEvent;
        private ConcurrentQueue<WorkItem> _queueNormal;
        private ConcurrentQueue<WorkItem> _queueHigh;

        public Parameter Parameter { get; private set; }

        public event EventHandler<OperationTriggeredEventArgs> OperationTriggered;
        public event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;

        public Operator(Parameter parameter)
        {
            InitializeParameter(parameter);
            InitializeInternalObject();
            InitializeThread();

        }
        private void InitializeParameter(Parameter parameter)
        {
            Parameter = new Parameter()
            {
                Name = parameter.Name,
                Sleep = parameter.Sleep,
                Priority = parameter.Priority
            };
        }

        private void InitializeInternalObject()
        {
            _queueHigh = new ConcurrentQueue<WorkItem>();
            _queueNormal = new ConcurrentQueue<WorkItem>();
        }

        private void InitializeThread()
        {
            _resetEvent = new AutoResetEvent(false);
            _thread = new Thread(() =>
            {
                while (true)
                {
                    if (!_queueHigh.IsEmpty)
                    {
                        try
                        {
                            if (_queueHigh.TryDequeue(out WorkItem workItem))
                            {
                                var result = workItem.Callback?.Invoke(workItem.Material);
                                OnOperationTriggered(this, new OperationTriggeredEventArgs() { OperationResult = result });
                            }
                        }
                        catch (Exception ex)
                        {
                            OnErrorOccurred(this, new ErrorOccurredEventArgs() { Error = ex });
                        }
                    }
                    else if (!_queueNormal.IsEmpty)
                    {
                        try
                        {
                            if (_queueNormal.TryDequeue(out WorkItem workItem))
                            {
                                var result = workItem.Callback?.Invoke(workItem.Material);
                                OnOperationTriggered(this, new OperationTriggeredEventArgs() { OperationResult = result });
                            }
                        }
                        catch (Exception ex)
                        {
                            OnErrorOccurred(this, new ErrorOccurredEventArgs() { Error = ex });
                        }
                    }
                    else
                    {
                        _resetEvent.WaitOne();
                    }

                    _resetEvent.WaitOne(Parameter.Sleep);
                }
            })
            {
                IsBackground = true,
                Name = Parameter.Name,
                Priority = ThreadPriority.Normal
            };

            _thread.Start();
        }

        public void Enqueue(WorkItem workItem, Priority priority = Priority.Normal)
        {
            switch (priority)
            {
                case Priority.High:
                    _queueHigh?.Enqueue(workItem);
                    _resetEvent?.Set();
                    break;
                case Priority.Normal:
                    _queueNormal?.Enqueue(workItem);
                    _resetEvent?.Set();
                    break;
                default:
                    break;
            }
        }

        protected virtual void OnErrorOccurred(object sender, ErrorOccurredEventArgs args)
        {
            ErrorOccurred?.Invoke(sender, args);
        }

        protected virtual void OnOperationTriggered(object sender, OperationTriggeredEventArgs args)
        {
            OperationTriggered?.Invoke(sender, args);
        }
    }

    public class Parameter
    {
        public string Name { get; set; }
        public int Sleep { get; set; }

        public Priority Priority { get; set; }
    }

    public class WorkItem
    {
        public string TicketId { get; set; }
        public object[] Material { get; set; }
        public Func<object[], OperationResult> Callback { get; set; }
    }

    public class OperationTriggeredEventArgs : EventArgsBase
    {
        public OperationResult OperationResult { get; set; }
    }

    public class ErrorOccurredEventArgs : EventArgsBase
    {
        public Exception Error { get; set; }
    }

    public class EventArgsBase : EventArgs
    {
        public DateTime Time { get; }

        public EventArgsBase()
        {
            Time = DateTime.Now;
        }
    }

    public class OperationResult
    {
        public WorkItem WorkItem { get; set; }
        public Exception Error { get; set; }
        public bool HasError { get => Error != null; }
        public object Data { get; set; }
    }

    public enum Priority
    {
        Normal,
        High
    }
}
