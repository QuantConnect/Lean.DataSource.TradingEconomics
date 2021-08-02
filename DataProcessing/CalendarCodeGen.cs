/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using QuantConnect.Configuration;
using QuantConnect.DataSource;
using QuantConnect.Logging;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace QuantConnect.DataProcessing
{
    public class CalendarCodeGen : BaseCodeGen
    {
        public CalendarCodeGen()
        {
            _codegenOutputPath = Config.Get("trading-economics-calendar-codegen-output-path", Path.Combine("..", "..", "..", "..", "Lean", "Common", "Data", "Custom", "TradingEconomics", "TradingEconomics.Calendar.cs"));
            _qcHeader.Add("namespace QuantConnect.DataSource.TradingEconomics");
            _qcHeader.Add("{");
            _qcHeader.Add("    /// <summary>");
            _qcHeader.Add("    /// TradingEconomics static class contains shortcut definitions of major Trading Economics Indicators available");
            _qcHeader.Add("    /// </summary>");
            _qcHeader.Add("    public static partial class TradingEconomics");
            _qcHeader.Add("    {");
            _qcHeader.Add("        public static class Calendar");
            _qcHeader.Add("        {");
        }

        /// <summary>
        /// Codegen the <see cref="TradingEconomicsCalendarEvents"/> static class and replace the existing file.
        /// </summary>
        /// <param name="calendars">List of <see cref="TradingEconomicsCalendar"/> that have been parsed from the API response</param>
        /// <returns>Boolean indicating success or failure</returns>
        public override bool TryCodeGen(List<TradingEconomicsCalendar> calendars)
        {
            var calendarEntries = new List<CalendarEntry>();
            var finalFile = new FileInfo(_codegenOutputPath);
            var info = CultureInfo.InvariantCulture.TextInfo;

            // Group by country first, since we're going to be generating one class at a time.
            // In addition to that and any subsequent loops, we apply order bys to get as deterministic
            // output as we can possibly get.
            foreach (var countryGroup in calendars.GroupBy(x => x.Country.ToLowerInvariant()).OrderBy(x => x.Key))
            {
                var countryCalendar = new CalendarEntry(info.ToTitleCase(countryGroup.Key));

                // There are some corrupted entries that have no ticker. Let's get rid of those
                foreach (var tickerGroup in countryGroup.Where(x => !string.IsNullOrEmpty(x.Ticker)).GroupBy(x => x.Ticker.ToLowerInvariant()).OrderBy(x => x.Key))
                {
                    // In the case of `japanleacomind`, there are two categories that appear:
                    // * Leading Composite Index @ ~200
                    // * Leading Economic Index @ ~300
                    // -------
                    // This means that because multiple categories might be present with the same ticker, we must
                    // add them in separately and avoid from preferring one or the other.
                    foreach (var categoryGroup in tickerGroup.GroupBy(x => x.Category.ToLowerInvariant()).OrderBy(x => x.Key))
                    {
                        var category = categoryGroup.First().Category;
                        var fieldName = Normalize(category).Replace("-", "");
                        var ticker = Normalize(countryCalendar.Country.ToLowerInvariant().Replace(' ', '-')) + TradingEconomics.Calendar.Delimiter + tickerGroup.Key.ToUpperInvariant().Replace(' ', '-');

                        countryCalendar.Add(category, fieldName, ticker);
                    }
                }

                calendarEntries.Add(countryCalendar);
            }

            var finalFileContents = new List<string>();

            Log.Trace("TradingEconomicsCalendarCodegen.TryCodeGen(): Writing new file...");
            finalFileContents.InsertRange(0, _qcHeader);

            finalFileContents.AddRange(CreateCode(calendarEntries, definition => definition.FieldName != "Calendar" && definition.Value.Contains("-CALENDAR")));

            finalFileContents.Add("        }");
            finalFileContents.Add("    }");
            finalFileContents.Add("}");
            return TryWriteCodeGen(finalFileContents, finalFile);
        }
    }
}
