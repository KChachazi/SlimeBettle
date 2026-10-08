using System;
using System.Collections.Generic;

namespace SlimeBattle.Core
{
    public readonly struct SubscriptionToken : IEquatable<SubscriptionToken>
    {
        internal SubscriptionToken(int id)
        {
            Id = id;
        }

        internal int Id { get; }

        public bool IsValid => Id > 0;

        public bool Equals(SubscriptionToken other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is SubscriptionToken && Equals((SubscriptionToken)obj);
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public static bool operator ==(SubscriptionToken left, SubscriptionToken right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SubscriptionToken left, SubscriptionToken right)
        {
            return !left.Equals(right);
        }
    }

    public interface ILevelEventBus
    {
        SubscriptionToken Subscribe<TEvent>(Action<TEvent> handler);
        void Unsubscribe(SubscriptionToken token);
        void Publish<TEvent>(TEvent eventData);
        void Clear();
    }

    public sealed class LevelEventBus : ILevelEventBus, IDisposable
    {
        private sealed class Subscription
        {
            public Subscription(SubscriptionToken token, Delegate handler)
            {
                Token = token;
                Handler = handler;
            }

            public SubscriptionToken Token { get; }
            public Delegate Handler { get; }
        }

        private readonly Dictionary<Type, List<Subscription>> subscriptions =
            new Dictionary<Type, List<Subscription>>();

        private int nextTokenId = 1;
        private bool disposed;

        public SubscriptionToken Subscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            ThrowIfDisposed();

            var eventType = typeof(TEvent);
            if (!subscriptions.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Subscription>();
                subscriptions.Add(eventType, handlers);
            }

            var token = new SubscriptionToken(nextTokenId++);
            handlers.Add(new Subscription(token, handler));
            return token;
        }

        public void Unsubscribe(SubscriptionToken token)
        {
            if (!token.IsValid || disposed)
            {
                return;
            }

            Type emptyEventType = null;

            foreach (var pair in subscriptions)
            {
                for (var index = 0; index < pair.Value.Count; index++)
                {
                    if (pair.Value[index].Token != token)
                    {
                        continue;
                    }

                    pair.Value.RemoveAt(index);
                    if (pair.Value.Count == 0)
                    {
                        emptyEventType = pair.Key;
                    }

                    break;
                }

                if (emptyEventType != null)
                {
                    break;
                }
            }

            if (emptyEventType != null)
            {
                subscriptions.Remove(emptyEventType);
            }
        }

        public void Publish<TEvent>(TEvent eventData)
        {
            ThrowIfDisposed();

            if (!subscriptions.TryGetValue(typeof(TEvent), out var handlers))
            {
                return;
            }

            var snapshot = handlers.ToArray();
            foreach (var subscription in snapshot)
            {
                if (!handlers.Contains(subscription))
                {
                    continue;
                }

                ((Action<TEvent>)subscription.Handler).Invoke(eventData);
            }
        }

        public void Clear()
        {
            if (disposed)
            {
                return;
            }

            subscriptions.Clear();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            Clear();
            disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(LevelEventBus));
            }
        }
    }
}
