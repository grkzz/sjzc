using UnityEngine;
using UnityEngine.EventSystems;

namespace ShenYi.Prototype
{
    // Used only by the opt-in smoke run. Feeds the real StandaloneInputModule,
    // including pointer-down/up, click routing, drag threshold and release cleanup.
    public sealed class PrototypePointerTestInput : BaseInput
    {
        Vector2 position;
        bool held, down, up;
        public void Frame(StandaloneInputModule module, Vector2 point, bool pressed)
        {
            position=point; down=pressed && !held; up=!pressed && held; held=pressed;
            module.Process(); down=up=false;
        }
        public override bool mousePresent { get { return true; } }
        public override Vector2 mousePosition { get { return position; } }
        public override Vector2 mouseScrollDelta { get { return Vector2.zero; } }
        public override bool touchSupported { get { return false; } }
        public override int touchCount { get { return 0; } }
        public override bool GetMouseButton(int button) { return button==0 && held; }
        public override bool GetMouseButtonDown(int button) { return button==0 && down; }
        public override bool GetMouseButtonUp(int button) { return button==0 && up; }
        public override bool GetButtonDown(string name) { return false; }
        public override float GetAxisRaw(string name) { return 0; }
    }
}
