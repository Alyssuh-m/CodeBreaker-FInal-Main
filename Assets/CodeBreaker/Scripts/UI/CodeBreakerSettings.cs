using UnityEngine;

// Code Breaker: player settings shared by the main menu, the in-game Tab menu and gameplay.
// Everything is stored in PlayerPrefs, so it carries over between scenes and game sessions.
// Music / SFX use the same keys AudioManager already used, so existing saved volumes are kept.
public static class CodeBreakerSettings
{
    public const string MasterKey = "CodeBreaker.MasterVolume";
    public const string MusicKey = "CodeBreaker.MusicVolume";      // also read by AudioManager
    public const string SfxKey = "CodeBreaker.SFXVolume";          // also read by AudioManager
    public const string SensitivityKey = "CodeBreaker.MouseSensitivity";
    public const string QualityKey = "CodeBreaker.QualityLevel";

    public const float DefaultMaster = 1f;
    public const float DefaultMusic = 0.6f;    // same defaults as AudioManager
    public const float DefaultSfx = 0.8f;
    public const float DefaultSensitivity = 100f;   // same as MouseMovement's default
    public const float MinSensitivity = 20f;
    public const float MaxSensitivity = 300f;

    // Raised whenever a setting changes (MouseMovement, menu music, etc. listen to it).
    public static event System.Action Changed;

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MasterKey, DefaultMaster);
        set
        {
            float v = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterKey, v);
            AudioListener.volume = v;
            Changed?.Invoke();
        }
    }

    public static float MusicVolume
    {
        get => AudioManager.Instance != null ? AudioManager.Instance.musicVolume : PlayerPrefs.GetFloat(MusicKey, DefaultMusic);
        set
        {
            float v = Mathf.Clamp01(value);
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(v);   // also saves the pref
            else PlayerPrefs.SetFloat(MusicKey, v);
            Changed?.Invoke();
        }
    }

    public static float SfxVolume
    {
        get => AudioManager.Instance != null ? AudioManager.Instance.sfxVolume : PlayerPrefs.GetFloat(SfxKey, DefaultSfx);
        set
        {
            float v = Mathf.Clamp01(value);
            if (AudioManager.Instance != null) AudioManager.Instance.SetSfxVolume(v);
            else PlayerPrefs.SetFloat(SfxKey, v);
            Changed?.Invoke();
        }
    }

    public static float MouseSensitivity
    {
        get => PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
        set
        {
            PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, MinSensitivity, MaxSensitivity));
            Changed?.Invoke();
        }
    }

    // Returns the saved sensitivity, or the given fallback when the player never changed it.
    public static float GetMouseSensitivity(float fallback) =>
        PlayerPrefs.HasKey(SensitivityKey) ? PlayerPrefs.GetFloat(SensitivityKey) : fallback;

    public static int QualityLevel
    {
        get => QualitySettings.GetQualityLevel();
        set
        {
            int q = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(q, true);
            PlayerPrefs.SetInt(QualityKey, q);
            Changed?.Invoke();
        }
    }

    public static void Save() => PlayerPrefs.Save();

    public static void ResetToDefaults()
    {
        MasterVolume = DefaultMaster;
        MusicVolume = DefaultMusic;
        SfxVolume = DefaultSfx;
        MouseSensitivity = DefaultSensitivity;
        Save();
    }

    // Applied once when the game starts, before any scene logic runs.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyOnStartup()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(MasterKey, DefaultMaster);
        if (PlayerPrefs.HasKey(QualityKey))
        {
            int q = PlayerPrefs.GetInt(QualityKey);
            if (q >= 0 && q < QualitySettings.names.Length && q != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(q, true);
        }
    }
}
