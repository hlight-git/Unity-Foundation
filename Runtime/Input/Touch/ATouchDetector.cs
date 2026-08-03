using System;
using UnityEngine;

namespace Hlight.Foundation
{
    public abstract class ATouchDetector : MonoBehaviour
    {
        [SerializeField] private Camera cameraForInput;
        [SerializeField] private ATouchResponder touchResponder;

        public Camera CameraForInput
        {
            get => cameraForInput;
            set => cameraForInput = value;
        }

        public Collider2D Collider2D { get; protected set; }
        public abstract TouchState CurrentState { get; protected set; }

        public abstract event Action<Vector3> TouchBegan;
        public abstract event Action<Vector3> DragBegan;
        public abstract event Action<Vector3, Vector3> Dragging;
        public abstract event Action<TouchState, Vector3> TouchEnded;

        protected virtual void Awake()
        {
            Collider2D = GetComponent<Collider2D>();
        }

        protected virtual void OnEnable()
        {
            TouchBegan += TriggerResponseOnTouchBegin;
            TouchEnded += TriggerResponseOnTouchEnd;
        }

        protected virtual void OnDisable()
        {
            TouchBegan -= TriggerResponseOnTouchBegin;
            TouchEnded -= TriggerResponseOnTouchEnd;
        }

        protected Vector3 GetWorldPositionFromTouch(Vector3 screenPosition)
        {
            if (cameraForInput == null)
                throw new InvalidOperationException($"{nameof(CameraForInput)} is not assigned on '{name}'.");

            var depth = transform.position.z - cameraForInput.transform.position.z;
            return cameraForInput.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
        }

        private void TriggerResponseOnTouchBegin(Vector3 position)
        {
            if (touchResponder != null)
                touchResponder.OnTouchBegin(position);
        }

        private void TriggerResponseOnTouchEnd(TouchState state, Vector3 position)
        {
            if (touchResponder != null)
                touchResponder.OnTouchEnd(state, position);
        }
    }
}
