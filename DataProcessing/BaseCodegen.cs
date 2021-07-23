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

using QuantConnect.DataSource;
using QuantConnect.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace QuantConnect.DataProcessing
{
    public abstract class BaseCodeGen
    {
        protected string _codegenOutputPath;
        protected readonly static string[] _unitsMap = new[] { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
        protected readonly static string[] _tensMap = new[] { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };
        protected readonly List<string> _qcHeader = new List<string>
        {
            {"/*"},
            {" * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals."},
            {" * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation."},
            {" *"},
            {" * Licensed under the Apache License, Version 2.0 (the \"License\");"},
            {" * you may not use this file except in compliance with the License."},
            {" * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0"},
            {" *"},
            {" * Unless required by applicable law or agreed to in writing, software"},
            {" * distributed under the License is distributed on an \"AS IS\" BASIS,"},
            {" * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied."},
            {" * See the License for the specific language governing permissions and"},
            {" * limitations under the License."},
            {"*/"},
            {""},
        };

        /// <summary>
        /// Attempt to codegen a class based on TradingEconomics Calendar data
        /// </summary>
        /// <param name="calendars">List of calendar data</param>
        /// <returns>Boolean indicating success</returns>
        public abstract bool TryCodeGen(List<TradingEconomicsCalendar> calendars);

        /// <summary>
        /// Creates the code to insert in the <see cref="TradingEconomics.Calendar"/> class
        /// </summary>
        /// <param name="calendarEntries">Calendar metadata</param>
        /// <param name="filter">Custom calendar definition filter</param>
        /// <returns>Class contents</returns>
        protected virtual List<string> CreateCode(IEnumerable<CalendarEntry> calendarEntries, Func<CalendarEntry.CalendarDefinition, bool> filter = null)
        {
            var newCalendarCode = new List<string>();

            foreach (var entry in calendarEntries.OrderBy(x => x.Country))
            {
                var country = Normalize(entry.Country);
                var existingDeclarations = new Dictionary<string, string>();

                newCalendarCode.Add("            /// <summary>");
                newCalendarCode.Add($"            /// {entry.Country}");
                newCalendarCode.Add("            /// </summary>");
                newCalendarCode.Add($"            public static class {country}");
                newCalendarCode.Add("            {");

                foreach (var definition in entry.Definitions.OrderBy(x => x.FieldName).ThenBy(x => x.Value))
                {
                    // Detect lines that we've already added.
                    if (existingDeclarations.ContainsKey(definition.FieldName) || filter != null && filter(definition))
                    {
                        continue;
                    }

                    newCalendarCode.Add("                /// <summary>");
                    newCalendarCode.Add($"                /// {definition.Summary}");
                    newCalendarCode.Add("                /// </summary>");
                    newCalendarCode.Add($"                public const string {definition.FieldName} = \"{definition.Value}\";");

                    existingDeclarations[definition.FieldName] = definition.Value;
                }

                newCalendarCode.Add("            }");
            }

            return newCalendarCode;
        }

        /// <summary>
        /// Writes the codegen to the final file
        /// </summary>
        /// <param name="finalFileContents">Final file contents to write to disk</param>
        /// <param name="finalFile">Final file to write to</param>
        /// <returns>Boolean indicating success or failure</returns>
        protected static bool TryWriteCodeGen(List<string> finalFileContents, FileInfo finalFile)
        {
            var tempFile = new FileInfo(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.cs"));
            var finalFileBackup = new FileInfo(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.cs"));

            try
            {
                Log.Trace($"TradingEconomicsCalendarEventCodegen.TryWriteCodeGen(): Writing contents to temp file: {tempFile.FullName}");
                File.WriteAllLines(tempFile.FullName, finalFileContents);

                if (finalFile.Exists)
                {
                    Log.Trace($"TradingEconomicsCalendarEventCodegen.TryWriteCodeGen(): Moving existing file to backup location: {finalFileBackup.FullName}");
                    File.Move(finalFile.FullName, finalFileBackup.FullName);
                }

                Log.Trace($"TradingEconomicsCalendarEventCodegen.TryWriteCodeGen(): Moving temp file: {tempFile.FullName} - to final path: {finalFile.FullName}");
                File.Move(tempFile.FullName, finalFile.FullName);

                Log.Trace($"TradingEconomicsCalendarEventCodegen.TryWriteCodeGen(): Deleting backup file: {finalFileBackup.FullName}");
                File.Delete(finalFileBackup.FullName);
            }
            catch (Exception err)
            {
                Log.Error(err, "Attempting backup recovery...");

                tempFile.Refresh();
                finalFileBackup.Refresh();
                finalFile.Refresh();

                if (tempFile.Exists)
                {
                    Log.Error($"TradingEconomicsCalendarEventCodegen.TryWriteCodeGen(): Deleting temp file {tempFile.FullName}");
                    File.Delete(tempFile.FullName);
                }
                if (!finalFile.Exists && finalFileBackup.Exists)
                {
                    Log.Error($"TradingEconomicsCalendarEventCodegen.TryWriteCodeGen(): Recovering from backup: {finalFileBackup.FullName}");
                    File.Move(finalFileBackup.FullName, finalFile.FullName);
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// Normalizes the field name to a variable name usable by C#
        /// </summary>
        /// <param name="word">Word to normalize</param>
        /// <returns>Normalized string. Numbers are converted to words</returns>
        public static string Normalize(string word)
        {
            var numberRegex = new Regex(@"[0-9]+");
            var info = CultureInfo.InvariantCulture.TextInfo;
            word = info.ToTitleCase(word);
            word = numberRegex.Replace(word, new MatchEvaluator(x => info.ToTitleCase(NumberToWords(Parse.Long(x.Value)))));

            return Regex.Replace(word, @"[^a-zA-Z\-]", "");
        }

        /// <summary>
        /// Converts numbers to words
        /// </summary>
        /// <param name="number">Number to convert</param>
        /// <returns>Word version of the number</returns>
        /// <remarks>
        /// Source: https://stackoverflow.com/a/2730393
        /// Author: @LukeH
        /// </remarks>
        public static string NumberToWords(long number)
        {
            if (number == 0)
                return "Zero";

            if (number < 0)
                return "Negative " + NumberToWords(Math.Abs(number));

            string words = "";

            if ((number / 1000000000000) > 0)
            {
                words += NumberToWords(number / 1000000000000) + " Trillion ";
                number %= 1000000000000;
            }

            if ((number / 1000000000) > 0)
            {
                words += NumberToWords(number / 1000000000) + " Billion ";
                number %= 1000000000;
            }

            if ((number / 1000000) > 0)
            {
                words += NumberToWords(number / 1000000) + " Million ";
                number %= 1000000;
            }

            if ((number / 1000) > 0)
            {
                words += NumberToWords(number / 1000) + " Thousand ";
                number %= 1000;
            }

            if ((number / 100) > 0)
            {
                words += NumberToWords(number / 100) + " Hundred ";
                number %= 100;
            }

            if (number > 0)
            {
                if (words != "")
                    words += " ";


                if (number < 20)
                    words += _unitsMap[number];
                else
                {
                    words += _tensMap[number / 10];
                    if ((number % 10) > 0)
                        words += " " + _unitsMap[number % 10];
                }
            }

            return words;
        }

        /// <summary>
        /// Converts the given event name to a field
        /// </summary>
        /// <param name="eventName">TE Event name</param>
        /// <returns>Field name of event</returns>
        protected static string ToFieldName(string eventName)
        {
            Func<string, string, string, string> SingleWordFilter = (x, y, z) =>
            {
                return (string)typeof(TradingEconomicsEventFilter)
                    .GetMethod("SingleWordFilter", BindingFlags.NonPublic | BindingFlags.InvokeMethod | BindingFlags.Static)
                    .Invoke(null, new object[] { x, y, z });
            };

            var info = CultureInfo.InvariantCulture.TextInfo;

            eventName = SingleWordFilter(eventName, "3m", "three months");
            eventName = eventName.Replace("three months three months", "3m 3m");
            eventName = SingleWordFilter(eventName, "1y", "one year");
            eventName = SingleWordFilter(eventName, "2y", "two year");
            eventName = SingleWordFilter(eventName, "5y", "five year");
            eventName = SingleWordFilter(eventName, "10y", "ten year");
            eventName = SingleWordFilter(eventName, "20y", "twenty year");
            eventName = SingleWordFilter(eventName, "30y", "thirty year");
            eventName = SingleWordFilter(eventName, "50y", "fifty year");
            eventName = SingleWordFilter(eventName, "wow", "WoW");
            eventName = SingleWordFilter(eventName, "mom", "MoM");
            eventName = SingleWordFilter(eventName, "qoq", "QoQ");
            eventName = SingleWordFilter(eventName, "yoy", "YoY");

            var fieldName = string.Join(" ", eventName.Split(' ').Select(x => x != "WoW" && x != "MoM" && x != "QoQ" && x != "YoY" ? info.ToTitleCase(x) : x));
            return Regex.Replace(fieldName, @"[0-9]+", new MatchEvaluator(x => NumberToWords(Parse.Long(x.Value)))).Replace(" ", "");
        }

        /// <summary>
        /// Contains metadata to construct a <see cref="TradingEconomics.Calendar"/> subclass and subclass fields
        /// </summary>0
        protected class CalendarEntry
        {
            /// <summary>
            /// Country
            /// </summary>
            public readonly string Country;

            /// <summary>
            /// Field definition. This list will contain the data necessary to generate a field entry in a given country's class.
            /// </summary>
            public readonly List<CalendarDefinition> Definitions;

            /// <summary>
            /// Creates an instance of the class
            /// </summary>
            /// <param name="country">Country</param>
            public CalendarEntry(string country)
            {
                Country = country;
                Definitions = new List<CalendarDefinition>();
            }

            /// <summary>
            /// Add a new category and ticker as a definition for this instance
            /// </summary>
            /// <param name="category">TE Calendar Category</param>
            /// <param name="ticker">TE Calendar ticker</param>
            public void Add(string summary, string fieldName, string value)
            {
                Definitions.Add(new CalendarDefinition(summary, fieldName, value));
            }

            /// <summary>
            /// Internal class used to contain metadata for a field
            /// </summary>
            public class CalendarDefinition
            {
                /// <summary>
                /// Entry summary
                /// </summary>
                public readonly string Summary;

                /// <summary>
                /// Field name to use
                /// </summary>
                public readonly string FieldName;

                /// <summary>
                /// string value to use
                /// </summary>
                public readonly string Value;

                /// <summary>
                /// Create an instance of the object
                /// </summary>
                /// <param name="summary">Summary of the new entry</param>
                /// <param name="fieldName">Field name of the entry</param>
                /// <param name="value">string value of the entry</param>
                public CalendarDefinition(string summary, string fieldName, string value)
                {
                    Summary = summary;
                    FieldName = fieldName;
                    Value = value;
                }
            }
        }
    }
}
