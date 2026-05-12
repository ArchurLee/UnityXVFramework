namespace Core
{
    public interface IModule
    {
        string ModuleName { get; }
        bool IsInitialized { get; }
        void Initialize();
        void Tick(float deltaTime);
        void Shutdown();
    }
}
