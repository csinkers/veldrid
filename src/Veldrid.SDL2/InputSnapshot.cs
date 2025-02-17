using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace Veldrid.SDL2
{
    public class InputSnapshot
    {
        public List<Rune> InputEvents { get; } = [];
        public List<KeyEvent> KeyEvents { get; } = [];
        public List<MouseButtonEvent> MouseEvents { get; } = [];

        public Vector2 MousePosition { get; set; }
        public Vector2 WheelDelta { get; set; }
        public MouseButton MouseDown { get; set; }

        internal void Clear()
        {
            InputEvents.Clear();
            KeyEvents.Clear();
            MouseEvents.Clear();
            WheelDelta = Vector2.Zero;
        }

        public void CopyTo(InputSnapshot other)
        {
            Debug.Assert(this != other);

            other.InputEvents.Clear();
            other.InputEvents.AddRange(InputEvents);

            other.MouseEvents.Clear();
            other.MouseEvents.AddRange(MouseEvents);

            other.KeyEvents.Clear();
            other.KeyEvents.AddRange(KeyEvents);

            other.MousePosition = MousePosition;
            other.WheelDelta = WheelDelta;
            other.MouseDown = MouseDown;
        }
    }
}
