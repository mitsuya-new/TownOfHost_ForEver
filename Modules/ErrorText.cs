using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using static TownOfHostForE.Translator;

namespace TownOfHostForE
{
    public class ErrorText
    {
        public static ErrorText Instance => _instance;
        private static ErrorText _instance;

        public TMPro.TextMeshPro Text;
        private readonly Transform transform;
        private readonly GameObject gameObject;
        private readonly SpriteRenderer background;
        public Camera Camera;
        public List<ErrorData> AllErrors = new();
        public Vector3 TextOffset = new(0, TextOffsetY, -1000f);
        public bool HnSFlag;

        private const float TextOffsetY = 0.3f;

        private ErrorText(TMPro.TextMeshPro text, SpriteRenderer background)
        {
            Text = text;
            this.background = background;
            transform = text.transform;
            gameObject = text.gameObject;
        }

        public static void Create(TMPro.TextMeshPro baseText)
        {
            if (_instance?.Text != null) return;
            _instance = null;

            var text = Object.Instantiate(baseText);
            text.fontSizeMax = text.fontSizeMin = 2f;
            text.gameObject.name = "ErrorText";
            Object.DontDestroyOnLoad(text.gameObject);

            text.enabled = false;
            text.text = "NO ERROR";
            text.color = Color.red;
            text.outlineColor = Color.black;
            text.alignment = TMPro.TextAlignmentOptions.Top;

            var bgObject = new GameObject("Background") { layer = LayerMask.NameToLayer("UI") };
            var bgRenderer = bgObject.AddComponent<SpriteRenderer>();
            var bgTexture = new Texture2D(Screen.width, 150, TextureFormat.ARGB32, false);
            for (var x = 0; x < bgTexture.width; x++)
            {
                for (var y = 0; y < bgTexture.height; y++)
                {
                    bgTexture.SetPixel(x, y, new(0f, 0f, 0f, 0.6f));
                }
            }
            bgTexture.Apply();
            var bgSprite = Sprite.Create(bgTexture, new(0, 0, bgTexture.width, bgTexture.height), new(0.5f, 1f));
            bgRenderer.sprite = bgSprite;
            bgObject.transform.SetParent(text.transform, false);
            bgObject.transform.localPosition = new(0f, TextOffsetY, 1f);
            bgObject.SetActive(false);

            _instance = new ErrorText(text, bgRenderer);
        }

        public void Update()
        {
            if (Text == null || gameObject == null)
            {
                _instance = null;
                return;
            }

            AllErrors.ForEach(err => err.IncreaseTimer());
            var toRemove = AllErrors
                .Where(err => (err.Code is ErrorCode.OnPlayerLeftPostfixFailedInGame or ErrorCode.OnPlayerLeftPostfixFailedInLobby ||
                               err.ErrorLevel <= 1) && 10f < err.Timer)
                .ToArray();
            if (!toRemove.Any()) return;

            AllErrors.RemoveAll(err => toRemove.Contains(err));
            UpdateText();

            if (HnSFlag)
                DestroySelf();
        }

        public void LateUpdate()
        {
            if (Text == null || !Text.enabled) return;

            if (Camera == null)
                Camera = !HudManager.InstanceExists ? Camera.main : HudManager.Instance.PlayerCam.GetComponent<Camera>();

            if (Camera != null)
            {
                transform.position = AspectPosition.ComputeWorldPosition(Camera, AspectPosition.EdgeAlignments.Top, TextOffset);
            }
        }

        public void AddError(ErrorCode code)
        {
            var error = new ErrorData(code);
            if (0 < error.ErrorLevel)
                Logger.Error($"Error occurred: {error}: {error.Message}", "ErrorText");

            if (!AllErrors.Any(e => e.Code == code))
            {
                AllErrors.Add(error);
            }
            UpdateText();
        }

        public void UpdateText()
        {
            if (Text == null) return;

            var text = "";
            var maxLevel = 0;
            foreach (var err in AllErrors)
            {
                text += $"{err}: {err.Message}\n";
                if (maxLevel < err.ErrorLevel) maxLevel = err.ErrorLevel;
            }

            if (maxLevel == 0)
            {
                Hide();
            }
            else
            {
                if (!HnSFlag)
                    text += $"{GetString($"ErrorLevel{maxLevel}")}";
                Show();
            }

            if (GameStates.IsInGame && maxLevel != 3)
                text += $"\n{GetString("TerminateCommand")}: Shift+L+Enter";

            Text.text = text;
        }

        public void Clear()
        {
            AllErrors.RemoveAll(err => err.ErrorLevel != 3);
            UpdateText();
        }

        private void DestroySelf()
        {
            if (gameObject != null)
                Object.Destroy(gameObject);
            _instance = null;
        }

        private void Show()
        {
            if (Text != null)
                Text.enabled = true;
            if (background != null)
                background.gameObject.SetActive(true);
        }

        private void Hide()
        {
            if (Text != null)
                Text.enabled = false;
            if (background != null)
                background.gameObject.SetActive(false);
        }

        public class ErrorData
        {
            public readonly ErrorCode Code;
            public readonly int ErrorType1;
            public readonly int ErrorType2;
            public readonly int ErrorLevel;
            public float Timer { get; private set; }
            public string Message => GetString(ToString());

            public ErrorData(ErrorCode code)
            {
                Code = code;
                ErrorType1 = (int)code / 10000;
                ErrorType2 = (int)code / 10 - ErrorType1 * 1000;
                ErrorLevel = (int)code - (int)code / 10 * 10;
                Timer = 0f;
            }

            public override string ToString()
            {
                return $"ERR-{ErrorType1:000}-{ErrorType2:000}-{ErrorLevel:0}";
            }

            public void IncreaseTimer() => Timer += Time.deltaTime;
        }
    }

    public enum ErrorCode
    {
        Main_DictionaryError = 0010003,
        OptionIDDuplicate = 001_010_3,
        UnsupportedVersion = 002_000_1,
        OnPlayerLeftPostfixFailedInGame = 010_000_2,
        OnPlayerLeftPostfixFailedInLobby = 010_001_2,
        NoError = 0000000,
        TestError0 = 0009000,
        TestError1 = 0009101,
        TestError2 = 0009202,
        TestError3 = 0009303,
        HnsUnload = 000_804_1,
    }
}
