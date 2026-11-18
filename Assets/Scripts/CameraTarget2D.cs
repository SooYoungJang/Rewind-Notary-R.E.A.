using UnityEngine;

namespace RewindNotary
{
    [DefaultExecutionOrder(-200)]
    public sealed class CameraTarget2D : MonoBehaviour
    {
        public Transform Player;
        internal RepresentativeArea Area;
        private void LateUpdate()
        {
            var bounds = Area?.Room.cameraBounds;
            if (Player != null && bounds != null && bounds.Defined)
                transform.position = new Vector3(Mathf.Clamp(Player.position.x, bounds.x, bounds.x + bounds.width),
                    Mathf.Clamp(Player.position.y, bounds.y, bounds.y + bounds.height), 0f);
            else if (Player != null)
                transform.position = new Vector3(Mathf.Clamp(Player.position.x, 0f, 53f),
                    Mathf.Clamp(Player.position.y, 1.5f, 5.5f), 0f);
        }
        public void Snap() { LateUpdate(); }
    }
}
