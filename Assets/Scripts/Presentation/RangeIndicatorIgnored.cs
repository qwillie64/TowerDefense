using UnityEngine;

namespace CyberWizard.Presentation
{
    /// <summary>
    /// 標記不作為射程圈投影地面的物件（例如房屋）。
    /// 掛在群組根物件上即可涵蓋所有子物件的碰撞體；<see cref="GroundRangeIndicator"/> 的 Raycast
    /// 會穿過這些碰撞體，改投影到下方的地面。碰撞本身不受影響。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RangeIndicatorIgnored : MonoBehaviour
    {
    }
}
