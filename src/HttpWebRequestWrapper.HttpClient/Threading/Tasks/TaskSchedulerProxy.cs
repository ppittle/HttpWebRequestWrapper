using System.Collections.Generic;
using System.Threading.Tasks;
using HttpWebRequestWrapper.HttpClient.Extensions;

namespace HttpWebRequestWrapper.HttpClient.Threading.Tasks
{
    /// <summary>
    /// Black Magic.  Intercepts the scheduling of all Tasks
    /// so the passed <see cref="IVisitTaskOnSchedulerQueue"/> can 
    /// inspect them and potentially modify them, like 
    /// changing thier <see cref="Task.AsyncState"/>.
    /// <para />
    /// <see cref="HttpClientHandlerStartRequestTaskVisitor"/> for an 
    /// example and more informaton.
    /// </summary>
    internal class TaskSchedulerProxy : TaskScheduler
    {
        private readonly IVisitTaskOnSchedulerQueue _taskVisitor;
        private readonly TaskScheduler _inner;

        public TaskSchedulerProxy(IVisitTaskOnSchedulerQueue taskVisitor, TaskScheduler inner)
        {
            _taskVisitor = taskVisitor;
            _inner = inner;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return _inner.GetScheduledTasks();
        }

        protected override void QueueTask(Task task)
        {
            _taskVisitor.Visit(task);   

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