using System;
using System.Collections.Generic;
namespace Core
{
    public class EventManager : ModuleBase
    {
        public override string ModuleName => "EventManager";
        private readonly Dictionary<string, Action> eventDic = new Dictionary<string, Action>();

        protected override void OnInitialize()
        {
        }

        protected override void OnShutdown()
        {
            eventDic.Clear();
        }

        public void RegisterEvent(string eventName, Action callback)
        {
            if (eventDic.ContainsKey(eventName))
            {
                eventDic[eventName] += callback;
            }
            else
            {
                eventDic[eventName] = callback;
            }
        }

        public void UnregisterEvent(string eventName, Action callback)
        {
            if (eventDic.ContainsKey(eventName))
            {
                eventDic[eventName] -= callback;
                if (eventDic[eventName] == null)
                {
                    eventDic.Remove(eventName);
                }
            }
        }

        public void BroadcastEvent(string eventName)
        {
            if (eventDic.TryGetValue(eventName, out Action method))
            {
                method?.Invoke();
            }
            else
            {
                Logger.Error(ModuleName, $"Event not found: {eventName}");
            }
        }

    }
}
