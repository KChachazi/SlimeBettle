using System;
using SlimeBattle.Player.Placement;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBattle.UI
{
    /// <summary>Selection and explicit confirm/cancel only; no inventory or mechanism creation.</summary>
    public sealed class PlacementSelectionView : MonoBehaviour
    {
        private PlacementSystem placement;
        private GameObject panel;
        private Text hint;
        private Button food;
        private Button furnace;
        private Button obstacle;
        private Button confirm;
        private Button cancel;
        public bool IsVisible => panel != null && panel.activeInHierarchy;

        public void Initialize(PlacementSystem placement)
        {
            Unbind();
            this.placement = placement ?? throw new ArgumentNullException(nameof(placement));
            if (isActiveAndEnabled) { Bind(); }
        }

        private void Bind()
        {
            placement.Changed += Render;
            food.onClick.AddListener(SelectFood);
            furnace.onClick.AddListener(SelectFurnace);
            obstacle.onClick.AddListener(SelectObstacle);
            confirm.onClick.AddListener(Confirm);
            cancel.onClick.AddListener(Cancel);
            Render();
        }

        private void Unbind()
        {
            if (placement != null) { placement.Changed -= Render; }
            food?.onClick.RemoveListener(SelectFood);
            furnace?.onClick.RemoveListener(SelectFurnace);
            obstacle?.onClick.RemoveListener(SelectObstacle);
            confirm?.onClick.RemoveListener(Confirm);
            cancel?.onClick.RemoveListener(Cancel);
            if (panel != null) { panel.SetActive(false); }
        }

        private void SelectFood() { placement.BeginPlacement(PlacementItemId.Food); }
        private void SelectFurnace() { placement.BeginPlacement(PlacementItemId.Furnace); }
        private void SelectObstacle() { placement.BeginPlacement(PlacementItemId.Obstacle); }
        private void Confirm() { placement.ConfirmPlacement(); }
        private void Cancel() { placement.CancelPlacement(); }
        private void OnEnable() { if (placement != null) { Bind(); } }
        private void OnDisable() { Unbind(); }

        private void Render()
        {
            panel.SetActive(isActiveAndEnabled && placement.IsRunning);
            hint.text = placement.Hint;
            confirm.interactable = placement.GetCurrentPreview().CanConfirm;
            cancel.interactable = placement.IsPlacing;
            Highlight(food, PlacementItemId.Food);
            Highlight(furnace, PlacementItemId.Furnace);
            Highlight(obstacle, PlacementItemId.Obstacle);
        }

        private void Highlight(Button button, PlacementItemId item)
        {
            button.GetComponent<Image>().color = placement.SelectedItem == item
                ? new Color(0.15f, 0.65f, 0.45f) : new Color(0.14f, 0.24f, 0.32f);
        }

        public static PlacementSelectionView Create(Transform parent, Font font = null)
        {
            var item = new GameObject("PlacementSelection", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PlacementSelectionView));
            item.transform.SetParent(parent, false);
            item.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            item.GetComponent<Canvas>().sortingOrder = 21;
            var scaler = item.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            var view = item.GetComponent<PlacementSelectionView>();
            var panel = new GameObject("PlacementPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(item.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.right;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, 110f);
            panel.GetComponent<Image>().color = new Color(0.04f, 0.08f, 0.12f, 0.97f);
            view.panel = panel;
            font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            view.food = Button(panel.transform, font, "Food", 24f);
            view.furnace = Button(panel.transform, font, "Furnace", 204f);
            view.obstacle = Button(panel.transform, font, "Obstacle", 384f);
            view.confirm = Button(panel.transform, font, "Confirm", 744f);
            view.cancel = Button(panel.transform, font, "Cancel", 924f);
            view.hint = Label(panel.transform, font, "PlacementHint", new Vector2(24f, 74f), new Vector2(1200f, 28f));
            panel.SetActive(false);
            return view;
        }

        private static Button Button(Transform parent, Font font, string name, float x)
        {
            var item = new GameObject("Placement" + name + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, 18f);
            rect.sizeDelta = new Vector2(160f, 44f);
            var button = item.GetComponent<Button>();
            item.GetComponent<Image>().color = new Color(0.14f, 0.24f, 0.32f);
            button.targetGraphic = item.GetComponent<Image>();
            var label = Label(item.transform, font, "Label", Vector2.zero, rect.sizeDelta);
            label.text = name.ToUpperInvariant();
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static Text Label(Transform parent, Font font, string name, Vector2 position, Vector2 size)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = item.GetComponent<Text>();
            label.font = font;
            label.fontSize = 18;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }
    }
}
