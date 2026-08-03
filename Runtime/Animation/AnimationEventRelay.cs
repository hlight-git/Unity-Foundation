using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace Hlight.Foundation
{
    [AddComponentMenu("Hlight/Foundation/Animation Event Relay")]
    public sealed class AnimationEventRelay : MonoBehaviour
    {
        public event Action<string> Notified;

        [Preserve]
        public void Notify(string eventName)
        {
            Notified?.Invoke(eventName);
        }
    }
}
