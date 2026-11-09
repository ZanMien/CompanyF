using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace CompanyF.UI
{
    /// <summary>Title animation -> tutorial -> fresh pedal press -> game.</summary>
    public sealed class TutorialVideoController : MonoBehaviour
    {
        public enum PlaybackStage { Idle, Preparing, Playing, AwaitingStart, Launching }

        [SerializeField] VideoClip tutorialClip;
        [SerializeField] Font instructionFont;
        [Tooltip("Maximum gap between successive presses in the three-press skip gesture.")]
        [SerializeField, Min(0.1f)] float skipPressInterval = 1f;

        public PlaybackStage Stage { get; private set; }
        public int SkipPressCount { get; private set; }
        public string Hint => hint != null ? hint.text : "";

        VideoPlayer player;
        AudioSource audioSource;
        RenderTexture texture;
        GameObject overlay;
        UnityEngine.UI.Text hint;
        string gameScene;
        float lastPress;
        float prepareStarted;
        bool wasDown;
        bool waitForRelease;

        public void Begin(string nextScene)
        {
            if (Stage != PlaybackStage.Idle) return;
            gameScene = nextScene;
            CreateOverlay();
            overlay.SetActive(true);
            Stage = PlaybackStage.Preparing;
            waitForRelease = true;
            SkipPressCount = 0;
            prepareStarted = Time.unscaledTime;
            UpdateHint();
            if (tutorialClip == null) { PlaybackFailed(null, "Tutorial clip is missing"); return; }
            player.Prepare();
        }

        void CreateOverlay()
        {
            overlay = new GameObject("TutorialVideoOverlay", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            overlay.transform.SetParent(transform, false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20000;
            var scaler = overlay.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var background = new GameObject("BlackBackground", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            Stretch((RectTransform)background.transform, overlay.transform);
            background.GetComponent<UnityEngine.UI.Image>().color = Color.black;
            background.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;

            var picture = new GameObject("TutorialPicture", typeof(RectTransform), typeof(UnityEngine.UI.RawImage), typeof(UnityEngine.UI.AspectRatioFitter));
            Stretch((RectTransform)picture.transform, overlay.transform);
            var fitter = picture.GetComponent<UnityEngine.UI.AspectRatioFitter>();
            fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = tutorialClip != null && tutorialClip.height > 0 ? (float)tutorialClip.width / tutorialClip.height : 16f / 9f;
            texture = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
            texture.Create();
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
            picture.GetComponent<UnityEngine.UI.RawImage>().texture = texture;
            picture.GetComponent<UnityEngine.UI.RawImage>().raycastTarget = false;

            var message = new GameObject("PedalInstructions", typeof(RectTransform), typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.Outline));
            var rect = (RectTransform)message.transform;
            rect.SetParent(overlay.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(-32, 24);
            rect.sizeDelta = new Vector2(1000, 100);
            hint = message.GetComponent<UnityEngine.UI.Text>();
            hint.font = instructionFont != null ? instructionFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hint.fontSize = 30;
            hint.alignment = TextAnchor.LowerRight;
            hint.color = Color.white;
            hint.raycastTarget = false;
            message.GetComponent<UnityEngine.UI.Outline>().effectColor = Color.black;
            message.GetComponent<UnityEngine.UI.Outline>().effectDistance = new Vector2(2, -2);

            audioSource = overlay.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            player = overlay.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = false;
            player.waitForFirstFrame = true;
            player.skipOnDrop = true;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = texture;
            player.clip = tutorialClip;
            player.aspectRatio = VideoAspectRatio.FitInside;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.controlledAudioTrackCount = 1;
            player.SetTargetAudioSource(0, audioSource);
            player.EnableAudioTrack(0, true);
            player.prepareCompleted += Prepared;
            player.loopPointReached += Completed;
            player.errorReceived += PlaybackFailed;
        }

        static void Stretch(RectTransform rect, Transform parent)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        void Prepared(VideoPlayer source)
        {
            if (Stage != PlaybackStage.Preparing) return;
            Stage = PlaybackStage.Playing;
            player.Play();
            UpdateHint();
        }

        void Completed(VideoPlayer source)
        {
            if (Stage != PlaybackStage.Playing) return;
            player.Pause();
            audioSource.Stop();
            Stage = PlaybackStage.AwaitingStart;
            SkipPressCount = 0;
            waitForRelease = true;
            UpdateHint();
        }

        void PlaybackFailed(VideoPlayer source, string message)
        {
            if (Stage == PlaybackStage.Launching || Stage == PlaybackStage.AwaitingStart) return;
            Debug.LogError("[Tutorial Video] " + message, this);
            player.Stop();
            Stage = PlaybackStage.AwaitingStart;
            waitForRelease = true;
            UpdateHint();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            bool down = keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed);
            TickInput(Time.unscaledTime, down);
            if (Stage == PlaybackStage.Preparing && Time.unscaledTime - prepareStarted > 15f)
                PlaybackFailed(player, "Video preparation timed out");
        }

        /// <summary>Only release-to-press edges count; a slow or held press never becomes a triple press.</summary>
        public void TickInput(float now, bool down)
        {
            if (Stage == PlaybackStage.Idle || Stage == PlaybackStage.Launching) return;
            if (waitForRelease)
            {
                wasDown = down;
                if (!down) { waitForRelease = false; wasDown = false; }
                return;
            }
            bool pressed = down && !wasDown;
            wasDown = down;
            if (SkipPressCount > 0 && now - lastPress > skipPressInterval)
            {
                SkipPressCount = 0;
                UpdateHint();
            }
            if (!pressed) return;
            if (Stage == PlaybackStage.AwaitingStart) { LaunchGame(); return; }
            lastPress = now;
            SkipPressCount++;
            if (SkipPressCount >= 3) LaunchGame();
            else UpdateHint();
        }

        void UpdateHint()
        {
            if (hint == null) return;
            hint.text = Stage == PlaybackStage.AwaitingStart
                ? "ペダルを1回踏むとスタート"
                : "3回ペダルを踏むとスキップ" + (SkipPressCount > 0 ? $"  {SkipPressCount}/3" : "");
        }

        void LaunchGame()
        {
            if (Stage == PlaybackStage.Launching) return;
            Stage = PlaybackStage.Launching;
            player.Stop();
            audioSource.Stop();
            SceneManager.LoadScene(gameScene);
        }

        void OnDestroy()
        {
            if (player != null)
            {
                player.prepareCompleted -= Prepared;
                player.loopPointReached -= Completed;
                player.errorReceived -= PlaybackFailed;
                player.Stop();
                player.targetTexture = null;
            }
            if (texture != null) { texture.Release(); Destroy(texture); }
        }
    }
}
