using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SasO.Lib.MyMath;


namespace OptoBulkOnnxHr
{
    /// <summary>
    /// OnnxMany2OneSigModel - zamyka model ONNX, wiele syg wejściowych, 1 syg wyjściowy
    /// </summary>
    class OnnxMany2OneSigModel : IDisposable
    {

        InferenceSession _session;
        readonly int _inpLen;
        readonly int[] _inpDim;
        readonly string _inpName;

        public int SeqLen { private set; get; }
        public int SigNum { private set; get; }

        public OnnxMany2OneSigModel(string modelPath)
        {
            _session = new InferenceSession(modelPath);

            var inputMeta = _session.InputMetadata;

            _inpName = inputMeta.Keys.Single();
            _inpDim = inputMeta[_inpName].Dimensions;
            _inpLen = _inpDim.Aggregate((i1, i2) => i1 * i2);

            Console.WriteLine(new { inp = _inpName });
            // 1,2048,9
            Console.WriteLine(new { dim = string.Join(",", _inpDim) });
            Console.WriteLine(new { len = _inpLen });

            if (_inpDim.Length != 3)
                throw new NotImplementedException("not implemented input dimension, " + new { dim = string.Join(",", _inpDim) });

            SeqLen = _inpDim[1];
            SigNum = _inpDim[2];
        }

        /// <summary>
        /// Run model.
        /// </summary>
        /// <returns>result signal</returns>
        /// <param name="inp">input signals (sig_num x seq_len)</param>
        public float[] Run(float[][] inp)
        {
            var iinp = inp.Transpose().SelectMany(item => item).ToArray();
            return InRun(iinp);
        }

        /// <summary>
        /// Run model.
        /// </summary>
        /// <returns>result signal</returns>
        /// <param name="inp">input signals (sig_num x seq_len)</param>
        public double[] Run(double[][] inp)
        {
            IEnumerable<double[]> zinp = inp;
            if (inp.Length < SigNum)
                zinp = zinp.Concat(Enumerable.Repeat(Enumerable.Repeat(0d, SeqLen).ToArray(), SigNum - inp.Length));
            var iinp = zinp.ToArray().Transpose().SelectMany(item => item).Select(samp => (float)samp).ToArray();
            return InRun(iinp).Select(samp => (double)samp).ToArray();
        }


        float[] InRun(float[] inp)
        {
            if (inp.Length != _inpLen)
                throw new NotImplementedException("inp.Length != inpLen");

            var container =
                new[] {
                    NamedOnnxValue.CreateFromTensor(
                        _inpName,
                        new DenseTensor<float>(inp, _inpDim))
                };

            float[] result;

            using (var results = _session.Run(container))
            {
                result = results.Single().AsTensor<float>().ToArray();
            }

            return result;
        }


        bool _disposedValue;


        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    _session.Dispose();
                    _session = null;
                }
                _disposedValue = true;
            }
        }


        public void Dispose()
        {
            Dispose(true);
            // GC.SuppressFinalize(this);
        }

    }

}