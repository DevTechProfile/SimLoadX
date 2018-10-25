using SimLoadX;
using System;
using System.Reactive.Linq;

namespace TestApp
{
    class Program
    {
        private static IObservable<int> _obs = Observable.Generate(0, // initialState
                                                                    x => true, //condition
                                                                    x => x + 1, //iterate
                                                                    x => x, //resultSelector
                                                                    x => TimeSpan.FromSeconds(2)).SampleByInterval(TimeSpan.FromSeconds(1));

        static void Main(string[] args)
        {
            _obs.Subscribe(WriteToConsole);

            Console.ReadKey();
        }

        private static void WriteToConsole(int x)
        {
            Console.WriteLine(x.ToString());
        }
    }
}
