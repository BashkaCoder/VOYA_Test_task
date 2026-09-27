using System;
using UnityEngine;
using Voya.Battleship.Configuration;
using Voya.Battleship.Domain;
using Voya.Battleship.Protocol;
using Voya.Battleship.Constants;

namespace Voya.Battleship.Presentation
{
    public class BoardView : MonoBehaviour
    {
        [SerializeField]
        private CellView[] _cells;
        public void Bind(Action<int> fire)
        {
            for (int cell = 0; cell < _cells.Length; cell++)
            {
                int index = cell;
                _cells[cell].Bind(() => fire(index));
            }
        }

        public void RenderOwn(Snapshot state, BoardPalette palette)
        {
            for (int cell = 0; cell < _cells.Length; cell++)
            {
                int ship = state?.OwnShips[cell] ?? 0;
                int shot = state?.OwnShots[cell] ?? 0;
                Color color = shot >= (int)ShotMark.Hit ? palette.Hit : shot == (int)ShotMark.Miss ? palette.Miss : ship == 1 ? palette.Ship : palette.Unknown;
                string mark = shot == (int)ShotMark.Miss ? UiText.MissMark : shot >= (int)ShotMark.Hit ? UiText.HitMark : ship == 1 ? UiText.OwnShipMark : string.Empty;
                _cells[cell].Render(color, mark, false);
            }
        }

        public void RenderEnemy(Snapshot state, BoardPalette palette, int pendingCell, bool canFire)
        {
            for (int cell = 0; cell < _cells.Length; cell++)
            {
                int shot = state?.EnemyShots[cell] ?? 0;
                bool pending = pendingCell == cell;
                Color color = pending ? palette.Pending : shot >= (int)ShotMark.Hit ? palette.Hit : shot == (int)ShotMark.Miss ? palette.Miss : palette.Unknown;
                string mark = pending ? UiText.PendingMark : shot == (int)ShotMark.Miss ? UiText.MissMark : shot == (int)ShotMark.Sunk ? UiText.SunkMark : shot == (int)ShotMark.Hit ? UiText.HitMark : string.Empty;
                _cells[cell].Render(color, mark, canFire && shot == 0);
            }
        }
    }
}
