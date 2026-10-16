using SlimeBattle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBattle.UI
{
    public sealed class ReferenceMainMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject home;
        [SerializeField] private GameObject levels;
        [SerializeField] private RectTransform[] cards;
        [SerializeField] private GameObject selectButton;
        [SerializeField] private Text selectionHint;
        [SerializeField] private MainMenuLevelEntry entry;
        private int selected=-1;
        public int SelectedLevel => selected;
        public void OpenLevels() { home.SetActive(false); levels.SetActive(true); SelectLevel(-1); }
        public void Back() { levels.SetActive(false); home.SetActive(true); }
        public void SelectLevel(int index)
        {
            selected=index;
            for(var i=0;i<cards.Length;i++)
            {
                cards[i].localScale=Vector3.one*(i==selected?1.4f:1);
                cards[i].GetComponent<Image>().color=i==selected?new Color(0.29f,0.34f,0.19f):new Color(0.44f,0.34f,0.23f);
                cards[i].SetSiblingIndex(i==selected?cards.Length:Mathf.Min(i,cards.Length));
            }
            selectButton.SetActive(index>=0);
            selectButton.GetComponent<Button>().interactable=index==0;
            selectionHint.text=string.Empty;
        }
        public void StartSelectedLevel() { if(selected==0 && entry!=null) entry.StartLevelTest(); }
        public void Quit()
        {
            var root=FindObjectOfType<GameRoot>();
            if(root!=null) root.RequestQuit(); else Application.Quit();
        }
        private void Awake() { Back(); }
    }
}
