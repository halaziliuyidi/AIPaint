using System;
using System.Threading.Tasks;

public static class TaskExtensions
{
    /// <summary>
    /// 扩展方法，等待Task执行完成后执行回调
    /// </summary>
    /// <typeparam name="T">Task的返回类型</typeparam>
    /// <param name="task">要执行的Task</param>
    /// <param name="callback">Task完成后的回调</param>
    /// <returns>包含回调后的Task</returns>
    public static async Task WithCallback<T>(this Task<T> task, Action<T> callback)
    {
        var result = await task;
        callback?.Invoke(result);
    }

    /// <summary>
    /// 扩展方法，等待Task执行完成后执行回调
    /// </summary>
    /// <param name="task">要执行的Task</param>
    /// <param name="callback">Task完成后的回调</param>
    /// <returns>包含回调后的Task</returns>
    public static async Task WithCallback(this Task task, Action callback)
    {
        await task;
        callback?.Invoke();
    }
}
