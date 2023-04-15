using Microsoft.Extensions.Options;

namespace AuraScheduler.UI.ViewModels.DesignTime
{
    public class MockOptionsMonitor<T> : IOptionsMonitor<T> where T : new()
    {
        private Action<T, string>? _listener;

        public T CurrentValue { get; } = new T();

        public T Get(string? name)
        {
            return CurrentValue;
        }

        public MockOptionsMonitor(T instance)
        {
            CurrentValue = instance;
        }

        public IDisposable? OnChange(Action<T, string?> listener)
        {
            _listener = listener;
            return null;
        }
    }
}
