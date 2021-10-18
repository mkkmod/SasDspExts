using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Autofac;
using SasO.Signals;
using SasExt.Signals.Tools;
using SasO.Lib;
using OptoOnnxHrAlg;
using SasO.Dsp.DetAlgsCommon;
using System.IO;
using System.Globalization;

namespace SasO.Dsp.OptoOnnxHrAlg
{
    class OnxxHrAlgTests
    {

        [Test]
        public void Test1()
        {
            //const string SourceSigId = "out0";
            //var inpPath = AppTool.AddExeDir("../../../OptoBulkOnnxHr/bin/Debug/out.csv");
            //var inpPath = @"D:\Badania\Opto\Tms_samp_res\xskl_ses_2018_03_28__14-59-04_m989_sav_2018_03_28__15-23-01\onnx_out.csv";
            //var inpPath = @"D:\Badania\Opto\Tms_samp_res\xskl_ses_2019_03_04__08-44-04_m867_sav_2019_03_04__09-05-33\onnx_out.csv";

            //const string SourceSigId = "detNorm";
            //var inpPath = @"/home/mkrej/dyskE/MojePrg/_R/OptoHrSrcSigQuality/dbg_sig_id54.csv";

            bool fltSpec = true;
            const string SourceSigId = "out0";
            var inpPath = @"/home/mkrej/dysk2T/NowyG/Badania/_OPTO/OrtoPlusOptoBad2017_forArt/Received 2020-06-19 2os/xskl_ses_2020_06_19__11-04-42_m672_sav_2020_06_19__11-23-09/agr_det_fun_ekg.csv";
            //var inpPath = @"/home/mkrej/dysk2T/NowyG/Badania/_OPTO/OrtoPlusOptoBad2017_forArt/Received 2020-06-19 2os/xskl_ses_2020_06_19__11-04-42_m672_sav_2020_06_19__11-23-09/agr_det_fun_fbg.csv";

            string outDir = Path.GetDirectoryName(inpPath);

            var builder = new ContainerBuilder();

            builder.RegisterModule(new SignalsModule<float>());

            builder
                .RegisterType<AutoSampCollectorFactory<float>>()
                .As<IAutoSampCollectorFactory<float>>();

            builder.RegisterType<DmlSignalsLoader>();

            builder
                .Register(c => new RelayLog(Console.WriteLine))
                .As<ILog>();


            using (IContainer container = builder.Build())
            {

                var signals = container.Resolve<ISigCollection<float>>();
                var loader = container.Resolve<DmlSignalsLoader>();

                Console.WriteLine("loading " + new { inpPath } + "...");

                float pp = 0 - 1;
                foreach (var p in loader.Load(inpPath))
                {
                    if (p - pp < 0.1) continue;
                    pp = p;
                    Console.Write(".");
                }
                Console.WriteLine("...loaded");

                var sourceSig = signals.Signals.FindOrThrow(SourceSigId).IterSig();
                var detected = new List<SigSample<float>>();

                var collector =
                    new RelayPointsCollector<float>(
                    (sample, un) =>
                    {
                        detected.Add(sample);
                        // Console.WriteLine("detected: " + new { time = sample.Time });
                    });

                var comp = new OptoHrAlgComp(sourceSig, collector, 100, false, Console.WriteLine, fltSpec);

                Console.WriteLine("Running alg");
                while (comp.ComputeNext())
                    Console.Write(".");
                Console.WriteLine("ok");

                comp.Fin();

                //var marks = detected.Select(s => SigSample.Create(s.Time, -0.1f));
                detected.SaveToCsv(Path.Combine(outDir, "test_detHR_points.csv"), "det", sampleToString:(smp) => smp.ToString(CultureInfo.InvariantCulture));
            }

        }

    }
}
