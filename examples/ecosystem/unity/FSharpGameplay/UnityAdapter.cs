using ThinkingInFSharp.UnitySample;
using UnityEngine;

namespace ThinkingInFSharp.UnityHost
{
    public sealed class UnityAdapter : MonoBehaviour
    {
        [SerializeField, Min(0.0f)]
        private float speed = 6.0f;

        private MotionState state;
        private float horizontal;

        public void SetHorizontal(float value)
        {
            horizontal = Mathf.Clamp(value, -1.0f, 1.0f);
        }

        private void Awake()
        {
            state = Gameplay.Create(transform.position.x);
        }

        private void FixedUpdate()
        {
            state = Gameplay.Step(state, horizontal, speed, Time.fixedDeltaTime);

            Vector3 position = transform.position;
            transform.position = new Vector3(state.PositionX, position.y, position.z);
        }

        private void OnDisable()
        {
            horizontal = 0.0f;
        }

        private void OnValidate()
        {
            speed = Mathf.Max(0.0f, speed);
        }
    }
}
