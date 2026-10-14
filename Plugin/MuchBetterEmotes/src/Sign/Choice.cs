using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuchBetterEmotes;

internal sealed class Choice
{
    private const string SureText = "Sure?";

    private readonly TMP_Text _label;
    private readonly GameObject _light;
    private readonly string _text;
    private bool _armed;
    private bool _shown;
    private float _armTime;

    public Choice(Transform content, string path, string text)
    {
        Button = content.Find(path).GetComponent<Button>();
        _label = content.Find($"{path}/Label").GetComponent<TMP_Text>();
        _light = content.Find($"{path}/Highlight").gameObject;
        _text = text;
    }

    public Button Button { get; }

    public bool Press(float now, bool confirm)
    {
        bool done = !confirm || IsArmed(now);
        _armed = !done;
        if (_armed) _armTime = now;
        Refresh(now);
        return done;
    }

    public void Refresh(float now)
    {
        bool armed = IsArmed(now);
        if (armed == _shown) return;

        _shown = armed;
        _label.text = armed ? SureText : _text;
        _light.SetActive(armed);
    }

    public void Reset()
    {
        _armed = false;
        _shown = false;
        _label.text = _text;
        _light.SetActive(false);
    }

    private bool IsArmed(float now) => _armed && now - _armTime <= Constants.Panel.Confirm;
}
