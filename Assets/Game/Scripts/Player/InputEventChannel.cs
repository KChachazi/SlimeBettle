using System;
using System.Collections.Generic;

namespace SlimeBattle.Player.Input
{
    /// <summary>Typed, instance-owned input delivery with safe unsubscription during dispatch.</summary>
    internal sealed class InputEventChannel : IDisposable
    {
        private sealed class Subscription
        {
            public InputSubscriptionToken Token;
            public Delegate Handler;
        }

        private readonly object owner = new object();
        private readonly Dictionary<Type, List<Subscription>> subscriptions = new Dictionary<Type, List<Subscription>>();
        private int nextId;
        private bool isDisposed;

        public InputSubscriptionToken Subscribe<T>(Action<T> handler)
        {
            if (isDisposed) { throw new ObjectDisposedException(nameof(InputEventChannel)); }
            if (handler == null) { throw new ArgumentNullException(nameof(handler)); }
            if (!subscriptions.TryGetValue(typeof(T), out var handlers))
            {
                handlers = new List<Subscription>();
                subscriptions.Add(typeof(T), handlers);
            }
            var token = new InputSubscriptionToken(owner, checked(++nextId));
            handlers.Add(new Subscription { Token = token, Handler = handler });
            return token;
        }

        public void Unsubscribe(InputSubscriptionToken token)
        {
            if (!token.IsValid || !token.BelongsTo(owner)) { return; }
            foreach (var handlers in subscriptions.Values)
            {
                var index = handlers.FindIndex(item => item.Token == token);
                if (index < 0) { continue; }
                handlers.RemoveAt(index);
                return;
            }
        }

        public void Publish<T>(T eventData)
        {
            if (isDisposed || !subscriptions.TryGetValue(typeof(T), out var handlers)) { return; }
            foreach (var subscription in handlers.ToArray())
            {
                if (isDisposed) { break; }
                if (handlers.Contains(subscription)) { ((Action<T>)subscription.Handler)(eventData); }
            }
        }

        public void Dispose()
        {
            isDisposed = true;
            subscriptions.Clear();
        }
    }
}
