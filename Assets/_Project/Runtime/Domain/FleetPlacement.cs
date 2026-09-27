using System;
using System.Collections.Generic;
using Voya.Battleship.Constants;

namespace Voya.Battleship.Domain
{
    public static class FleetPlacement
    {
        public static Board Create(int size, int[] lengths, Random random)
        {
            int[] cells = new int[size * size];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = GameRules.NoShip;
            }

            for (int ship = 0; ship < lengths.Length; ship++)
            {
                int length = lengths[ship];
                List<int[]> choices = new List<int[]>();
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        for (int direction = 0; direction < 2; direction++)
                        {
                            int dx = direction == 0 ? 1 : 0;
                            int dy = direction == 0 ? 0 : 1;
                            if (x + dx * (length - 1) >= size || y + dy * (length - 1) >= size)
                            {
                                continue;
                            }

                            int[] candidate = new int[length];
                            bool free = true;
                            for (int n = 0; n < length; n++)
                            {
                                candidate[n] = (y + dy * n) * size + x + dx * n;
                                if (cells[candidate[n]] >= 0)
                                {
                                    free = false;
                                }
                            }

                            if (free)
                            {
                                choices.Add(candidate);
                            }
                        }
                    }
                }

                if (choices.Count == 0)
                {
                    throw new ArgumentException(RuleText.FleetCannotFit);
                }

                foreach (int cell in choices[random.Next(choices.Count)])
                {
                    cells[cell] = ship;
                }
            }

            return new Board(size, lengths, cells);
        }
    }
}
