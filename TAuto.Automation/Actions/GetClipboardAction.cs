using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using TAuto.Automation.Models;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Retrieves text from the system clipboard.
/// </summary>
[ActionMetadata("Get Clipboard", "System", "📋", IsAdvanced = true)]
public class GetClipboardAction : ActionBase
{
    public override string DisplayName => $"📋 Get Clipboard to ${OutputVariable}";

    [ActionParameter("Output Var", "Variable name to store the clipboard text.", EditorType = ActionParameterEditorType.Choice)]
    public string OutputVariable { get; set; } = "ClipboardText";

    [ActionParameter("Log Result", "Whether to automatically log the text.")]
    public bool LogResult { get; set; } = true;

    public override Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        string text = string.Empty;
        Exception? error = null;

        var t = new Thread(() =>
        {
            try
            {
                text = ClipboardHelper.GetText();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();

        if (error != null)
            return Task.FromResult(ActionResult.Fail($"Clipboard Error: {error.Message}"));

        context.SetVariable(OutputVariable, text);

        if (LogResult)
            context.Logger?.Info($"Clipboard [{OutputVariable}]: {text}");

        return Task.FromResult(ActionResult.Ok());
    }
}

internal static class ClipboardHelper
{
    [DllImport("user32.dll")]
    static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll")]
    static extern bool IsClipboardFormatAvailable(uint format);

    const uint CF_UNICODETEXT = 13;

    public static string GetText()
    {
        if (!IsClipboardFormatAvailable(CF_UNICODETEXT)) return string.Empty;
        if (!OpenClipboard(IntPtr.Zero)) return string.Empty;

        string result = string.Empty;
        try
        {
            IntPtr hGlobal = GetClipboardData(CF_UNICODETEXT);
            if (hGlobal != IntPtr.Zero)
            {
                IntPtr ptr = Kernel32.GlobalLock(hGlobal);
                if (ptr != IntPtr.Zero)
                {
                    try
                    {
                        result = Marshal.PtrToStringUni(ptr) ?? string.Empty;
                    }
                    finally
                    {
                        Kernel32.GlobalUnlock(hGlobal);
                    }
                }
            }
        }
        finally
        {
            CloseClipboard();
        }
        return result;
    }

    static class Kernel32
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalUnlock(IntPtr hMem);
    }
}
