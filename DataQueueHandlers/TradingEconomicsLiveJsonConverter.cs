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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuantConnect.DataSource;

namespace QuantConnect.DataSource.DataQueueHandlers
{
    /// <summary>
    /// Converts a live Trading Economics Calendar stream message to an instance of <see cref="TradingEconomicsCalendar"/>
    /// </summary>
    public class TradingEconomicsLiveJsonConverter : JsonConverter
    {
        /// <summary>
        /// Determines if we can convert the object
        /// </summary>
        /// <param name="objectType">Type of the object we are converting</param>
        /// <returns>Boolean value indicating ability to convert</returns>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(TradingEconomicsCalendar);
        }

        /// <summary>
        /// Converts the streamed JSON message to an instance of <see cref="TradingEconomicsCalendar"/>
        /// </summary>
        /// <param name="reader">JSON reader</param>
        /// <param name="objectType">Type of object</param>
        /// <param name="existingValue">Existing value</param>
        /// <param name="serializer">JSON Serializer</param>
        /// <returns><see cref="TradingEconomicsCalendar"/></returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // Many of these values can be null since they can be omitted from the streamed
            // Calendar object. Few critical values have been left without null checks since
            // we wouldn't be able to reasonably emit those data points without those values.
            var token = JToken.ReadFrom(reader);
            var instance = new TradingEconomicsCalendar();
            var actual = token["actual"].Value<string>();
            var previous = token["previous"]?.Value<string>();
            var forecast = token["forecast"]?.Value<string>();
            var teForecast = token["teforecast"]?.Value<string>();
            var revised = token["revised"]?.Value<string>();
            var date = token["date"];
            var parseTime = DateTime.UtcNow;

            // Makes sure that we're not parsing a message that should
            // be parsed as percent as a raw value.
            var isPercent = actual.Contains("%") || (previous?.Contains("%") ?? false) ||(forecast?.Contains("%") ?? false) ||
                (teForecast?.Contains("%") ?? false) || (revised?.Contains("%") ?? false);

            instance.CalendarId = token["calendarId"].Value<string>();
            instance.Country = token["country"].Value<string>();
            instance.Category = token["category"].Value<string>();
            instance.Event = token["event"].Value<string>();
            instance.Reference = token["reference"]?.Value<string>();
            instance.Source = token["source"]?.Value<string>();
            instance.Actual = TradingEconomicsCalendar.ParseDecimal(actual, isPercent);
            instance.Previous = TradingEconomicsCalendar.ParseDecimal(previous, isPercent);
            instance.Forecast = TradingEconomicsCalendar.ParseDecimal(forecast, isPercent);
            instance.TradingEconomicsForecast = TradingEconomicsCalendar.ParseDecimal(teForecast, isPercent);
            instance.Revised = TradingEconomicsCalendar.ParseDecimal(revised, isPercent);
            instance.DateSpan = "0"; // The time of the event is known -- it's right now!!!
            // As for importance, we have the enum values of TEImportance in the right order, but
            // TE reports the importance starting from `1`.
            instance.Importance = (TradingEconomicsImportance)(token["importance"].Value<int>() - 1);
            instance.LastUpdate = date?.Value<DateTime>() ?? parseTime;
            instance.OCountry = instance.Country;
            instance.OCategory = instance.Category;
            instance.Ticker = token["ticker"].Value<string>();
            instance.IsPercentage = isPercent;

            var ticker = instance.Country.Replace(" ", "-").ToUpperInvariant() +
                TradingEconomics.Calendar.Delimiter +
                instance.Ticker.Replace(" ", "");
            instance.Symbol = Symbol.Create(ticker, SecurityType.Base, Market.USA, baseDataType: typeof(TradingEconomicsCalendar));

            instance.EndTime = instance.LastUpdate;

            return instance;
        }

        /// <summary>
        /// Not implemented
        /// </summary>
        /// <param name="writer">JSON serializer</param>
        /// <param name="value">Value to write</param>
        /// <param name="serializer">JSON writer</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
