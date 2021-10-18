using SasO.Signals;

namespace SasO.Dsp.DetAlgsCommon
{

    /// <summary>
    /// Interfejs kolektora punktów charakterystycznych
    /// </summary>
    /// <typeparam name="TSample">typ próbek sygnału</typeparam>
    public interface IValidPointsCollector<TSample>
    {
        /// <summary>
        /// True - kolektor włączony
        /// </summary>
        bool Enabled { get; set; }


        /// <summary>
        /// Dodaje wykrytą pozycję punktu charakterystycznego 
        /// </summary>
        /// <param name="foundPoint">punkt charakterystyczny</param>
        /// <param name="uncertainty">wskaźnik niepewności (0 najmniejsza niepewność)</param>
        void Push(SigSample<TSample> foundPoint, int uncertainty);
    }
}