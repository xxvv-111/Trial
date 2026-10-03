using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public static class EventCenter
    {
        private static readonly Dictionary<System.Type, System.Delegate> Events = new();
        public static void Subscribe<T>(System.Action<T> handler) where T : struct//订阅
        {
            if (Events.TryGetValue(typeof(T), out var d))
            {
                Events[typeof(T)] = (System.Action<T>)d + handler;
            }
            else
            {
                Events[typeof(T)] = handler;
            }
        }
        public static void Unsubscribe<T>(System.Action<T> handler) where T : struct//退订
        {
            if (Events.TryGetValue(typeof(T), out var d))
                Events[typeof(T)] = (System.Action<T>)d - handler;
        }
        public static void Publish<T>(T evt) where T : struct//发布
        {
            if (Events.TryGetValue(typeof(T), out var d))
                ((System.Action<T>)d)?.Invoke(evt);
        }
    }
}