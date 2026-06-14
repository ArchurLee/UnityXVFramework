using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public class UIManager : ModuleBase
    {
        public int panelCount;
        private readonly Dictionary<string, BasePanel> panelDict = new Dictionary<string, BasePanel>();
        private readonly Stack<BasePanel> panelStack = new Stack<BasePanel>();
        public MainCanvas MainCanvas { get; private set; }
        private Transform canvasTrans;

        public override string ModuleName => "UIManager";

        protected override void OnInitialize()
        {
            MainCanvas = GameObject.FindFirstObjectByType<MainCanvas>();
            if (MainCanvas == null)
            {
                Logger.Error(ModuleName, "MainCanvas not found.");
                return;
            }

            canvasTrans = MainCanvas.panelsParent;
        }

        protected override void OnShutdown()
        {
            panelStack.Clear();
            panelDict.Clear();
        }

        /// <summary>
        /// 加载UI面板资源
        /// </summary>
        /// <param name="panelName">面板名称</param>
        /// <returns>返回面板的游戏对象</returns>
        public async UniTask<GameObject> LoadPanelAsync(string panelName)
        {
            AssetHandle<GameObject> handle = await GameManager.AssetLoader.LoadPrefab(panelName);
            return handle.Asset;
        }

        /// <summary>
        /// 获得UI面板，但是不存在也不新建
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetPanelWithoutLoad<T>() where T : BasePanel
        {
            string panelName = typeof(T).Name;
            if (panelDict.ContainsKey(panelName))
            {
                return panelDict[panelName] as T;
            }
            return null;
        }

        /// <summary>
        /// 获得UI面板，如果没有则创建，加入面板之中
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async UniTask<T> GetPanel<T>() where T : BasePanel
        {
            string panelName = typeof(T).Name;
            if (panelDict.ContainsKey(panelName))
            {
                return panelDict[panelName] as T;
            }

            GameObject panelObj = await LoadPanelAsync(panelName);
            if (panelObj == null)
            {
                Logger.Error(ModuleName, $"Panel prefab load failed: {panelName}");
                return null;
            }

            panelObj = GameObject.Instantiate(panelObj);
            panelObj.transform.SetParent(canvasTrans, false);
            T panel = panelObj.GetComponent<T>();
            if (panel == null)
            {
                Logger.Error(ModuleName, $"Panel component missing: {panelName}");
                GameObject.Destroy(panelObj);
                return null;
            }

            panelDict[panelName] = panel;
            return panel;
        }

        /// <summary>
        /// 获取UI面板是否已经激活
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool IsShow<T>() where T : BasePanel
        {
            string panelName = typeof(T).Name;
            if (panelDict.ContainsKey(panelName))
            {
                return panelDict[panelName].IsShow;
            }
            return false;
        }

        /// <summary>
        /// 显示UI面板
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async UniTask<T> ShowPanel<T>() where T : BasePanel
        {
            T panel = await GetPanel<T>();
            if (panel == null)
            {
                return null;
            }

            if (panelStack.Count > 0 && panelStack.Peek() == panel)
            {
                ShowPanelInstance(panel);
                return panel;
            }

            if (panelStack.Count > 0)
            {
                HidePanelInstance(panelStack.Peek());
            }

            panelStack.Push(panel);
            ShowPanelInstance(panel);
            return panel;
        }

        /// <summary>
        /// 注册UI面板到DIct中，但是可以用Addressabe平替了
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="panel"></param>
        /// <param name="show"></param>
        public void RegisterPanel<T>(T panel, bool show = true) where T : BasePanel
        {
            if (panel == null)
            {
                Logger.Error(ModuleName, "Register panel failed: panel is null.");
                return;
            }

            string panelName = typeof(T).Name;
            if (!panelDict.ContainsKey(panelName))
            {
                if (show)
                {
                    if (panelStack.Count > 0)
                    {
                        HidePanelInstance(panelStack.Peek());
                    }

                    panelStack.Push(panel);
                    ShowPanelInstance(panel);
                }

                panelDict.Add(panelName, panel);
            }
        }

        /// <summary>
        /// 关闭当前栈顶UI，并自动显示下面一个UI
        /// </summary>
        public void CloseTopPanel(Action closeFinishCallBack = null)
        {
            if (panelStack.Count == 0)
            {
                closeFinishCallBack?.Invoke();
                return;
            }

            BasePanel currentPanel = panelStack.Pop();
            HidePanelInstance(currentPanel, () =>
            {
                if (panelStack.Count > 0)
                {
                    ShowPanelInstance(panelStack.Peek());
                }

                closeFinishCallBack?.Invoke();
            });
        }

        private void ShowPanelInstance(BasePanel panel)
        {
            panel.gameObject.SetActive(true);
            panel.Show();
        }

        private void HidePanelInstance(BasePanel panel, Action hideFinishCallBack = null)
        {
            panel.Hide(() =>
            {
                panel.gameObject.SetActive(false);
                hideFinishCallBack?.Invoke();
            });
        }
    }
}
