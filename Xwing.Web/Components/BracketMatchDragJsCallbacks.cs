using Microsoft.JSInterop;

namespace Xwing.Web.Components;

public sealed class BracketMatchDragJsCallbacks(
    Action<int, int, int> onStarted,
    Action<int, int, int> onMoved,
    Func<int, int, int, Task> onEnded)
{
    [JSInvokable]
    public void DragStarted(int matchId, int cellX, int cellY) =>
        onStarted(matchId, cellX, cellY);

    [JSInvokable]
    public void DragMoved(int matchId, int cellX, int cellY) =>
        onMoved(matchId, cellX, cellY);

    [JSInvokable]
    public Task DragEnded(int matchId, int cellX, int cellY) =>
        onEnded(matchId, cellX, cellY);
}
