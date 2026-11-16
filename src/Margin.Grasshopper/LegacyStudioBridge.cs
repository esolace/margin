#if MARGIN_RHINO7
namespace Margin.Native;

internal static class LegacyStudioBridge
{
    // Rhino 7's Eto has no MessageReceived. Acknowledge each cancelled navigation
    // before sending the next command so rapid input cannot drop group operations.
    internal const string Script = """
        (() => {
          const queue = [];
          let busy = false;
          function pump() {
            if (busy || !queue.length) return;
            busy = true;
            window.location.href = 'margin://message/' + encodeURIComponent(queue.shift());
          }
          window.MarginLegacyAck = () => { busy = false; pump(); };
          window.eto = { postMessage: message => { queue.push(message); pump(); } };
        })();
        """;
}
#endif
