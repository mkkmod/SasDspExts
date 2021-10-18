using SasO.Signals;

namespace SasO.Dsp.DetAlgsCommon
{
    /// <summary>
    /// Opis punktu charakterystycznego
    /// </summary>
    public class UnPoint
    {

        /// <summary>
        /// Próbka sygnału
        /// </summary>
        public SigSample<float> Point { get; set; }

        /// <summary>
        /// Wskaźnik niepewności (0 najmniejsza niepewność)
        /// </summary>
        public int Uncertainty { get; set; }

        /// <summary>
        /// Metoda tworząca obiekty
        /// </summary>
        /// <param name="point">próbka sygnału</param>
        /// <param name="uncertainty">wskaźnik niepewności (0 najmniejsza niepewność)</param>
        /// <returns>utworzony punkt charakterystyczny</returns>
        public static UnPoint Create(SigSample<float> point, int uncertainty = 0)
        {
            return new UnPoint
            {
                Point = point,
                Uncertainty = uncertainty,
            };
        }

        /// <summary>
        /// Kopiuje obiekt
        /// </summary>
        /// <returns>kopia</returns>
        public UnPoint Clone()
        {
            return Create(Point, Uncertainty);
        }
    }
}