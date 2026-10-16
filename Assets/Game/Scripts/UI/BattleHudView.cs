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
        [SerializeField] private Text weaponText;
        [SerializeField] private Text hintText;
        [SerializeField] private Text resultText;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseRestartButton;
        [SerializeField] private Button resultRestartButton;
        [SerializeField] private bool useChineseLabels;
        [SerializeField] private Text goalText;
        [SerializeField] private Text waitingText;
        [SerializeField] private Text routeHintText;
        [SerializeField] private Text resultCountsText;
        [SerializeField] private Text resultDetailText;
        [SerializeField] private ReferenceUiView referencePages;
        private bool isPauseVisible;
        private UISystem owner;

        public void Bind(UISystem uiSystem)
        {
            Unbind();
            owner = uiSystem;
            if(referencePages!=null) referencePages.Initialize(owner.SubmitCommand);
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
            if(referencePages!=null) referencePages.Initialize(null);
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
            isPauseVisible=showPause;
            if(referencePages!=null) referencePages.SetLevelState(state.LevelState);
            UpdatePauseVisibility();
            if (resultPanel != null) { resultPanel.SetActive(showResult); }
            if (objectiveText != null) { objectiveText.text = useChineseLabels ? FirstLevelUiText.Hint(state.ObjectiveText) : state.ObjectiveText; }
            if (countsText != null)
            {
                countsText.text = useChineseLabels ? "史莱姆数量 " + Count(state.SlimeCount) + " / " + Count(state.SlimeLimit) : "SLIMES  " + Count(state.SlimeCount) +
                    (state.SlimeLimit > 0 ? " / " + state.SlimeLimit : string.Empty) + "    ENEMIES  " + Count(state.EnemyCount) +
                    "    /    " + state.LevelState;
            }
            if (weaponText != null) { weaponText.text = state.WeaponText; weaponText.gameObject.SetActive(!string.IsNullOrEmpty(state.WeaponText)); }
            if (hintText != null) { hintText.text = useChineseLabels ? FirstLevelUiText.Hint(state.Hint) : state.Hint; }
            if (goalText != null) { goalText.text = string.IsNullOrEmpty(state.GoalText) ? "到达洞穴  -- / 6 · 未接入" : state.GoalText; }
            if (waitingText != null) { waitingText.text = string.IsNullOrEmpty(state.WaitingText) ? "等待窗口  -- · 未接入" : state.WaitingText; }
            if (routeHintText != null) { routeHintText.text = string.IsNullOrEmpty(state.RouteHint) ? "路线 / Anchor 提示未接入" : state.RouteHint; }
            if (resultCountsText != null) { resultCountsText.text = (goalText != null ? goalText.text : state.GoalText) + "\n" + (waitingText != null ? waitingText.text : state.WaitingText); }
            if (resultText != null)
            {
                resultText.text = (useChineseLabels ? (state.LevelState == LevelState.Succeeded ? "引导成功" : "再试一次吧") :
                    (state.LevelState == LevelState.Succeeded ? "LEVEL COMPLETE" : "LEVEL FAILED")) +
                    (resultDetailText != null || string.IsNullOrEmpty(state.ResultText) ? string.Empty : "\n\n" + state.ResultText);
            }
            if (resultDetailText != null) { resultDetailText.text = useChineseLabels ? FirstLevelUiText.Hint(state.ResultText) : state.ResultText; }
            if (pauseButton != null) { pauseButton.interactable = state.LevelState == LevelState.Running && !isPending; }
            if (resumeButton != null) { resumeButton.interactable = state.LevelState == LevelState.Paused && !isPending; }
            if (pauseRestartButton != null) { pauseRestartButton.interactable = !isPending && state.LevelState == LevelState.Paused; }
            if (resultRestartButton != null) { resultRestartButton.interactable = !isPending && (state.LevelState == LevelState.Succeeded || state.LevelState == LevelState.Failed); }
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
                new Vector2(0f, -180f), Vector2.zero, new Color(0.035f, 0.065f, 0.11f, 0.96f));
            view.hudPanel = hud.gameObject;
            Label("Title", hud, font, "SLIME BATTLE  /  P0", new Vector2(24f, -16f), new Vector2(800f, 28f), 20,
                new Color(0.4f, 0.94f, 0.77f));
            view.objectiveText = Label("Objective", hud, font, "", new Vector2(24f, -52f), new Vector2(950f, 28f), 22, Color.white);
            view.countsText = Label("Counts", hud, font, "", new Vector2(24f, -88f), new Vector2(950f, 22f), 16,
                new Color(0.6f, 0.72f, 0.8f));
            view.weaponText = Label("Weapon", hud, font, "", new Vector2(24f, -114f), new Vector2(1080f, 22f), 16,
                new Color(0.85f, 0.8f, 0.55f));
            view.hintText = Label("Hint", hud, font, "", new Vector2(24f, -146f), new Vector2(1200f, 26f), 15,
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

        public bool TryCloseReferencePage()
        { if(referencePages==null || !referencePages.IsOpen) return false; referencePages.Close(); return true; }
        private void Pause() { if(referencePages!=null) referencePages.OpenSettings(); else owner?.SubmitCommand(UiCommand.Pause); }
        private void Resume() { referencePages?.CloseSilently(); owner?.SubmitCommand(UiCommand.Resume); }
        private void Restart() { owner?.SubmitCommand(UiCommand.Restart); }
        private void UpdatePauseVisibility() { if(pausePanel!=null) pausePanel.SetActive(isPauseVisible && (referencePages==null || !referencePages.IsOpen)); }
        private void OnEnable() { if (owner != null) { AddListeners(); } if(referencePages!=null) referencePages.VisibilityChanged+=UpdatePauseVisibility; }
        private void OnDisable() { RemoveListeners(); if(referencePages!=null) referencePages.VisibilityChanged-=UpdatePauseVisibility; }
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
