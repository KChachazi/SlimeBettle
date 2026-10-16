using System;
using SlimeBattle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBattle.UI
{
    /// <summary>Presentation-only pages. Empty slots stay empty until an inventory/catalog source exists.</summary>
    public sealed class ReferenceUiView : MonoBehaviour
    {
        public enum Page { None, Bag, Catalog, Settings, Volume, Saves, Resolution }
        [SerializeField] private GameObject[] pages;
        [SerializeField] private Text resolutionText;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Text volumeText;
        [SerializeField] private Text feedbackText;
        private Action<UiCommand> submit;
        private LevelState phase;
        private bool resumeOnClose;
        public Page CurrentPage { get; private set; }
        public bool IsOpen => CurrentPage != Page.None;
        public event Action VisibilityChanged;

        public void Initialize(Action<UiCommand> submitCommand) { submit=submitCommand; }
        public void SetLevelState(LevelState state)
        {
            // A restart/result cannot leave a stale modal covering the authoritative view.
            if(IsOpen && ((state!=LevelState.Paused && state!=LevelState.Running) ||
                (state==LevelState.Running && phase==LevelState.Paused))) CloseSilently();
            phase=state;
        }
        public void OpenBag() { Open(Page.Bag); }
        public void OpenCatalog() { Open(Page.Catalog); }
        public void OpenSettings() { Open(Page.Settings); }
        public void OpenVolume() { Open(Page.Volume); }
        public void OpenSaves() { Open(Page.Saves); }
        public void OpenResolution() { Open(Page.Resolution); }
        public void Open(Page page)
        {
            if(page==Page.None) { Close(); return; }
            if(phase!=LevelState.Running && phase!=LevelState.Paused) return;
            if(!IsOpen) resumeOnClose=phase==LevelState.Running;
            CurrentPage=page;
            RefreshPages();
            if(resumeOnClose && phase==LevelState.Running) submit?.Invoke(UiCommand.Pause);
        }
        public void Close()
        {
            var resume=resumeOnClose;
            CloseSilently();
            if(resume && phase==LevelState.Paused) submit?.Invoke(UiCommand.Resume);
        }
        public void BackToSettings() { Open(Page.Settings); }
        public void CloseSilently() { CurrentPage=Page.None; resumeOnClose=false; RefreshPages(); }
        public void ShowUnavailableRemove() { if(feedbackText!=null) feedbackText.text="拆除功能尚未接入"; }
        public void Quit()
        {
            var root=FindObjectOfType<GameRoot>();
            if(root!=null) root.RequestQuit(); else Application.Quit();
        }
        public void SetVolume(float value)
        {
            AudioListener.volume=value;
            PlayerPrefs.SetFloat("SlimeBattle.Ui.Volume",value);
            if(volumeText!=null) volumeText.text=Mathf.RoundToInt(value*100)+" %";
        }
        public void SetResolution(int index)
        {
            var sizes=new[]{new Vector2Int(1280,720),new Vector2Int(1600,900),new Vector2Int(1920,1080)};
            if(index<0 || index>=sizes.Length) return;
            if(!Application.isEditor) Screen.SetResolution(sizes[index].x,sizes[index].y,Screen.fullScreenMode);
            PlayerPrefs.SetInt("SlimeBattle.Ui.Resolution",index);
            if(resolutionText!=null) resolutionText.text=sizes[index].x+" × "+sizes[index].y+(Application.isEditor?"（编辑器预览）":"");
        }
        private void Awake()
        {
            if(volumeSlider!=null) volumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("SlimeBattle.Ui.Volume",1));
            SetVolume(PlayerPrefs.GetFloat("SlimeBattle.Ui.Volume",1));
            if(resolutionText!=null)
            {
                var sizes=new[]{"1280 × 720","1600 × 900","1920 × 1080"};
                resolutionText.text=sizes[Mathf.Clamp(PlayerPrefs.GetInt("SlimeBattle.Ui.Resolution",0),0,2)];
            }
            RefreshPages();
        }
        private void RefreshPages()
        {
            if(pages!=null) for(var i=0;i<pages.Length;i++) if(pages[i]!=null) pages[i].SetActive((int)CurrentPage==i+1);
            VisibilityChanged?.Invoke();
        }
        private void OnDisable() { CloseSilently(); }
    }
}
