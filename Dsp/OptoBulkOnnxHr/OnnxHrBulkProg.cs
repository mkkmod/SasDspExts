using Autofac;
using OptoBulkOnnxHr.Properties;
using OptoOnnxHrAlg;
using SasO.Dsp;
using SasO.Dsp.DetAlgsCommon;
using SasO.Lib;
using SasO.Lib.Csv;
using SasO.Lib.MyMath;
using SasO.Signals;
using SasO.Streams.Xskl;
using SasO.Streams.XsklFile;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OptoBulkOnnxHr
{
    class OnnxHrBulkProg
    {
        private const double InpFSample = 1000.0;
        private const int DownSample = 8;
        private const double FSample = InpFSample / DownSample;

        // szerokość okna normalizującego w próbkach
        private const int NormWindow = (int)(3.0 * FSample);
        // krok w probkach
        private const int NormStride = 0xF;
        private const string ModelPath = "learnTCN_model.onnx";


        readonly SosFilterDescr _hrDesc;
        readonly SosFilterDescr _down8Desc;

        // filtr dolnoprzepustowy dla sygnału zsumowanego
        readonly SosFilterDescr _sumLpDesc;

        // filtr dolnoprzepustowy dla wyjścia modelu (wygładzający)
        readonly SosFilterDescr _outFtDesc;

        private readonly bool _modeLoadPrevModelOut;

        public OnnxHrBulkProg(bool modeLoadPrevModelOut)
        {
            /*  
             *  Paths file
                # R
                recDir = "F:/_OPTO/TmsFull/Received/"
                files = dir(recDir, "*\\.xskl", recursive = T)
                ffiles = paste(recDir, files, sep='')
                write.table(ffiles, file = "PathsFile.txt", row.names = F, quote = F, col.names = F)
             *
             */

            /*

                # Python
                import numpy as np
                from scipy import signal
                fsamp = 1000
                fparam = np.array([6, 20])
                sos = signal.butter(2, fparam / fsamp * 2, "bandpass", output = 'sos')
                zi0 = signal.sosfilt_zi(sos)
                for row in sos:
                    print("new[] {{ {} }},".format(", ".join("{}".format(x) for x in row )))
                for row in zi0:
                    print("new[] {{ {} }},".format(", ".join("{}".format(x) for x in row )))

            */

            // + 2 jednynki wzmocnienia w każdym wierszu!
            double[][] hrCoefs =
            {
                new[] { 0.001820128711054488, 0.003640257422108976, 0.001820128711054488, 1.0, -1.9024304981704596, 0.9141726484707781 , 1, 1},
                new[] { 1.0, -2.0, 1.0, 1.0, -1.9641322776562489, 0.9659292345166521, 1, 1 },
            };

            double[][] hrZi0 =
            {
                new[] { 0.6182123745367112, -0.5649966269209218 },
                new[] { -0.6200325032477565, 0.6200325032477568 },
            };

            _hrDesc = SosFilterDescr.FromCsv(hrCoefs, hrZi0);

            /*
                # Python
                import numpy as np
                from scipy import signal
                down = 8
                sos = signal.butter(5, .8 / down, output='sos')
                zi0 = signal.sosfilt_zi(sos)
                for row in sos:
                    print("new[] {{ {} }},".format(", ".join("{}".format(x) for x in row )))
                for row in zi0:
                    print("new[] {{ {} }},".format(", ".join("{}".format(x) for x in row )))

            */

            // + 2 jednynki wzmocnienia w każdym wierszu!
            double[][] down8Coefs =
            {
                new[] { 5.979578037000323e-05, 0.00011959156074000646, 5.979578037000323e-05, 1.0, -0.726542528005361, 0.0, 1.0, 1.0 },
                new[] { 1.0, 2.0, 1.0, 1.0, -1.521690426072246, 0.6000000000000001, 1.0, 1.0 },
                new[] { 1.0, 1.0, 0.0, 1.0, -1.7363101655347295, 0.8256645486206606, 1.0, 1.0 },
            };

            double[][] down8Zi0 =
            {
                new[] { 0.0008148671781345718, 5.979578037000323e-05 },
                new[] { 0.043802528584461056, -0.02593165196727481 },
                new[] { 0.9553228084570361, -0.8256645486206621 },
            };

            _down8Desc = SosFilterDescr.FromCsv(down8Coefs, down8Zi0);

            /*
                # Python
                import numpy as np
                from scipy import signal
                fsamp = 1000 / 8
                fparam = 4.0
                sos = signal.butter(5, fparam / fsamp * 2, output = 'sos')
                zi0 = signal.sosfilt_zi(sos)
                for row in sos:
                    print("new[] {{ {} }},".format(", ".join("{}".format(x) for x in np.append(row, [1., 1.]) )))
                for row in zi0:
                    print("new[] {{ {} }},".format(", ".join("{}".format(x) for x in row )))
            */

            // (2 jednynki wzmocnienia w każdym wierszu)
            double[][] sumLpCoefs =
            {
                new[] { 7.5378909682166275e-06, 1.5075781936433255e-05, 7.5378909682166275e-06, 1.0, -0.8167432698726974, 0.0, 1.0, 1.0 },
                new[] { 1.0, 2.0, 1.0, 1.0, -1.6871236197534254, 0.721809379507925, 1.0, 1.0 },
                new[] { 1.0, 1.0, 0.0, 1.0, -1.8457988824427625, 0.8837468643463591, 1.0, 1.0 },
            };

            double[][] sumLpZi0 =
            {
                new[] { 0.00015699393196631453, 7.5378909682166275e-06 },
                new[] { 0.01880945912886378, -0.013531072812771992 },
                new[] { 0.9810260090481987, -0.8837468643463564 },
            };

            _sumLpDesc = SosFilterDescr.FromCsv(sumLpCoefs, sumLpZi0);


            /*
                # Python

                # signal.butter(4, 5 / (.5 * fsamp))

                import numpy as np
                from scipy import signal
                fsamp = 1000 / 8
                fparam = 5.0

                sos = signal.butter(5, fparam / (.5 * fsamp), output = 'sos')
                zi0 = signal.sosfilt_zi(sos)
                for row in sos:
                    print("new[] {{ {} }},".format(", ".join("{}".format(x) for x in np.append(row, [1., 1.]) )))
                for row in zi0:
                    print("new[] {{ {} }},".format(", ".join("{}".format(x) for x in row )))

            */

            // (2 jednynki wzmocnienia w każdym wierszu)
            double[][] outFtCoefs =
            {
                new[] { 2.139615203813592e-05, 4.279230407627184e-05, 2.139615203813592e-05, 1.0, -0.7756795110496131, 0.0, 1.0, 1.0 },
                new[] { 1.0, 2.0, 1.0, 1.0, -1.6127001681678703, 0.6650095034572857, 1.0, 1.0 },
                new[] { 1.0, 1.0, 0.0, 1.0, -1.7989203686469164, 0.8572699184143814, 1.0, 1.0 },
            };

            double[][] outFtZi0 =
            {
                new[] { 2.139615203813592e-05, 4.279230407627184e-05, 2.139615203813592e-05, 1.0, -0.7756795110496131, 0.0, 1.0, 1.0 },
                new[] { 1.0, 2.0, 1.0, 1.0, -1.6127001681678703, 0.6650095034572857, 1.0, 1.0 },
                new[] { 1.0, 1.0, 0.0, 1.0, -1.7989203686469164, 0.8572699184143814, 1.0, 1.0 },
            };

            _outFtDesc = SosFilterDescr.FromCsv(outFtCoefs, outFtZi0);


            _modeLoadPrevModelOut = modeLoadPrevModelOut;
        }


        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            try
            {
                Console.WriteLine("start");
                var logsDir = AppTool.AddExeDir("Logs");
                new DirectoryInfo(logsDir).Create();

#pragma warning disable CS0162 // Unreachable code detected
                if (true)
                {
                    string dumpPath = Path.Combine(logsDir, DateTime.Now.FileTimeString() + " log.txt");
                    Console.WriteLine("Logs: " + new { dumpPath });
                    Trace.Listeners.Add(new TextWriterTraceListener(dumpPath));
                    Trace.WriteLine("start ");
                    Trace.WriteLine(new { time = DateTime.Now });
                }
                else
                {
                    Trace.Listeners.Add(new ConsoleTraceListener());
                }
#pragma warning restore CS0162 // Unreachable code detected

                if (args.Length != 1)
                {
                    Console.WriteLine("parameter - paths file");
                    return;
                }


                new OnnxHrBulkProg(Settings.Default.LoadPrevModelOut).Run(args[0]);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.ReadLine();
                Trace.WriteLine(ex);
                Trace.Flush();
            }
            Trace.Flush();

        }


        void Run(string pathsFile)
        {
            var paths = File.ReadAllLines(pathsFile).AsEnumerable();

            paths =
                paths
                    .Where(el => !string.IsNullOrEmpty(el.Trim()))
                    .Where(el => !el.TrimStart().StartsWith("#"));

            var builder = new ContainerBuilder();

            builder
                .RegisterModule(new StreamsModule(1));

            builder
                .RegisterModule(new SignalsModule<float>());

            builder
                .RegisterType<XsklFileCreate>();

            builder
                .RegisterType<XsklFileRead>();

            using (IContainer container = builder.Build())
            {
                foreach (var path in paths)
                {
                    //Console.WriteLine(new { path });
                    try
                    {
                        ProcFile(container, path);
                        Trace.Flush();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex);
                        Trace.WriteLine(ex);
                        Trace.Flush();
                    }
                }
            }


            Console.WriteLine("done");
            Trace.WriteLine("done");
            Trace.Flush();
            //Console.ReadLine();

        }


        void ProcFile(IContainer container, string path)
        {
            using (var life = container.BeginLifetimeScope())
            {
                string repPath = path;
                if (!string.IsNullOrEmpty(Settings.Default.SourceDirPart))
                    repPath = repPath.Replace(Settings.Default.SourceDirPart, Settings.Default.DestDirPart);
                string outDir = Path.GetDirectoryName(repPath);
                if (!Directory.Exists(outDir))
                    Directory.CreateDirectory(outDir);

                var signals = life.Resolve<ISigCollection<float>>();

                string outSigIdPref = "out";
                string modelOutPath = Path.Combine(outDir, "onnx_out.csv");

                ISignal<float> nnSignal;

                if (!_modeLoadPrevModelOut)
                {
                    var header = XmlExt.Load<Header>(path);
                    Trace.Write("loading '" + path + "' ");
                    Trace.WriteLine(new { time = DateTime.Now });
                    Trace.Flush();
                    foreach (var p in life.Resolve<XsklFileRead>().ReadFile(header, Path.GetDirectoryName(path) ?? ""))
                    {
                        Trace.Write(".");
                    }
                    Trace.WriteLine("");
                    Trace.WriteLine("..loaded");
                    Trace.Flush();

                    //var srcId = signals.Signals.Select(el => el.Info.Id).Where(el => el.StartsWith("peak", StringComparison.Ordinal)).ToArray();
                    var inpSignals = signals.Signals.Where(el => el.Info.Id.StartsWith("peak", StringComparison.Ordinal)).ToArray();
                    var srcIds = inpSignals.Select(el => el.Info.Id).ToArray();
                    Trace.WriteLine(new { srcIds = string.Join(", ", srcIds) });

                    // warunek max 1 pakiet na 100s
                    var contSigs =
                            inpSignals
                                .Where(
                                    sig =>
                                        sig.IterSig().PacketsIter().Count() * 100 < sig.Info.EndTime - sig.Info.StartTime)
                                .ToArray();
                    if (contSigs.Length < inpSignals.Length)
                        Trace.WriteLine("Chopped signals skipped : " + (inpSignals.Length - contSigs.Length));

                    Trace.Write("padding ...");
                    double start = inpSignals.Select(el => el.Info.StartTime).Min();
                    var padSigs =
                        contSigs
                            //.Select(sig => sig.EqSampPaddedIter(0f, start).Select(el => (double)el.Value).ToArray())
                            .Where(sig => 
                                   sig.IterSig().PacketsIter().Count() * 100 < 
                                   (sig.Info.EndTime-sig.Info.StartTime) / sig.Info.Interval
                                  )
                            .Select(sig => sig.EqSampPadRepIter(start).Select(el => (double)el.Value).ToArray())
                            .ToArray();

                    var packNum = inpSignals.Select(sig => sig.IterSig().PacketsIter().Count());


                    packNum = new[] { packNum.Sum() }.Concat(packNum);
                    File.WriteAllLines(Path.Combine(outDir, "pack_num.csv"), new[] { string.Join(", ", packNum) });

                    Trace.WriteLine("ok");
                    Trace.Flush();

                    Trace.Write("preprocessing ...");
                    double[][] prepSigs =
                        padSigs
                            .SosFilter(_hrDesc)
                            .SosFilter(_down8Desc)
                            .Downsample(DownSample)
                            .MovingQuantilesNorm(NormWindow, NormStride);

                    Trace.WriteLine("ok");
                    Trace.Flush();


                    //Trace.Write("saving 'prep_signals.csv' ...");
                    //SaveSigCsv(prepSigs, "prep_signals.csv", "sig", FSample);
                    //Trace.WriteLine("ok");
                    //Trace.Flush();

                    Trace.Write("running model ...");
                    // var outp = SingleRun(prepSigs);
                    var outp = FullRun(prepSigs);
                    //var outp = AggrDetFunRun(prepSigs);
                    Trace.WriteLine("ok");
                    Trace.Flush();

                    Trace.Write($"saving {modelOutPath} ...");
                    SaveSigCsv(new[] { outp }, modelOutPath, outSigIdPref, FSample);
                    Trace.WriteLine("ok");
                    Trace.Flush();

                    var factory = life.Resolve<SigFactory<float>>();
                    nnSignal = factory.Create(outSigIdPref, outSigIdPref, SamplingType.EquallySampled, 1.0 / FSample);
                    var col = nnSignal.CollectSig().CreateCollector();
                    for (int i = 0; i < outp.Length; i++)
                        col.PushSample(i / FSample, (float)outp[i]);
                }
                else
                {
                    Trace.Write("loading '" + modelOutPath + "' ");
                    Trace.WriteLine(new { time = DateTime.Now });
                    Trace.Flush();

                    if (!File.Exists(modelOutPath))
                        throw new Exception("Mode LoadPrevModelOut && model file not exists " + new { modelOutPath });

                    signals.SigLoadFromCsv(modelOutPath);
                    nnSignal = signals.Signals.First();
                }


                Trace.Write("run alg");
                var detected = RunAlg(nnSignal.IterSig());
                Trace.WriteLine("ok");
                Trace.Flush();

                detected.SaveToCsv(Path.Combine(outDir, "detHR_points.csv"), "det", "0.0000", smp => smp.ToString(CultureInfo.InvariantCulture));

                var saveTool = new TachoTool();
                saveTool.TimesToBpmAndToCsv(
                       detected.Select(el => el.Time).ToArray(),
                       Path.Combine(outDir, "detectedHR.csv"),
                       "detectedHR"
                       );

            }

        }


        private IEnumerable<SigSample<float>> RunAlg(IIterSig<float> sourceSig)
        {
            var detected = new List<SigSample<float>>();

            // Console.WriteLine("detected: " + new { time = sample.Time });
            var collector =
                    new RelayPointsCollector<float>(
                        (sample, un) => detected.Add(sample)
                    );

            var comp =
                new OptoHrAlgComp(
                    sourceSig,
                    collector,
                    100,
                    false,
                    msg => Trace.WriteLine(msg)
                    );

            while (comp.ComputeNext())
                Trace.Write(".");

            return detected;
        }


        private static double[] SingleRun(double[][] prepSigs)
        {
            using (var model = new OnnxMany2OneSigModel(ModelPath))
            {
                var frame =
                    prepSigs
                        .Select(sig => sig.Range((int)(5 * FSample), model.SeqLen).ToArray())
                        .ToArray();
                return model.Run(frame);

            }
        }


        private double[] AggrDetFunRun(double[][] prepSigs)
        {
            return
                prepSigs
                    .Select(el => el.AsEnumerable())
                    .Aggregate((sig1, sig2) => sig1.Zip(sig2, (s1, s2) => s1 + s2))
                    .Select(s => s / prepSigs.Length)
                    .Select(s => s * s)
                    .ToArray()
                    .SosFilter(_sumLpDesc);
        }


        private double[] FullRun(double[][] prepSigs)
        {
            using (var model = new OnnxMany2OneSigModel(ModelPath))
            {
                // TODO ew. innne kryterium dla seqMarg
                int seqMarg = model.SeqLen / 8;
                int seqStep = model.SeqLen - seqMarg;
                int sigLen = prepSigs.First().Length;
                int stepsNum = (sigLen - seqMarg) / seqStep;
                int infoIv = Math.Max(1, stepsNum / 10);

                return
                    Enumerable
                        .Range(0, stepsNum)
                        .Select(i =>
                            {
                                var frame =
                                    prepSigs
                                        .Select(sig => sig.Range(i * seqStep, model.SeqLen).ToArray())
                                        .ToArray();
                                var part = model.Run(frame);
                                if (i % infoIv == 0)
                                {
                                    Trace.Write(".");
                                    Trace.Flush();
                                }
                                // dbg mark
                                // part[model.SeqLen - 1] = -0.1; 
                                return i == 0 ? part : part.Skip(seqMarg).ToArray();
                            })
                        .SelectMany(seq => seq)
                        .ToArray()
                        .SosFilter(_outFtDesc);
            }
        }


        static void SaveSigCsv(IEnumerable<double[]> signals, string path, string sigNamePref, double fSample)
        {
            var tm = Enumerable.Range(0, signals.First().Length).Select(ii => ii / fSample).ToArray();
            var df = new[] { tm }.Concat(signals);
            var hd = new[] { "tm" }.Concat(signals.Select((s, ii) => sigNamePref + ii.ToString()));
            CsvPrinter.PrintTables(path, df, hd, cell => cell.ToString(CultureInfo.InvariantCulture));
        }


    }

}
