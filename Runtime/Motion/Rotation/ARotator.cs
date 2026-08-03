using System;
using UnityEngine;

namespace Hlight.Foundation
{
    public abstract class ARotator : MonoBehaviour
    {
        private Space space;
        private bool isRotating;

        public event Action<bool> IsRotatingChanged;
        public event Action ReachedDestination;

        public bool IsRotating => isRotating;

        protected Quaternion CurrentQuaternion
        {
            get => space switch
            {
                Space.World => transform.rotation,
                Space.Self => transform.localRotation,
                _ => throw new ArgumentOutOfRangeException()
            };
            set
            {
                switch (space)
                {
                    case Space.World:
                        transform.rotation = value;
                        break;
                    case Space.Self:
                        transform.localRotation = value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public Vector3 CurrentRotation
        {
            get => space switch
            {
                Space.World => transform.eulerAngles,
                Space.Self => transform.localEulerAngles,
                _ => throw new ArgumentOutOfRangeException()
            };
            protected set => CurrentQuaternion = Quaternion.Euler(value);
        }

        public virtual void RotateTo(Vector3 targetAngle, Space targetSpace)
        {
            space = targetSpace;
            SetIsRotating(true);
        }

        public virtual void Stop() => SetIsRotating(false);

        protected void CompleteRotation()
        {
            if (!isRotating)
                return;

            SetIsRotating(false);
            ReachedDestination?.Invoke();
        }

        private void SetIsRotating(bool value)
        {
            if (isRotating == value)
                return;

            isRotating = value;
            IsRotatingChanged?.Invoke(value);
        }
    }
}
