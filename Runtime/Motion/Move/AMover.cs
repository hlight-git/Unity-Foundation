using System;
using UnityEngine;

namespace Hlight.Foundation
{
    public abstract class AMover : MonoBehaviour
    {
        private Space space;
        private bool isMoving;

        public event Action<bool> IsMovingChanged;
        public event Action ReachedDestination;

        public bool IsMoving => isMoving;

        public Vector3 CurrentPosition
        {
            get => GetCurrentPosition(space);
            protected set
            {
                switch (space)
                {
                    case Space.World:
                        transform.position = value;
                        break;
                    case Space.Self:
                        transform.localPosition = value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public Vector3 GetCurrentPosition(Space targetSpace) => targetSpace switch
        {
            Space.World => transform.position,
            Space.Self => transform.localPosition,
            _ => throw new ArgumentOutOfRangeException(nameof(targetSpace), targetSpace, null)
        };

        public virtual void MoveTo(Vector3 targetPosition, Space targetSpace)
        {
            space = targetSpace;
            SetIsMoving(true);
        }

        public virtual void Stop() => SetIsMoving(false);

        protected void CompleteMovement()
        {
            if (!isMoving)
                return;

            SetIsMoving(false);
            ReachedDestination?.Invoke();
        }

        private void SetIsMoving(bool value)
        {
            if (isMoving == value)
                return;

            isMoving = value;
            IsMovingChanged?.Invoke(value);
        }
    }
}
