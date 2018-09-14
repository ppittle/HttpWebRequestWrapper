using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HttpWebRequestWrapper.Extensions;

namespace HttpWebRequestWrapper.Threading.Tasks
{
    /// <summary>
    /// Black Magic.  Intercepts the scheduling of all Tasks
    /// so the passed <see cref="IVisitTaskOnSchedulerQueue"/> can 
    /// inspect them and potentially modify them, like 
    /// changing their <see cref="Task.AsyncState"/>.
    /// <para />
    /// <see cref="HttpClientHandlerStartRequestTaskVisitor"/> for an 
    /// example and more information.
    /// </summary>
    internal class TaskSchedulerProxy : TaskScheduler
    {
        private readonly List<IVisitTaskOnSchedulerQueue> _taskVisitor;
        private readonly TaskScheduler _inner;

        public TaskSchedulerProxy(IVisitTaskOnSchedulerQueue taskVisitor, TaskScheduler inner)
            : this (new []{ taskVisitor}, inner){}

        public TaskSchedulerProxy(IEnumerable<IVisitTaskOnSchedulerQueue> taskVisitors, TaskScheduler inner)
        {
            _taskVisitor = taskVisitors?.ToList() ?? new List<IVisitTaskOnSchedulerQueue>();
            _inner = inner;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return _inner.GetScheduledTasks();
        }

        protected override void QueueTask(Task task)
        {
            foreach(var visitor in _taskVisitor)
                visitor.Visit(task);   

            _inner.QueueTask(task);
        }

        protected override bool TryDequeue(Task task)
        {
            return _inner.TryDequeue(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return _inner.TryExecuteTaskInline(task, taskWasPreviouslyQueued);
        }
    }
}