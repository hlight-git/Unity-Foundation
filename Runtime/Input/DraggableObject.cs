using System;
using UnityEngine;

namespace Hlight.Foundation
{
    public class DraggableObject : MonoBehaviour
    {
        public enum DragReleaseBehavior
        {
            StopImmediately,
            ReturnToOriginalPosition,
            ReturnToOriginalPositionImmediately,
            ContinueToLastTargetPosition
        }

        [SerializeField] private ATouchDetector touchDetector;
        [SerializeField] private AMover mover;
        [SerializeField] private DragReleaseBehavior releaseBehavior;
        [SerializeField] private bool centerWhenInteracting;

        private Vector3 originalLocalPosition;
        private Vector3 inputPositionOffset;

        public event Action<Vector3> DragBegan;
        public event Action<Vector3, Vector3> Dragged;
        public event Action<Vector3> DragEnded;

        public DragReleaseBehavior ReleaseBehavior
        {
            get => releaseBehavior;
            set => releaseBehavior = value;
        }

        public AMover Mover => mover;
        public Vector3 OriginalLocalPosition => originalLocalPosition;

        public ATouchDetector TouchDetector
        {
            get => touchDetector;
            set => touchDetector = value;
        }

        public virtual bool IsDragging { get; protected set; }

        protected virtual void Awake()
        {
            originalLocalPosition = transform.localPosition;
        }

        protected virtual void OnEnable()
        {
            if (touchDetector == null || mover == null)
                throw new InvalidOperationException($"'{name}' requires both a touch detector and a mover.");

            touchDetector.DragBegan += OnBeginDrag;
            touchDetector.Dragging += OnDragging;
            touchDetector.TouchEnded += OnEndTouch;
            touchDetector.enabled = true;
        }

        protected virtual void OnDisable()
        {
            if (touchDetector == null)
                return;

            touchDetector.enabled = false;
            touchDetector.DragBegan -= OnBeginDrag;
            touchDetector.Dragging -= OnDragging;
            touchDetector.TouchEnded -= OnEndTouch;
        }

        protected virtual void MoveWhenDragging(Vector3 worldPosition)
        {
            var targetPosition = worldPosition + (centerWhenInteracting ? Vector3.zero : inputPositionOffset);
            mover.MoveTo(targetPosition, Space.World);
        }

        protected virtual void OnBeginDrag(Vector3 worldPosition)
        {
            mover.ReachedDestination -= HandleDragCompleted;
            inputPositionOffset = transform.position - worldPosition;
            IsDragging = true;
            DragBegan?.Invoke(worldPosition);
        }

        protected virtual void OnDragging(Vector3 oldWorldPosition, Vector3 newWorldPosition)
        {
            MoveWhenDragging(newWorldPosition);
            Dragged?.Invoke(oldWorldPosition, newWorldPosition);
        }

        protected virtual void OnEndTouch(TouchState state, Vector3 worldPosition)
        {
            if (state != TouchState.Dragging)
                return;

            IsDragging = false;
            DragEnded?.Invoke(worldPosition);

            switch (releaseBehavior)
            {
                case DragReleaseBehavior.StopImmediately:
                    mover.Stop();
                    OnDragCompleted();
                    break;
                case DragReleaseBehavior.ReturnToOriginalPosition:
                    mover.ReachedDestination += HandleDragCompleted;
                    mover.MoveTo(originalLocalPosition, Space.Self);
                    break;
                case DragReleaseBehavior.ReturnToOriginalPositionImmediately:
                    mover.Stop();
                    transform.localPosition = originalLocalPosition;
                    OnDragCompleted();
                    break;
                case DragReleaseBehavior.ContinueToLastTargetPosition:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void HandleDragCompleted()
        {
            mover.ReachedDestination -= HandleDragCompleted;
            OnDragCompleted();
        }

        protected virtual void OnDragCompleted() { }
    }
}
