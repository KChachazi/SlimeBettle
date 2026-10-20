using System;
using SlimeBattle.Player.Placement;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBattle.UI
{
    /// <summary>Prefab-owned mechanism selection; creation stays in PlacementSystem.</summary>
    public sealed class PlacementSelectionView : MonoBehaviour
    {
        private PlacementSystem placement;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text hint;
        [SerializeField] private Text budget;
        [SerializeField] private Text foodRemainingText;
        [SerializeField] private Text obstacleRemainingText;
        [SerializeField] private Button food;
        [SerializeField] private Button furnace;
        [SerializeField] private Button obstacle;
        [SerializeField] private Button cancel;
        [SerializeField] private bool useChineseLabels;
        [SerializeField] private GameObject foodSelected;
        [SerializeField] private GameObject furnaceSelected;
        [SerializeField] private GameObject obstacleSelected;
        [SerializeField] private bool useReferenceLayout;
        [SerializeField] private bool useIllustratedSprites;
        public bool IsVisible => panel != null && panel.activeInHierarchy;

        public void Initialize(PlacementSystem placement)
        {
            Unbind();
            this.placement = placement ?? throw new ArgumentNullException(nameof(placement));
            if (panel == null || hint == null || budget == null || food == null ||
                furnace == null || obstacle == null || cancel == null)
                throw new InvalidOperationException("Placement selection prefab references are incomplete.");
            var fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var label in GetComponentsInChildren<Text>(true))
                if (label.font == null) label.font = fallbackFont;
            if (isActiveAndEnabled) { Bind(); }
        }

        private void Bind()
        {
            placement.Changed += Render;
            food.onClick.AddListener(SelectFood);
            furnace.onClick.AddListener(SelectFurnace);
            obstacle.onClick.AddListener(SelectObstacle);
            cancel.onClick.AddListener(Cancel);
            Render();
        }

        private void Unbind()
        {
            if (placement != null) { placement.Changed -= Render; }
            food?.onClick.RemoveListener(SelectFood);
            furnace?.onClick.RemoveListener(SelectFurnace);
            obstacle?.onClick.RemoveListener(SelectObstacle);
            cancel?.onClick.RemoveListener(Cancel);
            if (panel != null) { panel.SetActive(false); }
        }

        private void SelectFood() { placement.BeginPlacement(PlacementItemId.Food); }
        private void SelectFurnace() { placement.BeginPlacement(PlacementItemId.Furnace); }
        private void SelectObstacle() { placement.BeginPlacement(PlacementItemId.Obstacle); }
        private void Cancel() { placement.CancelPlacement(); }
        private void OnEnable() { if (placement != null) { Bind(); } }
        private void OnDisable() { Unbind(); }

        private void Render()
        {
            panel.SetActive(isActiveAndEnabled && placement.IsRunning);
            hint.text = useChineseLabels ? FirstLevelUiText.PlacementHint(placement) : placement.Hint;
            var capacity = placement.GetBudgetSnapshot();
            budget.text = capacity.HasData
                ? (useChineseLabels ? "场上 " : "Placed ") + capacity.ActiveCount + "/" + capacity.MaximumActiveCount
                : (useChineseLabels ? "放置配额未接入" : "Placed: unavailable");
            if (capacity.HasInventory)
                budget.text += useChineseLabels
                    ? " | 食物 " + capacity.FoodRemaining + " 熔炉 " + capacity.FurnaceRemaining +
                        " 障碍 " + capacity.ObstacleRemaining
                    : " | Food " + capacity.FoodRemaining + " Furnace " +
                        capacity.FurnaceRemaining + "  Obstacles " + capacity.ObstacleRemaining;
            RenderRemaining(foodRemainingText, capacity.HasInventory, capacity.FoodRemaining);
            RenderRemaining(obstacleRemainingText, capacity.HasInventory, capacity.ObstacleRemaining);
            food.interactable = !capacity.HasInventory || capacity.FoodRemaining > 0;
            furnace.interactable = !capacity.HasInventory || capacity.FurnaceRemaining > 0;
            obstacle.interactable = !capacity.HasInventory || capacity.ObstacleRemaining > 0;
            cancel.interactable = placement.IsInteracting;
            var cancelLabel = cancel.GetComponentInChildren<Text>(true);
            if (cancelLabel != null) cancelLabel.text = placement.IsRemoving
                ? (useChineseLabels ? "取消拆除" : "Cancel removal")
                : (useChineseLabels ? "取消放置" : "Cancel placement");
            Highlight(food, PlacementItemId.Food);
            Highlight(furnace, PlacementItemId.Furnace);
            Highlight(obstacle, PlacementItemId.Obstacle);
            if (foodSelected != null) foodSelected.SetActive(placement.SelectedItem == PlacementItemId.Food);
            if (furnaceSelected != null) furnaceSelected.SetActive(placement.SelectedItem == PlacementItemId.Furnace);
            if (obstacleSelected != null) obstacleSelected.SetActive(placement.SelectedItem == PlacementItemId.Obstacle);
            if(useReferenceLayout) cancel.gameObject.SetActive(placement.IsInteracting);
        }

        private void Highlight(Button button, PlacementItemId item)
        {
            if (button.targetGraphic == null) { return; }
            if (useIllustratedSprites)
            {
                button.targetGraphic.color = placement.SelectedItem == item
                    ? new Color(1f, 0.90f, 0.66f) : Color.white;
                return;
            }
            button.targetGraphic.color = placement.SelectedItem == item
                ? (useChineseLabels ? new Color(1f, 0.80f, 0.35f) : new Color(0.15f, 0.65f, 0.45f))
                : (useChineseLabels ? new Color(0.98f, 0.945f, 0.82f) : new Color(0.14f, 0.24f, 0.32f));
        }

        private void RenderRemaining(Text label, bool hasInventory, int remaining)
        {
            if (label != null)
                label.text = (useChineseLabels ? "剩余 " : "Left ") +
                    (hasInventory ? remaining.ToString() : "--");
        }
    }
}
