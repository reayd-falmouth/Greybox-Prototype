using System;
using System.Collections;
using System.Threading.Tasks;

namespace Runtime.RMC.Backgammon.Core
{
    /// <summary>
    /// Task-to-Coroutine adapter utilities.
    /// Pattern from MoneySession project - prevents blocking Unity's main thread.
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>
        /// Converts a Task&lt;T&gt; into a coroutine that yields control each frame until complete.
        /// </summary>
        public static IEnumerator AsCoroutine<T>(this Task<T> task, Action<T> callback)
        {
            while (!task.IsCompleted)
                yield return null;

            if (task.Exception != null)
            {
                throw task.Exception;
            }

            callback?.Invoke(task.Result);
        }

        /// <summary>
        /// Non-generic version for Task (no return value).
        /// </summary>
        public static IEnumerator AsCoroutine(this Task task, Action callback = null)
        {
            while (!task.IsCompleted)
                yield return null;

            if (task.Exception != null)
            {
                throw task.Exception;
            }

            callback?.Invoke();
        }
    }
}
