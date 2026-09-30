namespace Halka.Game.UI
{
    public sealed class TimedMessage
    {
        private string text;
        private float expiresAt;

        public void Show(string value, float now, float seconds)
        {
            text = value;
            expiresAt = now + seconds;
        }

        public string TextAt(float now) => now < expiresAt ? text : null;
    }
}
