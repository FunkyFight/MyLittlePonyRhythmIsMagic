using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Xna.Framework.Input;

namespace MLP_RiM.Elements.DevUI;

public static class DevUiTextInput
{
    private const int BackspaceInitialDelayMs = 320;
    private const int BackspaceRepeatIntervalMs = 38;

    public static bool ShouldBackspace(KeyboardState keyboard, KeyboardState previousKeyboard, ref long holdStartMs, ref long lastRepeatMs)
    {
        long now = Environment.TickCount64;
        bool isDown = keyboard.IsKeyDown(Keys.Back);
        bool wasDown = previousKeyboard.IsKeyDown(Keys.Back);
        if (!isDown)
        {
            holdStartMs = 0;
            lastRepeatMs = 0;
            return false;
        }

        if (!wasDown)
        {
            holdStartMs = now;
            lastRepeatMs = now;
            return true;
        }

        if (holdStartMs == 0)
            holdStartMs = now;

        if (now - holdStartMs < BackspaceInitialDelayMs || now - lastRepeatMs < BackspaceRepeatIntervalMs)
            return false;

        lastRepeatMs = now;
        return true;
    }

    public static bool TryGetTypedChar(Keys key, KeyboardState keyboard, out char c)
    {
        c = '\0';
        if (IsNonTextKey(key))
            return false;

        if (OperatingSystem.IsWindows() && WindowsKeyboard.TryGetChar(key, keyboard, out c))
            return !char.IsControl(c);

        return TryGetFallbackChar(key, keyboard, out c);
    }

    public static bool TryGetFloatChar(Keys key, KeyboardState keyboard, out char c)
    {
        if (!TryGetTypedChar(key, keyboard, out c))
            return false;

        if (c == ',')
            c = '.';

        return char.IsDigit(c) || c == '.' || c == '-';
    }

    private static bool IsNonTextKey(Keys key)
    {
        return key is Keys.Back
            or Keys.Tab
            or Keys.Enter
            or Keys.Escape
            or Keys.Delete
            or Keys.Left
            or Keys.Right
            or Keys.Up
            or Keys.Down
            or Keys.Home
            or Keys.End
            or Keys.PageUp
            or Keys.PageDown
            or Keys.LeftShift
            or Keys.RightShift
            or Keys.LeftControl
            or Keys.RightControl
            or Keys.LeftAlt
            or Keys.RightAlt
            or Keys.CapsLock;
    }

    private static bool TryGetFallbackChar(Keys key, KeyboardState keyboard, out char c)
    {
        bool shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        c = '\0';

        if (key >= Keys.A && key <= Keys.Z)
        {
            c = (char)((shift ? 'A' : 'a') + (key - Keys.A));
            return true;
        }

        if (key >= Keys.D0 && key <= Keys.D9)
        {
            const string normal = "à&é\"'(-è_ç";
            const string shifted = "0123456789";
            int index = key - Keys.D0;
            c = shift ? shifted[index] : normal[index];
            return true;
        }

        if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
        {
            c = (char)('0' + (key - Keys.NumPad0));
            return true;
        }

        c = key switch
        {
            Keys.Space => ' ',
            Keys.Decimal => '.',
            Keys.OemComma => shift ? '?' : ',',
            Keys.OemPeriod or Keys.OemSemicolon => shift ? '.' : ';',
            Keys.OemMinus or Keys.Subtract => shift ? '_' : '-',
            Keys.OemPlus or Keys.Add => shift ? '+' : '=',
            Keys.OemQuestion => shift ? '/' : ':',
            Keys.Oem8 => shift ? '§' : '!',
            _ => '\0'
        };

        return c != '\0';
    }

    private static class WindowsKeyboard
    {
        private const uint MapvkVkToVsc = 0;
        private const uint ToUnicodeNoStateChange = 0x04;

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        private static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);

        public static bool TryGetChar(Keys key, KeyboardState keyboard, out char c)
        {
            c = '\0';
            byte[] keyboardState = BuildKeyboardState(keyboard);

            uint virtualKey = (uint)key;
            uint scanCode = MapVirtualKey(virtualKey, MapvkVkToVsc);
            StringBuilder buffer = new(8);
            int result = ToUnicodeEx(virtualKey, scanCode, keyboardState, buffer, buffer.Capacity, ToUnicodeNoStateChange, GetKeyboardLayout(0));
            if (result <= 0 || buffer.Length == 0)
                return false;

            c = buffer[0];
            return true;
        }

        private static byte[] BuildKeyboardState(KeyboardState keyboard)
        {
            byte[] keyboardState = new byte[256];
            foreach (Keys pressedKey in keyboard.GetPressedKeys())
            {
                int index = (int)pressedKey;
                if (index >= 0 && index < keyboardState.Length)
                    keyboardState[index] = 0x80;
            }

            if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
                keyboardState[0x10] = 0x80;
            if (keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl))
                keyboardState[0x11] = 0x80;
            if (keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt))
                keyboardState[0x12] = 0x80;
            if (keyboard.IsKeyDown(Keys.CapsLock))
                keyboardState[0x14] = 0x01;

            return keyboardState;
        }
    }
}
