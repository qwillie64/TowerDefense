using UnityEngine;

namespace CyberWizard.Gameplay.Enemies
{
    public sealed class EnemyPath : MonoBehaviour
    {
        [SerializeField] private string pathId = "path_main";
        [SerializeField] private Transform[] waypoints;

        public string PathId => pathId;
        public Transform[] Waypoints => waypoints;

        public void Configure(string newPathId, params Transform[] newWaypoints)
        {
            pathId = newPathId;
            waypoints = newWaypoints;
        }

        private void OnDrawGizmos()
        {
            // 在 Scene View 顯示路徑，方便檢查與調整路徑點。
            if (waypoints == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.35f, 0.2f);
            for (var index = 0; index < waypoints.Length; index++)
            {
                if (waypoints[index] == null)
                {
                    continue;
                }

                Gizmos.DrawSphere(waypoints[index].position, 0.8f);
                if (index > 0 && waypoints[index - 1] != null)
                {
                    Gizmos.DrawLine(waypoints[index - 1].position, waypoints[index].position);
                }
            }
        }
    }
}
