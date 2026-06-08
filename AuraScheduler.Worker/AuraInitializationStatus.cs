namespace AuraScheduler.Worker
{
    /// <summary>
    /// Signals the result of AURA SDK initialisation to the UI layer so it can react
    /// (e.g. show an error dialog) without polling or tight coupling.
    /// </summary>
    public sealed class AuraInitializationStatus
    {
        private readonly TaskCompletionSource<Exception?> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Awaitable that completes with <see langword="null"/> on successful AURA init,
        /// or the caught <see cref="Exception"/> if initialisation failed.
        /// </summary>
        public Task<Exception?> Awaitable => _tcs.Task;

        internal void SignalSuccess() => _tcs.TrySetResult(null);
        internal void SignalFailure(Exception ex) => _tcs.TrySetResult(ex);
    }
}
