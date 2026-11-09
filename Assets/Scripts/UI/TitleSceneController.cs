using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TitleSceneController : MonoBehaviour
{
    [Header("Scene Settings / シーン設定")]
    [SerializeField] private string nextSceneName = "MainScene";

    [Header("UI References / UI参照")]
    [SerializeField] private GameObject gameStartRoot;
    [SerializeField] private RectTransform transitionTrain;

    [Header("Transition Settings / 演出設定")]
    [SerializeField] private float transitionDuration = 3.0f;
    [SerializeField] private float trainStartX = -2200f;
    [SerializeField] private float trainEndX = 2200f;
    [SerializeField] private float trainY = 80f;

    [SerializeField] private AudioSource transitionAudio;
    [SerializeField] private CompanyF.UI.TutorialVideoController tutorialVideo;
    private bool startInputArmed;

    // Prevent repeated input / 入力の連続受付を防止
    private bool isStarting = false;

    private void Start()
    {
        // Hide transition train at startup / 起動時は転場用の列車を非表示
        if (transitionTrain != null)
        {
            transitionTrain.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (isStarting)
            return;

        bool held = (Keyboard.current != null && Keyboard.current.enterKey.isPressed)
            || (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);
        if (!startInputArmed)
        {
            if (!held) startInputArmed = true;
            return;
        }

        bool keyboardStart =
            Keyboard.current != null &&
            Keyboard.current.enterKey.wasPressedThisFrame;

        bool gamepadStart =
            Gamepad.current != null &&
            Gamepad.current.buttonSouth.wasPressedThisFrame;

        if (keyboardStart || gamepadStart)
        {
            StartGame();
        }
    }

    // Start game / ゲーム開始
    private void StartGame()
    {
        if (isStarting)
            return;

        isStarting = true;

        // Stop blinking UI if needed / 必要なら点滅UIを止める
        if (gameStartRoot != null)
        {
            // If there is an Animator, disable it / Animatorがある場合は停止
            Animator animator = gameStartRoot.GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = false;
            }
        }

        StartCoroutine(PlayTransitionAndLoad());
    }

    // Play transition, then load next scene / 転場演出の後に次のシーンへ移動
    private IEnumerator PlayTransitionAndLoad()
    {
        if (transitionAudio != null && transitionAudio.clip != null) transitionAudio.Play();
        if (transitionTrain != null)
        {
            transitionTrain.gameObject.SetActive(true);

            Vector2 startPos = new Vector2(trainStartX, trainY);
            Vector2 endPos = new Vector2(trainEndX, trainY);

            transitionTrain.anchoredPosition = startPos;

            float timer = 0f;

            while (timer < transitionDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / transitionDuration);

                transitionTrain.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

                yield return null;
            }

            transitionTrain.anchoredPosition = endPos;
        }

        if (tutorialVideo != null) tutorialVideo.Begin(nextSceneName);
        else SceneManager.LoadScene(nextSceneName);
    }
}