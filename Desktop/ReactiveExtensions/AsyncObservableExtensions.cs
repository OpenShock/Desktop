// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT License.
// See the LICENSE file in the project root for more information. 

using System.Reactive;

namespace OpenShock.Desktop.ReactiveExtensions
{
    public static class AsyncObservableExtensions
    {
        public static ValueTask<IAsyncDisposable> SubscribeConcurrentAsync<T>(this IAsyncObservable<T> source,
            Func<T, ValueTask> onNextAsync)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(onNextAsync);

            return source.SubscribeAsync(new ConcurrentAsyncObserver<T>(onNextAsync, ex => new ValueTask(Task.FromException(ex)),
                () => default));
        }

        public static ValueTask<IAsyncDisposable> SubscribeConcurrentAsync<T>(this IAsyncObservable<T> source,
            Func<T, ValueTask> onNextAsync, Func<Exception, ValueTask> onErrorAsync)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(onNextAsync);
            ArgumentNullException.ThrowIfNull(onErrorAsync);

            return source.SubscribeAsync(new ConcurrentAsyncObserver<T>(onNextAsync, onErrorAsync, () => default));
        }

        public static ValueTask<IAsyncDisposable> SubscribeConcurrentAsync<T>(this IAsyncObservable<T> source,
            Func<T, ValueTask> onNextAsync, Func<ValueTask> onCompletedAsync)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(onNextAsync);
            ArgumentNullException.ThrowIfNull(onCompletedAsync);

            return source.SubscribeAsync(new ConcurrentAsyncObserver<T>(onNextAsync, ex => new ValueTask(Task.FromException(ex)),
                onCompletedAsync));
        }

        public static ValueTask<IAsyncDisposable> SubscribeConcurrentAsync<T>(this IAsyncObservable<T> source,
            Func<T, ValueTask> onNextAsync, Func<Exception, ValueTask> onErrorAsync, Func<ValueTask> onCompletedAsync)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(onNextAsync);
            ArgumentNullException.ThrowIfNull(onErrorAsync);
            ArgumentNullException.ThrowIfNull(onCompletedAsync);

            return source.SubscribeAsync(new ConcurrentAsyncObserver<T>(onNextAsync, onErrorAsync, onCompletedAsync));
        }

        public static ValueTask<IAsyncDisposable> SubscribeConcurrentAsync<T>(this IAsyncObservable<T> source,
            Action<T> onNext)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(onNext);

            return source.SubscribeAsync(new ConcurrentAsyncObserver<T>(x =>
            {
                onNext(x);
                return default;
            }, ex => new ValueTask(Task.FromException(ex)), () => default));
        }

        public static ValueTask<IAsyncDisposable> SubscribeConcurrentAsync<T>(this IAsyncObservable<T> source,
            Action<T> onNext, Action<Exception> onError)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(onNext);
            ArgumentNullException.ThrowIfNull(onError);

            return source.SubscribeAsync(new ConcurrentAsyncObserver<T>(x =>
            {
                onNext(x);
                return default;
            }, ex =>
            {
                onError(ex);
                return default;
            }, () => default));
        }

        public static ValueTask<IAsyncDisposable> SubscribeConcurrentAsync<T>(this IAsyncObservable<T> source,
            Action<T> onNext, Action onCompleted)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(onNext);
            ArgumentNullException.ThrowIfNull(onCompleted);

            return source.SubscribeAsync(new ConcurrentAsyncObserver<T>(x =>
            {
                onNext(x);
                return default;
            }, ex => new ValueTask(Task.FromException(ex)), () =>
            {
                onCompleted();
                return default;
            }));
        }

        public static ValueTask<IAsyncDisposable> SubscribeConcurrentAsync<T>(this IAsyncObservable<T> source,
            Action<T> onNext, Action<Exception> onError, Action onCompleted)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(onNext);
            ArgumentNullException.ThrowIfNull(onError);
            ArgumentNullException.ThrowIfNull(onCompleted);

            return source.SubscribeAsync(new ConcurrentAsyncObserver<T>(x =>
            {
                onNext(x);
                return default;
            }, ex =>
            {
                onError(ex);
                return default;
            }, () =>
            {
                onCompleted();
                return default;
            }));
        }
    }
}