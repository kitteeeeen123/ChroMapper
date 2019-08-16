﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PauseManager : MonoBehaviour {
    [SerializeField]
    private CanvasGroup loadingCanvasGroup;
    [SerializeField]
    private CanvasGroup advancedSettingsCanvasGroup;
    [SerializeField]
    private CanvasGroup marketingTextCanvasGroup;
    [SerializeField]
    private AnimationCurve fadeInCurve;
    [SerializeField]
    private AnimationCurve fadeOutCurve;
    private PlatformDescriptor platform;

    public static bool IsPaused = false;
    private bool ShowsHelpText = true;

    void Start()
    {
        LoadInitialMap.PlatformLoadedEvent += PlatformLoaded;
    }

    void PlatformLoaded(PlatformDescriptor descriptor)
    {
        platform = descriptor;
    }

    public void TogglePause()
    {
        IsPaused = !IsPaused;
        if (IsPaused)
        {
            foreach (LightingEvent e in platform.gameObject.GetComponentsInChildren<LightingEvent>())
                e.ChangeAlpha(0, 1);
            OptionsController.Find<OptionsController>()?.Close();
        }
        StartCoroutine(TransitionMenu());
    }

    public void ToggleLeftPanel()
    {
        ShowsHelpText = !ShowsHelpText;
        if (ShowsHelpText)
        {
            StartCoroutine(FadeInLoadingScreen(2f, marketingTextCanvasGroup));
            StartCoroutine(FadeOutLoadingScreen(2f, advancedSettingsCanvasGroup));
        }
        else
        {
            StartCoroutine(FadeInLoadingScreen(2f, advancedSettingsCanvasGroup));
            StartCoroutine(FadeOutLoadingScreen(2f, marketingTextCanvasGroup));
        }
    }

    void OnDestroy()
    {
        IsPaused = false;
        LoadInitialMap.PlatformLoadedEvent -= PlatformLoaded;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
    }

    public void Quit(bool save)
    {
        if (save)
        {
            if (BeatSaberSongContainer.Instance.map.Save()) PersistentUI.Instance.DisplayMessage("Map Saved!", PersistentUI.DisplayMessageType.BOTTOM);
            else
            {
                PersistentUI.Instance.DisplayMessage("Error Saving Map!", PersistentUI.DisplayMessageType.BOTTOM);
                return;
            }
        }
        SceneTransitionManager.Instance.LoadScene(2);
    }

    #region Transitions (Totally not ripped from PersistentUI)
    IEnumerator TransitionMenu()
    {
        if (IsPaused) yield return FadeInLoadingScreen(loadingCanvasGroup);
        else yield return FadeOutLoadingScreen(loadingCanvasGroup);
    }

    public Coroutine FadeInLoadingScreen(CanvasGroup group)
    {
        return StartCoroutine(FadeInLoadingScreen(2f, loadingCanvasGroup));
    }

    IEnumerator FadeInLoadingScreen(float rate, CanvasGroup group)
    {
        group.blocksRaycasts = true;
        group.interactable = true;
        float t = 0;
        while (t < 1)
        {
            group.alpha = fadeInCurve.Evaluate(t);
            t += Time.deltaTime * rate;
            yield return null;
        }
        group.alpha = 1;
    }

    public Coroutine FadeOutLoadingScreen(CanvasGroup group)
    {
        return StartCoroutine(FadeOutLoadingScreen(2f, group));
    }

    IEnumerator FadeOutLoadingScreen(float rate, CanvasGroup group)
    {
        float t = 1;
        while (t > 0)
        {
            group.alpha = fadeOutCurve.Evaluate(t);
            t -= Time.deltaTime * rate;
            yield return null;
        }
        group.alpha = 0;
        group.blocksRaycasts = false;
        group.interactable = false;
    }
    #endregion
}
