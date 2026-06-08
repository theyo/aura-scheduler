using System.Collections.Concurrent;
using System.Collections.ObjectModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.UI.Dispatching;

namespace AuraScheduler.UI.Infrastructure
{
    public sealed class ObservableLogger : ILogger
    {
        private readonly ObservableLoggerConfiguration _configuration;
        private readonly string _name;
        private readonly ObservableCollection<string> _collection;
        private readonly Func<DispatcherQueue?> _getDispatcherQueue;

        internal ObservableLogger(
            string name,
            ObservableLoggerConfiguration configuration,
            ObservableCollection<string> collection,
            Func<DispatcherQueue?> getDispatcherQueue)
        {
            _name = name ?? string.Empty;
            _configuration = configuration;
            _collection = collection;
            _getDispatcherQueue = getDispatcherQueue;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            ArgumentNullException.ThrowIfNull(formatter);

            var shortName = _name.Substring(_name.LastIndexOf('.') + 1);
            var message = $"{DateTime.Now} - {logLevel} - {shortName} - {formatter(state, exception)}";

            var queue = _getDispatcherQueue();
            if (queue is not null)
                queue.TryEnqueue(() => _collection.Add(message));
            else
                _collection.Add(message);
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _configuration.LogLevel;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
    }

    public interface IObservableLoggerProvider
    {
        ObservableCollection<string>? LogEntries { get; }
    }

    public class ObservableLoggerConfiguration
    {
        public LogLevel LogLevel { get; set; } = LogLevel.Debug;
        public int MaxEntriesToKeep { get; set; } = 1024;
    }

    public sealed class ObservableLoggerProvider : ILoggerProvider, IObservableLoggerProvider
    {
        private readonly ConcurrentDictionary<string, ObservableLogger> _loggers = new();
        private readonly ObservableLoggerConfiguration _configuration;
        private DispatcherQueue? _dispatcherQueue;

        public ObservableCollection<string>? LogEntries { get; } = new();

        public ObservableLoggerProvider() : this(new()) { }

        public ObservableLoggerProvider(ObservableLoggerConfiguration configuration)
        {
            _configuration = configuration;
            LogEntries!.CollectionChanged += (_, e) =>
            {
                if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                    return;
                while (LogEntries.Count > _configuration.MaxEntriesToKeep)
                    LogEntries.RemoveAt(0);
            };
        }

        public void SetDispatcherQueue(DispatcherQueue queue) => _dispatcherQueue = queue;

        public ILogger CreateLogger(string categoryName) =>
            _loggers.GetOrAdd(categoryName, name =>
                new ObservableLogger(name, _configuration, LogEntries!, () => _dispatcherQueue));

        public void Dispose() => _loggers.Clear();
    }

    public static class ObservableLoggerExtensions
    {
        public static ILoggingBuilder AddObservableLogger(this ILoggingBuilder builder)
        {
            builder.AddConfiguration();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, ObservableLoggerProvider>());
            return builder;
        }
    }
}
