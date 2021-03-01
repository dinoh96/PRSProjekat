using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Random;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PRSProjekatJun2020
{
    class ThreadedSimulation
    {
        private long StartingTime;
        private long TimeInMiliSeconds;
        private int Kmin;
        private int Kmax;
        private int fixedServers;

        private int jobs;
        private int k;
        private int DeviceNumber;

        private bool IsInitialized;

        public enum Server : int { CPU = 0, SD1, SD2, SD3, UD1, UD2, UD3, UD4, UD5, UD6, UD7, UD8, Error }
        public enum States : int { IDLE, INPROGRESS, FINISHING, FINISHED }

        public static Semaphore sem = new Semaphore(1, 1);

        private List<int> Queue;
        private List<int> ETA;
        private List<bool> Lock;

        private List<double> X;
        private List<double> U;
        private List<double> Jobs;
        private List<double> N;

        private List<double> XPrint;
        private List<double> UPrint;
        private List<double> JobsPrint;
        private List<double> NPrint;
        private double SystemResponse;

        private List<double> XBuzen;
        private List<double> UBuzen;
        private List<double> JobsBuzen;
        private List<double> NBuzen;
        private double SystemResponseBuzen;

        private List<double> XSingle;
        private List<double> USingle;
        private List<double> JobsSingle;
        private List<double> NSingle;

        private List<List<double>> XMulti;
        private List<List<double>> UMulti;
        private List<List<double>> JobsMulti;
        private List<List<double>> NMulti;


        private States SimulationState;

        public List<int> S { get; }
        public string Output { get; }
        public string OutputMean { get; }
        public string OutputDeviation { get; }
        public string OutputDeviationMean { get; }

        private Matrix<double> P;
        private int BottleNeck;
        public List<int> SimulationParameters { get; set; }

        public ThreadedSimulation(List<int> S, List<int> SimulationParameters, string FileName, string FileNameMean, string FileNameDeviation, string FileNameDeviationMean, int TimeInMinutes = 24 * 60, int fixedServers = 4, int Kmin = 2, int Kmax = 8)
        {
            StartingTime = TimeInMinutes * 60 * 1000;

            this.S = S;
            Output = FileName;
            OutputMean = FileNameMean;
            OutputDeviation = FileNameDeviation;
            OutputDeviationMean = FileNameDeviationMean;
            this.fixedServers = fixedServers;
            this.Kmin = Kmin;
            this.Kmax = Kmax;
            this.SimulationParameters = SimulationParameters;
            IsInitialized = false;
        }

        public void Init(Matrix<double> P, int k, int jobs)
        {
            this.k = k;
            this.jobs = jobs;
            this.P = P;


            DeviceNumber = fixedServers + k;


            Queue = new List<int>(DeviceNumber);
            ETA = new List<int>(DeviceNumber);
            Lock = new List<bool>(DeviceNumber);

            X = new List<double>(DeviceNumber);
            U = new List<double>(DeviceNumber);
            Jobs = new List<double>(DeviceNumber);
            N = new List<double>(DeviceNumber);

            XPrint = new List<double>(DeviceNumber);
            UPrint = new List<double>(DeviceNumber);
            JobsPrint = new List<double>(DeviceNumber);
            NPrint = new List<double>(DeviceNumber);

            XSingle = new List<double>(DeviceNumber);
            USingle = new List<double>(DeviceNumber);
            JobsSingle = new List<double>(DeviceNumber);
            NSingle = new List<double>(DeviceNumber);

            XMulti = new List<List<double>>(SimulationParameters.Count());
            UMulti = new List<List<double>>(SimulationParameters.Count());
            JobsMulti = new List<List<double>>(SimulationParameters.Count());
            NMulti = new List<List<double>>(SimulationParameters.Count());

            Populate();
        }

        public void Populate()
        {

            for (int i = 0; i < SimulationParameters.Count(); i++)
            {
                XMulti.Add(new List<double>(DeviceNumber));
                UMulti.Add(new List<double>(DeviceNumber));
                JobsMulti.Add(new List<double>(DeviceNumber));
                NMulti.Add(new List<double>(DeviceNumber));
            }

            for (int i = 0; i < DeviceNumber; i++)
            {
                if (i == 0)
                {

                    Queue.Add(jobs - 1);
                    ETA.Add(CalculateProcessDuration(i));
                    Jobs.Add(1);
                    N.Add(jobs - 1);

                }
                else
                {
                    Queue.Add(0);
                    ETA.Add(-1);
                    Jobs.Add(0);
                    N.Add(0);
                }
                X.Add(0);
                U.Add(0);
                Lock.Add(true);

                XPrint.Add(0);
                UPrint.Add(0);
                NPrint.Add(0);
                JobsPrint.Add(0);

                XSingle.Add(0);
                USingle.Add(0);
                NSingle.Add(0);
                JobsSingle.Add(0);

                for (int k = 0; k < SimulationParameters.Count(); k++)
                {
                    XMulti[k].Add(0);
                    UMulti[k].Add(0);
                    NMulti[k].Add(0);
                    JobsMulti[k].Add(0);
                }

            }

            TimeInMiliSeconds = StartingTime;
            SimulationState = States.IDLE;
            IsInitialized = true;
            BottleNeck = -1;
        }

        public void Reset()
        {

            for (int i = 0; i < DeviceNumber; i++)
            {
                if (i == 0)
                {

                    Queue[i] = jobs - 1;
                    ETA[i] = CalculateProcessDuration(i);
                    Jobs[i] = 1;
                    N[i] = 0;

                }
                else
                {
                    Queue[i] = 0;
                    ETA[i] = -1;
                    Jobs[i] = 0;
                    N[i] = 0;
                }

                X[i] = 0;
                U[i] = 0;
                Lock[i] = true;

            }

            TimeInMiliSeconds = StartingTime;
            SimulationState = States.IDLE;
            IsInitialized = true;
            BottleNeck = -1;
        }

        public void Tick()
        {
            int i;
            TimeInMiliSeconds--;

            for (i = 0; i < DeviceNumber; i++)
            {
                if (ETA[i] > -1)
                {
                    ETA[i]--;
                }
                Lock[i] = false;
                N[i] += Queue[i];
            }

            if (TimeInMiliSeconds <= 0) SimulationState = States.FINISHED;
            else SimulationState = States.INPROGRESS;


            for (i = 0; i < DeviceNumber; i++)
            {
                if (ETA[i] >= 0)
                {
                    // proces se izvrsava na i-tom serveru
                    U[i]++;                                         // iskoriscenje resursa 
                    N[i]++;
                }

                if (ETA[i] == 0)
                {
                    // proces je zavrsio, prebaci ga na sledeci server, zakljucaj server na koji posaljes da bi mogao da preuzme posao tek u sledecem taktu
                    int to = CalculateNextServer(i);
                    Queue[to]++;
                    Lock[to] = true;    
                }
                if (ETA[i] == -1 && Queue[i] > 0 /*&& !Lock[i]*/)
                {
                    // proces se ucitava u server i od sledeceg takta krece da se izvrsava

                    ETA[i] = CalculateProcessDuration(i);
                    Queue[i]--;
                    Jobs[i]++;
                }
            }
        }
        public void Print2()
        {
            Console.Write("\nU:\t");

            for (int i = 0; i < DeviceNumber; i++)
                Console.Write("{0}\t", U[i]);
            Console.Write("\nX:\t");

            for (int i = 0; i < DeviceNumber; i++)
                Console.Write("{0}\t", X[i]);
            Console.Write("\nN:\t");

            for (int i = 0; i < DeviceNumber; i++)
                Console.Write("{0}\t", N[i]);
            Console.Write("\nJobs:\t");

            for (int i = 0; i < DeviceNumber; i++)
                Console.Write("{0}\t", Jobs[i]);
            Console.WriteLine();

        }

        private int CalculateProcessDuration(int to)
        {
            SystemRandomSource randomNumberGenerator = SystemRandomSource.Default;
            //double rand = randomNumberGenerator.NextDouble();
            //if ((rand = Math.Round(rand, 5)) == 1) rand = .99999;
            //double randomNumber = rand;
            //double x = -Math.Log(1.0 - randomNumber) * S[to];
            //int ret = (int)Math.Ceiling(x);
            //return ret;
            return (int)Math.Ceiling(-Math.Log(1.0 - randomNumberGenerator.NextDouble()) * S[to]);
        }

        public void Run()
        {
            if (!IsInitialized) throw new Exception("Simulation needs to be initialized. Call Init method.\n");

            int maxNumOfSimulations = SimulationParameters.Last<int>();   // max num of repetitions
            int currentSimulation = -1;
            int currentSimulationValue = 0;


            for (int i = 0; i < maxNumOfSimulations; i++)
            {
                if (i != 0) Reset();
                while (SimulationState == States.IDLE || SimulationState == States.INPROGRESS || SimulationState == States.FINISHING)
                {
                    Tick();
                }

                if (i == currentSimulationValue)
                {
                    if (i == 0)
                    {
                        for (int k = 0; k < DeviceNumber; k++)
                        {
                            USingle[k] = U[k];
                            XSingle[k] = X[k];
                            JobsSingle[k] = Jobs[k];
                            NSingle[k] = N[k];
                        }

                        double sum = 0;
                        foreach (var tmp in Jobs)
                            sum += tmp;
                        Console.WriteLine("k: {0}\t jobs: {1}\t{2}", k, jobs, sum);
                    }
                    currentSimulation++;
                    currentSimulationValue = SimulationParameters[currentSimulation];
                }

                for (int k = 0; k < DeviceNumber; k++)
                {
                    UMulti[currentSimulation][k] += U[k];
                    XMulti[currentSimulation][k] += X[k];
                    JobsMulti[currentSimulation][k] += Jobs[k];
                    NMulti[currentSimulation][k] += N[k];
                }
            }
        }

        public void PrepareForWriting(int factor = 1, int indexOfSimulation = 0)
        {
            double max = 0;

            for (int i = 0; i < DeviceNumber; i++)
            {
                if (factor != 1)
                {
                    //Console.Write("{0}\t", indexOfSimulation);
                    UPrint[i] = UMulti[indexOfSimulation][i] / (StartingTime * factor);
                    XPrint[i] = JobsMulti[indexOfSimulation][i] / (StartingTime * factor) * 1000;
                    NPrint[i] = NMulti[indexOfSimulation][i] / (StartingTime * factor);
                    if (UPrint[i] > max)
                    {
                        max = UPrint[i];
                        BottleNeck = i;
                    }
                }
                else
                {
                    UPrint[i] = USingle[i] / StartingTime;
                    XPrint[i] = JobsSingle[i] / StartingTime * 1000;
                    NPrint[i] = NSingle[i] / StartingTime;
                    if (UPrint[i] > max)
                    {
                        max = UPrint[i];
                        BottleNeck = i;
                    }
                }
            }
            SystemResponse = (double)jobs / XPrint[0];
            IsInitialized = false;
        }

        public int CalculateNextServer(int from)
        {
            

            int to = 0;
            SystemRandomSource randomNumberGenerator = SystemRandomSource.Default;
            double randomNumber = Math.Round(randomNumberGenerator.NextDouble(), 5);
            for (; to < DeviceNumber && randomNumber > 0; to++)
                randomNumber -= P[from, to];
            if (to != 0) to--;
            return to;
        }

        public void Print()
        {
            sem.WaitOne();
            PrepareForWriting();
            using (StreamWriter File = new StreamWriter(Output, true))
            {
                Flush(File);
                File.Flush();
            }
            using (StreamWriter File = new StreamWriter(OutputMean, true))
            {
                for (int i = 0; i < SimulationParameters.Count(); i++)
                {
                    PrepareForWriting(SimulationParameters[i], i);
                    Flush(File, SimulationParameters[i]);
                    File.Flush();
                }
            }
            sem.Release();
        }

        public void SetB(Buzen B)

        {
            UBuzen = B.U.ToList<double>();
            XBuzen = B.X.ToList<double>();
            NBuzen = B.N.ToList<double>();
            SystemResponseBuzen = B.SystemResponse;
        }

        public void Compare()
        {
            sem.WaitOne();
            PrepareForWriting();
            PrepareDeviation();
            using (StreamWriter File = new StreamWriter(OutputDeviation, true))
            {
                Flush(File, -1, true);
                File.Flush();
            }
            using (StreamWriter File = new StreamWriter(OutputDeviationMean, true))
            {
                for (int i = 0; i < SimulationParameters.Count(); i++)
                {
                    PrepareForWriting(SimulationParameters[i], i);
                    PrepareDeviation();
                    Flush(File, SimulationParameters[i], true);
                    File.Flush();
                }
            }
            sem.Release();
        }
        private void PrepareDeviation()
        {
            for (int i = 0; i < DeviceNumber; i++)
            {
                UPrint[i] = Math.Abs(UPrint[i] - UBuzen[i]) / UPrint[i] * 100;
                XPrint[i] = Math.Abs(XPrint[i] - XBuzen[i]) / XPrint[i] * 100;
                NPrint[i] = Math.Abs(NPrint[i] - NBuzen[i]) / NPrint[i] * 100;
            }
            SystemResponse = Math.Abs(SystemResponse - SystemResponseBuzen) / SystemResponse * 100;
        }

        public void Flush(StreamWriter File, int SimulationParameter = -1, bool isDeviation = false)
        {
            string unit = isDeviation ? "%" : "";

            File.Write("Jobs: {0}", jobs);
            if (SimulationParameter != -1)
                File.Write("\tN: {0}", SimulationParameter);

            File.WriteLine("\n\tK: {0}", k);

            File.Write("\t\t\tCPU\tSD1\tSD2\tSD3\tUD1\tUD2");
            for (int i = 3; i <= k; i++) File.Write("\tSD{0}", i);
            File.Write("\n\t\t\t");

            for (int i = 0; i < DeviceNumber; i++) File.Write("========");
            File.WriteLine();

            File.Write("Iskoriscenje resursa\t");
            foreach (double tmp in UPrint)
                File.Write("{0:0.0000}{1}\t", tmp, (isDeviation ? unit : ""));
            File.WriteLine();

            File.Write("Protoci kroz resurse\t");
            foreach (double tmp in XPrint)
                File.Write("{0:0.0000}{1}\t", tmp, (isDeviation ? unit : ""));
            File.WriteLine();

            File.Write("Prosecan broj poslova\t");
            foreach (double tmp in NPrint)
                File.Write("{0:0.0000}{1}\t", tmp, (isDeviation ? unit : ""));
            File.WriteLine();

            File.Write("Vreme odziva sistema\t");
            File.WriteLine("{0:0.0000}{1}", SystemResponse, unit);                       // PRS10_b, slajd 22, ne znam zasto mnozim sa 100 EDIT 6\7: obrisao mnozenje sa 100

            File.Write("Kritican resurs sistema\t{0} - ", BottleNeck);
            string BottleNeckName;
            switch (BottleNeck)
            {
                case 0:
                    BottleNeckName = "CPU";
                    break;
                case 1:
                case 2:
                case 3:
                    BottleNeckName = "SD" + BottleNeck;
                    break;
                case -1:
                    BottleNeckName = "Error, critical resource is not set.";
                    break;
                default:
                    BottleNeckName = "UD" + (BottleNeck - 3).ToString();
                    break;
            }
            File.WriteLine("{0}\n", BottleNeckName);

            for (int i = 0; i < DeviceNumber + 3; i++) File.Write("********");
            File.WriteLine("\n");
        }
    }
}
