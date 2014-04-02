﻿using System;
using System.Reactive.Linq;
using Adaptive.ReactiveTrader.Client.Concurrency;
using Adaptive.ReactiveTrader.Client.Domain;
using Adaptive.ReactiveTrader.Client.Domain.Instrumentation;
using Adaptive.ReactiveTrader.Client.Domain.Transport;
using Adaptive.ReactiveTrader.Shared.UI;
using log4net;

namespace Adaptive.ReactiveTrader.Client.UI.Connectivity
{
    class ConnectivityStatusViewModel : ViewModelBase, IConnectivityStatusViewModel
    {
        private static readonly TimeSpan StatsFrequency = TimeSpan.FromSeconds(1);

        private readonly IPriceLatencyRecorder _priceLatencyRecorder;
        private static readonly ILog Log = LogManager.GetLogger(typeof(ConnectivityStatusViewModel));

        public ConnectivityStatusViewModel(IReactiveTrader reactiveTrader, IConcurrencyService concurrencyService)
        {
            _priceLatencyRecorder = reactiveTrader.PriceLatencyRecorder;
            reactiveTrader.ConnectionStatusStream
                .ObserveOn(concurrencyService.Dispatcher)
                .SubscribeOn(concurrencyService.ThreadPool)
                .Subscribe(
                OnStatusChange,
                ex => Log.Error("An error occured within the connection status stream.", ex));

            Observable
                .Interval(StatsFrequency, concurrencyService.Dispatcher)
                .Subscribe(OnTimerTick);
        }

        private void OnTimerTick(long _)
        {
            var stats = _priceLatencyRecorder.CalculateAndReset();

            if (stats == null)
                return;

            UiLatency = stats.UiLatencyMax;
            ServerClientLatency = stats.ServerLatencyMax;
            TotalLatency = stats.TotalLatencyMax;
            UiUpdates = stats.RenderedCount;
            TicksReceived = stats.ReceivedCount;
            CpuTime = Math.Round(stats.ProcessTime.TotalMilliseconds, 0);
            CpuPercent = Math.Round(CpuTime/(Environment.ProcessorCount*StatsFrequency.TotalMilliseconds)*100, 0);
            Histogram = stats.Histogram;
        }

        private void OnStatusChange(ConnectionInfo connectionInfo)
        {
            switch (connectionInfo.ConnectionStatus)
            {
                case ConnectionStatus.Uninitialized:
                case ConnectionStatus.Connecting:
                    Status = string.Format("Connecting to {0} ...", connectionInfo.Server);
                    Disconnected = true;
                    break;
                case ConnectionStatus.Reconnected:
                case ConnectionStatus.Connected:
                    Status = string.Format("Connected to {0}", connectionInfo.Server);
                    Disconnected = false;
                    break;
                case ConnectionStatus.ConnectionSlow:
                    Status = string.Format("Slow connection detected with {0}", connectionInfo.Server);
                    Disconnected = false;
                    break;
                case ConnectionStatus.Reconnecting:
                    Status = string.Format("Reconnecting to {0} ...", connectionInfo.Server);
                    Disconnected = true;
                    break;
                case ConnectionStatus.Closed:
                    Status = string.Format("Disconnected from {0}", connectionInfo.Server);
                    Disconnected = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public string Status { get; private set; }
        public bool Disconnected { get; private set; }
        public long UiUpdates { get; private set; }
        public long TicksReceived { get; private set; }
        public long TotalLatency { get; set; }
        public long ServerClientLatency { get; private set; }
        public long UiLatency { get; private set; }
        public string Histogram { get; private set; }
        public double CpuTime { get; private set; }
        public double CpuPercent { get; private set; }
    }
}