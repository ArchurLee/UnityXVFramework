using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public static AssetLoader AssetLoader => Instance.assetLoader;
        public static EventManager Event => Instance.eventManager;
        public static UIManager UI => Instance.uiManager;

        private readonly List<IModule> modules = new List<IModule>();

        private AssetLoader assetLoader;
        private EventManager eventManager;
        private UIManager uiManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            CreateModules();
            InitializeModules();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            for (int i = 0; i < modules.Count; i++)
            {
                modules[i].Tick(deltaTime);
            }
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            ShutdownModules();
            Instance = null;
        }

        private void CreateModules()
        {
            assetLoader = new AssetLoader();
            eventManager = new EventManager();
            uiManager = new UIManager();

            modules.Add(assetLoader);
            modules.Add(eventManager);
            modules.Add(uiManager);
        }

        private void InitializeModules()
        {
            for (int i = 0; i < modules.Count; i++)
            {
                modules[i].Initialize();
            }
        }

        private void ShutdownModules()
        {
            for (int i = modules.Count - 1; i >= 0; i--)
            {
                modules[i].Shutdown();
            }

            modules.Clear();
        }
    }
}
