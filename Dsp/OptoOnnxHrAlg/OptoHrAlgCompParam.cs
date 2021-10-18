using System;

namespace OptoOnnxHrAlg
{
    public static class OptoHrAlgCompParam
    {

        //public static double ErrRRCorrectorMaxHrChange = 100;
        /// <summary>
        /// Wymagane zmniejszenie zmienności tacho dzięki usunięciu punktu. 
        /// Parametr TachoVarDelCorrector.
        /// </summary>
        public static double TachoVarGain = 0.66;

        public const double WndHalfWidth = 60.0 / 250.0 * .66;


        /// <summary>
        /// Poniżej tej wartości maksimum piku funkcji detekcyjnej dany punkt jest odrzucany
        /// </summary>
        public const double DetFunTreshold = 0.1;


        /// <summary>
        /// [s]
        /// </summary>
        public const double PichCompInterval = 3;

        //public const double PitchToWindowMul = .66;
        // jednak o wiele większą zmienność zakładam bo są duże zniekształcenia piku
        // p. s.616 zapis 20429-K xskl_ses_2018_04_04__19-58-49_m760_sav_2018_04_04__20-21-59\onnx_out.csv
        public static double PitchToWindowMul = .4;

        public static double MaxWindowWidth => 60.0 / 40.0 * PitchToWindowMul;
        public static double MinWindowWidth => 60.0 / 250.0 * PitchToWindowMul;

        public static double PitchToWindow(double pitch)
        {
            return Math.Min(MaxWindowWidth, Math.Max(MinWindowWidth, pitch * PitchToWindowMul));
        }


    }
}