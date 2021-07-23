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

using Newtonsoft.Json;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.DataSource;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using QuantConnect.Packets;

namespace QuantConnect.DataSource.DataQueueHandlers
{
    /// <summary>
    /// Implements a streaming data source for <see cref="TradingEconomicsCalendar"/>
    /// via the Trading Economics streaming API: http://docs.tradingeconomics.com/#streaming
    /// </summary>
    public class TradingEconomicsCalendarDataQueueHandler : IDataQueueHandler
    {
        private IDataAggregator _dataAggregator;
        private readonly object _subscriptionLock = new object();
        private readonly bool _overrideSubscriptionCheck;
        private readonly int _heartbeatTimeout;
        private readonly RateGate _rateGate;
        private readonly Thread _thread;
        private readonly string _host = Config.Get("trading-economics-stream-host", "stream.tradingeconomics.com");
        private readonly int _port = Config.GetInt("trading-economics-stream-port", 80);
        private readonly string _user = Config.Get("trading-economics-stream-user");
        private readonly string _key = Config.Get("trading-economics-stream-key");
        private CancellationTokenSource _cancellationSource;

        private HashSet<Symbol> _subscriptionSymbols;
        private readonly HashSet<string> _supportedCountries = new HashSet<string>
        {
            "australia",
            "austria",
            "belgium",
            "canada",
            "china",
            "cyprus",
            "estonia",
            "finland",
            "france",
            "germany",
            "greece",
            "ireland",
            "italy",
            "japan",
            "latvia",
            "lithuania",
            "luxembourg",
            "malta",
            "netherlands",
            "new zealand",
            "portugal",
            "slovakia",
            "slovenia",
            "spain",
            "sweden",
            "switzerland",
            "united kingdom",
            "united states"
        };

        /// <summary>
        /// Creates a new DQH instance. The data collection begins immediately after construction on a separate thread.
        /// </summary>
        /// <param name="dataAggregator">The data aggregator instance</param>
        /// <param name="overrideSubscriptionCheck">Override subscription check</param>
        /// <param name="heartbeatTimeout">Seconds after expected heartbeat interval to consider the connection timed out</param>
        public TradingEconomicsCalendarDataQueueHandler()
        {
            _dataAggregator = Composer.Instance.GetPart<IDataAggregator>() ?? 
                Composer.Instance.GetExportedValueByTypeName<IDataAggregator>(Config.Get("data-aggregator", "QuantConnect.Data.Common.CustomDataAggregator"));

            _overrideSubscriptionCheck = Config.GetBool("trading-economics-override-subscription-check", true);
            _heartbeatTimeout = Config.GetInt("trading-economics-heartbeat-timeout", 5);
            _rateGate = new RateGate(1, TimeSpan.FromSeconds(5));
            _cancellationSource = new CancellationTokenSource();

            var supportedCountries = Config.Get("trading-economics-supported-countries").ToLowerInvariant()
                .Split(',')
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet();

            if (supportedCountries.Count != 0)
            {
                _supportedCountries = supportedCountries;
            }

            _thread = new Thread(() => Run())
            {
                Name = $"TradingEconomicsCalendarDQH-{Guid.NewGuid()}",
                IsBackground = true,
            };

            _thread.Start();
        }

        /// <summary>
        /// Subscribe to the specified configuration
        /// </summary>
        /// <param name="dataConfig">defines the parameters to subscribe to a data feed</param>
        /// <param name="newDataAvailableHandler">handler to be fired on new data available</param>
        /// <returns>The new enumerator for this subscription request</returns>
        public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            lock (_subscriptionLock)
            {
                if (_subscriptionSymbols.Add(dataConfig.Symbol))
                {
                    Log.Trace($"TradingEconomicsDataQueueHandler.Subscribe(): {dataConfig.Symbol}");
                }
            }
            return _dataAggregator.Add(dataConfig, newDataAvailableHandler);
        }

        /// <summary>
        /// Removes the specified configuration
        /// </summary>
        /// <param name="dataConfig">Subscription config to be removed</param>
        public void Unsubscribe(SubscriptionDataConfig dataConfig)
        {
            lock (_subscriptionLock)
            {
                if (_subscriptionSymbols.Remove(dataConfig.Symbol))
                {
                    _dataAggregator.Remove(dataConfig);
                    Log.Trace($"TradingEconomicsDataQueueHandler.Unsubscribe(): {dataConfig.Symbol}");
                }
            }
        }

        /// <summary>
        /// Sets the job we're subscribing for
        /// </summary>
        /// <param name="job">Job we're subscribing for</param>
        public void SetJob(LiveNodePacket job)
        {
        }

        /// <summary>
        /// Returns whether the data provider is connected
        /// </summary>
        /// <returns>True if the data provider is connected</returns>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Helper method determining if a <see cref="Symbol"/> is a valid subscription candidate
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>True if can subscribe</returns>
        private bool CanSubscribe(Symbol symbol)
        {
            // Allow overriding of subscriptions
            if (_overrideSubscriptionCheck)
            {
                return true;
            }

            // ignore unsupported security types and universe symbols
            return symbol.ID.SecurityType == SecurityType.Base && !symbol.Value.Contains("-UNIVERSE-");
        }

        /// <summary>
        /// Method collects data from TradingEconomics and adds them to the <see cref="_subscriptionData"/> collection.
        /// </summary>
        /// <remarks>
        /// Runs in a new thread on class creation
        /// </remarks>
        private async void Run()
        {
            var teLiveConverter = new TradingEconomicsLiveJsonConverter();

            while (!_cancellationSource.Token.IsCancellationRequested)
            {
                // Prevents us from essentially doing a DoS attack by attempting
                // to connect to the host without rate limits if we have a bug somewhere.
                _rateGate.WaitToProceed();

                var subscribed = false;
                var lastHeartbeat = DateTime.UtcNow;

                try
                {
                    using (var websocket = new ClientWebSocket())
                    {
                        IsConnected = false;

                        await websocket.ConnectAsync(new Uri($"ws://{_host}:{_port}/?client={_user}:{_key}"), _cancellationSource.Token);

                        while (!_cancellationSource.Token.IsCancellationRequested && websocket.State == WebSocketState.Open)
                        {
                            IsConnected = true;

                            // Trading Economics sends out a keepalive every 45 seconds
                            if (subscribed && (DateTime.UtcNow - lastHeartbeat) > TimeSpan.FromSeconds(_heartbeatTimeout + 45))
                            {
                                IsConnected = false;

                                Log.Error($"TradingEconomicsDataQueueHandler.Run(): Connection timed out ({_heartbeatTimeout} seconds)", overrideMessageFloodProtection: true);
                                break;
                            }
                            if (!subscribed)
                            {
                                var message = Encoding.UTF8.GetBytes("{\"topic\": \"subscribe\", \"to\": \"calendar\" }");
                                await websocket.SendAsync(new ArraySegment<byte>(message), WebSocketMessageType.Text, true, _cancellationSource.Token);
                                subscribed = true;
                            }

                            var final = new List<byte>();
                            var buf = new ArraySegment<byte>(new byte[4096]);
                            var done = false;

                            while (!done)
                            {
                                var receiveStatus = await websocket.ReceiveAsync(buf, _cancellationSource.Token);
                                final.AddRange(buf.Take(receiveStatus.Count));
                                done = receiveStatus.EndOfMessage;
                            }

                            // Clean up message. Newlines and carriage returns shouldn't be part of the data.
                            var response = Encoding.UTF8.GetString(final.ToArray()).Replace("\r", "").Replace("\n", "");
                            if (response == "{\"topic\":\"keepalive\"}")
                            {
                                // Heartbeats come at a 45 second interval
                                lastHeartbeat = DateTime.UtcNow;
                                continue;
                            }

                            try
                            {
                                // Wrap around try block since some data can come malformed through the stream
                                // Example: "2020-03-0 T05:00:00" from Calendar ID: 252270
                                var calendar = JsonConvert.DeserializeObject<TradingEconomicsCalendar>(response, teLiveConverter);
                                lock (_subscriptionLock)
                                {
                                    if (_supportedCountries.Contains(calendar.Country.ToLowerInvariant()) &&
                                        CanSubscribe(calendar.Symbol))
                                    {
                                        _dataAggregator.Update(calendar);
                                    }
                                }
                            }
                            catch (Exception err)
                            {
                                var responseLower = response.ToLowerInvariant();
                                if (_supportedCountries.Any(x => responseLower.Contains(x)))
                                {
                                    Log.Error(err, response);
                                }
                            }
                        }
                    }
                }
                catch (Exception err)
                {
                    IsConnected = false;

                    // Most likely a connection error/timeout occurred. Log the error and restart the connection.
                    Log.Error(err, overrideMessageFloodProtection: true);
                }
            }
        }

        /// <summary>
        /// Shuts down the background thread and sets the
        /// cancellation token to a canceled state.
        /// </summary>
        public void Dispose()
        {
            _cancellationSource.Cancel();
            _thread.Join();
        }
    }
}

