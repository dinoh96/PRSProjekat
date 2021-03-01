using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Random;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PRSProjekatJun2020
{
    class Simulation_old
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
        public enum States : int { IDLE, INPROGRESS, FINISHING, FINISHED}

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

        private List<double> XOld;
        private List<double> UOld;
        private List<double> JobsOld;
        private List<double> NOld;


        private States SimulationState;

        public List<int> S { get; }
        public StreamWriter Output { get; }
        public StreamWriter OutputMean { get; }

        private Matrix<double> P;
        private int BottleNeck;
        private List<int> SimulationParameters;

        public Simulation_old(List<int> S, List<int> SimulationParameters, string FileName, string FileNameMean, int TimeInMinutes = 24 * 60, int fixedServers = 4, int Kmin = 2, int Kmax = 8)
        {
            StartingTime = TimeInMinutes * 60 * 1000;
            
            this.S = S;
            Output = new StreamWriter(FileName);
            OutputMean = new StreamWriter(FileNameMean);
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

            XOld = new List<double>(DeviceNumber);
            UOld = new List<double>(DeviceNumber);
            JobsOld = new List<double>(DeviceNumber);
            NOld = new List<double>(DeviceNumber);

            Populate();
        }

        public void Populate()
        {

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

                XOld.Add(0);
                UOld.Add(0);
                NOld.Add(0);
                JobsOld.Add(0);

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
                    N[i] = jobs - 1;

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
                if (ETA[i] > -1) ETA[i]--;
                Lock[i] = false;
                N[i] += Queue[i];
            }

            //if (TimeInMiliSeconds >= 0)
            //{// simulacija traje
            if (TimeInMiliSeconds <= 0) SimulationState = States.FINISHING;
            else SimulationState = States.INPROGRESS;

            
            for (i = 0; i < DeviceNumber; i++)
            {
                if (ETA[i] >= 0)
                {
                    // proces se izvrsava na i-tom serveru
                    U[i]++;                                         // iskoriscenje resursa 
                }
                // proces se 
                if (ETA[i] == 0)
                {
                    // proces je zavrsio, prebaci ga na sledeci server, zakljucaj server na koji posaljes da bi mogao da preuzme posao tek u sledecem taktu
                    int to = CalculateNextServer(i);
                    Queue[to]++;
                    Lock[to] = true;
                }
                if (ETA[i] == -1 && Queue[i] > 0 && !Lock[i])
                {
                    // proces se ucitava u server i od sledeceg takta krece da se izvrsava
                    if (TimeInMiliSeconds > 0 || (TimeInMiliSeconds < 0 && i != 0))
                    {
                        ETA[i] = CalculateProcessDuration(i);
                        Queue[i]--;
                        Jobs[i]++;                                         // dodavanje novog procesa na resurs i
                    }
                }
            }

            if (SimulationState == States.FINISHING)
            {
                for (i = 0; i < DeviceNumber; i++)
                    if (ETA[i] != -1 || (i != 0 && Queue[i] != 0))
                        break;
                if (i == DeviceNumber)
                    SimulationState = States.FINISHED;
            }
        }

        private int CalculateProcessDuration(int to)
        {
            // da li treba da se mnozi sa 1000? Sada je range: 0.00005..230
            SystemRandomSource randomNumberGenerator = SystemRandomSource.Default;
            double rand = randomNumberGenerator.NextDouble();
            if ((rand = Math.Round(rand, 5)) == 1) rand = .99999;
            double randomNumber = rand;
            double x = -Math.Log(1.0 - randomNumber) * S[to];

            //Console.Write("{0}\t", (int)x);
            int ret = (x < 1.0) ? 1 : (int)x;
            return ret;
        }

        public void Run()
        {
            if (!IsInitialized) throw new Exception("Simulation needs to be initialized. Call Init method, which definition is: void Init(Matrix<double> P, int k, int jobs)\n");

            int maxNumOfSimulations = SimulationParameters.Last<int>();   // max num of repetiotions
            int currentSimulation = -1;
            int currentSimulationValue = 0;


            for (int i = 0; i < maxNumOfSimulations; i++)
            {
                if (i != 0) Reset();
                while (SimulationState == States.IDLE || SimulationState == States.INPROGRESS || SimulationState == States.FINISHING)
                {
                    Tick();
                }

                for (int k = 0; k < DeviceNumber; k++)
                {
                    UOld[k] += U[k];
                    XOld[k] += X[k];
                    JobsOld[k] += Jobs[k];
                    NOld[k] += N[k];
                }

                if (i == currentSimulationValue)
                {
                    if (i == 0)
                    {
                        PrepareForWriting();
                        Flush(Output);
                    }
                    currentSimulation++;
                    currentSimulationValue = SimulationParameters[currentSimulation];
                }
                if (i == maxNumOfSimulations - 1 || i == SimulationParameters[currentSimulation]-1)
                {
                    PrepareForWriting(currentSimulationValue);
                    Flush(OutputMean, currentSimulationValue);
                }
            }
        }

        public void PrepareForWriting(int factor = 1)
        {
            double max = 0;
           
            for (int i = 0; i < DeviceNumber; i++)
            { 
                if (factor != 1)
                {
                    UPrint[i] = UOld[i] / (StartingTime * factor);
                    XPrint[i] = JobsOld[i] / (StartingTime * factor) * 1000;
                    NPrint[i] = NOld[i] / (StartingTime * factor);
                    if (UPrint[i] > max)
                    {
                        max = UPrint[i];
                        BottleNeck = i;
                    }
                }
                else
                {
                    UPrint[i] = U[i] / StartingTime;
                    XPrint[i] = Jobs[i] / StartingTime * 1000;
                    NPrint[i] = N[i] / StartingTime;
                    if (UPrint[i] > max)
                    {
                        max = UPrint[i];
                        BottleNeck = i;
                    }
                }
            }

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

        public void Flush(StreamWriter File, int SimulationParameter = -1)
        {
            File.WriteLine("Jobs: {0}\n\tK: {1}", jobs, k);
            if (SimulationParameter != -1)
                File.WriteLine("\t\tN: {0}", SimulationParameter);
            File.Write("\t\t\tCPU\tSD1\tSD2\tSD3\tUD1\tUD2");
            for (int i = 3; i <= k; i++) File.Write("\tSD{0}", i);
            File.Write("\n\t\t\t");
            for (int i = 0; i < DeviceNumber; i++) File.Write("========");
            File.WriteLine();

            File.Write("Iskoriscenje resursa\t");
            foreach (double tmp in UPrint)
                File.Write("{0,-5}\t", Math.Round(tmp, 3));
            File.WriteLine();

            File.Write("Protoci kroz resurse\t");
            foreach (double tmp in XPrint)
                File.Write("{0,-5}\t", Math.Round(tmp, 2));
            File.WriteLine();

            File.Write("Prosecan broj poslova\t");
            foreach (double tmp in NPrint)
                File.Write("{0,-5}\t", Math.Round(tmp, 3));
            File.WriteLine();

            File.Write("Vreme odziva sistema\t");
            File.WriteLine("{0}ms", Math.Round(jobs / XPrint[0], 3));                       // PRS10_b, slajd 22, ne znam zasto mnozim sa 100 EDIT 6\7: obrisao mnozenje sa 100

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
        public void Clean()
        {
            Output.Close();
            OutputMean.Close();
        }
    }
}
