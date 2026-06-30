using Scriptr.Core;

namespace Scriptr.Gui;

internal sealed class HotkeyEngine : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly Dictionary<int, Action> _callbacks = new();
    private bool _disposed;

    internal HotkeyEngine(IntPtr hwnd) => _hwnd = hwnd;

    // Returns false if RegisterHotKey fails (e.g. the key is already owned by another process).
    internal bool Register(int id, HotkeyBinding binding, Action callback)
    {
        Unregister(id);
        bool ok = Platform.RegisterHotKey(_hwnd, id, binding.Win32Modifiers, binding.Win32Vk);
        if (ok) _callbacks[id] = callback;
        return ok;
    }

    internal void Unregister(int id)
    {
        if (!_callbacks.ContainsKey(id)) return;
        Platform.UnregisterHotKey(_hwnd, id);
        _callbacks.Remove(id);
    }

    // Called from Form.WndProc. Returns true when WM_HOTKEY was consumed.
    internal bool ProcessMessage(ref Message m)
    {
        if (m.Msg != Platform.WM_HOTKEY) return false;
        int id = m.WParam.ToInt32();
        if (!_callbacks.TryGetValue(id, out Action? callback)) return false;
        callback();
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (int id in _callbacks.Keys.ToArray())
            Platform.UnregisterHotKey(_hwnd, id);
        _callbacks.Clear();
    }
}
