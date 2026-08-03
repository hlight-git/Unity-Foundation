using UnityEngine;

namespace Hlight.Foundation
{
    public abstract class ATouchResponder : MonoBehaviour
    {
        public abstract void OnTouchBegin(Vector3 position);
        public abstract void OnTouchEnd(TouchState state, Vector3 position);
    }
}
