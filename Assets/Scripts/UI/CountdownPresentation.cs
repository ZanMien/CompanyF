using CompanyF.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyF.UI
{
    /// <summary>Transparent uGUI countdown presentation, driven by the same clock as gameplay.</summary>
    public class CountdownPresentation : MonoBehaviour
    {
        [SerializeField] GameFlow flow;
        [SerializeField] UnityEngine.UI.Text text;
        [SerializeField] CanvasGroup textGroup;
        [SerializeField] CanvasGroup levers;
        [SerializeField] CanvasGroup rails;
        [SerializeField] UnityEngine.UI.Outline border;
        [SerializeField] UnityEngine.UI.Shadow bevel;
        [SerializeField] UnityEngine.UI.Image departureArtwork;
        [SerializeField] UnityEngine.UI.Image goArtwork;

        public string DisplayedText => text != null ? text.text : "";

        void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (flow == null || text == null) return;
            levers.alpha = rails.alpha = textGroup.alpha = 0f;
            var rect = text.rectTransform;
            rect.localScale = Vector3.one;
            rect.anchoredPosition = Vector2.zero;
            text.fontSize = 150;
            SetStyle(false);
            switch (flow.Phase)
            {
                case GamePhase.Ready:
                    float ready = flow.ReadyElapsed;
                    text.text = "出発";
                    textGroup.alpha = Fade(ready, 1f, 0.2f, 0.2f);
                    levers.alpha = Fade(ready, 1f, 0.3f, 0.2f);
                    float stretch = ready >= 0.2f && ready < 0.35f ? Mathf.Sin((ready - 0.2f) / 0.15f * Mathf.PI) : 0f;
                    rect.localScale = new Vector3(1f, 1f + stretch * 0.18f, 1f);
                    break;
                case GamePhase.Running:
                    if (flow.ShowsGoMessage)
                    {
                        float go = flow.GoElapsed;
                        text.text = "進行!!";
                        textGroup.alpha = Mathf.Clamp01(go / 0.2f);
                        rails.alpha = Fade(go, 1.2f, 0.2f, 0.2f);
                        float move = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((go - 0.5f) / 0.5f));
                        rect.anchoredPosition = new Vector2(0f, move * (Screen.height / GetComponent<Canvas>().scaleFactor + 250f));
                        rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.25f, move);
                    }
                    else if (flow.SecondsShown >= 1 && flow.SecondsShown <= 5)
                    {
                        text.text = flow.SecondsShown.ToString();
                        float elapsed = Mathf.Clamp01(flow.SecondsShown - flow.TimeLeft);
                        textGroup.alpha = Fade(elapsed, 1f, 0.2f, 0.8f);
                        rect.anchoredPosition = new Vector2(0f, 160f);
                        text.fontSize = 190;
                        SetStyle(true);
                    }
                    else text.text = "";
                    break;
                case GamePhase.Finishing:
                    text.text = "フィニッシュ!!";
                    text.fontSize = 120;
                    rect.anchoredPosition = new Vector2(0f, 160f);
                    textGroup.alpha = Mathf.Clamp01(flow.FinishElapsed / 0.2f);
                    SetStyle(true);
                    break;
                default: text.text = ""; break;
            }
            if (departureArtwork != null)
            {
                bool show = flow.Phase == GamePhase.Ready;
                departureArtwork.enabled = show;
                if (show)
                {
                    departureArtwork.color = new Color(1f, 1f, 1f, textGroup.alpha);
                    departureArtwork.rectTransform.localScale = rect.localScale;
                    textGroup.alpha = levers.alpha = 0f;
                }
            }
            if (goArtwork != null)
            {
                bool show = flow.ShowsGoMessage;
                goArtwork.enabled = show;
                if (show)
                {
                    goArtwork.color = new Color(1f, 1f, 1f, textGroup.alpha);
                    goArtwork.rectTransform.anchoredPosition = rect.anchoredPosition;
                    goArtwork.rectTransform.localScale = rect.localScale;
                    textGroup.alpha = rails.alpha = 0f;
                }
            }
        }

        void SetStyle(bool ending)
        {
            text.color = ending ? new Color32(0xfc, 0x52, 0x52, 255) : new Color32(0xed, 0x70, 0x45, 255);
            border.effectColor = ending ? new Color32(0xcb, 0x83, 0x2d, 255) : new Color32(0x9d, 0x3d, 0x24, 255);
            bevel.effectColor = ending ? new Color32(0xff, 0xa7, 0x83, 255) : new Color32(0xff, 0xb0, 0x7a, 255);
        }

        public static float Fade(float elapsed, float duration, float fadeIn, float fadeOut)
        {
            if (elapsed < 0f || elapsed >= duration) return 0f;
            return Mathf.Min(Mathf.Clamp01(elapsed / fadeIn), Mathf.Clamp01((duration - elapsed) / fadeOut));
        }
    }
}
