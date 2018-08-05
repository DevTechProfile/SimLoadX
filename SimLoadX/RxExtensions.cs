using System;
using System.Reactive.Linq;

namespace SimLoadX
{
    public static class RxExtensions
    {
        public static IObservable<T> SampleByInterval<T>(this IObservable<T> observable, TimeSpan interval)
        {
            return Observable.Generate(0, // initialState
                                        x => true, //condition
                                        x => x, //iterate
                                        x => x, //resultSelector
                                        x => interval).CombineWithLatest(observable, (clock, element) => element);
        }

        /// <summary>
        /// Combines sequence with latest element form other squence
        /// </summary>
        /// <typeparam name="TSource1">The type of the source1.</typeparam>
        /// <typeparam name="TSource2">The type of the source2.</typeparam>
        /// <typeparam name="TRes">The type of the resource.</typeparam>
        /// <param name="source1">The source1.</param>
        /// <param name="source2">The source2.</param>
        /// <param name="resultSelector">The result selector.</param>
        /// <returns></returns>
        public static IObservable<TRes> CombineWithLatest<TSource1, TSource2, TRes>(this IObservable<TSource1> source1, IObservable<TSource2> source2, Func<TSource1, TSource2, TRes> resultSelector)
        {
            var latestCache = default(TSource2);
            source2.Subscribe(s2 => latestCache = s2);

            return source1.Select(s1 => resultSelector(s1, latestCache));
        }
    }
}
