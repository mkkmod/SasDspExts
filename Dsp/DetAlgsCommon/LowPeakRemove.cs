using System;
using SasO.Dsp.DetAlgsCommon;
using SasO.Signals;

namespace SasO.Dsp.DetAlgsCommon
{
    public class LowPeakRemove : IValidPointsCollector<float>
    {
        private readonly ISigProbe<float> _detFun;

        private readonly IValidPointsCollector<float> _outCollector;
        private readonly double _threshold;
        private readonly Action<object> _debug;

        public bool Enabled
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public LowPeakRemove(ISigProbe<float> detFun, IValidPointsCollector<float> outCollector, double threshold, Action<object> debug = null)
        {
            _detFun = detFun;
            _outCollector = outCollector;
            _threshold = threshold;
            _debug = debug ?? (msg => { });

        }

        public void Push(SigSample<float> foundPoint, int uncertainty)
        {
            if (_detFun != null && _detFun.Sample(foundPoint.Time).Value > _threshold ||
                _detFun == null && foundPoint.Value > _threshold
                )
            {
                _outCollector.Push(foundPoint, uncertainty);
            }
            else
            {
                _debug("[LowPeakRemove] removing " + new { foundPoint.Time });
            }
        }
    }
}