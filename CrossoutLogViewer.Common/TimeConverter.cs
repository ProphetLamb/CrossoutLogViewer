﻿using System;
using System.Globalization;

namespace CrossoutLogView.Common
{
    public static class TimeConverter
    {
        /// <summary>
        /// Returns the ticks of the <see cref="DateTime"/> obtained by combining the log date with the line timestamp.
        /// </summary>
        /// <param name="lineTimeStamp">The <see cref="String"/> containing the hour, minute, second and millisecond data of the line.</param>
        /// <param name="logDate">The <see cref="DateTime"/> containing the year, month and day data.</param>
        /// <returns>The ticks of the <see cref="DateTime"/> obtained by combining the log date with the line timestamp.</returns>
        public static long FromString(ReadOnlySpan<char> lineTimeStamp, DateTime logDate)
        {
            int hour, minute, second, millisecond;
            Int32.TryParse(lineTimeStamp[0..2], NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out hour);
            Int32.TryParse(lineTimeStamp[3..5], NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out minute);
            Int32.TryParse(lineTimeStamp[6..8], NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out second);
            Int32.TryParse(lineTimeStamp[9..12], NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out millisecond);
            var dateTime = new DateTime(logDate.Year, logDate.Month, logDate.Day, hour, minute, second, millisecond);
            return dateTime.Ticks;
        }
    }
}
