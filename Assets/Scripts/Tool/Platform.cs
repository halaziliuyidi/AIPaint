
using UnityEngine;

namespace MFramework
{
    public static class Platform
    {
        public static bool IsEditor
        {
            get
            {
#if UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsDebugBuild
        {
            get
            {
                return Debug.isDebugBuild;
            }
        }
    }
}
