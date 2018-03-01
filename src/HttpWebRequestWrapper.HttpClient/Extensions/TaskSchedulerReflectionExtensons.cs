using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HttpWebRequestWrapper.HttpClient.Threading.Tasks;

namespace HttpWebRequestWrapper.HttpClient.Extensions
{
    /// <summary>
    /// Helper methods for using reflection to invoke
    /// protected methods on a <see cref="TaskScheduler"/>.
    /// <para />
    /// Helps <see cref="TaskSchedulerProxy"/> act as a 
    /// decorator.
    /// </summary>
    internal static class TaskSchedulerReflectionExtensons
    {
        private static readonly MethodInfo _getScheduledTasksMethod =
            typeof(TaskScheduler)
                .GetMethod(
                    "GetScheduledTasks",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly MethodInfo _queueTask =
            typeof(TaskScheduler)
                .GetMethod(
                    "QueueTask",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly MethodInfo _tryExecuteTaskInlineMethod =
            typeof(TaskScheduler)
                .GetMethod(
                    "TryExecuteTaskInline",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo _defaultTaskScheduleField =
            typeof(TaskScheduler)
                .GetField(
                    "s_defaultTaskScheduler",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);


        /// <summary>
        /// Uses reflection to execute the protected method
        /// <see cref="TaskScheduler.GetScheduledTasks"/>.
        /// </summary>
        public static IEnumerable<Task> GetScheduledTasks(this TaskScheduler scheduler)
        {
            return (IEnumerable<Task>)
                _getScheduledTasksMethod.Invoke(scheduler, new object[0]);
        }

        /// <summary>
        /// Uses reflection to execute the protected method
        /// <see cref="TaskScheduler.QueueTask"/>.
        /// </summary>
        public static void QueueTask(this TaskScheduler scheduler, Task task)
        {
            _queueTask.Invoke(
                scheduler,
                new object[]
                {
                    task
                });
        }

        /// <summary>
        /// Uses reflection to execute the protected method
        /// <see cref="TaskScheduler.TryExecuteTaskInline"/>.
        /// </summary>
        public static bool TryExecuteTaskInline(
            this TaskScheduler scheduler,
            Task task,
            bool taskWasPreviouslyQueued)
        {
            return (bool)
                _tryExecuteTaskInlineMethod.Invoke(
                    scheduler,
                    new object[]
                    {
                        task,
                        taskWasPreviouslyQueued
                    });
        }

        /// <summary>
        /// Uses reflection to set the
        /// static <see cref="TaskScheduler.Default"/> property
        /// </summary>
        public static void SetDefaultTaskScheduler(TaskScheduler scheduler)
        {
            _defaultTaskScheduleField.SetValue(null, scheduler);
        }

        /// <summary>
        /// Uses reflection to set the
        /// static <see cref="TaskScheduler.Default"/> property
        /// to <paramref name="scheduler"/>.
        /// </summary>
        public static void SetAsDefaultTaskScheduler(this TaskScheduler scheduler)
        {
            SetDefaultTaskScheduler(scheduler);
        }
    }
}