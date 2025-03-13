using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public static class WebRequestExtensions
{
    public static TaskAwaiter<UnityWebRequestAsyncOperation> GetAwaiter(this UnityWebRequestAsyncOperation asyncOp)
    {
        var taskCompletionSource = new TaskCompletionSource<UnityWebRequestAsyncOperation>();
        
        asyncOp.completed += operation => {
            taskCompletionSource.SetResult(asyncOp);
        };
        
        if (asyncOp.isDone)
        {
            taskCompletionSource.SetResult(asyncOp);
        }
        
        return taskCompletionSource.Task.GetAwaiter();
    }
}