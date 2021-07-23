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
using QuantConnect.Data;
using QuantConnect.DataSource;
using QuantConnect.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace QuantConnect.DataProcessing
{
    public class CodeGen
    {
        public static int Run()
        {
            if (string.IsNullOrEmpty(Config.Get("trading-economics-calendar-codegen-output-path")))
            {
                throw new ArgumentException("No value was found for required config parameter \"trading-economics-calendar-codegen-output-path\"");
            }
            if (string.IsNullOrEmpty(Config.Get("trading-economics-events-codegen-output-path")))
            {
                throw new ArgumentException("No value was found for required config parameter \"trading-economics-events-codegen-output-path\"");
            }

            var stopwatch = Stopwatch.StartNew();
            Log.Trace($"Begin codegen of TradingEconomics classes");

            var dataPath = new DirectoryInfo(Path.Combine(Globals.DataFolder, "alternative", "trading-economics", "calendar"));
            var data = new List<TradingEconomicsCalendar>();
            var factory = new TradingEconomicsCalendar();

            foreach (var file in dataPath.GetFiles("*.csv"))
            {
                var contents = File.ReadLines(file.FullName).ToList();
                var tickers = contents.Select(x => x.Split(',')[2]).ToList();
                var i = 0;

                foreach (var line in contents)
                {
                    var config = new SubscriptionDataConfig(
                        typeof(TradingEconomicsCalendar),
                        Symbol.Create($"Foobar//{tickers[i++].Replace(' ', '-')}", SecurityType.Base, Market.USA),
                        Resolution.Tick,
                        TimeZones.Utc,
                        TimeZones.Utc,
                        false,
                        false,
                        false,
                        true);

                    data.Add((TradingEconomicsCalendar)factory.Reader(config, line, DateTime.MinValue, false));
                }
            }

            var calendarGenerator = new CalendarCodeGen();
            var calendarEventGenerator = new CalendarEventCodeGen();

            var status = (calendarGenerator.TryCodeGen(data) && calendarEventGenerator.TryCodeGen(data)) ? 0 : 1;
            Log.Trace($"Finished codegen of TradingEconomics Calendar classes in {stopwatch.Elapsed:g}");

            return status;
        }
    }
}
