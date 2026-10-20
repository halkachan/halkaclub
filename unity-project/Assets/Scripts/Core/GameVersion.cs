namespace Halka.Game.Core
{
    public static class GameVersion
    {
        public const string Value = "3.0";
        public static bool IsPreview => true;
        public static string Label => "ver" + Value + (IsPreview ? " Preview" : "");
        public static string ProductVersion => Value + (IsPreview ? " Preview" : "");
    }
}
