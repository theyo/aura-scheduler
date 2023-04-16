using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Data;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.Console;
using Microsoft.VisualBasic;

namespace AuraScheduler.UI.Infrastructure
{
    public sealed class ObservableLogger : ILogger
    {
        private readonly ObservableLoggerConfiguration _configuration;
        private readonly string _name;

        private ObservableCollection<string> LogEntryCollection { get; }


        internal ObservableLogger(
            string name,
            ObservableLoggerConfiguration configuration,
            ObservableCollection<string> collection)
        {
            _name = name ?? string.Empty;
            _configuration = configuration;

            LogEntryCollection = collection;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(formatter);

            var message = $"{DateTime.Now} - {logLevel} - {_name.Substring(_name.LastIndexOf(".") + 1)} - {formatter(state, exception)}";

            LogEntryCollection.Add(message);
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _configuration.LogLevel;
        }


        /// <inheritdoc />
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
        private static object _lock = new object();

        private readonly ConcurrentDictionary<string, ObservableLogger> _loggers = new ConcurrentDictionary<string, ObservableLogger>();

        private readonly ObservableLoggerConfiguration _configuration;
        private ObservableCollection<string> Entries { get; } = new();

        public ObservableCollection<string>? LogEntries { get; }

        public ObservableLoggerProvider() : this(new())
        { }

        public ObservableLoggerProvider(ObservableLoggerConfiguration configuration)
        {
            _configuration = configuration;

            LogEntries = Entries;

            //Ensures updates to the collection happen on the UI thread see https://stackoverflow.com/a/33343937/83381
            BindingOperations.EnableCollectionSynchronization(LogEntries, _lock);

            LogEntries.CollectionChanged += Entries_CollectionChanged;
        }

        private void Entries_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Move)
            {
                while (e.NewItems?.Count > _configuration.MaxEntriesToKeep)
                {
                    e.NewItems.RemoveAt(0);
                }
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new ObservableLogger(name, _configuration, Entries));
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
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
