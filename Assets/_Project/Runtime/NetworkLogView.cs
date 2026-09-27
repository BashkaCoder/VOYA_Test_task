using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Voya.Battleship
{
    public class NetworkLogView : MonoBehaviour
    {
        [SerializeField] private Text _text;
        private List<string> _lines;
        private int _maxLines;

        public void Configure(int maxLines)
        {
            _maxLines = maxLines;
            _lines = new List<string>();
        }

        public void Append(string line)
        {
            _lines.Add(line);
            while (_lines.Count > _maxLines) _lines.RemoveAt(0);
            _text.text = string.Join(System.Environment.NewLine, _lines);
        }
    }
}
