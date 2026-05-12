namespace Core
{
    public abstract class ModuleBase : IModule
    {
        public virtual string ModuleName => GetType().Name;

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            OnInitialize();
            IsInitialized = true;
        }

        public virtual void Tick(float deltaTime)
        {
        }

        public void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            OnShutdown();
            IsInitialized = false;
        }

        protected virtual void OnInitialize()
        {
        }

        protected virtual void OnShutdown()
        {
        }
    }
}
