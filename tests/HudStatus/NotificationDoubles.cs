using System.Text;

namespace TMPro
{
    public enum TextAlignmentOptions { TopLeft }
    public enum TextOverflowModes { Ellipsis }
    public class TMP_Text : UnityEngine.UI.Graphic
    {
        private string _text = "", _backing = "";
        private bool _dirty;
        public int Reads;
        public string text
        {
            get
            {
                Reads++;
                // Installed TMP getter consumes dirty backing text without
                // assigning its serialized string. This reproduces the bug.
                if (_dirty) { _dirty = false; return _backing; }
                return _text;
            }
            set { _dirty = false; _text = value; }
        }
        public void SetText(StringBuilder value) { _backing = value.ToString(); _dirty = true; }
        public object? font, fontSharedMaterial;
        public int fontSize, maxVisibleLines;
        public bool richText, enableWordWrapping;
        public TextAlignmentOptions alignment;
        public TextOverflowModes overflowMode;
    }
    public sealed class TextMeshProUGUI : TMP_Text { }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string method) { }
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPostfix : Attribute { }
}
public sealed class MessageFeed { }
