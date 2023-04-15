using System.Collections.ObjectModel;
using System.Windows.Data;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.Console;

namespace AuraScheduler.UI.Infrastructure
{
    public sealed class ObservableLogger : ILogger
    {
        private static object _lock = new object();

        private readonly string _name;

        internal IExternalScopeProvider? ScopeProvider { get; set; }
        internal ConsoleFormatter Formatter { get; set; }

        private ObservableCollection<string> Collection { get; }

        internal ObservableLogger(
            string name,
            ConsoleFormatter formatter,
            IExternalScopeProvider externalScopeProvider,
            ObservableCollection<string> collection)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            _name = name;
            Formatter = formatter;
            ScopeProvider = externalScopeProvider;
            Collection = collection;

            BindingOperations.EnableCollectionSynchronization(Collection, _lock);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(formatter);

            var message = formatter(state, exception);

            Collection.Add(message);
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }


        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => ScopeProvider?.Push(state) ?? MyNullScope.Instance;

    }

    public interface IObservableLoggerProvider
    {
        ObservableCollection<string>? LogEntries { get; }
    }

    public sealed class ObservableLoggerProvider : ILoggerProvider, IObservableLoggerProvider
    {
        private readonly IExternalScopeProvider _scopeProvider = MyNullExternalScopeProvider.Instance;
        private readonly ConsoleFormatter _formatter;
        private readonly int _maxEntries;

        public ObservableCollection<string>? LogEntries { get; }

        private ObservableCollection<string> Entries { get; } = new();

        public ObservableLoggerProvider(ConsoleFormatter formatter, int maxEntries = 1024)
        {
            this._maxEntries = maxEntries;
            _formatter = formatter;

            LogEntries = Entries;

            LogEntries.CollectionChanged += Entries_CollectionChanged;
        }

        private void Entries_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Move)
            {
                while (e.NewItems?.Count > _maxEntries)
                {
                    e.NewItems.RemoveAt(0);
                }
            }
        }

        public ILogger CreateLogger(string name)
        {
            return new ObservableLogger(name, _formatter, _scopeProvider, Entries);
        }

        public void Dispose()
        {
        }
    }

    public static class ObservableLoggerExtensions
    {
        public static ILoggingBuilder AddObservableLogger(this ILoggingBuilder builder)
        {
            builder.AddConfiguration();

            builder.AddSimpleConsole();

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, ObservableLoggerProvider>());

            return builder;
        }
    }

    public sealed class MyNullExternalScopeProvider : IExternalScopeProvider
    {
        private MyNullExternalScopeProvider()
        {
        }

        /// <summary>
        /// Returns a cached instance of <see cref="MyNullExternalScopeProvider"/>.
        /// </summary>
        public static IExternalScopeProvider Instance { get; } = new MyNullExternalScopeProvider();

        /// <inheritdoc />
        void IExternalScopeProvider.ForEachScope<TState>(Action<object?, TState> callback, TState state)
        {
        }

        /// <inheritdoc />
        IDisposable IExternalScopeProvider.Push(object? state)
        {
            return MyNullScope.Instance;
        }
    }

    public sealed class MyNullScope : IDisposable
    {
        public static MyNullScope Instance { get; } = new MyNullScope();

        private MyNullScope()
        {
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
