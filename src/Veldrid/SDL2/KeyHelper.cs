using System;

namespace Veldrid.SDL2;
#pragma warning disable CS1591
public static class KeyHelper
{
    public static Key KeyForDigit(int digit) =>
        digit switch
        {
            0 => Key.Num0,
            1 => Key.Num1,
            2 => Key.Num2,
            3 => Key.Num3,
            4 => Key.Num4,
            5 => Key.Num5,
            6 => Key.Num6,
            7 => Key.Num7,
            8 => Key.Num8,
            9 => Key.Num9,
            _ => throw new ArgumentOutOfRangeException(nameof(digit))
        };

    public static int? DigitForKey(Key key) =>
        key switch
        {
            Key.Num0 => 0,
            Key.Num1 => 1,
            Key.Num2 => 2,
            Key.Num3 => 3,
            Key.Num4 => 4,
            Key.Num5 => 5,
            Key.Num6 => 6,
            Key.Num7 => 7,
            Key.Num8 => 8,
            Key.Num9 => 9,
            Key.Keypad0 => 0,
            Key.Keypad1 => 1,
            Key.Keypad2 => 2,
            Key.Keypad3 => 3,
            Key.Keypad4 => 4,
            Key.Keypad5 => 5,
            Key.Keypad6 => 6,
            Key.Keypad7 => 7,
            Key.Keypad8 => 8,
            Key.Keypad9 => 9,
            _ => null
        };
}
