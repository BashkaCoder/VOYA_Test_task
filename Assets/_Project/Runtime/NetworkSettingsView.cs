using UnityEngine;
using UnityEngine.UI;

namespace Voya.Battleship
{
    public class NetworkSettingsView : MonoBehaviour
    {
        private const float PercentScale = 100f;
        [SerializeField] private Slider _latency;
        [SerializeField] private Slider _jitter;
        [SerializeField] private Slider _loss;
        [SerializeField] private Slider _duplication;
        [SerializeField] private Text _latencyLabel;
        [SerializeField] private Text _jitterLabel;
        [SerializeField] private Text _lossLabel;
        [SerializeField] private Text _duplicationLabel;

        public void Bind(NetworkProfile profile, NetworkSettingsLimits limits)
        {
            _latency.maxValue = limits.LatencyMaxMs;
            _jitter.maxValue = limits.JitterMaxMs;
            _latency.SetValueWithoutNotify(profile.LatencyMs);
            _jitter.SetValueWithoutNotify(profile.JitterMs);
            _loss.SetValueWithoutNotify(profile.Loss * PercentScale);
            _duplication.SetValueWithoutNotify(profile.Duplication * PercentScale);
            _latency.onValueChanged.AddListener(value => { profile.LatencyMs = (int)value; UpdateLabels(); });
            _jitter.onValueChanged.AddListener(value => { profile.JitterMs = (int)value; UpdateLabels(); });
            _loss.onValueChanged.AddListener(value => { profile.Loss = value / PercentScale; UpdateLabels(); });
            _duplication.onValueChanged.AddListener(value => { profile.Duplication = value / PercentScale; UpdateLabels(); });
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            _latencyLabel.text = string.Format(UiText.SliderFormat, UiText.Latency, _latency.value);
            _jitterLabel.text = string.Format(UiText.SliderFormat, UiText.Jitter, _jitter.value);
            _lossLabel.text = string.Format(UiText.SliderFormat, UiText.Loss, _loss.value);
            _duplicationLabel.text = string.Format(UiText.SliderFormat, UiText.Duplication, _duplication.value);
        }
    }
}
