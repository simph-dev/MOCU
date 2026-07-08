using System;
using System.Runtime.InteropServices;

public static class WinAPI
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetForegroundWindow(IntPtr hWnd);




    private const int SW_MINIMIZE = 6;
    private const int SW_RESTORE = 9;

    public static void MinimizeGameWindow()
    {
        IntPtr hWnd = GetActiveWindow();
        if (hWnd != IntPtr.Zero)
        {
            ShowWindow(hWnd, SW_MINIMIZE);
        }
    }

    public static void RestoreWindow(string windowName)
    {
        IntPtr hWnd = FindWindow(null, windowName);
        if (hWnd != IntPtr.Zero)
        {
            ShowWindow(hWnd, SW_RESTORE);
        }
    }

    public static void RestoreAndActivateWindow(string windowName)
    {
        IntPtr hWnd = FindWindow(null, windowName);
        if (hWnd != IntPtr.Zero)
        {
            ShowWindow(hWnd, SW_RESTORE);
            SetForegroundWindow(hWnd);
        }
    }
}