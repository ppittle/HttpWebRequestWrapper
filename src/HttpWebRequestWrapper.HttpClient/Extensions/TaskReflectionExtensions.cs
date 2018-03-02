using System.Reflection;
using System.Threading.Tasks;

namespace HttpWebRequestWrapper.HttpClient.Extensions
{
    internal static class TaskReflectionExtensions
    {
        private static readonly FieldInfo _actionFiled =
            typeof(Task).GetField(
                "m_action", 
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        /// <summary>
        /// Uses reflection to get <see cref="Task"/>
        /// <paramref name="t"/>'s <see cref="T:System.Threading.Tasks.Task.m_action"/> field.
        /// </summary>
        public static object GetAction(this Task t)
        {
            return _actionFiled.GetValue(t);
        }
    }
}