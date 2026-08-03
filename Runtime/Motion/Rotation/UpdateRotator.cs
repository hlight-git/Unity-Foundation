using UnityEngine;

namespace Hlight.Foundation
{
    public class UpdateRotator : ARotator
    {
        private enum UpdateMethod { FixedUpdate, Update, LateUpdate }
        private enum RotateMethod { RotateTowards, Smooth }

        [SerializeField] private UpdateMethod updateMethod = UpdateMethod.Update;
        [SerializeField] private RotateMethod rotateMethod = RotateMethod.Smooth;
        [SerializeField, Min(0.01f)] private float speed = 10f;

        private Quaternion targetRotation;

        protected virtual void FixedUpdate()
        {
            if (IsRotating && updateMethod == UpdateMethod.FixedUpdate)
                RotateStep(Time.fixedDeltaTime);
        }

        protected virtual void Update()
        {
            if (IsRotating && updateMethod == UpdateMethod.Update)
                RotateStep(Time.deltaTime);
        }

        protected virtual void LateUpdate()
        {
            if (IsRotating && updateMethod == UpdateMethod.LateUpdate)
                RotateStep(Time.deltaTime);
        }

        private void RotateStep(float deltaTime)
        {
            var currentRotation = CurrentQuaternion;
            if (Quaternion.Angle(currentRotation, targetRotation) <= 0.01f)
            {
                CurrentQuaternion = targetRotation;
                CompleteRotation();
                return;
            }

            var nextRotation = rotateMethod switch
            {
                RotateMethod.RotateTowards => Quaternion.RotateTowards(
                    currentRotation,
                    targetRotation,
                    deltaTime * speed),
                RotateMethod.Smooth => Quaternion.Slerp(
                    currentRotation,
                    targetRotation,
                    1f - Mathf.Exp(-speed * deltaTime)),
                _ => currentRotation
            };

            if (Quaternion.Angle(nextRotation, targetRotation) <= 0.01f)
            {
                CurrentQuaternion = targetRotation;
                CompleteRotation();
                return;
            }

            CurrentQuaternion = nextRotation;
        }

        public override void RotateTo(Vector3 destinationAngle, Space targetSpace)
        {
            targetRotation = Quaternion.Euler(destinationAngle);
            base.RotateTo(destinationAngle, targetSpace);
        }

        private void OnValidate() => speed = Mathf.Max(0.01f, speed);
    }
}
