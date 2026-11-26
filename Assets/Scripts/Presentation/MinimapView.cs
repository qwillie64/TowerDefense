using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Waves;
using UnityEngine;

namespace CyberWizard.Presentation
{
    /// <summary>
    /// 把全圖俯視攝影機縮成畫面右上角的正方形小地圖，並在上面畫出玩家與敵人的位置點
    /// （角色在全圖比例下只有幾個像素，所以額外標示）。
    /// 取景（Orthographic Size、位置）仍由同物件上的 TopDownMapCamera 負責。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class MinimapView : MonoBehaviour
    {
        [SerializeField] private PrototypePlayerController player;
        [SerializeField] private WaveDirector waveDirector;

        [Header("版面")]
        [SerializeField, Range(0.1f, 0.6f), Tooltip("小地圖邊長占畫面高度的比例。")]
        private float sizeRatio = 0.32f;

        [SerializeField, Min(0f), Tooltip("與畫面右上角的距離（像素）。")]
        private float marginPixels = 12f;

        [SerializeField, Min(1f)] private float borderPixels = 2f;
        [SerializeField, Min(2f)] private float playerMarkerPixels = 9f;
        [SerializeField, Min(2f)] private float enemyMarkerPixels = 5f;
        [SerializeField] private Color borderColor = new Color(0.9f, 0.9f, 0.9f, 0.9f);
        [SerializeField] private Color playerColor = new Color(0.2f, 0.95f, 1f, 1f);
        [SerializeField] private Color enemyColor = new Color(1f, 0.2f, 0.15f, 1f);

        private Camera _camera;
        private int _screenWidth;
        private int _screenHeight;

        /// <summary>小地圖在螢幕上的像素範圍（左下角為原點）。</summary>
        public Rect PixelRect => _camera != null ? _camera.pixelRect : Rect.zero;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            ApplyViewport();
        }

        private void Update()
        {
            if (Screen.width != _screenWidth || Screen.height != _screenHeight)
            {
                ApplyViewport();
            }
        }

        /// <summary>依目前畫面大小把攝影機的 Viewport Rect 設為右上角的正方形。</summary>
        public void ApplyViewport()
        {
            if (_camera == null)
            {
                return;
            }

            _screenWidth = Mathf.Max(1, Screen.width);
            _screenHeight = Mathf.Max(1, Screen.height);
            var size = Mathf.Min(_screenHeight * sizeRatio, _screenWidth * 0.5f);
            _camera.rect = new Rect(
                1f - (size + marginPixels) / _screenWidth,
                1f - (size + marginPixels) / _screenHeight,
                size / _screenWidth,
                size / _screenHeight);
        }

        private void OnGUI()
        {
            if (_camera == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            var pixelRect = _camera.pixelRect;
            var guiRect = new Rect(
                pixelRect.x,
                Screen.height - pixelRect.yMax,
                pixelRect.width,
                pixelRect.height);
            DrawFrame(guiRect);

            if (waveDirector != null)
            {
                foreach (var enemy in waveDirector.AliveEnemies)
                {
                    if (enemy != null && enemy.IsAlive)
                    {
                        DrawMarker(enemy.transform.position, enemyMarkerPixels, enemyColor, guiRect);
                    }
                }
            }

            if (player != null && player.IsAlive)
            {
                DrawMarker(player.transform.position, playerMarkerPixels, playerColor, guiRect);
            }

            GUI.color = Color.white;
        }

        private void DrawFrame(Rect rect)
        {
            GUI.color = borderColor;
            var texture = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(rect.x - borderPixels, rect.y - borderPixels, rect.width + borderPixels * 2f, borderPixels), texture);
            GUI.DrawTexture(new Rect(rect.x - borderPixels, rect.yMax, rect.width + borderPixels * 2f, borderPixels), texture);
            GUI.DrawTexture(new Rect(rect.x - borderPixels, rect.y, borderPixels, rect.height), texture);
            GUI.DrawTexture(new Rect(rect.xMax, rect.y, borderPixels, rect.height), texture);
        }

        private void DrawMarker(Vector3 worldPosition, float sizePixels, Color color, Rect clip)
        {
            var screen = _camera.WorldToScreenPoint(worldPosition);
            var x = screen.x;
            var y = Screen.height - screen.y;
            if (!clip.Contains(new Vector2(x, y)))
            {
                return;
            }

            // 外框加深色描邊，讓點在任何地面顏色上都看得到。
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(x - sizePixels * 0.5f - 1f, y - sizePixels * 0.5f - 1f, sizePixels + 2f, sizePixels + 2f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(x - sizePixels * 0.5f, y - sizePixels * 0.5f, sizePixels, sizePixels), Texture2D.whiteTexture);
        }
    }
}
