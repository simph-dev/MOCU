namespace MoogModule
{
    public interface ITrajectoryGenerator
    {
        public DofParameters[] GetWholePath(int fps);
        public DofParameters? GetNextPosition();
    }
}