using System;
using Avalonia.Rendering;
using Avalonia.Threading;

namespace Avalonia.Headless;

/// <summary>
/// Provides extension methods for headless operations on the <see cref="Dispatcher"/> class, when Avalonia is running in headless mode.
/// </summary>
public static class HeadlessExtensions
{
    /// <summary>
    ///  Runs all remaining jobs, including any previously scheduled timers with zero time.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use.</param>
    public static void Idle(this Dispatcher dispatcher)
    {
        dispatcher.RunJobs();
        dispatcher.PulseTime(TimeSpan.FromTicks(1));
    }

    /// <summary>
    /// Pulses the time provider by the specified duration.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use.</param>
    /// <param name="duration">The duration to pulse the time provider.</param>
    public static void PulseTime(this Dispatcher dispatcher, TimeSpan duration)
    {
        // We can technically allow negative time spans. But should we do that?
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentException("Only non-negative TimeSpan argument is allowed.", nameof(duration));
        }

        // Avalonia DispatcherTimer doesn't work well about zero durations, and it might stack.
        // Force pulse to be at least 1 tick, TODO: find a better solution.
        if (duration == default)
        {
            duration = TimeSpan.FromTicks(1);
        }

        ValidateDispatcher(dispatcher);

        HeadlessTimeProvider.GetCurrent().Pulse(duration);

        var frame = new DispatcherFrame(dispatcher, true);
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromTicks(1));
        dispatcher.PushFrame(frame);
        dispatcher.RunJobs();
    }

    /// <summary>
    /// Forces the renderer to process a rendering timer tick.
    /// Use this method before calling <see cref="HeadlessWindowExtensions.GetLastRenderedFrame"/>.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use.</param>
    /// <param name="framesCount">The number of frames to be ticked on the timer.</param>
    public static void PulseRenderFrames(this Dispatcher dispatcher, int framesCount = 1)
    {
        if (framesCount < 0)
        {
            throw new ArgumentException("Only non-negative framesCount argument is allowed.", nameof(framesCount));
        }

        if (framesCount == 0)
        {
            return;
        }

        ValidateDispatcher(dispatcher);

        var timer = (AvaloniaHeadlessPlatform.RenderTimer)AvaloniaLocator.Current.GetRequiredService<IRenderTimer>();
        var singleFrame = 1d / timer.FramesPerSecond;
        for (var c = 0; c < framesCount; c++)
            dispatcher.PulseTime(TimeSpan.FromSeconds(singleFrame));
    }

    /// <summary>
    /// Sets the time provider that is used by Dispatcher and render timer.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use.</param>
    /// <param name="timeProvider">The time provider to set. If null, the system time provider will be used.</param>
    public static void SetTimeProvider(this Dispatcher dispatcher, TimeProvider? timeProvider)
    {
        ValidateDispatcher(dispatcher);

        HeadlessTimeProvider.GetCurrent().SetNested(timeProvider ?? TimeProvider.System);
        dispatcher.Idle();
    }

    private static void ValidateDispatcher(this Dispatcher dispatcher)
    {
        dispatcher.VerifyAccess();

        var impl = AvaloniaLocator.Current.GetRequiredService<IDispatcherImpl>();
        if (impl is not HeadlessDispatcherImpl)
        {
            throw new InvalidOperationException(
                "Headless Dispatcher extenions can only be applied when Avalonia runs in a headless mode.");
        }
    }
}
