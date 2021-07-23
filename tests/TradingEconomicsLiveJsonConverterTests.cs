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

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuantConnect.DataProcessing;
using QuantConnect.DataSource;
using QuantConnect.DataSource.DataQueueHandlers;

namespace QuantConnect.DataLibrary.Tests
{
    [TestFixture]
    public class TradingEconomicsLiveJsonConverterTests
    {
        [Test, TestCaseSource(nameof(GetLiveCalendarEventTestCases))]
        public void DeserializesLiveCalendarEvent(LiveCalendarEventTestParameters parameters)
        {
            var converter = new TradingEconomicsLiveJsonConverter();

            var result = JsonConvert.DeserializeObject<TradingEconomicsCalendar>(parameters.Content, converter);

            Assert.AreEqual(parameters.Country, result.Country);
            Assert.AreEqual(parameters.Category, result.Category);
            Assert.AreEqual(parameters.Importance, result.Importance);
            Assert.AreEqual(parameters.Event, result.Event);
            Assert.AreEqual(parameters.Actual, result.Actual);
            Assert.AreEqual(parameters.Previous, result.Previous);
            Assert.AreEqual(parameters.LastUpdate, result.LastUpdate);
            Assert.AreEqual(parameters.LastUpdate, result.Time);
            Assert.AreEqual(parameters.LastUpdate, result.EndTime);
            Assert.AreEqual(parameters.Symbol, result.Symbol.Value);
        }

        private static TestCaseData[] GetLiveCalendarEventTestCases()
        {
            return new List<LiveCalendarEventTestParameters>
            {
                new LiveCalendarEventTestParameters
                {
                    Content = @"{""event"":""Loan Prime Rate 1Y"",""country"":""China"",""category"":""Interest Rate"",""ticker"":""CHLR12M"",""actual"":""4.05%"",""previous"":""4.05%"",""revised"":null,""date"":""2020-03-20T01:30:00"",""referenceDate"":""2020-03-20T00:00:00"",""reference"":"""",""calendarId"":229704,""importance"":3,""teforecast"":""3.95%"",""forecast"":null,""symbol"":""CHLR12M"",""source"":""People's Bank of China"",""topic"":""calendar""}",
                    Country = "China",
                    Category = "Interest Rate",
                    Importance = TradingEconomicsImportance.High,
                    Event = "loan prime rate 1y",
                    Actual = 0.0405m,
                    Previous = 0.0405m,
                    LastUpdate = new DateTime(2020, 3, 20, 1, 30, 0),
                    Symbol = QuantConnect.Data.Custom.TradingEconomics.TradingEconomics.Calendar.China.InterestRate
                },
                new LiveCalendarEventTestParameters
                {
                    Content = @"{""event"":""Wholesale Prices MoM"",""country"":""Ireland"",""category"":""Producer Prices"",""ticker"":""IRELANDPROPRI"",""actual"":""2.5%"",""previous"":""-1%"",""revised"":null,""date"":""2020-03-20T11:00:00"",""referenceDate"":""2020-02-29T00:00:00"",""reference"":""Feb"",""calendarId"":236561,""importance"":1,""teforecast"":null,""forecast"":null,""topic"":""calendar""}",
                    Country = "Ireland",
                    Category = "Producer Prices",
                    Importance = TradingEconomicsImportance.Low,
                    Event = "wholesale prices mom",
                    Actual = 0.025m,
                    Previous = -0.01m,
                    LastUpdate = new DateTime(2020, 3, 20, 11, 0, 0),
                    Symbol = QuantConnect.Data.Custom.TradingEconomics.TradingEconomics.Calendar.Ireland.ProducerPrices
                },
                new LiveCalendarEventTestParameters
                {
                    Content = @"{""event"":""PPI YoY"",""country"":""Latvia"",""category"":""Producer Prices Change"",""ticker"":""LATVIAPROPRICHA"",""actual"":""-1.9%"",""previous"":""-1.3%"",""revised"":null,""date"":""2020-03-20T11:00:00"",""referenceDate"":""2020-02-29T00:00:00"",""reference"":""Feb"",""calendarId"":""236450"",""importance"":1,""teforecast"":""-1.4%"",""forecast"":null,""symbol"":""LATVIAPROPRICHA"",""source"":null,""topic"":""calendar""}",
                    Country = "Latvia",
                    Category = "Producer Prices Change",
                    Importance = TradingEconomicsImportance.Low,
                    Event = "producer price index yoy",
                    Actual = -0.019m,
                    Previous = -0.013m,
                    LastUpdate = new DateTime(2020, 3, 20, 11, 0, 0),
                    Symbol = QuantConnect.Data.Custom.TradingEconomics.TradingEconomics.Calendar.Latvia.ProducerPricesChange
                },
                new LiveCalendarEventTestParameters
                {
                    Content = @"{""event"":""GDP Growth Rate QoQ"",""country"":""Luxembourg"",""category"":""GDP Growth Rate"",""ticker"":""ENGKLUQ"",""actual"":""0.4%"",""previous"":""0.3%"",""revised"":""0.2%"",""date"":""2020-03-20T11:00:00"",""referenceDate"":""2019-12-31T00:00:00"",""reference"":""Q4"",""calendarId"":236447,""importance"":1,""teforecast"":""0.5%"",""forecast"":null,""symbol"":""ENGKLUQ"",""source"":null,""topic"":""calendar""}",
                    Country = "Luxembourg",
                    Category = "GDP Growth Rate",
                    Importance = TradingEconomicsImportance.Low,
                    Event = "gdp growth rate qoq",
                    Actual = 0.004m,
                    Previous = 0.003m,
                    LastUpdate = new DateTime(2020, 3, 20, 11, 0, 0),
                    Symbol = QuantConnect.Data.Custom.TradingEconomics.TradingEconomics.Calendar.Luxembourg.GDPGrowthRate
                },
                new LiveCalendarEventTestParameters
                {
                    Content = @"{""event"":""Unemployment Rate"",""country"":""Slovenia"",""category"":""Unemployment Rate"",""ticker"":""SVUER"",""actual"":""8.2%"",""previous"":""7.7%"",""revised"":null,""date"":""2020-03-20T10:00:00"",""referenceDate"":""2020-01-31T00:00:00"",""reference"":""Jan"",""calendarId"":236456,""importance"":1,""teforecast"":""8%"",""forecast"":null,""symbol"":""SVUER"",""source"":null,""topic"":""calendar""}",
                    Country = "Slovenia",
                    Category = "Unemployment Rate",
                    Importance = TradingEconomicsImportance.Low,
                    Event = "unemployment rate",
                    Actual = 0.082m,
                    Previous = 0.077m,
                    LastUpdate = new DateTime(2020, 3, 20, 10, 0, 0),
                    Symbol = QuantConnect.Data.Custom.TradingEconomics.TradingEconomics.Calendar.Slovenia.UnemploymentRate
                },
                new LiveCalendarEventTestParameters
                {
                    Content = @"{""event"":""Existing Home Sales MoM"",""country"":""United States"",""category"":""Existing Home Sales"",""ticker"":""UNITEDSTAEXIHOMSAL"",""actual"":""6.5%"",""previous"":""-1.3%"",""revised"":null,""date"":""2020-03-20T14:00:00"",""referenceDate"":""2020-02-29T00:00:00"",""reference"":""Feb"",""calendarId"":""236580"",""importance"":2,""teforecast"":""0.5%"",""forecast"":""0.7%"",""topic"":""calendar""}",
                    Country = "United States",
                    Category = "Existing Home Sales",
                    Importance = TradingEconomicsImportance.Medium,
                    Event = "existing home sales mom",
                    Actual = 0.065m,
                    Previous = -0.013m,
                    LastUpdate = new DateTime(2020, 3, 20, 14, 0, 0),
                    Symbol = QuantConnect.Data.Custom.TradingEconomics.TradingEconomics.Calendar.UnitedStates.ExistingHomeSales
                },
                new LiveCalendarEventTestParameters
                {
                    Content = @"{""event"":""Existing Home Sales"",""country"":""United States"",""category"":""Existing Home Sales"",""ticker"":""UNITEDSTAEXIHOMSAL"",""actual"":""5.77M"",""previous"":""5.46M"",""revised"":null,""date"":""2020-03-20T14:00:00"",""referenceDate"":""2020-02-29T00:00:00"",""reference"":""Feb"",""calendarId"":""236581"",""importance"":2,""teforecast"":""5.49M"",""forecast"":""5.5M"",""symbol"":""UNITEDSTAEXIHOMSAL"",""source"":null,""topic"":""calendar""}",
                    Country = "United States",
                    Category = "Existing Home Sales",
                    Importance = TradingEconomicsImportance.Medium,
                    Event = "existing home sales",
                    Actual = 5770000m,
                    Previous = 5460000m,
                    LastUpdate = new DateTime(2020, 3, 20, 14, 0, 0),
                    Symbol = QuantConnect.Data.Custom.TradingEconomics.TradingEconomics.Calendar.UnitedStates.ExistingHomeSales
                },
            }.Select(x => new TestCaseData(x).SetName(x.Name)).ToArray();
        }

        public class LiveCalendarEventTestParameters
        {
            public string Name => Country + "/" + Event;

            public string Content { get; set; }
            public string Country { get; set; }
            public string Category { get; set; }
            public TradingEconomicsImportance Importance { get; set; }
            public string Event { get; set; }
            public decimal? Actual { get; set; }
            public decimal? Previous { get; set; }
            public DateTime LastUpdate { get; set; }
            public string Symbol { get; set; }
        }
    }
}
