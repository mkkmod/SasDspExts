using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autofac;
using SasO.Dsp.DetAlgsCommon;
using SasO.Signals;

namespace OptoOnnxHrAlg
{
    public class OptoHrAlgCompPlain : IDisposable
    {
        IContainer container;

        public OptoHrAlgCompPlain()
        {
            var builder = new ContainerBuilder();

            builder.RegisterModule(new SignalsModule<float>());

            container = builder.Build();


        }


        public void SetPitchToWindowMul(double value)
        {
            OptoHrAlgCompParam.PitchToWindowMul = value;
        }


        public int[] Comp(double[] sig, double fsample, double startPitchBpm, bool constPitch)
        {
            using (var life = container.BeginLifetimeScope())
            {
                var detected = new List<int>();

                var collector =
                        new RelayPointsCollector<float>(
                        (sample, un) => detected.Add((int)Math.Round(sample.Time * fsample))
                        );

                var factory = life.Resolve<SigFactory<float>>();
                var nnSignal = factory.Create("inp", "inp", SamplingType.EquallySampled, 1.0 / fsample);
                var col = nnSignal.CollectSig().CreateCollector();
                for (int i = 0; i < sig.Length; i++)
                    col.PushSample(i / fsample, (float)sig[i]);

                var comp =
                    new OptoHrAlgComp(
                        nnSignal.IterSig(),
                        collector,
                        startPitchBpm,
                        constPitch,
                        Console.WriteLine
                        );

                while (comp.ComputeNext())
                    Console.Write(".");

                comp.Fin();

                #if false
                Console.WriteLine("det len = " + detected.Count);
                Console.WriteLine("res = " + string.Join(", ", detected));
#endif

                return detected.ToArray();
            }
        }


        private bool disposedValue = false; 

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    container.Dispose();
                }
                disposedValue = true;
            }
        }

   
        public void Dispose()
        {
            Dispose(true);
        }

    }
}