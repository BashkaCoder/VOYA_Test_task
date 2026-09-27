using System;
using UnityEngine;
using UnityEngine.UI;

namespace Voya.Battleship
{
    public class CellView : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private Text _mark;
        [SerializeField] private Button _button;

        public void Bind(Action click)
        {
            _button.onClick.RemoveAllListeners();
            if (click != null) _button.onClick.AddListener(() => click());
        }

        public void Render(Color color, string mark, bool enabled)
        {
            _image.color = color;
            _mark.text = mark;
            _button.interactable = enabled;
        }
    }
}
