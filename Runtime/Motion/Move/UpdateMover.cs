using UnityEngine;

namespace Hlight.Foundation
{
    public class UpdateMover : AMover
    {
        private enum UpdateMethod { FixedUpdate, Update, LateUpdate }
        private enum MoveMethod { MoveTowards, Smooth }

        [SerializeField] private UpdateMethod updateMethod = UpdateMethod.Update;
        [SerializeField] private MoveMethod moveMethod = MoveMethod.Smooth;
        [SerializeField, Min(0.01f)] private float speed = 10f;

        private Vector3 targetPosition;

        protected virtual void FixedUpdate()
        {
            if (IsMoving && updateMethod == UpdateMethod.FixedUpdate)
                MoveStep(Time.fixedDeltaTime);
        }

        protected virtual void Update()
        {
            if (IsMoving && updateMethod == UpdateMethod.Update)
                MoveStep(Time.deltaTime);
        }

        protected virtual void LateUpdate()
        {
            if (IsMoving && updateMethod == UpdateMethod.LateUpdate)
                MoveStep(Time.deltaTime);
        }

        private void MoveStep(float deltaTime)
        {
            var currentPosition = CurrentPosition;
            if (currentPosition.ApproximatelyEquals(targetPosition))
            {
                CurrentPosition = targetPosition;
                CompleteMovement();
                return;
            }

            var nextPosition = moveMethod switch
            {
                MoveMethod.MoveTowards => Vector3.MoveTowards(currentPosition, targetPosition, deltaTime * speed),
                MoveMethod.Smooth => Vector3.Lerp(
                    currentPosition,
                    targetPosition,
                    1f - Mathf.Exp(-speed * deltaTime)),
                _ => currentPosition
            };

            if (nextPosition.ApproximatelyEquals(targetPosition))
            {
                CurrentPosition = targetPosition;
                CompleteMovement();
                return;
            }

            CurrentPosition = nextPosition;
        }

        public override void MoveTo(Vector3 destination, Space targetSpace)
        {
            targetPosition = destination;
            base.MoveTo(destination, targetSpace);
        }

        private void OnValidate() => speed = Mathf.Max(0.01f, speed);
    }
}
