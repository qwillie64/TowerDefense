using System.Text;
using CyberWizard.Gameplay.Towers;
using UnityEngine;

namespace CyberWizard.Presentation
{
    /// <summary>
    /// 玩家靠近固定防禦塔時，在塔旁邊（攝影機視角的左側或右側）顯示的 3D 升級提示：
    /// 列出三個部位的目前等級、升級後數值與花費，能量不足的花費以紅色顯示；
    /// 提示面板永遠面向攝影機，且畫在最上層不會被建築或塔擋住（塔的射程圈由 FixedTowerCombat 持續顯示，與本提示無關）。
    /// 文字使用內建 TextMesh 與 LegacyRuntime 字型（不需要 TextMeshPro）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerUpgradePrompt : MonoBehaviour
    {
        private const string AffordableColor = "#ffd75e";
        private const string UnaffordableColor = "#ff6b6b";
        private const string MutedColor = "#9aa0a6";
        private const string OverlayShaderName = "CyberWizard/WorldOverlay";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int UseVertexColorId = Shader.PropertyToID("_UseVertexColor");

        [SerializeField] private TowerUpgradeInteractor interactor;
        [SerializeField, Tooltip("面板背景（Quad），會依文字大小縮放。使用 CyberWizard/WorldOverlay 時，面板永遠畫在最上層，不會被建築擋住。")]
        private Material backgroundMaterial;

        [Header("版面（暫定值）")]
        [SerializeField, Min(0f), Tooltip("面板離塔外緣（朝玩家方向）的距離。")]
        private float sideOffset = 1.5f;

        [SerializeField, Tooltip("面板中心離塔底的高度。")]
        private float height = 4.5f;

        [SerializeField, Min(0.01f)] private float characterSize = 0.07f;
        [SerializeField, Min(8)] private int fontSize = 48;
        [SerializeField, Min(0f)] private float padding = 0.35f;
        [SerializeField, Min(0.1f), Tooltip("升級失敗訊息顯示秒數。")] private float feedbackDuration = 1.2f;

        private readonly StringBuilder _builder = new StringBuilder(256);
        private Transform _panel;
        private TextMesh _text;
        private MeshRenderer _textRenderer;
        private Material _textMaterial;
        private Font _font;
        private Transform _background;
        private Camera _camera;
        private FixedTowerCombat _shownTower;
        private string _lastText;
        private int _lastSignature;
        private int _fitFramesRemaining;
        private float _side = 1f;
        private TowerPart _feedbackPart;
        private string _feedbackMessage;
        private float _feedbackUntil;

        public bool IsVisible => _panel != null && _panel.gameObject.activeSelf;
        public FixedTowerCombat ShownTower => _shownTower;
        public string CurrentText => _lastText;

        private void Awake()
        {
            BuildPanel();
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (interactor != null)
            {
                interactor.UpgradeAttempted += HandleUpgradeAttempted;
            }
        }

        private void OnDisable()
        {
            if (interactor != null)
            {
                interactor.UpgradeAttempted -= HandleUpgradeAttempted;
            }
        }

        private void LateUpdate()
        {
            var tower = interactor != null ? interactor.CurrentTower : null;
            if (tower != _shownTower)
            {
                _shownTower = tower;
            }

            if (tower == null)
            {
                SetVisible(false);
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            SetVisible(true);
            if (_fitFramesRemaining > 0)
            {
                FitBackground();
                _fitFramesRemaining--;
            }

            UpdateText(tower);
            PlacePanel(tower);
        }

        private void PlacePanel(FixedTowerCombat tower)
        {
            // 面板放在塔的左側或右側（以攝影機視角而言），不擋住塔與玩家之間的視線；
            // 選靠近畫面中央的那一側，塔在畫面右半邊時放左側，反之放右側（有緩衝區避免來回切換）。
            var towerPosition = tower.transform.position;
            var right = _camera != null ? _camera.transform.right : Vector3.right;
            right.y = 0f;
            right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
            if (_camera != null)
            {
                var viewportX = _camera.WorldToViewportPoint(towerPosition).x;
                if (viewportX > 0.6f)
                {
                    _side = -1f;
                }
                else if (viewportX < 0.4f)
                {
                    _side = 1f;
                }
            }

            var halfWidth = _background != null ? _background.localScale.x * 0.5f : 0f;
            _panel.position = towerPosition +
                              right * (_side * (tower.FootprintRadius + sideOffset + halfWidth)) +
                              Vector3.up * height;
            if (_camera != null)
            {
                _panel.rotation = _camera.transform.rotation;
            }
        }

        private void UpdateText(FixedTowerCombat tower)
        {
            var balance = interactor.Wallet != null ? interactor.Wallet.Balance : 0;
            var feedbackActive = _feedbackMessage != null && Time.time < _feedbackUntil;

            // 內容只在狀態改變時重建，避免每幀配置字串。
            var signature = System.HashCode.Combine(
                tower.GetInstanceID(),
                tower.GetLevel(TowerPart.Turret) * 100 + tower.GetLevel(TowerPart.Body) * 10 + tower.GetLevel(TowerPart.Base),
                balance,
                Mathf.CeilToInt(tower.Health),
                Mathf.CeilToInt(tower.MaxHealth),
                feedbackActive ? (int)_feedbackPart + 1 : 0,
                feedbackActive ? _feedbackMessage : null);
            if (_lastText != null && signature == _lastSignature)
            {
                return;
            }

            _lastSignature = signature;
            _builder.Clear();
            _builder.Append("<b>").Append(tower.TowerNumber).Append("號塔</b>　血量 ")
                .Append(Mathf.CeilToInt(tower.Health)).Append('/').Append(Mathf.CeilToInt(tower.MaxHealth))
                .Append("　能量 ").Append(balance).Append('\n');

            var profile = tower.Profile;
            AppendPart(tower, profile, TowerPart.Turret, "1", "砲塔", balance);
            AppendPart(tower, profile, TowerPart.Body, "2", "塔身", balance);
            AppendPart(tower, profile, TowerPart.Base, "3", "塔底", balance);

            var text = _builder.ToString();
            if (text == _lastText)
            {
                return;
            }

            _lastText = text;
            _text.text = text;

            // TextMesh 的網格在設定文字後不會立刻更新範圍，延後到之後的幀再調整背景大小。
            _fitFramesRemaining = 2;
        }

        private void AppendPart(
            FixedTowerCombat tower,
            FixedTowerProfile profile,
            TowerPart part,
            string key,
            string label,
            int balance)
        {
            var level = tower.GetLevel(part);
            _builder.Append('\n').Append('[').Append(key).Append("] ").Append(label).Append(" Lv").Append(level);

            if (profile == null || !tower.TryGetUpgradeCost(part, out var cost))
            {
                _builder.Append("　<color=").Append(MutedColor).Append(">已滿級</color>");
            }
            else
            {
                var next = level + 1;
                _builder.Append("→").Append(next).Append("　");
                switch (part)
                {
                    case TowerPart.Turret:
                        _builder.Append("攻擊 ").Append(profile.GetDamage(level).ToString("0.#"))
                            .Append("→").Append(profile.GetDamage(next).ToString("0.#"))
                            .Append("　攻速 ").Append(profile.GetShotsPerSecond(level).ToString("0.0#"))
                            .Append("→").Append(profile.GetShotsPerSecond(next).ToString("0.0#"));
                        break;
                    case TowerPart.Body:
                        _builder.Append("血量上限 ").Append(profile.GetMaxHealth(level).ToString("0"))
                            .Append("→").Append(profile.GetMaxHealth(next).ToString("0"));
                        break;
                    default:
                        _builder.Append("射程 ").Append(profile.GetAttackRange(level).ToString("0.#"))
                            .Append("→").Append(profile.GetAttackRange(next).ToString("0.#"));
                        break;
                }

                _builder.Append("　<color=").Append(balance >= cost ? AffordableColor : UnaffordableColor)
                    .Append('>').Append(cost).Append(" 能量</color>");
            }

            if (_feedbackMessage != null && part == _feedbackPart && Time.time < _feedbackUntil)
            {
                _builder.Append("　<color=").Append(UnaffordableColor).Append(">").Append(_feedbackMessage).Append("</color>");
            }
        }

        private void HandleUpgradeAttempted(FixedTowerCombat tower, TowerPart part, TowerUpgradeResult result)
        {
            switch (result)
            {
                case TowerUpgradeResult.NotEnoughEnergy:
                    _feedbackMessage = "能量不足";
                    break;
                case TowerUpgradeResult.MaxLevel:
                    _feedbackMessage = "已滿級";
                    break;
                case TowerUpgradeResult.TowerDestroyed:
                    _feedbackMessage = "塔已損毀";
                    break;
                default:
                    _feedbackMessage = null;
                    return;
            }

            _feedbackPart = part;
            _feedbackUntil = Time.time + feedbackDuration;
        }

        private void BuildPanel()
        {
            _panel = new GameObject("Tower Upgrade Prompt Panel").transform;
            _panel.SetParent(transform, false);

            var textObject = new GameObject("Text");
            textObject.transform.SetParent(_panel, false);
            _text = textObject.AddComponent<TextMesh>();
            var font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.font = font;
            _text.fontSize = fontSize;
            _text.characterSize = characterSize;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Left;
            _text.richText = true;
            _text.color = Color.white;
            _textRenderer = textObject.GetComponent<MeshRenderer>();
            if (font != null)
            {
                _font = font;
                _textRenderer.sharedMaterial = CreateTextMaterial(font);
                Font.textureRebuilt += HandleFontTextureRebuilt;
            }

            _textRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _textRenderer.receiveShadows = false;

            var background = GameObject.CreatePrimitive(PrimitiveType.Quad);
            background.name = "Background";
            var backgroundCollider = background.GetComponent<Collider>();
            if (backgroundCollider != null)
            {
                Destroy(backgroundCollider);
            }

            background.transform.SetParent(_panel, false);
            var backgroundRenderer = background.GetComponent<MeshRenderer>();
            backgroundRenderer.sharedMaterial = backgroundMaterial;
            backgroundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            backgroundRenderer.receiveShadows = false;
            _background = background.transform;
        }

        /// <summary>
        /// 文字材質：背景材質使用 CyberWizard/WorldOverlay（永遠畫在最上層）時，以同一個 Shader 建立文字材質，
        /// 貼上字型貼圖並開啟頂點色（TextMesh 的 rich text 顏色）；佇列比背景高 1，確保文字畫在背景之上。
        /// 背景不是該 Shader 時沿用字型預設材質（會被建築擋住）。
        /// </summary>
        private Material CreateTextMaterial(Font font)
        {
            var overlayShader = backgroundMaterial != null ? backgroundMaterial.shader : null;
            if (overlayShader == null || !overlayShader.name.Equals(OverlayShaderName))
            {
                Debug.LogWarning($"TowerUpgradePrompt 的背景材質不是 {OverlayShaderName}，提示會被建築擋住。", this);
                return font.material;
            }

            _textMaterial = new Material(overlayShader) { name = "Tower Upgrade Prompt Text (Overlay)" };
            _textMaterial.SetColor(BaseColorId, Color.white);
            _textMaterial.SetFloat(UseVertexColorId, 1f);
            _textMaterial.SetTexture(MainTexId, font.material.mainTexture);
            _textMaterial.renderQueue = backgroundMaterial.renderQueue + 1;
            return _textMaterial;
        }

        private void HandleFontTextureRebuilt(Font font)
        {
            // 動態字型新增字元時可能重建貼圖，需要重新指定。
            if (font == _font && _textMaterial != null)
            {
                _textMaterial.SetTexture(MainTexId, font.material.mainTexture);
            }
        }

        private void OnDestroy()
        {
            Font.textureRebuilt -= HandleFontTextureRebuilt;
            if (_textMaterial != null)
            {
                Destroy(_textMaterial);
            }
        }

        private void FitBackground()
        {
            // localBounds 是文字網格在本身座標下的範圍；背景放在文字後方一點點。
            var bounds = _textRenderer.localBounds;
            _background.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0.05f);
            _background.localScale = new Vector3(
                bounds.size.x + padding * 2f,
                bounds.size.y + padding * 2f,
                1f);
        }

        private void SetVisible(bool visible)
        {
            if (_panel != null && _panel.gameObject.activeSelf != visible)
            {
                _panel.gameObject.SetActive(visible);
            }
        }
    }
}
