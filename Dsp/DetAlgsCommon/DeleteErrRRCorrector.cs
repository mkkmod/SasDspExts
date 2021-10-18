using System;
using System.Collections.Generic;
using System.Linq;
using SasO.Lib.MyMath;
using SasO.Signals;

namespace SasO.Dsp.DetAlgsCommon
{

    /// <summary>
    /// Dekorator na kolektor kasujący błędne pozycje punktów charakterystycznych.
    /// Przerobiony QrsErrorCorrector.
    /// Przerobiony ze starego SAS.
    /// Spora zmiana działania:
    /// teraz ten korektor usuwa jeden z 3 załamków, tak aby max HR change całej kolejki
    /// było minimalne i robi to dopiero, gdy maxChange znajdzie się na środku kolejki.
    /// 
    /// ten korektor jest raczej niebezpieczny!
    /// 
    /// 
    /// </summary>
    [Obsolete("ten korektor jest raczej niebezpieczny! użyj TachoVarDelCorrector")]
    public class DeleteErrRRCorrector : IValidPointsCollector<float>
    {
        //oryg był: 09 03 2010

        private readonly IValidPointsCollector<float> _outCollector;

        /// <summary>
        /// Rozmiar kolejki
        /// </summary>
        private const int QueueSize = 5;

        /// <summary>
        /// Indeks elementu środkowego kolejki
        /// </summary>
        private const int QueueMiddle = 2;


        /// <summary>
        /// Kolejka FIFO punktów charakterystycznych
        /// </summary>
        private readonly List<UnPoint> _queue = new List<UnPoint>();


        /// <summary>
        /// Maksymalna wartość poprawnego rytmu [bpm]
        /// </summary>
        private readonly double _maxHrChange;

        /// <summary>
        /// Debug log
        /// </summary>
        private readonly Action<object> _debug;


        /// <summary>
        /// Konstruktor obiektów korektora
        /// </summary>
        /// <param name="outCollector">dekorowany kolektor pozycji punktów charakterystycznych</param>
        /// <param name="maxHrChange">maksymalna zmiana poprawnego rytmu [bpm]</param>
        /// <param name="debug">debug log</param>
        public DeleteErrRRCorrector(IValidPointsCollector<float> outCollector, double maxHrChange, Action<object> debug = null)
        {
            _outCollector = outCollector;
            _maxHrChange = maxHrChange;
            _debug = debug ?? (msg => { });
            Enabled = true;
        }

        /// <summary>
        /// True - kolektor włączony
        /// </summary>
        public bool Enabled { get; set; }


        /// <summary>
        /// Dodaje wykrytą pozycję punktu charakterystycznego 
        /// Przetwarza punkt charakterystyczny, sprawdza czy należy go usunąć.
        /// </summary>
        /// <param name="foundPoint">punkt charakterystyczny</param>
        /// <param name="uncertainty">wskaźnik niepewności (0 najmniejsza niepewność)</param>
        public void Push(SigSample<float> foundPoint, int uncertainty)
        {
            if (!Enabled)
            {
                _outCollector.Push(foundPoint, uncertainty);
                return;
            }

            _queue.Add(UnPoint.Create(foundPoint, uncertainty));
            if (_queue.Count < QueueSize)
            {
                return;
            }

            var maxHrChange = FindMaxHrChange(_queue);
            double bbOryg = maxHrChange.Change;

            //gdy jest coś źle i maxHrChange jest na środku kolejki
            if (maxHrChange.Change > _maxHrChange && maxHrChange.Index == QueueMiddle)
            {
                var bestToRemove = BestToRemove(_queue);

                //wersja kasująca tylko wtedy gdy coś poprawia:
                if (bestToRemove.Change < bbOryg)  //?? && bestToRemove.Change < _maxBBHr
                {
                    _debug("[DeleteErrRRCorrector] removing " + new { _queue[bestToRemove.Index].Point.Time });
                    foreach (UnPoint point in _queue)
                    {
                        point.Uncertainty++;
                    }
                    _queue.RemoveAt(bestToRemove.Index);
                }

            }

            _outCollector.Push(_queue[0].Point, _queue[0].Uncertainty);
            _queue.RemoveAt(0); //remove only after push


        }


        public void Flush()
        {
            while (_queue.Any())
            {
                _outCollector.Push(_queue[0].Point, _queue[0].Uncertainty);
                _queue.RemoveAt(0);
            }
        }


        /// <summary>
        /// Oblicza zmianę rytmu na danej pozycji kolejki czasów wystąpienia załamków R.
        /// </summary>
        /// <param name="idx">indeks pozycji w tablicy</param>
        /// <param name="list">tablicy punktów charakterystycznych</param>
        /// <returns>zmiana w jednostkach bpm</returns>
        private double HrChangeAt(int idx, IList<UnPoint> list)
        {
            if (idx <= 0 || idx >= list.Count - 1)
                throw new Exception("queueIdx == " + idx);

            double prevRR = list[idx].Point.Time - list[idx - 1].Point.Time;
            double nextRR = list[idx + 1].Point.Time - list[idx].Point.Time;
            return Math.Abs(60.0 / nextRR - 60.0 / prevRR);
        }


        /// <summary>
        /// Wewnętrzna reprezentacja względnej zmiany rytmu
        /// </summary>
        private class HrChange
        {
            /// <summary>
            /// Indeks w kolejce FIFO
            /// </summary>
            public int Index { get; private set; }

            /// <summary>
            /// Wartość względnej zmiany rytmu
            /// </summary>
            public double Change { get; private set; }

            /// <summary>
            /// Metoda tworząca 
            /// </summary>
            /// <param name="index">indeks w kolejce FIFO</param>
            /// <param name="change"> wartość względnej zmiany rytmu</param>
            /// <returns></returns>
            public static HrChange Create(int item1, double item2)
            {
                return new HrChange(item1, item2);
            }

            /// <summary>
            /// Konstruktor
            /// </summary>
            /// <param name="index">indeks w kolejce FIFO</param>
            /// <param name="change">wartość względnej zmiany rytmu</param>
            private HrChange(int index, double change)
            {
                Index = index;
                Change = change;
            }
        }

        /// <summary>
        /// Ustala maksymalną zmianę rytmu w tablicy punktów charakterystycznych
        /// </summary>
        /// <param name="buf">tablica punktów charakterystycznych</param>
        /// <returns>obiekt z opisem maksymalnej względnej zmiany rytmu</returns>
        private HrChange FindMaxHrChange(IList<UnPoint> buf)
        {
            return
                Enumerable
                    .Range(1, buf.Count - 2)
                    .Select(i => HrChange.Create(i, HrChangeAt(i, buf)))
                    .MaxItem(el => el.Change);
        }

        /// <summary>
        /// Podaje pozycję w kolekcji punktów charakterystycznych, której odpowiada największa względna zmiana rytmu
        /// </summary>
        /// <returns>obiekt z opisem maksymalnej względnej zmiany rytmu</returns>
        private HrChange BestToRemove(ICollection<UnPoint> buf)
        {
            return
            Enumerable
                .Range(1, buf.Count - 2)
                .Select(i =>
                    HrChange
                        .Create(
                            i,
                            FindMaxHrChange(buf.Where((sample, i2) => i2 != i).ToList()).Change))
                .MinItem(el => el.Change);
        }

    }
}