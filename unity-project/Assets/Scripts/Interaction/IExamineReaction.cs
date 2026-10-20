namespace Halka.Game.Interaction
{
    // Optional response after an examine message and its sound have been played.
    public interface IExamineReaction
    {
        void OnExamined();
    }
}
