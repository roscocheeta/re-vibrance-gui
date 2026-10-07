using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace ReVibranceGUI.Helpers
{
    public class GlobalHotkey : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;

        private HwndSource? _source;
        private readonly int _id;
        private readonly Action _onHotkey;
        private bool _isRegistered;
        
        public GlobalHotkey(System.Windows.Window window, Action onHotkey)
        {
            _onHotkey = onHotkey;
            var helper = new WindowInteropHelper(window);
            _source = HwndSource.FromHwnd(helper.Handle);
            if (_source != null)
            {
                _source.AddHook(HwndHook);
            }
            _id = GetHashCode();
        }
        
        public bool Register(Key key, ModifierKeys modifiers)
        {
            if (_isRegistered)
            {
                Unregister();
            }
            
            if (_source == null || key == Key.None) return false;

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            uint mod = 0;
            if (modifiers.HasFlag(ModifierKeys.Alt)) mod |= 1;
            if (modifiers.HasFlag(ModifierKeys.Control)) mod |= 2;
            if (modifiers.HasFlag(ModifierKeys.Shift)) mod |= 4;
            if (modifiers.HasFlag(ModifierKeys.Windows)) mod |= 8;
            
            _isRegistered = RegisterHotKey(_source.Handle, _id, mod, vk);
            return _isRegistered;
        }
        
        public void Unregister()
        {
            if (_isRegistered && _source != null)
            {
                UnregisterHotKey(_source.Handle, _id);
                _isRegistered = false;
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
            {
                _onHotkey?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
            if (_source != null)
            {
                _source.RemoveHook(HwndHook);
            }
        }
    }
}
