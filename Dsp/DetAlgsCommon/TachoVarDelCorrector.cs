using System;
using System.Collections.Generic;
using System.Linq;
using SasO.Lib.MyMath;
using SasO.Signals;

namespace SasO.Dsp.DetAlgsCommon
{
    /// <summary>
    /// Korektor odstępów uderzeń oparty na stosunku zmienności odstępów (tacho)
    /// 
    /// Przerobiony z DeleteErrRRCorrector.
    /// </summary>
    public class TachoVarDelCorrector : IValidPointsCollector<float>
    {

        private readonly IValidPointsCollector<float> _outCollector;

        /// <summary>
        /// Rozmiar kolejki
        /// </summary>
        private const int QueueSize = 7;

        /// <summary>
        /// Indeks elementu środkowego kolejki
        /// </summary>
        private const int QueueMiddle = 3;

        /// <summary>
        /// Wymagane zmniejszenie zmienności tacho dzięki usunięciu punktu.
        /// To zmniejszenie powinno być wyraźne, żeby była pewność że trzeba usunąć.
        /// </summary>
        private readonly double _tachoVarGain;

        /// <summary>
        /// Kolejka FIFO punktów charakterystycznych
        /// </summary>
        private readonly List<UnPoint> _queue = new List<UnPoint>();

        /// <summary>
        /// Gdy w kolejce jest jakiś b. długi RR (powyżej tej wartości [s]) to już nic nie usuwać.
        /// </summary>
        private const double LongRR = 2.0;

        /// <summary>
        /// Debug log
        /// </summary>
        private readonly Action<object> _debug;

        int _blockAfterRemove;

        /// <summary>
        /// Konstruktor obiektów korektora
        /// </summary>
        /// <param name="outCollector">dekorowany kolektor pozycji punktów charakterystycznych</param>
        /// <param name="tachoVarGain">wymagane zmniejszenie zmienności tacho dzięki usunięciu punktu</param>
        /// <param name="debug">debug log</param>
        public TachoVarDelCorrector(IValidPointsCollector<float> outCollector, double tachoVarGain, Action<object> debug = null)
        {
            _outCollector = outCollector;
            _tachoVarGain = tachoVarGain;
            _debug = debug ?? (msg => { });
            Enabled = true;
            _blockAfterRemove = 0;
        }

        /// <summary>
        /// True - kolektor włączony
        /// </summary>
        public bool Enabled { get; set; }


        public void Flush()
        {
            foreach (var item in _queue)
                _outCollector.Push(item.Point, item.Uncertainty);
            _queue.Clear();
        }


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


#if false
            var dbgTime = new[] { 374.0, 420.350, 386.100, 968.8 };
            if (dbgTime.Any(tm => Math.Abs(_queue[QueueMiddle].Point.Time - tm) < 200e-3))
            {
                _debug("***");
            }
#endif

            // gdy w kolejce jest jakiś b. długi RR to już nic nie usuwać
            var alreadyLong = _queue.Select(el => el.Point.Time).Diff().Any(rr => rr > LongRR);

            if (_blockAfterRemove == 0 && !alreadyLong)
            {
                double tachoPre = TachoVar(_queue);
                double tachoRem = TachoVar(_queue.Where((sample, ii) => ii != QueueMiddle));

                if (tachoRem < _tachoVarGain * tachoPre)
                {
                    _debug(
                        "[TachoVarDelCorrector] removing " +
                        new
                        {
                            _queue[QueueMiddle].Point.Time,
                            pre = Math.Round(tachoPre, 3),
                            rem = Math.Round(tachoRem, 3),
                            gain = Math.Round(tachoRem / Math.Max(1e-10, tachoPre), 3)
                        });
                    foreach (UnPoint point in _queue)
                    {
                        point.Uncertainty++;
                    }
                    _queue.RemoveAt(QueueMiddle);
                    _blockAfterRemove += QueueMiddle + 1;
                }
#if false
                else
                {
                    var dbgTime = new[] { 374.0, 420.350, 386.100, 968.8 };
                    if (dbgTime.Any(tm => Math.Abs(_queue[QueueMiddle].Point.Time - tm) < 200e-3))
                    {
                        _debug(
                            "[TachoVarDelCorrector] NOT REMOVING " + 
                            new { 
                                _queue[QueueMiddle].Point.Time, 
                                pre = Math.Round(tachoPre,3), 
                                rem = Math.Round(tachoRem, 3),
                                gain = Math.Round(tachoRem / Math.Max(1e-10, tachoPre), 3)
                            });
                    }
                }
#endif
            }

            if (_blockAfterRemove > 0)
                _blockAfterRemove--;

            _outCollector.Push(_queue[0].Point, _queue[0].Uncertainty);
            _queue.RemoveAt(0); // remove after push

        }


        private double TachoVar(IEnumerable<UnPoint> points)
        {
            return
                points
                    .Select(el => el.Point.Time)
                    .Diff()
                    .SafeStdStat()
                    .StdDeviation;
        }

#if false
        private int IdxOfMinVarAfterRemove(ICollection<UnPoint> buf)
        {
            //var dt = buf.Select(el => el.Point.Time).Intervals().SafeStdStat().StdDeviation;

            return
                Enumerable
                    .Range(0, buf.Count)
                    .Select(i =>
                        new
                        {
                            idx = i,
                            var = TachoVar(buf.Where((sample, i2) => i2 != i))
                        }
                        )
                    .MinItem(el => el.var).idx;
        }
#endif

    }

}