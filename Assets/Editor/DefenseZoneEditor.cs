using CyberWizard.Gameplay.Level;
using UnityEditor;
using UnityEngine;

namespace CyberWizard.Editor
{
    /// <summary>
    /// 防區的 Scene View 編輯工具：選取防區時，每個邊界頂點顯示一個可拖曳的控制點，
    /// 只能在防區本地的水平面（XZ）上移動，並標示頂點編號。修改支援 Undo。
    /// 新增或刪除頂點仍在 Inspector 的 boundary 清單中操作。
    /// </summary>
    [CustomEditor(typeof(DefenseZone))]
    public sealed class DefenseZoneEditor : UnityEditor.Editor
    {
        private static readonly Color HandleColor = new Color(0.4f, 0.8f, 1f, 1f);
        private const float HandleScreenSize = 0.08f;

        private void OnSceneGUI()
        {
            var zone = (DefenseZone)target;
            var zoneTransform = zone.transform;
            var boundary = zone.Boundary;

            using (new Handles.DrawingScope(HandleColor))
            {
                for (var index = 0; index < boundary.Count; index++)
                {
                    var local = boundary[index];
                    var world = zoneTransform.TransformPoint(new Vector3(local.x, 0f, local.y));
                    var size = HandleUtility.GetHandleSize(world) * HandleScreenSize;

                    Handles.Label(world + zoneTransform.up * size * 3f, index.ToString());

                    EditorGUI.BeginChangeCheck();
                    var moved = Handles.Slider2D(
                        world,
                        zoneTransform.up,
                        zoneTransform.right,
                        zoneTransform.forward,
                        size,
                        Handles.SphereHandleCap,
                        Vector2.zero);
                    if (!EditorGUI.EndChangeCheck())
                    {
                        continue;
                    }

                    var movedLocal = zoneTransform.InverseTransformPoint(moved);
                    Undo.RecordObject(zone, "移動防區頂點");
                    zone.SetBoundaryPoint(index, new Vector2(movedLocal.x, movedLocal.z));
                    PrefabUtility.RecordPrefabInstancePropertyModifications(zone);
                    EditorUtility.SetDirty(zone);
                }
            }
        }
    }
}
