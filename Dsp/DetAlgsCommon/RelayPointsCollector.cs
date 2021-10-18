using System;
using SasO.Signals;

namespace SasO.Dsp.DetAlgsCommon
{
    /// <summary>
    /// Korektor pozycji punktów charakterystycznych działający na 
    /// zasadzie delegowania operacji do zewnętrznych funkcji lambda.
    /// </summary>
    /// <typeparam name="TSample">typ próbek sygnalu</typeparam>
    public class RelayPointsCollector<TSample> : IValidPointsCollector<TSample>
    {
        /// <summary>
        /// Funkcja realizująca dodanie punktu charakterystycznego
        /// </summary>
        private readonly Action<SigSample<TSample>, int> _push;

        /// <summary>
        /// True - kolektor włączony
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Konstruktor obiektów korektora
        /// </summary>
        /// <param name="push">funkcja realizująca dodanie punktu charakterystycznego</param>
        public RelayPointsCollector(Action<SigSample<TSample>, int> push)
        {
            _push = push;
        }

        /// <summary>
        /// Dodaje wykrytą pozycję punktu charakterystycznego 
        /// </summary>
        /// <param name="foundPoint">punkt charakterystyczny</param>
        /// <param name="uncertainty">wskaźnik niepewności (0 najmniejsza niepewność)</param>
        public void Push(SigSample<TSample> foundPoint, int uncertainty)
        {
            _push(foundPoint, uncertainty);
        }
    }
}