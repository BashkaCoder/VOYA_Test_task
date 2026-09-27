using System;
using UnityEngine;

namespace Voya.Battleship.Configuration
{
    [Serializable]
    public class BoardPalette
    {
        [SerializeField]
        private Color _unknown;
        [SerializeField]
        private Color _ship;
        [SerializeField]
        private Color _miss;
        [SerializeField]
        private Color _hit;
        [SerializeField]
        private Color _pending;
        public Color Unknown => _unknown;
        public Color Ship => _ship;
        public Color Miss => _miss;
        public Color Hit => _hit;
        public Color Pending => _pending;
    }
}
