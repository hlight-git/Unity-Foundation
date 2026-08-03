#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Hlight.Foundation
{
    public static class UnityObjectExtensions
    {
        public static T Clone<T>(this T prototype, Transform parent = null) where T : Object
        {
            if (prototype == null) throw new System.ArgumentNullException(nameof(prototype));

#if UNITY_EDITOR
            if (!Application.isPlaying && PrefabUtility.IsPartOfRegularPrefab(prototype))
                return (T)PrefabUtility.InstantiatePrefab(prototype, parent);
#endif
            return parent ? Object.Instantiate(prototype, parent) : Object.Instantiate(prototype);
        }

        public static void DestroySafely(this Object target)
        {
            if (target == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(target, false);
                return;
            }
#endif
            Object.Destroy(target);
        }
    }
}
