using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

namespace MFramework
{

    /// <summary>
    /// 协程管理器 - 提供协程的生命周期管理、池化与回收功能
    /// </summary>
    public class CoroutineManager : SingletonMonoBehaviour<CoroutineManager>
    {
        public override void Initialized()
        {
            base.Initialized();
            Initialize();
        }

        #region 私有变量

        // 存储所有活动协程的字典，按类型分组
        private Dictionary<string, List<CoroutineHandle>> activeCoroutines = new Dictionary<string, List<CoroutineHandle>>();

        // 协程池 - 存储可重用的协程包装器对象
        private Dictionary<string, Queue<CoroutineWrapper>> coroutinePool = new Dictionary<string, Queue<CoroutineWrapper>>();

        // 最大池大小
        private int maxPoolSize = 20;

        // 调试模式
        private bool debugMode = false;

        #endregion

        #region 公共属性

        /// <summary>
        /// 协程池最大容量
        /// </summary>
        public int MaxPoolSize
        {
            get => maxPoolSize;
            set => maxPoolSize = Mathf.Max(1, value);
        }

        /// <summary>
        /// 启用或禁用调试日志
        /// </summary>
        public bool DebugMode
        {
            get => debugMode;
            set => debugMode = value;
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化协程管理器
        /// </summary>
        private void Initialize()
        {
            LogDebug("协程管理器已初始化");
        }

        #endregion

        #region 协程句柄类

        /// <summary>
        /// 协程句柄，用于跟踪和控制协程
        /// </summary>
        public class CoroutineHandle
        {
            // 内部协程引用
            private Coroutine coroutine;

            // 协程类型
            public string Type { get; private set; }

            // 是否正在运行
            public bool IsRunning { get; internal set; }

            // 创建时间
            public float StartTime { get; private set; }

            // 唯一ID
            public string Id { get; private set; }

            // 内部包装器引用
            internal CoroutineWrapper Wrapper { get; set; }

            /// <summary>
            /// 创建协程句柄
            /// </summary>
            public CoroutineHandle(Coroutine coroutine, string type)
            {
                this.coroutine = coroutine;
                this.Type = type;
                this.IsRunning = true;
                this.StartTime = Time.time;
                this.Id = Guid.NewGuid().ToString().Substring(0, 8);
            }

            /// <summary>
            /// 获取内部协程
            /// </summary>
            internal Coroutine GetCoroutine() => coroutine;

            /// <summary>
            /// 设置内部协程引用
            /// </summary>
            internal void SetCoroutine(Coroutine coroutine) => this.coroutine = coroutine;

            /// <summary>
            /// 获取协程运行时间
            /// </summary>
            public float GetElapsedTime() => Time.time - StartTime;
        }

        #endregion

        #region 协程包装器类

        /// <summary>
        /// 协程包装器，用于池化管理
        /// </summary>
        public class CoroutineWrapper
        {
            // 协程类型
            public string Type { get; private set; }

            // 当前使用此包装器的协程句柄
            public CoroutineHandle CurrentHandle { get; set; }

            // 是否正在使用
            public bool InUse { get; set; }

            // 创建时间
            public float CreationTime { get; private set; }

            // 包装的协程
            private IEnumerator _coroutine;

            // 完成回调
            private Action<bool> _onComplete;

            // 超时时间
            private float _timeout;

            // 是否使用超时
            private bool _useTimeout;

            // 取消源标记
            private CancellationTokenSource _cts;

            /// <summary>
            /// 创建协程包装器
            /// </summary>
            public CoroutineWrapper(string type)
            {
                this.Type = type;
                this.InUse = false;
                this.CreationTime = Time.time;
                this._cts = new CancellationTokenSource();
            }

            /// <summary>
            /// 初始化包装器以执行协程
            /// </summary>
            public void Initialize(IEnumerator coroutine, Action<bool> onComplete = null, float timeout = 0f)
            {
                this._coroutine = coroutine;
                this._onComplete = onComplete;
                this._timeout = timeout;
                this._useTimeout = timeout > 0f;

                // 重置取消标记
                if (this._cts != null)
                {
                    this._cts.Dispose();
                }
                this._cts = new CancellationTokenSource();

                this.InUse = true;
            }

            /// <summary>
            /// 获取要执行的包装协程
            /// </summary>
            public IEnumerator GetWrappedCoroutine()
            {
                return ExecuteCoroutineWithTimeout();
            }

            /// <summary>
            /// 使用超时和取消功能执行协程
            /// </summary>
            private IEnumerator ExecuteCoroutineWithTimeout()
            {
                if (_coroutine == null)
                {
                    yield break;
                }

                float startTime = Time.time;
                bool completed = false;
                bool cancelled = false;

                // 创建协程运行器，这样我们可以在外部控制它的执行
                // 注意：这里不使用 try-catch，因为 yield 不能在 try-catch 块内
                IEnumerator runner = RunOriginalCoroutine();

                // 开始执行原始协程
                while (true)
                {
                    // 检查是否被取消
                    if (_cts.IsCancellationRequested)
                    {
                        cancelled = true;
                        break;
                    }

                    // 检查是否超时
                    if (_useTimeout && (Time.time - startTime) > _timeout)
                    {
                        Debug.LogWarning($"协程执行超时 ({_timeout} 秒)");
                        break;
                    }

                    // 推进原协程
                    if (!runner.MoveNext())
                    {
                        completed = true;
                        break;
                    }

                    // 传递当前值
                    yield return runner.Current;
                }

                // 处理完成回调
                _onComplete?.Invoke(completed && !cancelled);

                // 标记为可回收
                Cleanup();
            }

            /// <summary>
            /// 运行原始协程，捕获异常
            /// </summary>
            private IEnumerator RunOriginalCoroutine()
            {
                bool hasError = false;
                object currentYieldInstruction = null;
                bool hasNext = true;

                // 使用 MoveNext 手动迭代协程，这样可以捕获异常
                while (hasNext)
                {
                    try
                    {
                        // 尝试推进协程，并保存当前的 yield 指令
                        hasNext = _coroutine.MoveNext();
                        if (hasNext)
                        {
                            currentYieldInstruction = _coroutine.Current;
                        }
                    }
                    catch (Exception e)
                    {
                        hasError = true;
                        Debug.LogError($"协程执行过程中发生错误: {e.Message}\n{e.StackTrace}");
                        break;
                    }

                    // 在 try-catch 块外返回当前指令
                    if (hasNext)
                    {
                        yield return currentYieldInstruction;
                    }
                }

                if (hasError)
                {
                    _onComplete?.Invoke(false);
                    Cleanup();
                }
            }

            /// <summary>
            /// 取消协程执行
            /// </summary>
            public void Cancel()
            {
                _cts?.Cancel();
            }

            /// <summary>
            /// 清理资源，准备回收
            /// </summary>
            public void Cleanup()
            {
                InUse = false;
                _coroutine = null;
                _onComplete = null;
                CurrentHandle = null;

                // 确保取消令牌被处理
                if (_cts != null)
                {
                    _cts.Dispose();
                    _cts = new CancellationTokenSource();
                }
            }

            /// <summary>
            /// 释放资源
            /// </summary>
            public void Dispose()
            {
                Cleanup();
                _cts?.Dispose();
                _cts = null;
            }
        }

        #endregion

        #region 公共方法 - 启动协程

        /// <summary>
        /// 启动协程并获取句柄
        /// </summary>
        /// <param name="coroutine">要启动的协程</param>
        /// <param name="type">协程类型标识</param>
        /// <param name="onComplete">完成回调</param>
        /// <param name="timeout">超时时间(秒)，0表示不超时</param>
        /// <returns>协程句柄，用于控制和停止协程</returns>
        public CoroutineHandle StartCoroutine(IEnumerator coroutine, string type, Action<bool> onComplete = null, float timeout = 0f)
        {
            if (coroutine == null)
            {
                Debug.LogError("尝试启动空协程");
                return null;
            }

            // 获取或创建协程包装器
            CoroutineWrapper wrapper = GetPooledWrapper(type);
            wrapper.Initialize(coroutine, onComplete, timeout);

            // 启动包装后的协程
            Coroutine handle = base.StartCoroutine(wrapper.GetWrappedCoroutine());

            // 创建并注册句柄
            CoroutineHandle coroutineHandle = new CoroutineHandle(handle, type);
            coroutineHandle.Wrapper = wrapper;
            wrapper.CurrentHandle = coroutineHandle;

            // 添加到活动协程列表
            RegisterCoroutineHandle(coroutineHandle);

            LogDebug($"启动协程: ID={coroutineHandle.Id}, Type={type}");

            return coroutineHandle;
        }

        /// <summary>
        /// 使用Action启动协程
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <param name="type">协程类型标识</param>
        /// <param name="onComplete">完成回调</param>
        /// <returns>协程句柄</returns>
        public CoroutineHandle StartCoroutine(Action action, string type, Action<bool> onComplete = null)
        {
            if (action == null)
            {
                Debug.LogError("尝试启动空操作");
                return null;
            }

            return StartCoroutine(ActionCoroutine(action), type, onComplete);
        }

        /// <summary>
        /// 将Action转换为IEnumerator
        /// </summary>
        private IEnumerator ActionCoroutine(Action action)
        {
            action?.Invoke();
            yield return null;
        }

        /// <summary>
        /// 延迟执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <param name="delay">延迟时间(秒)</param>
        /// <param name="type">协程类型标识</param>
        /// <returns>协程句柄</returns>
        public CoroutineHandle StartDelayedAction(Action action, float delay, string type = "Delayed")
        {
            return StartCoroutine(DelayedActionCoroutine(action, delay), type);
        }

        /// <summary>
        /// 延迟执行协程
        /// </summary>
        private IEnumerator DelayedActionCoroutine(Action action, float delay)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }

        #endregion

        #region 公共方法 - 停止协程

        /// <summary>
        /// 停止指定句柄的协程
        /// </summary>
        /// <param name="handle">协程句柄</param>
        public void StopCoroutine(CoroutineHandle handle)
        {
            if (handle == null || !handle.IsRunning)
            {
                return;
            }

            // 通知包装器取消执行
            handle.Wrapper?.Cancel();

            // 停止Unity协程
            if (handle.GetCoroutine() != null)
            {
                base.StopCoroutine(handle.GetCoroutine());
            }

            // 从活动列表中移除
            UnregisterCoroutineHandle(handle);

            // 标记为非运行状态
            handle.IsRunning = false;

            LogDebug($"停止协程: ID={handle.Id}, Type={handle.Type}");
        }

        /// <summary>
        /// 停止所有指定类型的协程
        /// </summary>
        /// <param name="type">协程类型</param>
        public void StopAllCoroutinesOfType(string type)
        {
            if (!activeCoroutines.ContainsKey(type))
            {
                return;
            }

            // 创建列表的副本，因为我们将修改原列表
            List<CoroutineHandle> coroutinesToStop = new List<CoroutineHandle>(activeCoroutines[type]);

            foreach (var handle in coroutinesToStop)
            {
                StopCoroutine(handle);
            }

            LogDebug($"已停止所有类型为 {type} 的协程: {coroutinesToStop.Count}个");
        }

        /// <summary>
        /// 停止所有协程
        /// </summary>
        public void StopAllCoroutines()
        {
            // 获取所有活动协程类型
            List<string> allTypes = new List<string>(activeCoroutines.Keys);

            foreach (var type in allTypes)
            {
                StopAllCoroutinesOfType(type);
            }

            // 确保所有协程都被停止
            base.StopAllCoroutines();

            // 清空所有活动协程记录
            activeCoroutines.Clear();

            LogDebug("已停止所有协程");
        }

        #endregion

        #region 协程池管理

        /// <summary>
        /// 从池中获取或创建协程包装器
        /// </summary>
        private CoroutineWrapper GetPooledWrapper(string type)
        {
            // 确保池中有该类型的队列
            if (!coroutinePool.ContainsKey(type))
            {
                coroutinePool[type] = new Queue<CoroutineWrapper>();
            }

            // 尝试从池中获取可用的包装器
            Queue<CoroutineWrapper> pool = coroutinePool[type];
            CoroutineWrapper wrapper = null;

            // 查找未使用的包装器
            while (pool.Count > 0)
            {
                wrapper = pool.Dequeue();
                if (wrapper != null && !wrapper.InUse)
                {
                    return wrapper;
                }
            }

            // 如果没有找到可用的，创建新的包装器
            wrapper = new CoroutineWrapper(type);
            LogDebug($"创建新的协程包装器: Type={type}");

            return wrapper;
        }

        /// <summary>
        /// 将协程包装器归还到池中
        /// </summary>
        private void ReturnWrapperToPool(CoroutineWrapper wrapper)
        {
            if (wrapper == null)
            {
                return;
            }

            // 清理包装器状态
            wrapper.Cleanup();

            string type = wrapper.Type;

            // 确保池中有该类型的队列
            if (!coroutinePool.ContainsKey(type))
            {
                coroutinePool[type] = new Queue<CoroutineWrapper>();
            }

            // 检查池大小
            if (coroutinePool[type].Count < maxPoolSize)
            {
                coroutinePool[type].Enqueue(wrapper);
                LogDebug($"协程包装器返回到池中: Type={type}, 当前池大小={coroutinePool[type].Count}");
            }
            else
            {
                // 池已满，直接释放资源
                wrapper.Dispose();
                LogDebug($"协程包装器被释放(池已满): Type={type}");
            }
        }

        /// <summary>
        /// 清空协程池
        /// </summary>
        public void ClearCoroutinePool()
        {
            // 销毁所有池中的包装器
            foreach (var queue in coroutinePool.Values)
            {
                foreach (var wrapper in queue)
                {
                    if (wrapper != null)
                    {
                        wrapper.Dispose();
                    }
                }
                queue.Clear();
            }

            coroutinePool.Clear();
            LogDebug("协程池已清空");
        }

        #endregion

        #region 协程跟踪与统计

        /// <summary>
        /// 注册协程句柄
        /// </summary>
        private void RegisterCoroutineHandle(CoroutineHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            string type = handle.Type;

            // 确保字典中有该类型的列表
            if (!activeCoroutines.ContainsKey(type))
            {
                activeCoroutines[type] = new List<CoroutineHandle>();
            }

            // 添加到活动列表
            activeCoroutines[type].Add(handle);
        }

        /// <summary>
        /// 注销协程句柄
        /// </summary>
        private void UnregisterCoroutineHandle(CoroutineHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            string type = handle.Type;

            if (activeCoroutines.ContainsKey(type))
            {
                // 从活动列表中移除
                activeCoroutines[type].Remove(handle);

                // 如果列表为空，移除该类型键
                if (activeCoroutines[type].Count == 0)
                {
                    activeCoroutines.Remove(type);
                }
            }

            // 如果有包装器，返回到池中
            if (handle.Wrapper != null)
            {
                ReturnWrapperToPool(handle.Wrapper);
            }
        }

        /// <summary>
        /// 获取指定类型的活动协程数量
        /// </summary>
        public int GetActiveCoroutineCount(string type)
        {
            if (!activeCoroutines.ContainsKey(type))
            {
                return 0;
            }

            // 清理列表中的已停止协程
            activeCoroutines[type].RemoveAll(h => h == null || !h.IsRunning);
            return activeCoroutines[type].Count;
        }

        /// <summary>
        /// 获取所有活动协程的总数
        /// </summary>
        public int GetTotalActiveCoroutineCount()
        {
            int total = 0;

            // 遍历所有类型，统计总数
            foreach (var type in activeCoroutines.Keys.ToList())
            {
                // 清理列表中的已停止协程
                activeCoroutines[type].RemoveAll(h => h == null || !h.IsRunning);
                total += activeCoroutines[type].Count;
            }

            return total;
        }

        /// <summary>
        /// 输出当前协程统计信息
        /// </summary>
        public void LogCoroutineStats()
        {
            StringBuilder stats = new StringBuilder("协程管理器统计信息:\n");
            int totalActive = 0;

            // 统计活动协程
            stats.AppendLine("活动协程:");
            foreach (var type in activeCoroutines.Keys.ToList())
            {
                // 清理列表中的已停止协程
                activeCoroutines[type].RemoveAll(h => h == null || !h.IsRunning);
                int count = activeCoroutines[type].Count;
                totalActive += count;

                if (count > 0)
                {
                    stats.AppendLine($"  - {type}: {count}个");

                    // 在调试模式下显示每个协程的详细信息
                    if (debugMode)
                    {
                        foreach (var handle in activeCoroutines[type])
                        {
                            stats.AppendLine($"    * ID={handle.Id}, 运行时间={handle.GetElapsedTime():F1}秒");
                        }
                    }
                }
            }
            stats.AppendLine($"总活动协程: {totalActive}个");

            // 统计协程池
            int totalPooled = 0;
            stats.AppendLine("协程池:");
            foreach (var type in coroutinePool.Keys)
            {
                int count = coroutinePool[type].Count;
                totalPooled += count;

                if (count > 0)
                {
                    stats.AppendLine($"  - {type}: {count}个");
                }
            }
            stats.AppendLine($"总池化协程包装器: {totalPooled}个");

            Debug.Log(stats.ToString());
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 输出调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (debugMode)
            {
                Debug.Log($"[CoroutineManager] {message}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void OnDestroy()
        {
            // 停止所有协程
            StopAllCoroutines();

            // 清空协程池
            ClearCoroutinePool();

            Debug.Log("协程管理器已销毁");
        }

        #endregion
    }
}