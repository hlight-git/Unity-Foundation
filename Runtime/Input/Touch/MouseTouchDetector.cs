using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hlight.Foundation
{
    public sealed class MouseTouchDetector : ATouchDetector
    {
        [SerializeField, Min(0f)] private float dragDistanceThreshold = 0.15f;

        private Vector3 lastTouchWorldPosition;
        private Vector3 startTouchWorldPosition;

        public override TouchState CurrentState { get; protected set; }
        public override event Action<Vector3> TouchBegan;
        public override event Action<Vector3> DragBegan;
        public override event Action<Vector3, Vector3> Dragging;
        public override event Action<TouchState, Vector3> TouchEnded;

        protected override void OnDisable()
        {
            if (CurrentState != TouchState.None)
                EndTouch(lastTouchWorldPosition);

            base.OnDisable();
        }

        private void OnMouseDown()
        {
            if (!enabled || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            CurrentState = TouchState.Touching;
            startTouchWorldPosition = GetWorldPositionFromTouch(Input.mousePosition);
            lastTouchWorldPosition = startTouchWorldPosition;
            TouchBegan?.Invoke(startTouchWorldPosition);
        }

        private void OnMouseDrag()
        {
            if (CurrentState == TouchState.None)
                return;

            var currentPosition = GetWorldPositionFromTouch(Input.mousePosition);
            if (CurrentState == TouchState.Touching)
            {
                if (Vector3.Distance(startTouchWorldPosition, currentPosition) < dragDistanceThreshold)
                    return;

                CurrentState = TouchState.Dragging;
                DragBegan?.Invoke(currentPosition);
            }

            if (!ApproximatelyEqual(lastTouchWorldPosition, currentPosition))
                Dragging?.Invoke(lastTouchWorldPosition, currentPosition);

            lastTouchWorldPosition = currentPosition;
        }

        private void OnMouseUp()
        {
            if (CurrentState == TouchState.None)
                return;

            EndTouch(GetWorldPositionFromTouch(Input.mousePosition));
        }

        private void EndTouch(Vector3 worldPosition)
        {
            if (CurrentState == TouchState.None)
                return;

            var completedState = CurrentState;
            CurrentState = TouchState.None;
            TouchEnded?.Invoke(completedState, worldPosition);
        }

        private static bool ApproximatelyEqual(Vector3 left, Vector3 right)
        {
            const float tolerance = 0.001f;
            var difference = left - right;
            return Mathf.Abs(difference.x) <= tolerance
                && Mathf.Abs(difference.y) <= tolerance
                && Mathf.Abs(difference.z) <= tolerance;
        }
    }
}
