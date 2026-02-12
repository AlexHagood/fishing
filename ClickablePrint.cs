using Godot;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

/// <summary>
/// Provides clickable console output in the Godot editor.
/// Usage: ClickablePrint.Log("Your message here");
/// </summary>
public static class ClickablePrint
{
    public static long playerId;
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

        string visibleText = $"({fileName}:{callerLineNumber}, {playerId})";
        string sender = $"[url={absolutePath}:{callerLineNumber}]{visibleText}[/url]";
        
        // Use monospace font or tabs to preserve spacing
        // Option 1: Add dots/characters that won't be stripped
        int paddingLength = System.Math.Max(0, 32 - visibleText.Length);
        string padding = new string('\u00A0', paddingLength); // Use non-breaking spaces to preserve spacing
        
        GD.PrintRich($"{sender}{padding} - {messageStr}");
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
