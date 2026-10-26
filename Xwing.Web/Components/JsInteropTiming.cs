using Microsoft.JSInterop;

namespace Xwing.Web.Components;

internal static class JsInteropTiming
{
    internal static bool IsJsAvailable(bool rendererIsInteractive) =>
        rendererIsInteractive;

    internal static bool IsJsUnavailableException(InvalidOperationException ex) =>
        ex.Message.Contains("JavaScript interop", StringComparison.Ordinal)
        && ex.Message.Contains("statically rendered", StringComparison.Ordinal);

    internal static async ValueTask<bool> TryInvokeVoidAsync(
        IJSRuntime js,
        bool rendererIsInteractive,
        string identifier,
        params object?[] args)
    {
        if (!rendererIsInteractive)
        {
            return false;
        }

        try
        {
            switch (args.Length)
            {
                case 0:
                    await js.InvokeVoidAsync(identifier);
                    break;
                case 1:
                    await js.InvokeVoidAsync(identifier, args[0]);
                    break;
                case 2:
                    await js.InvokeVoidAsync(identifier, args[0], args[1]);
                    break;
                case 3:
                    await js.InvokeVoidAsync(identifier, args[0], args[1], args[2]);
                    break;
                case 4:
                    await js.InvokeVoidAsync(identifier, args[0], args[1], args[2], args[3]);
                    break;
                default:
                    throw new NotSupportedException("Too many JavaScript interop arguments.");
            }
        }
        catch (InvalidOperationException ex) when (IsJsUnavailableException(ex))
        {
            return false;
        }
        catch (JSDisconnectedException)
        {
            return false;
        }

        return true;
    }
}
