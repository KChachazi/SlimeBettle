using SlimeBattle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBattle.UI
{
    public sealed class ReferenceMainMenuView : MonoBehaviour
    {
        private const int FirstLevelIndex = 0;
        private const int CastleLevelIndex = 1;
        [SerializeField] private GameObject home;
        [SerializeField] private GameObject levels;
        [SerializeField] private RectTransform[] cards;
        [SerializeField] private GameObject selectButton;
        [SerializeField] private Text selectionHint;
        [SerializeField] private MainMenuLevelEntry entry;
        [SerializeField] private bool useIllustratedCards;
        private int selected=-1;
        public int SelectedLevel => selected;
        public void OpenLevels() { home.SetActive(false); levels.SetActive(true); SelectLevel(-1); }
        public void Back() { levels.SetActive(false); home.SetActive(true); }
        public void SelectLevel(int index)
        {
            if(index < -1 || index >= cards.Length) return;
            selected=index;
            for(var i=0;i<cards.Length;i++)
            {
                cards[i].localScale=Vector3.one*(i==selected?(useIllustratedCards?1.08f:1.4f):1);
                cards[i].GetComponent<Image>().color=useIllustratedCards
                    ? (i==selected?new Color(1f,0.94f,0.78f):Color.white)
                    : (i==selected?new Color(0.29f,0.34f,0.19f):new Color(0.44f,0.34f,0.23f));
            }
            if(selected>=0) cards[selected].SetAsLastSibling();
            selectButton.SetActive(index>=0);
            selectButton.GetComponent<Button>().interactable =
                index == FirstLevelIndex || index == CastleLevelIndex;
            selectionHint.text=index>CastleLevelIndex?"第 3 章尚未开放":string.Empty;
        }
        public void StartSelectedLevel()
        {
            if (entry == null) return;
            if (selected == FirstLevelIndex) entry.StartFirstLevel();
            else if (selected == CastleLevelIndex) entry.StartCastleLevel();
        }
        public void Quit()
        {
            var root=FindObjectOfType<GameRoot>();
            if(root!=null) root.RequestQuit(); else Application.Quit();
        }
        private void Awake() { Back(); }
    }
}
