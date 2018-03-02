using System.Threading.Tasks;

namespace HttpWebRequestWrapper.HttpClient.Threading.Tasks
{
    /// <summary>
    /// Plugin for <see cref="TaskSchedulerProxy"/>.  Indicates
    /// a class wants to be called by <see cref="TaskSchedulerProxy.QueueTask"/>
    /// so it can inspect and possibly manipulate the <see cref="Task"/>
    /// before it is executed.
    /// </summary>
    internal interface IVisitTaskOnSchedulerQueue
    {
        /// <summary>
        /// Called by <see cref="TaskSchedulerProxy.QueueTask"/> before
        /// a <see cref="Task"/> is scheduled.  Allows  inspecting and 
        /// modifying <paramref name="task"/>.
        /// <para />
        /// <see cref="HttpClientHandlerStartRequestTaskVisitor"/> for an 
        /// example.
        /// </summary>
        void Visit(Task task);
    }
}