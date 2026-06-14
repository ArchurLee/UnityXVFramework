using System;
using System.Collections.Generic;
namespace Core
{
    public class EventManager : ModuleBase
    {
        public override string ModuleName => "EventManager";
        private readonly Dictionary<string, Delegate> eventDic = new Dictionary<string, Delegate>();

        protected override void OnInitialize()
        {
        }

        protected override void OnShutdown()
        {
            eventDic.Clear();
        }

        public void RegisterEvent(string eventName, Action callback)
        {
            RegisterEventInternal(eventName, callback);
        }

        public void RegisterEvent<T>(string eventName, Action<T> callback)
        {
            RegisterEventInternal(eventName, callback);
        }

        private void RegisterEventInternal(string eventName, Delegate callback)
        {
            if(eventDic.TryGetValue(eventName, out Delegate existingDelegate))
            {
                if (existingDelegate.GetType() != callback.GetType())
                {
                    Logger.Error(ModuleName, $"Event type mismatch for event: {eventName}");
                    return;
                }
                eventDic[eventName] = Delegate.Combine(existingDelegate, callback);
            }
            else
            {
                eventDic[eventName] = callback;
            }
        }

        public void UnregisterEvent(string eventName, Action callback)
        {
            UnregisterEventInternal(eventName, callback);
        }

        public void UnregisterEvent<T>(string eventName, Action<T> callback)
        {
            UnregisterEventInternal(eventName, callback);
        }


        private void UnregisterEventInternal(string eventName, Delegate callback)
        {
            if (eventDic.TryGetValue(eventName, out Delegate existingDelegate))
            {
                if (existingDelegate.GetType() != callback.GetType())
                {
                    Logger.Error(ModuleName, $"Event type mismatch for event: {eventName}");
                    return;
                }
                Delegate newDelegate = Delegate.Remove(existingDelegate, callback);
                if (newDelegate == null)
                {
                    eventDic.Remove(eventName);
                }
                else
                {
                    eventDic[eventName] = newDelegate;
                }
            }
            else
            {
                Logger.Error(ModuleName, $"Event not found: {eventName}");
            }
        }

        public void BroadcastEvent(string eventName)
        {
            BroadcastEventInternal(eventName);
        }

        public void BroadcastEvent<T>(string eventName, T arg)
        {
            BroadcastEventInternal(eventName, arg);
        }
        private void BroadcastEventInternal(string eventName, params object[] args)
        {
            if (eventDic.TryGetValue(eventName, out Delegate method))
            {
                try
                {
                    method.DynamicInvoke(args);
                }
                catch (Exception ex)
                {
                    Logger.Error(ModuleName, $"Error invoking event: {eventName}, Exception: {ex}");
                }
            }
            else
            {
                Logger.Error(ModuleName, $"Event not found: {eventName}");
            }
        }

    }
}
