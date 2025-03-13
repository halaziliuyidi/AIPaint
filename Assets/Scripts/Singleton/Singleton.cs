using System;

namespace MFramework
{
    public class Singleton<T> where T : class, new()
    {
        private static readonly Lazy<T> instance = new Lazy<T>(() => new T());

        public static T Instance
        {
            get
            {
                return instance.Value;
            }
        }

        // 私有构造函数以防止外部实例化
        protected Singleton() { }

        public virtual void Initialized()
        {
            
        }
    }
}