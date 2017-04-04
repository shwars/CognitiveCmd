using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Face
{
    class Program
    {
        static string Dir;
        static void Main(string[] args)
        {
            Dir = AppDomain.CurrentDomain.BaseDirectory; // Environment.CurrentDirectory; // Path.GetDirectoryName(Environment.CommandLine.Trim('"', '\n', '\r', ' '));
            if (args.Length==0)
            {
                Console.WriteLine(@"
face.exe - Cognitive Services Face API command-line client
  face key [<key>] - set service key in face.key text file
  face detect [<url>|<file>] - call face detection on URL");
                return;
            }
            switch(args[0])
            {
                case "key":
                    WriteKeyFile(GetArg(args));
                    break;
                case "detect":
                    Detect(GetArg(args));
                    break;
                case "persongroup":
                    CreatePG(GetArg(args),GetArg(args,2));
                    break;
                default:
                    Console.WriteLine($"Unknown command: {args[0]}");
                    break;
            }
        }

        private static void CreatePG(string name, string path)
        {
            Console.WriteLine($" - trying to delete person group {name}");
            try { Client.DeletePersonGroupAsync(name).Wait(); }
            catch { }
            Console.WriteLine($" - creating person group {name}");
            Client.CreatePersonGroupAsync(name, name).Wait();
            var dir = Path.Combine(Environment.CurrentDirectory, path);
            foreach(var dr in Directory.EnumerateDirectories(dir))
            {
                var n = Path.GetFileName(dr);
                Console.Write($" - Creating person {n}");
                var t = Client.CreatePersonAsync(name, n);
                t.Wait();
                Console.WriteLine($" -> {t.Result.PersonId}");
                var pid = t.Result.PersonId;
                foreach(var f in Directory.EnumerateFiles(Path.GetFullPath(dr)))
                {
                    Console.Write($" --- uploading face {Path.GetFileName(f)}");
                    var t1 = Client.AddPersonFaceAsync(name, pid, OpenStream(f));
                    t1.Wait();
                    Console.WriteLine($" -> {t1.Result.PersistedFaceId}");
                    Task.Delay(1000).Wait();
                }
            }
            Console.WriteLine($" - training");
            Client.TrainPersonGroupAsync(name).Wait();
            Status st = Status.Running;
            while (st==Status.Running)
            {
                var t = Client.GetPersonGroupTrainingStatusAsync(name);
                t.Wait();
                st = t.Result.Status;
                Task.Delay(5000).Wait();
            }
            Console.WriteLine($" - Status={st}");
        }

        private static FaceServiceClient _cli;
        private static FaceServiceClient Client
        {
            get
            {
                if (_cli == null) _cli = new FaceServiceClient(GetKey());
                return _cli;
            }
        }

        private static void Detect(string arg)
        {
            var str = OpenStream(arg);
            var aw = Client.DetectAsync(str,returnFaceAttributes: new FaceAttributeType[] { FaceAttributeType.Age, FaceAttributeType.Gender, FaceAttributeType.Smile });
            aw.Wait();
            var res = aw.Result;
            Console.WriteLine($" - faces detected: {res.Length}");
            int i = 1;
            foreach (var f in res)
            {
                Console.WriteLine($" #{i++}: {f.FaceAttributes.Gender}, {f.FaceAttributes.Age} years old, smile={f.FaceAttributes.Smile}");
            }
        }

        private static Stream OpenStream(string x)
        {
            if (x.StartsWith("http"))
            {
                var cli = new HttpClient();
                var t = cli.GetStreamAsync(x);
                t.Wait();
                return t.Result;
            }
            else
            {
                return File.OpenRead(x);
            }
        }

        private static string GetArg(string[] args, int n = 1)
        {
            if (args.Length <= n) return Console.ReadLine();
            else return args[n];
        }

        private static void WriteKeyFile(string v)
        {
            var fn = Path.Combine(Dir, "face.key");
            var f = File.CreateText(fn);
            f.WriteLine(v);
            f.Close();
        }

        private static string GetKey()
        {
            var fn = Path.Combine(Dir, "face.key");
            try
            {
                var f = File.OpenText(fn);
                var s = f.ReadLine();
                f.Close();
                return s;
            }
            catch
            {
                Console.WriteLine($"Cannot read face api key from {fn}");
                return "";
            }
        }

    }
}
