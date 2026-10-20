using UnityEngine;
using UnityEngine.UI;

namespace SlimeBattle.UI
{
    /// <summary>Flips presentation pages; it does not create unlock or inventory state.</summary>
    public sealed class TapTapCatalogView : MonoBehaviour
    {
        [SerializeField] private Image pagination;
        [SerializeField] private Sprite[] paginationSprites;
        [SerializeField] private GameObject[] contentPages;
        [SerializeField] private Button previous;
        [SerializeField] private Button next;
        public int PageIndex { get; private set; }

        public void Previous() { SetPage(PageIndex - 1); }
        public void Next() { SetPage(PageIndex + 1); }
        private void OnEnable() { SetPage(0); }
        private void SetPage(int index)
        {
            if (contentPages == null || contentPages.Length == 0) return;
            PageIndex = Mathf.Clamp(index, 0, contentPages.Length - 1);
            for (var i = 0; i < contentPages.Length; i++)
                if (contentPages[i] != null) contentPages[i].SetActive(i == PageIndex);
            if (pagination != null && paginationSprites != null && PageIndex < paginationSprites.Length)
                pagination.sprite = paginationSprites[PageIndex];
            if (previous != null) previous.interactable = PageIndex > 0;
            if (next != null) next.interactable = PageIndex < contentPages.Length - 1;
        }
    }
}
