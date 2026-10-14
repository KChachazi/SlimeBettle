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
        [SerializeField] private Button food;
        [SerializeField] private Button furnace;
        [SerializeField] private Button obstacle;
        [SerializeField] private Button cancel;
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
            hint.text = placement.Hint;
            var capacity = placement.GetBudgetSnapshot();
            budget.text = capacity.HasData
                ? "Placed: " + capacity.ActiveCount + " / " + capacity.MaximumActiveCount
                : "Placed: unavailable";
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
    }
}
