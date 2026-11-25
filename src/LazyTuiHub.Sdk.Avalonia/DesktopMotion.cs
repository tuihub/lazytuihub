using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;

namespace LazyTuiHub.Sdk.Avalonia;

public static partial class DesktopMotion
{
    public static bool Reduced =>
        (Application.Current?.Resources.TryGetValue("ReduceMotion", out var preference) == true && preference is true)
        || SystemReducesMotion();

    private static bool SystemReducesMotion()
    {
        if (OperatingSystem.IsMacOS())
        {
            var workspace = SendObject(GetClass("NSWorkspace"), Selector("sharedWorkspace"));
            return workspace != 0 && SendBool(workspace, Selector("accessibilityDisplayShouldReduceMotion")) != 0;
        }
        const uint getClientAreaAnimation = 0x1042;
        return OperatingSystem.IsWindows()
            && SystemParameters(getClientAreaAnimation, 0, out var animate, 0) != 0
            && animate == 0;
    }

    public static Action? Reveal(Control target)
    {
        if (Reduced || TopLevel.GetTopLevel(target) is not { } window)
            return null;
        var duration = (TimeSpan)target.FindResource("PanelRevealDuration")!;
        var easing = (Easing)target.FindResource("PanelRevealEasing")!;
        var distance = (double)target.FindResource("PanelRevealOffset")!;
        var translation = new TranslateTransform(distance, 0);
        TimeSpan? firstFrame = null;
        var active = true;
        target.RenderTransform = translation;
        target.Opacity = 0;
        window.RequestAnimationFrame(Frame);
        return Finish;

        void Frame(TimeSpan frameTime)
        {
            if (!active)
                return;
            firstFrame ??= frameTime;
            var elapsed = frameTime - firstFrame.Value;
            if (elapsed >= duration || !target.IsEffectivelyVisible)
            {
                Finish();
                return;
            }
            var progress = easing.Ease(elapsed / duration);
            translation.X = distance * (1 - progress);
            target.Opacity = progress;
            window.RequestAnimationFrame(Frame);
        }

        void Finish()
        {
            active = false;
            target.Opacity = 1;
            target.RenderTransform = null;
        }
    }

    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    [LibraryImport(ObjectiveC, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint GetClass(string name);

    [LibraryImport(ObjectiveC, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint Selector(string name);

    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static partial nint SendObject(nint receiver, nint selector);

    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static partial byte SendBool(nint receiver, nint selector);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    private static partial int SystemParameters(uint action, uint parameter, out int value, uint flags);
}
