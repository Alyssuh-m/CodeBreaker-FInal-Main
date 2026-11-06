using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Code Breaker: the Settings screen of the main menu.
// Every control changes the real setting immediately and it is saved (see CodeBreakerSettings).
//   Master / Music / Sound Effects volume, Mouse Sensitivity, Display Mode, Resolution, Graphics Quality.
public class SettingsPanel : MonoBehaviour
{
    [Header("Audio")]
    public Slider masterSlider;
    public TMP_Text masterValue;
    public Slider musicSlider;
    public TMP_Text musicValue;
    public Slider sfxSlider;
    public TMP_Text sfxValue;

    [Header("Controls")]
    public Slider sensitivitySlider;
    public TMP_Text sensitivityValue;

    [Header("Display")]
    public Toggle fullscreenToggle;
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown qualityDropdown;

    [Header("Buttons")]
    public Button resetButton;

    readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    bool wired;

    void Wire()
    {
        if (wired) return;
        wired = true;
        if (masterSlider != null) masterSlider.onValueChanged.AddListener(v => { CodeBreakerSettings.MasterVolume = v; Refresh(); });
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(v => { CodeBreakerSettings.MusicVolume = v; Refresh(); });
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(v => { CodeBreakerSettings.SfxVolume = v; Refresh(); });
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = CodeBreakerSettings.MinSensitivity;
            sensitivitySlider.maxValue = CodeBreakerSettings.MaxSensitivity;
            sensitivitySlider.onValueChanged.AddListener(v => { CodeBreakerSettings.MouseSensitivity = Mathf.Round(v); Refresh(); });
        }
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(SetResolution);
        if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(q => CodeBreakerSettings.QualityLevel = q);
        if (resetButton != null) resetButton.onClick.AddListener(() => { CodeBreakerSettings.ResetToDefaults(); Load(); });
    }

    void OnEnable()
    {
        Wire();
        Load();
    }

    void OnDisable() => CodeBreakerSettings.Save();

    // Puts the current values on the controls without triggering their callbacks.
    void Load()
    {
        if (masterSlider != null) masterSlider.SetValueWithoutNotify(CodeBreakerSettings.MasterVolume);
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(CodeBreakerSettings.MusicVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(CodeBreakerSettings.SfxVolume);
        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(CodeBreakerSettings.GetMouseSensitivity(CodeBreakerSettings.DefaultSensitivity));
        if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreenMode != FullScreenMode.Windowed);

        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            qualityDropdown.SetValueWithoutNotify(QualitySettings.GetQualityLevel());
            qualityDropdown.RefreshShownValue();
        }

        if (resolutionDropdown != null)
        {
            resolutions.Clear();
            foreach (Resolution r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (!resolutions.Contains(v) && r.width >= 800) resolutions.Add(v);
            }
            var current = new Vector2Int(Screen.width, Screen.height);
            if (!resolutions.Contains(current)) resolutions.Add(current);
            resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            var labels = new List<string>();
            foreach (Vector2Int r in resolutions) labels.Add(r.x + " x " + r.y + "  (" + Aspect(r) + ")");
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(labels);
            resolutionDropdown.SetValueWithoutNotify(resolutions.IndexOf(current));
            resolutionDropdown.RefreshShownValue();
        }
        Refresh();
    }

    void Refresh()
    {
        if (masterValue != null) masterValue.text = Pct(CodeBreakerSettings.MasterVolume);
        if (musicValue != null) musicValue.text = Pct(CodeBreakerSettings.MusicVolume);
        if (sfxValue != null) sfxValue.text = Pct(CodeBreakerSettings.SfxVolume);
        if (sensitivityValue != null) sensitivityValue.text = Mathf.RoundToInt(CodeBreakerSettings.GetMouseSensitivity(CodeBreakerSettings.DefaultSensitivity)).ToString();
    }

    static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

    static string Aspect(Vector2Int r)
    {
        float a = (float)r.x / r.y;
        if (Mathf.Abs(a - 16f / 9f) < 0.02f) return "16:9";
        if (Mathf.Abs(a - 16f / 10f) < 0.02f) return "16:10";
        if (Mathf.Abs(a - 4f / 3f) < 0.02f) return "4:3";
        if (Mathf.Abs(a - 21f / 9f) < 0.06f) return "21:9";
        return a.ToString("0.00") + ":1";
    }

    void SetFullscreen(bool on)
    {
        Screen.fullScreenMode = on ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    void SetResolution(int index)
    {
        if (index < 0 || index >= resolutions.Count) return;
        Vector2Int r = resolutions[index];
        Screen.SetResolution(r.x, r.y, Screen.fullScreenMode);
    }
}
