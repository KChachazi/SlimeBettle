using SlimeBattle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBattle.UI
{
    /// <summary>A small uGUI view. All buttons submit public flow commands.</summary>
    public sealed class BattleHudView : MonoBehaviour
    {
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text objectiveText;
        [SerializeField] private Text countsText;
        [SerializeField] private Text hintText;
        [SerializeField] private Text resultText;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseRestartButton;
        [SerializeField] private Button resultRestartButton;
        private UISystem owner;

        public void Bind(UISystem uiSystem)
        {
            Unbind();
            owner = uiSystem;
            if (isActiveAndEnabled) { AddListeners(); }
        }

        private void AddListeners()
        {
            RemoveListeners();
            pauseButton?.onClick.AddListener(Pause);
            resumeButton?.onClick.AddListener(Resume);
            pauseRestartButton?.onClick.AddListener(Restart);
            resultRestartButton?.onClick.AddListener(Restart);
        }

        public void Unbind()
        {
            RemoveListeners();
            owner = null;
        }

        private void RemoveListeners()
        {
            pauseButton?.onClick.RemoveListener(Pause);
            resumeButton?.onClick.RemoveListener(Resume);
            pauseRestartButton?.onClick.RemoveListener(Restart);
            resultRestartButton?.onClick.RemoveListener(Restart);
        }

        public void Render(UiState state, bool showHud, bool showPause, bool showResult, bool isPending)
        {
            if (hudPanel != null) { hudPanel.SetActive(showHud); }
            if (pausePanel != null) { pausePanel.SetActive(showPause); }
            if (resultPanel != null) { resultPanel.SetActive(showResult); }
            if (objectiveText != null) { objectiveText.text = state.ObjectiveText; }
            if (countsText != null)
            {
                countsText.text = "SLIMES  " + Count(state.SlimeCount) + "    ENEMIES  " + Count(state.EnemyCount) +
                    "    /    " + state.LevelState;
            }
            if (hintText != null) { hintText.text = state.Hint; }
            if (resultText != null)
            {
                resultText.text = (state.LevelState == LevelState.Succeeded ? "LEVEL COMPLETE" : "LEVEL FAILED") +
                    (string.IsNullOrEmpty(state.ResultText) ? string.Empty : "\n\n" + state.ResultText);
            }
            if (pauseButton != null) { pauseButton.interactable = state.LevelState == LevelState.Running && !isPending; }
            if (resumeButton != null) { resumeButton.interactable = state.LevelState == LevelState.Paused && !isPending; }
            if (pauseRestartButton != null) { pauseRestartButton.interactable = !isPending; }
            if (resultRestartButton != null) { resultRestartButton.interactable = !isPending; }
        }

        public static BattleHudView Create(Transform parent, Font font = null)
        {
            var root = new GameObject("BattleHUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(BattleHudView));
            root.transform.SetParent(parent, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 20;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            var view = root.GetComponent<BattleHudView>();
            font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var hud = Panel("HUD", root.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -150f), Vector2.zero, new Color(0.035f, 0.065f, 0.11f, 0.96f));
            view.hudPanel = hud.gameObject;
            Label("Title", hud, font, "SLIME BATTLE  /  P0", new Vector2(24f, -16f), new Vector2(800f, 28f), 20,
                new Color(0.4f, 0.94f, 0.77f));
            view.objectiveText = Label("Objective", hud, font, "", new Vector2(24f, -52f), new Vector2(950f, 28f), 22, Color.white);
            view.countsText = Label("Counts", hud, font, "", new Vector2(24f, -88f), new Vector2(950f, 22f), 16,
                new Color(0.6f, 0.72f, 0.8f));
            view.hintText = Label("Hint", hud, font, "", new Vector2(24f, -118f), new Vector2(1080f, 22f), 15,
                new Color(0.7f, 0.78f, 0.84f));
            view.pauseButton = ActionButton("Pause", hud, font, new Vector2(-100f, -32f), new Vector2(150f, 44f));
            var pauseRect = view.pauseButton.GetComponent<RectTransform>();
            pauseRect.anchorMin = pauseRect.anchorMax = new Vector2(1f, 1f);

            view.pausePanel = Modal("PausePanel", root.transform);
            var pauseCard = Card(view.pausePanel.transform);
            CenterLabel(pauseCard, font, "PAUSED", 80f);
            view.resumeButton = ActionButton("Resume", pauseCard, font, new Vector2(0f, 0f), new Vector2(260f, 48f));
            view.pauseRestartButton = ActionButton("Restart", pauseCard, font, new Vector2(0f, -64f), new Vector2(260f, 48f));

            view.resultPanel = Modal("ResultPanel", root.transform);
            var resultCard = Card(view.resultPanel.transform);
            view.resultText = CenterLabel(resultCard, font, "", 55f);
            view.resultRestartButton = ActionButton("Restart", resultCard, font, new Vector2(0f, -90f), new Vector2(260f, 48f));
            view.pausePanel.SetActive(false);
            view.resultPanel.SetActive(false);
            return view;
        }

        private void Pause() { owner?.SubmitCommand(UiCommand.Pause); }
        private void Resume() { owner?.SubmitCommand(UiCommand.Resume); }
        private void Restart() { owner?.SubmitCommand(UiCommand.Restart); }
        private void OnEnable() { if (owner != null) { AddListeners(); } }
        private void OnDisable() { RemoveListeners(); }
        private static string Count(int value) { return value < 0 ? "--" : value.ToString(); }

        private static RectTransform Panel(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            item.GetComponent<Image>().color = color;
            return rect;
        }

        private static GameObject Modal(string name, Transform parent)
        {
            return Panel(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.01f, 0.02f, 0.04f, 0.82f)).gameObject;
        }

        private static RectTransform Card(Transform parent)
        {
            var rect = Panel("Card", parent, Vector2.one * 0.5f, Vector2.one * 0.5f,
                new Vector2(-250f, -170f), new Vector2(250f, 170f), new Color(0.06f, 0.11f, 0.17f));
            return rect;
        }

        private static Text Label(string name, Transform parent, Font font, string content,
            Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = item.GetComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.text = content;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static Text CenterLabel(Transform parent, Font font, string text, float y)
        {
            var label = Label("Heading", parent, font, text, Vector2.zero, new Vector2(440f, 140f), 26, Color.white);
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = new Vector2(0f, y);
            label.alignment = TextAnchor.MiddleCenter;
            return label;
        }

        private static Button ActionButton(string text, Transform parent, Font font, Vector2 position, Vector2 size)
        {
            var rect = Panel(text + "Button", parent, Vector2.one * 0.5f, Vector2.one * 0.5f,
                position - size * 0.5f, position + size * 0.5f, new Color(0.15f, 0.36f, 0.39f));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var label = CenterLabel(rect, font, text.ToUpperInvariant(), 0f);
            label.fontSize = 19;
            label.rectTransform.sizeDelta = size;
            return button;
        }
    }
}
