using Godot;
using System.Runtime.CompilerServices;

/// <summary>
/// Provides clickable console output in the Godot editor.
/// Usage: ClickablePrint.Log("Your message here");
/// </summary>
public static class ClickablePrint
{
    /// <summary>
    /// Prints a message with a clickable link to the source file and line number.
    /// Only works when running in the Godot editor.
    /// </summary>
    /// <param name="message">The message to print</param>
    /// <param name="callerFilePath">Automatically filled by compiler</param>
    /// <param name="callerLineNumber">Automatically filled by compiler</param>
    public static void Log(
        object message = null,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0,
        [CallerMemberName] string callerMemberName = "")
    {
        // Only print in editor mode
        if (!OS.HasFeature("editor"))
        {
            return;
        }

        string messageStr = message?.ToString() ?? "";
        string fileName = System.IO.Path.GetFileName(callerFilePath);
        
        // Use absolute file path with forward slashes
        string absolutePath = callerFilePath.Replace("\\", "/");
        
        // Format: absolute/path:line
        // The plugin will intercept this and open in VS Code
        GD.PrintRich($"  At: [url={absolutePath}:{callerLineNumber}]{fileName}:{callerLineNumber}[/url] - {messageStr}");
    }

    /// <summary>
    /// Prints an error message with a clickable link.
    /// </summary>
    public static void Error(
        object message = null,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (!OS.HasFeature("editor"))
        {
            return;
        }

        string messageStr = message?.ToString() ?? "";
        string fileName = System.IO.Path.GetFileName(callerFilePath);
        string absolutePath = callerFilePath.Replace("\\", "/");
        
        GD.PrintRich($"[color=red]  At: [url={absolutePath}:{callerLineNumber}]{fileName}:{callerLineNumber}[/url] - ERROR: {messageStr}[/color]");
    }

    /// <summary>
    /// Prints a warning message with a clickable link.
    /// </summary>
    public static void Warning(
        object message = null,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (!OS.HasFeature("editor"))
        {
            return;
        }

        string messageStr = message?.ToString() ?? "";
        string fileName = System.IO.Path.GetFileName(callerFilePath);
        string absolutePath = callerFilePath.Replace("\\", "/");
        
        GD.PrintRich($"[color=yellow]  At: [url={absolutePath}:{callerLineNumber}]{fileName}:{callerLineNumber}[/url] - WARNING: {messageStr}[/color]");
    }
}
