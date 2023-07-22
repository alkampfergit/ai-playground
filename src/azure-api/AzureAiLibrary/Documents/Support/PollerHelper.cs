
using Serilog;

namespace AzureAiLibrary.Documents.Support
{
    public class PollerHelper
    {
        private readonly Func<Task> _callBack;
        private readonly string _pollerName;
        private readonly Int32 PollingTimeInMs = 0;

        private Int32 _pollerGate;
        private Guid _instanceId = Guid.NewGuid();

        public Boolean Stopped { get; private set; } = true;

        private Int64 NumOfRuns = 0;

        private CancellationTokenSource _stopCancellationToken;

        public PollerHelper(
            Func<Task> callBack,
            Int32 intervalInMilliseconds,
            String pollerName)
        {
            PollingTimeInMs = intervalInMilliseconds;
            _callBack = callBack;
            _pollerName = pollerName;
        }

        /// <summary>
        /// Start poller.
        /// </summary>
        /// <param name="performImmediatePollAsync">If True it will immediately perform a first poll in another
        /// thread of thread poll without waiting</param>
        public void Start(Boolean performImmediatePollAsync = false)
        {
            if (Stopped)
            {
                _stopCancellationToken = new CancellationTokenSource();
                Stopped = false;
                //Fire and forget, we do not want blocking caller thread
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                InnerPoll(performImmediatePollAsync);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
        }

        private async Task InnerPoll(Boolean performImmediatePollAsync)
        {
            if (performImmediatePollAsync)
            {
                await Poll();
            }

            //Cycle until stopped
            while (!Stopped)
            {
                try
                {
                    await Task.Delay(PollingTimeInMs, _stopCancellationToken.Token);
                    await Poll();
                }
                catch (OperationCanceledException)
                {
                    //cancellation requested simply ignore.
                }
            }
        }

        public async Task StopAsync(Boolean waitForStop)
        {
            Stopped = true;
            _stopCancellationToken.Cancel();

            //wait asyncronously for the child thread to finish
            while (waitForStop && IsPolling)
            {
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Return true if the timer is active in polling function.
        /// </summary>
        public Boolean IsPolling
        {
            get { return _pollerGate == 1; }
        }

        private bool _pollrequested = false;

        /// <summary>
        /// function is public because we can poll from external code.
        /// Remember that the Poll will  be executed if the timer based
        /// is stopped, manual polling will always be executed, but if another
        /// thread is polling, poll will be skipped.
        /// </summary>
        public async Task Poll()
        {
            if (Interlocked.CompareExchange(ref _pollerGate, 1, 0) == 0)
            {
                Interlocked.Increment(ref NumOfRuns);
                try
                {
                    do
                    {
                        //Set poll requested to false, if WHILE we are executing the callback and some other thread will ask
                        //for a poll we will immediately repoll without waiting for the next timer tick.
                        _pollrequested = false;

                        if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug)) Log.Debug("TimerBasePoller: Started callback for {type} / {id}", _pollerName, _instanceId);
                        await _callBack();
                        if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug)) Log.Debug("TimerBasePoller: Ended callback for {type} / {id}", _pollerName, _instanceId);
                    } while (_pollrequested);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in timer based poller {name} {Message}", this._pollerName, ex.Message);
                }
                finally
                {
                    Interlocked.Exchange(ref _pollerGate, 0);
                }
            }
            else
            {
                _pollrequested = true; //we signal that some external thread requested a poll.
                if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug)) Log.Debug("TimerBasePoller: Skipped callback for {type} / {id}", _pollerName, _instanceId);
            }
        }
    }
}
