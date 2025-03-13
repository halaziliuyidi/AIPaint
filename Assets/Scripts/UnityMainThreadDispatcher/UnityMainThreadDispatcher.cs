using System;
using System.Collections.Generic;


namespace MFramework
{
    public class UnityMainThreadDispatcher : SingletonMonoBehaviour<UnityMainThreadDispatcher>
    {
        private static readonly Queue<Action> executionQueue = new Queue<Action>();

        public void Enqueue(Action action)
        {
            lock (executionQueue)
            {
                executionQueue.Enqueue(action);
            }
        }

        void Update()
        {
            lock (executionQueue)
            {
                while (executionQueue.Count > 0)
                {
                    executionQueue.Dequeue().Invoke();
                }
            }
        }
    }
}
