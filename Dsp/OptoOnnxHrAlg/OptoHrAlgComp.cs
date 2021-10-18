using SasO.Dsp;
using SasO.Dsp.DetAlgsCommon;
using SasO.Signals;
using System;

namespace OptoOnnxHrAlg
{

    public class OptoHrAlgComp
    {
        private const double TimeStep = 10.0;

        private readonly IIterSig<float> _sourceSig;

        private IValidPointsCollector<float> _collector;

        private LocalExtremesPlain _locals;

        private readonly Action<object> _debug;

        private double _time;

        private double _pitchBpm;
        private double? _pitchCompTime;
        private readonly MaxNormPowCompPlain _pitchComp;
        private readonly bool _constPitch;

        // private const double ResampPeriod = 0.20;
        // private const int BufSize = 64;

        private const double ResampPeriod = 1.0 / 25;
        private const int BufSize = 128;

        // zmieniam rozmiar bufora FFT z 128 na 256 żeby zagęścić prążki widma bo ostre piki 
        // składowych cz. EKG wypadają między prążkami i przeskakuje na 2gą składową (2020.06.19)
        //private const int BufSize = 256;

        private const double TimeEps = 1e-12;

        private double? _lastDetTime;

        readonly TachoVarDelCorrector _deleteErrRR;


        public OptoHrAlgComp(
            IIterSig<float> sourceSig,
            IValidPointsCollector<float> collector,
            double startPitchBpm = 100,
            bool constPitch = false,
            Action<object> debug = null,
            bool fltSpec = false
            )
        {
            _sourceSig = sourceSig;
            _collector = collector ?? new RelayPointsCollector<float>((samp, un) => { });
            _debug = debug ?? (msg => { });


            //_deleteErrRR = new DeleteErrRRCorrector(_collector, OptoHrAlgCompParam.ErrRRCorrectorMaxRelHrChange, _debug);
            _deleteErrRR = new TachoVarDelCorrector(_collector, OptoHrAlgCompParam.TachoVarGain, _debug);

            _collector = _deleteErrRR;
            _collector = new LowPeakRemove(null, _collector, OptoHrAlgCompParam.DetFunTreshold, _debug);
            _collector = NoRepCollector(_collector);

            _pitchBpm = startPitchBpm;
            _pitchComp = new MaxNormPowCompPlain(_sourceSig, ResampPeriod, BufSize, false, fltSpec);
            _constPitch = constPitch;
        }

        // wywoływać gdy zależy na detekcji na końcówce
        public void Fin()
        {
            _deleteErrRR.Flush();
        }

        private IValidPointsCollector<float> NoRepCollector(IValidPointsCollector<float> decored)
        {
            return
                new RelayPointsCollector<float>(
                    (samp, un) =>
                    {
                        if (!_lastDetTime.HasValue || samp.Time - _lastDetTime.Value > TimeEps)
                        {
                            decored.Push(samp, un);
                            _lastDetTime = samp.Time;
                        }
                        else
                        {
                            _debug("[NoRepCollector] removing " + new { samp.Time });
                        }
                    }
                );
        }

        private LocalExtremesPlain GenLocalFinder(double prevPitch, double newPitchBpm, double time)
        {
            double prevWndHalfWidth = OptoHrAlgCompParam.PitchToWindow(60.0 / prevPitch);
            double newWndHalfWidth = OptoHrAlgCompParam.PitchToWindow(60.0 / newPitchBpm);

            _debug(new { time, newWndHalfWidth });

            var locals = new LocalExtremesPlain(
                newWndHalfWidth,
                (tm, val) =>
                    {
                        _collector.Push(SigSample.Create(tm, val), 0);
                        if (false && Math.Abs(tm - 480) < 1.0)
                        {
                            Console.WriteLine("x");
                        }
                    }
                );

            // gdy zmiana szerokości okna
            // odtworzenie stanu detektora maksimów
            foreach (var sample in _sourceSig.CreateIter(time - Math.Max(prevWndHalfWidth, newWndHalfWidth) * 2, time))
                locals.Next(sample.Time, sample.Value);

            return locals;
        }


        public bool ComputeNext()
        {
            //return _szukEkstr.Next();

            foreach (var sample in _sourceSig.CreateIter(_time, _time + TimeStep))
            {
                _time = sample.Time;

                if (_locals is null)
                    _locals = GenLocalFinder(_pitchBpm, _pitchBpm, _time);

                if (!_pitchCompTime.HasValue)
                    _pitchCompTime = _time;

                _locals.Next(_time, sample.Value);

                if (!_constPitch &&_time > _pitchComp.CompDuration && _time - _pitchCompTime.Value > OptoHrAlgCompParam.PichCompInterval)
                {
                    _pitchCompTime = _time;

                    _pitchComp.Comp(_time, out _, out double newPitchBpm);

                    if (_pitchComp.LastValidFract > 0.5)
                    {

                        _debug(new { _time, newPitchBpm, lastValidFract = Math.Round(_pitchComp.LastValidFract, 1) });
                        if (false && Math.Abs(_time - 477) < 1.0)
                        {
                            Console.WriteLine("x");
                        }

                        if (Math.Abs(newPitchBpm - _pitchBpm) > 5)
                        {
                            _locals = GenLocalFinder(_pitchBpm, newPitchBpm, _time);
                            _pitchBpm = newPitchBpm;
                        }
                    }
                }

            }

            return _time +_sourceSig.Interval < _sourceSig.EndTime;
        }
    }
}