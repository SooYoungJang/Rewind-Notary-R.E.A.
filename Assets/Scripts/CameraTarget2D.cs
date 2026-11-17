using UnityEngine;

namespace RewindNotary
{
    [DefaultExecutionOrder(-200)]
    public sealed class CameraTarget2D : MonoBehaviour
    {
        public Transform Player;
        private void LateUpdate()
        {
            if (Player != null)
                transform.position = new Vector3(Mathf.Clamp(Player.position.x, 0f, 53f),
                    Mathf.Clamp(Player.position.y, 1.5f, 5.5f), 0f);
        }
        public void Snap() { LateUpdate(); }
    }
}
