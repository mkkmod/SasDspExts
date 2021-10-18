using System;
using System.Linq;
using SasO.Lib;
using SasO.Lib.MyMath;
using SasO.Signals;

namespace SasO.Dsp.DetAlgsCommon
{

    /// <summary>
    /// Korektor pozycji punktów charakterystycznych działający 
    /// na zasadzie uśredniania według pozycji sąsiednich
    /// </summary>
    public class MeanCorrector : IValidPointsCollector<float>
    {
        /// <summary>
        /// Dekorowany kolektor pozycji punktów charakterystycznych
        /// </summary>
        public IValidPointsCollector<float> Collector { private get; set; }

        /// <summary>
        /// True - kolektor włączony
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Parametry korektora pozycji punktów charakterystycznych
        /// </summary>
        public class Param
        {
            /// <summary>
            /// Liczba odstępów z których wyznaczana jest średnia
            /// </summary>
            public int MeanCorrectorQueueSiz { get; set; }

            /// <summary>
            /// Maksymalna względna bradykardia włączająca uśrednianie
            /// </summary>
            public double MaxRBradycardia { get; set; }


            /// <summary>
            /// Konstruktor. Ustawia wartości domyślne
            /// </summary>
            public Param()
            {
                MeanCorrectorQueueSiz = 5;
                MaxRBradycardia = 0.15;
            }
        }

        /// <summary>
        /// Parametry korektora pozycji punktów charakterystycznych
        /// </summary>
        public Param OptParam { private get; set; }


        /// <summary>
        /// Konstruktor obiektów
        /// </summary>
        public MeanCorrector()
        {
            OptParam = new Param();
            Enabled = true;
        }


        /// <summary>
        /// Dodaje wykrytą pozycję punktu charakterystycznego 
        /// </summary>
        /// <param name="foundPoint">punkt charakterystyczny</param>
        /// <param name="uncertainty">wskaźnik niepewności (0 najmniejsza niepewność)</param>
        public void Push(SigSample<float> foundPoint, int uncertainty)
        {
            if (!Enabled)
            {
                Collector.Push(foundPoint, uncertainty);
                return;
            }

            double time = foundPoint.Time;
            float value = foundPoint.Value;
            InnerPoint last = _queue == null ? null : _queue.Elements.LastOrDefault();
            var rr = last != null ? time - last.Point.Time : 1.0;
            InputQrs(new InnerPoint { Point = SigSample.Create(time, value), Uncertainty = uncertainty, TimeDelta = (float)rr });
        }



        /// <summary>
        /// Wewnętrzna reprezentacja punktu charakterystycznego 
        /// </summary>
        private class InnerPoint
        {
            public SigSample<float> Point { get; set; }
            public int Uncertainty { get; set; }
            public float TimeDelta { get; set; }

            public override string ToString()
            {
                return string.Format("Qrs[Tm:{0:0.000}s]", Point.Time);
            }
        }

        /// <summary>
        /// Kolejka FIFO punktów charakterystycznych
        /// </summary>
        private FixedFifo<InnerPoint> _queue;


        private void OutPush(InnerPoint innerPoint)
        {
            Collector.Push(SigSample.Create(innerPoint.Point.Time, innerPoint.Point.Value), innerPoint.Uncertainty);
        }

        /// <summary>
        /// Przetwarza punkt charakterystyczny, ewentualnie poprawia pozycję
        /// </summary>
        /// <param name="innerPoint">wejściowy punkt charakterystyczny</param>
        private void InputQrs(InnerPoint innerPoint)
        {
            if (_queue == null)
            {
                _queue = new FixedFifo<InnerPoint>(OptParam.MeanCorrectorQueueSiz);
            }
            _queue.Enqueue(innerPoint);

            //var mod = _queue.Elements.Select(q => q.Clone()).ToArray();

            if (_queue.HasFullSize)
            {
                var max =
                    Enumerable
                        .Range(1, OptParam.MeanCorrectorQueueSiz - 2)
                        .Select(i => new { rBr = RelBrady(i), id = i })
                        .MaxItem(it => it.rBr);
                if (max.rBr > OptParam.MaxRBradycardia)
                {
                    var modQ = _queue.Elements.ElementAt(max.id);
                    double meanTime = (_queue.Elements.ElementAt(max.id - 1).Point.Time + _queue.Elements.ElementAt(max.id + 1).Point.Time) / 2;
                    modQ.Point = SigSample.Create(meanTime, modQ.Point.Value);
                    modQ.Uncertainty = modQ.Uncertainty + 1;
                }
            }


            //podmianka zmodyfikowanej
            //foreach (var q in mod.Reverse())_queue.Enqueue(q);

            if (_queue.HasFullSize) OutPush(_queue.Elements.First());
        }



        /// <summary>
        /// Liczy względną zmianę rytmu
        /// </summary>
        /// <param name="id">indeks w kolejce</param>
        /// <returns>względna zmiana rytmu</returns>
        private float RelBrady(int id)
        {
            if (id <= 0 || id >= OptParam.MeanCorrectorQueueSiz - 1)
                throw new Exception("Niepoprawne użycie metody idx == " + id);
            var qr1 = _queue.Elements.ElementAt(id - 1);
            var qr2 = _queue.Elements.ElementAt(id);
            float hr1 = 60f / qr1.TimeDelta;
            float dtHr = Math.Abs(hr1 - 60f / qr2.TimeDelta);
            return dtHr / hr1;
        }

    }
}