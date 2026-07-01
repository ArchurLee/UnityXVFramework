using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core
{
    public abstract class BasePanel : MonoBehaviour
    {
        private CanvasGroup canvasGroup;
        private float alphaSpeed = 4;
        public bool IsShow => isShow;
        private bool isShow;
        private Action hideCallBack;
        
        // 资源释放账本
        private readonly List<Action> _releaseActions = new List<Action>();

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = this.gameObject.AddComponent<CanvasGroup>();
            }
            InternalCreate();
        }

        /// <summary>
        /// 框架调用：一次性创建
        /// </summary>
        internal void InternalCreate()
        {
            OnCreate();
        }

        /// <summary>
        /// 框架调用：每次显示
        /// </summary>
        internal void InternalShow()
        {
            isShow = true;
            canvasGroup.alpha = 0;
            OnShow();
        }

        /// <summary>
        /// 框架调用：每次隐藏
        /// </summary>
        internal void InternalHide(Action callBack)
        {
            isShow = false;
            if (canvasGroup)
            {
                canvasGroup.alpha = 1;
            }
            hideCallBack = callBack;
            OnHide();
        }

        /// <summary>
        /// 框架调用：真销毁时统一释放资源
        /// </summary>
        internal void InternalDestroy()
        {
            OnDestroy_();
            
            // 遍历账本，逐个释放资源
            foreach (var releaseAction in _releaseActions)
            {
                releaseAction?.Invoke();
            }
            _releaseActions.Clear();
        }

        /// <summary>
        /// 面板内统一加载入口，自动登记到资源账本
        /// </summary>
        protected async UniTask<T> LoadAssetAsync<T>(string assetName)
        {
            var handle = await GameManager.AssetLoader.LoadAsset<T>(assetName);
            if (handle.IsValid)
            {
                _releaseActions.Add(() => GameManager.AssetLoader.Release<T>(assetName));
                Logger.Log("BasePanel", $"Panel {GetType().Name} loaded asset: {assetName}");
            }
            return handle.Asset;
        }

        public void SetAlphaSpeed(float speed)
        {
            alphaSpeed = speed;
        }

        protected virtual void Update()
        {
            if (isShow && canvasGroup.alpha != 1)
            {
                canvasGroup.alpha += alphaSpeed * Time.unscaledDeltaTime;
                if (canvasGroup.alpha >= 1)
                {
                    canvasGroup.alpha = 1;
                }
            }
            else if (!isShow && canvasGroup.alpha != 0)
            {
                canvasGroup.alpha -= alphaSpeed * Time.unscaledDeltaTime;
                if (canvasGroup.alpha <= 0)
                {
                    canvasGroup.alpha = 0;
                    hideCallBack?.Invoke();
                }
            }
        }

        /// <summary>
        /// 关闭自己
        /// </summary>
        public void Close()
        {
            GameManager.UI.ClosePanel(this);
        }

        // ========== 子类重写这些生命周期方法 ==========
        
        /// <summary>
        /// 实例化后一次：找组件、建子物体、绑按钮
        /// </summary>
        protected virtual void OnCreate() { }
        
        /// <summary>
        /// 每次显示：刷新数据
        /// </summary>
        protected virtual void OnShow() { }
        
        /// <summary>
        /// 每次隐藏：停动画、清临时态
        /// </summary>
        protected virtual void OnHide() { }
        
        /// <summary>
        /// 真销毁一次：清 OnCreate 建的东西
        /// </summary>
        protected virtual void OnDestroy_() { }
    }
}
