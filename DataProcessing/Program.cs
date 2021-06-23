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

using QuantConnect;
using QuantConnect.Configuration;
using QuantConnect.DataSource;
using QuantConnect.Logging;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace QuantConnect.DataProcessing
{
    /// <summary>
    /// Console program to convert from raw Trading Economics data to a formatted form usable by LEAN
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            // N.B. - Trading Economics requires that you have the two following values defined in the config.json file:
            // trading-economics-auth-id
            // trading-economics-auth-token

	    var processingDateValue = Environment.GetEnvironmentVariable("QC_DATAFLEET_DEPLOYMENT_DATE");
            var processingDate = DateTime.ParseExact(processingDateValue, "yyyyMMdd", CultureInfo.InvariantCulture);
            var temporaryFolder = Config.Get("temp-output-directory", "/temp-output-directory");
            var dataFolder = Path.Combine(temporaryFolder, "alternative", "trading-economics");

            Log.Trace($"DataProcessing.Main(): Processing {processingDate:yyyy-MM-dd}");
            Log.Trace("DataProcessing.Main(): Begin downloading Calendar data");

            var timer = Stopwatch.StartNew();

            // TradingEconomicsCalendarDownloader constructor creates the dataFolder for us
            var calendarDownloader = new TradingEconomicsCalendarDownloader(dataFolder);
            if (!calendarDownloader.Run())
            {
                Log.Error("DataProcessing.Main(): Calendar download or write to disk failed");
                Environment.Exit(1);
            }

            timer.Stop();
            Log.Trace($"DataProcessing.Main(): Finished downloading Calendar data in {timer.Elapsed.TotalMinutes} minutes");

            Environment.Exit(0);
        }
    }
}
