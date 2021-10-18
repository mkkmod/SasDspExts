using System;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace OnnxTest0
{
    /// <summary>
    /// Main class. 
    /// Prog podobny do, tylko tamten na nowego dotnet-a (core) 
    /// a ten na .Net Framework
    /// >> _Sharp/_ProbyMoje/OnnxDotNetApp
    /// </summary>
    class MainClass
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            //var session = new InferenceSession("/home/mkrej/dyskE/MojePrg/_Python/OptoSigNN/learnTCN_model_2020_02_27_n100_nodet0020_onnx/learnTCN_model.onnx");

            //var options = SessionOptions.;
            //var session = new InferenceSession("learnTCN_model.onnx");

            using (var session = new InferenceSession("learnTCN_model.onnx"))
            {
                var inputMeta = session.InputMetadata;
                var container = new List<NamedOnnxValue>();

                //float[] inputData = LoadTensorFromFile(@"bench.in"); // this is the data for only one input tensor for this model

                foreach (var name in inputMeta.Keys)
                {
                    //var tensor = new DenseTensor<float>(inputData, inputMeta[name].Dimensions);
                    //container.Add(NamedOnnxValue.CreateFromTensor<float>(name, tensor));
                    Console.WriteLine(new { name });
                    Console.WriteLine(new { dims = string.Join(", ", inputMeta[name].Dimensions) });
                }

                int sigLen = 2048;
                int sigNum = 9;

                var dim = new int[] { 1, sigLen, sigNum };
                var sourceData = new float[sigLen * sigNum];

                var rnd = new Random();
                for (int i = 0; i < sourceData.Length; i++)
                    sourceData[i] = (float)rnd.NextDouble();

                Tensor<float> t1 = new DenseTensor<float>(sourceData, dim);
                container.Add(NamedOnnxValue.CreateFromTensor<float>("0", t1));

                using (var results = session.Run(container))
                {
                    foreach (var r in results)
                    {
                        Console.WriteLine("Output for {0}", r.Name);
                        Console.WriteLine(r.AsTensor<float>().GetArrayString());
                    }
                }
            }

            Console.WriteLine("DONE");
        }
    }
}
