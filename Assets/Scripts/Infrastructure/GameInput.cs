using UnityEngine;

namespace CyberWizard.Infrastructure
{
    /// <summary>
    /// 遊戲內所有鍵盤／滑鼠輸入的唯一入口。按鍵配置集中在這裡，控制器只讀取語意化的輸入，
    /// 不直接呼叫 UnityEngine.Input。目前底層仍是舊版 Input Manager（Project Settings 的
    /// Active Input Handling 未變更）；日後改用 Input System 時只需要修改本類別。
    /// </summary>
    public static class GameInput
    {
        private const string HorizontalAxis = "Horizontal";
        private const string VerticalAxis = "Vertical";
        private const string LookXAxis = "Mouse X";
        private const string LookYAxis = "Mouse Y";
        private const KeyCode JumpKey = KeyCode.Space;
        private const int AttackMouseButton = 0;

        private static readonly KeyCode[] UpgradeKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };
        private static readonly KeyCode[] UpgradeKeypadKeys = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3 };

        /// <summary>移動輸入（x = 左右、y = 前後），長度不超過 1。</summary>
        public static Vector2 Move => Vector2.ClampMagnitude(
            new Vector2(Input.GetAxisRaw(HorizontalAxis), Input.GetAxisRaw(VerticalAxis)),
            1f);

        /// <summary>本幀的滑鼠視角位移（未乘靈敏度）。</summary>
        public static Vector2 Look => new Vector2(Input.GetAxis(LookXAxis), Input.GetAxis(LookYAxis));

        public static bool JumpPressed => Input.GetKeyDown(JumpKey);

        public static bool AttackPressed => Input.GetMouseButtonDown(AttackMouseButton);

        /// <summary>滑鼠游標的螢幕座標。</summary>
        public static Vector3 PointerPosition => Input.mousePosition;

        /// <summary>升級欄位的數量（對應數字鍵 1 起算）。</summary>
        public static int UpgradeSlotCount => UpgradeKeys.Length;

        /// <summary>本幀是否按下第 slot 個升級鍵（0 起算：0 = 數字鍵 1）。</summary>
        public static bool UpgradeSlotPressed(int slot)
        {
            return slot >= 0 && slot < UpgradeKeys.Length &&
                   (Input.GetKeyDown(UpgradeKeys[slot]) || Input.GetKeyDown(UpgradeKeypadKeys[slot]));
        }
    }
}
