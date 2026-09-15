using clib.TaskSystem;
using ECommons.DalamudServices;
using System.Threading.Tasks;

namespace AutoSightseeingLog.Core.Tasks;

internal abstract class AutoCommon : TaskBase
{
    private const int DelayPollFrames = 2;

    protected void Diag(string message) => Svc.Log.Info($"{AslConstants.LogPrefix} {message}");

    protected new async Task DelayMs(int milliseconds)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            if (CancelToken.IsCancellationRequested)
            {
                return;
            }

            await NextFrame(DelayPollFrames);
        }
    }
}
