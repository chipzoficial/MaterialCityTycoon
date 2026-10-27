using System;

namespace MaterialCityTycoon.Building
{
    /// <summary>Stable slot convention for the 5 by 5 parts-bin organizer.</summary>
    public static class PartsBinRackSlotLayout
    {
        public const int Rows = 5;
        public const int Columns = 5;

        public static string GetSlotId(int rowIndex, int columnIndex)
        {
            ValidateIndexes(rowIndex, columnIndex);
            return $"bin_r{rowIndex + 1:D2}_c{columnIndex + 1:D2}";
        }

        public static string GetDisplayName(int rowIndex, int columnIndex)
        {
            ValidateIndexes(rowIndex, columnIndex);
            return $"Fileira {rowIndex + 1} — Caixa {columnIndex + 1}";
        }

        private static void ValidateIndexes(int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= Rows || columnIndex < 0 || columnIndex >= Columns)
                throw new ArgumentOutOfRangeException(rowIndex < 0 || rowIndex >= Rows ? nameof(rowIndex) : nameof(columnIndex));
        }
    }
}
