using System;
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

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = this.gameObject.AddComponent<CanvasGroup>();
            }
            Init();
        }

        public abstract void Init();

        public virtual void Show()
        {
            isShow = true;
            canvasGroup.alpha = 0;
        }

        public virtual void Hide(Action callBack)
        {
            isShow = false;
            if (canvasGroup)
            {
                canvasGroup.alpha = 1;
            }
            hideCallBack = callBack;
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

        public void Close(Action callback = null)
        {
            GameManager.UI.CloseTopPanel(callback);
        }
    }
}
